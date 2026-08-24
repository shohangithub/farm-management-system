import { Component, Input, OnChanges, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { FeedCostTrend } from '../../models/dashboard.model';

@Component({
  selector: 'app-feed-cost-trend-chart',
  standalone: true,
  imports: [CommonModule, BaseChartDirective],
  template: `
    <div class="chart-container" style="position: relative; height:300px; width:100%">
      <canvas baseChart
        [data]="chartData"
        [options]="chartOptions"
        [type]="chartType">
      </canvas>
    </div>
  `
})
export class FeedCostTrendChartComponent implements OnChanges {
  @Input() data: FeedCostTrend[] | null = null;
  @ViewChild(BaseChartDirective) chart: BaseChartDirective | undefined;

  public chartType: ChartType = 'bar';
  
  public chartData: ChartData<'bar'> = {
    labels: [],
    datasets: []
  };

  public chartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'bottom' }
    }
  };

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data'] && this.data && this.data.length > 0) {
      this.chartData.labels = this.data[0].dataPoints.map(p => p.label);
      this.chartData.datasets = this.data.map(group => ({
        data: group.dataPoints.map(p => p.costPerAnimal),
        label: group.groupName
      }));
      this.chart?.update();
    }
  }
}
