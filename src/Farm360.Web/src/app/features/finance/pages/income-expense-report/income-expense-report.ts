import {
  ChangeDetectionStrategy, Component, DestroyRef, ElementRef, ViewEncapsulation, computed, inject, signal, viewChild,
} from '@angular/core';
import { CommonModule, DecimalPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, catchError, combineLatest, of, switchMap, tap } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { ReportService } from '../../../reports/services/report.service';
import { ReportExportFormat } from '../../../reports/models/report.models';
import { FinanceService } from '../../services/finance.service';
import { AccountHeadSummary, IncomeExpenseReport } from '../../models/finance.model';
import { ACCOUNTS_PAPER_CSS, RANGE_PRESETS, addDays, printPaper, toIsoDate } from '../../utils/accounts-report-print';

type ReportMode = 'daily' | 'range';

/** One row of the two-sided head summary; the opening/closing rows are synthetic balance lines. */
interface SummaryCell {
  label: string;
  labelBn: string;
  code: string;
  count: number;
  amount: number;
  isBalance: boolean;
  group?: string;
}

/**
 * Income & Expense statement (আয়-ব্যয় বিবরণী) — daily or for any date range.
 *
 * Laid out as a classic two-sided cash book: opening balance + income on the left, expenses +
 * closing balance on the right, so both sides always total the same figure. Investor capital and
 * loans are listed (they move cash) but flagged, and the operating result excludes them.
 */
@Component({
  selector: 'app-income-expense-report',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink, MatIconModule, MatButtonModule, MatTooltipModule,
    PageHeaderComponent, DecimalPipe, DatePipe,
  ],
  templateUrl: './income-expense-report.html',
  styles: [ACCOUNTS_PAPER_CSS],
  encapsulation: ViewEncapsulation.None,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IncomeExpenseReportComponent {
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly reports = inject(ReportService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly paper = viewChild<ElementRef<HTMLElement>>('paper');

  readonly presets = RANGE_PRESETS.filter(p => p.key !== 'today' && p.key !== 'yesterday');

  // ── Filter state ──
  readonly mode = signal<ReportMode>('daily');
  readonly reportDate = signal(toIsoDate(new Date()));
  readonly fromDate = signal(toIsoDate(addDays(new Date(), -6)));
  readonly toDate = signal(toIsoDate(new Date()));
  readonly showDetails = signal(true);
  readonly showDayWise = signal(true);
  readonly activePreset = signal<string | null>('last-7');

  // ── Result state ──
  readonly data = signal<IncomeExpenseReport | null>(null);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly exporting = signal<ReportExportFormat | null>(null);
  readonly farmName = signal('');
  readonly orgName = signal('');
  readonly generatedAt = signal(new Date());

  private readonly request$ = new BehaviorSubject<{ from: string; to: string }>(this.currentRange());

  readonly todayIso = toIsoDate(new Date());

  /** Left side: opening balance first, then income heads (largest first). */
  readonly incomeSide = computed<SummaryCell[]>(() => {
    const d = this.data();
    if (!d) return [];
    return [
      { label: 'Opening Balance', labelBn: 'ওপেনিং ব্যালেন্স', code: '1010', count: 0, amount: d.openingBalanceBdt, isBalance: true },
      ...d.incomeHeads.map(h => this.toCell(h)),
    ];
  });

  /** Right side: expense heads, then the closing balance that makes both sides agree. */
  readonly expenseSide = computed<SummaryCell[]>(() => {
    const d = this.data();
    if (!d) return [];
    return [
      ...d.expenseHeads.map(h => this.toCell(h)),
      { label: 'Closing Balance', labelBn: 'ক্লোজিং ব্যালেন্স', code: '1010', count: 0, amount: d.closingBalanceBdt, isBalance: true },
    ];
  });

  readonly summaryRows = computed(() => {
    const rows = Math.max(this.incomeSide().length, this.expenseSide().length);
    return Array.from({ length: rows }, (_, i) => i);
  });

  readonly sideTotal = computed(() => {
    const d = this.data();
    return d ? d.openingBalanceBdt + d.totalIncomeBdt : 0;
  });

  readonly hasCapitalMoney = computed(() => {
    const d = this.data();
    if (!d) return false;
    return [...d.incomeHeads, ...d.expenseHeads].some(h => h.group !== 'Operating');
  });

  readonly periodLabel = computed(() => {
    const d = this.data();
    if (!d) return '';
    return d.isSingleDay ? this.fmt(d.fromDate) : `${this.fmt(d.fromDate)} — ${this.fmt(d.toDate)}`;
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
          return this.finance.getIncomeExpenseReport(farm.id, req.from, req.to).pipe(
            catchError(err => {
              this.error.set(parseApiError(err, 'Failed to load the income & expense report.'));
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

  // ── Filter actions ──

  setMode(mode: ReportMode): void {
    if (this.mode() === mode) return;
    this.mode.set(mode);
    this.run();
  }

  shiftDay(days: number): void {
    const next = addDays(new Date(`${this.reportDate()}T00:00:00`), days);
    if (toIsoDate(next) > this.todayIso) return;
    this.reportDate.set(toIsoDate(next));
    this.run();
  }

  goToday(): void {
    this.reportDate.set(this.todayIso);
    this.run();
  }

  applyPreset(key: string): void {
    const preset = RANGE_PRESETS.find(p => p.key === key);
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
    const range = this.currentRange();
    if (range.from > range.to) {
      this.error.set('The start date must not be after the end date.');
      return;
    }
    this.request$.next(range);
  }

  /** Opens a single-day report for a row of the day-wise table. */
  drillToDay(date: string): void {
    this.reportDate.set(date.substring(0, 10));
    this.mode.set('daily');
    this.run();
  }

  // ── Output ──

  print(): void {
    const el = this.paper()?.nativeElement;
    if (el) printPaper(el, `Income & Expense Statement — ${this.periodLabel()}`);
  }

  export(format: ReportExportFormat): void {
    const farmId = this.context.currentFarmValue?.id;
    const { from, to } = this.currentRange();
    this.exporting.set(format);
    this.reports
      .export('finance.income-expense', format, { parameters: { farmId: farmId ?? null, period: `${from}..${to}` } })
      .subscribe({
        next: res => {
          if (res.body) {
            const fallback = `Income_Expense_${from}_${to}.${format}`;
            this.reports.saveBlob(res.body, this.reports.fileNameFrom(res.headers, fallback));
          }
          this.exporting.set(null);
        },
        error: err => {
          this.error.set(parseApiError(err, `Export to ${format.toUpperCase()} failed.`));
          this.exporting.set(null);
        },
      });
  }

  // ── Helpers ──

  private currentRange(): { from: string; to: string } {
    return this.mode() === 'daily'
      ? { from: this.reportDate(), to: this.reportDate() }
      : { from: this.fromDate(), to: this.toDate() };
  }

  private toCell(h: AccountHeadSummary): SummaryCell {
    return { label: h.name, labelBn: h.nameBn, code: h.code, count: h.count, amount: h.totalBdt, isBalance: false, group: h.group };
  }

  private fmt(iso: string): string {
    return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }
}
