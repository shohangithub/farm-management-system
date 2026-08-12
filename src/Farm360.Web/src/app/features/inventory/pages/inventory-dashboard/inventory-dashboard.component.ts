import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { InventoryItem, InventoryValuationReport, StockTransaction, StockTransactionType, InventoryStatus } from '../../models/inventory.models';
import { StockInDialogComponent } from '../../components/dialogs/stock-in-dialog/stock-in-dialog.component';
import { StockOutDialogComponent } from '../../components/dialogs/stock-out-dialog/stock-out-dialog.component';
import { CreateItemDialogComponent } from '../../components/dialogs/create-item-dialog/create-item-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-inventory-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Inventory & Stock Control Intelligence"
      description="Real-time stock levels, weighted average valuation, low-stock alerts, and movement ledgers."
      breadcrumbActiveNode="Inventory Control">
      <div actions class="flex items-center gap-2">
        <button (click)="openStockInDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add_business</mat-icon> Record Stock In
        </button>
        <button (click)="openStockOutDialog()"
          class="px-4 py-2 text-sm font-semibold text-amber-700 dark:text-amber-300 bg-amber-50 dark:bg-amber-950/40 hover:bg-amber-100 dark:hover:bg-amber-900/50 border border-amber-200 dark:border-amber-800 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">remove_shopping_cart</mat-icon> Deduct Stock
        </button>
      </div>
    </app-page-header>

    <div class="space-y-6">
      <!-- KPI Summary Cards Grid -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl p-5 rounded-2xl border border-gray-100 dark:border-gray-800/50 shadow-sm flex items-center gap-4 relative overflow-hidden group">
          <mat-icon class="absolute -right-3 -bottom-3 text-[80px] text-emerald-500/5 rotate-[-10deg] pointer-events-none">account_balance_wallet</mat-icon>
          <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20 group-hover:scale-110 transition-transform duration-300">
            <mat-icon class="!w-6 !h-6 !text-[24px]">account_balance_wallet</mat-icon>
          </div>
          <div>
            <div class="text-[11px] uppercase tracking-wider font-bold text-gray-400">Total Stock Value</div>
            <div class="text-2xl font-extrabold text-gray-900 dark:text-white mt-0.5">
              ৳ {{ report()?.totalValuationBdt || 0 | number:'1.0-0' }}
            </div>
          </div>
        </div>

        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl p-5 rounded-2xl border border-gray-100 dark:border-gray-800/50 shadow-sm flex items-center gap-4 relative overflow-hidden group">
          <mat-icon class="absolute -right-3 -bottom-3 text-[80px] text-blue-500/5 rotate-[-10deg] pointer-events-none">inventory_2</mat-icon>
          <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 text-white flex items-center justify-center shadow-md shadow-blue-500/20 group-hover:scale-110 transition-transform duration-300">
            <mat-icon class="!w-6 !h-6 !text-[24px]">inventory_2</mat-icon>
          </div>
          <div>
            <div class="text-[11px] uppercase tracking-wider font-bold text-gray-400">Total Catalog SKUs</div>
            <div class="text-2xl font-extrabold text-gray-900 dark:text-white mt-0.5">
              {{ report()?.totalSkusCount || 0 }} <span class="text-xs font-medium text-gray-400">items</span>
            </div>
          </div>
        </div>

        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl p-5 rounded-2xl border border-gray-100 dark:border-gray-800/50 shadow-sm flex items-center gap-4 relative overflow-hidden group">
          <mat-icon class="absolute -right-3 -bottom-3 text-[80px] text-amber-500/5 rotate-[-10deg] pointer-events-none">warning</mat-icon>
          <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-amber-500 to-orange-600 text-white flex items-center justify-center shadow-md shadow-amber-500/20 group-hover:scale-110 transition-transform duration-300">
            <mat-icon class="!w-6 !h-6 !text-[24px]">warning</mat-icon>
          </div>
          <div>
            <div class="text-[11px] uppercase tracking-wider font-bold text-gray-400">Low Stock SKUs</div>
            <div class="text-2xl font-extrabold text-amber-600 dark:text-amber-400 mt-0.5">
              {{ report()?.lowStockCount || 0 }}
            </div>
          </div>
        </div>

        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl p-5 rounded-2xl border border-gray-100 dark:border-gray-800/50 shadow-sm flex items-center gap-4 relative overflow-hidden group">
          <mat-icon class="absolute -right-3 -bottom-3 text-[80px] text-red-500/5 rotate-[-10deg] pointer-events-none">remove_shopping_cart</mat-icon>
          <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-red-500 to-rose-600 text-white flex items-center justify-center shadow-md shadow-red-500/20 group-hover:scale-110 transition-transform duration-300">
            <mat-icon class="!w-6 !h-6 !text-[24px]">remove_shopping_cart</mat-icon>
          </div>
          <div>
            <div class="text-[11px] uppercase tracking-wider font-bold text-gray-400">Out of Stock</div>
            <div class="text-2xl font-extrabold text-red-600 dark:text-red-400 mt-0.5">
              {{ report()?.outOfStockCount || 0 }}
            </div>
          </div>
        </div>
      </div>

      <!-- Quick Module Navigation Tabs -->
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <a routerLink="../current-stock" class="p-4 bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl border border-gray-100 dark:border-gray-800/50 hover:border-emerald-500 transition-all flex items-center justify-between group shadow-sm">
          <div class="flex items-center gap-3">
            <div class="w-9 h-9 rounded-xl bg-teal-50 dark:bg-teal-950/40 text-teal-600 flex items-center justify-center group-hover:scale-110 transition-transform">
              <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">analytics</mat-icon>
            </div>
            <span class="font-semibold text-gray-900 dark:text-white text-sm">Current Stock Report</span>
          </div>
          <mat-icon class="text-gray-400 group-hover:translate-x-1 transition-transform !text-[18px] !w-[18px] !h-[18px]">chevron_right</mat-icon>
        </a>

        <a routerLink="../items" class="p-4 bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl border border-gray-100 dark:border-gray-800/50 hover:border-emerald-500 transition-all flex items-center justify-between group shadow-sm">
          <div class="flex items-center gap-3">
            <div class="w-9 h-9 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 flex items-center justify-center group-hover:scale-110 transition-transform">
              <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">list_alt</mat-icon>
            </div>
            <span class="font-semibold text-gray-900 dark:text-white text-sm">Full Stock Catalog</span>
          </div>
          <mat-icon class="text-gray-400 group-hover:translate-x-1 transition-transform !text-[18px] !w-[18px] !h-[18px]">chevron_right</mat-icon>
        </a>

        <a routerLink="../transactions" class="p-4 bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl border border-gray-100 dark:border-gray-800/50 hover:border-emerald-500 transition-all flex items-center justify-between group shadow-sm">
          <div class="flex items-center gap-3">
            <div class="w-9 h-9 rounded-xl bg-blue-50 dark:bg-blue-950/40 text-blue-600 flex items-center justify-center group-hover:scale-110 transition-transform">
              <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">receipt_long</mat-icon>
            </div>
            <span class="font-semibold text-gray-900 dark:text-white text-sm">Stock Movement Ledger</span>
          </div>
          <mat-icon class="text-gray-400 group-hover:translate-x-1 transition-transform !text-[18px] !w-[18px] !h-[18px]">chevron_right</mat-icon>
        </a>

        <a routerLink="../suppliers" class="p-4 bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl border border-gray-100 dark:border-gray-800/50 hover:border-emerald-500 transition-all flex items-center justify-between group shadow-sm">
          <div class="flex items-center gap-3">
            <div class="w-9 h-9 rounded-xl bg-purple-50 dark:bg-purple-950/40 text-purple-600 flex items-center justify-center group-hover:scale-110 transition-transform">
              <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">local_shipping</mat-icon>
            </div>
            <span class="font-semibold text-gray-900 dark:text-white text-sm">Suppliers & Vendors</span>
          </div>
          <mat-icon class="text-gray-400 group-hover:translate-x-1 transition-transform !text-[18px] !w-[18px] !h-[18px]">chevron_right</mat-icon>
        </a>
      </div>

      <!-- Dashboard Content Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Low Stock Alerts -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl border border-gray-100 dark:border-gray-800/50 p-6 shadow-sm relative overflow-hidden">
          <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

          <div class="flex items-center justify-between mb-4">
            <h2 class="font-bold text-gray-900 dark:text-white text-base flex items-center gap-2">
              <mat-icon class="text-amber-500 !text-[20px] !w-[20px] !h-[20px]">notification_important</mat-icon> Low Stock Alerts
            </h2>
            <a routerLink="../items" class="text-xs font-semibold text-emerald-600 hover:underline">View Catalog</a>
          </div>

          <app-empty-state
            *ngIf="!isLoading() && lowStockItems().length === 0"
            icon="task_alt"
            title="All stock levels healthy"
            description="No items are currently below their reorder threshold."
            actionLabel="Add Stock Item"
            (action)="openCreateItemDialog()">
          </app-empty-state>

          <div *ngIf="!isLoading() && lowStockItems().length > 0" class="space-y-3">
            @for (item of lowStockItems(); track item.id) {
              <div class="p-3.5 rounded-xl bg-amber-50/50 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-800/40 flex items-center justify-between">
                <div>
                  <div class="font-semibold text-gray-900 dark:text-white text-sm">{{ item.name }}</div>
                  <div class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                    Category: {{ item.categoryName }} • Reorder at: {{ item.reorderThreshold }} {{ item.unitOfMeasure }}
                  </div>
                </div>
                <div class="text-right flex items-center gap-3">
                  <div>
                    <div class="font-extrabold text-amber-600 dark:text-amber-400 text-sm">{{ item.currentStock }} {{ item.unitOfMeasure }}</div>
                    <div class="text-[10px] uppercase font-bold text-gray-400">Current</div>
                  </div>
                  <button (click)="openStockInDialogForItem(item)" class="px-2.5 py-1 text-xs font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors">
                    Reorder
                  </button>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Recent Stock Transactions -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl border border-gray-100 dark:border-gray-800/50 p-6 shadow-sm relative overflow-hidden">
          <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

          <div class="flex items-center justify-between mb-4">
            <h2 class="font-bold text-gray-900 dark:text-white text-base flex items-center gap-2">
              <mat-icon class="text-emerald-600 !text-[20px] !w-[20px] !h-[20px]">history</mat-icon> Recent Stock Movements
            </h2>
            <a routerLink="../transactions" class="text-xs font-semibold text-emerald-600 hover:underline">View Ledger</a>
          </div>

          <app-empty-state
            *ngIf="!isLoading() && recentTransactions().length === 0"
            icon="history_toggle_off"
            title="No transactions recorded"
            description="Record a stock-in receipt to start logging inventory movements."
            actionLabel="Record Stock In"
            (action)="openStockInDialog()">
          </app-empty-state>

          <div *ngIf="!isLoading() && recentTransactions().length > 0" class="space-y-3">
            @for (tx of recentTransactions(); track tx.id) {
              <div class="p-3.5 rounded-xl bg-gray-50/80 dark:bg-gray-900/50 border border-gray-100 dark:border-gray-800 flex items-center justify-between">
                <div>
                  <div class="font-semibold text-gray-900 dark:text-white text-sm flex items-center gap-2">
                    <span>{{ tx.itemName }}</span>
                    <span class="text-xs px-2 py-0.5 rounded-full font-bold uppercase tracking-wider text-[9px]"
                      [ngClass]="isStockIn(tx.transactionType) ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800' : 'bg-amber-50 text-amber-700 dark:bg-amber-950/60 dark:text-amber-400 border border-amber-200 dark:border-amber-800'">
                      {{ tx.transactionTypeName }}
                    </span>
                  </div>
                  <div class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                    {{ tx.transactionDate }} {{ tx.supplierName ? '• Supplier: ' + tx.supplierName : '' }}
                  </div>
                </div>
                <div class="text-right">
                  <div class="font-bold text-gray-900 dark:text-white text-sm">
                    {{ isStockIn(tx.transactionType) ? '+' : '-' }}{{ getAbsQuantity(tx.quantity) }}
                  </div>
                  <div class="text-[11px] font-semibold text-emerald-600 dark:text-emerald-400">৳ {{ tx.totalCostBdt | number:'1.0-0' }}</div>
                </div>
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InventoryDashboardComponent {
  private readonly inventoryService = inject(InventoryService);
  private readonly contextService = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);

  readonly isLoading = signal(true);
  readonly report = signal<InventoryValuationReport | null>(null);
  readonly lowStockItems = signal<InventoryItem[]>([]);
  readonly recentTransactions = signal<StockTransaction[]>([]);
  readonly activeFarmId = signal<string | null>(null);

  constructor() {
    this.contextService.currentFarm$.pipe(
      takeUntilDestroyed()
    ).subscribe(farm => {
      const farmId = farm?.id || null;
      this.activeFarmId.set(farmId);
      if (farmId) {
        this.loadDashboard(farmId);
      }
    });
  }

  loadDashboard(farmId: string): void {
    this.isLoading.set(true);

    this.inventoryService.getValuationReport(farmId).subscribe({
      next: (res) => {
        this.report.set(res);
        this.lowStockItems.set(res.items.filter(i => i.status === InventoryStatus.LowStock || i.status === InventoryStatus.OutOfStock));
      },
      error: () => {}
    });

    this.inventoryService.getTransactions({ farmId, pageSize: 5 }).subscribe({
      next: (res) => {
        this.recentTransactions.set(res?.items || []);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  openCreateItemDialog(): void {
    const farmId = this.activeFarmId();
    if (!farmId) return;
    const dialogRef = this.dialog.open(CreateItemDialogComponent, {
      width: '720px',
      data: { farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadDashboard(farmId);
    });
  }

  openStockInDialog(): void {
    const farmId = this.activeFarmId();
    if (!farmId) return;
    const dialogRef = this.dialog.open(StockInDialogComponent, {
      width: '720px',
      data: { farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadDashboard(farmId);
    });
  }

  openStockInDialogForItem(item: InventoryItem): void {
    const dialogRef = this.dialog.open(StockInDialogComponent, {
      width: '720px',
      data: { item, farmId: item.farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res && this.activeFarmId()) this.loadDashboard(this.activeFarmId()!);
    });
  }

  openStockOutDialog(): void {
    const farmId = this.activeFarmId();
    if (!farmId) return;
    const dialogRef = this.dialog.open(StockOutDialogComponent, {
      width: '720px',
      data: { farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadDashboard(farmId);
    });
  }

  isStockIn(type: any): boolean {
    if (type == null) return false;
    return type === 1 || type === '1' || type === 'StockIn' || type === StockTransactionType.StockIn;
  }

  getAbsQuantity(qty: number | null | undefined): string {
    if (qty == null) return '0';
    return Math.abs(qty).toString();
  }
}
