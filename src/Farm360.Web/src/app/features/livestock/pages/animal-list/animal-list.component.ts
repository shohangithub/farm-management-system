import {
  Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule }      from '@angular/common';
import { RouterModule }      from '@angular/router';
import { FormsModule }       from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { AnimalService }     from '../../services/animal.service';
import {
  AnimalListItemDto, AnimalListParams, AnimalSpecies, AnimalStatus, AnimalSex,
  SPECIES_LABELS, STATUS_LABELS, STATUS_BADGE_CLASS, SEX_LABELS,
  PagedAnimalListDto,
} from '../../models/animal.models';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { DataTableComponent, TableColumn } from '../../../../shared/components/data-table/data-table.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatCardModule } from '@angular/material/card';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';

@Component({
  selector: 'app-animal-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, RouterModule, FormsModule, 
    PageHeaderComponent, DataTableComponent, LoadingComponent, EmptyStateComponent,
    MatDialogModule, MatCardModule, MatSelectModule, MatInputModule, MatIconModule
  ],
  template: `
    <app-page-header 
      title="Livestock" 
      description="Manage your herd — register, track, and monitor all animals"
      primaryActionLabel="Register Animal"
      primaryActionIcon="add"
      (primaryAction)="onRegister()">
    </app-page-header>

    <!-- Filters -->
    <mat-card class="mb-6 !bg-white dark:!bg-gray-800 !shadow-sm !rounded-xl border border-gray-200 dark:border-gray-700">
      <mat-card-content class="!p-4">
        <div class="flex flex-col md:flex-row gap-4 items-center">
          
          <mat-form-field appearance="outline" class="w-full md:w-1/3 !mb-[-1.25em]">
            <mat-icon matPrefix class="text-gray-400">search</mat-icon>
            <input matInput placeholder="Search by tag ID or breed..." 
                   [ngModel]="searchTerm()" (ngModelChange)="onSearchChange($event)">
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full md:w-48 !mb-[-1.25em]">
            <mat-select [ngModel]="params().species" (ngModelChange)="setFilter('species', $event || null)">
              <mat-option [value]="null">All Species</mat-option>
              <mat-option *ngFor="let s of speciesOptions" [value]="s.value">{{ s.label }}</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full md:w-48 !mb-[-1.25em]">
            <mat-select [ngModel]="params().status" (ngModelChange)="setFilter('status', $event || null)">
              <mat-option [value]="null">All Status</mat-option>
              <mat-option *ngFor="let s of statusOptions" [value]="s.value">{{ s.label }}</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full md:w-48 !mb-[-1.25em]">
            <mat-select [ngModel]="params().sex" (ngModelChange)="setFilter('sex', $event || null)">
              <mat-option [value]="null">Both Sexes</mat-option>
              <mat-option [value]="AnimalSex.Male">Male</mat-option>
              <mat-option [value]="AnimalSex.Female">Female</mat-option>
            </mat-select>
          </mat-form-field>

          <button *ngIf="hasActiveFilters()" mat-button color="warn" (click)="clearFilters()">Clear</button>
        </div>
      </mat-card-content>
    </mat-card>

    <!-- Content -->
    <div class="relative min-h-[400px]">
      <app-loading *ngIf="loading()" [overlay]="true"></app-loading>

      <app-empty-state 
        *ngIf="!loading() && error()" 
        icon="error_outline" 
        title="Failed to load animals" 
        [description]="error() || 'An unknown error occurred.'"
        actionLabel="Try Again" 
        (action)="refresh()">
      </app-empty-state>

      <app-empty-state 
        *ngIf="!loading() && !error() && result()?.items?.length === 0" 
        icon="pets" 
        title="No animals found" 
        [description]="hasActiveFilters() ? 'Try adjusting your filters.' : 'Register your first animal to get started.'"
        [actionLabel]="!hasActiveFilters() ? 'Register Animal' : undefined"
        (action)="onRegister()">
      </app-empty-state>

      <app-data-table 
        *ngIf="!error() && result()?.items?.length"
        [data]="result()?.items || []" 
        [columns]="tableColumns" 
        [displayedColumns]="displayedColumns"
        (view)="onView($event)"
        (delete)="onDelete($event)">
      </app-data-table>
    </div>
  `
})
export class AnimalListComponent implements OnInit, OnDestroy {
  private readonly svc   = inject(AnimalService);
  private readonly destroy$ = new Subject<void>();
  private readonly search$  = new Subject<string>();

  // ── Signals ──────────────────────────────────────────────────────────────
  readonly loading   = signal(false);
  readonly error     = signal<string | null>(null);
  readonly result    = signal<PagedAnimalListDto | null>(null);
  readonly searchTerm = signal('');
  readonly params    = signal<AnimalListParams>({ pageNumber: 1, pageSize: 20 });

  private dialog = inject(MatDialog);
  private router = inject(Router);

  tableColumns: TableColumn[] = [
    { def: 'tagId', header: 'Tag ID', cell: (e: AnimalListItemDto) => `<span class="font-semibold text-green-700">${e.tagId}</span>` },
    { def: 'breed', header: 'Breed', cell: (e: AnimalListItemDto) => `<span class="font-medium">${this.speciesLabel(e.species)}</span><br><span class="text-xs text-gray-500">${e.breedName}</span>` },
    { def: 'sex', header: 'Sex', cell: (e: AnimalListItemDto) => this.sexLabel(e.sex) },
    { def: 'age', header: 'Age', cell: (e: AnimalListItemDto) => this.ageLabel(e.dateOfBirth) },
    { def: 'weight', header: 'Weight', cell: (e: AnimalListItemDto) => e.latestWeightKg ? `${e.latestWeightKg.toFixed(1)} kg` : '—' },
    { def: 'status', header: 'Status', cell: (e: AnimalListItemDto) => `<span class="px-2 py-1 text-xs font-medium rounded-full bg-gray-100 text-gray-800">${this.statusLabel(e.status)}</span>` },
    { def: 'actions', header: 'Actions', cell: () => '', isAction: true }
  ];
  displayedColumns = ['tagId', 'breed', 'sex', 'age', 'weight', 'status', 'actions'];

