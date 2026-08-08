import { Component, ChangeDetectionStrategy, inject, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { NgxEchartsDirective } from 'ngx-echarts';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { AnalyticsService, HealthAnalyticsDto } from '../../../../core/services/analytics.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';

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
    EmptyStateComponent
  ],
  templateUrl: './health-dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HealthDashboardComponent {
  private analyticsService = inject(AnalyticsService);
  private workingContext = inject(WorkingContextService);

  public isLoading = signal(true);
  
  private data = toSignal(this.analyticsService.getHealthAnalytics(this.workingContext.currentFarmValue?.id || ''));

  public hasData = computed(() => !!this.data());

  public chartOptions = computed(() => {
    const data = this.data() as HealthAnalyticsDto | undefined;
    if (!data) return {};

    return {
      tooltip: {
        trigger: 'item',
        formatter: '{a} <br/>{b}: {c}%'
      },
      legend: {
        orient: 'vertical',
        left: 'left'
      },
      color: ['#10b981', '#f59e0b'],
      series: [
        {
          name: 'Vaccination Compliance',
          type: 'pie',
          radius: ['40%', '70%'],
          avoidLabelOverlap: false,
          itemStyle: {
            borderRadius: 10,
            borderColor: '#fff',
            borderWidth: 2
          },
          label: {
            show: false,
            position: 'center'
          },
          emphasis: {
            label: {
              show: true,
              fontSize: '20',
              fontWeight: 'bold',
              formatter: '{d}%'
            }
          },
          labelLine: {
            show: false
          },
          data: [
            { value: data.vaccinationCompliancePercentage, name: 'Vaccinated (Last 6 Mos)' },
            { value: 100 - data.vaccinationCompliancePercentage, name: 'Not Vaccinated / Overdue' }
          ]
        }
      ]
    };
  });

  constructor() {
    effect(() => {
      if (this.data()) {
        this.isLoading.set(false);
      }
    }, { allowSignalWrites: true });
  }

  public get dataValue(): HealthAnalyticsDto | undefined {
    return this.data() as HealthAnalyticsDto | undefined;
  }
}
