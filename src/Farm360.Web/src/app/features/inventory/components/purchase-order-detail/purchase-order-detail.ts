import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { toObservable, toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, filter, tap } from 'rxjs/operators';
import { of } from 'rxjs';

import { InventoryService } from '../../services/inventory.service';
import { PurchaseOrder, PurchaseOrderStatus } from '../../models/inventory.models';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';

@Component({
  selector: 'app-purchase-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent
  ],
  template: `
    <app-page-header
      title="Purchase Order Details"
      description="View and manage the status of your purchase order."
      breadcrumbActiveNode="PO Details">
      <div actions class="flex items-center gap-3">
        <button [routerLink]="['/inventory/purchase-orders']"
          class="px-4 py-2 text-sm font-semibold text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">arrow_back</mat-icon> Back to List
        </button>
        <button *ngIf="po()?.status === PurchaseOrderStatus.PendingApproval" (click)="onApprove()"
          class="px-4 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">check_circle</mat-icon> Approve PO
        </button>
        <button *ngIf="po()?.status === PurchaseOrderStatus.Approved" (click)="onFulfill()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">inventory</mat-icon> Receive Stock (Fulfill)
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative min-h-[400px]">
      <app-loading *ngIf="loading()" [overlay]="true"></app-loading>

      <app-empty-state
        *ngIf="error()"
        icon="error_outline"
        title="Error loading PO"
        [description]="error() || 'An unknown error occurred.'"
        actionLabel="Go Back"
        (action)="router.navigate(['/inventory/purchase-orders'])">
      </app-empty-state>

      <ng-container *ngIf="po() as data">
        <!-- PO Header Info -->
        <div class="p-6 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-900/30 flex flex-col md:flex-row md:items-start justify-between gap-6">
          <div>
            <div class="flex items-center gap-3 mb-2">
              <h2 class="text-2xl font-black text-gray-900 dark:text-white">PO #{{ data.poNumber }}</h2>
              <span class="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold uppercase tracking-wider shadow-sm"
                [ngClass]="{
                  'bg-gray-100 text-gray-700 border border-gray-200': data.status === PurchaseOrderStatus.Draft,
                  'bg-blue-50 text-blue-700 border border-blue-200': data.status === PurchaseOrderStatus.PendingApproval,
                  'bg-emerald-50 text-emerald-700 border border-emerald-200': data.status === PurchaseOrderStatus.Approved,
                  'bg-indigo-50 text-indigo-700 border border-indigo-200': data.status === PurchaseOrderStatus.Fulfilled,
                  'bg-red-50 text-red-700 border border-red-200': data.status === PurchaseOrderStatus.Cancelled
                }">
                {{ getStatusName(data.status) }}
              </span>
            </div>
            <div class="text-sm text-gray-500 dark:text-gray-400">
              <p>Supplier ID: <span class="font-semibold text-gray-700 dark:text-gray-300">{{ data.supplierId }}</span></p>
              <p>Order Date: <span class="font-semibold text-gray-700 dark:text-gray-300">{{ data.orderDate | date:'longDate' }}</span></p>
              <p *ngIf="data.expectedDeliveryDate">Expected Delivery: <span class="font-semibold text-gray-700 dark:text-gray-300">{{ data.expectedDeliveryDate | date:'longDate' }}</span></p>
            </div>
          </div>
          
          <div class="text-right">
            <p class="text-sm text-gray-500 dark:text-gray-400 font-medium uppercase tracking-wider mb-1">Total Amount</p>
            <p class="text-3xl font-black text-emerald-600 dark:text-emerald-400">৳ {{ data.totalAmountBdt | number:'1.2-2' }}</p>
          </div>
        </div>

        <div *ngIf="data.notes" class="p-6 border-b border-gray-100 dark:border-gray-800">
          <h3 class="text-sm font-bold text-gray-900 dark:text-white uppercase tracking-wider mb-2">Notes</h3>
          <p class="text-sm text-gray-600 dark:text-gray-400 whitespace-pre-wrap">{{ data.notes }}</p>
        </div>

        <!-- PO Items -->
        <div class="p-6">
          <h3 class="text-lg font-bold text-gray-900 dark:text-white mb-4">Order Items ({{ data.items.length || 0 }})</h3>
          
          <div class="overflow-x-auto rounded-xl border border-gray-200 dark:border-gray-700">
            <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
              <thead class="bg-gray-50 dark:bg-gray-800/50">
                <tr>
                  <th scope="col" class="px-4 py-3 text-left text-xs font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Item ID</th>
                  <th scope="col" class="px-4 py-3 text-right text-xs font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Quantity</th>
                  <th scope="col" class="px-4 py-3 text-right text-xs font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Unit Cost</th>
                  <th scope="col" class="px-4 py-3 text-right text-xs font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Total</th>
                </tr>
              </thead>
              <tbody class="bg-white dark:bg-gray-900 divide-y divide-gray-200 dark:divide-gray-700">
                <tr *ngFor="let item of data.items" class="hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
                  <td class="px-4 py-3 text-sm text-gray-900 dark:text-white font-medium">
                    {{ item.inventoryItemId }}
                  </td>
                  <td class="px-4 py-3 text-sm text-gray-900 dark:text-white text-right font-medium">
                    {{ item.quantity }}
                  </td>
                  <td class="px-4 py-3 text-sm text-gray-500 dark:text-gray-400 text-right">
                    ৳ {{ item.unitCostBdt | number:'1.2-2' }}
                  </td>
                  <td class="px-4 py-3 text-sm font-bold text-gray-900 dark:text-white text-right">
                    ৳ {{ item.totalCostBdt | number:'1.2-2' }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </ng-container>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PurchaseOrderDetail {
  private readonly inventoryService = inject(InventoryService);
  private readonly route = inject(ActivatedRoute);
  readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  private readonly refreshTrigger = signal(0);
  
  readonly PurchaseOrderStatus = PurchaseOrderStatus;

  readonly poId = toSignal(
    this.route.paramMap.pipe(switchMap(params => of(params.get('id'))))
  );

  private readonly fetchParams = computed(() => ({
    id: this.poId(),
    refresh: this.refreshTrigger()
  }));

  readonly po = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(p => !!p.id),
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(p => this.inventoryService.getPurchaseOrderById(p.id!).pipe(
        catchError(e => {
          this.error.set(e?.error?.detail ?? 'Failed to load purchase order details');
          return of(null);
        })
      )),
      tap(() => this.loading.set(false))
    )
  );

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

  reload(): void {
    this.refreshTrigger.update(n => n + 1);
  }

  onApprove(): void {
    const data = this.po();
    if (!data) return;

    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      width: '450px',
      panelClass: ['!rounded-2xl', '!bg-white', 'dark:!bg-gray-900'],
      data: {
        title: 'Approve Purchase Order',
        message: `Are you sure you want to approve PO #${data.poNumber}? Once approved, it can be fulfilled.`,
        confirmButtonText: 'Approve',
        cancelButtonText: 'Cancel'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.loading.set(true);
        this.inventoryService.approvePurchaseOrder(data.id).subscribe({
          next: () => this.reload(),
          error: (err) => {
            this.loading.set(false);
            this.error.set(err?.error?.detail || 'Failed to approve PO');
          }
        });
      }
    });
  }

  onFulfill(): void {
    const data = this.po();
    if (!data) return;

    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      width: '500px',
      panelClass: ['!rounded-2xl', '!bg-white', 'dark:!bg-gray-900'],
      data: {
        title: 'Fulfill Purchase Order',
        message: `Are you sure you want to fulfill PO #${data.poNumber}? This will automatically receive all items into stock and update the current inventory balance.`,
        confirmButtonText: 'Fulfill & Receive Stock',
        cancelButtonText: 'Cancel'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.loading.set(true);
        this.inventoryService.fulfillPurchaseOrder(data.id).subscribe({
          next: () => {
            // Re-fetch to show new status
            this.reload();
          },
          error: (err) => {
            this.loading.set(false);
            this.error.set(err?.error?.detail || 'Failed to fulfill PO');
          }
        });
      }
    });
  }
}
