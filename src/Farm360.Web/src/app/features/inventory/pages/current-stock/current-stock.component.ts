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
import { CurrentStockSummary, InventoryItem, InventoryStatus } from '../../models/inventory.models';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { StockWriteOffDialog } from '../../components/stock-write-off-dialog/stock-write-off-dialog';

@Component({
  selector: 'app-current-stock',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatPaginatorModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  templateUrl: './current-stock.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CurrentStockComponent implements OnInit {
  private readonly inventoryService = inject(InventoryService);
  private readonly contextService = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);

  readonly isLoading = signal(false);
  readonly summary = signal<CurrentStockSummary | null>(null);
  readonly items = signal<InventoryItem[]>([]);
  readonly totalItemsCount = signal(0);
  
  readonly displayedColumns = ['name', 'category', 'status', 'currentStock', 'valuation', 'updated', 'actions'];

  readonly activeFarmId = signal<string | null>(null);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);
  readonly today = new Date();

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

    // Fetch Summary
    this.inventoryService.getCurrentStockSummary(farmId).subscribe({
      next: (data) => this.summary.set(data),
      error: () => this.isLoading.set(false)
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
      error: () => this.isLoading.set(false)
    });
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.loadData();
  }

  getStatusClass(status: InventoryStatus): string {
    switch (status) {
      case InventoryStatus.Sufficient: return 'bg-teal-100 text-teal-800 dark:bg-teal-900/30 dark:text-teal-400 border border-teal-200 dark:border-teal-800';
      case InventoryStatus.LowStock: return 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
      case InventoryStatus.OutOfStock: return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400 border border-red-200 dark:border-red-800';
      case InventoryStatus.Excess: return 'bg-indigo-100 text-indigo-800 dark:bg-indigo-900/30 dark:text-indigo-400 border border-indigo-200 dark:border-indigo-800';
      default: return 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-300';
    }
  }

  printReport(): void {
    window.print();
  }

  openStockWriteOffDialog(item: InventoryItem): void {
    const dialogRef = this.dialog.open(StockWriteOffDialog, {
      width: '600px',
      data: { item }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadData();
    });
  }
}
