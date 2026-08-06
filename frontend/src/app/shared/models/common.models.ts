export interface EntityMeta { etag?: string; boardEtag?: string; }
export type ProjectRole = 'owner' | 'member';
export interface User { id: string; name: string; email?: string; avatarUrl?: string; role?: ProjectRole; }
export interface PageResult<T> { items: T[]; total: number; page: number; pageSize: number; }
export interface CursorPageResult<T> { items: T[]; total: number; hasMore: boolean; etag?: string; }
