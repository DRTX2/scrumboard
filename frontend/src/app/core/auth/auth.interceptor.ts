import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { ProblemDetails, problemMessage } from '../http/problem-details';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const messages = inject(MessageService);
  const token = auth.token();
  let headers = request.headers;
  if (token) headers = headers.set('Authorization', `Bearer ${token}`);
  if (token && request.method === 'POST' && !headers.has('Idempotency-Key')) {
    headers = headers.set('Idempotency-Key', crypto.randomUUID());
  }

  return next(request.clone({ headers })).pipe(
    catchError((error: HttpErrorResponse) => {
      const problem = error.error as ProblemDetails;
      if (error.status === 401) {
        auth.logout(true);
        messages.add({ severity: 'warn', summary: 'Sesión finalizada', detail: 'Inicia sesión nuevamente.' });
      } else if (error.status !== 0) {
        messages.add({
          severity: 'error',
          summary: error.status === 412 ? 'Datos desactualizados' : 'No se pudo completar la operación',
          detail: problemMessage(problem, error.message)
        });
      } else {
        messages.add({ severity: 'error', summary: 'Sin conexión', detail: 'No se pudo contactar con el servidor.' });
      }
      return throwError(() => error);
    })
  );
};