  // Expose enums to template
  readonly AnimalStatus = AnimalStatus;
  readonly AnimalSex    = AnimalSex;

  readonly speciesOptions = Object.entries(SPECIES_LABELS).map(([v, l]) => ({ value: +v, label: l }));
  readonly statusOptions  = Object.entries(STATUS_LABELS).map(([v, l]) => ({ value: +v, label: l }));

  readonly hasActiveFilters = computed(() => {
    const p = this.params();
    return !!(p.species != null || p.status != null || p.sex != null || p.search);
  });

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    // Debounce search input
    this.search$.pipe(
      debounceTime(350),
      distinctUntilChanged(),
      takeUntil(this.destroy$)
    ).subscribe(term => {
      this.params.update(p => ({ ...p, search: term || undefined, pageNumber: 1 }));
      this.load();
    });

    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Data ──────────────────────────────────────────────────────────────────
  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.svc.getList(this.params()).pipe(takeUntil(this.destroy$)).subscribe({
      next:  r => { this.result.set(r); this.loading.set(false); },
      error: e => { this.error.set(e?.error?.detail ?? 'An error occurred'); this.loading.set(false); }
    });
  }

  refresh(): void { this.load(); }

  // ── Filters ───────────────────────────────────────────────────────────────
  onSearchChange(term: string): void {
    this.searchTerm.set(term);
    this.search$.next(term);
  }

  setFilter(key: keyof AnimalListParams, value: unknown): void {
    this.params.update(p => ({ ...p, [key]: value ?? undefined, pageNumber: 1 }));
    this.load();
  }

  clearFilters(): void {
    this.searchTerm.set('');
    this.params.set({ pageNumber: 1, pageSize: 20 });
    this.load();
  }

  toggleSort(field: string): void {
    this.params.update(p => ({
      ...p,
      sortBy: field,
      sortDesc: p.sortBy === field ? !p.sortDesc : false,
    }));
    this.load();
  }

  // ── Pagination ─────────────────────────────────────────────────────────────
  prevPage(): void { this.params.update(p => ({ ...p, pageNumber: (p.pageNumber ?? 1) - 1 })); this.load(); }
  nextPage(): void { this.params.update(p => ({ ...p, pageNumber: (p.pageNumber ?? 1) + 1 })); this.load(); }

  pageStart = computed(() => {
    const p = this.params();
    return ((p.pageNumber ?? 1) - 1) * (p.pageSize ?? 20) + 1;
  });
  pageEnd = computed(() => {
    const r = this.result();
    if (!r) return 0;
    return Math.min(this.pageStart() + (p => p.pageSize ?? 20)(this.params()) - 1, r.totalCount);
  });

  // ── Stats ─────────────────────────────────────────────────────────────────
  countByStatus(status: AnimalStatus): number {
    return this.result()?.items.filter(a => a.status === status).length ?? 0;
  }

  // ── Display helpers ───────────────────────────────────────────────────────
  speciesLabel(s: AnimalSpecies): string { return SPECIES_LABELS[s] ?? '—'; }
  statusLabel(s: AnimalStatus):  string  { return STATUS_LABELS[s]  ?? '—'; }
  sexLabel(s: AnimalSex):        string  { return SEX_LABELS[s]     ?? '—'; }
  statusBadgeClass(s: AnimalStatus): string { return STATUS_BADGE_CLASS[s] ?? 'badge-default'; }

  tagTypeLabel(t: number): string {
    return t === 1 ? 'Manual' : t === 2 ? 'Ear Tag' : 'RFID';
  }

  speciesEmoji(s: AnimalSpecies): string {
    return s === AnimalSpecies.Goat ? '🐐' : s === AnimalSpecies.Sheep ? '🐑' : '🐄';
  }

  ageLabel(dob: string): string {
    const days = Math.floor((Date.now() - new Date(dob).getTime()) / 86_400_000);
    if (days < 30)  return `${days}d`;
    if (days < 365) return `${Math.floor(days / 30)}mo`;
    const y = Math.floor(days / 365);
    const m = Math.floor((days % 365) / 30);
    return m > 0 ? `${y}y ${m}mo` : `${y}y`;
  }

  // ── Actions ───────────────────────────────────────────────────────────────
  onRegister(): void {
    this.router.navigate(['/livestock/register']);
  }

  onView(animal: AnimalListItemDto): void {
    this.router.navigate(['/livestock', animal.id]);
  }

  onDelete(animal: AnimalListItemDto): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      data: {
        title: 'Delete Animal',
        message: `Are you sure you want to delete animal ${animal.tagId}? This action cannot be undone.`,
        confirmButtonText: 'Delete',
        isDestructive: true
      }
    });

    dialogRef.afterClosed().pipe(takeUntil(this.destroy$)).subscribe(result => {
      if (result) {
        this.svc.delete(animal.id).pipe(takeUntil(this.destroy$)).subscribe({
          next: () => this.load(),
          error: e => alert(e?.error?.detail ?? 'Delete failed'),
        });
      }
    });
  }
}
