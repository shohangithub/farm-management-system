import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { RouterModule } from '@angular/router';
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
import { LoanRepaymentDialogComponent } from '../../components/loan-repayment-dialog/loan-repayment-dialog';
import { LoanRecord } from '../../models/finance.model';

@Component({
  selector: 'app-loan-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule,
    MatButtonModule,
    MatDialogModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    CurrencyPipe,
    DatePipe
  ],
  template: `
    <app-page-header 
      title="Loans & Liabilities" 
      description="Manage farm financing, loans, and track repayments."
      breadcrumbActiveNode="Loans">
      <div actions>
        <button mat-flat-button color="primary" (click)="openLoanDialog()" class="!rounded-xl !px-6 !py-2.5 !bg-primary-600 hover:!bg-primary-700 !text-white flex items-center gap-2 shadow-sm shadow-primary-500/30 transition-all">
          <mat-icon class="!text-[20px]">add</mat-icon>
          <span>New Loan</span>
        </button>
      </div>
    </app-page-header>

    <div class="px-6 py-4 mx-auto max-w-7xl space-y-6">

      <!-- Sub-Navigation Bar -->
      <div class="flex items-center gap-2 border-b border-gray-200 dark:border-gray-700/60 pb-3 overflow-x-auto">
        <a routerLink="/finance" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">dashboard</mat-icon> Overview
        </a>
        <a routerLink="/finance/transactions" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">receipt_long</mat-icon> General Ledger
        </a>
        <a routerLink="/finance/reports/monthly-pnl" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">calendar_view_month</mat-icon> Monthly P&L
        </a>
        <a routerLink="/finance/reports/trial-balance" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">balance</mat-icon> Trial Balance
        </a>
        <a routerLink="/finance/reports/balance-sheet" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">account_balance_wallet</mat-icon> Balance Sheet
        </a>
        <a routerLink="/finance/loans" 
          class="px-4 py-2 rounded-xl text-xs font-semibold bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800/60 flex items-center gap-2 whitespace-nowrap">
          <mat-icon class="text-base">account_balance</mat-icon> Loans & Liabilities
        </a>
        <a routerLink="/finance/investors" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">groups</mat-icon> Investors & Equity
        </a>
      </div>

      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <div *ngIf="!isLoading()" class="space-y-6">
      
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
          
          <!-- Background Watermark -->
          <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-indigo-500/5 rotate-[-10deg] pointer-events-none">real_estate_agent</mat-icon>
          
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
              <span class="text-sm font-bold" [ngClass]="loan.outstandingBalanceBdt > 0 ? 'text-rose-600 dark:text-rose-400' : 'text-emerald-600 dark:text-emerald-400'">
                {{ loan.outstandingBalanceBdt | currency:'BDT ':'symbol':'1.0-0' }}
              </span>
            </div>
          </div>

          <!-- Progress Bar -->
          <div class="mt-6 pt-2">
            <div class="flex justify-between text-xs font-bold uppercase tracking-wider mb-2">
              <span class="text-gray-500">Repayment Progress</span>
              <span class="text-emerald-600 dark:text-emerald-400">{{ loan.repaymentProgressPercent }}%</span>
            </div>
            <div class="w-full h-2 bg-gray-100 dark:bg-gray-700 rounded-full overflow-hidden">
              <div class="h-full bg-gradient-to-r from-emerald-400 to-emerald-600 rounded-full transition-all duration-500" [style.width.%]="loan.repaymentProgressPercent"></div>
            </div>
            <p class="text-xs text-center text-gray-500 mt-2">Total Repaid: {{ loan.totalRepaidBdt | currency:'BDT ':'symbol':'1.0-0' }}</p>
          </div>

          <!-- Action Button -->
          <div *ngIf="loan.isActive" class="mt-5 pt-4 border-t border-gray-100 dark:border-gray-700/50">
            <button (click)="openRepaymentDialog(loan)" 
              class="w-full py-2.5 px-4 text-sm font-semibold text-white bg-gradient-to-r from-emerald-500 to-teal-600 rounded-xl hover:from-emerald-600 hover:to-teal-700 transition-all shadow-sm shadow-emerald-500/20 flex items-center justify-center gap-2">
              <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">payments</mat-icon>
              Make Repayment
            </button>
          </div>

          <!-- Settled Badge -->
          <div *ngIf="!loan.isActive" class="mt-5 pt-4 border-t border-gray-100 dark:border-gray-700/50">
            <div class="w-full py-2.5 px-4 text-sm font-semibold text-emerald-700 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-900/20 rounded-xl flex items-center justify-center gap-2 border border-emerald-200 dark:border-emerald-800/30">
              <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">check_circle</mat-icon>
              Fully Settled
            </div>
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
        this.refreshTrigger$.next();
      }
    });
  }

  openRepaymentDialog(loan: LoanRecord): void {
    const dialogRef = this.dialog.open(LoanRepaymentDialogComponent, {
      width: '500px',
      disableClose: true,
      panelClass: ['premium-dialog-panel'],
      data: loan
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.refreshTrigger$.next();
      }
    });
  }
}
