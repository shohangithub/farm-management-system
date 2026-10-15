import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, catchError, combineLatest, of, switchMap, tap } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { ReportService } from '../../../reports/services/report.service';
import { FinanceService } from '../../services/finance.service';
import { FinanceSubnavComponent } from '../../components/finance-subnav/finance-subnav.component';
import { IncomeStatement, IncomeStatementTotals } from '../../models/finance.model';

interface Period { fromDate: string | null; toDate: string; comparative: boolean; }

/**
 * Statement of profit or loss: revenue, cost of sales, gross profit, operating expenses
 * (depreciation included), finance costs and net profit, beside the same period last year.
 * Reads the same ledger as the balance sheet, so the financial-year figure equals its
 * "Profit for the year".
 */
@Component({
  selector: 'app-income-statement',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatIconModule, MatButtonModule, PageHeaderComponent, FinanceSubnavComponent, CurrencyPipe, DatePipe],
  template: `
    <app-page-header
      title="Profit & Loss Statement"
      description="Statement of profit or loss for the financial year (or any period), compared with the same period last year."
      breadcrumbActiveNode="Profit & Loss">
      <div actions class="flex items-center gap-2 flex-wrap">
        <div class="flex items-center gap-1.5 px-3 py-1.5 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl text-xs font-medium">
          <span class="text-gray-500">From</span>
          <input type="date" [ngModel]="period.fromDate" (ngModelChange)="setPeriod({ fromDate: $event || null })"
                 class="bg-transparent text-xs font-semibold outline-none" />
          <span class="text-gray-500">to</span>
          <input type="date" [ngModel]="period.toDate" (ngModelChange)="setPeriod({ toDate: $event })"
                 class="bg-transparent text-xs font-semibold outline-none" />
        </div>
        <label class="flex items-center gap-1.5 px-3 py-1.5 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl text-xs cursor-pointer">
          <input type="checkbox" [ngModel]="period.comparative" (ngModelChange)="setPeriod({ comparative: $event })" class="accent-emerald-600" />
          <span>Compare last year</span>
        </label>
        <button mat-stroked-button (click)="export('pdf')" class="!rounded-xl !text-xs">
          <mat-icon class="!text-[16px] !w-4 !h-4">picture_as_pdf</mat-icon> PDF
        </button>
        <button mat-stroked-button (click)="export('xlsx')" class="!rounded-xl !text-xs">
          <mat-icon class="!text-[16px] !w-4 !h-4">file_download</mat-icon> Excel
        </button>
        <button mat-stroked-button (click)="print()" class="!rounded-xl !text-xs">
          <mat-icon class="!text-[16px] !w-4 !h-4">print</mat-icon> Print
        </button>
      </div>
    </app-page-header>

    <div class="px-6 py-4 mx-auto max-w-6xl space-y-6">
      <app-finance-subnav></app-finance-subnav>

      <p class="text-[11px] text-gray-500 dark:text-gray-400 -mt-2">Leave "From" blank for the financial year to date.</p>

      <div *ngIf="error()" class="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-800 text-sm text-rose-700 dark:text-rose-300">{{ error() }}</div>
      <div *ngIf="isLoading()" class="text-sm text-gray-500 py-10 text-center">Loading…</div>

      <ng-container *ngIf="statement() as st">
        <!-- KPI cards -->
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <div *ngFor="let k of kpis(st.totals)" class="bg-white/80 dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-4">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ k.label }}</p>
            <h3 class="text-xl font-black mt-1" [ngClass]="k.value >= 0 ? k.color : 'text-rose-600 dark:text-rose-400'">{{ k.value | currency:'BDT ':'symbol':'1.0-0' }}</h3>
            <p *ngIf="k.hint" class="text-[10px] text-gray-400 mt-0.5">{{ k.hint }}</p>
          </div>
        </div>

        <!-- Statement -->
        <div class="bg-white dark:bg-gray-800 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden">
          <div class="p-5 text-center border-b border-gray-100 dark:border-gray-700/60">
            <h2 class="text-base font-black uppercase tracking-wider text-gray-900 dark:text-white">Statement of Profit or Loss</h2>
            <p class="text-xs text-gray-500 mt-1">For the period {{ st.fromDate | date:'d MMM yyyy' }} to {{ st.toDate | date:'d MMM yyyy' }}</p>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
                <tr>
                  <th class="text-left px-5 py-2 w-16">Code</th>
                  <th class="text-left px-3 py-2">Particulars</th>
                  <th class="text-right px-5 py-2 w-44">Current</th>
                  <th *ngIf="st.priorTotals" class="text-right px-5 py-2 w-44">
                    {{ st.priorFromDate | date:'d MMM yy' }} – {{ st.priorToDate | date:'d MMM yy' }}
                  </th>
                </tr>
              </thead>
              <tbody>
                <ng-container *ngFor="let section of st.sections; let i = index">
                  <tr class="bg-gray-50/60 dark:bg-gray-900/20">
                    <td colspan="4" class="px-5 pt-3 pb-1 text-xs font-black uppercase tracking-widest text-gray-800 dark:text-gray-100">{{ section.name }}</td>
                  </tr>
                  <tr *ngIf="!section.lines.length">
                    <td></td><td colspan="3" class="px-3 py-1.5 text-xs text-gray-400 italic">Nothing in this period</td>
                  </tr>
                  <tr *ngFor="let line of section.lines" class="border-t border-gray-50 dark:border-gray-700/40">
                    <td class="px-5 py-1.5 font-mono text-[11px] text-gray-400">{{ line.accountCode }}</td>
                    <td class="px-3 py-1.5 text-gray-800 dark:text-gray-200">{{ line.accountName }}</td>
                    <td class="px-5 py-1.5 text-right font-mono">{{ fmt(line.amountBdt) }}</td>
                    <td *ngIf="st.priorTotals" class="px-5 py-1.5 text-right font-mono text-gray-500">{{ fmt(line.priorAmountBdt) }}</td>
                  </tr>
                  <tr class="font-semibold">
                    <td></td>
                    <td class="px-3 py-1 text-xs text-gray-600 dark:text-gray-300">Total {{ section.name.toLowerCase() }}</td>
                    <td class="px-5 py-1 text-right font-mono border-t border-gray-300 dark:border-gray-600">{{ fmt(section.totalBdt) }}</td>
                    <td *ngIf="st.priorTotals" class="px-5 py-1 text-right font-mono border-t border-gray-300 dark:border-gray-600 text-gray-500">{{ fmt(section.priorTotalBdt) }}</td>
                  </tr>

                  <!-- Running results after the sections they close -->
                  <tr *ngIf="section.key === 'cost-of-sales'" class="font-black bg-emerald-50/50 dark:bg-emerald-950/20">
                    <td></td>
                    <td class="px-3 py-2 text-xs uppercase tracking-wider">Gross Profit <span class="font-normal normal-case text-gray-500">({{ st.totals.grossMarginPercent }}% of revenue)</span></td>
                    <td class="px-5 py-2 text-right font-mono">{{ fmt(st.totals.grossProfitBdt) }}</td>
                    <td *ngIf="st.priorTotals" class="px-5 py-2 text-right font-mono text-gray-500">{{ fmt(st.priorTotals.grossProfitBdt) }}</td>
                  </tr>
                  <tr *ngIf="section.key === 'operating-expenses'" class="font-black bg-emerald-50/50 dark:bg-emerald-950/20">
                    <td></td>
                    <td class="px-3 py-2 text-xs uppercase tracking-wider">Operating Profit</td>
                    <td class="px-5 py-2 text-right font-mono">{{ fmt(st.totals.operatingProfitBdt) }}</td>
                    <td *ngIf="st.priorTotals" class="px-5 py-2 text-right font-mono text-gray-500">{{ fmt(st.priorTotals.operatingProfitBdt) }}</td>
                  </tr>
                </ng-container>
                <tr class="font-black bg-gray-50 dark:bg-gray-900/50">
                  <td></td>
                  <td class="px-3 py-3 text-xs uppercase tracking-wider">{{ st.totals.netProfitBdt >= 0 ? 'Net Profit for the period' : 'Net Loss for the period' }}</td>
                  <td class="px-5 py-3 text-right font-mono border-b-4 border-double border-gray-800 dark:border-gray-200"
                      [ngClass]="st.totals.netProfitBdt >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">{{ fmt(st.totals.netProfitBdt) }}</td>
                  <td *ngIf="st.priorTotals" class="px-5 py-3 text-right font-mono border-b-4 border-double border-gray-800 dark:border-gray-200 text-gray-600">{{ fmt(st.priorTotals.netProfitBdt) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
          <div class="px-5 py-3 border-t border-gray-100 dark:border-gray-700/60 text-[11px] text-gray-500 flex items-start gap-2">
            <mat-icon class="!w-4 !h-4 !text-[16px] shrink-0">info</mat-icon>
            <span>
              Cost of sales is the purchase cost of the animals sold or lost plus the feed, health, labour and consumables used.
              Stock and animal purchases are not costs until used or sold. A negative "Loss / (Gain) on disposal" is a gain.
              See the <a routerLink="/finance/reports/balance-sheet" class="underline">balance sheet</a> for the financial position.
            </span>
          </div>
        </div>
      </ng-container>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IncomeStatementComponent {
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly reports = inject(ReportService);
  private readonly destroyRef = inject(DestroyRef);

  readonly statement = signal<IncomeStatement | null>(null);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);

  period: Period = { fromDate: null, toDate: new Date().toISOString().split('T')[0], comparative: true };
  private readonly period$ = new BehaviorSubject<Period>(this.period);

  constructor() {
    combineLatest([this.period$, this.context.currentFarm$]).pipe(
      tap(() => { this.isLoading.set(true); this.error.set(null); }),
      switchMap(([p, farm]) => !farm ? of(null) : this.finance.getIncomeStatement(farm.id, p.fromDate, p.toDate, p.comparative).pipe(
        catchError(err => { this.error.set(parseApiError(err, 'Failed to load the profit and loss statement.')); return of(null); })
      )),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(st => { this.statement.set(st); this.isLoading.set(false); });
  }

  setPeriod(change: Partial<Period>): void {
    this.period = { ...this.period, ...change };
    this.period$.next(this.period);
  }

  kpis(t: IncomeStatementTotals) {
    return [
      { label: 'Revenue', value: t.revenueBdt, color: 'text-gray-900 dark:text-white', hint: '' },
      { label: 'Gross Profit', value: t.grossProfitBdt, color: 'text-emerald-600 dark:text-emerald-400', hint: `${t.grossMarginPercent}% margin` },
      { label: 'Operating Expenses', value: t.operatingExpensesBdt, color: 'text-amber-600 dark:text-amber-400', hint: 'incl. depreciation' },
      { label: 'Net Profit', value: t.netProfitBdt, color: 'text-emerald-600 dark:text-emerald-400', hint: `${t.netMarginPercent}% of revenue` },
    ];
  }

  /** Accounting format: negatives in brackets, zero as a dash. */
  fmt(value: number | null | undefined): string {
    if (value === null || value === undefined) return '';
    if (Math.abs(value) < 0.005) return '–';
    const text = Math.abs(value).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    return value < 0 ? `(${text})` : text;
  }

  export(format: 'pdf' | 'xlsx'): void {
    const farmId = this.context.currentFarmValue?.id ?? null;
    this.reports.export('finance.income-statement', format, {
      parameters: { farmId, fromDate: this.period.fromDate, toDate: this.period.toDate }
    }).subscribe({
      next: res => {
        if (res.body) {
          this.reports.saveBlob(res.body, this.reports.fileNameFrom(res.headers, `Farm360_Profit_and_Loss_${this.period.toDate}.${format}`));
        }
      },
      error: err => this.error.set(parseApiError(err, 'Export failed.'))
    });
  }

  print(): void {
    window.print();
  }
}
