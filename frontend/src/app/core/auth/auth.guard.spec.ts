import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { authGuard } from './auth.guard';

describe('authGuard', () => {
  const auth = { isAuthenticated: jasmine.createSpy('isAuthenticated') };
  const urlTree = { redirected: true };
  const router = { createUrlTree: jasmine.createSpy('createUrlTree').and.returnValue(urlTree) };

  beforeEach(() => TestBed.configureTestingModule({ providers: [
    { provide: AuthService, useValue: auth }, { provide: Router, useValue: router }
  ] }));

  it('allows an authenticated session', () => {
    auth.isAuthenticated.and.returnValue(true);
    expect(TestBed.runInInjectionContext(() => authGuard({} as never, { url: '/projects' } as never))).toBeTrue();
  });

  it('returns a login UrlTree preserving the requested URL', () => {
    auth.isAuthenticated.and.returnValue(false);
    expect(TestBed.runInInjectionContext(() => authGuard({} as never, { url: '/projects/1' } as never))).toBe(urlTree as never);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/projects/1' } });
  });
});
