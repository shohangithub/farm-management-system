import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { toObservable, toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, debounceTime, distinctUntilChanged, tap, filter } from 'rxjs/operators';
import { of } from 'rxjs';

import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { PurchaseOrder, PurchaseOrderStatus, PurchaseOrderParams } from '../../models/inventory.models';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-purchase-order-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Purchase Orders"
      description="Manage purchase orders, approve requests, and fulfill stock arrivals from suppliers."
      breadcrumbActiveNode="Purchase Orders">
      <div actions>
        <button [routerLink]="['/inventory/purchase-orders/new']"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> Create PO
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative min-h-[400px]">
      <app-loading *ngIf="loading()" [overlay]="true"></app-loading>

      <!-- Filters Toolbar -->
      <div class="p-4 border-b border-gray-100 dark:border-gray-800 flex flex-col sm:flex-row items-center gap-4 bg-gray-50/50 dark:bg-gray-900/30">
        <div class="relative w-full sm:w-72">
          <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 !text-[18px] !w-[18px] !h-[18px]">search</mat-icon>
          <input [ngModel]="searchTerm()" (ngModelChange)="onSearchChange($event)"
            placeholder="Search PO number..."
            class="w-full pl-9 pr-4 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all" />
        </div>

        <select [ngModel]="params().status ?? null" (ngModelChange)="onStatusChange($event)"
          class="w-full sm:w-48 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option [ngValue]="null">All Statuses</option>
          <option [ngValue]="PurchaseOrderStatus.Draft">Draft</option>
          <option [ngValue]="PurchaseOrderStatus.PendingApproval">Pending Approval</option>
          <option [ngValue]="PurchaseOrderStatus.Approved">Approved</option>
          <option [ngValue]="PurchaseOrderStatus.Fulfilled">Fulfilled</option>
          <option [ngValue]="PurchaseOrderStatus.Cancelled">Cancelled</option>
        </select>
      </div>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!loading() && (!result()?.items || result()?.items?.length === 0)"
        icon="request_quote"
        title="No Purchase Orders found"
        description="Create your first purchase order to restock your inventory."
        actionLabel="Create PO"
        (action)="router.navigate(['/inventory/purchase-orders/new'])">
      </app-empty-state>

      <!-- Items Grid -->
      <div *ngIf="!loading() && result()?.items?.length" class="p-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        @for (po of result()?.items; track po.id) {
          <div class="group relative bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 shadow-sm hover:shadow-xl transition-all duration-300 overflow-hidden transform hover:-translate-y-1 flex flex-col justify-between cursor-pointer"
               (click)="router.navigate(['/inventory/purchase-orders', po.id])">
            <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-emerald-500/5 rotate-[-10deg] pointer-events-none transition-transform duration-500 group-hover:scale-110">request_quote</mat-icon>

            <!-- Card Header -->
            <div class="p-5 flex items-start justify-between border-b border-gray-50 dark:border-gray-700/50 relative z-10">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20 group-hover:scale-110 transition-transform duration-300">
                  <mat-icon class="!w-5 !h-5 !text-[20px]">request_quote</mat-icon>
                </div>
                <div>
                  <h3 class="font-bold text-gray-900 dark:text-white text-base leading-tight group-hover:text-emerald-600 transition-colors">PO #{{ po.poNumber }}</h3>
                  <span class="inline-flex items-center mt-1 text-xs font-semibold text-gray-400">
                    {{ po.orderDate | date:'mediumDate' }}
                  </span>
                </div>
              </div>

              <span class="inline-flex items-center px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider shadow-sm"
                [ngClass]="{
                  'bg-gray-100 text-gray-700 border border-gray-200': po.status === PurchaseOrderStatus.Draft,
                  'bg-blue-50 text-blue-700 border border-blue-200': po.status === PurchaseOrderStatus.PendingApproval,
                  'bg-emerald-50 text-emerald-700 border border-emerald-200': po.status === PurchaseOrderStatus.Approved,
                  'bg-indigo-50 text-indigo-700 border border-indigo-200': po.status === PurchaseOrderStatus.Fulfilled,
                  'bg-red-50 text-red-700 border border-red-200': po.status === PurchaseOrderStatus.Cancelled
                }">
                {{ getStatusName(po.status) }}
              </span>
            </div>

            <!-- Card Body -->
            <div class="p-5 flex-1 relative z-10">
              <div class="flex items-center justify-between text-xs mb-2 text-gray-400">
                <span>Items Count</span>
                <span class="font-bold text-gray-700 dark:text-gray-300">{{ po.items.length || 0 }} Items</span>
              </div>
              <div class="flex items-center justify-between text-xs text-gray-400">
                <span>Total Amount</span>
                <span class="font-extrabold text-emerald-600 dark:text-emerald-400 text-base">৳ {{ po.totalAmountBdt | number:'1.2-2' }}</span>
              </div>
            </div>
            
            <!-- Footer Action -->
            <div class="p-3 bg-gray-50/80 dark:bg-gray-800/80 border-t border-gray-100 dark:border-gray-700/50 flex items-center justify-end relative z-10">
               <span class="text-xs font-medium text-emerald-600 dark:text-emerald-400 inline-flex items-center gap-1">
                 View Details <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">arrow_forward</mat-icon>
               </span>
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
export class PurchaseOrderList {
  private readonly inventoryService = inject(InventoryService);
  private readonly contextService = inject(WorkingContextService);
  private readonly route = inject(ActivatedRoute);
  readonly router = inject(Router);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly params = signal<PurchaseOrderParams>({ pageNumber: 1, pageSize: 20 });
  private readonly refreshTrigger = signal(0);

