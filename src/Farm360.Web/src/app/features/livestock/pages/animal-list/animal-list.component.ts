import {
  Component, inject, signal, computed, ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule }      from '@angular/common';
import { RouterModule, Router }      from '@angular/router';
import { FormsModule }       from '@angular/forms';
import { AnimalService }     from '../../services/animal.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import {
  AnimalListItemDto, AnimalListParams, AnimalSpecies, AnimalStatus, AnimalSex,
  SPECIES_LABELS, STATUS_LABELS, SEX_LABELS, PagedAnimalListDto
} from '../../models/animal.models';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { toObservable, toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, debounceTime, distinctUntilChanged, tap, filter } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-animal-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, RouterModule, FormsModule, 
    PageHeaderComponent, LoadingComponent, EmptyStateComponent, MatIconModule, MatMenuModule, MatButtonModule
  ],
  templateUrl: './animal-list.component.html'
})
export class AnimalListComponent {
  private readonly svc      = inject(AnimalService);
  private readonly router   = inject(Router);
  private readonly dialog   = inject(MatDialog);
  private readonly contextService = inject(WorkingContextService);

  // ── Signals ──────────────────────────────────────────────────────────────
  readonly loading    = signal(true);
  readonly error      = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly params     = signal<AnimalListParams>({ pageNumber: 1, pageSize: 20 });

  readonly Math = Math;
  readonly AnimalStatus = AnimalStatus;
  readonly AnimalSex    = AnimalSex;

  readonly speciesOptions = Object.entries(SPECIES_LABELS).map(([v, l]) => ({ value: +v, label: l }));
  readonly statusOptions  = Object.entries(STATUS_LABELS).map(([v, l]) => ({ value: +v, label: l }));
  readonly sexOptions     = Object.entries(SEX_LABELS).map(([v, l]) => ({ value: +v, label: l }));

  readonly hasActiveFilters = computed(() => {
    const p = this.params();
    return !!(p.species != null || p.status != null || p.sex != null || p.search);
  });

  constructor() {
    // Listen to farm context changes
    this.contextService.currentFarm$.pipe(
      takeUntilDestroyed()
    ).subscribe(farm => {
      this.params.update(p => ({ ...p, farmId: farm?.id || undefined, pageNumber: 1 }));
    });

    // Debounce search input
    toObservable(this.searchTerm).pipe(
      debounceTime(350),
      distinctUntilChanged(),
      takeUntilDestroyed()
    ).subscribe(term => {
      // Only update if it actually changes the search term in params to avoid infinite loops
      // Wait, toObservable(this.searchTerm) is triggered when this.searchTerm.set() is called
      this.params.update(p => ({ ...p, search: term || undefined, pageNumber: 1 }));
    });
  }

  // ── Data ──────────────────────────────────────────────────────────────────
  private fetchParams = computed(() => this.params());
  private refreshTrigger = signal(0);

  private combinedParams = computed(() => ({
    params: this.fetchParams(),
    refresh: this.refreshTrigger()
  }));

  readonly result = toSignal(
    toObservable(this.combinedParams).pipe(
      // Ensure farmId is present before fetching, unless your API allows farm-less fetches
      filter(({ params }) => !!params.farmId), 
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(({ params }) => this.svc.getList(params).pipe(
        catchError(e => {
          this.error.set(e?.error?.detail ?? 'An error occurred');
          return of(null);
        })
      )),
      tap(() => this.loading.set(false))
    ),
    { initialValue: null }
  );

  refresh(): void { this.refreshTrigger.update(v => v + 1); }

  // ── Filters ───────────────────────────────────────────────────────────────
  onSearchChange(term: string): void {
    this.searchTerm.set(term);
  }

  setFilter(key: keyof AnimalListParams, event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    const numVal = val ? parseInt(val, 10) : null;
    this.params.update(p => ({ ...p, [key]: numVal ?? undefined, pageNumber: 1 }));
  }

  clearFilters(): void {
    this.searchTerm.set('');
    // Notice how changing searchTerm will also update params due to the observable subscription.
    // However, we want to clear everything else as well.
    // So we manually set params, taking care to preserve the farmId
    const currentFarmId = this.params().farmId;
    this.params.set({ pageNumber: 1, pageSize: 20, farmId: currentFarmId });
  }

  // ── Pagination ─────────────────────────────────────────────────────────────
  prevPage(): void { this.params.update(p => ({ ...p, pageNumber: (p.pageNumber ?? 1) - 1 })); }
  nextPage(): void { this.params.update(p => ({ ...p, pageNumber: (p.pageNumber ?? 1) + 1 })); }

  pageStart = computed(() => {
    const p = this.params();
    return ((p.pageNumber ?? 1) - 1) * (p.pageSize ?? 20) + 1;
  });
  pageEnd = computed(() => {
    const r = this.result();
    if (!r) return 0;
    return Math.min(this.pageStart() + (p => p.pageSize ?? 20)(this.params()) - 1, r.totalCount);
  });

  // ── Display helpers ───────────────────────────────────────────────────────
  speciesLabel(s: AnimalSpecies): string { return SPECIES_LABELS[s] ?? '—'; }
  statusLabel(s: AnimalStatus):  string  { return STATUS_LABELS[s]  ?? '—'; }
  sexLabel(s: AnimalSex):        string  { return SEX_LABELS[s]     ?? '—'; }

  statusClass(status: AnimalStatus): string {
    switch (status) {
      case AnimalStatus.Active: return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
      case AnimalStatus.Quarantined: return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
      case AnimalStatus.Sold: return 'bg-blue-50 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400 border border-blue-200 dark:border-blue-800';
      case AnimalStatus.Dead: return 'bg-red-50 text-red-700 dark:bg-red-900/20 dark:text-red-400 border border-red-200 dark:border-red-800';
      default: return 'bg-gray-50 text-gray-700 dark:bg-gray-900/20 dark:text-gray-400 border border-gray-200 dark:border-gray-800';
    }
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
    return m > 0 ? `${y}y ${m}m` : `${y}y`;
  }

  // ── Actions ───────────────────────────────────────────────────────────────
  onRegister(): void {
    this.router.navigate(['/livestock/register']);
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

    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.svc.delete(animal.id).subscribe({
          next: () => this.refresh(),
          error: e => alert(e?.error?.detail ?? 'Delete failed'),
        });
      }
    });
  }
}
