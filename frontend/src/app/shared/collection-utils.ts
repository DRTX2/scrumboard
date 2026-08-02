import { BoardTask, TaskFilters } from './models';

export function filterTasks(tasks: BoardTask[], filters: TaskFilters): BoardTask[] {
  const search = filters.search.trim().toLocaleLowerCase();
  return tasks.filter(task =>
    (!search || `${task.title} ${task.description ?? ''}`.toLocaleLowerCase().includes(search)) &&
    (!filters.assigneeId || task.assigneeId === filters.assigneeId || task.assignee?.id === filters.assigneeId) &&
    (!filters.priority || task.priority === filters.priority)
  );
}

export function adjacentIds<T extends { id: string }>(items: T[], index: number): { beforeId: string | null; afterId: string | null } {
  return {
    beforeId: index > 0 ? items[index - 1].id : null,
    afterId: index < items.length - 1 ? items[index + 1].id : null
  };
}

export function normalizeArray<T>(response: T[] | { items?: T[]; data?: T[] }): T[] {
  if (Array.isArray(response)) return response;
  return response.items ?? response.data ?? [];
}
