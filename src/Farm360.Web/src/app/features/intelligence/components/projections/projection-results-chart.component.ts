import { Component, ChangeDetectionStrategy, input, effect, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProfitProjectionResponse } from '../../models/projection.model';
import { MatIconModule } from '@angular/material/icon';
import Chart from 'chart.js/auto';

@Component({
  selector: 'app-projection-results-chart',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  template: `
    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 relative overflow-hidden h-full flex flex-col">
      <div class="flex items-center gap-3 mb-6">
        <div class="w-10 h-10 rounded-xl bg-emerald-50 dark:bg-emerald-500/10 flex items-center justify-center text-emerald-600 dark:text-emerald-400">
          <mat-icon class="material-icons-outlined">trending_up</mat-icon>
        </div>
        <div>
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white m-0">Projection Trajectory</h3>
          <p class="text-sm text-gray-500 dark:text-gray-400 m-0">Cost vs Meat Value over time</p>
        </div>
      </div>

      <!-- KPI Summary -->
      <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6" *ngIf="data()?.summary as summary">
        <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-700/50">
          <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Total Cost</p>
          <p class="text-xl font-bold text-gray-900 dark:text-white">৳ {{ summary.totalInvestmentBdt | number:'1.0-0' }}</p>
        </div>
        <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-700/50">
          <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Meat Value</p>
          <p class="text-xl font-bold text-gray-900 dark:text-white">৳ {{ summary.expectedSaleValueBdt | number:'1.0-0' }}</p>
        </div>
        <div class="p-4 rounded-xl bg-emerald-50 dark:bg-emerald-500/10">
          <p class="text-xs text-emerald-600 dark:text-emerald-400 uppercase tracking-wider mb-1">Net Profit</p>
          <p class="text-xl font-bold text-emerald-600 dark:text-emerald-400">৳ {{ summary.profitLossBdt | number:'1.0-0' }}</p>
        </div>
        <div class="p-4 rounded-xl bg-indigo-50 dark:bg-indigo-500/10">
          <p class="text-xs text-indigo-600 dark:text-indigo-400 uppercase tracking-wider mb-1">ROI</p>
          <p class="text-xl font-bold text-indigo-600 dark:text-indigo-400">{{ summary.profitPercent | number:'1.1-2' }}%</p>
        </div>
      </div>

      <div class="flex-1 relative w-full" style="min-height: 300px;">
        <canvas #chartCanvas></canvas>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectionResultsChartComponent implements AfterViewInit {
  data = input<ProfitProjectionResponse | null>(null);

  @ViewChild('chartCanvas') chartCanvas!: ElementRef<HTMLCanvasElement>;
  private chartInstance: Chart | null = null;

  constructor() {
    effect(() => {
      const projectionData = this.data();
      if (projectionData && this.chartCanvas) {
        this.updateChart(projectionData);
      }
    });
  }

  ngAfterViewInit() {
    if (this.data()) {
      this.updateChart(this.data()!);
    }
  }

  private updateChart(data: ProfitProjectionResponse) {
    if (this.chartInstance) {
      this.chartInstance.destroy();
    }

    const ctx = this.chartCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    const labels = data.days.filter((_, i) => i % 5 === 0 || i === data.days.length - 1).map(d => `Day ${d.day}`);
    const costs = data.days.filter((_, i) => i % 5 === 0 || i === data.days.length - 1).map(d => d.totalInvestmentBdt);
    const values = data.days.filter((_, i) => i % 5 === 0 || i === data.days.length - 1).map(d => d.meatValueBdt);

    this.chartInstance = new Chart(ctx, {
      type: 'line',
      data: {
        labels,
        datasets: [
          {
            label: 'Total Investment (BDT)',
            data: costs,
            borderColor: '#f43f5e', // rose-500
            backgroundColor: 'rgba(244, 63, 94, 0.1)',
            borderWidth: 2,
            pointRadius: 0,
            fill: true,
            tension: 0.4
          },
          {
            label: 'Meat Value (BDT)',
            data: values,
            borderColor: '#10b981', // emerald-500
            backgroundColor: 'rgba(16, 185, 129, 0.1)',
            borderWidth: 2,
            pointRadius: 0,
            fill: true,
            tension: 0.4
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: {
          mode: 'index',
          intersect: false,
        },
        plugins: {
          legend: {
            position: 'top',
          },
          tooltip: {
            callbacks: {
              label: function(context) {
                let label = context.dataset.label || '';
                if (label) {
                  label += ': ';
                }
                if (context.parsed.y !== null) {
                  label += new Intl.NumberFormat('en-US', { style: 'currency', currency: 'BDT' }).format(context.parsed.y);
                }
                return label;
              }
            }
          }
        },
        scales: {
          y: {
            beginAtZero: false,
            ticks: {
              callback: function(value) {
                return '৳' + value;
              }
            }
          }
        }
      }
    });
  }
}
