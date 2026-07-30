import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { toObservable, toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, debounceTime, distinctUntilChanged, tap } from 'rxjs/operators';
import { of } from 'rxjs';

import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { Supplier, SupplierParams } from '../../models/inventory.models';
import { CreateSupplierDialogComponent } from '../../components/dialogs/create-supplier-dialog/create-supplier-dialog.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-supplier-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Suppliers & Vendor Management"
      description="Manage feed mills, pharmaceutical companies, veterinary product suppliers, and procurement contacts."
      breadcrumbActiveNode="Suppliers">
      <div actions>
        <button (click)="openCreateSupplierDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> Add Supplier
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative min-h-[400px]">
      <app-loading *ngIf="loading()" [overlay]="true"></app-loading>

      <!-- Filters & Sorting Toolbar -->
      <div class="p-4 border-b border-gray-100 dark:border-gray-800 flex flex-col sm:flex-row items-center gap-4 bg-gray-50/50 dark:bg-gray-900/30">
        <div class="relative w-full sm:w-72">
          <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 !text-[18px] !w-[18px] !h-[18px]">search</mat-icon>
          <input [ngModel]="searchTerm()" (ngModelChange)="onSearchChange($event)"
            placeholder="Search suppliers by name or phone..."
            class="w-full pl-9 pr-4 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all" />
        </div>

        <select [ngModel]="currentSortKey()" (ngModelChange)="onSortChange($event)"
          class="w-full sm:w-52 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option value="name_asc">Sort: Name (A-Z)</option>
          <option value="name_desc">Sort: Name (Z-A)</option>
        </select>
      </div>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!loading() && (!result()?.items || result()?.items?.length === 0)"
        icon="local_shipping"
        title="No suppliers registered"
        description="Add suppliers to associate them with incoming stock purchases and receipts."
        actionLabel="Add Supplier"
        (action)="openCreateSupplierDialog()">
      </app-empty-state>

      <!-- Suppliers Grid -->
      <div *ngIf="!loading() && result()?.items?.length" class="p-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        @for (sup of result()?.items; track sup.id) {
          <div class="group relative bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 shadow-sm hover:shadow-xl transition-all duration-300 overflow-hidden transform hover:-translate-y-1 p-5 flex flex-col justify-between">
            <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-emerald-500/5 rotate-[-10deg] pointer-events-none transition-transform duration-500 group-hover:scale-110">local_shipping</mat-icon>

            <div>
              <div class="flex items-center justify-between mb-3">
                <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20 group-hover:scale-110 transition-transform duration-300">
                  <mat-icon class="!w-5 !h-5 !text-[20px]">local_shipping</mat-icon>
                </div>
                <div class="flex items-center gap-1">
                  <button (click)="openEditDialog(sup); $event.stopPropagation()" class="px-2.5 py-1 text-xs font-semibold text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors inline-flex items-center gap-1">
                    <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">edit</mat-icon> Edit
                  </button>
                  <button (click)="onDelete(sup, $event)" class="p-1 text-gray-400 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-900/30 rounded-lg transition-colors">
                    <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">delete</mat-icon>
                  </button>
                </div>
              </div>

              <h3 class="font-bold text-gray-900 dark:text-white text-base leading-tight group-hover:text-purple-600 transition-colors">{{ sup.name }}</h3>

              <div class="mt-3 space-y-1.5 text-xs text-gray-500 dark:text-gray-400">
                <div *ngIf="sup.contactPerson" class="flex items-center gap-2">
                  <mat-icon class="text-gray-400 !text-[16px] !w-[16px] !h-[16px]">person</mat-icon> {{ sup.contactPerson }}
                </div>
                <div *ngIf="sup.phone" class="flex items-center gap-2">
                  <mat-icon class="text-gray-400 !text-[16px] !w-[16px] !h-[16px]">phone</mat-icon> {{ sup.phone }}
                </div>
                <div *ngIf="sup.email" class="flex items-center gap-2">
                  <mat-icon class="text-gray-400 !text-[16px] !w-[16px] !h-[16px]">email</mat-icon> {{ sup.email }}
                </div>
                <div *ngIf="sup.address" class="flex items-start gap-2">
                  <mat-icon class="text-gray-400 !text-[16px] !w-[16px] !h-[16px] mt-0.5">location_on</mat-icon> {{ sup.address }}
                </div>
              </div>
            </div>

            <div *ngIf="sup.notes" class="mt-4 p-2.5 rounded-xl bg-gray-50 dark:bg-gray-900/50 text-xs text-gray-500 dark:text-gray-400 border border-gray-100 dark:border-gray-800 italic">
              "{{ sup.notes }}"
            </div>
          </div>
        }
      </div>

      <!-- Pagination Footer -->
      <div *ngIf="!loading() && result()?.items?.length" class="px-6 py-4 border-t border-gray-100 dark:border-gray-800/50 bg-gray-50/50 dark:bg-gray-900/30 flex flex-col sm:flex-row items-center justify-between gap-4 relative z-10">
        <div class="text-sm text-gray-500 dark:text-gray-400 font-medium">
          Showing <span class="font-bold text-gray-900 dark:text-white">{{ pageStart() }}</span> to <span class="font-bold text-gray-900 dark:text-white">{{ pageEnd() }}</span> of <span class="font-bold text-gray-900 dark:text-white">{{ result()?.totalCount }}</span> suppliers
        </div>
        <div class="flex items-center gap-4">
          <!-- Page Size Filter -->
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
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SupplierListComponent {
  private readonly inventoryService = inject(InventoryService);
  private readonly contextService = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly params = signal<SupplierParams>({ pageNumber: 1, pageSize: 20 });
  private readonly refreshTrigger = signal(0);

  constructor() {
    // 1. Initialize from URL Query Parameters
    const qp = this.route.snapshot.queryParams;
    const initialSearch = qp['search'] || '';
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
      sortBy: initialSortBy,
      sortDesc: initialSortDesc
    });

    // 2. Dynamic WorkingContextService binding for FarmId
    this.contextService.currentFarm$.pipe(
      takeUntilDestroyed()
    ).subscribe(farm => {
      this.params.update(p => ({ ...p, farmId: farm?.id || undefined }));
    });

    // 3. Debounce search input
    toObservable(this.searchTerm).pipe(
      debounceTime(350),
      distinctUntilChanged(),
      takeUntilDestroyed()
    ).subscribe(term => {
      this.params.update(p => ({ ...p, search: term || undefined, pageNumber: 1 }));
    });

    // 4. Synchronize URL query parameters
    toObservable(this.params).pipe(
      takeUntilDestroyed()
    ).subscribe(p => {
      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: {
          pageNumber: p.pageNumber && p.pageNumber > 1 ? p.pageNumber : null,
          pageSize: p.pageSize && p.pageSize !== 20 ? p.pageSize : null,
          search: p.search || null,
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
      switchMap(({ params }) => this.inventoryService.getSuppliers(params).pipe(
        catchError(e => {
          this.error.set(e?.error?.detail ?? 'Failed to load suppliers');
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

  onSortChange(key: string): void {
    let sortBy: string | undefined;
    let sortDesc = false;

    switch (key) {
      case 'name_asc': sortBy = 'name'; sortDesc = false; break;
      case 'name_desc': sortBy = 'name'; sortDesc = true; break;
    }

    this.params.update(p => ({ ...p, sortBy, sortDesc, pageNumber: 1 }));
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

  onPageSizeChange(pageSize: number): void {
    this.params.update(p => ({ ...p, pageSize, pageNumber: 1 }));
  }

  reload(): void {
    this.refreshTrigger.update(n => n + 1);
  }

  openCreateSupplierDialog(): void {
    const dialogRef = this.dialog.open(CreateSupplierDialogComponent, {
      width: '600px'
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.reload();
    });
  }

  openEditDialog(supplier: Supplier): void {
    const dialogRef = this.dialog.open(CreateSupplierDialogComponent, {
      width: '600px',
      data: supplier
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.reload();
    });
  }

  onDelete(supplier: Supplier, event: Event): void {
    event.stopPropagation();
    
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      width: '450px',
      panelClass: ['!rounded-2xl', '!bg-white', 'dark:!bg-gray-900'],
      data: {
        title: 'Delete Supplier',
        message: `Are you sure you want to delete supplier "${supplier.name}"? This action cannot be undone.`,
        confirmButtonText: 'Delete',
        cancelButtonText: 'Cancel',
        isDestructive: true
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.inventoryService.deleteSupplier(supplier.id).subscribe({
          next: () => this.reload(),
          error: (err) => {
            console.error('Failed to delete supplier', err);
            this.error.set(err?.error?.detail || 'Failed to delete supplier');
          }
        });
      }
    });
  }
}
