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
import { BreedSetupDialogComponent } from '../../components/dialogs/breed-setup-dialog/breed-setup-dialog.component';
import { BreedDetailDialogComponent } from '../../components/dialogs/breed-detail-dialog/breed-detail-dialog.component';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { toObservable, toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, debounceTime, distinctUntilChanged, tap, skip } from 'rxjs/operators';
import { of } from 'rxjs';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';

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
    EmptyStateComponent
  ],
  template: `
    <app-page-header
      title="Breed Management"
      description="Manage livestock breeds, growth targets, and intelligence baselines."
      breadcrumbActiveNode="Breeds">
      <div actions>
        <button (click)="openSetupDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> Add Breed
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative min-h-[400px] mt-6 mx-6 mb-6">
      <app-loading *ngIf="loading()" [overlay]="true"></app-loading>

      <!-- Filters & Sorting Toolbar -->
      <div class="p-4 border-b border-gray-100 dark:border-gray-800 flex flex-col sm:flex-row items-center gap-4 bg-gray-50/50 dark:bg-gray-900/30">
        <div class="relative w-full sm:w-72">
          <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 !text-[18px] !w-[18px] !h-[18px]">search</mat-icon>
          <input [ngModel]="searchTerm()" (ngModelChange)="onSearchChange($event)"
            placeholder="Search breeds..."
            class="w-full pl-9 pr-4 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all" />
        </div>

        <select [ngModel]="params().category ?? null" (ngModelChange)="onCategoryChange($event)"
          class="w-full sm:w-48 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option [ngValue]="null">All Categories</option>
          <option value="Indigenous">Indigenous</option>
          <option value="Exotic">Exotic</option>
          <option value="Crossbred">Crossbred</option>
        </select>

        <select [ngModel]="params().mainPurpose ?? null" (ngModelChange)="onPurposeChange($event)"
          class="w-full sm:w-48 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option [ngValue]="null">All Purposes</option>
          <option value="Dairy">Dairy</option>
          <option value="Beef">Beef</option>
          <option value="Dual-purpose">Dual-purpose</option>
        </select>

        <select [ngModel]="currentSortKey()" (ngModelChange)="onSortChange($event)"
          class="w-full sm:w-52 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option value="name_asc">Sort: Name (A-Z)</option>
          <option value="name_desc">Sort: Name (Z-A)</option>
          <option value="adg_desc">Sort: Highest ADG</option>
          <option value="fcr_asc">Sort: Best FCR (Lowest)</option>
        </select>
      </div>

      <!-- Empty State -->
      <app-empty-state 
        *ngIf="!loading() && (!result()?.items || result()?.items?.length === 0)"
        icon="pets"
        title="No Breeds Found"
        description="Get started by adding your first livestock breed."
        actionLabel="Add Breed"
        (action)="openSetupDialog()">
      </app-empty-state>

      <!-- Breeds Grid -->
      <div *ngIf="!loading() && result()?.items?.length" class="p-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        @for (breed of result()?.items; track breed.id) {
          <div (click)="goToDetails(breed.id)" class="group relative bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 shadow-sm hover:shadow-xl transition-all duration-300 overflow-hidden transform hover:-translate-y-1 flex flex-col justify-between cursor-pointer">
            <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-emerald-500/5 rotate-[-10deg] pointer-events-none transition-transform duration-500 group-hover:scale-110">pets</mat-icon>

            <!-- Card Header -->
            <div class="p-5 flex items-start justify-between border-b border-gray-50 dark:border-gray-700/50 relative z-10">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20 group-hover:scale-110 transition-transform duration-300">
                  <mat-icon class="!w-5 !h-5 !text-[20px]">pets</mat-icon>
                </div>
                <div>
                  <h3 class="font-bold text-gray-900 dark:text-white text-base leading-tight group-hover:text-emerald-600 transition-colors">{{ breed.name }}</h3>
                  <span class="inline-flex items-center mt-1 text-xs font-semibold text-gray-400">
                    {{ breed.category }} • {{ breed.mainPurpose }}
                  </span>
                </div>
              </div>

              <div class="flex items-center gap-1">
                <button (click)="openSetupDialog(breed); $event.stopPropagation()" class="p-1.5 text-gray-400 hover:text-emerald-600 hover:bg-emerald-50 dark:hover:bg-emerald-900/30 rounded-lg transition-colors" title="Edit Breed">
                  <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">edit</mat-icon>
                </button>
                <button (click)="onDelete(breed, $event)" class="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-900/30 rounded-lg transition-colors" title="Delete Breed">
                  <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">delete</mat-icon>
                </button>
              </div>
            </div>

            <!-- Card Body -->
            <div class="p-5 flex-1 relative z-10">
              <p class="text-sm text-gray-500 dark:text-gray-400 mb-4 line-clamp-2">{{ breed.description || 'No description provided.' }}</p>

              <div class="grid grid-cols-2 gap-3 p-3 bg-gray-50/80 dark:bg-gray-900/50 rounded-xl border border-gray-100 dark:border-gray-800 mb-2">
                <div>
                  <div class="text-[10px] uppercase tracking-wider font-bold text-gray-400">Standard ADG</div>
                  <div class="font-extrabold text-gray-900 dark:text-white text-base mt-0.5">{{ breed.standardAdgMin }} - {{ breed.standardAdgMax }} kg</div>
                </div>
                <div>
                  <div class="text-[10px] uppercase tracking-wider font-bold text-gray-400">Target FCR</div>
                  <div class="font-bold text-emerald-600 dark:text-emerald-400 text-sm mt-0.5">{{ breed.fcrMin }} - {{ breed.fcrMax }}</div>
                </div>
              </div>
            </div>
          </div>
        }
      </div>

      <!-- Pagination Footer -->
      <div *ngIf="!loading() && result()?.items?.length" class="px-6 py-4 border-t border-gray-100 dark:border-gray-800/50 bg-gray-50/50 dark:bg-gray-900/30 flex flex-col sm:flex-row items-center justify-between gap-4 relative z-10">
        <div class="text-sm text-gray-500 dark:text-gray-400 font-medium">
          Showing <span class="font-bold text-gray-900 dark:text-white">{{ pageStart() }}</span> to <span class="font-bold text-gray-900 dark:text-white">{{ pageEnd() }}</span> of <span class="font-bold text-gray-900 dark:text-white">{{ result()?.totalCount }}</span> breeds
        </div>
        <div class="flex items-center gap-4">
          <div class="flex items-center gap-2">
            <label class="text-sm text-gray-500 dark:text-gray-400">Rows per page:</label>
            <select [ngModel]="params().pageSize" (ngModelChange)="onPageSizeChange($event)"
              class="px-2 py-1 text-sm rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
              <option [ngValue]="10">10</option>
              <option [ngValue]="20">20</option>
              <option [ngValue]="50">50</option>
              <option [ngValue]="100">100</option>
            </select>
          </div>
          <div class="flex items-center gap-2">
            <button (click)="prevPage()" [disabled]="!result()?.hasPreviousPage"
                  class="inline-flex items-center justify-center p-2 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors shadow-sm">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">chevron_left</mat-icon>
          </button>
          <button (click)="nextPage()" [disabled]="!result()?.hasNextPage"
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

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
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
          this.error.set(e?.error?.detail ?? 'Failed to load breeds');
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
    
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, { disableClose: true,
      width: '450px',
      panelClass: ['!rounded-2xl', '!bg-white', 'dark:!bg-gray-900'],
      data: {
        title: 'Delete Breed',
        message: `Are you sure you want to delete "${breed.name}"? This action cannot be undone.`,
        confirmButtonText: 'Delete',
        cancelButtonText: 'Cancel',
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
