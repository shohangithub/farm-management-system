import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, DestroyRef, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of, combineLatest, BehaviorSubject } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';
import { ReportService } from '../../../reports/services/report.service';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { formatMoney } from '../../../../shared/pipes/money.pipe';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { FinanceSubnavComponent } from '../../components/finance-subnav/finance-subnav.component';
import { I18nService } from '../../../../core/i18n/i18n.service';

@Component({
  selector: 'app-monthly-pnl-report',
  standalone: true,
  imports: [FinanceSubnavComponent, MatMenuModule, MoneyPipe, ErrorStateComponent,
    CommonModule,
    RouterLink,
    ReactiveFormsModule,
    MatIconModule,
    MatButtonModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    TranslatePipe
  ],
  template: `
    <app-page-header 
      [title]="'Monthly Profit & Loss' | t" 
      [description]="'Income and expenses by category for one month.' | t"
      [breadcrumbActiveNode]="'Monthly P&L' | t">
      <div actions class="flex flex-wrap items-center gap-2">
        <form [formGroup]="filterForm" class="flex flex-wrap items-center gap-2">
          <select formControlName="year" class="px-3 py-2 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl text-sm font-medium focus:ring-2 focus:ring-primary-500 outline-none">
            <option *ngFor="let y of years" [value]="y">{{ y }}</option>
          </select>
          <select formControlName="month" class="px-3 py-2 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl text-sm font-medium focus:ring-2 focus:ring-primary-500 outline-none">
            <option *ngFor="let m of months" [value]="m.value">{{ m.label | t }}</option>
          </select>
        </form>
        <button mat-stroked-button type="button" [matMenuTriggerFor]="exportMenu"
          class="!rounded-xl !min-h-[40px] !border-gray-200 dark:!border-gray-700 !text-gray-700 dark:!text-gray-200 hover:!bg-gray-50 dark:hover:!bg-gray-800 !text-xs !font-semibold">
          <mat-icon class="!w-4 !h-4 !text-[16px]">download</mat-icon>
          <span>{{ 'Download' | t }}</span>
          <mat-icon iconPositionEnd class="!w-4 !h-4 !text-[16px]">expand_more</mat-icon>
        </button>
        <mat-menu #exportMenu="matMenu" xPosition="before">
          <button mat-menu-item (click)="exportPdf()" [disabled]="isExporting() || !reportData()"><mat-icon>picture_as_pdf</mat-icon><span>{{ 'Download PDF' | t }}</span></button>
          <button mat-menu-item (click)="exportExcel()" [disabled]="!reportData()"><mat-icon>grid_on</mat-icon><span>{{ 'Download Excel' | t }}</span></button>
          <a mat-menu-item routerLink="/reports/finance.monthly-pnl"><mat-icon>table_view</mat-icon><span>{{ 'Open as report' | t }}</span></a>
        </mat-menu>
      </div>
    </app-page-header>

    <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

    <div *ngIf="!isLoading()" class="p-6 max-w-6xl mx-auto space-y-6">
      <app-finance-subnav></app-finance-subnav>

      
      <app-error-state *ngIf="loadError() as err" [title]="loadErrorTitle" [message]="err" (retry)="retryLoad()"></app-error-state>

      <app-empty-state
        *ngIf="!reportData() && !loadError()"
        icon="event_note"
        [title]="'No Data Available' | t"
        [description]="'There are no financial records for the selected month.' | t">
      </app-empty-state>

      <!-- Main Report View -->
      <div *ngIf="reportData() as report" #reportSheet id="monthlyPnlReport" class="space-y-6">
        
        <!-- KPIs -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div class="bg-white dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 flex items-center justify-between">
            <div>
              <p class="text-sm font-bold uppercase tracking-wider text-gray-500">{{ 'Total Income' | t }}</p>
              <h2 class="text-2xl font-bold text-gray-900 dark:text-white mt-1">
                {{ report.totalIncomeBdt | money }}
              </h2>
            </div>
            <div class="w-12 h-12 rounded-full bg-emerald-100 dark:bg-emerald-900/30 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
              <mat-icon>arrow_downward</mat-icon>
            </div>
          </div>

          <div class="bg-white dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 flex items-center justify-between">
            <div>
              <p class="text-sm font-bold uppercase tracking-wider text-gray-500">{{ 'Total Expense' | t }}</p>
              <h2 class="text-2xl font-bold text-gray-900 dark:text-white mt-1">
                {{ report.totalExpenseBdt | money }}
              </h2>
            </div>
            <div class="w-12 h-12 rounded-full bg-rose-100 dark:bg-rose-900/30 text-rose-600 dark:text-rose-400 flex items-center justify-center">
              <mat-icon>arrow_upward</mat-icon>
            </div>
          </div>

          <div class="bg-white dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 flex items-center justify-between relative overflow-hidden"
               [ngClass]="report.netProfitBdt >= 0 ? 'bg-gradient-to-br from-success-50 to-teal-50 dark:from-success-900/10 dark:to-teal-900/10' : 'bg-gradient-to-br from-rose-50 to-red-50 dark:from-rose-900/10 dark:to-red-900/10'">
            <div class="relative z-10">
              <p class="text-sm font-bold uppercase tracking-wider text-gray-500">{{ 'Net Profit' | t }}</p>
              <h2 class="text-2xl font-bold mt-1" [ngClass]="report.netProfitBdt >= 0 ? 'text-success-700 dark:text-success-400' : 'text-red-700 dark:text-red-400'">
                {{ Math.abs(report.netProfitBdt) | money }}
                <span *ngIf="report.netProfitBdt < 0" class="text-sm font-normal">{{ 'Loss' | t }}</span>
              </h2>
            </div>
            <div class="absolute right-[-10px] bottom-[-10px] opacity-20">
              <mat-icon class="!w-[80px] !h-[80px] !text-[80px]" [ngClass]="report.netProfitBdt >= 0 ? 'text-success-500' : 'text-rose-500'">
                {{ report.netProfitBdt >= 0 ? 'trending_up' : 'trending_down' }}
              </mat-icon>
            </div>
          </div>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          
          <!-- Income Breakdown -->
          <div class="bg-white dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6">
            <div class="flex items-center gap-2 mb-6 pb-4 border-b border-gray-100 dark:border-gray-700">
              <div class="w-8 h-8 rounded-full bg-emerald-100 dark:bg-emerald-900/30 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                <mat-icon class="!w-4 !h-4 !text-[16px]">add</mat-icon>
              </div>
              <h3 class="text-lg font-bold text-gray-900 dark:text-white">{{ 'Income by Category' | t }}</h3>
            </div>
            
            <div class="space-y-4">
              <div *ngIf="getCategoryKeys(report.incomeByCategory).length === 0" class="text-sm text-gray-500 italic">{{ 'No income recorded.' | t }}</div>
              <div *ngFor="let key of getCategoryKeys(report.incomeByCategory)" class="flex items-center justify-between">
                <span class="text-sm font-medium text-gray-600 dark:text-gray-300">{{ formatCategory(key) }}</span>
                <span class="text-sm font-bold text-gray-900 dark:text-white">{{ report.incomeByCategory[key] | money }}</span>
              </div>
            </div>
          </div>

          <!-- Expense Breakdown -->
          <div class="bg-white dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6">
            <div class="flex items-center gap-2 mb-6 pb-4 border-b border-gray-100 dark:border-gray-700">
              <div class="w-8 h-8 rounded-full bg-rose-100 dark:bg-rose-900/30 text-rose-600 dark:text-rose-400 flex items-center justify-center">
                <mat-icon class="!w-4 !h-4 !text-[16px]">remove</mat-icon>
              </div>
              <h3 class="text-lg font-bold text-gray-900 dark:text-white">{{ 'Expense by Category' | t }}</h3>
            </div>
            
            <div class="space-y-4">
              <div *ngIf="getCategoryKeys(report.expenseByCategory).length === 0" class="text-sm text-gray-500 italic">{{ 'No expenses recorded.' | t }}</div>
              <div *ngFor="let key of getCategoryKeys(report.expenseByCategory)" class="flex items-center justify-between">
                <span class="text-sm font-medium text-gray-600 dark:text-gray-300">{{ formatCategory(key) }}</span>
                <span class="text-sm font-bold text-gray-900 dark:text-white">{{ report.expenseByCategory[key] | money }}</span>
              </div>
            </div>
          </div>

        </div>
      </div>

    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MonthlyPnlReportComponent implements OnInit {
  private readonly financeService = inject(FinanceService);
  private readonly workingContextService = inject(WorkingContextService);
  private readonly pdfExportService = inject(PdfExportService);
  private readonly reportService = inject(ReportService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(I18nService);
  readonly loadErrorTitle = this.i18n.t("Couldn't load the report");

  @ViewChild('reportSheet') reportSheet?: ElementRef<HTMLElement>;
  readonly isExporting = signal(false);

  Math = Math;

  readonly currentYear = new Date().getFullYear();
  readonly years = [this.currentYear - 2, this.currentYear - 1, this.currentYear, this.currentYear + 1];
  
  readonly months = [
    { value: 1, label: 'January' },
    { value: 2, label: 'February' },
    { value: 3, label: 'March' },
    { value: 4, label: 'April' },
    { value: 5, label: 'May' },
    { value: 6, label: 'June' },
    { value: 7, label: 'July' },
    { value: 8, label: 'August' },
    { value: 9, label: 'September' },
    { value: 10, label: 'October' },
    { value: 11, label: 'November' },
    { value: 12, label: 'December' }
  ];

  filterForm!: FormGroup;
  private filterChanges$ = new BehaviorSubject<{ year: number, month: number }>({
    year: this.currentYear,
    month: new Date().getMonth() + 1
  });

  private readonly reportData$ = combineLatest([
    this.filterChanges$,
    this.workingContextService.currentFarm$
  ]).pipe(
    switchMap(([filters, farm]) => {
      this.loadError.set(null);
      if (!farm) {
        return of(null);
      }
      return this.financeService.getMonthlyPnL(farm.id, filters.year, filters.month).pipe(
        catchError(err => {
          this.loadError.set(parseApiError(err, this.i18n.t('Could not load the monthly P&L report.')));
          return of(null);
        })
      );
    })
  );

  readonly loadError = signal<string | null>(null);

  retryLoad(): void {
    this.filterChanges$.next(this.filterChanges$.value);
  }

  readonly reportData = toSignal(this.reportData$);
  readonly isLoading = computed(() => this.reportData() === undefined);

  ngOnInit(): void {
    this.filterForm = this.fb.group({
      year: [this.filterChanges$.value.year],
      month: [this.filterChanges$.value.month]
    });

    this.filterForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(val => {
        this.filterChanges$.next({
          year: Number(val.year),
          month: Number(val.month)
        });
      });
  }

  getCategoryKeys(dict: { [key: string]: number }): string[] {
    if (!dict) return [];
    return Object.keys(dict).sort();
  }

  formatCategory(key: string): string {
    // Convert CamelCase to Space Case
    return this.i18n.t(key.replace(/([A-Z])/g, ' $1').trim());
  }

  async exportPdf(): Promise<void> {
    const farmId = this.workingContextService.currentFarmValue?.id;
    const year = this.filterForm.value.year || this.currentYear;
    const monthVal = Number(this.filterForm.value.month) || (new Date().getMonth() + 1);
    const monthObj = this.months.find(m => m.value === monthVal);
    const monthLabel = monthObj ? monthObj.label : `Month_${monthVal}`;

    this.isExporting.set(true);

    this.reportService.export('finance.monthly-pnl', 'pdf', {
      parameters: {
        farmId: farmId || null,
        year: year.toString(),
        month: monthVal.toString()
      }
    }).subscribe({
      next: res => {
        if (res.body) {
          const fn = this.reportService.fileNameFrom(res.headers, `Farm360_Monthly_PnL_${year}_${monthLabel}.pdf`);
          this.reportService.saveBlob(res.body, fn);
        }
        this.isExporting.set(false);
      },
      error: async err => {
        console.warn('Server-side PDF export fallback to client-side renderer:', err);
        const el = this.reportSheet?.nativeElement;
        if (el) {
          const report = this.reportData();
          const farm = this.workingContextService.currentFarmValue;
          const org = this.workingContextService.currentOrgValue;
          try {
            await this.pdfExportService.exportElement(el, {
              filename: `Farm360_Monthly_PnL_${year}_${monthLabel}`,
              orientation: 'landscape',
              header: {
                title: this.i18n.t('Monthly Profit & Loss Statement'),
                subtitle: this.i18n.t('Income & Expense Breakdown for {month} {year}', { month: this.i18n.t(monthLabel), year }),
                farmName: farm?.name || 'Primary Farm',
                orgName: org?.name || 'Farm360 Enterprise',
                dateRange: this.i18n.t('Period: {month} {year}', { month: this.i18n.t(monthLabel), year }),
                currency: 'BDT (৳)',
                metaFields: [
                  { label: this.i18n.t('Total Income'), value: `${formatMoney((report?.totalIncomeBdt || 0))}` },
                  { label: this.i18n.t('Total Expense'), value: `${formatMoney((report?.totalExpenseBdt || 0))}` },
                  { label: this.i18n.t('Net Profit'), value: `${formatMoney((report?.netProfitBdt || 0))}` }
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
    const year = this.filterForm.value.year || this.currentYear;
    const monthVal = Number(this.filterForm.value.month) || (new Date().getMonth() + 1);
    const monthObj = this.months.find(m => m.value === monthVal);
    const monthLabel = monthObj ? monthObj.label : `Month_${monthVal}`;

    this.reportService.export('finance.monthly-pnl', 'xlsx', {
      parameters: {
        farmId: farmId || null,
        year: year.toString(),
        month: monthVal.toString()
      }
    }).subscribe({
      next: res => {
        if (res.body) {
          const fn = this.reportService.fileNameFrom(res.headers, `Farm360_Monthly_PnL_${year}_${monthLabel}.xlsx`);
          this.reportService.saveBlob(res.body, fn);
        }
      },
      error: err => console.error('Failed to export Excel:', err)
    });
  }
}
