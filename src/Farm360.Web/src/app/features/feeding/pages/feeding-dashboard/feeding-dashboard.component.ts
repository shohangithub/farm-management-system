import { FeedingSubnavComponent } from '../../components/feeding-subnav/feeding-subnav.component';
import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { FeedingService } from '../../services/feeding.service';
import { FcrAnalytics, FeedingSchedule } from '../../models/feeding.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { formatMoney } from '../../../../shared/pipes/money.pipe';
import { APP_NUMBER_LOCALE } from '../../../../shared/utils/locale';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

@Component({
  selector: 'app-feeding-dashboard',
  standalone: true,
  imports: [ErrorStateComponent, 
    FeedingSubnavComponent,
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    PageHeaderComponent,
    LoadingComponent,
    TranslatePipe
  ],
  template: `
    <app-page-header
      [title]="'Feeding Overview' | t"
      [description]="'Feed used, feed cost and feed efficiency at a glance.' | t"
      [breadcrumbActiveNode]="'Feeding Overview' | t">
      <div actions class="flex items-center gap-2">
        <a routerLink="/feeding/sheet"
          class="min-h-10 px-4 text-sm font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded-xl transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">edit_calendar</mat-icon> {{ 'Record Daily Feed' | t }}
        </a>
      </div>
    </app-page-header>

    <app-feeding-subnav></app-feeding-subnav>

    <div class="space-y-6">
      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-5">
        @for (kpi of kpis(); track kpi.label) {
          <div class="bg-white dark:bg-gray-900 p-4 sm:p-5 rounded-2xl border border-gray-200 dark:border-gray-800 shadow-xs flex items-center gap-3 sm:gap-4 min-w-0">
            <div class="hidden sm:flex w-11 h-11 shrink-0 rounded-xl bg-primary-50 dark:bg-primary-950/40 text-primary-600 dark:text-primary-400 items-center justify-center">
              <mat-icon class="!w-6 !h-6 !text-[22px]">{{ kpi.icon }}</mat-icon>
            </div>
            <div class="min-w-0">
              <div class="text-[11px] uppercase tracking-wider font-semibold text-gray-500 dark:text-gray-400 leading-tight">{{ kpi.label | t }}</div>
              <div class="text-xl sm:text-2xl font-bold text-gray-900 dark:text-white mt-0.5 truncate">
                {{ kpi.value }} <span *ngIf="kpi.unit" class="text-xs font-medium text-gray-400">{{ kpi.unit | t }}</span>
              </div>
              <div *ngIf="kpi.note" class="text-[11px] text-gray-400 dark:text-gray-500 mt-0.5 leading-snug">{{ kpi.note | t }}</div>
            </div>
          </div>
        }
      </div>

      <!-- Content Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Old schedules: only shown while some are left to convert into feeding plans -->
        <div *ngIf="!isLoading() && activeSchedules().length > 0"
          class="bg-white dark:bg-gray-900 rounded-2xl border border-gray-200 dark:border-gray-800 p-5 sm:p-6 shadow-xs relative overflow-hidden">
          <div class="flex items-center justify-between gap-3 mb-4">
            <h2 class="m-0 font-bold text-gray-900 dark:text-white text-base flex items-center gap-2">
              <mat-icon class="text-gray-400 !text-[20px] !w-[20px] !h-[20px]">schedule</mat-icon> {{ 'Old Feeding Schedules' | t }}
            </h2>
            <a routerLink="/feeding/board" class="text-xs font-semibold text-primary-600 dark:text-primary-400 hover:underline whitespace-nowrap">{{ 'Convert in Feeding Plans' | t }}</a>
          </div>

          <div class="space-y-3">
            @for (schedule of activeSchedules(); track schedule.id) {
              <div class="p-3.5 rounded-xl bg-gray-50 dark:bg-gray-800/50 border border-gray-100 dark:border-gray-800 flex items-center justify-between gap-3">
                <div class="min-w-0">
                  <div class="font-semibold text-gray-900 dark:text-white text-sm truncate">{{ schedule.title }}</div>
                  <div class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                    {{ 'Formula' | t }}: <strong class="text-gray-700 dark:text-gray-300">{{ schedule.formulaTitle }}</strong> • {{ '{qty} kg/head' | t: { qty: schedule.targetQuantityKgPerHead } }}
                  </div>
                </div>
                <span class="shrink-0 px-2.5 py-1 rounded-full text-[11px] font-semibold bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-300">
                  {{ schedule.frequencyName | t }}
                </span>
              </div>
            }
          </div>
        </div>

      </div>

      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>
      <app-error-state *ngIf="!isLoading() && loadError()" [compact]="true" [message]="loadError()!" (retry)="retryLoad()"></app-error-state>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeedingDashboardComponent {
  private readonly feedingService = inject(FeedingService);
  private readonly contextService = inject(WorkingContextService);
  private readonly i18n = inject(I18nService);

  readonly isLoading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly fcrAnalytics = signal<FcrAnalytics | null>(null);
  readonly activeSchedules = signal<FeedingSchedule[]>([]);

  readonly activeFarmId = signal<string | null>(null);

  /** The four headline numbers. All calm white cards; the label says what each one means. */
  readonly kpis = computed(() => {
    const a = this.fcrAnalytics();
    const kg = (n: number | null | undefined) => (n ?? 0).toLocaleString(APP_NUMBER_LOCALE, { maximumFractionDigits: 1 });
    return [
      { label: 'Total Consumed', icon: 'scale', value: kg(a?.totalFeedConsumedKg), unit: 'kg', note: '' },
      { label: 'Feed efficiency (FCR)', icon: 'analytics', value: String(a?.fcrValue || 0), unit: '', note: 'kg feed per kg gain · lower is better' },
      { label: 'Feed Expenditure', icon: 'payments', value: formatMoney(a?.totalFeedCostBdt || 0), unit: '', note: '' },
      { label: 'Cost / kg Gain', icon: 'trending_up', value: formatMoney(a?.costPerKgGainBdt || 0, 1), unit: '', note: '' },
    ];
  });

  constructor() {
    this.contextService.currentFarm$.pipe(
      takeUntilDestroyed()
    ).subscribe(farm => {
      const farmId = farm?.id || null;
      this.activeFarmId.set(farmId);
      if (farmId) {
        this.loadDashboardData(farmId);
      } else {
        this.fcrAnalytics.set(null);
        this.activeSchedules.set([]);
        this.isLoading.set(false);
      }
    });
  }

  loadDashboardData(farmId: string): void {
    this.isLoading.set(true);
    this.loadError.set(null);

    this.feedingService.getFcrAnalytics(farmId).subscribe({
      next: (res) => this.fcrAnalytics.set(res),
      error: () => {}
    });

    this.feedingService.getSchedules(farmId).subscribe({
      next: (res) => {
        this.activeSchedules.set(res.slice(0, 5));
        this.isLoading.set(false);
      },
      error: (err) => {
        this.loadError.set(parseApiError(err, this.i18n.t('Could not load feeding data.')));
        this.isLoading.set(false);
      }
    });
  }

  retryLoad(): void {
    const farmId = this.activeFarmId();
    if (farmId) this.loadDashboardData(farmId);
  }
}
