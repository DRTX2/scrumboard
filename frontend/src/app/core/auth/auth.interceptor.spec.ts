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

  it('preserves a caller-provided idempotency key', () => {
    client.post('/projects/intent', {}, { headers: { 'Idempotency-Key': 'business-intent' } }).subscribe();
    const request = controller.expectOne('/projects/intent');
    expect(request.request.headers.get('Idempotency-Key')).toBe('business-intent');
    request.flush({});
  });

  it('uses different keys for concurrent identical requests', () => {
    client.post('/projects/concurrent', { name: 'Project' }).subscribe();
    client.post('/projects/concurrent', { name: 'Project' }).subscribe();
    const requests = controller.match('/projects/concurrent');

    expect(requests).toHaveSize(2);
    expect(requests[0].request.headers.get('Idempotency-Key'))
      .not.toBe(requests[1].request.headers.get('Idempotency-Key'));
    requests.forEach(request => request.flush({}));
  });

  it('does not retain or fingerprint an unauthenticated login body', () => {
    auth.token.and.returnValue(null);
    client.post('/sessions', { email: 'owner@example.com', password: 'secret' }).subscribe();
    const request = controller.expectOne('/sessions');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    expect(request.request.headers.has('Idempotency-Key')).toBeFalse();
    request.flush({});
  });
});
