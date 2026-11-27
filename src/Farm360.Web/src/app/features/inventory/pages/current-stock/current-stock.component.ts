import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { InventorySubnavComponent } from '../../components/inventory-subnav/inventory-subnav.component';
import { Component, ChangeDetectionStrategy, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { CurrentStockSummary, InventoryItem, InventoryStatus, stockStatusLabel } from '../../models/inventory.models';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { StockWriteOffDialog } from '../../components/stock-write-off-dialog/stock-write-off-dialog';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

@Component({
  selector: 'app-current-stock',
  standalone: true,
  imports: [HasPermissionDirective, InventorySubnavComponent, MoneyPipe, 
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatPaginatorModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent,
    ErrorStateComponent,
    MatTooltipModule,
    TranslatePipe
  ],
  templateUrl: './current-stock.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CurrentStockComponent implements OnInit {
  private readonly inventoryService = inject(InventoryService);
  private readonly contextService = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly pdfService = inject(PdfExportService);
  private readonly i18n = inject(I18nService);

  readonly isLoading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly isExporting = signal(false);
  readonly summary = signal<CurrentStockSummary | null>(null);
  readonly items = signal<InventoryItem[]>([]);
  readonly totalItemsCount = signal(0);
  
  readonly displayedColumns = ['name', 'category', 'status', 'currentStock', 'valuation', 'updated', 'actions'];

  readonly activeFarmId = signal<string | null>(null);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);
  readonly today = new Date();
  readonly statusLabel = stockStatusLabel;

  constructor() {
    this.contextService.currentFarm$.pipe(
      takeUntilDestroyed()
    ).subscribe(farm => {
      const farmId = farm?.id || null;
      this.activeFarmId.set(farmId);
      if (farmId) {
        this.loadData();
      }
    });
  }

  ngOnInit(): void {
  }

  loadData(): void {
    const farmId = this.activeFarmId();
    if (!farmId) return;

    this.isLoading.set(true);
    this.loadError.set(null);

    // Fetch Summary
    this.inventoryService.getCurrentStockSummary(farmId).subscribe({
      next: (data) => this.summary.set(data),
      error: () => this.summary.set(null)
    });

    // Fetch Table Data
    this.inventoryService.getItems({
      farmId,
      pageNumber: this.pageIndex() + 1,
      pageSize: this.pageSize()
    }).subscribe({
      next: (response) => {
        this.items.set(response.items);
        this.totalItemsCount.set(response.totalCount);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.items.set([]);
        this.isLoading.set(false);
        this.loadError.set(parseApiError(err, this.i18n.t('Could not load current stock.')));
      }
    });
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.loadData();
  }

  getStatusClass(status: InventoryStatus): string {
    switch (status) {
      case InventoryStatus.Sufficient: return 'bg-success-50 text-success-700 dark:bg-success-900/30 dark:text-success-400 border border-success-200 dark:border-success-800';
      case InventoryStatus.LowStock: return 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
      case InventoryStatus.OutOfStock: return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400 border border-red-200 dark:border-red-800';
      case InventoryStatus.Excess: return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
      default: return 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-300';
    }
  }

  printReport(): void {
    window.print();
  }

  async exportPdf(): Promise<void> {
    const element = document.getElementById('reportSheet');
    if (!element) return;

    this.isExporting.set(true);
    try {
      const farmName = this.contextService.currentFarmValue?.name || this.i18n.t('All Units');
      await this.pdfService.exportElement(element, {
        filename: `Current_Stock_Report_${toLocalIsoDate()}`,
        orientation: 'landscape',
        header: {
          title: this.i18n.t('Current Stock Balance & Valuation Report'),
          subtitle: this.i18n.t('Real-time snapshot of current inventory balance and valuation'),
          farmName,
          dateRange: this.i18n.t('As of {date}', { date: new Date().toLocaleDateString('en-GB') }),
          currency: 'BDT (৳)'
        },
        showSignatures: true
      });
    } finally {
      this.isExporting.set(false);
    }
  }

  openStockWriteOffDialog(item: InventoryItem): void {
    const dialogRef = this.dialog.open(StockWriteOffDialog, { maxWidth: '95vw', disableClose: true,
      width: '600px',
      data: { item }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadData();
    });
  }
}
