import { Component, Input, OnChanges, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { VaccinationCompliance } from '../../models/dashboard.model';

@Component({
  selector: 'app-vaccination-compliance-chart',
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
export class VaccinationComplianceChartComponent implements OnChanges {
  @Input() data: VaccinationCompliance | null = null;
  @ViewChild(BaseChartDirective) chart: BaseChartDirective | undefined;

  public chartType: ChartType = 'polarArea';
  
  public chartData: ChartData<'polarArea'> = {
    labels: ['Completed', 'Due (7 Days)', 'Overdue'],
    datasets: [{ data: [] }]
  };

  public chartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'bottom' }
    }
  };

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data'] && this.data) {
      this.chartData.datasets[0].data = [
        this.data.completed,
        this.data.due,
        this.data.overdue
      ];
      this.chart?.update();
    }
  }
}
