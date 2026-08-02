import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { RuntimeConfigService } from '../config/runtime-config.service';
import { AuthService } from './auth.service';

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

  it('stores a successful JWT session only in sessionStorage', () => {
    service.login('ana@example.com', 'secret').subscribe();
    http.expectOne('/sessions').flush({ accessToken: 'header.payload.signature', user: { id: 'u1', name: 'Ana' } });
    expect(service.token()).toBe('header.payload.signature');
    expect(localStorage.length).toBe(0);
    expect(service.user()?.name).toBe('Ana');
  });

  it('clears token and user on logout', () => {
    sessionStorage.setItem('scrumboard.session', JSON.stringify({ token: 'token', user: { id: '1', name: 'A' } }));
    service.logout(false);
    expect(service.token()).toBeNull();
    expect(service.user()).toBeNull();
  });
});
