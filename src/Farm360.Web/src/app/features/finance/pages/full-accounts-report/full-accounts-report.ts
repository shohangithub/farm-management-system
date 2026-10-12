import {
  ChangeDetectionStrategy, Component, DestroyRef, ElementRef, ViewEncapsulation, computed, inject, signal, viewChild,
} from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
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
import { ReportExportFormat } from '../../../reports/models/report.models';
import { FinanceService } from '../../services/finance.service';
import { FullAccountsReport } from '../../models/finance.model';
import { ACCOUNTS_PAPER_CSS, RANGE_PRESETS, printPaper, toIsoDate } from '../../utils/accounts-report-print';

/** Sections the user can include or leave out of the statement (and therefore the print-out). */
type SectionKey =
  | 'cash' | 'pnl' | 'costs' | 'assets' | 'credit' | 'capital' | 'investors' | 'profit' | 'shares' | 'loans' | 'trend' | 'ledger';

/**
 * Full Accounts Statement (সম্পূর্ণ হিসাব বিবরণী) — every money-related record of the farm for a
 * period on one printable statement: cash position, operating P&L, cost analysis (labour, feed,
 * health…), investment & financing, investor accounts, profit sharing, share capital, loans,
 * a trend, and the complete cash-book ledger with running balance.
 */
