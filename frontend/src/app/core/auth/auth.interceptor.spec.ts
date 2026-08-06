import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { RuntimeConfigService } from '../config/runtime-config.service';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  const auth = { token: jasmine.createSpy('token'), expireSession: jasmine.createSpy('expireSession') };
  const messages = { add: jasmine.createSpy('add') };
  let client: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    auth.token.and.returnValue('jwt-token');
    auth.expireSession.and.returnValue(true);
    auth.expireSession.calls.reset();
    messages.add.calls.reset();
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
      { provide: AuthService, useValue: auth },
      { provide: MessageService, useValue: messages },
      { provide: Router, useValue: { url: '/projects/1/board' } }
    ] });
    TestBed.inject(RuntimeConfigService).setForTesting({ apiBaseUrl: '/api', hubUrl: '/hub', endpoints: { sessions: '/v1/sessions' } });
    client = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('adds Bearer and a UUID idempotency key only to API POST requests', () => {
    client.post('/api/v1/projects', {}).subscribe();
    const request = controller.expectOne('/api/v1/projects');
    expect(request.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    expect(request.request.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/i);
    request.flush({});
  });

  it('does not intercept login or asset requests', () => {
    client.post('/api/v1/sessions', {}).subscribe();
    client.get('/assets/app-config.json').subscribe();
    const login = controller.expectOne('/api/v1/sessions');
    const asset = controller.expectOne('/assets/app-config.json');
    expect(login.request.headers.has('Authorization')).toBeFalse();
    expect(asset.request.headers.has('Authorization')).toBeFalse();
    login.flush({});
    asset.flush({});
  });

  it('handles concurrent 401 responses idempotently through AuthService', () => {
    auth.expireSession.and.returnValues(true, false);
    client.get('/api/one').subscribe({ error: () => undefined });
    client.get('/api/two').subscribe({ error: () => undefined });
    controller.expectOne('/api/one').flush({}, { status: 401, statusText: 'Unauthorized' });
    controller.expectOne('/api/two').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.expireSession).toHaveBeenCalledTimes(2);
    expect(messages.add).toHaveBeenCalledTimes(1);
  });

  it('does not expire a newer session when an old request returns 401 late', () => {
    auth.token.and.returnValues('old-token', 'new-token');
    client.get('/api/slow').subscribe({ error: () => undefined });

    controller.expectOne('/api/slow').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(auth.expireSession).not.toHaveBeenCalled();
  });

  it('reads ProblemDetails from Blob API errors in Spanish handling flow', done => {
    client.get('/api/report', { responseType: 'blob' }).subscribe({ error: () => {
      expect(messages.add).toHaveBeenCalledWith(jasmine.objectContaining({ detail: 'Reporte inválido' }));
      done();
    } });
    controller.expectOne('/api/report').flush(new Blob([JSON.stringify({ detail: 'Reporte inválido' })], { type: 'application/problem+json' }), { status: 400, statusText: 'Bad Request' });
  });
});
