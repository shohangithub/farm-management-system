import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe, PercentPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of, combineLatest, BehaviorSubject } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { LoanFormDialogComponent } from '../../components/loan-form-dialog/loan-form-dialog';

@Component({
  selector: 'app-loan-list',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    MatButtonModule,
    MatDialogModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    CurrencyPipe,
    DatePipe,
    PercentPipe
  ],
  template: `
    <app-page-header 
      title="Loans & Investments" 
      description="Manage farm financing, loans, and track repayments."
      breadcrumbActiveNode="Loans">
      <div actions>
        <button mat-flat-button color="primary" (click)="openLoanDialog()" class="!rounded-xl !px-6 !py-2.5 !bg-primary-600 hover:!bg-primary-700 !text-white flex items-center gap-2 shadow-sm shadow-primary-500/30 transition-all">
          <mat-icon class="!text-[20px]">add</mat-icon>
          <span>New Loan</span>
        </button>
      </div>
    </app-page-header>

    <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

    <div *ngIf="!isLoading()" class="p-6 max-w-7xl mx-auto space-y-6">
      
      <app-empty-state 
        *ngIf="!loans() || loans()!.length === 0"
        icon="account_balance"
        title="No Active Loans"
        description="The farm currently has no recorded loans or external investments."
        actionText="Add New Loan"
        (action)="openLoanDialog()">
      </app-empty-state>

      <!-- Loans Grid -->
      <div *ngIf="loans() && loans()!.length > 0" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        
        <div *ngFor="let loan of loans()" class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative p-6">
          
          <div class="flex justify-between items-start mb-6">
            <div class="flex items-center gap-3">
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-indigo-500 to-purple-600 text-white flex items-center justify-center shadow-md shadow-indigo-500/20">
                <mat-icon>real_estate_agent</mat-icon>
              </div>
              <div>
                <h3 class="text-lg font-bold text-gray-900 dark:text-white">{{ loan.lenderName }}</h3>
                <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">Disbursed: {{ loan.disbursementDate | date:'mediumDate' }}</p>
              </div>
            </div>
            <span class="px-2.5 py-1 text-xs font-semibold rounded-full" [ngClass]="loan.isActive ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400' : 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-400'">
              {{ loan.isActive ? 'Active' : 'Settled' }}
            </span>
          </div>

          <div class="space-y-4">
            <div class="flex justify-between items-center pb-3 border-b border-gray-100 dark:border-gray-700/50">
              <span class="text-sm font-medium text-gray-500 dark:text-gray-400">Principal Amount</span>
              <span class="text-sm font-bold text-gray-900 dark:text-white">{{ loan.principalAmountBdt | currency:'BDT ':'symbol':'1.0-0' }}</span>
            </div>
            
            <div class="flex justify-between items-center pb-3 border-b border-gray-100 dark:border-gray-700/50">
              <span class="text-sm font-medium text-gray-500 dark:text-gray-400">Interest Rate</span>
              <span class="text-sm font-bold text-gray-900 dark:text-white">{{ loan.interestRatePercent }}%</span>
            </div>

            <div class="flex justify-between items-center pb-3 border-b border-gray-100 dark:border-gray-700/50">
              <span class="text-sm font-medium text-gray-500 dark:text-gray-400">Outstanding Balance</span>
              <span class="text-sm font-bold text-rose-600 dark:text-rose-400">{{ loan.outstandingBalanceBdt | currency:'BDT ':'symbol':'1.0-0' }}</span>
            </div>
          </div>

          <!-- Progress Bar -->
          <div class="mt-6 pt-2">
            <div class="flex justify-between text-xs font-bold uppercase tracking-wider mb-2">
              <span class="text-gray-500">Repayment Progress</span>
              <span class="text-emerald-600 dark:text-emerald-400">{{ loan.repaymentProgressPercent | percent:'1.0-0' }}</span>
            </div>
            <div class="w-full h-2 bg-gray-100 dark:bg-gray-700 rounded-full overflow-hidden">
              <div class="h-full bg-gradient-to-r from-emerald-400 to-emerald-600 rounded-full" [style.width.%]="loan.repaymentProgressPercent * 100"></div>
            </div>
            <p class="text-xs text-center text-gray-500 mt-2">Total Repaid: {{ loan.totalRepaidBdt | currency:'BDT ':'symbol':'1.0-0' }}</p>
          </div>

        </div>

      </div>

    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoanListComponent implements OnInit {
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);
  private dialog = inject(MatDialog);

  private refreshTrigger$ = new BehaviorSubject<void>(undefined);

  private readonly loans$ = combineLatest([
    this.refreshTrigger$,
    this.workingContextService.currentFarm$
  ]).pipe(
    switchMap(([_, farm]) => {
      if (!farm) {
        return of([]);
      }
      return this.financeService.getLoans(farm.id).pipe(
        catchError(() => of([]))
      );
    })
  );

  readonly loans = toSignal(this.loans$);
  readonly isLoading = computed(() => this.loans() === undefined);

  ngOnInit(): void { }

  openLoanDialog(): void {
    const dialogRef = this.dialog.open(LoanFormDialogComponent, {
      width: '600px',
      disableClose: true,
      panelClass: ['premium-dialog-panel']
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.refreshTrigger$.next(); // Refresh list on success
      }
    });
  }
}