@Component({
  selector: 'app-full-accounts-report',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatIconModule, MatButtonModule, PageHeaderComponent, DecimalPipe, DatePipe],
  templateUrl: './full-accounts-report.html',
  styles: [ACCOUNTS_PAPER_CSS],
  encapsulation: ViewEncapsulation.None,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FullAccountsReportComponent {
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly reports = inject(ReportService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly paper = viewChild<ElementRef<HTMLElement>>('paper');

  readonly presets = [
    ...RANGE_PRESETS,
    {
      key: 'all-time', label: 'All time',
      range: () => ({ from: '2000-01-01', to: toIsoDate(new Date()) }),
    },
  ];

  readonly sectionOptions: { key: SectionKey; label: string }[] = [
    { key: 'cash', label: 'Cash position' },
    { key: 'pnl', label: 'Profit & loss' },
    { key: 'costs', label: 'Cost analysis' },
    { key: 'assets', label: 'Stock & livestock' },
    { key: 'credit', label: 'Receivables & payables' },
    { key: 'capital', label: 'Investment & financing' },
    { key: 'investors', label: 'Investor accounts' },
    { key: 'profit', label: 'Profit sharing' },
    { key: 'shares', label: 'Share capital' },
    { key: 'loans', label: 'Loans' },
    { key: 'trend', label: 'Trend' },
    { key: 'ledger', label: 'Cash book' },
  ];

  readonly todayIso = toIsoDate(new Date());
  readonly fromDate = signal(RANGE_PRESETS.find(p => p.key === 'this-month')!.range().from);
  readonly toDate = signal(this.todayIso);
  readonly activePreset = signal<string | null>('this-month');
  readonly sections = signal<Set<SectionKey>>(new Set(this.sectionOptions.map(s => s.key)));

  readonly data = signal<FullAccountsReport | null>(null);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly exporting = signal<ReportExportFormat | null>(null);
  readonly farmName = signal('');
  readonly orgName = signal('');
  readonly generatedAt = signal(new Date());

  private readonly request$ = new BehaviorSubject({ from: this.fromDate(), to: this.toDate() });

  readonly periodLabel = computed(() => {
    const d = this.data();
    if (!d) return '';
    const f = (iso: string) => new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
    return d.fromDate.substring(0, 10) === d.toDate.substring(0, 10) ? f(d.fromDate) : `${f(d.fromDate)} — ${f(d.toDate)}`;
  });

  /** Loans recorded in the loan register but never posted to the cash book. */
  readonly unpostedLoanAmount = computed(() => {
    const d = this.data();
    return d ? Math.max(0, d.loans.disbursedInPeriodBdt - d.capital.loanReceivedBdt) : 0;
  });

  readonly principalDue = computed(() =>
    (this.data()?.loans.loans ?? []).reduce((sum, l) => sum + l.outstandingPrincipalBdt, 0));

  readonly maxCostGroup = computed(() => {
    const d = this.data();
    return d ? Math.max(1, ...d.costs.groups.map(g => g.amountBdt)) : 1;
  });

  readonly investorTotals = computed(() => {
    const list = this.data()?.investors ?? [];
    const sum = (f: (i: (typeof list)[number]) => number) => list.reduce((s, i) => s + f(i), 0);
    return {
      opening: sum(i => i.openingCapitalBdt),
      contributed: sum(i => i.contributedInPeriodBdt),
      withdrawn: sum(i => i.withdrawnInPeriodBdt),
      closing: sum(i => i.closingCapitalBdt),
      entitled: sum(i => i.profitEntitledInPeriodBdt),
      paid: sum(i => i.profitPaidInPeriodBdt),
      payable: sum(i => i.profitPayableBdt),
      lifetimePaid: sum(i => i.profitPaidLifetimeBdt),
      shares: sum(i => i.shareCount),
      shareValue: sum(i => i.shareMarketValueBdt),
    };
  });

  readonly trendTotals = computed(() => {
    const list = this.data()?.trend ?? [];
    const sum = (f: (t: (typeof list)[number]) => number) => list.reduce((s, t) => s + f(t), 0);
    return {
      income: sum(t => t.operatingIncomeBdt),
      expense: sum(t => t.operatingExpenseBdt),
      net: sum(t => t.netOperatingBdt),
      capIn: sum(t => t.capitalInBdt),
      capOut: sum(t => t.capitalOutBdt),
    };
  });

  constructor() {
    combineLatest([this.request$, this.context.currentFarm$])
      .pipe(
        tap(() => {
          this.isLoading.set(true);
          this.error.set(null);
        }),
        switchMap(([req, farm]) => {
          if (!farm) return of(null);
          this.farmName.set(farm.name ?? '');
          this.orgName.set(this.context.currentOrgValue?.name ?? '');
          return this.finance.getFullAccountsReport(farm.id, req.from, req.to).pipe(
            catchError(err => {
              this.error.set(parseApiError(err, 'Failed to load the full accounts statement.'));
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => {
        this.data.set(result);
        this.generatedAt.set(new Date());
        this.isLoading.set(false);
      });
  }

  has(key: SectionKey): boolean {
    return this.sections().has(key);
  }

  toggleSection(key: SectionKey): void {
    const next = new Set(this.sections());
    if (next.has(key)) next.delete(key);
    else next.add(key);
    this.sections.set(next);
  }

  applyPreset(key: string): void {
    const preset = this.presets.find(p => p.key === key);
    if (!preset) return;
    const { from, to } = preset.range();
    this.fromDate.set(from);
    this.toDate.set(to);
    this.activePreset.set(key);
    this.run();
  }

  onRangeEdited(): void {
    this.activePreset.set(null);
  }

  run(): void {
    if (this.fromDate() > this.toDate()) {
      this.error.set('The start date must not be after the end date.');
      return;
    }
    this.request$.next({ from: this.fromDate(), to: this.toDate() });
  }

  print(): void {
    const el = this.paper()?.nativeElement;
    if (el) printPaper(el, `Full Accounts Statement — ${this.periodLabel()}`);
  }

  export(format: ReportExportFormat): void {
    const farmId = this.context.currentFarmValue?.id;
    const from = this.fromDate();
    const to = this.toDate();
    this.exporting.set(format);
    this.reports
      .export('finance.full-accounts', format, { parameters: { farmId: farmId ?? null, period: `${from}..${to}` } })
      .subscribe({
        next: res => {
          if (res.body) {
            this.reports.saveBlob(res.body, this.reports.fileNameFrom(res.headers, `Full_Accounts_${from}_${to}.${format}`));
          }
          this.exporting.set(null);
        },
        error: err => {
          this.error.set(parseApiError(err, `Export to ${format.toUpperCase()} failed.`));
          this.exporting.set(null);
        },
      });
  }

  abs(value: number): number {
    return Math.abs(value);
  }
}
