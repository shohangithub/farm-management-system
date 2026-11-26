import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { BreedService, BreedParams } from '../../services/breed.service';
import { BreedDto } from '../../models/breed.models';
import { MatIconModule } from '@angular/material/icon';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { BreedSetupDialogComponent } from '../../components/dialogs/breed-setup-dialog/breed-setup-dialog.component';
import { BreedDetailDialogComponent } from '../../components/dialogs/breed-detail-dialog/breed-detail-dialog.component';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { toObservable, toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, debounceTime, distinctUntilChanged, tap, skip } from 'rxjs/operators';
import { of } from 'rxjs';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-breed-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatIconModule,
    MatDialogModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    TranslatePipe
  ],
  template: `
    <app-page-header
      [title]="'Breeds' | t"
      [description]="'The breeds you keep, with their expected daily gain and feed use.' | t"
      [breadcrumbActiveNode]="'Breeds' | t">
      <div actions>
        <button (click)="openSetupDialog()"
          class="h-10 px-4 text-sm font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded-xl transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> {{ 'Add Breed' | t }}
        </button>
      </div>
    </app-page-header>

    <div class="bg-white dark:bg-surface-dark rounded-2xl shadow-sm border border-gray-200 dark:border-gray-800 overflow-hidden relative min-h-[400px]">
      <app-loading *ngIf="loading()" [overlay]="true"></app-loading>
      <app-error-state *ngIf="error() as err" [message]="err" (retry)="retryLoad()"></app-error-state>

      <!-- Filters & Sorting Toolbar -->
      <div class="p-4 border-b border-gray-100 dark:border-gray-800 grid grid-cols-2 sm:flex sm:flex-row items-center gap-2 sm:gap-3">
        <div class="relative col-span-2 w-full sm:w-72">
          <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 !text-[18px] !w-[18px] !h-[18px]">search</mat-icon>
          <input [ngModel]="searchTerm()" (ngModelChange)="onSearchChange($event)"
            [placeholder]="'Search breeds...' | t"
            class="w-full pl-9 pr-4 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all" />
        </div>

        <select [ngModel]="params().category ?? null" (ngModelChange)="onCategoryChange($event)"
          class="w-full sm:w-48 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option [ngValue]="null">{{ 'All Categories' | t }}</option>
          <option value="Indigenous">{{ 'Indigenous' | t }}</option>
          <option value="Exotic">{{ 'Exotic' | t }}</option>
          <option value="Crossbred">{{ 'Crossbred' | t }}</option>
        </select>

        <select [ngModel]="params().mainPurpose ?? null" (ngModelChange)="onPurposeChange($event)"
          class="w-full sm:w-48 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option [ngValue]="null">{{ 'All Purposes' | t }}</option>
          <option value="Dairy">{{ 'Dairy' | t }}</option>
          <option value="Beef">{{ 'Beef' | t }}</option>
          <option value="Dual-purpose">{{ 'Dual-purpose' | t }}</option>
        </select>

        <select [ngModel]="currentSortKey()" (ngModelChange)="onSortChange($event)" [attr.aria-label]="'Sort' | t"
          class="col-span-2 w-full sm:w-52 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option value="name_asc">{{ 'Sort: Name (A-Z)' | t }}</option>
          <option value="name_desc">{{ 'Sort: Name (Z-A)' | t }}</option>
          <option value="adg_desc">{{ 'Sort: Highest ADG' | t }}</option>
          <option value="fcr_asc">{{ 'Sort: Best FCR (Lowest)' | t }}</option>
        </select>
      </div>

      <!-- Empty State -->
      <app-empty-state 
        *ngIf="!loading() && !error() && (!result()?.items || result()?.items?.length === 0)"
        icon="pets"
        [title]="'No Breeds Found' | t"
        [description]="'Get started by adding your first livestock breed.' | t"
        [actionLabel]="'Add Breed' | t"
        (action)="openSetupDialog()">
      </app-empty-state>

      <!-- Breeds Grid -->
      <div *ngIf="!loading() && result()?.items?.length" class="p-4 sm:p-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        @for (breed of result()?.items; track breed.id) {
          <div (click)="goToDetails(breed.id)" (keydown.enter)="goToDetails(breed.id)" role="link" tabindex="0" [attr.aria-label]="'Open breed {name}' | t: { name: breed.name }" class="group relative bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-sm hover:shadow-md transition-shadow overflow-hidden flex flex-col cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-500">

            <!-- Card Header -->
            <div class="px-5 pt-5 pb-3 flex items-start justify-between gap-2">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 shrink-0 rounded-xl bg-primary-50 text-primary-600 dark:bg-primary-900/30 dark:text-primary-400 flex items-center justify-center">
                  <mat-icon class="!w-5 !h-5 !text-[20px]">pets</mat-icon>
                </div>
                <div>
                  <h3 class="font-bold text-gray-900 dark:text-white text-base leading-tight group-hover:text-primary-600 dark:group-hover:text-primary-400 transition-colors">{{ breed.name }}</h3>
                  <span class="inline-flex items-center mt-0.5 text-xs text-gray-500 dark:text-gray-400">
                    {{ breed.category | t }} • {{ breed.mainPurpose | t }}
                  </span>
                </div>
              </div>

              <div class="flex items-center shrink-0 -mr-2 -mt-1">
                <button [attr.aria-label]="'Edit Breed' | t" (click)="openSetupDialog(breed); $event.stopPropagation()" class="w-9 h-9 inline-flex items-center justify-center text-gray-400 hover:text-gray-700 hover:bg-gray-100 dark:hover:text-gray-200 dark:hover:bg-gray-700 rounded-lg transition-colors" [title]="'Edit Breed' | t">
                  <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">edit</mat-icon>
                </button>
                <button [attr.aria-label]="'Delete Breed' | t" (click)="onDelete(breed, $event)" class="w-9 h-9 inline-flex items-center justify-center text-gray-400 hover:text-red-600 hover:bg-red-50 dark:hover:text-red-400 dark:hover:bg-red-900/30 rounded-lg transition-colors" [title]="'Delete Breed' | t">
                  <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">delete</mat-icon>
                </button>
              </div>
            </div>

            <!-- Card Body -->
            <div class="px-5 pb-5 flex-1 flex flex-col">
              <p *ngIf="descriptionOf(breed) as desc" class="text-sm text-gray-500 dark:text-gray-400 mb-3 line-clamp-2">{{ desc }}</p>

              <dl class="mt-auto grid grid-cols-2 gap-3 px-3 py-2.5 bg-gray-50 dark:bg-gray-900/50 rounded-xl">
                <div>
                  <dt class="text-[11px] text-gray-500 dark:text-gray-400">{{ 'Daily gain (ADG)' | t }}</dt>
                  <dd class="font-semibold text-gray-900 dark:text-white text-sm mt-0.5 tabular-nums">{{ breed.standardAdgMin }}–{{ breed.standardAdgMax }} {{ 'kg/day' | t }}</dd>
                </div>
                <div>
                  <dt class="text-[11px] text-gray-500 dark:text-gray-400" [title]="'Kg of feed needed for 1 kg of weight gain (FCR)' | t">{{ 'Feed per kg gain (FCR)' | t }}</dt>
                  <dd class="font-semibold text-gray-900 dark:text-white text-sm mt-0.5 tabular-nums">{{ breed.fcrMin }}–{{ breed.fcrMax }} {{ 'kg' | t }}</dd>
                </div>
              </dl>
            </div>
          </div>
        }
      </div>

      <!-- Pagination Footer -->
      <div *ngIf="!loading() && result()?.items?.length" class="px-4 sm:px-6 py-4 border-t border-gray-100 dark:border-gray-800/50 flex flex-col sm:flex-row items-center justify-between gap-4 relative z-10">
        <div class="text-sm text-gray-500 dark:text-gray-400 font-medium">
          {{ 'Showing {from} to {to} of {total} breeds' | t: { from: pageStart(), to: pageEnd(), total: result()?.totalCount } }}
        </div>
        <div class="flex items-center gap-4">
          <div class="flex items-center gap-2">
            <label class="text-sm text-gray-500 dark:text-gray-400">{{ 'Rows per page:' | t }}</label>
            <select [ngModel]="params().pageSize" (ngModelChange)="onPageSizeChange($event)"
              class="px-2 py-1 text-sm rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
              <option [ngValue]="10">10</option>
              <option [ngValue]="20">20</option>
              <option [ngValue]="50">50</option>
              <option [ngValue]="100">100</option>
            </select>
          </div>
          <div class="flex items-center gap-2">
            <button [attr.aria-label]="'Previous' | t" (click)="prevPage()" [disabled]="!result()?.hasPreviousPage"
                  class="inline-flex items-center justify-center p-2 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors shadow-sm">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">chevron_left</mat-icon>
          </button>
          <button [attr.aria-label]="'Next' | t" (click)="nextPage()" [disabled]="!result()?.hasNextPage"
                  class="inline-flex items-center justify-center p-2 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors shadow-sm">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">chevron_right</mat-icon>
          </button>
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BreedList {
  private readonly breedService = inject(BreedService);
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);

  /** The breed's description without a repeated "Name - " prefix (seed data often starts with the name). */
  descriptionOf(breed: BreedDto): string {
    const d = (breed.description ?? '').trim();
    if (breed.name && d.toLowerCase().startsWith(breed.name.toLowerCase())) {
      const rest = d.slice(breed.name.length);
      const stripped = rest.replace(/^\s*[-–—:]\s*/, '');
      if (stripped !== rest) return stripped.trim();
    }
    return d;
  }

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  retryLoad(): void {
    this.error.set(null);
    this.refreshTrigger.update(n => n + 1);
  }
  readonly searchTerm = signal('');
  readonly params = signal<BreedParams>({ pageNumber: 1, pageSize: 20 });
  private readonly refreshTrigger = signal(0);

  constructor() {
    // Initialize from URL Query Parameters
    const qp = this.route.snapshot.queryParams;
    const initialSearch = qp['search'] || '';
    const initialCategory = qp['category'] || undefined;
    const initialPurpose = qp['mainPurpose'] || undefined;
    const initialPage = qp['pageNumber'] ? parseInt(qp['pageNumber'], 10) : 1;
    const initialPageSize = qp['pageSize'] ? parseInt(qp['pageSize'], 10) : 20;
    const initialSortBy = qp['sortBy'] || undefined;
    const initialSortDesc = qp['sortDesc'] === 'true';

    if (initialSearch) {
      this.searchTerm.set(initialSearch);
    }

    this.params.set({
      pageNumber: initialPage,
      pageSize: initialPageSize,
      search: initialSearch || undefined,
      category: initialCategory,
      mainPurpose: initialPurpose,
      sortBy: initialSortBy,
      sortDesc: initialSortDesc
    });

    // Debounce search input
    toObservable(this.searchTerm).pipe(
      skip(1),
      debounceTime(350),
      distinctUntilChanged(),
      takeUntilDestroyed()
    ).subscribe(term => {
      this.params.update(p => ({ ...p, search: term || undefined, pageNumber: 1 }));
    });

    // Synchronize URL query parameters
    toObservable(this.params).pipe(
      takeUntilDestroyed()
    ).subscribe(p => {
      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: {
          pageNumber: p.pageNumber && p.pageNumber > 1 ? p.pageNumber : null,
          pageSize: p.pageSize && p.pageSize !== 20 ? p.pageSize : null,
          search: p.search || null,
          category: p.category || null,
          mainPurpose: p.mainPurpose || null,
          sortBy: p.sortBy || null,
          sortDesc: p.sortDesc ? true : null
        },
        queryParamsHandling: 'merge',
        replaceUrl: true
      });
    });
  }

  private combinedParams = computed(() => ({
    params: this.params(),
    refresh: this.refreshTrigger()
  }));

  readonly result = toSignal(
    toObservable(this.combinedParams).pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(({ params }) => this.breedService.getBreeds(params).pipe(
        catchError(e => {
          this.error.set(parseApiError(e, this.i18n.t('Could not load breeds.')));
          return of(null);
        })
      )),
      tap(() => this.loading.set(false))
    )
  );

  readonly currentSortKey = computed(() => {
    const p = this.params();
    if (!p.sortBy) return 'name_asc';
    if (p.sortBy === 'name') return p.sortDesc ? 'name_desc' : 'name_asc';
    if (p.sortBy === 'standardadg') return p.sortDesc ? 'adg_desc' : 'adg_asc';
    if (p.sortBy === 'fcr') return p.sortDesc ? 'fcr_desc' : 'fcr_asc';
    return 'name_asc';
  });

  readonly pageStart = computed(() => {
    const res = this.result();
    if (!res || res.totalCount === 0) return 0;
    return (res.pageNumber - 1) * res.pageSize + 1;
  });

  readonly pageEnd = computed(() => {
    const res = this.result();
    if (!res || res.totalCount === 0) return 0;
    return Math.min(res.pageNumber * res.pageSize, res.totalCount);
  });

  onSearchChange(term: string): void {
    this.searchTerm.set(term);
  }

  onCategoryChange(category: string | null): void {
    this.params.update(p => ({ ...p, category: category ?? undefined, pageNumber: 1 }));
  }

  onPurposeChange(purpose: string | null): void {
    this.params.update(p => ({ ...p, mainPurpose: purpose ?? undefined, pageNumber: 1 }));
  }

  onSortChange(key: string): void {
    let sortBy: string | undefined;
    let sortDesc = false;

    switch (key) {
      case 'name_asc': sortBy = 'name'; sortDesc = false; break;
      case 'name_desc': sortBy = 'name'; sortDesc = true; break;
      case 'adg_desc': sortBy = 'standardadg'; sortDesc = true; break;
      case 'fcr_asc': sortBy = 'fcr'; sortDesc = false; break;
    }

    this.params.update(p => ({ ...p, sortBy, sortDesc, pageNumber: 1 }));
  }

  onPageSizeChange(size: number): void {
    this.params.update(p => ({ ...p, pageSize: size, pageNumber: 1 }));
  }

  prevPage(): void {
    const res = this.result();
    if (res && res.hasPreviousPage) {
      this.params.update(p => ({ ...p, pageNumber: (p.pageNumber || 1) - 1 }));
    }
  }

  nextPage(): void {
    const res = this.result();
    if (res && res.hasNextPage) {
      this.params.update(p => ({ ...p, pageNumber: (p.pageNumber || 1) + 1 }));
    }
  }

  reload(): void {
    this.refreshTrigger.update(n => n + 1);
  }

  goToDetails(id: string): void {
    if (this.dialog.openDialogs.length > 0) return;

    this.dialog.open(BreedDetailDialogComponent, { disableClose: true,
      width: '720px',
      maxWidth: '95vw',
      panelClass: ['!rounded-2xl', '!bg-white', 'dark:!bg-gray-900'],
      data: { id }
    });
  }

  openSetupDialog(breed?: BreedDto): void {
    if (this.dialog.openDialogs.length > 0) return;

    const dialogRef = this.dialog.open(BreedSetupDialogComponent, { disableClose: true,
      width: '720px',
      maxWidth: '95vw',
      panelClass: ['!rounded-2xl', '!bg-white', 'dark:!bg-gray-900'],
      data: { breed }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.reload();
      }
    });
  }

  onDelete(breed: BreedDto, event: Event): void {
    event.stopPropagation();
    
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '450px',
      panelClass: ['!rounded-2xl', '!bg-white', 'dark:!bg-gray-900'],
      data: {
        title: this.i18n.t('Delete Breed'),
        message: this.i18n.t('Are you sure you want to delete "{name}"? This action cannot be undone.', { name: breed.name }),
        confirmButtonText: this.i18n.t('Delete'),
        cancelButtonText: this.i18n.t('Cancel'),
        isDestructive: true
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        // We can optionally show a global loading state here, but for simplicity we rely on the list reloading
        this.breedService.deleteBreed(breed.id).subscribe({
          next: () => this.reload(),
          error: (err) => {
            console.error('Failed to delete breed', err);
            // Ideally a toast notification would be shown here, since the list doesn't have an inline error banner for individual cards
          }
        });
      }
    });
  }
}
