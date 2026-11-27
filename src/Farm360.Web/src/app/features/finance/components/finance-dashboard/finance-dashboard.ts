import { Component, ChangeDetectionStrategy, inject, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { NgxEchartsDirective } from 'ngx-echarts';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { AnalyticsService, FinanceAnalyticsDto } from '../../../../core/services/analytics.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

@Component({
  selector: 'app-finance-dashboard',
  standalone: true,
  imports: [
    TranslatePipe, CommonModule, 
    MatIconModule, 
    MatButtonModule, 
    NgxEchartsDirective,
    PageHeaderComponent, 
    LoadingComponent, 
    EmptyStateComponent
  ],
  templateUrl: './finance-dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FinanceDashboardComponent {
  private analyticsService = inject(AnalyticsService);
  private workingContext = inject(WorkingContextService);
  private readonly i18n = inject(I18nService);

  public currentYear = signal(new Date().getFullYear());
  public isLoading = signal(true);
  
  private data = toSignal(this.analyticsService.getFinanceAnalytics(this.workingContext.currentFarmValue?.id || '', this.currentYear()));

  public hasData = computed(() => {
    const d = this.data() as FinanceAnalyticsDto | undefined;
    return d && d.monthlyData && d.monthlyData.length > 0;
  });

  public chartOptions = computed(() => {
    const data = this.data() as FinanceAnalyticsDto | undefined;
    if (!data || !data.monthlyData) return {};

    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'].map(m => this.i18n.t(m));
    const revenueName = this.i18n.t('Revenue (BDT)');
    const expenseName = this.i18n.t('Expense (BDT)');
    const revenues = data.monthlyData.map(m => m.totalRevenueBdt);
    const expenses = data.monthlyData.map(m => m.totalExpenseBdt);

    return {
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' }
      },
      legend: {
        data: [revenueName, expenseName]
      },
      grid: {
        left: '3%',
        right: '4%',
        bottom: '3%',
        containLabel: true
      },
      xAxis: {
        type: 'category',
        data: months
      },
      yAxis: {
        type: 'value'
      },
      color: ['#8b5cf6', '#ef4444'], // Green for revenue, Red for expense
      series: [
        {
          name: revenueName,
          type: 'bar',
          data: revenues
        },
        {
          name: expenseName,
          type: 'bar',
          data: expenses
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
}
