import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { toSignal } from '@angular/core/rxjs-interop';
import { forkJoin, switchMap, BehaviorSubject, of, catchError } from 'rxjs';
import { DatePipe, CurrencyPipe } from '@angular/common';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { FinancialTransaction } from '../../models/finance.model';
import { IncomeFormDialogComponent } from '../../components/income-form-dialog/income-form-dialog';
import { ExpenseFormDialogComponent } from '../../components/expense-form-dialog/expense-form-dialog';

@Component({
  selector: 'app-finance-ledger',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    MatButtonModule,
    MatDialogModule,
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
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);
  private dialog = inject(MatDialog);

  private refreshTrigger$ = new BehaviorSubject<void>(undefined);

  private readonly ledgerData$ = this.refreshTrigger$.pipe(
    switchMap(() => {
      const currentFarmId = this.workingContextService.currentFarmValue?.id;
      if (!currentFarmId) {
        return of({ transactions: [] });
      }
      return forkJoin({
        transactions: this.financeService.getTransactions(currentFarmId).pipe(catchError(() => of([])))
      });
    })
  );

  private readonly ledgerData = toSignal(this.ledgerData$);

  readonly transactions = computed(() => this.ledgerData()?.transactions ?? []);
  
  // Loading is true if we are waiting for the initial emission or data is undefined
  readonly isLoading = computed(() => this.ledgerData() === undefined);

  ngOnInit(): void {
  }

  openIncomeDialog(): void {
    const dialogRef = this.dialog.open(IncomeFormDialogComponent, {
      width: '600px',
      disableClose: true,
      panelClass: ['premium-dialog-panel']
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) this.refreshTrigger$.next();
    });
  }

  openExpenseDialog(): void {
    const dialogRef = this.dialog.open(ExpenseFormDialogComponent, {
      width: '600px',
      disableClose: true,
      panelClass: ['premium-dialog-panel']
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) this.refreshTrigger$.next();
    });
  }
}
