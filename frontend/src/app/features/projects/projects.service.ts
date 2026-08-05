import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { RuntimeConfigService } from '../../core/config/runtime-config.service';
import { PageResult, Project } from '../../shared/models';

export interface ProjectQuery { page: number; pageSize: number; search: string; sort: string; direction: 'asc' | 'desc'; }
export type ProjectInput = Required<Pick<Project, 'name' | 'status' | 'startDate' | 'expectedEndDate'>> & Pick<Project, 'description'>;

@Injectable({ providedIn: 'root' })
export class ProjectsService {
  constructor(private readonly http: HttpClient, private readonly config: RuntimeConfigService) {}

  list(query: ProjectQuery): Observable<PageResult<Project>> {
    const params = new HttpParams()
      .set('page', query.page).set('pageSize', query.pageSize)
      .set('search', query.search).set('sort', query.sort).set('direction', query.direction);
    return this.http.get<unknown>(this.config.endpoint('projects'), { params, observe: 'response' }).pipe(
      map(response => {
        const body = response.body as Project[] | { items?: Project[]; data?: Project[]; results?: Project[]; total?: number; totalCount?: number } | null;
        const items = Array.isArray(body) ? body : body?.items ?? body?.data ?? body?.results ?? [];
        return {
          items,
          total: Number(response.headers.get('X-Total-Count') ?? (!Array.isArray(body) && (body?.total ?? body?.totalCount)) ?? items.length),
          page: query.page,
          pageSize: query.pageSize
        };
      })
    );
  }

  create(input: ProjectInput, idempotencyKey: string): Observable<Project> {
    return this.http.post<Project>(this.config.endpoint('projects'), input, {
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }), observe: 'response'
    }).pipe(map(response => this.entity(response)));
  }

  update(project: Project, input: ProjectInput): Observable<Project> {
    return this.http.put<Project>(this.config.endpoint('project', { projectId: project.id }), input, {
      headers: this.ifMatch(project.etag), observe: 'response'
    }).pipe(map(response => this.entity(response)));
  }

  delete(project: Project): Observable<void> {
    return this.http.delete<void>(this.config.endpoint('project', { projectId: project.id }), { headers: this.ifMatch(project.etag) });
  }

  private ifMatch(etag?: string): HttpHeaders {
    return etag ? new HttpHeaders({ 'If-Match': etag }) : new HttpHeaders();
  }

  private entity(response: HttpResponse<Project>): Project {
    const body = response.body as Project & { data?: Project };
    const entity = body?.data ?? body;
    return { ...entity, etag: response.headers.get('ETag') ?? entity.etag };
  }
}
