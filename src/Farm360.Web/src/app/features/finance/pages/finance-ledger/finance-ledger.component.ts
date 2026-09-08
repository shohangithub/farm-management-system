import { Component, ChangeDetectionStrategy, inject, signal, computed, DestroyRef, OnInit } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of, debounceTime, distinctUntilChanged, Subject, tap } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { 
  FinancialTransaction, 
  FinancialTransactionParams, 
  PagedFinancialTransactionsResult, 
  TRANSACTION_CATEGORIES 
} from '../../models/finance.model';
import { IncomeFormDialogComponent } from '../../components/income-form-dialog/income-form-dialog';
import { ExpenseFormDialogComponent } from '../../components/expense-form-dialog/expense-form-dialog';
import { TransactionDetailDialogComponent } from '../../components/transaction-detail-dialog/transaction-detail-dialog.component';
import { EditTransactionDialogComponent } from '../../components/edit-transaction-dialog/edit-transaction-dialog.component';

@Component({
  selector: 'app-finance-ledger',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatDialogModule,
    MatMenuModule,
    MatDividerModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent,
    DatePipe,
    CurrencyPipe
  ],
  templateUrl: './finance-ledger.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FinanceLedgerComponent implements OnInit {
  private readonly financeService = inject(FinanceService);
  private readonly workingContextService = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);

  readonly categories = TRANSACTION_CATEGORIES;
  readonly allCategories = computed(() => {
    const type = this.selectedType();
    if (type === 'Income') return this.categories.Income;
    if (type === 'Expense') return this.categories.Expense;
    return [...this.categories.Income, ...this.categories.Expense];
  });

  readonly params = signal<FinancialTransactionParams>({
    pageNumber: 1,
    pageSize: 10,
    sortBy: 'TransactionDate',
    sortDesc: true
  });

  readonly searchInput$ = new Subject<string>();
  readonly refreshTrigger = signal(0);
  readonly isLoading = signal(true);
  readonly isExporting = signal(false);

  readonly pageIndex = computed(() => (this.params().pageNumber ?? 1) - 1);
  readonly pageSize = computed(() => this.params().pageSize ?? 10);
  readonly selectedType = computed(() => this.params().type ?? '');
  readonly selectedCategory = computed(() => this.params().category ?? '');
  readonly startDate = computed(() => this.params().startDate ?? '');
  readonly endDate = computed(() => this.params().endDate ?? '');
  readonly searchTerm = computed(() => this.params().search ?? '');
  readonly selectedOrigin = computed(() => {
    const isAuto = this.params().isAutomated;
    if (isAuto === true) return 'automated';
    if (isAuto === false) return 'manual';
    return '';
  });
  readonly hasActiveFilters = computed(() => 
    !!(this.params().search || this.params().type || this.params().category || this.params().startDate || this.params().endDate || this.params().isAutomated !== undefined)
  );

  private readonly combinedParams = computed(() => ({
    farmId: this.workingContextService.currentFarmValue?.id,
    params: this.params(),
    refresh: this.refreshTrigger()
  }));

  readonly result = toSignal(
    of(null).pipe(
      switchMap(() => {
        const p = this.combinedParams();
        if (!p.farmId) {
          this.isLoading.set(false);
          return of<PagedFinancialTransactionsResult>({
            items: [],
            totalCount: 0,
            pageNumber: 1,
            pageSize: 10,
            totalIncomeBdt: 0,
            totalExpenseBdt: 0,
            netCashFlowBdt: 0,
            hasPreviousPage: false,
            hasNextPage: false
          });
        }
        this.isLoading.set(true);
        return this.financeService.getPagedTransactions(p.farmId, p.params).pipe(
          tap(() => this.isLoading.set(false)),
          catchError(() => {
            this.isLoading.set(false);
            return of<PagedFinancialTransactionsResult>({
              items: [],
              totalCount: 0,
              pageNumber: 1,
              pageSize: 10,
              totalIncomeBdt: 0,
              totalExpenseBdt: 0,
              netCashFlowBdt: 0,
              hasPreviousPage: false,
              hasNextPage: false
            });
          })
        );
      })
    )
  );

  readonly transactions = computed(() => this.result()?.items ?? []);
  readonly totalCount = computed(() => this.result()?.totalCount ?? 0);
  readonly totalIncome = computed(() => this.result()?.totalIncomeBdt ?? 0);
  readonly totalExpense = computed(() => this.result()?.totalExpenseBdt ?? 0);
  readonly netCashFlow = computed(() => this.result()?.netCashFlowBdt ?? 0);

  ngOnInit(): void {
    this.searchInput$.pipe(
      debounceTime(350),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(term => {
      this.params.update(p => ({ ...p, search: term || undefined, pageNumber: 1 }));
      this.refresh();
    });
  }

  refresh(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  onSearchChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.searchInput$.next(val);
  }

  onTypeChange(type: string): void {
    this.params.update(p => ({ ...p, type: type || undefined, category: undefined, pageNumber: 1 }));
    this.refresh();
  }

  onCategoryChange(category: string): void {
    this.params.update(p => ({ ...p, category: category || undefined, pageNumber: 1 }));
    this.refresh();
  }

  onOriginChange(origin: string): void {
    let isAutomated: boolean | undefined = undefined;
    if (origin === 'automated') isAutomated = true;
    else if (origin === 'manual') isAutomated = false;

    this.params.update(p => ({ ...p, isAutomated, pageNumber: 1 }));
    this.refresh();
  }

  getSourceModuleBadge(sourceModule?: string): { label: string; icon: string; bgClass: string; textClass: string } {
    switch (sourceModule?.toLowerCase()) {
      case 'feeding':
        return { label: 'Feeding', icon: 'restaurant', bgClass: 'bg-amber-50 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300 border-amber-200 dark:border-amber-800/40', textClass: 'text-amber-700 dark:text-amber-300' };
      case 'health':
        return { label: 'Health', icon: 'medical_services', bgClass: 'bg-blue-50 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300 border-blue-200 dark:border-blue-800/40', textClass: 'text-blue-700 dark:text-blue-300' };
      case 'livestock':
        return { label: 'Livestock', icon: 'pets', bgClass: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800/40', textClass: 'text-emerald-700 dark:text-emerald-300' };
      case 'inventory':
        return { label: 'Inventory', icon: 'inventory_2', bgClass: 'bg-purple-50 text-purple-700 dark:bg-purple-900/30 dark:text-purple-300 border-purple-200 dark:border-purple-800/40', textClass: 'text-purple-700 dark:text-purple-300' };
      default:
        return { label: 'System', icon: 'smart_toy', bgClass: 'bg-sky-50 text-sky-700 dark:bg-sky-900/30 dark:text-sky-300 border-sky-200 dark:border-sky-800/40', textClass: 'text-sky-700 dark:text-sky-300' };
    }
  }

  onStartDateChange(val: string): void {
    this.params.update(p => ({ ...p, startDate: val || undefined, pageNumber: 1 }));
    this.refresh();
  }

  onEndDateChange(val: string): void {
    this.params.update(p => ({ ...p, endDate: val || undefined, pageNumber: 1 }));
    this.refresh();
  }

  onPaginatorChange(event: PageEvent): void {
    this.params.update(p => ({
      ...p,
      pageNumber: event.pageIndex + 1,
      pageSize: event.pageSize
    }));
    this.refresh();
  }

  clearFilters(): void {
    this.params.set({
      pageNumber: 1,
      pageSize: 10,
      sortBy: 'TransactionDate',
      sortDesc: true
    });
    this.refresh();
  }

  openIncomeDialog(): void {
    const dialogRef = this.dialog.open(IncomeFormDialogComponent, {
      width: '600px',
      disableClose: true,
      panelClass: ['premium-dialog-panel']
    });

    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(result => {
      if (result) this.refresh();
    });
  }

  openExpenseDialog(): void {
    const dialogRef = this.dialog.open(ExpenseFormDialogComponent, {
      width: '600px',
      disableClose: true,
      panelClass: ['premium-dialog-panel']
    });

    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(result => {
      if (result) this.refresh();
    });
  }

  openTransactionDetails(txn: FinancialTransaction): void {
    this.dialog.open(TransactionDetailDialogComponent, {
      width: '540px',
      data: txn,
      panelClass: ['premium-dialog-panel']
    });
  }

  openEditDialog(txn: FinancialTransaction): void {
    const dialogRef = this.dialog.open(EditTransactionDialogComponent, {
      width: '540px',
      data: txn,
      disableClose: true,
      panelClass: ['premium-dialog-panel']
    });

    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(result => {
      if (result) this.refresh();
    });
  }

  voidTransaction(txn: FinancialTransaction): void {
    if (!confirm(`Are you sure you want to void this ${txn.type} transaction of ${txn.amountBdt} BDT? This action cannot be undone.`)) {
      return;
    }

    const farmId = this.workingContextService.currentFarmValue?.id;
    if (!farmId) return;

    this.financeService.deleteTransaction(farmId, txn.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.refresh(),
      error: () => alert('Failed to void transaction. Please try again.')
    });
  }

  exportCsv(): void {
    const farmId = this.workingContextService.currentFarmValue?.id;
    if (!farmId) return;

    this.isExporting.set(true);
    this.financeService.exportTransactionsCsv(farmId, this.params()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `finance_ledger_${new Date().toISOString().substring(0, 10)}.csv`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.isExporting.set(false);
      },
      error: () => {
        alert('Failed to export CSV.');
        this.isExporting.set(false);
      }
    });
  }
}
