import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RuntimeConfigService } from '../../core/config/runtime-config.service';
import { BoardTask, TaskFilters } from '../../shared/models';
import { BoardService } from './board.service';

describe('BoardService', () => {
  const filters: TaskFilters = { search: '  login  ', assigneeId: 'user-1', priority: 'high' };
  let service: BoardService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const config = TestBed.inject(RuntimeConfigService);
    config.setForTesting({
      apiBaseUrl: '/api',
      hubUrl: '/hubs/boards',
      endpoints: {
        board: '/v1/projects/{projectId}/board',
        columnTasks: '/v1/projects/{projectId}/columns/{columnId}/tasks'
      }
    });
    service = TestBed.inject(BoardService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the first 20 filtered tasks for every board column', () => {
    let taskTotal: number | undefined;
    service.load('project 1', filters).subscribe(board => taskTotal = board.columns[0].taskTotal);

    const boardRequest = http.expectOne(request => request.url === '/api/v1/projects/project%201/board');
    expect(boardRequest.request.params.get('taskLimit')).toBe('20');
    expect(boardRequest.request.params.get('search')).toBe('login');
    expect(boardRequest.request.params.get('assigneeId')).toBe('user-1');
    expect(boardRequest.request.params.get('priority')).toBe('high');
    boardRequest.flush({
      project: { id: 'project 1', name: 'Project' },
      members: [],
      columns: [{ id: 'todo', name: 'To do', tasks: [], taskTotal: 37, hasMoreTasks: true }]
    });
    expect(taskTotal).toBe(37);
  });

  it('caps board snapshots at the API maximum of 50 tasks', () => {
    service.load('project 1', filters, 60).subscribe();

    const request = http.expectOne(item => item.url === '/api/v1/projects/project%201/board');
    expect(request.request.params.get('taskLimit')).toBe('50');
    request.flush({ project: { id: 'project 1', name: 'Project' }, members: [], columns: [] });
  });

  it('loads a filtered continuation page using the final task cursor', () => {
    const lastTask: BoardTask = {
      id: 'task/20', title: 'Last task', priority: 'high', columnId: 'todo', position: 2048
    };

    service.loadColumnTasks('project 1', 'to do', lastTask, filters, '"7"').subscribe();

    const request = http.expectOne(item => item.url === '/api/v1/projects/project%201/columns/to%20do/tasks');
    expect(request.request.params.get('limit')).toBe('20');
    expect(request.request.params.get('afterPosition')).toBe('2048');
    expect(request.request.params.get('afterTaskId')).toBe('task/20');
    expect(request.request.params.get('search')).toBe('login');
    expect(request.request.params.get('assigneeId')).toBe('user-1');
    expect(request.request.params.get('priority')).toBe('high');
    expect(request.request.headers.get('If-Match')).toBe('"7"');
    request.flush({ items: [], total: 20, hasMore: false, etag: 'column-etag' });
  });

  it('bounds a requested continuation page to 20 tasks', () => {
    const lastTask: BoardTask = { id: 'task-50', title: 'Last task', priority: 'high', columnId: 'todo', position: 50 };
    service.loadColumnTasks('project 1', 'todo', lastTask, filters, '"7"', 40).subscribe();

    const request = http.expectOne(item => item.url === '/api/v1/projects/project%201/columns/todo/tasks');
    expect(request.request.params.get('limit')).toBe('20');
    request.flush({ items: [], total: 50, hasMore: false, etag: '"7"' });
  });
});