  readonly PurchaseOrderStatus = PurchaseOrderStatus;

  constructor() {
    // Sync URL queries
    const qp = this.route.snapshot.queryParams;
    if (qp['search']) this.searchTerm.set(qp['search']);
    this.params.set({
      pageNumber: qp['pageNumber'] ? parseInt(qp['pageNumber'], 10) : 1,
      pageSize: qp['pageSize'] ? parseInt(qp['pageSize'], 10) : 20,
      search: qp['search'] || undefined,
      status: qp['status'] != null ? qp['status'] as PurchaseOrderStatus : undefined
    });

    this.contextService.currentFarm$.pipe(takeUntilDestroyed()).subscribe(farm => {
      this.params.update(p => ({ ...p, farmId: farm?.id || undefined }));
    });

    toObservable(this.searchTerm).pipe(
      debounceTime(350),
      distinctUntilChanged(),
      takeUntilDestroyed()
    ).subscribe(term => {
      this.params.update(p => ({ ...p, search: term || undefined, pageNumber: 1 }));
    });

    toObservable(this.params).pipe(takeUntilDestroyed()).subscribe(p => {
      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: {
          pageNumber: p.pageNumber && p.pageNumber > 1 ? p.pageNumber : null,
          pageSize: p.pageSize && p.pageSize !== 20 ? p.pageSize : null,
          search: p.search || null,
          status: p.status ?? null
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
      switchMap(({ params }) => {
        if (!params.farmId) {
          return of({ items: [], totalCount: 0, pageNumber: params.pageNumber ?? 1, pageSize: params.pageSize ?? 20, totalPages: 0, hasPreviousPage: false, hasNextPage: false } as any);
        }
        return this.inventoryService.getPurchaseOrders(params).pipe(
          catchError(e => {
            this.error.set(e?.error?.detail ?? 'Failed to load purchase orders');
            return of(null);
          })
        );
      }),
      tap(() => this.loading.set(false))
    )
  );

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

  onStatusChange(status: PurchaseOrderStatus | null): void {
    this.params.update(p => ({ ...p, status: status ?? undefined, pageNumber: 1 }));
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

  getStatusName(status: PurchaseOrderStatus): string {
    switch (status) {
      case PurchaseOrderStatus.Draft: return 'Draft';
      case PurchaseOrderStatus.PendingApproval: return 'Pending Approval';
      case PurchaseOrderStatus.Approved: return 'Approved';
      case PurchaseOrderStatus.Fulfilled: return 'Fulfilled';
      case PurchaseOrderStatus.Cancelled: return 'Cancelled';
      default: return 'Unknown';
    }
  }
}
