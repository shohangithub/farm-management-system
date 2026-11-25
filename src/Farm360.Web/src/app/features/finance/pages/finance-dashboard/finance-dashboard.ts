import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, DestroyRef } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap, BehaviorSubject, combineLatest } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceSubnavComponent } from '../../components/finance-subnav/finance-subnav.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { IncomeFormDialogComponent } from '../../components/income-form-dialog/income-form-dialog';
import { ExpenseFormDialogComponent } from '../../components/expense-form-dialog/expense-form-dialog';
import { AllocateOverheadDialogComponent } from '../../components/allocate-overhead-dialog/allocate-overhead-dialog';
import { MonthlyCashFlowPoint, categoryLabel } from '../../models/finance.model';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

@Component({
  selector: 'app-finance-dashboard',
  standalone: true,
  imports: [TranslatePipe, MoneyPipe, HasPermissionDirective, ErrorStateComponent,
    FinanceSubnavComponent,
    CommonModule,
    RouterLink,
    MatIconModule,
    MatButtonModule,
    MatDialogModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    DecimalPipe
  ],
  templateUrl: './finance-dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FinanceDashboardComponent implements OnInit {
  private readonly financeService = inject(FinanceService);
  private readonly workingContextService = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(I18nService);
  readonly couldNotLoadTitle = this.i18n.t("Couldn't load the finance dashboard");
  
  readonly Math = Math;
  private readonly refreshTrigger$ = new BehaviorSubject<void>(undefined);

  readonly loadError = signal<string | null>(null);

  // Reload when the farm is chosen or changed, not only on open: after a page refresh the farm
  // arrives a moment later, and reading it once showed "No Financial Activity".
  private readonly dashboardData$ = combineLatest([this.workingContextService.currentFarm$, this.refreshTrigger$]).pipe(
    switchMap(([farm]) => {
      this.loadError.set(null);
      const currentFarmId = farm?.id;
      if (!currentFarmId) {
        return of(null);
      }
      return this.financeService.getDashboard(currentFarmId).pipe(
        catchError(err => {
          this.loadError.set(parseApiError(err, this.i18n.t('Could not load the finance dashboard.')));
          return of(null);
        })
      );
    })
  );

  readonly dashboardData = toSignal(this.dashboardData$);
  readonly isLoading = computed(() => this.dashboardData() === undefined);

  ngOnInit(): void {}

  /** The head's friendly name in the interface language (never the raw code). */
  categoryText(category: string): string {
    return categoryLabel(category);
  }

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
    const dialogRef = this.dialog.open(IncomeFormDialogComponent, { maxWidth: '95vw',
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
    const dialogRef = this.dialog.open(ExpenseFormDialogComponent, { maxWidth: '95vw',
      width: '550px',
      panelClass: 'dialog-responsive'
    });

    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(result => {
      if (result) {
        this.refresh();
      }
    });
  }

  openAllocateOverheadDialog(): void {
    const dialogRef = this.dialog.open(AllocateOverheadDialogComponent, { maxWidth: '95vw',
      width: '560px',
      panelClass: 'dialog-responsive'
    });

    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(result => {
      if (result) {
        this.refresh();
      }
    });
  }
}
