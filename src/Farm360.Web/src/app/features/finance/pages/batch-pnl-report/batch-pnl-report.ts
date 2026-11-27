import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule, PercentPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of, combineLatest, map, BehaviorSubject } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';
import { ReportService } from '../../../reports/services/report.service';
import { BatchPnLReport } from '../../models/finance.model';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { formatMoney } from '../../../../shared/pipes/money.pipe';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

/** Batch P&L with the unrealised-value fields the API now returns (ratios are 0–1 fractions). */
interface BatchPnLReportView extends BatchPnLReport {
  onHandAnimals?: number;
  unrealisedValueBdt?: number;
  estimatedTotalProfitBdt?: number;
  estimatedRoiPercent?: number;
}

@Component({
  selector: 'app-batch-pnl-report',
  standalone: true,
  imports: [MatMenuModule, MoneyPipe, ErrorStateComponent,
    CommonModule,
    RouterLink,
    MatIconModule,
    MatButtonModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    PercentPipe,
    TranslatePipe
  ],
  template: `
    <app-page-header 
      [title]="'Batch Profit & Loss' | t" 
      [description]="'Financial performance for batch {batch}' | t: { batch: (batchId() || '') }"
      [breadcrumbActiveNode]="'Batch P&L' | t">
      <div actions class="flex flex-wrap items-center gap-2">
        <button mat-stroked-button type="button" [matMenuTriggerFor]="exportMenu"
          class="!rounded-xl !min-h-[40px] !border-gray-200 dark:!border-gray-700 !text-gray-700 dark:!text-gray-200 hover:!bg-gray-50 dark:hover:!bg-gray-800 !text-xs !font-semibold">
          <mat-icon class="!w-4 !h-4 !text-[16px]">download</mat-icon>
          <span>{{ 'Download' | t }}</span>
          <mat-icon iconPositionEnd class="!w-4 !h-4 !text-[16px]">expand_more</mat-icon>
        </button>
        <mat-menu #exportMenu="matMenu" xPosition="before">
          <button mat-menu-item (click)="exportPdf()" [disabled]="isExporting() || !reportData()"><mat-icon>picture_as_pdf</mat-icon><span>{{ 'Download PDF' | t }}</span></button>
          <button mat-menu-item (click)="exportExcel()" [disabled]="!reportData()"><mat-icon>grid_on</mat-icon><span>{{ 'Download Excel' | t }}</span></button>
          <a mat-menu-item [routerLink]="['/reports/finance.batch-pnl']" [queryParams]="{ batchId: batchId() }"><mat-icon>table_view</mat-icon><span>{{ 'Open as report' | t }}</span></a>
        </mat-menu>
      </div>
    </app-page-header>

    <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

    <div *ngIf="!isLoading()" class="p-6 max-w-5xl mx-auto space-y-6">
      
      <app-error-state *ngIf="loadError() as err" [title]="loadErrorTitle" [message]="err" (retry)="retryLoad()"></app-error-state>

      <app-empty-state
        *ngIf="!reportData() && !loadError()"
        icon="trending_flat"
        [title]="'No P&L Data' | t"
        [description]="'No financial transactions found for this batch.' | t">
      </app-empty-state>

      <!-- Main Report View -->
      <div *ngIf="reportData() as report" #reportSheet id="batchPnlReport" class="bg-white dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative p-6 md:p-8">
        <div class="absolute -right-4 -bottom-4 text-[150px] text-gray-500/5 rotate-[-10deg] pointer-events-none">
          <mat-icon inline="true">leaderboard</mat-icon>
        </div>

        <div class="flex flex-col md:flex-row gap-6 mb-8 border-b border-gray-100 dark:border-gray-700/50 pb-8">
          <!-- Total Income -->
          <div class="flex-1">
            <p class="text-sm font-bold uppercase tracking-wider text-gray-500">{{ 'Realised Income' | t }}</p>
            <h2 class="text-3xl font-bold text-gray-900 dark:text-white mt-1">
              {{ report.totalIncomeBdt | money }}
            </h2>
          </div>

          <!-- Total Cost -->
          <div class="flex-1 border-l-0 md:border-l border-gray-100 dark:border-gray-700/50 md:pl-6">
            <p class="text-sm font-bold uppercase tracking-wider text-gray-500">{{ 'Total Cost' | t }}</p>
            <h2 class="text-3xl font-bold text-gray-900 dark:text-white mt-1">
              {{ report.totalCostBdt | money }}
            </h2>
          </div>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          
          <!-- Gross Profit -->
          <div class="bg-gray-50/50 dark:bg-gray-800/50 rounded-xl p-6 border border-gray-100 dark:border-gray-700 flex items-center justify-between">
            <div>
              <p class="text-xs font-bold uppercase tracking-wider text-gray-500 mb-1">{{ 'Realised Profit / Loss' | t }}</p>
              <h3 class="text-2xl font-bold" [ngClass]="report.grossProfitBdt >= 0 ? 'text-success-600 dark:text-success-400' : 'text-red-600 dark:text-red-400'">
                {{ Math.abs(report.grossProfitBdt) | money }}
                <span *ngIf="report.grossProfitBdt < 0" class="text-sm">{{ 'Loss' | t }}</span>
              </h3>
            </div>
            <div class="w-12 h-12 rounded-full flex items-center justify-center" [ngClass]="report.grossProfitBdt >= 0 ? 'bg-success-100 dark:bg-success-900/40 text-success-600 dark:text-success-400' : 'bg-red-100 dark:bg-red-900/40 text-red-600 dark:text-red-400'">
              <mat-icon>{{ report.grossProfitBdt >= 0 ? 'trending_up' : 'trending_down' }}</mat-icon>
            </div>
          </div>

          <!-- ROI -->
          <div class="bg-blue-50/50 dark:bg-blue-900/20 rounded-xl p-6 border border-blue-100 dark:border-blue-800/50 flex items-center justify-between">
            <div>
              <p class="text-xs font-bold uppercase tracking-wider text-blue-600/70 dark:text-blue-400 mb-1">{{ 'Realised ROI' | t }}</p>
              <h3 class="text-2xl font-bold text-blue-900 dark:text-blue-100">
                {{ report.returnOnInvestmentPercent | percent:'1.2-2' }}
              </h3>
            </div>
            <div class="w-12 h-12 rounded-full bg-blue-100 dark:bg-blue-800/50 text-blue-600 dark:text-blue-300 flex items-center justify-center">
              <mat-icon>percent</mat-icon>
            </div>
          </div>

        </div>

        <!-- Unrealised value & estimated P&L (animals still on the farm) -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mt-6">
          <div class="bg-amber-50/50 dark:bg-amber-900/20 rounded-xl p-6 border border-amber-100 dark:border-amber-800/50">
            <p class="text-xs font-bold uppercase tracking-wider text-amber-700/80 dark:text-amber-400 mb-1">{{ 'Unrealised value ({n} on hand)' | t: { n: (report.onHandAnimals ?? 0) } }}</p>
            <h3 class="text-2xl font-bold text-amber-900 dark:text-amber-100">
              {{ (report.unrealisedValueBdt ?? 0) | money }}
            </h3>
          </div>
          <div class="bg-gray-50/50 dark:bg-gray-800/50 rounded-xl p-6 border border-gray-100 dark:border-gray-700">
            <p class="text-xs font-bold uppercase tracking-wider text-gray-500 mb-1">{{ 'Estimated Total P&L' | t }}</p>
            <h3 class="text-2xl font-bold" [ngClass]="(report.estimatedTotalProfitBdt ?? 0) >= 0 ? 'text-success-600 dark:text-success-400' : 'text-red-600 dark:text-red-400'">
              {{ Math.abs(report.estimatedTotalProfitBdt ?? 0) | money }}
              <span *ngIf="(report.estimatedTotalProfitBdt ?? 0) < 0" class="text-sm">{{ 'Loss' | t }}</span>
            </h3>
          </div>
          <div class="bg-blue-50/50 dark:bg-blue-900/20 rounded-xl p-6 border border-blue-100 dark:border-blue-800/50">
            <p class="text-xs font-bold uppercase tracking-wider text-blue-600/70 dark:text-blue-400 mb-1">{{ 'Estimated ROI' | t }}</p>
            <h3 class="text-2xl font-bold text-blue-900 dark:text-blue-100">
              {{ (report.estimatedRoiPercent ?? 0) | percent:'1.2-2' }}
            </h3>
          </div>
        </div>

        <div class="mt-6 flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-800/30 p-3 rounded-lg border border-gray-100 dark:border-gray-700/50">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">info</mat-icon>
          <span>{{ batchNote | t: { n: report.totalAnimals } }}</span>
        </div>

      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BatchPnlReportComponent implements OnInit {
  private readonly financeService = inject(FinanceService);
  private readonly workingContextService = inject(WorkingContextService);
  private readonly pdfExportService = inject(PdfExportService);
  private readonly reportService = inject(ReportService);
  private readonly route = inject(ActivatedRoute);
  private readonly i18n = inject(I18nService);
  readonly loadErrorTitle = this.i18n.t("Couldn't load the report");
  readonly batchNote = "This batch contains {n} animals. Realised P&L = realised income − total cost of every animal; the estimate adds the value of animals still on the farm at the farm's valuation rate. ROI = profit ÷ total cost.";

  @ViewChild('reportSheet') reportSheet?: ElementRef<HTMLElement>;
  readonly isExporting = signal(false);

  Math = Math;

  private routeParams$ = this.route.paramMap.pipe(
    map(params => params.get('batchId'))
  );

  readonly batchId = toSignal(this.routeParams$);

  readonly loadError = signal<string | null>(null);
  private readonly retry$ = new BehaviorSubject<void>(undefined);

  private readonly reportData$ = combineLatest([
    this.routeParams$,
    this.workingContextService.currentFarm$,
    this.retry$
  ]).pipe(
    switchMap(([batchId, farm]) => {
      this.loadError.set(null);
      if (!batchId || !farm) {
        return of(null);
      }
      return this.financeService.getBatchPnL(farm.id, batchId).pipe(
        map(r => r as BatchPnLReportView | null),
        catchError(err => {
          this.loadError.set(parseApiError(err, this.i18n.t('Could not load the batch P&L report.')));
          return of(null as BatchPnLReportView | null);
        }));
    })
  );

  retryLoad(): void {
    this.retry$.next();
  }

  readonly reportData = toSignal(this.reportData$);
  readonly isLoading = computed(() => this.reportData() === undefined);

  ngOnInit(): void {}

  async exportPdf(): Promise<void> {
    const farmId = this.workingContextService.currentFarmValue?.id;
    const batch = this.batchId() || '';

    this.isExporting.set(true);

    this.reportService.export('finance.batch-pnl', 'pdf', {
      parameters: {
        farmId: farmId || null,
        batchId: batch
      }
    }).subscribe({
      next: res => {
        if (res.body) {
          const fn = this.reportService.fileNameFrom(res.headers, `Farm360_Batch_PnL_${batch}.pdf`);
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
              filename: `Farm360_Batch_PnL_${batch}`,
              orientation: 'landscape',
              header: {
                title: this.i18n.t('Batch Profit & Loss Statement'),
                subtitle: this.i18n.t('Batch Code: {batch} Performance Report', { batch }),
                farmName: farm?.name || 'Primary Farm',
                orgName: org?.name || 'Farm360 Enterprise',
                currency: 'BDT (৳)',
                metaFields: [
                  { label: this.i18n.t('Total Animals'), value: `${report?.totalAnimals || 0}` },
                  { label: this.i18n.t('Realised P&L'), value: `${formatMoney((report?.grossProfitBdt || 0))}` },
                  { label: this.i18n.t('Realised ROI'), value: `${((report?.returnOnInvestmentPercent || 0) * 100).toFixed(2)}%` },
                  { label: this.i18n.t('Estimated P&L'), value: `${formatMoney((report?.estimatedTotalProfitBdt || 0))}` },
                  { label: this.i18n.t('Estimated ROI'), value: `${((report?.estimatedRoiPercent || 0) * 100).toFixed(2)}%` }
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
    const batch = this.batchId() || '';

    this.reportService.export('finance.batch-pnl', 'xlsx', {
      parameters: {
        farmId: farmId || null,
        batchId: batch
      }
    }).subscribe({
      next: res => {
        if (res.body) {
          const fn = this.reportService.fileNameFrom(res.headers, `Farm360_Batch_PnL_${batch}.xlsx`);
          this.reportService.saveBlob(res.body, fn);
        }
      },
      error: err => console.error('Failed to export Excel:', err)
    });
  }
}
