export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  errors?: Record<string, string[] | string>;
}

export function problemMessage(problem: ProblemDetails | null, fallback: string): string {
  if (!problem) return fallback;
  const validation = problem.errors
    ? Object.values(problem.errors).flatMap(value => Array.isArray(value) ? value : [value]).join(' ')
    : '';
  return validation || problem.detail || problem.title || fallback;
}

export async function httpProblemMessage(error: HttpErrorResponse): Promise<string> {
  if (error.status === 0) return 'No se pudo contactar con el servidor. Revisa tu conexión e inténtalo nuevamente.';
  const fallback = error.status === 403
    ? 'No tienes permisos para realizar esta operación.'
    : error.status === 404
      ? 'No se encontró el recurso solicitado.'
      : error.status === 412
        ? 'La información cambió en el servidor. Actualiza los datos e inténtalo nuevamente.'
        : 'Ocurrió un error inesperado. Inténtalo nuevamente.';

  if (error.error instanceof Blob) {
    try {
      const text = await error.error.text();
      return problemMessage(JSON.parse(text) as ProblemDetails, fallback);
    } catch {
      return fallback;
    }
  }
  return problemMessage(error.error && typeof error.error === 'object' ? error.error as ProblemDetails : null, fallback);
}
import { HttpErrorResponse } from '@angular/common/http';
