import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MessageService } from 'primeng/api';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  const auth = { token: jasmine.createSpy('token'), logout: jasmine.createSpy('logout') };
  const messages = { add: jasmine.createSpy('add') };
  let client: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    auth.token.and.returnValue('jwt-token');
    auth.logout.calls.reset();
    messages.add.calls.reset();
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
      { provide: AuthService, useValue: auth },
      { provide: MessageService, useValue: messages }
    ] });
    client = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('adds Bearer and a UUID idempotency key to POST', () => {
    client.post('/projects', {}).subscribe();
    const request = controller.expectOne('/projects');
    expect(request.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    expect(request.request.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/i);
    request.flush({});
  });

  it('clears the session when the server returns 401', () => {
    client.get('/secure').subscribe({ error: () => undefined });
    controller.expectOne('/secure').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.logout).toHaveBeenCalledWith(true);
    expect(messages.add).toHaveBeenCalled();
  });
});
