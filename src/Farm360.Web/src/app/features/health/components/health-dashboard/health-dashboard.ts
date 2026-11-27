import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { catchError, distinctUntilChanged, map, of, switchMap, tap, Subject, startWith, combineLatest } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { NgxEchartsDirective } from 'ngx-echarts';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { AnalyticsService, HealthTrendsDto } from '../../../../core/services/analytics.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { HealthNavComponent } from '../health-nav/health-nav.component';

/**
 * Health trends (route /health/analytics). The Health overview answers "what needs doing today";
 * this page answers "how has the last year gone": deaths by month, common illnesses, vaccine coverage.
 * All figures are counted on the server over every record in the 12-month window.
 */
@Component({
  selector: 'app-health-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    MatButtonModule,
    NgxEchartsDirective,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    MoneyPipe,
    TranslatePipe,
    HealthNavComponent
  ],
  templateUrl: './health-dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HealthDashboardComponent {
  private analyticsService = inject(AnalyticsService);
  private workingContext = inject(WorkingContextService);
  private readonly i18n = inject(I18nService);

  /** The farm picked in the context selector; the page re-loads whenever it changes. */
  private farm = toSignal(this.workingContext.currentFarm$, { initialValue: this.workingContext.currentFarmValue });
  public hasFarm = computed(() => !!this.farm()?.id);

  public isLoading = signal(false);
  public loadFailed = signal(false);
  private data = signal<HealthTrendsDto | null>(null);
  private reload$ = new Subject<void>();

  public hasData = computed(() => !!this.data());

  public coverage = computed(() => Math.round(this.data()?.vaccinationCoveragePercent ?? 0));
  public totalDeaths = computed(() => this.data()?.totalDeaths ?? 0);
  public treatmentCount = computed(() => this.data()?.totalTreatments ?? 0);
  public treatmentCost = computed(() => this.data()?.totalTreatmentCostBdt ?? 0);

  public topIllnesses = computed(() => {
    const rows = this.data()?.topIllnesses ?? [];
    const max = rows[0]?.count || 1;
    return rows.map(r => ({ name: r.name, count: r.count, share: (r.count / max) * 100 }));
  });

  public deathsChart = computed(() => {
    const months = (this.data()?.months ?? []).map(m => ({
      label: new Date(m.year, m.month - 1, 1).toLocaleDateString(this.i18n.isBangla ? 'bn-BD' : 'en-GB', { month: 'short' }),
      count: m.deaths
    }));

    return {
      grid: { left: 28, right: 8, top: 12, bottom: 24 },
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      xAxis: {
        type: 'category',
        data: months.map(m => m.label),
        axisTick: { show: false },
        axisLine: { lineStyle: { color: '#d1d5db' } },
        axisLabel: { color: '#9ca3af', fontSize: 11 }
      },
      yAxis: {
        type: 'value',
        minInterval: 1,
        axisLabel: { color: '#9ca3af', fontSize: 11 },
        splitLine: { lineStyle: { color: 'rgba(156,163,175,0.2)' } }
      },
      series: [{
        name: this.i18n.t('Deaths'),
        type: 'bar',
        barMaxWidth: 22,
        itemStyle: { color: '#e11d48', borderRadius: [4, 4, 0, 0] },
        data: months.map(m => m.count)
      }]
    };
  });

  constructor() {
    combineLatest([
      this.workingContext.currentFarm$.pipe(map(f => f?.id ?? null), distinctUntilChanged()),
      this.reload$.pipe(startWith(undefined))
    ]).pipe(
      map(([id]) => id),
      tap(id => {
        this.data.set(null);
        this.loadFailed.set(false);
        this.isLoading.set(!!id);
      }),
      switchMap(id => !id ? of(null) : this.analyticsService.getHealthTrends(id).pipe(
        catchError(() => {
          this.loadFailed.set(true);
          return of(null);
        })
      )),
      takeUntilDestroyed()
    ).subscribe(result => {
      this.data.set(result);
      this.isLoading.set(false);
    });
  }

  public reload(): void {
    this.reload$.next();
  }

  public printReport(): void {
    window.print();
  }

}
