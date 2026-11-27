import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { catchError, distinctUntilChanged, map, of, switchMap, tap } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import * as echarts from 'echarts/core';
import { PieChart, LineChart } from 'echarts/charts';
import { TitleComponent, TooltipComponent, GridComponent, LegendComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { AnalyticsService, BreedingAnalyticsDto } from '../../../../core/services/analytics.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

echarts.use([PieChart, LineChart, TitleComponent, TooltipComponent, GridComponent, LegendComponent, CanvasRenderer]);

@Component({
  selector: 'app-breeding-dashboard',
  standalone: true,
  imports: [
    CommonModule, 
    MatIconModule, 
    MatButtonModule, 
    NgxEchartsDirective,
    PageHeaderComponent, 
    LoadingComponent, 
    EmptyStateComponent,
    TranslatePipe
  ],
  templateUrl: './breeding-dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BreedingDashboardComponent {
  private analyticsService = inject(AnalyticsService);
  private workingContext = inject(WorkingContextService);
  private router = inject(Router);
  private i18n = inject(I18nService);
  readonly noDataDescription = this.i18n.t("No matings recorded yet. Record one from a female animal's page, in its Breeding History tab.");

  /** The farm picked in the context selector; the page re-loads whenever it changes. */
  private farm = toSignal(this.workingContext.currentFarm$, { initialValue: this.workingContext.currentFarmValue });
  public hasFarm = computed(() => !!this.farm()?.id);

  public isLoading = signal(false);
  public loadFailed = signal(false);
  private data = signal<BreedingAnalyticsDto | null>(null);

  /** All-zero analytics means the farm has no breeding records yet. */
  public hasData = computed(() => {
    const d = this.data();
    return !!d && (d.totalConfirmedPregnancies > 0 || d.expectedCalvingsNext30Days > 0 || d.conceptionRatePercentage > 0);
  });

  public chartOptions = computed(() => {
    const data = this.data();
    if (!data) return {};

    return {
      tooltip: {
        trigger: 'item',
        formatter: '{a} <br/>{b}: {c} ({d}%)'
      },
      legend: {
        orient: 'vertical',
        left: 'left'
      },
      color: ['#8b5cf6', '#f43f5e'],
      series: [
        {
          name: this.i18n.t('Conception'),
          type: 'pie',
          radius: ['50%', '70%'],
          avoidLabelOverlap: false,
          label: {
            show: false,
            position: 'center'
          },
          emphasis: {
            label: {
              show: true,
              fontSize: '20',
              fontWeight: 'bold'
            }
          },
          labelLine: {
            show: false
          },
          data: [
            { value: data.conceptionRatePercentage, name: this.i18n.t('Conception Rate') },
            { value: 100 - data.conceptionRatePercentage, name: this.i18n.t('Did Not Conceive') }
          ]
        }
      ]
    };
  });

  constructor() {
    this.workingContext.currentFarm$.pipe(
      map(f => f?.id ?? null),
      distinctUntilChanged(),
      tap(id => {
        this.data.set(null);
        this.loadFailed.set(false);
        this.isLoading.set(!!id);
      }),
      switchMap(id => !id ? of(null) : this.analyticsService.getBreedingAnalytics(id).pipe(
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

  public printReport(): void {
    window.print();
  }

  /** Breeding is recorded on a female animal's page (Breeding History tab). */
  public goToAnimals(): void {
    this.router.navigate(['/livestock']);
  }

  public get dataValue(): BreedingAnalyticsDto | undefined {
    return this.data() ?? undefined;
  }
}
