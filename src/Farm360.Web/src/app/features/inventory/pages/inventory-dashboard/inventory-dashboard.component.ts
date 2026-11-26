import { InventorySubnavComponent } from '../../components/inventory-subnav/inventory-subnav.component';
import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { InventoryItem, InventoryValuationReport, StockTransaction, StockTransactionType, InventoryStatus, movementLabel } from '../../models/inventory.models';
import { StockInDialogComponent } from '../../components/dialogs/stock-in-dialog/stock-in-dialog.component';
import { StockOutDialogComponent } from '../../components/dialogs/stock-out-dialog/stock-out-dialog.component';
import { CreateItemDialogComponent } from '../../components/dialogs/create-item-dialog/create-item-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ExpiringItemsPanel } from '../../components/expiring-items-panel/expiring-items-panel';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

@Component({
  selector: 'app-inventory-dashboard',
  standalone: true,
  imports: [InventorySubnavComponent, MoneyPipe, 
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    LoadingComponent,
    ExpiringItemsPanel,
    TranslatePipe
  ],
  template: `
    <app-page-header
      [title]="'Stock Overview' | t"
      [description]="'Stock value, items running low and the latest movements.' | t"
      [breadcrumbActiveNode]="'Overview' | t">
      <div actions class="flex items-center gap-2">
        <button (click)="openStockInDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add_business</mat-icon> {{ 'Record Stock In' | t }}
        </button>
        <button (click)="openStockOutDialog()"
          class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-900 hover:bg-gray-50 dark:hover:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">remove_shopping_cart</mat-icon> {{ 'Deduct Stock' | t }}
        </button>
      </div>
    </app-page-header>
    <app-inventory-subnav></app-inventory-subnav>

    <div class="space-y-6">
      <app-error-state *ngIf="loadError() as err" [message]="err" (retry)="reload()"></app-error-state>
      <!-- Key numbers -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-5">
        <div class="bg-white dark:bg-gray-900 p-4 sm:p-5 rounded-2xl border border-gray-200 dark:border-gray-800 shadow-sm flex items-center gap-3 sm:gap-4 min-w-0">
          <div class="hidden sm:flex w-11 h-11 shrink-0 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 items-center justify-center">
            <mat-icon class="!w-[22px] !h-[22px] !text-[22px]">account_balance_wallet</mat-icon>
          </div>
          <div class="min-w-0">
            <div class="text-xs font-medium text-gray-500 dark:text-gray-400">{{ 'Total Stock Value' | t }}</div>
            <div class="text-xl sm:text-2xl font-bold text-gray-900 dark:text-white mt-0.5 truncate">{{ report()?.totalValuationBdt || 0 | money }}</div>
          </div>
        </div>
        <div class="bg-white dark:bg-gray-900 p-4 sm:p-5 rounded-2xl border border-gray-200 dark:border-gray-800 shadow-sm flex items-center gap-3 sm:gap-4 min-w-0">
          <div class="hidden sm:flex w-11 h-11 shrink-0 rounded-xl bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-300 items-center justify-center">
            <mat-icon class="!w-[22px] !h-[22px] !text-[22px]">inventory_2</mat-icon>
          </div>
          <div class="min-w-0">
            <div class="text-xs font-medium text-gray-500 dark:text-gray-400">{{ 'Stock Items' | t }}</div>
            <div class="text-xl sm:text-2xl font-bold text-gray-900 dark:text-white mt-0.5 truncate">{{ report()?.totalSkusCount || 0 }}</div>
          </div>
        </div>
        <div class="bg-white dark:bg-gray-900 p-4 sm:p-5 rounded-2xl border border-gray-200 dark:border-gray-800 shadow-sm flex items-center gap-3 sm:gap-4 min-w-0">
          <div class="hidden sm:flex w-11 h-11 shrink-0 rounded-xl bg-amber-50 dark:bg-amber-950/40 text-amber-600 dark:text-amber-400 items-center justify-center">
            <mat-icon class="!w-[22px] !h-[22px] !text-[22px]">warning</mat-icon>
          </div>
          <div class="min-w-0">
            <div class="text-xs font-medium text-gray-500 dark:text-gray-400">{{ 'Running Low' | t }}</div>
            <div class="text-xl sm:text-2xl font-bold text-amber-600 dark:text-amber-400 mt-0.5 truncate">{{ report()?.lowStockCount || 0 }}</div>
          </div>
        </div>
        <div class="bg-white dark:bg-gray-900 p-4 sm:p-5 rounded-2xl border border-gray-200 dark:border-gray-800 shadow-sm flex items-center gap-3 sm:gap-4 min-w-0">
          <div class="hidden sm:flex w-11 h-11 shrink-0 rounded-xl bg-rose-50 dark:bg-rose-950/40 text-rose-600 dark:text-rose-400 items-center justify-center">
            <mat-icon class="!w-[22px] !h-[22px] !text-[22px]">remove_shopping_cart</mat-icon>
          </div>
          <div class="min-w-0">
            <div class="text-xs font-medium text-gray-500 dark:text-gray-400">{{ 'Out of Stock' | t }}</div>
            <div class="text-xl sm:text-2xl font-bold text-rose-600 dark:text-rose-400 mt-0.5 truncate">{{ report()?.outOfStockCount || 0 }}</div>
          </div>
        </div>
      </div>

      <!-- Dashboard Content Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Low Stock Alerts -->
        <div class="bg-white dark:bg-gray-900 rounded-2xl border border-gray-200 dark:border-gray-800 p-5 sm:p-6 shadow-sm relative overflow-hidden">
          <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

          <div class="flex items-center justify-between mb-4">
            <h2 class="font-bold text-gray-900 dark:text-white text-base flex items-center gap-2">
              <mat-icon class="text-amber-500 !text-[20px] !w-[20px] !h-[20px]">notification_important</mat-icon> {{ 'Low Stock Alerts' | t }}
            </h2>
            <a routerLink="../items" class="text-xs font-semibold text-emerald-600 hover:underline">{{ 'View all items' | t }}</a>
          </div>

          <app-empty-state
            *ngIf="!isLoading() && !loadError() && lowStockItems().length === 0"
            icon="task_alt"
            [title]="'All stock levels healthy' | t"
            [description]="'No items are currently below their reorder threshold.' | t"
            [actionLabel]="'Add Stock Item' | t"
            (action)="openCreateItemDialog()">
          </app-empty-state>

          <div *ngIf="!isLoading() && lowStockItems().length > 0" class="space-y-3">
            @for (item of lowStockItems(); track item.id) {
              <div class="p-3.5 rounded-xl bg-amber-50/50 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-800/40 flex items-center justify-between">
                <div>
                  <div class="font-semibold text-gray-900 dark:text-white text-sm">{{ item.name }}</div>
                  <div class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                    {{ 'Category: {cat}' | t: { cat: (item.categoryName | t) } }} • {{ 'Reorder at: {qty} {unit}' | t: { qty: item.reorderThreshold, unit: item.unitOfMeasure } }}
                  </div>
                </div>
                <div class="text-right flex items-center gap-3">
                  <div>
                    <div class="font-extrabold text-amber-600 dark:text-amber-400 text-sm">{{ item.currentStock | number:'1.0-2' }} {{ item.unitOfMeasure }}</div>
                    <div class="text-[11px] uppercase font-bold text-gray-400">{{ 'Current' | t }}</div>
                  </div>
                  <button (click)="openStockInDialogForItem(item)" class="px-2.5 py-1 text-xs font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors">
                    {{ 'Reorder' | t }}
                  </button>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Expiring Items Panel -->
        <div class="flex flex-col h-full min-h-[300px]">
          <app-expiring-items-panel [farmId]="activeFarmId() || ''" [daysThreshold]="30"></app-expiring-items-panel>
        </div>

        <!-- Recent Stock Transactions -->
        <div class="bg-white dark:bg-gray-900 rounded-2xl border border-gray-200 dark:border-gray-800 p-5 sm:p-6 shadow-sm relative overflow-hidden">
          <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

          <div class="flex items-center justify-between mb-4">
            <h2 class="font-bold text-gray-900 dark:text-white text-base flex items-center gap-2">
              <mat-icon class="text-emerald-600 !text-[20px] !w-[20px] !h-[20px]">history</mat-icon> {{ 'Recent Stock Movements' | t }}
            </h2>
            <a routerLink="../transactions" class="text-xs font-semibold text-emerald-600 hover:underline">{{ 'View history' | t }}</a>
          </div>

          <app-empty-state
            *ngIf="!isLoading() && !loadError() && recentTransactions().length === 0"
            icon="history_toggle_off"
            [title]="'No transactions recorded' | t"
            [description]="'Record a stock-in receipt to start logging inventory movements.' | t"
            [actionLabel]="'Record Stock In' | t"
            (action)="openStockInDialog()">
          </app-empty-state>

          <div *ngIf="!isLoading() && recentTransactions().length > 0" class="space-y-3">
            @for (tx of recentTransactions(); track tx.id) {
              <div class="p-3.5 rounded-xl bg-gray-50/80 dark:bg-gray-900/50 border border-gray-100 dark:border-gray-800 flex items-start justify-between gap-3">
                <div class="min-w-0">
                  <div class="font-semibold text-gray-900 dark:text-white text-sm flex flex-wrap items-center gap-x-2 gap-y-1">
                    <span>{{ tx.itemName }}</span>
                    <span class="px-2 py-0.5 rounded-full font-semibold text-[11px] whitespace-nowrap"
                      [ngClass]="isStockIn(tx.transactionType) ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800' : 'bg-amber-50 text-amber-700 dark:bg-amber-950/60 dark:text-amber-400 border border-amber-200 dark:border-amber-800'">
                      {{ movementLabel(tx.transactionTypeName) | t }}
                    </span>
                  </div>
                  <div class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                    {{ tx.transactionDate }} {{ tx.supplierName ? '• ' + ('Supplier: {name}' | t: { name: tx.supplierName }) : '' }}
                  </div>
                </div>
                <div class="text-right shrink-0">
                  <div class="font-bold text-gray-900 dark:text-white text-sm whitespace-nowrap">
                    {{ isStockIn(tx.transactionType) ? '+' : '-' }}{{ getAbsQuantity(tx.quantity) }}
                  </div>
                  <div class="text-[11px] font-medium text-gray-500 dark:text-gray-400">{{ tx.totalCostBdt | money }}</div>
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
  private readonly i18n = inject(I18nService);

  readonly isLoading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly report = signal<InventoryValuationReport | null>(null);
  readonly lowStockItems = signal<InventoryItem[]>([]);
  readonly recentTransactions = signal<StockTransaction[]>([]);
  readonly activeFarmId = signal<string | null>(null);
  readonly movementLabel = movementLabel;

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

  reload(): void {
    const farmId = this.activeFarmId();
    if (farmId) this.loadDashboard(farmId);
  }

  loadDashboard(farmId: string): void {
    this.isLoading.set(true);
    this.loadError.set(null);

    this.inventoryService.getValuationReport(farmId).subscribe({
      next: (res) => {
        this.report.set(res);
        this.lowStockItems.set(res.items.filter(i => i.status === InventoryStatus.LowStock || i.status === InventoryStatus.OutOfStock));
      },
      error: (err) => this.loadError.set(parseApiError(err, this.i18n.t('Could not load the inventory dashboard.')))
    });

    this.inventoryService.getTransactions({ farmId, pageSize: 5 }).subscribe({
      next: (res) => {
        this.recentTransactions.set(res?.items || []);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.loadError.set(parseApiError(err, this.i18n.t('Could not load the inventory dashboard.')));
      }
    });
  }

  openCreateItemDialog(): void {
    const farmId = this.activeFarmId();
    if (!farmId) return;
    const dialogRef = this.dialog.open(CreateItemDialogComponent, { 
      disableClose: true,
      width: '560px',
      maxWidth: 'calc(100vw - 1.5rem)',
      panelClass: 'dialog-responsive',
      data: { farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadDashboard(farmId);
    });
  }

  openStockInDialog(): void {
    const farmId = this.activeFarmId();
    if (!farmId) return;
    const dialogRef = this.dialog.open(StockInDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '720px',
      data: { farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadDashboard(farmId);
    });
  }

  openStockInDialogForItem(item: InventoryItem): void {
    const dialogRef = this.dialog.open(StockInDialogComponent, { maxWidth: '95vw', disableClose: true,
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
    const dialogRef = this.dialog.open(StockOutDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '720px',
      data: { farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadDashboard(farmId);
    });
  }

  isStockIn(type: any): boolean {
    if (type == null) return false;
    // Stock coming in: purchases, and feed put back when a confirmed feeding is reversed.
    return type === 1 || type === '1' || type === 'StockIn' || type === StockTransactionType.StockIn
      || type === 11 || type === '11' || type === 'FeedConsumptionReversal';
  }

  getAbsQuantity(qty: number | null | undefined): string {
    if (qty == null) return '0';
    return (Math.round(Math.abs(qty) * 1000) / 1000).toString();
  }
}
