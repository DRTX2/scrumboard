import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { RuntimeConfigService } from '../config/runtime-config.service';
import { AuthService, safeInternalReturnUrl } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        AuthService,
        { provide: RuntimeConfigService, useValue: { endpoint: () => '/sessions' } },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } }
      ]
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => { http.verify(); sessionStorage.clear(); });

  it('stores a successful valid JWT session only in sessionStorage', () => {
    const token = jwt({ sub: 'u1', exp: Math.floor(Date.now() / 1000) + 60 });
    service.login('ana@example.com', 'secret').subscribe();
    http.expectOne('/sessions').flush({ accessToken: token, user: { id: 'u1', name: 'Ana' } });
    expect(service.token()).toBe(token);
    expect(localStorage.getItem('scrumboard.session')).toBeNull();
    expect(service.user()?.name).toBe('Ana');
  });

  it('rejects malformed, subjectless, and expired tokens and clears the session', () => {
    for (const token of ['not-a-jwt', jwt({ exp: Math.floor(Date.now() / 1000) + 60 }), jwt({ sub: 'u1', exp: String(Math.floor(Date.now() / 1000) + 60) }), jwt({ sub: 'u1', exp: Math.floor(Date.now() / 1000) - 1 })]) {
      sessionStorage.setItem('scrumboard.session', JSON.stringify({ token, user: { id: 'u1', name: 'Ana' } }));
      expect(service.token()).toBeNull();
      expect(sessionStorage.getItem('scrumboard.session')).toBeNull();
    }
  });

  it('derives the user identity from the token when response metadata disagrees', () => {
    const token = jwt({ sub: 'trusted-id', name: 'Token User', exp: Math.floor(Date.now() / 1000) + 60 });
    service.login('ana@example.com', 'secret').subscribe();
    http.expectOne('/sessions').flush({ accessToken: token, user: { id: 'other-id', name: 'Injected Owner' } });

    expect(service.user()).toEqual(jasmine.objectContaining({ id: 'trusted-id', name: 'Token User' }));
  });

  it('allows only safe internal return URLs', () => {
    expect(safeInternalReturnUrl('/projects/1/board?tab=1')).toBe('/projects/1/board?tab=1');
    expect(safeInternalReturnUrl('//evil.example/path')).toBe('/projects');
    expect(safeInternalReturnUrl('https://evil.example')).toBe('/projects');
    expect(safeInternalReturnUrl('/login')).toBe('/projects');
  });
});

function jwt(payload: Record<string, unknown>): string {
  const encoded = btoa(JSON.stringify(payload)).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `header.${encoded}.signature`;
}
