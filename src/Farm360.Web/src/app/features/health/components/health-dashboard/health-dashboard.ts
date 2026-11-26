import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { catchError, distinctUntilChanged, forkJoin, map, of, switchMap, tap, Subject, startWith, combineLatest } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { NgxEchartsDirective } from 'ngx-echarts';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { AnalyticsService, HealthAnalyticsDto } from '../../../../core/services/analytics.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { HealthService } from '../../services/health.service';
import { MedicalTreatmentDto, MortalityRecordDto } from '../../models/health.models';
import { HealthNavComponent } from '../health-nav/health-nav.component';

interface TrendsData {
  summary: HealthAnalyticsDto;
  deaths: MortalityRecordDto[];
  treatments: MedicalTreatmentDto[];
}

/**
 * Health trends (route /health/analytics). The Health overview answers "what needs doing today";
 * this page answers "how has the last year gone": deaths by month, common illnesses, vaccine coverage.
 * Monthly and illness figures are worked out here from the latest 100 deaths and treatments.
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
  private healthService = inject(HealthService);
  private workingContext = inject(WorkingContextService);
  private readonly i18n = inject(I18nService);

  /** The farm picked in the context selector; the page re-loads whenever it changes. */
  private farm = toSignal(this.workingContext.currentFarm$, { initialValue: this.workingContext.currentFarmValue });
  public hasFarm = computed(() => !!this.farm()?.id);

  public isLoading = signal(false);
  public loadFailed = signal(false);
  private data = signal<TrendsData | null>(null);
  private reload$ = new Subject<void>();

  public hasData = computed(() => !!this.data());

  /** Start of the 12-month window (first day of the month, 11 months back). */
  private readonly since = (() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth() - 11, 1);
  })();

  public coverage = computed(() => Math.round(this.data()?.summary.vaccinationCompliancePercentage ?? 0));

  private recentTreatments = computed(() =>
    (this.data()?.treatments ?? []).filter(t => new Date(t.startDate) >= this.since));

  public treatmentCount = computed(() => this.recentTreatments().length);
  public treatmentCost = computed(() => this.recentTreatments().reduce((sum, t) => sum + (t.costBdt || 0), 0));

  public topIllnesses = computed(() => {
    const counts = new Map<string, number>();
    for (const t of this.recentTreatments()) {
      const name = (t.diagnosis || '').trim();
      if (name) counts.set(name, (counts.get(name) ?? 0) + 1);
    }
    const rows = [...counts.entries()].sort((a, b) => b[1] - a[1]).slice(0, 5);
    const max = rows[0]?.[1] ?? 1;
    return rows.map(([name, count]) => ({ name, count, share: (count / max) * 100 }));
  });

  public deathsChart = computed(() => {
    const months: { key: string; label: string; count: number }[] = [];
    for (let i = 0; i < 12; i++) {
      const d = new Date(this.since.getFullYear(), this.since.getMonth() + i, 1);
      months.push({
        key: `${d.getFullYear()}-${d.getMonth()}`,
        label: d.toLocaleDateString(this.i18n.isBangla ? 'bn-BD' : 'en-GB', { month: 'short' }),
        count: 0
      });
    }
    for (const m of this.data()?.deaths ?? []) {
      const d = new Date(m.deathDate);
      const slot = months.find(x => x.key === `${d.getFullYear()}-${d.getMonth()}`);
      if (slot) slot.count++;
    }

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
      switchMap(id => !id ? of(null) : forkJoin({
        summary: this.analyticsService.getHealthAnalytics(id),
        deaths: this.healthService.getMortalityRecords({ farmId: id, pageNumber: 1, pageSize: 100, sortBy: 'deathdate', sortDesc: true })
          .pipe(map(r => r.items), catchError(() => of([] as MortalityRecordDto[]))),
        treatments: this.healthService.getTreatments({ farmId: id, pageNumber: 1, pageSize: 100, sortBy: 'startdate', sortDesc: true })
          .pipe(map(r => r.items), catchError(() => of([] as MedicalTreatmentDto[])))
      }).pipe(
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

  public get dataValue(): HealthAnalyticsDto | undefined {
    return this.data()?.summary ?? undefined;
  }
}
