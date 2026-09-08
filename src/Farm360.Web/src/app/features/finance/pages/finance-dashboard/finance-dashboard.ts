import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, DestroyRef } from '@angular/core';
import { CommonModule, CurrencyPipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap, BehaviorSubject } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { IncomeFormDialogComponent } from '../../components/income-form-dialog/income-form-dialog';
import { ExpenseFormDialogComponent } from '../../components/expense-form-dialog/expense-form-dialog';
import { MonthlyCashFlowPoint } from '../../models/finance.model';

@Component({
  selector: 'app-finance-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatIconModule,
    MatButtonModule,
    MatDialogModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    CurrencyPipe,
    DecimalPipe
  ],
  templateUrl: './finance-dashboard.html',
  styleUrls: ['./finance-dashboard.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FinanceDashboardComponent implements OnInit {
  private readonly financeService = inject(FinanceService);
  private readonly workingContextService = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);
  
  readonly Math = Math;
  private readonly refreshTrigger$ = new BehaviorSubject<void>(undefined);

  private readonly dashboardData$ = this.refreshTrigger$.pipe(
    switchMap(() => {
      const currentFarmId = this.workingContextService.currentFarmValue?.id;
      if (!currentFarmId) {
        return of(null);
      }
      return this.financeService.getDashboard(currentFarmId).pipe(
        catchError(() => of(null))
      );
    })
  );

  readonly dashboardData = toSignal(this.dashboardData$);
  readonly isLoading = computed(() => this.dashboardData() === undefined);

  ngOnInit(): void {}

  refresh(): void {
    this.refreshTrigger$.next();
  }

  getBarHeight(value: number, trendList: MonthlyCashFlowPoint[]): number {
    if (!trendList || trendList.length === 0) return 0;
    const maxVal = Math.max(
      ...trendList.flatMap(t => [t.incomeBdt, t.expenseBdt, 100])
    );
    if (maxVal === 0) return 0;
    const percentage = (value / maxVal) * 100;
    return Math.min(Math.max(percentage, 5), 100);
  }

  openIncomeDialog(): void {
    const dialogRef = this.dialog.open(IncomeFormDialogComponent, {
      width: '550px',
      panelClass: 'dialog-responsive'
    });

    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(result => {
      if (result) {
        this.refresh();
      }
    });
  }

  openExpenseDialog(): void {
    const dialogRef = this.dialog.open(ExpenseFormDialogComponent, {
      width: '550px',
      panelClass: 'dialog-responsive'
    });

    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(result => {
      if (result) {
        this.refresh();
      }
    });
  }
}
