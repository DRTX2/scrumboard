import { problemMessage } from './problem-details';

describe('problemMessage', () => {
  it('prioritizes RFC9457 validation errors and flattens fields', () => {
    expect(problemMessage({ title: 'Invalid', errors: { name: ['Required'], status: 'Unknown' } }, 'Fallback')).toBe('Required Unknown');
  });
});
