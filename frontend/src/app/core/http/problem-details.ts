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
