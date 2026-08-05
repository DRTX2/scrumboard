import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, forkJoin, map, Observable, of } from 'rxjs';
import { RuntimeConfigService } from '../../core/config/runtime-config.service';
import { Board, BoardColumn, BoardTask, TaskFilters, User } from '../../shared/models';
import { normalizeArray } from '../../shared/collection-utils';

export type ColumnInput = Pick<BoardColumn, 'name'>;
export type TaskInput = Pick<BoardTask, 'title' | 'description' | 'priority' | 'assigneeId' | 'dueDate' | 'columnId'>;

@Injectable({ providedIn: 'root' })
export class BoardService {
  constructor(private readonly http: HttpClient, private readonly config: RuntimeConfigService) {}

  load(projectId: string): Observable<Board> {
    const board$ = this.http.get<unknown>(this.config.endpoint('board', { projectId })).pipe(map(body => this.normalizeBoard(body)));
    const members$ = this.http.get<User[] | { items?: User[]; data?: User[] }>(this.config.endpoint('members', { projectId })).pipe(
      map(normalizeArray), catchError(() => of([] as User[]))
    );
    return forkJoin({ board: board$, members: members$ }).pipe(map(({ board, members }) => ({ ...board, members: board.members.length ? board.members : members })));
  }

  createColumn(projectId: string, input: ColumnInput, idempotencyKey: string): Observable<BoardColumn> {
    return this.http.post<BoardColumn>(this.config.endpoint('columns', { projectId }), input, {
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }), observe: 'response'
    }).pipe(map(response => this.entity(response)));
  }

  updateColumn(projectId: string, column: BoardColumn, input: ColumnInput): Observable<BoardColumn> {
    return this.http.put<BoardColumn>(this.config.endpoint('column', { projectId, columnId: column.id }), input, { headers: this.ifMatch(column.etag), observe: 'response' }).pipe(map(response => this.entity(response)));
  }

  deleteColumn(projectId: string, column: BoardColumn): Observable<void> {
    return this.http.delete<void>(this.config.endpoint('column', { projectId, columnId: column.id }), { headers: this.ifMatch(column.etag) });
  }

  moveColumn(projectId: string, column: BoardColumn, boardEtag: string | undefined, beforeColumnId: string | null, afterColumnId: string | null): Observable<BoardColumn> {
    return this.http.patch<BoardColumn>(this.config.endpoint('column', { projectId, columnId: column.id }), { beforeColumnId, afterColumnId }, { headers: this.ifMatch(boardEtag), observe: 'response' }).pipe(map(response => this.entity(response)));
  }

  createTask(projectId: string, input: TaskInput, idempotencyKey: string): Observable<BoardTask> {
    return this.http.post<BoardTask>(this.config.endpoint('tasks', { projectId }), input, {
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }), observe: 'response'
    }).pipe(map(response => this.entity(response)));
  }

  updateTask(projectId: string, task: BoardTask, input: TaskInput): Observable<BoardTask> {
    return this.http.put<BoardTask>(this.config.endpoint('task', { projectId, taskId: task.id }), input, { headers: this.ifMatch(task.etag), observe: 'response' }).pipe(map(response => this.entity(response)));
  }

  deleteTask(projectId: string, task: BoardTask): Observable<void> {
    return this.http.delete<void>(this.config.endpoint('task', { projectId, taskId: task.id }), { headers: this.ifMatch(task.etag) });
  }

  moveTask(projectId: string, task: BoardTask, boardEtag: string | undefined, columnId: string, beforeTaskId: string | null, afterTaskId: string | null): Observable<BoardTask> {
    return this.http.patch<BoardTask>(this.config.endpoint('task', { projectId, taskId: task.id }), { columnId, beforeTaskId, afterTaskId }, { headers: this.ifMatch(boardEtag), observe: 'response' }).pipe(map(response => this.entity(response)));
  }

  report(projectId: string, format: 'pdf' | 'xlsx', filters: TaskFilters): Observable<HttpResponse<Blob>> {
    let params = new HttpParams().set('format', format);
    const search = filters.search.trim();
    if (search) params = params.set('search', search);
    if (filters.assigneeId) params = params.set('assigneeId', filters.assigneeId);
    if (filters.priority) params = params.set('priority', filters.priority);
    return this.http.get(this.config.endpoint('reports', { projectId }), { params, responseType: 'blob', observe: 'response' });
  }

  private normalizeBoard(response: unknown): Board {
    const wrapped = response as { data?: unknown };
    const raw = (wrapped?.data ?? response) as Partial<Board> & { projectId?: string; name?: string };
    const columns = normalizeArray((raw.columns ?? []) as BoardColumn[]).map(column => ({ ...column, tasks: normalizeArray(column.tasks ?? []) }));
    return {
      project: raw.project ?? { id: raw.projectId ?? '', name: raw.name ?? 'Tablero' },
      columns,
      members: normalizeArray(raw.members ?? []),
      etag: raw.etag ?? raw.project?.etag
    };
  }

  private ifMatch(etag?: string): HttpHeaders { return etag ? new HttpHeaders({ 'If-Match': etag }) : new HttpHeaders(); }
  private entity<T extends { etag?: string; boardEtag?: string }>(response: HttpResponse<T>): T {
    const body = response.body as T & { data?: T };
    const entity = body?.data ?? body;
    return {
      ...entity,
      etag: response.headers.get('ETag') ?? entity.etag,
      boardEtag: response.headers.get('X-Board-ETag') ?? entity.boardEtag
    };
  }
}
