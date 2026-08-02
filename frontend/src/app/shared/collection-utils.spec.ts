import { adjacentIds, filterTasks, normalizeArray } from './collection-utils';
import { BoardTask } from './models';

describe('collection utilities', () => {
  const tasks: BoardTask[] = [
    { id: '1', title: 'Login mobile', description: 'Acceso', priority: 'high', assigneeId: 'ana', columnId: 'todo' },
    { id: '2', title: 'Export report', priority: 'low', assigneeId: 'leo', columnId: 'todo' }
  ];

  it('combines text, assignee and priority task filters', () => {
    expect(filterTasks(tasks, { search: 'mobile', assigneeId: 'ana', priority: 'high' })).toEqual([tasks[0]]);
    expect(filterTasks(tasks, { search: 'mobile', assigneeId: 'leo', priority: null })).toEqual([]);
  });

  it('calculates adjacent identifiers after an optimistic move', () => {
    expect(adjacentIds(tasks, 0)).toEqual({ beforeId: null, afterId: '2' });
    expect(adjacentIds(tasks, 1)).toEqual({ beforeId: '1', afterId: null });
  });

  it('accepts direct and wrapped API collections', () => {
    expect(normalizeArray(tasks)).toBe(tasks);
    expect(normalizeArray({ data: tasks })).toBe(tasks);
  });
});
