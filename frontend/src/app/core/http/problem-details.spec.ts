import { HttpErrorResponse } from '@angular/common/http';
import { httpProblemMessage, problemMessage } from './problem-details';

describe('problemMessage', () => {
  it('prioritizes RFC9457 validation errors and flattens fields', () => {
    expect(problemMessage({ title: 'Invalid', errors: { name: ['Required'], status: 'Unknown' } }, 'Fallback')).toBe('Required Unknown');
  });

  it('parses ProblemDetails transported as a Blob', async () => {
    const error = new HttpErrorResponse({ status: 400, error: new Blob([JSON.stringify({ detail: 'Archivo inválido' })], { type: 'application/problem+json' }) });
    expect(await httpProblemMessage(error)).toBe('Archivo inválido');
  });
});
