import { DatePipe } from '@angular/common';
import { Component, OnDestroy } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize, Subscription } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { SkeletonModule } from 'primeng/skeleton';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { Project } from '../../shared/models';
import { dateOrder, nonWhitespace, trimOptional, trimRequired } from '../../shared/form-validators';
import { ProjectInput, ProjectsService } from './projects.service';

@Component({
  standalone: true,
  imports: [DatePipe, ReactiveFormsModule, ButtonModule, ConfirmDialogModule, DialogModule, DropdownModule, InputTextModule, SkeletonModule, TableModule, TagModule, ToolbarModule],
  providers: [ConfirmationService],
  templateUrl: './projects.component.html',
  styleUrl: './projects.component.scss'
})
export class ProjectsComponent implements OnDestroy {
  projects: Project[] = [];
  total = 0;
  first = 0;
  rows = 10;
  search = '';
  sort = 'updatedAt';
  direction: 'asc' | 'desc' = 'desc';
  loading = true;
  loadError = '';
  saving = false;
  dialogOpen = false;
  selected: Project | null = null;
  readonly statuses = ['planned', 'active', 'completed', 'archived'].map(value => ({ label: this.statusLabel(value), value }));
  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, nonWhitespace, Validators.maxLength(160)]],
    description: ['', Validators.maxLength(2000)],
    status: ['active'],
    startDate: [this.isoDate(0), Validators.required],
    expectedEndDate: [this.isoDate(30), Validators.required]
  }, { validators: dateOrder('startDate', 'expectedEndDate') });
  private searchTimer?: ReturnType<typeof setTimeout>;
  private loadSubscription?: Subscription;
  private activeQuery = '';
  private createIntentKey = crypto.randomUUID();

  constructor(
    private readonly fb: FormBuilder,
    private readonly projectsService: ProjectsService,
    private readonly router: Router,
    private readonly confirmation: ConfirmationService,
    private readonly messages: MessageService
  ) {}

  ngOnDestroy(): void {
    clearTimeout(this.searchTimer);
    this.loadSubscription?.unsubscribe();
  }

  lazyLoad(event: TableLazyLoadEvent): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 10;
    this.sort = typeof event.sortField === 'string' ? event.sortField : 'updatedAt';
    this.direction = event.sortOrder === 1 ? 'asc' : 'desc';
    this.load();
  }

  searchChanged(value: string): void {
    this.search = value;
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => { this.first = 0; this.load(); }, 300);
  }

  load(force = false): void {
    const query = { page: Math.floor(this.first / this.rows) + 1, pageSize: this.rows, search: this.search.trim(), sort: this.sort, direction: this.direction };
    const queryKey = JSON.stringify(query);
    if (!force && this.loading && this.activeQuery === queryKey) return;
    this.loadSubscription?.unsubscribe();
    this.activeQuery = queryKey;
    this.loading = true;
    this.loadError = '';
    this.loadSubscription = this.projectsService.list(query)
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: page => { this.projects = page.items; this.total = page.total; },
        error: () => this.loadError = 'No se pudieron cargar los proyectos. Revisa tu conexión e inténtalo nuevamente.'
      });
  }

  openCreate(): void {
    this.selected = null;
    this.createIntentKey = crypto.randomUUID();
    this.form.reset({ name: '', description: '', status: 'active', startDate: this.isoDate(0), expectedEndDate: this.isoDate(30) });
    this.dialogOpen = true;
  }

  openEdit(project: Project): void {
    this.selected = project;
    this.form.reset({ name: project.name, description: project.description ?? '', status: project.status ?? 'active', startDate: project.startDate ?? this.isoDate(0), expectedEndDate: project.expectedEndDate ?? this.isoDate(30) });
    this.dialogOpen = true;
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.saving = true;
    const value = this.form.getRawValue();
    const input: ProjectInput = {
      ...value,
      name: trimRequired(value.name),
      description: trimOptional(value.description)
    };
    const request = this.selected
      ? this.projectsService.update(this.selected, input)
      : this.projectsService.create(input, this.createIntentKey);
    request.pipe(finalize(() => this.saving = false)).subscribe({
      next: () => {
        this.dialogOpen = false;
        this.messages.add({ severity: 'success', summary: this.selected ? 'Proyecto actualizado' : 'Proyecto creado' });
        this.load(true);
      },
      error: () => undefined
    });
  }

  confirmDelete(project: Project): void {
    this.confirmation.confirm({
      message: `¿Eliminar “${project.name}”? Esta acción no se puede deshacer.`,
      header: 'Eliminar proyecto', icon: 'pi pi-exclamation-triangle', acceptLabel: 'Eliminar', rejectLabel: 'Cancelar', acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.projectsService.delete(project).subscribe({
        next: () => {
          if (this.projects.length === 1 && this.first > 0) this.first = Math.max(0, this.first - this.rows);
          this.messages.add({ severity: 'success', summary: 'Proyecto eliminado' });
          this.load(true);
        }, error: () => undefined
      })
    });
  }

  openBoard(project: Project): void { void this.router.navigate(['/projects', project.id, 'board']); }
  statusLabel(status?: string): string { return ({ planned: 'Planificado', active: 'Activo', completed: 'Completado', archived: 'Archivado' } as Record<string, string>)[status ?? ''] ?? status ?? 'Activo'; }
  severity(status?: string): 'success' | 'warning' | 'info' | 'secondary' { return status === 'planned' ? 'warning' : status === 'completed' ? 'info' : status === 'archived' ? 'secondary' : 'success'; }

  private isoDate(daysFromToday: number): string {
    const value = new Date();
    value.setDate(value.getDate() + daysFromToday);
    return value.toISOString().slice(0, 10);
  }
}
