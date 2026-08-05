import { CdkDragDrop, DragDropModule, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnDestroy, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
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
import { BoardRealtimeService } from '../../core/realtime/board-realtime.service';
import { adjacentIds, filterTasks } from '../../shared/collection-utils';
import { BoardColumn, BoardTask, Project, TaskFilters, TaskPriority, User } from '../../shared/models';
import { BoardService, TaskInput } from './board.service';

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
  saving = false;
  downloading: 'pdf' | 'xlsx' | null = null;
  filters: TaskFilters = { search: '', assigneeId: null, priority: null };
  columnDialog = false;
  taskDialog = false;
  selectedColumn: BoardColumn | null = null;
  selectedTask: BoardTask | null = null;
  targetColumnId = '';
  readonly priorities: { label: string; value: TaskPriority }[] = [
    { label: 'Baja', value: 'low' }, { label: 'Media', value: 'medium' }, { label: 'Alta', value: 'high' }, { label: 'Crítica', value: 'critical' }
  ];
  readonly columnForm = this.fb.nonNullable.group({ name: ['', [Validators.required, Validators.maxLength(80)]] });
  readonly taskForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(160)]],
    description: ['', Validators.maxLength(2000)],
    priority: ['medium' as TaskPriority, Validators.required],
    assigneeId: [''],
    dueDate: ['']
  });

  constructor(
    private readonly route: ActivatedRoute,
    private readonly fb: FormBuilder,
    private readonly boardService: BoardService,
    private readonly realtime: BoardRealtimeService,
    private readonly messages: MessageService,
    private readonly confirmation: ConfirmationService
  ) {
    this.realtime.events$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => {
      if (event.name === 'PresenceChanged') this.updatePresence(event.payload);
      else this.load(false);
    });
    this.realtime.resubscribing$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.presenceVersion = 0;
      this.connectedUsers = [];
      this.connectedCount = 0;
    });
    this.realtime.reconnected$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load(false));
  }

  ngOnInit(): void {
    this.load();
    this.realtime.connect(this.projectId).catch(() => this.messages.add({ severity: 'warn', summary: 'Tiempo real no disponible', detail: 'El tablero seguirá funcionando mediante la API.' }));
  }

  ngOnDestroy(): void { void this.realtime.stop(); }

  load(showLoader = true): void {
    if (showLoader) this.loading = true;
    this.boardService.load(this.projectId).pipe(finalize(() => this.loading = false)).subscribe({
      next: board => { this.project = board.project; this.columns = board.columns; this.members = board.members; this.boardEtag = board.etag; }, error: () => undefined
    });
  }

  visibleTasks(column: BoardColumn): BoardTask[] { return filterTasks(column.tasks, this.filters); }
  get filtersActive(): boolean { return Boolean(this.filters.search.trim() || this.filters.assigneeId || this.filters.priority); }
  get taskListIds(): string[] { return this.columns.map(column => `tasks-${column.id}`); }

  dropColumn(event: CdkDragDrop<BoardColumn[]>): void {
    if (event.previousIndex === event.currentIndex) return;
    const snapshot = this.cloneColumns();
    moveItemInArray(this.columns, event.previousIndex, event.currentIndex);
    const column = this.columns[event.currentIndex];
    const position = adjacentIds(this.columns, event.currentIndex);
    this.boardService.moveColumn(this.projectId, column, this.boardEtag, position.beforeId, position.afterId).subscribe({
      next: updated => { Object.assign(column, updated); this.boardEtag = updated.boardEtag; },
      error: () => this.rollback(snapshot, 'No se pudo reordenar la columna. Se restauró el tablero.')
    });
  }

  dropTask(event: CdkDragDrop<BoardTask[]>): void {
    if (this.filtersActive) return;
    if (event.previousContainer === event.container && event.previousIndex === event.currentIndex) return;
    const snapshot = this.cloneColumns();
    const task = event.previousContainer.data[event.previousIndex];
    if (event.previousContainer === event.container) moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    else transferArrayItem(event.previousContainer.data, event.container.data, event.previousIndex, event.currentIndex);
    const target = this.columns.find(column => column.tasks === event.container.data);
    if (!target) { this.columns = snapshot; return; }
    task.columnId = target.id;
    const position = adjacentIds(target.tasks, event.currentIndex);
    this.boardService.moveTask(this.projectId, task, this.boardEtag, target.id, position.beforeId, position.afterId).subscribe({
      next: updated => { Object.assign(task, updated); this.boardEtag = updated.boardEtag; },
      error: () => this.rollback(snapshot, 'No se pudo mover la tarea. Se revirtió el cambio.')
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
    const request = this.selectedColumn
      ? this.boardService.updateColumn(this.projectId, this.selectedColumn, this.columnForm.getRawValue())
      : this.boardService.createColumn(this.projectId, this.columnForm.getRawValue(), this.columnCreateIntentKey);
    request.pipe(finalize(() => this.saving = false)).subscribe({ next: () => { this.columnDialog = false; this.load(false); }, error: () => undefined });
  }

  confirmDeleteColumn(column: BoardColumn): void {
    this.confirmation.confirm({ message: `¿Eliminar la columna “${column.name}”?`, header: 'Eliminar columna', icon: 'pi pi-exclamation-triangle', acceptLabel: 'Eliminar', rejectLabel: 'Cancelar', acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.boardService.deleteColumn(this.projectId, column).subscribe({ next: () => this.load(false), error: () => undefined }) });
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
    const input: TaskInput = { ...value, assigneeId: value.assigneeId || null, dueDate: value.dueDate || null, columnId: this.targetColumnId };
    const request = this.selectedTask
      ? this.boardService.updateTask(this.projectId, this.selectedTask, input)
      : this.boardService.createTask(this.projectId, input, this.taskCreateIntentKey);
    request.pipe(finalize(() => this.saving = false)).subscribe({ next: () => { this.taskDialog = false; this.load(false); }, error: () => undefined });
  }

  confirmDeleteTask(task: BoardTask): void {
    this.confirmation.confirm({ message: `¿Eliminar la tarea “${task.title}”?`, header: 'Eliminar tarea', icon: 'pi pi-exclamation-triangle', acceptLabel: 'Eliminar', rejectLabel: 'Cancelar', acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.boardService.deleteTask(this.projectId, task).subscribe({ next: () => this.load(false), error: () => undefined }) });
  }

  download(format: 'pdf' | 'xlsx'): void {
    this.downloading = format;
    this.boardService.report(this.projectId, format, this.filters).pipe(finalize(() => this.downloading = null)).subscribe({
      next: response => {
        if (!response.body) return;
        const url = URL.createObjectURL(response.body);
        const link = document.createElement('a');
        link.href = url;
        link.download = `${this.project.name.replace(/[^a-z0-9]+/gi, '-').toLowerCase()}-report.${format}`;
        link.click();
        URL.revokeObjectURL(url);
      }, error: () => undefined
    });
  }

  priorityLabel(priority: TaskPriority): string { return this.priorities.find(item => item.value === priority)?.label ?? priority; }
  prioritySeverity(priority: TaskPriority): 'info' | 'warning' | 'danger' | 'secondary' { return priority === 'critical' ? 'danger' : priority === 'high' ? 'warning' : priority === 'medium' ? 'info' : 'secondary'; }
  initials(user?: User | null): string { return user?.name?.split(' ').slice(0, 2).map(part => part[0]).join('').toUpperCase() || '?'; }

  private cloneColumns(): BoardColumn[] { return this.columns.map(column => ({ ...column, tasks: column.tasks.map(task => ({ ...task })) })); }
  private rollback(snapshot: BoardColumn[], detail: string): void {
    this.columns = snapshot;
    this.messages.add({ severity: 'warn', summary: 'Cambio revertido', detail });
  }

  private updatePresence(payload: unknown): void {
    const value = payload as User[] | { users?: User[]; connectedUsers?: User[]; count?: number; connectedCount?: number; version?: number };
    if (!Array.isArray(value) && value?.version != null && value.version < this.presenceVersion) return;
    if (!Array.isArray(value) && value?.version != null) this.presenceVersion = value.version;
    this.connectedUsers = Array.isArray(value) ? value : value?.users ?? value?.connectedUsers ?? [];
    this.connectedCount = Array.isArray(value) ? value.length : value?.count ?? value?.connectedCount ?? this.connectedUsers.length;
  }
}
