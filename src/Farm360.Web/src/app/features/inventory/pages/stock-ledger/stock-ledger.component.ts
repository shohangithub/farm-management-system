import { InventorySubnavComponent } from '../../components/inventory-subnav/inventory-subnav.component';
import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { toObservable, toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, debounceTime, distinctUntilChanged, filter, tap } from 'rxjs/operators';
import { of } from 'rxjs';

import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { StockTransaction, StockTransactionParams, StockTransactionType, movementLabel, cleanReference } from '../../models/inventory.models';
import { StockInDialogComponent } from '../../components/dialogs/stock-in-dialog/stock-in-dialog.component';
import { StockOutDialogComponent } from '../../components/dialogs/stock-out-dialog/stock-out-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';

import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';
@Component({
  selector: 'app-stock-ledger',
  standalone: true,
  imports: [InventorySubnavComponent, MoneyPipe, 
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    LoadingComponent,
    TranslatePipe
  ],
  template: `
    <app-page-header
      [title]="'Stock History' | t"
      [description]="'Every stock movement in and out, newest first.' | t"
      [breadcrumbActiveNode]="'Stock History' | t">
      <div actions class="flex items-center gap-2 no-print">
        <button (click)="exportPdf()" [disabled]="isExporting() || loading() || !result()?.items?.length"
          class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-900 hover:bg-gray-50 dark:hover:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl transition-colors shadow-sm inline-flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]" [class.animate-spin]="isExporting()">{{ isExporting() ? 'refresh' : 'picture_as_pdf' }}</mat-icon>
          {{ (isExporting() ? 'Exporting PDF...' : 'Export PDF') | t }}
        </button>
        <button (click)="openStockInDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-xl transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add_business</mat-icon> {{ 'Record Stock In' | t }}
        </button>
        <button (click)="openStockOutDialog()"
          class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-900 hover:bg-gray-50 dark:hover:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">remove_shopping_cart</mat-icon> {{ 'Deduct Stock' | t }}
        </button>
      </div>
    </app-page-header>
    <app-inventory-subnav></app-inventory-subnav>

    <div id="reportSheet" class="bg-white dark:bg-gray-900 rounded-2xl shadow-sm border border-gray-200 dark:border-gray-800 overflow-hidden relative min-h-[400px]">
      <app-loading *ngIf="loading()" [overlay]="true"></app-loading>
      <app-error-state *ngIf="error() as err" [message]="err" (retry)="retryLoad()"></app-error-state>

      <!-- Filters & Search Toolbar -->
      <div class="p-4 border-b border-gray-100 dark:border-gray-800 grid grid-cols-2 sm:flex sm:flex-row sm:items-center gap-3 sm:gap-4 bg-gray-50/50 dark:bg-gray-900/30 no-print">
        <div class="relative col-span-2 w-full sm:w-72">
          <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 !text-[18px] !w-[18px] !h-[18px]">search</mat-icon>
          <input [ngModel]="searchTerm()" (ngModelChange)="onSearchChange($event)"
            [placeholder]="'Search by item, invoice or supplier...' | t"
            class="w-full pl-9 pr-4 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all" />
        </div>

        <select [ngModel]="params().transactionType ?? null" (ngModelChange)="onTypeChange($event)"
          class="w-full sm:w-56 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option [ngValue]="null">{{ 'All Movement Types' | t }}</option>
          <option [ngValue]="StockTransactionType.StockIn">{{ 'Stock In (Receipt)' | t }}</option>
          <option [ngValue]="StockTransactionType.ManualStockOut">{{ 'Stock Out (Issue)' | t }}</option>
          <option [ngValue]="StockTransactionType.Adjustment">{{ 'Adjustment' | t }}</option>
          <option [ngValue]="StockTransactionType.WriteOff">{{ 'Write-Off' | t }}</option>
          <option [ngValue]="StockTransactionType.PurchaseReturn">{{ 'Purchase Return' | t }}</option>
        </select>

        <select [ngModel]="currentSortKey()" (ngModelChange)="onSortChange($event)"
          class="w-full sm:w-52 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
          <option value="date_desc">{{ 'Sort: Newest First' | t }}</option>
          <option value="date_asc">{{ 'Sort: Oldest First' | t }}</option>
          <option value="cost_desc">{{ 'Sort: Highest Total Cost' | t }}</option>
          <option value="qty_desc">{{ 'Sort: Highest Quantity' | t }}</option>
        </select>
      </div>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!loading() && !error() && (!result()?.items || result()?.items?.length === 0)"
        icon="receipt_long"
        [title]="'No transactions logged' | t"
        [description]="'Transactions are created when stock is received or issued.' | t"
        [actionLabel]="'Record Stock In' | t"
        (action)="openStockInDialog()">
      </app-empty-state>

      <!-- Transactions Table -->
      <div *ngIf="!loading() && result()?.items?.length" class="overflow-x-auto">
        <table class="w-full text-left text-sm text-gray-600 dark:text-gray-300">
          <thead class="text-xs uppercase bg-gray-50/80 dark:bg-gray-900/50 text-gray-400 font-bold border-b border-gray-100 dark:border-gray-800">
            <tr>
              <th class="py-3.5 px-4">{{ 'Date' | t }}</th>
              <th class="py-3.5 px-4">{{ 'Item Name' | t }}</th>
              <th class="py-3.5 px-4">{{ 'Movement Type' | t }}</th>
              <th class="py-3.5 px-4 text-right">{{ 'Quantity' | t }}</th>
              <th class="py-3.5 px-4 text-right">{{ 'Unit Cost' | t }}</th>
              <th class="py-3.5 px-4 text-right">{{ 'Total Cost' | t }}</th>
              <th class="py-3.5 px-4 text-right">{{ 'Balance After' | t }}</th>
              <th class="py-3.5 px-4">{{ 'Supplier / Reference' | t }}</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
            @for (tx of result()?.items; track tx.id) {
              <tr class="hover:bg-gray-50/50 dark:hover:bg-gray-700/30 transition-colors">
                <td class="py-3 px-4 font-medium text-gray-900 dark:text-white whitespace-nowrap">
                  {{ tx.transactionDate }}
                </td>
                <td class="py-3 px-4 font-semibold text-gray-900 dark:text-white min-w-[140px]">
                  {{ tx.itemName }}
                </td>
                <td class="py-3.5 px-4">
                  <span class="px-2.5 py-1 rounded-full text-[11px] font-semibold whitespace-nowrap"
                    [ngClass]="{
                      'bg-emerald-50 text-emerald-700 border border-emerald-200 dark:bg-emerald-950/40 dark:text-emerald-300 dark:border-emerald-800': isStockIn(tx.transactionType),
                      'bg-amber-50 text-amber-700 border border-amber-200 dark:bg-amber-950/40 dark:text-amber-300 dark:border-amber-800': !isStockIn(tx.transactionType)
                    }">
                    {{ movementLabel(tx.transactionTypeName) | t }}
                  </span>
                </td>
                <td class="py-3.5 px-4 text-right font-extrabold"
                  [ngClass]="isStockIn(tx.transactionType) ? 'text-emerald-600 dark:text-emerald-400' : 'text-amber-700 dark:text-amber-400'">
                  {{ isStockIn(tx.transactionType) ? '+' : '-' }}{{ getAbsQuantity(tx.quantity) }}
                  <span *ngIf="tx.enteredUnitName" class="block text-[11px] font-normal text-gray-500">{{ tx.enteredQuantity | number:'1.0-4' }} × {{ tx.enteredUnitName }}</span>
                </td>
                <td class="py-3.5 px-4 text-right whitespace-nowrap">{{ tx.unitCostBdt | money:2 }}</td>
                <td class="py-3.5 px-4 text-right font-bold text-gray-900 dark:text-white">{{ tx.totalCostBdt | money }}</td>
                <td class="py-3.5 px-4 text-right font-semibold text-gray-500 dark:text-gray-400 whitespace-nowrap">{{ tx.balanceAfter | number:'1.0-2' }}</td>
                <td class="py-3.5 px-4 text-xs text-gray-500 dark:text-gray-400 min-w-[180px]">
                  {{ tx.supplierName || tx.invoiceNumber || cleanReference(tx.reason) || '—' }}
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <!-- Pagination Footer -->
      <div *ngIf="!loading() && result()?.items?.length" class="px-6 py-4 border-t border-gray-200 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-900/30 flex flex-col sm:flex-row items-center justify-between gap-4 relative z-10 no-print">
        <div class="text-sm text-gray-500 dark:text-gray-400 font-medium">
          {{ 'Showing {from} to {to} of {total} transactions' | t: { from: pageStart(), to: pageEnd(), total: result()?.totalCount ?? 0 } }}
        </div>
        <div class="flex items-center gap-4">
          <!-- Page Size Filter -->
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
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StockLedgerComponent {
  private readonly inventoryService = inject(InventoryService);
  private readonly contextService = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly pdfService = inject(PdfExportService);
  private readonly i18n = inject(I18nService);

  readonly loading = signal(true);
  readonly isExporting = signal(false);
  readonly error = signal<string | null>(null);

  retryLoad(): void {
    this.error.set(null);
    this.refreshTrigger.update(n => n + 1);
  }
  readonly searchTerm = signal('');
  readonly params = signal<StockTransactionParams>({ pageNumber: 1, pageSize: 20 });
  private readonly refreshTrigger = signal(0);

  readonly StockTransactionType = StockTransactionType;
  readonly movementLabel = movementLabel;
  readonly cleanReference = cleanReference;

  constructor() {
    // 1. Initialize from URL Query Parameters
    const qp = this.route.snapshot.queryParams;
    const initialSearch = qp['search'] || '';
    const initialType = qp['transactionType'] != null ? qp['transactionType'] as StockTransactionType : undefined;
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
      transactionType: initialType,
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
          transactionType: p.transactionType ?? null,
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
      switchMap(({ params }) => this.inventoryService.getTransactions(params).pipe(
        catchError(e => {
          this.error.set(parseApiError(e, this.i18n.t('Could not load stock transactions.')));
          return of(null);
        })
      )),
      tap(() => this.loading.set(false))
    )
  );

  readonly currentSortKey = computed(() => {
    const p = this.params();
    if (!p.sortBy) return 'date_desc';
    if (p.sortBy === 'transactionDate') return p.sortDesc ? 'date_desc' : 'date_asc';
    if (p.sortBy === 'totalCostBdt') return 'cost_desc';
    if (p.sortBy === 'quantity') return 'qty_desc';
    return 'date_desc';
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

  onTypeChange(type: StockTransactionType | null): void {
    this.params.update(p => ({ ...p, transactionType: type ?? undefined, pageNumber: 1 }));
  }

  onSortChange(key: string): void {
    let sortBy: string | undefined;
    let sortDesc = false;

    switch (key) {
      case 'date_desc': sortBy = 'transactionDate'; sortDesc = true; break;
      case 'date_asc': sortBy = 'transactionDate'; sortDesc = false; break;
      case 'cost_desc': sortBy = 'totalCostBdt'; sortDesc = true; break;
      case 'qty_desc': sortBy = 'quantity'; sortDesc = true; break;
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

  async exportPdf(): Promise<void> {
    const element = document.getElementById('reportSheet');
    if (!element) return;

    this.isExporting.set(true);
    try {
      const farmName = this.contextService.currentFarmValue?.name || this.i18n.t('All Units');
      await this.pdfService.exportElement(element, {
        filename: `Stock_Transaction_Ledger_${toLocalIsoDate()}`,
        orientation: 'landscape',
        header: {
          title: this.i18n.t('Stock Transaction Audit Ledger'),
          subtitle: this.i18n.t('Immutable history of receipts, issues, adjustments, and write-offs'),
          farmName,
          dateRange: this.i18n.t('Generated: {date}', { date: new Date().toLocaleDateString('en-GB') }),
          currency: 'BDT (৳)'
        },
        showSignatures: true
      });
    } finally {
      this.isExporting.set(false);
    }
  }

  openStockInDialog(): void {
    const farmId = this.params().farmId;
    if (!farmId) return;
    const dialogRef = this.dialog.open(StockInDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '720px',
      data: { farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.reload();
    });
  }

  openStockOutDialog(): void {
    const farmId = this.params().farmId;
    if (!farmId) return;
    const dialogRef = this.dialog.open(StockOutDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '720px',
      data: { farmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.reload();
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
