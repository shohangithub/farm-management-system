import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { toObservable, toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, debounceTime, distinctUntilChanged, tap, filter } from 'rxjs/operators';
import { of } from 'rxjs';

import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import {
  InventoryCategory, InventoryCategoryNames, InventoryItem, InventoryStatus, InventoryItemParams
} from '../../models/inventory.models';
import { CreateItemDialogComponent } from '../../components/dialogs/create-item-dialog/create-item-dialog.component';
import { StockInDialogComponent } from '../../components/dialogs/stock-in-dialog/stock-in-dialog.component';
import { StockOutDialogComponent } from '../../components/dialogs/stock-out-dialog/stock-out-dialog.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-inventory-item-list',
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
      title="Stock Catalog & Inventory Items"
      description="Manage feed, veterinary medicines, chemicals, equipment, stock thresholds, and unit costs."
      breadcrumbActiveNode="Stock Catalog">
      <div actions>
        <button (click)="openCreateItemDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> Add Inventory Item
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
            placeholder="Search items by name or SKU..."
            class="w-full pl-9 pr-4 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all" />
        </div>

        <select [ngModel]="params().category ?? null" (ngModelChange)="onCategoryChange($event)"
          class="w-full sm:w-56 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option [ngValue]="null">All Categories</option>
          @for (cat of categoryOptions; track cat.value) {
            <option [ngValue]="cat.value">{{ cat.label }}</option>
          }
        </select>

        <select [ngModel]="params().status ?? null" (ngModelChange)="onStatusChange($event)"
          class="w-full sm:w-48 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option [ngValue]="null">All Statuses</option>
          <option [ngValue]="InventoryStatus.Sufficient">Sufficient</option>
          <option [ngValue]="InventoryStatus.LowStock">Low Stock</option>
          <option [ngValue]="InventoryStatus.OutOfStock">Out of Stock</option>
        </select>

        <select [ngModel]="currentSortKey()" (ngModelChange)="onSortChange($event)"
          class="w-full sm:w-52 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option value="name_asc">Sort: Name (A-Z)</option>
          <option value="name_desc">Sort: Name (Z-A)</option>
          <option value="stock_desc">Sort: Highest Stock</option>
          <option value="stock_asc">Sort: Lowest Stock</option>
          <option value="value_desc">Sort: Highest Total Value</option>
        </select>
      </div>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!loading() && (!result()?.items || result()?.items?.length === 0)"
        icon="inventory_2"
        title="No inventory items found"
        description="Add your first stock item to start monitoring farm inventory."
        actionLabel="Add Inventory Item"
        (action)="openCreateItemDialog()">
      </app-empty-state>

      <!-- Items Grid -->
      <div *ngIf="!loading() && result()?.items?.length" class="p-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        @for (item of result()?.items; track item.id) {
          <div class="group relative bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 shadow-sm hover:shadow-xl transition-all duration-300 overflow-hidden transform hover:-translate-y-1 flex flex-col justify-between">
            <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-emerald-500/5 rotate-[-10deg] pointer-events-none transition-transform duration-500 group-hover:scale-110">inventory_2</mat-icon>

            <!-- Card Header -->
            <div class="p-5 flex items-start justify-between border-b border-gray-50 dark:border-gray-700/50 relative z-10">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20 group-hover:scale-110 transition-transform duration-300">
                  <mat-icon class="!w-5 !h-5 !text-[20px]">inventory_2</mat-icon>
                </div>
                <div>
                  <h3 class="font-bold text-gray-900 dark:text-white text-base leading-tight group-hover:text-emerald-600 transition-colors">{{ item.name }}</h3>
                  <span class="inline-flex items-center mt-1 text-xs font-semibold text-gray-400">
                    SKU: {{ item.sku }} • {{ item.categoryName }}
                  </span>
                </div>
              </div>

              <span class="inline-flex items-center px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider shadow-sm"
                [ngClass]="{
                  'bg-emerald-50 text-emerald-700 border border-emerald-200': item.status === InventoryStatus.Sufficient,
                  'bg-amber-50 text-amber-700 border border-amber-200': item.status === InventoryStatus.LowStock,
                  'bg-red-50 text-red-700 border border-red-200': item.status === InventoryStatus.OutOfStock,
                  'bg-blue-50 text-blue-700 border border-blue-200': item.status === InventoryStatus.Excess
                }">
                {{ item.statusName }}
              </span>
            </div>

            <!-- Card Body -->
            <div class="p-5 flex-1 relative z-10">
              <div class="grid grid-cols-2 gap-3 p-3 bg-gray-50/80 dark:bg-gray-900/50 rounded-xl border border-gray-100 dark:border-gray-800 mb-4">
                <div>
                  <div class="text-[10px] uppercase tracking-wider font-bold text-gray-400">Current Stock</div>
                  <div class="font-extrabold text-gray-900 dark:text-white text-base mt-0.5">{{ item.currentStock }} {{ item.unitOfMeasure }}</div>
                </div>
                <div>
                  <div class="text-[10px] uppercase tracking-wider font-bold text-gray-400">Reorder Level</div>
                  <div class="font-bold text-gray-600 dark:text-gray-400 text-sm mt-0.5">{{ item.reorderThreshold }} {{ item.unitOfMeasure }}</div>
                </div>
              </div>

              <div class="flex items-center justify-between text-xs mb-1 text-gray-400">
                <span>Weighted Avg Cost</span>
                <span class="font-bold text-emerald-600 dark:text-emerald-400">৳ {{ item.weightedAverageCostBdt }} / {{ item.unitOfMeasure }}</span>
              </div>
              <div class="flex items-center justify-between text-xs text-gray-400">
                <span>Total Value</span>
                <span class="font-extrabold text-gray-900 dark:text-white">৳ {{ item.totalValueBdt | number:'1.0-0' }}</span>
              </div>
            </div>

            <!-- Footer Action -->
            <div class="p-3 bg-gray-50/80 dark:bg-gray-800/80 border-t border-gray-100 dark:border-gray-700/50 flex items-center justify-between relative z-10">
              <button (click)="openStockInDialog(item)" class="px-3 py-1.5 text-xs font-semibold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 hover:bg-emerald-100 dark:hover:bg-emerald-900/50 rounded-lg border border-emerald-200 dark:border-emerald-800 transition-colors inline-flex items-center gap-1">
                <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">add_business</mat-icon> Stock In
              </button>
              <div class="flex items-center gap-1">
                <button (click)="openStockOutDialog(item)" class="px-2.5 py-1.5 text-xs font-semibold text-amber-700 dark:text-amber-300 hover:bg-amber-50 dark:hover:bg-amber-950/30 rounded-lg border border-amber-200 dark:border-amber-800 transition-colors inline-flex items-center gap-1">
                  <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">remove</mat-icon> Out
                </button>
                <button (click)="openEditDialog(item)" class="px-2.5 py-1.5 text-xs font-semibold text-gray-700 dark:text-gray-300 hover:bg-white dark:hover:bg-gray-700 rounded-lg border border-gray-200 dark:border-gray-700 transition-colors inline-flex items-center gap-1">
                  <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">edit</mat-icon> Edit
                </button>
                <button (click)="onDelete(item, $event)" class="px-2 py-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-900/30 rounded-lg border border-transparent transition-colors">
                  <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">delete</mat-icon>
                </button>
              </div>
            </div>
          </div>
        }
      </div>

      <!-- Pagination Footer -->
      <div *ngIf="!loading() && result()?.items?.length" class="px-6 py-4 border-t border-gray-100 dark:border-gray-800/50 bg-gray-50/50 dark:bg-gray-900/30 flex flex-col sm:flex-row items-center justify-between gap-4 relative z-10">
        <div class="text-sm text-gray-500 dark:text-gray-400 font-medium">
          Showing <span class="font-bold text-gray-900 dark:text-white">{{ pageStart() }}</span> to <span class="font-bold text-gray-900 dark:text-white">{{ pageEnd() }}</span> of <span class="font-bold text-gray-900 dark:text-white">{{ result()?.totalCount }}</span> items
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
export class InventoryItemListComponent {
  private readonly inventoryService = inject(InventoryService);
  private readonly contextService = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly params = signal<InventoryItemParams>({ pageNumber: 1, pageSize: 20 });
  private readonly refreshTrigger = signal(0);

