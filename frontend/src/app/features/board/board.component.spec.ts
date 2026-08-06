import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { HttpErrorResponse } from '@angular/common/http';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { BehaviorSubject, Subject, of, throwError } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { BoardEvent, BoardRealtimeService, RealtimeState } from '../../core/realtime/board-realtime.service';
import { AuthService } from '../../core/auth/auth.service';
import { Board, BoardColumn, BoardTask, CursorPageResult } from '../../shared/models';
import { BoardComponent } from './board.component';
import { BoardService } from './board.service';

describe('BoardComponent', () => {
  let boardService: jasmine.SpyObj<BoardService>;
  let realtimeEvents: Subject<BoardEvent>;
  let component: BoardComponent;

  beforeEach(() => {
    boardService = jasmine.createSpyObj<BoardService>('BoardService', ['load', 'loadColumnTasks', 'moveTask']);
    realtimeEvents = new Subject<BoardEvent>();
    const realtime = {
      events$: realtimeEvents.asObservable(),
      reconnected$: new Subject<void>().asObservable(),
      resubscribing$: new Subject<void>().asObservable(),
      state$: new BehaviorSubject<RealtimeState>('connected').asObservable(),
      connect: () => Promise.resolve(),
      stop: () => Promise.resolve()
    };
    TestBed.configureTestingModule({ providers: [
      FormBuilder,
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'project-1' }) } } },
      { provide: BoardService, useValue: boardService },
      { provide: BoardRealtimeService, useValue: realtime },
      { provide: MessageService, useValue: { add: jasmine.createSpy('add') } },
      { provide: ConfirmationService, useValue: { confirm: jasmine.createSpy('confirm') } },
      { provide: AuthService, useValue: { user: () => ({ id: 'owner-id', name: 'Owner' }) } }
    ] });
    component = TestBed.runInInjectionContext(() => new BoardComponent(
      TestBed.inject(ActivatedRoute),
      TestBed.inject(FormBuilder),
      TestBed.inject(BoardService),
      TestBed.inject(BoardRealtimeService),
      TestBed.inject(MessageService),
      TestBed.inject(ConfirmationService),
      TestBed.inject(AuthService)
    ));
  });

  it('refreshes 60 loaded tasks through a capped snapshot and continuation replay', fakeAsync(() => {
    component.columns = [column('todo', tasks('todo', 1, 60), 70, true)];
    boardService.load.and.returnValue(of(board(column('todo', tasks('todo', 1, 50), 70, true), '"2"')));
    boardService.loadColumnTasks.and.callFake((_projectId, _columnId, _lastTask, _filters, _etag, limit) =>
      of({ items: tasks('todo', 51, 10), total: 70, hasMore: true, etag: '"2"' } as CursorPageResult<BoardTask>));

    realtimeEvents.next({ name: 'TaskUpdated', payload: {} });
    tick(101);

    expect(boardService.load).toHaveBeenCalledWith('project-1', jasmine.any(Object), 50);
    expect(boardService.loadColumnTasks).toHaveBeenCalledWith(
      'project-1', 'todo', jasmine.objectContaining({ id: 'todo-50' }), jasmine.any(Object), '"2"', 10
    );
    expect(boardService.load.calls.allArgs().every(args => (args[2] ?? 20) <= 50)).toBeTrue();
    expect(boardService.loadColumnTasks.calls.allArgs().every(args => (args[5] ?? 20) <= 50)).toBeTrue();
    expect(component.columns[0].tasks.length).toBe(60);
    expect(component.loading).toBeFalse();
  }));

  it('restores the optimistic task snapshot before a 412 reload completes', () => {
    const reload = new Subject<Board>();
    boardService.load.and.returnValue(reload);
    boardService.moveTask.and.returnValue(throwError(() => new HttpErrorResponse({ status: 412 })));
    const source = column('todo', tasks('todo', 1, 2), 2, false);
    const target = column('done', tasks('done', 1, 1), 1, false);
    component.columns = [source, target];

    component.dropTask(dropEvent(source, target));

    expect(component.columns[0].tasks.map(task => task.id)).toEqual(['todo-1', 'todo-2']);
    expect(component.columns[1].tasks.map(task => task.id)).toEqual(['done-1']);
    expect(component.columns[0].tasks[0].columnId).toBe('todo');
    expect(boardService.load).toHaveBeenCalled();
  });

  it('keeps the rejected ordering rolled back when the 412 reload fails', () => {
    boardService.load.and.returnValue(throwError(() => new HttpErrorResponse({ status: 503 })));
    boardService.moveTask.and.returnValue(throwError(() => new HttpErrorResponse({ status: 412 })));
    const source = column('todo', tasks('todo', 1, 2), 2, false);
    const target = column('done', tasks('done', 1, 1), 1, false);
    component.columns = [source, target];

    component.dropTask(dropEvent(source, target));

    expect(component.columns[0].tasks.map(task => task.id)).toEqual(['todo-1', 'todo-2']);
    expect(component.columns[1].tasks.map(task => task.id)).toEqual(['done-1']);
    expect(component.loadError).toContain('No se pudo cargar el tablero');
  });

  it('allows column administration only for the current project owner', () => {
    component.members = [
      { id: 'owner-id', name: 'Owner', role: 'owner' },
      { id: 'member-id', name: 'Member', role: 'member' }
    ];

    expect(component.canManageColumns).toBeTrue();
    component.members[0].role = 'member';
    expect(component.canManageColumns).toBeFalse();
  });
});

function board(boardColumn: BoardColumn, etag: string): Board {
  return { project: { id: 'project-1', name: 'Project' }, columns: [boardColumn], members: [], etag };
}

function column(id: string, taskItems: BoardTask[], taskTotal: number, hasMoreTasks: boolean): BoardColumn {
  return { id, name: id, tasks: taskItems, taskTotal, hasMoreTasks };
}

function tasks(columnId: string, start: number, count: number): BoardTask[] {
  return Array.from({ length: count }, (_, index) => {
    const position = start + index;
    return { id: `${columnId}-${position}`, title: `Task ${position}`, priority: 'medium', columnId, position };
  });
}

function dropEvent(source: BoardColumn, target: BoardColumn): CdkDragDrop<BoardTask[]> {
  return {
    previousContainer: { data: source.tasks },
    container: { data: target.tasks },
    previousIndex: 0,
    currentIndex: 1
  } as CdkDragDrop<BoardTask[]>;
}
