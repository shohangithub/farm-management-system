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

@Component({
  selector: 'app-animal-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <!-- ── Page Header ───────────────────────────────────── -->
    <div class="page-header">
      <div>
        <nav class="breadcrumb">
          <a routerLink="/">Home</a>
          <span class="separator">›</span>
          <span>Livestock</span>
        </nav>
        <h1 class="page-title">Livestock</h1>
        <p class="page-subtitle">Manage your herd — register, track, and monitor all animals</p>
      </div>
      <div class="d-flex gap-3 align-center">
        <button class="btn btn-secondary btn-sm" (click)="refresh()">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 4v6h6M23 20v-6h-6"/><path d="M20.49 9A9 9 0 0 0 5.64 5.64L1 10m22 4l-4.64 4.36A9 9 0 0 1 3.51 15"/></svg>
          Refresh
        </button>
        <a routerLink="/livestock/register" class="btn btn-primary">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
          Register Animal
        </a>
      </div>
    </div>

    <!-- ── Summary Stats ──────────────────────────────────── -->
    <div class="stat-grid" *ngIf="result()">
      <div class="stat-card">
        <div class="stat-label">Total Animals</div>
        <div class="stat-value">{{ result()!.totalCount }}</div>
        <div class="stat-delta">in your herd</div>
      </div>
      <div class="stat-card">
        <div class="stat-label">Active</div>
        <div class="stat-value text-accent">
          {{ countByStatus(AnimalStatus.Active) }}
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-label">Quarantined</div>
        <div class="stat-value text-warning">
          {{ countByStatus(AnimalStatus.Quarantined) }}
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-label">Sold this month</div>
        <div class="stat-value">{{ countByStatus(AnimalStatus.Sold) }}</div>
      </div>
    </div>

    <!-- ── Filter Bar ─────────────────────────────────────── -->
    <div class="filter-bar">
      <div class="search-input-wrapper">
        <svg class="search-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
        </svg>
        <input
          class="form-control"
          type="text"
          placeholder="Search by tag ID or breed..."
          [ngModel]="searchTerm()"
          (ngModelChange)="onSearchChange($event)"
        />
      </div>

      <select class="form-control" style="width:auto"
        [ngModel]="params().species"
        (ngModelChange)="setFilter('species', $event || null)">
        <option [value]="null">All Species</option>
        <option *ngFor="let s of speciesOptions" [value]="s.value">{{ s.label }}</option>
      </select>

      <select class="form-control" style="width:auto"
        [ngModel]="params().status"
        (ngModelChange)="setFilter('status', $event || null)">
        <option [value]="null">All Status</option>
        <option *ngFor="let s of statusOptions" [value]="s.value">{{ s.label }}</option>
      </select>

      <select class="form-control" style="width:auto"
        [ngModel]="params().sex"
        (ngModelChange)="setFilter('sex', $event || null)">
        <option [value]="null">Both Sexes</option>
        <option [value]="AnimalSex.Male">Male ♂</option>
        <option [value]="AnimalSex.Female">Female ♀</option>
      </select>

      <button class="btn btn-ghost btn-sm" (click)="clearFilters()" *ngIf="hasActiveFilters()">
        Clear filters
      </button>
    </div>

    <!-- ── Animal Table ───────────────────────────────────── -->
    <div class="card">

      <!-- Loading State -->
      <div *ngIf="loading()" class="card-body">
        <div class="d-flex" style="flex-direction:column;gap:12px">
          <div class="skeleton" style="height:44px;border-radius:8px;" *ngFor="let i of skeletonRows"></div>
        </div>
      </div>

      <!-- Error State -->
      <div *ngIf="!loading() && error()" class="empty-state">
        <div class="empty-icon">⚠️</div>
        <h3>Failed to load animals</h3>
        <p>{{ error() }}</p>
        <button class="btn btn-primary btn-sm" (click)="refresh()">Try again</button>
      </div>

      <!-- Empty State -->
      <div *ngIf="!loading() && !error() && result()?.items?.length === 0" class="empty-state">
        <div class="empty-icon">🐄</div>
        <h3>No animals found</h3>
        <p *ngIf="hasActiveFilters()">Try adjusting your filters</p>
        <p *ngIf="!hasActiveFilters()">Register your first animal to get started</p>
        <a *ngIf="!hasActiveFilters()" routerLink="/livestock/register" class="btn btn-primary">
          Register Animal
        </a>
      </div>

      <!-- Table -->
      <div *ngIf="!loading() && !error() && result()?.items?.length" style="overflow-x:auto">
        <table class="data-table">
          <thead>
            <tr>
              <th></th>
              <th (click)="toggleSort('tagid')" style="cursor:pointer">
                Tag ID <span *ngIf="params().sortBy === 'tagid'">{{ params().sortDesc ? '↓' : '↑' }}</span>
              </th>
              <th>Species / Breed</th>
              <th>Sex</th>
              <th (click)="toggleSort('dateofbirth')" style="cursor:pointer">
                Age <span *ngIf="params().sortBy === 'dateofbirth'">{{ params().sortDesc ? '↓' : '↑' }}</span>
              </th>
              <th (click)="toggleSort('weight')" style="cursor:pointer">
                Weight <span *ngIf="params().sortBy === 'weight'">{{ params().sortDesc ? '↓' : '↑' }}</span>
              </th>
              <th>ADG</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let animal of result()!.items" class="animal-row">
              <!-- Photo -->
              <td style="width:48px;padding-right:0">
                <div class="animal-avatar" [style.background-image]="animal.primaryPhotoUrl ? 'url(' + animal.primaryPhotoUrl + ')' : 'none'">
                  <span *ngIf="!animal.primaryPhotoUrl">{{ speciesEmoji(animal.species) }}</span>
                </div>
              </td>
              <!-- Tag -->
              <td>
                <a [routerLink]="['/livestock', animal.id]" class="fw-600 text-primary" style="font-size:0.875rem">
                  {{ animal.tagId }}
                </a>
                <div class="text-xs text-muted">{{ tagTypeLabel(animal.tagType) }}</div>
              </td>
              <!-- Species/Breed -->
              <td>
                <div class="fw-500" style="font-size:0.875rem">{{ speciesLabel(animal.species) }}</div>
                <div class="text-xs text-muted">{{ animal.breedName }}</div>
              </td>
              <!-- Sex -->
              <td><span class="text-sm">{{ sexLabel(animal.sex) }}</span></td>
              <!-- Age -->
              <td class="text-sm">{{ ageLabel(animal.dateOfBirth) }}</td>
              <!-- Weight -->
              <td>
                <span *ngIf="animal.latestWeightKg" class="fw-500 text-primary text-sm">
                  {{ animal.latestWeightKg | number:'1.1-1' }} kg
                </span>
                <span *ngIf="!animal.latestWeightKg" class="text-muted text-sm">—</span>
              </td>
              <!-- ADG -->
              <td>
                <span *ngIf="animal.adgKgPerDay" class="text-sm" [class.text-accent]="animal.adgKgPerDay > 0">
                  {{ animal.adgKgPerDay | number:'1.3-3' }} kg/d
                </span>
                <span *ngIf="!animal.adgKgPerDay" class="text-muted text-sm">—</span>
              </td>
              <!-- Status -->
              <td>
                <span class="badge" [ngClass]="statusBadgeClass(animal.status)">
                  {{ statusLabel(animal.status) }}
                </span>
              </td>
              <!-- Actions -->
              <td style="text-align:right">
                <div class="d-flex gap-2 justify-end">
                  <a [routerLink]="['/livestock', animal.id]" class="btn btn-ghost btn-sm btn-icon" title="View detail">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
                  </a>
                  <button class="btn btn-ghost btn-sm btn-icon text-danger" title="Delete" (click)="onDelete(animal)">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14H6L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4h6v2"/></svg>
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div *ngIf="result() && result()!.totalPages > 1" class="pagination-bar">
        <span class="text-sm text-muted">
          Showing {{ pageStart() }}–{{ pageEnd() }} of {{ result()!.totalCount }}
        </span>
        <div class="d-flex gap-2 align-center">
          <button class="btn btn-ghost btn-sm" [disabled]="!result()!.hasPreviousPage" (click)="prevPage()">← Previous</button>
          <span class="text-sm">{{ params().pageNumber }} / {{ result()!.totalPages }}</span>
          <button class="btn btn-ghost btn-sm" [disabled]="!result()!.hasNextPage" (click)="nextPage()">Next →</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .animal-avatar {
      width: 36px; height: 36px;
      border-radius: 8px;
      background: var(--bg-overlay);
      background-size: cover;
      background-position: center;
      display: flex; align-items: center; justify-content: center;
      font-size: 1.2rem;
      border: 1px solid var(--border-subtle);
    }
    .animal-row { transition: background var(--transition-fast); cursor: pointer; }
    .pagination-bar {
      display: flex; align-items: center; justify-content: space-between;
      padding: 12px 16px;
      border-top: 1px solid var(--border-subtle);
    }
    .justify-end { justify-content: flex-end; }
  `],
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

  readonly skeletonRows = Array(8).fill(0);

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
  onDelete(animal: AnimalListItemDto): void {
    if (!confirm(`Delete animal ${animal.tagId}? This cannot be undone.`)) return;
    this.svc.delete(animal.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.load(),
      error: e => alert(e?.error?.detail ?? 'Delete failed'),
    });
  }
}
