import { HttpResponse } from '@angular/common/http';

export type ReportFormat = 'pdf' | 'xlsx';

const reportTypes: Record<ReportFormat, string> = {
  pdf: 'application/pdf',
  xlsx: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
};

export interface PreparedReport {
  blob: Blob;
  fileName: string;
}

export function prepareReport(response: HttpResponse<Blob>, format: ReportFormat, fallbackBase: string): PreparedReport {
  const blob = response.body;
  if (!blob || blob.size === 0) throw new Error('El reporte recibido está vacío.');
  const mediaType = (response.headers.get('Content-Type') || blob.type).split(';')[0].trim().toLowerCase();
  if (mediaType !== reportTypes[format]) throw new Error('El servidor devolvió un formato de reporte no válido.');

  const fallback = `${slug(fallbackBase) || 'reporte'}.${format}`;
  const fileName = contentDispositionFileName(response.headers.get('Content-Disposition')) || fallback;
  return { blob, fileName: expectedExtension(safeFileName(fileName, fallback), format) };
}

export function downloadPreparedReport(report: PreparedReport): void {
  const url = URL.createObjectURL(report.blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = report.fileName;
  link.hidden = true;
  document.body.appendChild(link);
  try {
    link.click();
  } finally {
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 0);
  }
}

export function contentDispositionFileName(contentDisposition: string | null): string | null {
  if (!contentDisposition) return null;
  const encoded = /filename\*\s*=\s*([^;]+)/i.exec(contentDisposition)?.[1]?.trim().replace(/^"|"$/g, '');
  if (encoded) {
    const extendedValue = /^[^']*'[^']*'(.*)$/.exec(encoded)?.[1] ?? encoded;
    try { return decodeURIComponent(extendedValue); } catch { /* Fall through to the legacy filename. */ }
  }
  return /filename\s*=\s*"([^"]+)"/i.exec(contentDisposition)?.[1]
    ?? /filename\s*=\s*([^;]+)/i.exec(contentDisposition)?.[1]?.trim()
    ?? null;
}

function safeFileName(value: string, fallback: string): string {
  const result = value.replace(/[\\/\u0000-\u001f\u007f]/g, '-').replace(/^\.+/, '').trim();
  return result || fallback;
}

function expectedExtension(value: string, format: ReportFormat): string {
  if (value.toLowerCase().endsWith(`.${format}`)) return value;
  return `${value.replace(/\.[^.]*$/, '') || 'reporte'}.${format}`;
}

function slug(value: string): string {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[^a-z0-9]+/gi, '-').replace(/^-|-$/g, '').toLowerCase();
}
