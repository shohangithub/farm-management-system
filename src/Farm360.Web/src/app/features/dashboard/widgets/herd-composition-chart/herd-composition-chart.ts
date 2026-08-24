import { Component, Input, OnChanges, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { HerdComposition } from '../../models/dashboard.model';

@Component({
  selector: 'app-herd-composition-chart',
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
export class HerdCompositionChartComponent implements OnChanges {
  @Input() data: HerdComposition | null = null;
  @ViewChild(BaseChartDirective) chart: BaseChartDirective | undefined;

  public chartType: ChartType = 'doughnut';
  
  public chartData: ChartData<'doughnut'> = {
    labels: [],
    datasets: [{ data: [] }]
  };

  public chartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'right' }
    }
  };

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data'] && this.data) {
      // Just showing by species for the basic chart
      const speciesData = this.data.bySpecies || {};
      this.chartData.labels = Object.keys(speciesData);
      this.chartData.datasets[0].data = Object.values(speciesData);
      this.chart?.update();
    }
  }
}
