import { Component, ChangeDetectionStrategy, inject, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
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
    EmptyStateComponent
  ],
  templateUrl: './breeding-dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BreedingDashboardComponent {
  private analyticsService = inject(AnalyticsService);
  private workingContext = inject(WorkingContextService);

  private farmId = computed(() => this.workingContext.currentFarmValue?.id);

  // Derive the query observable, mapping empty farmId to null so we don't query
  private breedingData$ = computed(() => {
    const id = this.farmId();
    if (!id) return null;
    return this.analyticsService.getBreedingAnalytics(id);
  });

  // Use toSignal to automatically subscribe/unsubscribe and turn the Observable into a Signal
  public isLoading = signal(true);
  
  // We'll fetch data when the component initializes, using the current farm ID.
  
  private data = toSignal(this.analyticsService.getBreedingAnalytics(this.workingContext.currentFarmValue?.id || ''));

  public hasData = computed(() => !!this.data());

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
      color: ['#10b981', '#f43f5e'],
      series: [
        {
          name: 'Conception',
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
            { value: data.conceptionRatePercentage, name: 'Conception Rate' },
            { value: 100 - data.conceptionRatePercentage, name: 'Failed' }
          ]
        }
      ]
    };
  });

  constructor() {
    effect(() => {
      // Manage loading state
      if (this.data()) {
        this.isLoading.set(false);
      }
    }, { allowSignalWrites: true });
  }

  public get dataValue(): BreedingAnalyticsDto | undefined {
    return this.data() as BreedingAnalyticsDto | undefined;
  }
}
