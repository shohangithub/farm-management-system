import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges, ViewChild, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { VaccinationCompliance } from '../../models/dashboard.model';

@Component({
  selector: 'app-vaccination-compliance-chart',
  standalone: true,
  imports: [CommonModule, MatIconModule, BaseChartDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col h-full">
      <!-- Top Compliance Badge Header -->
      <div class="flex items-center justify-between pb-3 mb-2 border-b border-gray-100 dark:border-gray-800/60">
        <span class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Health Status:</span>
        <div 
          class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold"
          [ngClass]="getComplianceBadgeClass()">
          <span class="w-1.5 h-1.5 rounded-full" [ngClass]="getComplianceDotClass()"></span>
          {{ compliancePercentage() }}% Compliance Rate
        </div>
      </div>

      <!-- Chart Content Area -->
      <div *ngIf="hasData(); else emptyState" class="flex flex-col flex-1 justify-between">
        <!-- Chart Canvas with Centered Gauge Rate -->
        <div class="relative flex items-center justify-center h-[210px] my-auto">
          <canvas baseChart
            [data]="chartData"
            [options]="chartOptions"
            [type]="chartType">
          </canvas>
          
          <!-- Center Stat Overlay -->
          <div class="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
            <span 
              class="text-3xl font-extrabold tracking-tight leading-none"
              [ngClass]="getComplianceTextClass()">
              {{ compliancePercentage() }}%
            </span>
            <span class="text-[11px] font-semibold text-gray-400 dark:text-gray-500 uppercase tracking-wider mt-1">
              Protected
            </span>
          </div>
        </div>

        <!-- Metric Breakdown Badges -->
        <div class="grid grid-cols-3 gap-2 pt-3 border-t border-gray-100 dark:border-gray-800/60">
          <div class="flex flex-col items-center p-2 rounded-xl bg-emerald-50/60 dark:bg-emerald-950/20 border border-emerald-100/60 dark:border-emerald-800/30">
            <div class="flex items-center gap-1 text-[11px] font-medium text-emerald-700 dark:text-emerald-400">
              <mat-icon class="text-[14px] w-[14px] h-[14px]">check_circle</mat-icon>
              Completed
            </div>
            <span class="text-base font-bold text-gray-900 dark:text-white mt-0.5">{{ completedCount() }}</span>
          </div>

          <div class="flex flex-col items-center p-2 rounded-xl bg-amber-50/60 dark:bg-amber-950/20 border border-amber-100/60 dark:border-amber-800/30">
            <div class="flex items-center gap-1 text-[11px] font-medium text-amber-700 dark:text-amber-400">
              <mat-icon class="text-[14px] w-[14px] h-[14px]">schedule</mat-icon>
              Due Soon
            </div>
            <span class="text-base font-bold text-gray-900 dark:text-white mt-0.5">{{ dueCount() }}</span>
          </div>

          <div class="flex flex-col items-center p-2 rounded-xl bg-rose-50/60 dark:bg-rose-950/20 border border-rose-100/60 dark:border-rose-800/30">
            <div class="flex items-center gap-1 text-[11px] font-medium text-rose-700 dark:text-rose-400">
              <mat-icon class="text-[14px] w-[14px] h-[14px]">warning</mat-icon>
              Overdue
            </div>
            <span class="text-base font-bold text-gray-900 dark:text-white mt-0.5">{{ overdueCount() }}</span>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <ng-template #emptyState>
        <div class="flex flex-col items-center justify-center flex-1 py-12 text-center text-gray-400 dark:text-gray-500">
          <div class="w-14 h-14 rounded-2xl bg-gray-50 dark:bg-gray-800/50 border border-gray-100 dark:border-gray-700/50 flex items-center justify-center mb-3">
            <mat-icon class="text-2xl text-gray-400">health_and_safety</mat-icon>
          </div>
          <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">No vaccination schedules</p>
          <p class="text-xs text-gray-400 max-w-[220px]">Schedule vaccination events or protocols to monitor compliance.</p>
        </div>
      </ng-template>
    </div>
  `
})
export class VaccinationComplianceChartComponent implements OnChanges {
  @Input() data: VaccinationCompliance | null = null;
  @ViewChild(BaseChartDirective) chart: BaseChartDirective | undefined;

  private rawData = signal<VaccinationCompliance | null>(null);

  public readonly chartType = 'doughnut' as const;

  public completedCount = computed(() => this.rawData()?.completed ?? 0);
  public dueCount = computed(() => this.rawData()?.due ?? 0);
  public overdueCount = computed(() => this.rawData()?.overdue ?? 0);

  public totalEvents = computed(() => this.completedCount() + this.dueCount() + this.overdueCount());
  public hasData = computed(() => this.totalEvents() > 0);

  public compliancePercentage = computed(() => {
    const total = this.totalEvents();
    if (total === 0) return 100;
    return Math.round((this.completedCount() / total) * 100);
  });

  public chartData: ChartData<'doughnut'> = {
    labels: ['Completed', 'Due Soon (7 Days)', 'Overdue'],
    datasets: [{
      data: [],
      backgroundColor: ['#10b981', '#f59e0b', '#f43f5e'],
      borderWidth: 2,
      borderColor: '#ffffff',
      hoverOffset: 4
    }]
  };

  public chartOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '72%',
    plugins: {
      legend: {
        display: false
      },
      tooltip: {
        backgroundColor: 'rgba(17, 24, 39, 0.95)',
        titleFont: { family: 'Inter, sans-serif', size: 12, weight: 'bold' },
        bodyFont: { family: 'Inter, sans-serif', size: 12 },
        padding: 10,
        cornerRadius: 10,
        callbacks: {
          label: (context) => {
            const label = context.label || '';
            const value = (context.raw as number) || 0;
            const total = this.totalEvents();
            const percentage = total > 0 ? Math.round((value / total) * 100) : 0;
            return ` ${label}: ${value} doses (${percentage}%)`;
          }
        }
      }
    }
  };

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data']) {
      this.rawData.set(this.data);
      if (this.data) {
        this.chartData = {
          labels: ['Completed', 'Due Soon (7 Days)', 'Overdue'],
          datasets: [{
            data: [this.data.completed, this.data.due, this.data.overdue],
            backgroundColor: ['#10b981', '#f59e0b', '#f43f5e'],
            borderWidth: 2,
            borderColor: '#ffffff',
            hoverOffset: 4
          }]
        };
        this.chart?.update();
      }
    }
  }

  public getComplianceBadgeClass(): string {
    const rate = this.compliancePercentage();
    if (rate >= 80) return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/50';
    if (rate >= 50) return 'bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 border border-amber-200/50';
    return 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 border border-rose-200/50';
  }

  public getComplianceDotClass(): string {
    const rate = this.compliancePercentage();
    if (rate >= 80) return 'bg-emerald-500';
    if (rate >= 50) return 'bg-amber-500';
    return 'bg-rose-500';
  }

  public getComplianceTextClass(): string {
    const rate = this.compliancePercentage();
    if (rate >= 80) return 'text-emerald-600 dark:text-emerald-400';
    if (rate >= 50) return 'text-amber-600 dark:text-amber-400';
    return 'text-rose-600 dark:text-rose-400';
  }
}
