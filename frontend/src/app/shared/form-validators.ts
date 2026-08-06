import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export const nonWhitespace: ValidatorFn = (control: AbstractControl): ValidationErrors | null =>
  typeof control.value === 'string' && control.value.trim().length === 0 ? { whitespace: true } : null;

export function dateOrder(startControl: string, endControl: string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const start = control.get(startControl)?.value;
    const end = control.get(endControl)?.value;
    return start && end && end < start ? { dateOrder: true } : null;
  };
}

export function trimRequired(value: string): string {
  return value.trim();
}

export function trimOptional(value: string): string | undefined {
  return value.trim() || undefined;
}
