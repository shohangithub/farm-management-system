import { Component, ChangeDetectionStrategy, inject, signal, computed, DestroyRef, ViewChild, ElementRef } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of, combineLatest, BehaviorSubject } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceSubnavComponent } from '../../components/finance-subnav/finance-subnav.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';
import { ReportService } from '../../../reports/services/report.service';
import { TrialBalance, TrialBalanceLine } from '../../models/finance.model';

import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { formatMoney } from '../../../../shared/pipes/money.pipe';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';
@Component({
  selector: 'app-trial-balance',
  standalone: true,
  imports: [MatMenuModule, MoneyPipe, ErrorStateComponent,
    FinanceSubnavComponent,
    CommonModule,
    RouterLink,
    ReactiveFormsModule,
    MatIconModule,
    MatButtonModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    CurrencyPipe,
    DatePipe,
    TranslatePipe
  ],
  template: `
    <app-page-header 
      [title]="'Trial Balance' | t" 
      [description]="'Debit and credit totals for every account. Both sides should match.' | t"
      [breadcrumbActiveNode]="'Trial Balance' | t">
      <div actions class="flex flex-wrap items-center gap-2">
        <form [formGroup]="filterForm" class="flex flex-wrap items-center gap-2">
          <div class="flex items-center gap-1.5 px-3 py-1.5 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl text-xs font-medium">
            <span class="text-gray-500 dark:text-gray-400">{{ 'As of:' | t }}</span>
            <input 
              type="date" 
              formControlName="asOfDate" 
              class="bg-transparent text-gray-800 dark:text-gray-200 text-xs font-semibold outline-none cursor-pointer" />
          </div>
        </form>
        <button mat-stroked-button type="button" [matMenuTriggerFor]="exportMenu"
          class="!rounded-xl !min-h-[40px] !border-gray-200 dark:!border-gray-700 !text-gray-700 dark:!text-gray-200 hover:!bg-gray-50 dark:hover:!bg-gray-800 !text-xs !font-semibold">
          <mat-icon class="!w-4 !h-4 !text-[16px]">download</mat-icon>
          <span>{{ 'Download' | t }}</span>
          <mat-icon iconPositionEnd class="!w-4 !h-4 !text-[16px]">expand_more</mat-icon>
        </button>
        <mat-menu #exportMenu="matMenu" xPosition="before">
          <button mat-menu-item (click)="exportPdf()" [disabled]="isExporting() || !tbData()"><mat-icon>picture_as_pdf</mat-icon><span>{{ 'Download PDF' | t }}</span></button>
          <button mat-menu-item (click)="exportExcel()" [disabled]="!tbData()"><mat-icon>grid_on</mat-icon><span>{{ 'Download Excel' | t }}</span></button>
          <a mat-menu-item routerLink="/reports/finance.trial-balance"><mat-icon>table_view</mat-icon><span>{{ 'Open as report' | t }}</span></a>
        </mat-menu>
        <button mat-flat-button type="button" (click)="printReport()"
          class="!rounded-xl !min-h-[40px] !bg-primary-600 hover:!bg-primary-700 !text-white !text-xs !font-semibold">
          <mat-icon class="!w-4 !h-4 !text-[16px]">print</mat-icon>
          <span>{{ 'Print' | t }}</span>
        </button>
      </div>
    </app-page-header>

    <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

    <div *ngIf="!isLoading()" class="px-6 py-4 mx-auto max-w-7xl space-y-6">

      <app-finance-subnav></app-finance-subnav>

      <app-error-state *ngIf="loadError() as err" [title]="loadErrorTitle" [message]="err" (retry)="retryLoad()"></app-error-state>

      <app-empty-state
        *ngIf="!loadError() && (!tbData() || !tbData()?.lines?.length)"
        icon="account_balance_wallet"
        [title]="'No Ledger Records Found' | t"
        [description]="'There are no transaction lines recorded as of the selected date. Start by recording revenue or expenses.' | t">
      </app-empty-state>

      <div *ngIf="tbData() as data" #reportSheet id="trialBalanceReport" class="space-y-6">

        <!-- Top Status & KPI Cards -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <!-- Total Debits -->
          <div class="bg-white dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">{{ 'Total Debits' | t }}</p>
                <h3 class="text-2xl font-black text-gray-900 dark:text-white mt-1">
                  {{ data.totalDebitBdt | money }}
                </h3>
                <p class="text-[11px] text-gray-400 mt-0.5">{{ 'Assets, Expenses, Drawings' | t }}</p>
              </div>
              <div class="w-11 h-11 rounded-2xl bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-300 flex items-center justify-center">
                <mat-icon class="text-lg">arrow_downward</mat-icon>
              </div>
            </div>
          </div>

          <!-- Total Credits -->
          <div class="bg-white dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">{{ 'Total Credits' | t }}</p>
                <h3 class="text-2xl font-black text-gray-900 dark:text-white mt-1">
                  {{ data.totalCreditBdt | money }}
                </h3>
                <p class="text-[11px] text-gray-400 mt-0.5">{{ 'Revenue, Liabilities, Capital' | t }}</p>
              </div>
              <div class="w-11 h-11 rounded-2xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                <mat-icon class="text-lg">arrow_upward</mat-icon>
              </div>
            </div>
          </div>

          <!-- Balance Integrity -->
          <div class="bg-white dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 relative overflow-hidden"
               [ngClass]="data.isBalanced ? 'bg-gradient-to-br from-emerald-50/50 to-teal-50/50 dark:from-emerald-950/20 dark:to-teal-950/20' : 'bg-gradient-to-br from-amber-50/60 to-rose-50/60 dark:from-amber-950/20 dark:to-rose-950/20'">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">{{ 'Ledger Integrity' | t }}</p>
                <div class="flex items-center gap-2 mt-1">
                  <span *ngIf="data.isBalanced" class="px-2.5 py-0.5 rounded-full text-xs font-bold bg-emerald-100 dark:bg-emerald-900/50 text-emerald-700 dark:text-emerald-300 flex items-center gap-1">
                    <mat-icon class="text-xs !w-3.5 !h-3.5 !text-[14px]">check_circle</mat-icon> {{ 'Balanced' | t }}
                  </span>
                  <span *ngIf="!data.isBalanced" class="px-2.5 py-0.5 rounded-full text-xs font-bold bg-rose-100 dark:bg-rose-900/50 text-rose-700 dark:text-rose-300 flex items-center gap-1">
                    <mat-icon class="text-xs !w-3.5 !h-3.5 !text-[14px]">warning</mat-icon> {{ 'Discrepancy' | t }}
                  </span>
                </div>
                <p class="text-[11px] mt-1 font-semibold" [ngClass]="data.isBalanced ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">
                  {{ data.isBalanced ? ('Debits equal Credits (100% in sync)' | t) : ('Difference: {amount}' | t: { amount: (data.differenceBdt | money:2) }) }}
                </p>
              </div>
              <div class="w-11 h-11 rounded-2xl flex items-center justify-center text-white"
                   [ngClass]="data.isBalanced ? 'bg-gradient-to-br from-emerald-500 to-teal-600' : 'bg-gradient-to-br from-rose-500 to-amber-600 shadow-md shadow-rose-500/20'">
                <mat-icon class="text-lg">{{ data.isBalanced ? 'verified' : 'priority_high' }}</mat-icon>
              </div>
            </div>
          </div>

          <!-- Total Accounts -->
          <div class="bg-white dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">{{ 'Active Accounts' | t }}</p>
                <h3 class="text-2xl font-black text-gray-900 dark:text-white mt-1">
                  {{ data.lines.length }}
                </h3>
                <p class="text-[11px] text-gray-400 mt-0.5">{{ 'As of {date}' | t: { date: (data.asOfDate | date:'mediumDate') } }}</p>
              </div>
              <div class="w-11 h-11 rounded-2xl bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-300 flex items-center justify-center">
                <mat-icon class="text-lg">format_list_bulleted</mat-icon>
              </div>
            </div>
          </div>
        </div>

        <!-- Filter Classification Tabs -->
        <div class="flex items-center justify-between flex-wrap gap-3">
          <div class="flex items-center gap-1.5 p-1 bg-gray-100 dark:bg-gray-800/60 rounded-xl text-xs font-semibold">
            <button 
              type="button"
              (click)="selectedGroup.set('ALL')"
              class="px-3 py-1.5 rounded-lg transition-all"
              [ngClass]="selectedGroup() === 'ALL' ? 'bg-white dark:bg-gray-700 text-gray-900 dark:text-white shadow-sm font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-gray-900'">
              {{ 'All ({n})' | t: { n: data.lines.length } }}
            </button>
            <button 
              type="button"
              (click)="selectedGroup.set('Asset')"
              class="px-3 py-1.5 rounded-lg transition-all"
              [ngClass]="selectedGroup() === 'Asset' ? 'bg-white dark:bg-gray-700 text-blue-600 dark:text-blue-400 shadow-sm font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-gray-900'">
              {{ 'Assets' | t }}
            </button>
            <button 
              type="button"
              (click)="selectedGroup.set('Liability')"
              class="px-3 py-1.5 rounded-lg transition-all"
              [ngClass]="selectedGroup() === 'Liability' ? 'bg-white dark:bg-gray-700 text-amber-600 dark:text-amber-400 shadow-sm font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-gray-900'">
              {{ 'Liabilities' | t }}
            </button>
            <button 
              type="button"
              (click)="selectedGroup.set('Equity')"
              class="px-3 py-1.5 rounded-lg transition-all"
              [ngClass]="selectedGroup() === 'Equity' ? 'bg-white dark:bg-gray-700 text-purple-600 dark:text-purple-400 shadow-sm font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-gray-900'">
              {{ 'Equity' | t }}
            </button>
            <button 
              type="button"
              (click)="selectedGroup.set('Revenue')"
              class="px-3 py-1.5 rounded-lg transition-all"
              [ngClass]="selectedGroup() === 'Revenue' ? 'bg-white dark:bg-gray-700 text-emerald-600 dark:text-emerald-400 shadow-sm font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-gray-900'">
              {{ 'Revenue' | t }}
            </button>
            <button 
              type="button"
              (click)="selectedGroup.set('Expense')"
              class="px-3 py-1.5 rounded-lg transition-all"
              [ngClass]="selectedGroup() === 'Expense' ? 'bg-white dark:bg-gray-700 text-rose-600 dark:text-rose-400 shadow-sm font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-gray-900'">
              {{ 'Expenses' | t }}
            </button>
          </div>

          <div class="text-xs text-gray-500 dark:text-gray-400 font-medium">
            {{ 'Showing {shown} of {total} accounts' | t: { shown: filteredLines().length, total: data.lines.length } }}
          </div>
        </div>

        <!-- Main Trial Balance Table -->
        <div class="bg-white dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative print:border-none print:shadow-none">
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse">
              <thead>
                <tr class="border-b border-gray-200 dark:border-gray-700/60 bg-gray-50/80 dark:bg-gray-900/40 text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">
                  <th class="py-3.5 px-5">{{ 'Code' | t }}</th>
                  <th class="py-3.5 px-5">{{ 'Account Name' | t }}</th>
                  <th class="py-3.5 px-5">{{ 'Classification' | t }}</th>
                  <th class="py-3.5 px-5 text-right">{{ 'Debit (BDT)' | t }}</th>
                  <th class="py-3.5 px-5 text-right">{{ 'Credit (BDT)' | t }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-800/50 text-sm">
                <tr *ngFor="let line of filteredLines()" 
                    class="hover:bg-gray-50/50 dark:hover:bg-gray-700/20 transition-colors">
                  <td class="py-3.5 px-5 font-mono text-xs font-bold text-gray-600 dark:text-gray-400">
                    {{ line.accountCode }}
                  </td>
                  <td class="py-3.5 px-5 font-semibold text-gray-900 dark:text-white">
                    {{ line.accountName }}
                  </td>
                  <td class="py-3.5 px-5">
                    <span [ngClass]="getBadgeClass(line.categoryGroup)"
                          class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold">
                      {{ line.categoryGroup | t }}
                    </span>
                  </td>
                  <td class="py-3.5 px-5 text-right font-mono font-medium text-gray-800 dark:text-gray-200">
                    <span *ngIf="line.debitBdt > 0">{{ line.debitBdt | currency:'':'':'1.2-2' }}</span>
                    <span *ngIf="line.debitBdt === 0" class="text-gray-300 dark:text-gray-600">-</span>
                  </td>
                  <td class="py-3.5 px-5 text-right font-mono font-medium text-gray-800 dark:text-gray-200">
                    <span *ngIf="line.creditBdt > 0">{{ line.creditBdt | currency:'':'':'1.2-2' }}</span>
                    <span *ngIf="line.creditBdt === 0" class="text-gray-300 dark:text-gray-600">-</span>
                  </td>
                </tr>
              </tbody>
              <!-- Accounting Double Underline Totals Footer -->
              <tfoot>
                <tr class="border-t-2 border-b-4 border-gray-900 dark:border-gray-200 bg-gray-50 dark:bg-gray-900/60 font-black text-sm">
                  <td colspan="3" class="py-4 px-5 text-gray-900 dark:text-white uppercase tracking-wider flex items-center gap-2">
                    <span>{{ 'Total Balances' | t }}</span>
                    <span *ngIf="data.isBalanced" class="text-xs font-bold text-emerald-600 dark:text-emerald-400">{{ '✓ In Equilibrium' | t }}</span>
                  </td>
                  <td class="py-4 px-5 text-right font-mono text-base font-black text-blue-700 dark:text-blue-400">
                    {{ data.totalDebitBdt | money:2 }}
                  </td>
                  <td class="py-4 px-5 text-right font-mono text-base font-black text-emerald-700 dark:text-emerald-400">
                    {{ data.totalCreditBdt | money:2 }}
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>

      </div>

    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TrialBalanceComponent {
  private readonly fb = inject(FormBuilder);
  private readonly financeService = inject(FinanceService);
  private readonly workingContextService = inject(WorkingContextService);
  private readonly pdfExportService = inject(PdfExportService);
  private readonly reportService = inject(ReportService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(I18nService);
  readonly loadErrorTitle = this.i18n.t("Couldn't load the trial balance");

  @ViewChild('reportSheet') reportSheet?: ElementRef<HTMLElement>;
  readonly isExporting = signal(false);

  readonly selectedGroup = signal<'ALL' | 'Asset' | 'Liability' | 'Equity' | 'Revenue' | 'Expense'>('ALL');

  private readonly todayStr = toLocalIsoDate();

  readonly filterForm = this.fb.group({
    asOfDate: [this.todayStr]
  });

  private readonly filterChanges$ = new BehaviorSubject<{ asOfDate: string }>({
    asOfDate: this.todayStr
  });

  constructor() {
    this.filterForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(val => {
        if (val.asOfDate) {
          this.filterChanges$.next({ asOfDate: val.asOfDate });
        }
      });
  }

  private readonly tbData$ = combineLatest([
    this.filterChanges$,
    this.workingContextService.currentFarm$
  ]).pipe(
    switchMap(([filter, farm]) => {
      this.loadError.set(null);
      if (!farm) return of(null);
      return this.financeService.getTrialBalance(farm.id, filter.asOfDate).pipe(
        catchError(err => {
          this.loadError.set(parseApiError(err, this.i18n.t('Could not load the trial balance.')));
          return of(null);
        })
      );
    })
  );

  readonly loadError = signal<string | null>(null);

  retryLoad(): void {
    this.filterChanges$.next(this.filterChanges$.value);
  }

  readonly tbData = toSignal(this.tbData$);
  readonly isLoading = computed(() => this.tbData() === undefined);

  readonly filteredLines = computed<TrialBalanceLine[]>(() => {
    const data = this.tbData();
    if (!data || !data.lines) return [];
    const group = this.selectedGroup();
    if (group === 'ALL') return data.lines;
    return data.lines.filter(l => l.categoryGroup.toLowerCase() === group.toLowerCase());
  });

  getBadgeClass(group: string): string {
    switch (group.toLowerCase()) {
      case 'asset':
        return 'bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300';
      case 'liability':
        return 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300';
      case 'equity':
        return 'bg-purple-100 dark:bg-purple-900/40 text-purple-700 dark:text-purple-300';
      case 'revenue':
        return 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300';
      case 'expense':
        return 'bg-rose-100 dark:bg-rose-900/40 text-rose-700 dark:text-rose-300';
      default:
        return 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300';
    }
  }

  async exportPdf(): Promise<void> {
    const farmId = this.workingContextService.currentFarmValue?.id;
    const asOf = this.filterForm.value.asOfDate || this.todayStr;
    this.isExporting.set(true);

    // Try server-side vector QuestPDF report
    this.reportService.export('finance.trial-balance', 'pdf', {
      parameters: {
        farmId: farmId || null,
        asOfDate: asOf
      }
    }).subscribe({
      next: res => {
        if (res.body) {
          const fn = this.reportService.fileNameFrom(res.headers, `Farm360_Trial_Balance_${asOf}.pdf`);
          this.reportService.saveBlob(res.body, fn);
        }
        this.isExporting.set(false);
      },
      error: async err => {
        console.warn('Server-side PDF export fallback to client-side renderer:', err);
        const el = this.reportSheet?.nativeElement;
        if (el) {
          const data = this.tbData();
          const farm = this.workingContextService.currentFarmValue;
          const org = this.workingContextService.currentOrgValue;
          try {
            await this.pdfExportService.exportElement(el, {
              filename: `Farm360_Trial_Balance_${asOf}`,
              orientation: 'landscape',
              header: {
                title: this.i18n.t('Trial Balance Report'),
                subtitle: this.i18n.t('Debit & Credit Ledger Accounts Verification'),
                farmName: farm?.name || 'Primary Farm',
                orgName: org?.name || 'Farm360 Enterprise',
                dateRange: this.i18n.t('As of {date}', { date: asOf }),
                currency: 'BDT (৳)',
                metaFields: [
                  { label: this.i18n.t('Total Debits'), value: `${formatMoney((data?.totalDebitBdt || 0))}` },
                  { label: this.i18n.t('Total Credits'), value: `${formatMoney((data?.totalCreditBdt || 0))}` },
                  { label: this.i18n.t('Integrity'), value: data?.isBalanced ? this.i18n.t('Balanced (100% in sync)') : this.i18n.t('Variance: {amount}', { amount: formatMoney((data?.differenceBdt || 0)) }) }
                ]
              },
              showSignatures: true
            });
          } catch (clientErr) {
            console.error('Client PDF export failed:', clientErr);
          }
        }
        this.isExporting.set(false);
      }
    });
  }

  exportExcel(): void {
    const farmId = this.workingContextService.currentFarmValue?.id;
    const asOf = this.filterForm.value.asOfDate || this.todayStr;

    this.reportService.export('finance.trial-balance', 'xlsx', {
      parameters: {
        farmId: farmId || null,
        asOfDate: asOf
      }
    }).subscribe({
      next: res => {
        if (res.body) {
          const fn = this.reportService.fileNameFrom(res.headers, `Farm360_Trial_Balance_${asOf}.xlsx`);
          this.reportService.saveBlob(res.body, fn);
        }
      },
      error: err => console.error('Failed to export Excel:', err)
    });
  }

  printReport(): void {
    window.print();
  }
}
