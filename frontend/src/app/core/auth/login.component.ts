import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { AuthService } from './auth.service';
import { safeInternalReturnUrl } from './auth.service';
import { HttpErrorResponse } from '@angular/common/http';
import { httpProblemMessage } from '../http/problem-details';
import { nonWhitespace, trimRequired } from '../../shared/form-validators';

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, PasswordModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  loading = false;
  errorMessage = '';
  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, nonWhitespace, Validators.email, Validators.maxLength(254)]],
    password: ['', [Validators.required, Validators.maxLength(256)]]
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly auth: AuthService,
    private readonly router: Router,
    private readonly route: ActivatedRoute
  ) {
    if (auth.isAuthenticated()) void router.navigate(['/projects']);
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading = true;
    this.errorMessage = '';
    const { email, password } = this.form.getRawValue();
    this.auth.login(trimRequired(email), password).pipe(finalize(() => this.loading = false)).subscribe({
      next: () => void this.router.navigateByUrl(safeInternalReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'))),
      error: (error: unknown) => {
        if (error instanceof HttpErrorResponse) {
          void httpProblemMessage(error).then(message => this.errorMessage = error.status === 401 ? 'Correo o contraseña incorrectos.' : message);
        } else {
          this.errorMessage = error instanceof Error ? error.message : 'No se pudo iniciar sesión. Inténtalo nuevamente.';
        }
      }
    });
  }
}
