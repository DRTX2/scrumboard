import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { contentDispositionFileName, prepareReport } from './report-download';

describe('report download', () => {
  it('prefers and decodes the RFC 5987 filename', () => {
    expect(contentDispositionFileName("attachment; filename=reporte.pdf; filename*=UTF-8'es'avance%20agosto.pdf"))
      .toBe('avance agosto.pdf');
  });

  it('validates MIME and sanitizes the server filename', () => {
    const response = new HttpResponse({
      body: new Blob(['pdf'], { type: 'application/pdf' }),
      headers: new HttpHeaders({ 'Content-Disposition': 'attachment; filename="../reporte.pdf"' })
    });
    expect(prepareReport(response, 'pdf', 'Proyecto').fileName).toBe('-reporte.pdf');
  });

  it('rejects content with an unexpected MIME type', () => {
    const response = new HttpResponse({ body: new Blob(['error'], { type: 'application/problem+json' }) });
    expect(() => prepareReport(response, 'pdf', 'Proyecto')).toThrowError('El servidor devolvió un formato de reporte no válido.');
  });

  it('uses the fallback filename when Content-Disposition is not exposed cross-origin', () => {
    const response = new HttpResponse({ body: new Blob(['xlsx'], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }) });
    expect(prepareReport(response, 'xlsx', 'Avance Agosto').fileName).toBe('avance-agosto.xlsx');
  });

  it('keeps an exposed filename but enforces the MIME-matched extension', () => {
    const response = new HttpResponse({
      body: new Blob(['pdf'], { type: 'application/pdf' }),
      headers: new HttpHeaders({ 'Content-Disposition': 'attachment; filename="avance.exe"' })
    });
    expect(prepareReport(response, 'pdf', 'Proyecto').fileName).toBe('avance.pdf');
  });
});
