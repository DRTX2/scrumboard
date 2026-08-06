import { EntityMeta, User } from './common.models';
import { Project } from './project.models';

export type TaskPriority = 'low' | 'medium' | 'high' | 'critical';
export interface BoardTask extends EntityMeta {
  id: string;
  title: string;
  description?: string;
  priority: TaskPriority;
  assigneeId?: string | null;
  assignee?: User | null;
  dueDate?: string | null;
  position: number;
  columnId: string;
}
export interface BoardColumn extends EntityMeta {
  id: string;
  name: string;
  position?: number;
  tasks: BoardTask[];
  taskTotal: number;
  hasMoreTasks: boolean;
}
export interface Board extends EntityMeta { project: Project; columns: BoardColumn[]; members: User[]; }
export interface TaskFilters { search: string; assigneeId: string | null; priority: TaskPriority | null; }
