import { CdkDragDrop, DragDropModule, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnDestroy, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, Subject, Subscription, catchError, concatMap, debounceTime, distinctUntilChanged, endWith, finalize, from, ignoreElements, of, switchMap } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AvatarModule } from 'primeng/avatar';
import { AvatarGroupModule } from 'primeng/avatargroup';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { MenuModule } from 'primeng/menu';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { BoardRealtimeService, RealtimeState } from '../../core/realtime/board-realtime.service';
import { AuthService } from '../../core/auth/auth.service';
import { adjacentIds } from '../../shared/collection-utils';
import { nonWhitespace, trimOptional, trimRequired } from '../../shared/form-validators';
import { Board, BoardColumn, BoardTask, Project, TaskFilters, TaskPriority, User } from '../../shared/models';
import { BoardService, TaskInput } from './board.service';
import { downloadPreparedReport, prepareReport, ReportFormat } from './report-download';

@Component({
  standalone: true,
  imports: [DatePipe, FormsModule, ReactiveFormsModule, RouterLink, DragDropModule, AvatarModule, AvatarGroupModule, ButtonModule, ConfirmDialogModule, DialogModule, DropdownModule, InputTextModule, MenuModule, SkeletonModule, TagModule, TooltipModule],
  providers: [ConfirmationService],
  templateUrl: './board.component.html',
  styleUrl: './board.component.scss'
})
export class BoardComponent implements OnInit, OnDestroy {
  private readonly destroyRef = inject(DestroyRef);
  private presenceVersion = 0;
  private loadVersion = 0;
  private readonly searchChanges = new Subject<string>();
  private readonly boardRefreshes = new Subject<void>();
  private boardLoadSubscription?: Subscription;
  private moveOperation = 0;
  private pendingRealtimeRefresh = false;
  private columnCreateIntentKey = crypto.randomUUID();
  private taskCreateIntentKey = crypto.randomUUID();
  readonly projectId = this.route.snapshot.paramMap.get('id') ?? '';
  project: Project = { id: this.projectId, name: 'Tablero' };
  boardEtag?: string;
  columns: BoardColumn[] = [];
  members: User[] = [];
  connectedUsers: User[] = [];
  connectedCount = 0;
  loading = true;
  loadError = '';
  saving = false;
  downloading: 'pdf' | 'xlsx' | null = null;
  loadingColumnIds = new Set<string>();
  failedColumnIds = new Set<string>();
  realtimeState: RealtimeState = 'disconnected';
  movePending = false;
  moveMessage = '';
  filters: TaskFilters = { search: '', assigneeId: null, priority: null };
  columnDialog = false;
  taskDialog = false;
  selectedColumn: BoardColumn | null = null;
  selectedTask: BoardTask | null = null;
  targetColumnId = '';
  readonly priorities: { label: string; value: TaskPriority }[] = [
    { label: 'Baja', value: 'low' }, { label: 'Media', value: 'medium' }, { label: 'Alta', value: 'high' }, { label: 'Crítica', value: 'critical' }
  ];
  readonly columnForm = this.fb.nonNullable.group({ name: ['', [Validators.required, nonWhitespace, Validators.maxLength(100)]] });
  readonly taskForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, nonWhitespace, Validators.maxLength(200)]],
    description: ['', Validators.maxLength(4000)],
    priority: ['medium' as TaskPriority, Validators.required],
    assigneeId: ['', Validators.required],
    dueDate: ['']
  });

  constructor(
    private readonly route: ActivatedRoute,
    private readonly fb: FormBuilder,
    private readonly boardService: BoardService,
    private readonly realtime: BoardRealtimeService,
    private readonly messages: MessageService,
    private readonly confirmation: ConfirmationService,
    private readonly auth: AuthService
  ) {
    this.realtime.events$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => {
      if (event.name === 'PresenceChanged') this.updatePresence(event.payload);
      else if (this.movePending) this.pendingRealtimeRefresh = true;
      else this.boardRefreshes.next();
    });
    this.realtime.resubscribing$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.presenceVersion = 0;
      this.connectedUsers = [];
      this.connectedCount = 0;
    });
    this.realtime.reconnected$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.boardRefreshes.next());
    this.realtime.state$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(state => this.realtimeState = state);
    this.boardRefreshes.pipe(debounceTime(100), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load(false, true));
    this.searchChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(search => {
      this.filters.search = search;
      this.load();
    });
  }

  ngOnInit(): void {
    this.load();
    this.realtime.connect(this.projectId)
      .then(() => this.boardRefreshes.next())
      .catch(() => undefined);
  }

  ngOnDestroy(): void { this.boardLoadSubscription?.unsubscribe(); void this.realtime.stop(); }

  load(showLoader = true, preserveDepth = false): void {
    const version = ++this.loadVersion;
    const depths = preserveDepth ? new Map(this.columns.map(column => [column.id, column.tasks.length])) : new Map<string, number>();
    const taskLimit = preserveDepth ? Math.min(50, Math.max(20, ...depths.values())) : 20;
    const filters = { ...this.filters };
    this.boardLoadSubscription?.unsubscribe();
    this.loadingColumnIds.clear();
    this.failedColumnIds.clear();
    if (showLoader) this.loading = true;
    this.loadError = '';
    this.boardLoadSubscription = this.boardService.load(this.projectId, filters, taskLimit).pipe(
      switchMap(board => preserveDepth ? this.replayDepth(board, depths, filters).pipe(
        catchError((error: unknown) => {
          if (error instanceof HttpErrorResponse && (error.status === 412 || error.status === 428)) {
            this.moveMessage = 'El tablero cambió durante la actualización. Se reinició la profundidad cargada para mantener datos coherentes.';
            this.applyBoard(board);
            return this.boardService.load(this.projectId, filters, 20);
          }
          this.moveMessage = 'El tablero se actualizó, pero no se pudo restaurar toda la profundidad cargada.';
          return of(board);
        })
      ) : of(board)),
      finalize(() => {
        if (version === this.loadVersion) this.loading = false;
      })
    ).subscribe({
      next: board => {
        if (version !== this.loadVersion) return;
        this.applyBoard(board);
      }, error: () => {
        if (version === this.loadVersion) this.loadError = 'No se pudo cargar el tablero. Revisa tu conexión e inténtalo nuevamente.';
      }
    });
  }

  get filtersActive(): boolean { return Boolean(this.filters.search.trim() || this.filters.assigneeId || this.filters.priority); }
  get taskListIds(): string[] { return this.columns.map(column => `tasks-${column.id}`); }
  get canManageColumns(): boolean {
    const userId = this.auth.user()?.id;
    return Boolean(userId && this.members.some(member => member.id === userId && member.role === 'owner'));
  }

  searchChanged(search: string): void {
    this.filters.search = search;
    this.searchChanges.next(search);
  }
  filterChanged(filter: 'assigneeId' | 'priority', value: string | TaskPriority | null): void {
    if (filter === 'assigneeId') this.filters.assigneeId = value;
    else this.filters.priority = value as TaskPriority | null;
    this.load();
  }

  onTaskListScroll(column: BoardColumn, event: Event): void {
    const element = event.currentTarget as HTMLElement;
    if (element.scrollHeight - element.scrollTop - element.clientHeight <= 120) this.loadMoreTasks(column);
  }

  loadMoreTasks(column: BoardColumn): void {
    if (!column.hasMoreTasks || this.loadingColumnIds.has(column.id)) return;
    const lastTask = column.tasks.at(-1);
    if (!lastTask || !this.boardEtag) return;

    const version = this.loadVersion;
    this.loadingColumnIds.add(column.id);
    this.failedColumnIds.delete(column.id);
    this.boardService.loadColumnTasks(this.projectId, column.id, lastTask, this.filters, this.boardEtag).pipe(
      finalize(() => {
        if (version === this.loadVersion) this.loadingColumnIds.delete(column.id);
      })
    ).subscribe({
      next: page => {
        if (version !== this.loadVersion) return;
        const loadedIds = new Set(column.tasks.map(task => task.id));
        for (const task of page.items) {
          if (loadedIds.has(task.id)) continue;
          column.tasks.push(task);
          loadedIds.add(task.id);
        }
        column.taskTotal = page.total;
        column.hasMoreTasks = page.hasMore;
        this.boardEtag = page.etag ?? this.boardEtag;
      },
      error: (error: unknown) => {
        if (version !== this.loadVersion) return;
        if (error instanceof HttpErrorResponse && (error.status === 412 || error.status === 428)) {
          this.moveMessage = 'El tablero cambió en el servidor y se actualizaron los datos.';
          this.load(false, true);
        }
        else this.failedColumnIds.add(column.id);
      }
    });
  }

  dropColumn(event: CdkDragDrop<BoardColumn[]>): void {
    if (this.movePending || event.previousIndex === event.currentIndex) return;
    const operation = this.beginMove();
    const snapshot = this.cloneColumns();
    moveItemInArray(this.columns, event.previousIndex, event.currentIndex);
    const column = this.columns[event.currentIndex];
    const position = adjacentIds(this.columns, event.currentIndex);
    this.boardService.moveColumn(this.projectId, column, this.boardEtag, position.beforeId, position.afterId).subscribe({
      next: updated => {
        if (operation !== this.moveOperation) return;
        Object.assign(column, updated);
        this.boardEtag = updated.boardEtag;
        this.finishMove(operation, true);
      },
      error: (error: unknown) => this.failMove(operation, snapshot, error, 'No se pudo reordenar la columna. Se restauró el tablero.')
    });
  }

  dropTask(event: CdkDragDrop<BoardTask[]>): void {
    if (this.filtersActive || this.movePending) return;
    if (event.previousContainer === event.container && event.previousIndex === event.currentIndex) return;
    const operation = this.beginMove();
    const snapshot = this.cloneColumns();
    const source = this.columns.find(column => column.tasks === event.previousContainer.data);
    const task = event.previousContainer.data[event.previousIndex];
    if (event.previousContainer === event.container) moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    else transferArrayItem(event.previousContainer.data, event.container.data, event.previousIndex, event.currentIndex);
    const target = this.columns.find(column => column.tasks === event.container.data);
    if (!target) { this.columns = snapshot; this.finishMove(operation, false); return; }
    if (source && source !== target) {
      source.taskTotal = Math.max(0, source.taskTotal - 1);
      target.taskTotal++;
    }
    task.columnId = target.id;
    const position = adjacentIds(target.tasks, event.currentIndex);
    this.boardService.moveTask(this.projectId, task, this.boardEtag, target.id, position.beforeId, position.afterId).subscribe({
      next: updated => {
        if (operation !== this.moveOperation) return;
        Object.assign(task, updated);
        this.boardEtag = updated.boardEtag;
        this.finishMove(operation, true);
      },
      error: (error: unknown) => this.failMove(operation, snapshot, error, 'No se pudo mover la tarea. Se revirtió el cambio.')
    });
  }

  openNewColumn(): void {
    this.selectedColumn = null;
    this.columnCreateIntentKey = crypto.randomUUID();
    this.columnForm.reset({ name: '' });
    this.columnDialog = true;
  }
  openEditColumn(column: BoardColumn): void { this.selectedColumn = column; this.columnForm.reset({ name: column.name }); this.columnDialog = true; }
  saveColumn(): void {
    if (this.columnForm.invalid) { this.columnForm.markAllAsTouched(); return; }
    this.saving = true;
    const input = { name: trimRequired(this.columnForm.getRawValue().name) };
    const request = this.selectedColumn
      ? this.boardService.updateColumn(this.projectId, this.selectedColumn, input)
      : this.boardService.createColumn(this.projectId, input, this.columnCreateIntentKey);
    request.pipe(finalize(() => this.saving = false)).subscribe({ next: () => { this.columnDialog = false; this.load(false, true); }, error: () => undefined });
  }

  confirmDeleteColumn(column: BoardColumn): void {
    this.confirmation.confirm({ message: `¿Eliminar la columna “${column.name}”?`, header: 'Eliminar columna', icon: 'pi pi-exclamation-triangle', acceptLabel: 'Eliminar', rejectLabel: 'Cancelar', acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.boardService.deleteColumn(this.projectId, column).subscribe({ next: () => this.load(false, true), error: () => undefined }) });
  }

  openNewTask(column: BoardColumn): void {
    this.selectedTask = null; this.targetColumnId = column.id; this.taskCreateIntentKey = crypto.randomUUID();
    this.taskForm.reset({ title: '', description: '', priority: 'medium', assigneeId: '', dueDate: '' });
    this.taskDialog = true;
  }

  openEditTask(task: BoardTask): void {
    this.selectedTask = task; this.targetColumnId = task.columnId;
    this.taskForm.reset({ title: task.title, description: task.description ?? '', priority: task.priority, assigneeId: task.assigneeId ?? task.assignee?.id ?? '', dueDate: task.dueDate?.slice(0, 10) ?? '' });
    this.taskDialog = true;
  }

  saveTask(): void {
    if (this.taskForm.invalid) { this.taskForm.markAllAsTouched(); return; }
    this.saving = true;
    const value = this.taskForm.getRawValue();
    const input: TaskInput = {
      ...value,
      title: trimRequired(value.title),
      description: trimOptional(value.description),
      assigneeId: value.assigneeId,
      dueDate: value.dueDate || null,
      columnId: this.targetColumnId
    };
    const request = this.selectedTask
      ? this.boardService.updateTask(this.projectId, this.selectedTask, input)
      : this.boardService.createTask(this.projectId, input, this.taskCreateIntentKey);
    request.pipe(finalize(() => this.saving = false)).subscribe({ next: () => { this.taskDialog = false; this.load(false, true); }, error: () => undefined });
  }

  confirmDeleteTask(task: BoardTask): void {
    this.confirmation.confirm({ message: `¿Eliminar la tarea “${task.title}”?`, header: 'Eliminar tarea', icon: 'pi pi-exclamation-triangle', acceptLabel: 'Eliminar', rejectLabel: 'Cancelar', acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.boardService.deleteTask(this.projectId, task).subscribe({ next: () => this.load(false, true), error: () => undefined }) });
  }

  download(format: ReportFormat): void {
    if (this.downloading) return;
    this.downloading = format;
    this.boardService.report(this.projectId, format, this.filters).pipe(finalize(() => this.downloading = null)).subscribe({
      next: response => {
        try {
          downloadPreparedReport(prepareReport(response, format, `${this.project.name}-reporte`));
        } catch (error) {
          this.messages.add({ severity: 'error', summary: 'No se pudo descargar el reporte', detail: error instanceof Error ? error.message : 'El reporte recibido no es válido.' });
        }
      }, error: () => undefined
    });
  }

  priorityLabel(priority: TaskPriority): string { return this.priorities.find(item => item.value === priority)?.label ?? priority; }
  prioritySeverity(priority: TaskPriority): 'info' | 'warning' | 'danger' | 'secondary' { return priority === 'critical' ? 'danger' : priority === 'high' ? 'warning' : priority === 'medium' ? 'info' : 'secondary'; }
  initials(user?: User | null): string { return user?.name?.split(' ').slice(0, 2).map(part => part[0]).join('').toUpperCase() || '?'; }
  realtimeLabel(): string {
    return ({ connected: 'Tiempo real conectado', connecting: 'Conectando tiempo real', reconnecting: 'Reconectando tiempo real', disconnected: 'Tiempo real desconectado', error: 'Tiempo real no disponible' } as Record<RealtimeState, string>)[this.realtimeState];
  }

  private cloneColumns(): BoardColumn[] { return this.columns.map(column => ({ ...column, tasks: column.tasks.map(task => ({ ...task })) })); }
  private beginMove(): number {
    this.movePending = true;
    this.moveMessage = '';
    return ++this.moveOperation;
  }

  private finishMove(operation: number, refresh: boolean): void {
    if (operation !== this.moveOperation) return;
    this.movePending = false;
    const shouldRefresh = refresh || this.pendingRealtimeRefresh;
    this.pendingRealtimeRefresh = false;
    if (shouldRefresh) this.load(false, true);
  }

  private failMove(operation: number, snapshot: BoardColumn[], error: unknown, detail: string): void {
    if (operation !== this.moveOperation) return;
    this.columns = snapshot;
    if (error instanceof HttpErrorResponse && error.status === 412) {
      this.moveMessage = 'El tablero cambió en el servidor. Se recargaron los datos antes de aplicar otro movimiento.';
      this.finishMove(operation, true);
      return;
    }
    this.moveMessage = detail;
    this.finishMove(operation, this.pendingRealtimeRefresh);
  }

  private replayDepth(board: Board, depths: ReadonlyMap<string, number>, filters: TaskFilters): Observable<Board> {
    return from(board.columns).pipe(
      concatMap(column => this.replayColumnDepth(board, column, depths.get(column.id) ?? 0, filters)),
      ignoreElements(),
      endWith(board)
    );
  }

  private replayColumnDepth(board: Board, column: BoardColumn, targetDepth: number, filters: TaskFilters): Observable<void> {
    const lastTask = column.tasks.at(-1);
    if (column.tasks.length >= targetDepth || !column.hasMoreTasks || !lastTask || !board.etag) return of(undefined);

    const previousLength = column.tasks.length;
    const limit = Math.min(20, targetDepth - previousLength);
    return this.boardService.loadColumnTasks(this.projectId, column.id, lastTask, filters, board.etag, limit).pipe(
      switchMap(page => {
        const loadedIds = new Set(column.tasks.map(task => task.id));
        for (const task of page.items) {
          if (loadedIds.has(task.id)) continue;
          column.tasks.push(task);
          loadedIds.add(task.id);
        }
        column.taskTotal = page.total;
        column.hasMoreTasks = page.hasMore;
        board.etag = page.etag ?? board.etag;
        return column.tasks.length === previousLength
          ? of(undefined)
          : this.replayColumnDepth(board, column, targetDepth, filters);
      })
    );
  }

  private applyBoard(board: Board): void {
    this.project = board.project;
    this.columns = board.columns;
    this.members = board.members;
    this.boardEtag = board.etag;
  }

  private updatePresence(payload: unknown): void {
    const value = payload as User[] | { users?: User[]; connectedUsers?: User[]; count?: number; connectedCount?: number; version?: number };
    if (!Array.isArray(value) && value?.version != null && value.version < this.presenceVersion) return;
    if (!Array.isArray(value) && value?.version != null) this.presenceVersion = value.version;
    this.connectedUsers = Array.isArray(value) ? value : value?.users ?? value?.connectedUsers ?? [];
    this.connectedCount = Array.isArray(value) ? value.length : value?.count ?? value?.connectedCount ?? this.connectedUsers.length;
  }
}
