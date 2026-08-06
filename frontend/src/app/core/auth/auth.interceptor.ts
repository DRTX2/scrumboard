import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { catchError, from, mergeMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { httpProblemMessage } from '../http/problem-details';
import { RuntimeConfigService } from '../config/runtime-config.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const messages = inject(MessageService);
  const config = inject(RuntimeConfigService);
  const router = inject(Router);
  if (!config.isApiUrl(request.url) || config.isEndpointUrl('sessions', request.url)) return next(request);

  const token = auth.token();
  let headers = request.headers;
  if (token) headers = headers.set('Authorization', `Bearer ${token}`);
  if (token && request.method === 'POST' && !headers.has('Idempotency-Key')) {
    headers = headers.set('Idempotency-Key', crypto.randomUUID());
  }

  return next(request.clone({ headers })).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        if (auth.token() === token && auth.expireSession(router.url)) {
          messages.add({ severity: 'warn', summary: 'Sesión finalizada', detail: 'Inicia sesión nuevamente para continuar.' });
        }
        return throwError(() => error);
      }
      return from(httpProblemMessage(error)).pipe(mergeMap(detail => {
        messages.add({
          severity: 'error',
          summary: error.status === 0 ? 'Sin conexión' : error.status === 412 ? 'Datos desactualizados' : 'No se pudo completar la operación',
          detail
        });
        return throwError(() => error);
      }));
    })
  );
};