  readonly InventoryStatus = InventoryStatus;

  readonly categoryOptions = [
    { value: InventoryCategory.Feed, label: InventoryCategoryNames[InventoryCategory.Feed] },
    { value: InventoryCategory.Medicine, label: InventoryCategoryNames[InventoryCategory.Medicine] },
    { value: InventoryCategory.Vaccine, label: InventoryCategoryNames[InventoryCategory.Vaccine] },
    { value: InventoryCategory.Chemical, label: InventoryCategoryNames[InventoryCategory.Chemical] },
    { value: InventoryCategory.Equipment, label: InventoryCategoryNames[InventoryCategory.Equipment] },
    { value: InventoryCategory.Other, label: InventoryCategoryNames[InventoryCategory.Other] },
  ];

  constructor() {
    // 1. Initialize from URL Query Parameters
    const qp = this.route.snapshot.queryParams;
    const initialSearch = qp['search'] || '';
    const initialCategory = qp['category'] != null ? parseInt(qp['category'], 10) : undefined;
    const initialStatus = qp['status'] != null ? parseInt(qp['status'], 10) : undefined;
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
      status: initialStatus,
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
          category: p.category ?? null,
          status: p.status ?? null,
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
      filter(({ params }) => !!params.farmId),
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(({ params }) => this.inventoryService.getItems(params).pipe(
        catchError(e => {
          this.error.set(e?.error?.detail ?? 'Failed to load inventory items');
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
    if (p.sortBy === 'currentStock') return p.sortDesc ? 'stock_desc' : 'stock_asc';
    if (p.sortBy === 'totalValueBdt') return 'value_desc';
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

  onCategoryChange(category: InventoryCategory | null): void {
    this.params.update(p => ({ ...p, category: category ?? undefined, pageNumber: 1 }));
  }

  onStatusChange(status: InventoryStatus | null): void {
    this.params.update(p => ({ ...p, status: status ?? undefined, pageNumber: 1 }));
  }

  onSortChange(key: string): void {
    let sortBy: string | undefined;
    let sortDesc = false;

    switch (key) {
      case 'name_asc': sortBy = 'name'; sortDesc = false; break;
      case 'name_desc': sortBy = 'name'; sortDesc = true; break;
      case 'stock_desc': sortBy = 'currentStock'; sortDesc = true; break;
      case 'stock_asc': sortBy = 'currentStock'; sortDesc = false; break;
      case 'value_desc': sortBy = 'totalValueBdt'; sortDesc = true; break;
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

  openCreateItemDialog(): void {
    const farmId = this.params().farmId;
    if (!farmId) return;
    const dialogRef = this.dialog.open(CreateItemDialogComponent, {
      width: '600px',
      data: { farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.reload();
    });
  }

  openEditDialog(item: InventoryItem): void {
    const dialogRef = this.dialog.open(CreateItemDialogComponent, {
      width: '600px',
      data: { item, farmId: item.farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.reload();
    });
  }

  openStockInDialog(item: InventoryItem): void {
    const dialogRef = this.dialog.open(StockInDialogComponent, {
      width: '600px',
      data: { item, farmId: item.farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.reload();
    });
  }

  openStockOutDialog(item: InventoryItem): void {
    const dialogRef = this.dialog.open(StockOutDialogComponent, {
      width: '600px',
      data: { item, farmId: item.farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.reload();
    });
  }

  onDelete(item: InventoryItem, event: Event): void {
    event.stopPropagation();
    
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      width: '450px',
      panelClass: ['!rounded-2xl', '!bg-white', 'dark:!bg-gray-900'],
      data: {
        title: 'Delete Inventory Item',
        message: `Are you sure you want to delete "${item.name}"? This action cannot be undone.`,
        confirmButtonText: 'Delete',
        cancelButtonText: 'Cancel',
        isDestructive: true
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.inventoryService.deleteItem(item.id).subscribe({
          next: () => this.reload(),
          error: (err) => {
            console.error('Failed to delete item', err);
            this.error.set(err?.error?.detail || 'Failed to delete item');
          }
        });
      }
    });
  }
}
