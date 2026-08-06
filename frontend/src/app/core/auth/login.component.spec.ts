import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideNoopAnimations(),
        { provide: AuthService, useValue: { isAuthenticated: () => false } },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }
      ]
    });
  });

  it('accepts 256 password characters, rejects 257, and exposes the same HTML maximum', () => {
    const fixture = TestBed.createComponent(LoginComponent);
    const password = fixture.componentInstance.form.controls.password;
    fixture.detectChanges();

    password.setValue('a'.repeat(256));
    expect(password.valid).toBeTrue();
    password.setValue('a'.repeat(257));
    expect(password.hasError('maxlength')).toBeTrue();
    expect((fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('#password')?.maxLength).toBe(256);
  });
});
