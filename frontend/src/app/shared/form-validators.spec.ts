import { FormControl, FormGroup } from '@angular/forms';
import { dateOrder, nonWhitespace, trimOptional, trimRequired } from './form-validators';

describe('form validators', () => {
  it('rejects values containing only whitespace', () => {
    expect(nonWhitespace(new FormControl('   '))).toEqual({ whitespace: true });
    expect(nonWhitespace(new FormControl(' válido '))).toBeNull();
  });

  it('rejects a project end date before its start date', () => {
    const form = new FormGroup({ start: new FormControl('2026-08-05'), end: new FormControl('2026-08-04') }, { validators: dateOrder('start', 'end') });
    expect(form.hasError('dateOrder')).toBeTrue();
  });

  it('normalizes required and optional text before transport', () => {
    expect(trimRequired('  Proyecto  ')).toBe('Proyecto');
    expect(trimOptional('   ')).toBeUndefined();
  });
});
