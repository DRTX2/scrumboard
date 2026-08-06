import { adjacentIds, normalizeArray } from './collection-utils';
import { BoardTask } from './models';

describe('collection utilities', () => {
  const tasks: BoardTask[] = [
    { id: '1', title: 'Login mobile', description: 'Acceso', priority: 'high', assigneeId: 'ana', columnId: 'todo', position: 1 },
    { id: '2', title: 'Export report', priority: 'low', assigneeId: 'leo', columnId: 'todo', position: 2 }
  ];

  it('calculates adjacent identifiers after an optimistic move', () => {
    expect(adjacentIds(tasks, 0)).toEqual({ beforeId: '2', afterId: null });
    expect(adjacentIds(tasks, 1)).toEqual({ beforeId: null, afterId: '1' });
  });

  it('accepts direct and wrapped API collections', () => {
    expect(normalizeArray(tasks)).toBe(tasks);
    expect(normalizeArray({ data: tasks })).toBe(tasks);
  });
});
