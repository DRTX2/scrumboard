export interface User { id: string; name: string; email?: string; avatarUrl?: string; }
export interface EntityMeta { etag?: string; boardEtag?: string; }
export interface Project extends EntityMeta {
  id: string;
  name: string;
  description?: string;
  status?: string;
  startDate?: string;
  expectedEndDate?: string;
  updatedAt?: string;
}
export type TaskPriority = 'low' | 'medium' | 'high' | 'critical';
export interface BoardTask extends EntityMeta {
  id: string;
  title: string;
  description?: string;
  priority: TaskPriority;
  assigneeId?: string | null;
  assignee?: User | null;
  dueDate?: string | null;
  position?: number;
  columnId: string;
}
export interface BoardColumn extends EntityMeta {
  id: string;
  name: string;
  position?: number;
  tasks: BoardTask[];
}
export interface Board extends EntityMeta { project: Project; columns: BoardColumn[]; members: User[]; }
export interface PageResult<T> { items: T[]; total: number; page: number; pageSize: number; }
export interface TaskFilters { search: string; assigneeId: string | null; priority: TaskPriority | null; }
