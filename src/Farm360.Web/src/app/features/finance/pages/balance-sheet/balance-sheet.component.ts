import { Component, ChangeDetectionStrategy, inject, signal, computed, DestroyRef, ViewChild, ElementRef } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of, combineLatest, BehaviorSubject } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';
import { ReportService } from '../../../reports/services/report.service';
import { BalanceSheet, BalanceSheetLine } from '../../models/finance.model';
import { FinanceSubnavComponent } from '../../components/finance-subnav/finance-subnav.component';

interface StatementGroup { title: string | null; lines: BalanceSheetLine[]; total: number; priorTotal: number; }
interface StatementBlock { title: string; totalLabel: string; total: number; priorTotal: number; color: string; groups: StatementGroup[]; }

@Component({
  selector: 'app-balance-sheet',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    ReactiveFormsModule,
    MatIconModule,
    MatButtonModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    FinanceSubnavComponent,
    CurrencyPipe,
    DatePipe
  ],
  template: `
    <app-page-header 
      title="Balance Sheet" 
      description="Statement of financial position: assets, liabilities and equity, compared with the previous financial year end."
      breadcrumbActiveNode="Balance Sheet">
      <div actions class="flex items-center gap-3">
        <a mat-stroked-button routerLink="/finance"
          class="rounded-xl border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-800 text-gray-700 dark:text-gray-200 flex items-center gap-2 px-3 py-1.5 text-xs">
          <mat-icon class="text-sm">arrow_back</mat-icon>
          <span>Overview</span>
        </a>
        <form [formGroup]="filterForm" class="flex items-center gap-2">
          <div class="flex items-center gap-1.5 px-3 py-1.5 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl text-xs font-medium">
            <span class="text-gray-500 dark:text-gray-400">As of:</span>
            <input 
              type="date" 
              formControlName="asOfDate" 
              class="bg-transparent text-gray-800 dark:text-gray-200 text-xs font-semibold outline-none cursor-pointer" />
          </div>
          <label class="flex items-center gap-1.5 px-3 py-1.5 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl text-xs font-medium cursor-pointer">
            <input type="checkbox" formControlName="comparative" class="accent-emerald-600" />
            <span class="text-gray-600 dark:text-gray-300">Compare previous year end</span>
          </label>
        </form>
        <a mat-stroked-button routerLink="/reports/finance.balance-sheet"
          matTooltip="Open in Enterprise SAP Report Viewer with multi-page table banding"
          class="rounded-xl border-emerald-300 dark:border-emerald-700 bg-emerald-50/50 dark:bg-emerald-950/20 hover:bg-emerald-100 dark:hover:bg-emerald-900/40 text-emerald-800 dark:text-emerald-300 flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold">
          <mat-icon class="!w-4 !h-4 !text-[16px] text-emerald-600 dark:text-emerald-400">table_view</mat-icon>
          <span>SAP Report</span>
        </a>
        <button mat-flat-button (click)="exportPdf()" [disabled]="isExporting() || !bsData()"
          class="rounded-xl bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-700 hover:to-teal-700 text-white flex items-center gap-2 px-3.5 py-1.5 text-xs font-semibold shadow-sm shadow-emerald-600/20 disabled:opacity-50">
          <mat-icon class="text-sm">{{ isExporting() ? 'hourglass_empty' : 'picture_as_pdf' }}</mat-icon>
          <span>{{ isExporting() ? 'Exporting...' : 'Export PDF' }}</span>
        </button>
        <button mat-stroked-button (click)="exportExcel()" [disabled]="!bsData()"
          class="rounded-xl border-emerald-200 dark:border-emerald-800 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-50 dark:hover:bg-emerald-950/30 flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold">
          <mat-icon class="!w-4 !h-4 !text-[16px]">file_download</mat-icon>
          <span>Excel</span>
        </button>
        <button mat-stroked-button (click)="printReport()"
          class="rounded-xl border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-800 text-gray-700 dark:text-gray-200 flex items-center gap-2 px-3 py-1.5 text-xs">
          <mat-icon class="text-sm">print</mat-icon>
          <span>Print</span>
        </button>
      </div>
    </app-page-header>

    <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

    <div *ngIf="!isLoading()" class="px-6 py-4 mx-auto max-w-7xl space-y-6">

      <app-finance-subnav></app-finance-subnav>

      <app-empty-state
        *ngIf="!bsData()"
        icon="account_balance"
        title="No Financial Records Found"
        description="There is no balance sheet data available for this farm as of the selected date.">
      </app-empty-state>

      <div *ngIf="bsData() as sheet" #reportSheet id="balanceSheetReport" class="space-y-6">

        <!-- Setup / difference banner -->
        <div *ngIf="sheet.message"
             class="p-4 rounded-2xl border flex items-start gap-3 print:hidden"
             [ngClass]="sheet.isBalanced ? 'bg-amber-50/70 dark:bg-amber-950/30 border-amber-200 dark:border-amber-800/60 text-amber-800 dark:text-amber-200' : 'bg-rose-50/70 dark:bg-rose-950/30 border-rose-200 dark:border-rose-800/60 text-rose-800 dark:text-rose-200'">
          <mat-icon class="shrink-0">{{ sheet.isBalanced ? 'info' : 'error_outline' }}</mat-icon>
          <div class="text-xs leading-relaxed flex-1">{{ sheet.message }}</div>
          <a routerLink="/finance/accounts-setup" class="text-xs font-bold underline whitespace-nowrap">Accounts setup</a>
        </div>

        <!-- KPI cards -->
        <div class="grid grid-cols-2 lg:grid-cols-5 gap-4">
          <div class="bg-white/80 dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-4">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500">Total Assets</p>
            <h3 class="text-xl font-black text-gray-900 dark:text-white mt-1">{{ sheet.totalAssetsBdt | currency:'BDT ':'symbol':'1.0-0' }}</h3>
          </div>
          <div class="bg-white/80 dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-4">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500">Total Liabilities</p>
            <h3 class="text-xl font-black text-amber-600 dark:text-amber-400 mt-1">{{ sheet.liabilities.totalBdt | currency:'BDT ':'symbol':'1.0-0' }}</h3>
          </div>
          <div class="bg-white/80 dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-4">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500">Total Equity</p>
            <h3 class="text-xl font-black text-purple-600 dark:text-purple-400 mt-1">{{ sheet.equity.totalBdt | currency:'BDT ':'symbol':'1.0-0' }}</h3>
          </div>
          <div class="bg-white/80 dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-4">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500">Profit for the year</p>
            <h3 class="text-xl font-black mt-1" [ngClass]="(sheet.currentYearProfitBdt || 0) >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">
              {{ (sheet.currentYearProfitBdt || 0) | currency:'BDT ':'symbol':'1.0-0' }}
            </h3>
            <p class="text-[10px] text-gray-400 mt-0.5">since {{ sheet.fiscalYearStart | date:'d MMM yyyy' }}</p>
          </div>
          <div class="bg-white/80 dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-4">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500">Net Working Capital</p>
            <h3 class="text-xl font-black mt-1" [ngClass]="sheet.netWorkingCapitalBdt >= 0 ? 'text-teal-600 dark:text-teal-400' : 'text-rose-600 dark:text-rose-400'">
              {{ sheet.netWorkingCapitalBdt | currency:'BDT ':'symbol':'1.0-0' }}
            </h3>
            <p class="text-[10px] text-gray-400 mt-0.5">Current assets − current liabilities</p>
          </div>
        </div>

        <!-- Statement of financial position (auditor layout) -->
        <div class="bg-white dark:bg-gray-800 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden">
          <div class="p-5 text-center border-b border-gray-100 dark:border-gray-700/60">
            <h2 class="text-base font-black uppercase tracking-wider text-gray-900 dark:text-white">Statement of Financial Position</h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-1">
              As at {{ sheet.asOfDate | date:'d MMMM yyyy' }}
              <span *ngIf="sheet.booksStartDate"> · Books start {{ sheet.booksStartDate | date:'d MMM yyyy' }}</span>
              · Financial year from {{ monthName(sheet.fiscalYearStartMonth) }}
            </p>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
                <tr>
                  <th class="text-left px-5 py-2 w-16">Code</th>
                  <th class="text-left px-3 py-2">Particulars</th>
                  <th class="text-right px-5 py-2 w-44">{{ sheet.asOfDate | date:'d MMM yyyy' }}</th>
                  <th *ngIf="sheet.priorDate" class="text-right px-5 py-2 w-44">{{ sheet.priorDate | date:'d MMM yyyy' }}</th>
                </tr>
              </thead>
              <tbody>
                <ng-container *ngFor="let block of blocks(sheet)">
                  <tr class="bg-gray-50/60 dark:bg-gray-900/20">
                    <td colspan="4" class="px-5 pt-4 pb-1 text-xs font-black uppercase tracking-widest text-gray-800 dark:text-gray-100">{{ block.title }}</td>
                  </tr>
                  <ng-container *ngFor="let g of block.groups">
                    <tr *ngIf="g.title">
                      <td></td>
                      <td colspan="3" class="px-3 pt-2 pb-1 text-xs font-bold text-gray-600 dark:text-gray-300 italic">{{ g.title }}</td>
                    </tr>
                    <tr *ngFor="let line of g.lines" class="border-t border-gray-50 dark:border-gray-700/40 hover:bg-gray-50/60 dark:hover:bg-gray-700/20">
                      <td class="px-5 py-1.5 font-mono text-[11px] text-gray-400">{{ line.lineCode }}</td>
                      <td class="px-3 py-1.5">
                        <span class="text-gray-800 dark:text-gray-200" [class.pl-4]="line.isDeduction">{{ line.lineName }}</span>
                        <span *ngIf="line.notes" class="block text-[11px] text-gray-400 print:hidden">{{ line.notes }}</span>
                      </td>
                      <td class="px-5 py-1.5 text-right font-mono" [ngClass]="line.amountBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-gray-900 dark:text-white'">{{ fmt(line.amountBdt) }}</td>
                      <td *ngIf="sheet.priorDate" class="px-5 py-1.5 text-right font-mono text-gray-500">{{ fmt(line.priorAmountBdt) }}</td>
                    </tr>
                    <tr *ngIf="g.title && g.lines.length > 1" class="font-semibold">
                      <td></td>
                      <td class="px-3 py-1 text-xs text-gray-600 dark:text-gray-300">Total {{ g.title.toLowerCase() }}</td>
                      <td class="px-5 py-1 text-right font-mono border-t border-gray-300 dark:border-gray-600">{{ fmt(g.total) }}</td>
                      <td *ngIf="sheet.priorDate" class="px-5 py-1 text-right font-mono border-t border-gray-300 dark:border-gray-600 text-gray-500">{{ fmt(g.priorTotal) }}</td>
                    </tr>
                  </ng-container>
                  <tr class="font-black">
                    <td></td>
                    <td class="px-3 py-2 text-xs uppercase tracking-wider text-gray-900 dark:text-white">{{ block.totalLabel }}</td>
                    <td class="px-5 py-2 text-right font-mono border-t-2 border-gray-800 dark:border-gray-200" [ngClass]="block.color">{{ fmt(block.total) }}</td>
                    <td *ngIf="sheet.priorDate" class="px-5 py-2 text-right font-mono border-t-2 border-gray-800 dark:border-gray-200 text-gray-600">{{ fmt(block.priorTotal) }}</td>
                  </tr>
                </ng-container>

                <tr class="font-black bg-gray-50 dark:bg-gray-900/50">
                  <td></td>
                  <td class="px-3 py-3 text-xs uppercase tracking-wider text-gray-900 dark:text-white">Total Liabilities &amp; Equity</td>
                  <td class="px-5 py-3 text-right font-mono border-b-4 border-double border-gray-800 dark:border-gray-200 text-indigo-700 dark:text-indigo-400">{{ fmt(sheet.totalLiabilitiesAndEquityBdt) }}</td>
                  <td *ngIf="sheet.priorDate" class="px-5 py-3 text-right font-mono border-b-4 border-double border-gray-800 dark:border-gray-200 text-gray-600">{{ fmt(sheet.priorTotalLiabilitiesAndEquityBdt) }}</td>
                </tr>
                <tr *ngIf="sheet.differenceBdt !== 0 || (sheet.priorDifferenceBdt || 0) !== 0" class="font-black bg-rose-50 dark:bg-rose-950/30 text-rose-700 dark:text-rose-300">
                  <td></td>
                  <td class="px-3 py-2 text-xs uppercase tracking-wider">Difference (assets − liabilities &amp; equity)</td>
                  <td class="px-5 py-2 text-right font-mono">{{ fmt(sheet.differenceBdt) }}</td>
                  <td *ngIf="sheet.priorDate" class="px-5 py-2 text-right font-mono">{{ fmt(sheet.priorDifferenceBdt) }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="px-5 py-3 border-t border-gray-100 dark:border-gray-700/60 text-[11px] text-gray-500 dark:text-gray-400 flex items-start gap-2">
            <mat-icon class="!w-4 !h-4 !text-[16px] shrink-0">info</mat-icon>
            <span>
              Livestock and inventory are carried at cost until sold, used or written off. Fixed assets are shown at cost less
              depreciation from the fixed-asset register. Retained earnings brought forward are the profits of earlier financial
              years; the profit for the year runs from {{ sheet.fiscalYearStart | date:'d MMM yyyy' }}.
              <span *ngIf="sheet.isBalanced" class="font-semibold text-emerald-600 dark:text-emerald-400">Assets = Liabilities + Equity ✓</span>
            </span>
          </div>
        </div>

        <!-- Books vs registers -->
        <div *ngIf="sheet.reconciliations?.length" class="bg-white/80 dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden">
          <div class="p-4 border-b border-gray-100 dark:border-gray-700/60 flex items-center justify-between gap-3 flex-wrap">
            <div>
              <h3 class="text-sm font-black text-gray-900 dark:text-white">Proof: books vs. registers</h3>
              <p class="text-xs text-gray-500 dark:text-gray-400">Each balance must match what really exists — stock on hand, the herd, the loan register and the investor register.</p>
            </div>
            <a routerLink="/finance/accounting-health" class="text-xs font-semibold text-emerald-700 dark:text-emerald-300 flex items-center gap-1">
              <mat-icon class="!w-4 !h-4 !text-[16px]">fact_check</mat-icon> Accounting Health
            </a>
          </div>
          <table class="w-full text-sm">
            <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
              <tr><th class="text-left px-4 py-2">Account</th><th class="text-right px-4 py-2">Books</th><th class="text-right px-4 py-2">Register</th><th class="text-right px-4 py-2">Difference</th></tr>
            </thead>
            <tbody>
              <tr *ngFor="let r of sheet.reconciliations" class="border-t border-gray-100 dark:border-gray-700/60">
                <td class="px-4 py-2">
                  <span class="font-semibold text-gray-800 dark:text-gray-200">{{ r.account }}</span>
                  <span class="block text-[11px] text-gray-500">{{ r.registerSource }}</span>
                </td>
                <td class="px-4 py-2 text-right font-mono">{{ r.booksBdt | currency:'BDT ':'symbol':'1.2-2' }}</td>
                <td class="px-4 py-2 text-right font-mono">{{ r.registerBdt | currency:'BDT ':'symbol':'1.2-2' }}</td>
                <td class="px-4 py-2 text-right font-mono font-bold" [ngClass]="r.isReconciled ? 'text-emerald-600' : 'text-amber-600'">
                  {{ r.isReconciled ? '✓' : (r.differenceBdt | currency:'BDT ':'symbol':'1.2-2') }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>

      </div>

    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BalanceSheetComponent {
  private readonly financeService = inject(FinanceService);
  private readonly workingContextService = inject(WorkingContextService);
  private readonly fb = inject(FormBuilder);
  private readonly pdfExportService = inject(PdfExportService);
  private readonly reportService = inject(ReportService);
  private readonly destroyRef = inject(DestroyRef);

  @ViewChild('reportSheet') reportSheet?: ElementRef<HTMLElement>;
  readonly isExporting = signal(false);

  private readonly todayStr = new Date().toISOString().split('T')[0];

  readonly filterForm = this.fb.group({
    asOfDate: [this.todayStr],
    comparative: [true]
  });

  private readonly filterChanges$ = new BehaviorSubject<{ asOfDate: string; comparative: boolean }>({
    asOfDate: this.todayStr,
    comparative: true
  });

  constructor() {
    this.filterForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(val => {
        if (val.asOfDate) {
          this.filterChanges$.next({ asOfDate: val.asOfDate, comparative: val.comparative !== false });
        }
      });
  }

  private readonly bsData$ = combineLatest([
    this.filterChanges$,
    this.workingContextService.currentFarm$
  ]).pipe(
    switchMap(([filter, farm]) => {
      if (!farm) return of(null);
      return this.financeService.getBalanceSheet(farm.id, filter.asOfDate, null, filter.comparative).pipe(
        catchError(err => {
          console.error('Error loading balance sheet:', err);
          return of(null);
        })
      );
    })
  );

  readonly bsData = toSignal(this.bsData$);
  readonly isLoading = computed(() => this.bsData() === undefined);

  async exportPdf(): Promise<void> {
    const farmId = this.workingContextService.currentFarmValue?.id;
    const asOf = this.filterForm.value.asOfDate || this.todayStr;
    this.isExporting.set(true);

    this.reportService.export('finance.balance-sheet', 'pdf', {
      parameters: {
        farmId: farmId || null,
        asOfDate: asOf,
        comparative: String(this.filterForm.value.comparative !== false)
      }
    }).subscribe({
      next: res => {
        if (res.body) {
          const fn = this.reportService.fileNameFrom(res.headers, `Farm360_Balance_Sheet_${asOf}.pdf`);
          this.reportService.saveBlob(res.body, fn);
        }
        this.isExporting.set(false);
      },
      error: async err => {
        console.warn('Server-side PDF export fallback to client-side renderer:', err);
        const el = this.reportSheet?.nativeElement;
        if (el) {
          const sheet = this.bsData();
          const farm = this.workingContextService.currentFarmValue;
          const org = this.workingContextService.currentOrgValue;
          try {
            await this.pdfExportService.exportElement(el, {
              filename: `Farm360_Balance_Sheet_${asOf}`,
              orientation: 'landscape',
              header: {
                title: 'Balance Sheet',
                subtitle: `Statement of Financial Position (Assets = Liabilities + Equity)`,
                farmName: farm?.name || 'Primary Farm',
                orgName: org?.name || 'Farm360 Enterprise',
                dateRange: `As of ${asOf}`,
                currency: 'BDT (৳)',
                metaFields: [
                  { label: 'Total Assets', value: `৳ ${(sheet?.totalAssetsBdt || 0).toLocaleString()}` },
                  { label: 'Status', value: sheet?.isBalanced ? 'Balanced' : 'Unbalanced' }
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

    this.reportService.export('finance.balance-sheet', 'xlsx', {
      parameters: {
        farmId: farmId || null,
        asOfDate: asOf,
        comparative: String(this.filterForm.value.comparative !== false)
      }
    }).subscribe({
      next: res => {
        if (res.body) {
          const fn = this.reportService.fileNameFrom(res.headers, `Farm360_Balance_Sheet_${asOf}.xlsx`);
          this.reportService.saveBlob(res.body, fn);
        }
      },
      error: err => console.error('Failed to export Excel:', err)
    });
  }

  private readonly months = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

  monthName(month?: number): string {
    return this.months[((month || 7) - 1) % 12];
  }

  /** Accounting format: thousands separators, negatives in brackets, zero as a dash. */
  fmt(value: number | null | undefined): string {
    if (value === null || value === undefined) return '';
    if (Math.abs(value) < 0.005) return '–';
    const text = Math.abs(value).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    return value < 0 ? `(${text})` : text;
  }

  /** Sections, then sub-groups (non-current / current, capital / reserves) with their totals. */
  blocks(sheet: BalanceSheet): StatementBlock[] {
    const group = (lines: BalanceSheetLine[]): StatementGroup[] => {
      const order: string[] = [];
      const map = new Map<string, BalanceSheetLine[]>();
      for (const l of lines) {
        const key = l.group || '';
        if (!map.has(key)) { map.set(key, []); order.push(key); }
        map.get(key)!.push(l);
      }
      return order.map(key => {
        const ls = map.get(key)!;
        return {
          title: key ? this.groupTitle(key) : null,
          lines: ls,
          total: ls.reduce((sum, l) => sum + l.amountBdt, 0),
          priorTotal: ls.reduce((sum, l) => sum + (l.priorAmountBdt || 0), 0)
        };
      });
    };
    return [
      { title: 'Assets', totalLabel: 'Total Assets', total: sheet.totalAssetsBdt, priorTotal: sheet.priorTotalAssetsBdt || 0, color: 'text-emerald-700 dark:text-emerald-400', groups: group(sheet.assets.lines) },
      { title: 'Liabilities', totalLabel: 'Total Liabilities', total: sheet.liabilities.totalBdt, priorTotal: sheet.liabilities.priorTotalBdt || 0, color: 'text-amber-700 dark:text-amber-400', groups: group(sheet.liabilities.lines) },
      { title: 'Equity', totalLabel: 'Total Equity', total: sheet.equity.totalBdt, priorTotal: sheet.equity.priorTotalBdt || 0, color: 'text-purple-700 dark:text-purple-300', groups: group(sheet.equity.lines) }
    ];
  }

  private groupTitle(key: string): string {
    switch (key) {
      case 'Non-current': return 'Non-current assets';
      case 'Current': return 'Current';
      case 'Capital': return 'Capital';
      case 'Reserves': return 'Reserves & retained earnings';
      default: return key;
    }
  }

  printReport(): void {
    window.print();
  }
}
