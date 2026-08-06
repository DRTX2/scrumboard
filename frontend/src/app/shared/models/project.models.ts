import { EntityMeta, ProjectRole } from './common.models';

export interface Project extends EntityMeta {
  id: string;
  name: string;
  description?: string;
  status?: string;
  startDate?: string;
  expectedEndDate?: string;
  updatedAt?: string;
  role?: ProjectRole;
}
