import { Component, Input, OnChanges, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { AdgTrend } from '../../models/dashboard.model';

@Component({
  selector: 'app-adg-trend-chart',
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
export class AdgTrendChartComponent implements OnChanges {
  @Input() data: AdgTrend[] | null = null;
  @ViewChild(BaseChartDirective) chart: BaseChartDirective | undefined;

  public chartType: ChartType = 'line';
  
  public chartData: ChartData<'line'> = {
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
      // Assuming all batches share the same labels (e.g. months)
      this.chartData.labels = this.data[0].dataPoints.map(p => p.label);
      this.chartData.datasets = this.data.map(batch => ({
        data: batch.dataPoints.map(p => p.adgValue),
        label: batch.batchName,
        tension: 0.4
      }));
      this.chart?.update();
    }
  }
}
