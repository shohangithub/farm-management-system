import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges, ViewChild, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { HerdComposition } from '../../models/dashboard.model';

export type CompositionTab = 'species' | 'sex' | 'status';

interface LegendItem {
  label: string;
  count: number;
  percentage: number;
  color: string;
}

@Component({
  selector: 'app-herd-composition-chart',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, BaseChartDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col h-full">
      <!-- Filter Pill Tabs -->
      <div class="flex items-center justify-between gap-2 pb-3 mb-2 border-b border-gray-100 dark:border-gray-800/60">
        <span class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Group By:</span>
        <div class="inline-flex p-1 bg-gray-100/80 dark:bg-gray-800/80 rounded-xl border border-gray-200/50 dark:border-gray-700/50 text-xs">
          <button 
            type="button"
            (click)="setTab('species')"
            [class.bg-white]="activeTab() === 'species'"
            [class.dark:bg-gray-700]="activeTab() === 'species'"
            [class.shadow-sm]="activeTab() === 'species'"
            [class.text-emerald-600]="activeTab() === 'species'"
            [class.dark:text-emerald-400]="activeTab() === 'species'"
            [class.text-gray-500]="activeTab() !== 'species'"
            class="px-3 py-1 font-medium rounded-lg transition-all cursor-pointer">
            Species
          </button>
          <button 
            type="button"
            (click)="setTab('sex')"
            [class.bg-white]="activeTab() === 'sex'"
            [class.dark:bg-gray-700]="activeTab() === 'sex'"
            [class.shadow-sm]="activeTab() === 'sex'"
            [class.text-emerald-600]="activeTab() === 'sex'"
            [class.dark:text-emerald-400]="activeTab() === 'sex'"
            [class.text-gray-500]="activeTab() !== 'sex'"
            class="px-3 py-1 font-medium rounded-lg transition-all cursor-pointer">
            Sex
          </button>
          <button 
            type="button"
            (click)="setTab('status')"
            [class.bg-white]="activeTab() === 'status'"
            [class.dark:bg-gray-700]="activeTab() === 'status'"
            [class.shadow-sm]="activeTab() === 'status'"
            [class.text-emerald-600]="activeTab() === 'status'"
            [class.dark:text-emerald-400]="activeTab() === 'status'"
            [class.text-gray-500]="activeTab() !== 'status'"
            class="px-3 py-1 font-medium rounded-lg transition-all cursor-pointer">
            Status
          </button>
        </div>
      </div>

      <!-- Content Area -->
      <div *ngIf="hasData(); else emptyState" class="flex flex-col flex-1 justify-between">
        <!-- Chart Canvas with Centered Counter -->
        <div class="relative flex items-center justify-center h-[210px] my-auto">
          <canvas baseChart
            [data]="chartData"
            [options]="chartOptions"
            [type]="chartType">
          </canvas>
          
          <!-- Center Stat Overlay -->
          <div class="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
            <span class="text-3xl font-extrabold text-gray-900 dark:text-white tracking-tight leading-none">
              {{ totalCount() }}
            </span>
            <span class="text-[11px] font-semibold text-gray-400 dark:text-gray-500 uppercase tracking-wider mt-1">
              Total Head
            </span>
          </div>
        </div>

        <!-- Custom Legend Pills -->
        <div class="flex flex-wrap items-center justify-center gap-2 pt-3 border-t border-gray-100 dark:border-gray-800/60 max-h-[75px] overflow-y-auto">
          <div 
            *ngFor="let item of legendItems()" 
            class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-gray-50 dark:bg-gray-800/60 border border-gray-100 dark:border-gray-700/40 text-xs">
            <span class="w-2.5 h-2.5 rounded-full flex-shrink-0" [style.backgroundColor]="item.color"></span>
            <span class="font-medium text-gray-700 dark:text-gray-300">{{ item.label }}</span>
            <span class="font-bold text-gray-900 dark:text-white">{{ item.count }}</span>
            <span class="text-gray-400 text-[10px]">({{ item.percentage }}%)</span>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <ng-template #emptyState>
        <div class="flex flex-col items-center justify-center flex-1 py-12 text-center text-gray-400 dark:text-gray-500">
          <div class="w-14 h-14 rounded-2xl bg-gray-50 dark:bg-gray-800/50 border border-gray-100 dark:border-gray-700/50 flex items-center justify-center mb-3">
            <mat-icon class="text-2xl text-gray-400">donut_large</mat-icon>
          </div>
          <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">No demographics data</p>
          <p class="text-xs text-gray-400 max-w-[220px]">Register livestock to view demographic composition and distribution.</p>
        </div>
      </ng-template>
    </div>
  `
})
export class HerdCompositionChartComponent implements OnChanges {
  @Input() data: HerdComposition | null = null;
  @ViewChild(BaseChartDirective) chart: BaseChartDirective | undefined;

  public activeTab = signal<CompositionTab>('species');
  private rawData = signal<HerdComposition | null>(null);

  public readonly chartType = 'doughnut' as const;

  private readonly speciesPalette = ['#10b981', '#3b82f6', '#f59e0b', '#8b5cf6', '#ec4899', '#06b6d4'];
  private readonly sexPalette = ['#0284c7', '#ec4899', '#94a3b8'];
  private readonly statusPalette = ['#10b981', '#f59e0b', '#6366f1', '#ef4444', '#64748b'];

  public chartData: ChartData<'doughnut'> = {
    labels: [],
    datasets: [{
      data: [],
      backgroundColor: [],
      borderWidth: 2,
      borderColor: '#ffffff',
      hoverBorderColor: '#ffffff',
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
        boxPadding: 4,
        callbacks: {
          label: (context) => {
            const label = context.label || '';
            const value = (context.raw as number) || 0;
            const total = (context.chart.data.datasets[0].data as number[]).reduce((a, b) => a + b, 0);
            const percentage = total > 0 ? Math.round((value / total) * 100) : 0;
            return ` ${label}: ${value} head (${percentage}%)`;
          }
        }
      }
    }
  };

  public totalCount = computed(() => {
    const d = this.rawData();
    if (!d) return 0;
    const currentMap = this.getCurrentDataMap(d, this.activeTab());
    return Object.values(currentMap).reduce((sum, val) => sum + val, 0);
  });

  public hasData = computed(() => this.totalCount() > 0);

  public legendItems = computed<LegendItem[]>(() => {
    const d = this.rawData();
    if (!d) return [];
    const tab = this.activeTab();
    const currentMap = this.getCurrentDataMap(d, tab);
    const total = Object.values(currentMap).reduce((sum, val) => sum + val, 0);
    const palette = this.getPalette(tab);

    return Object.entries(currentMap).map(([label, count], index) => ({
      label: this.formatLabel(label),
      count,
      percentage: total > 0 ? Math.round((count / total) * 100) : 0,
      color: palette[index % palette.length]
    }));
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data']) {
      this.rawData.set(this.data);
      this.updateChart();
    }
  }

  public setTab(tab: CompositionTab): void {
    this.activeTab.set(tab);
    this.updateChart();
  }

  private updateChart(): void {
    const d = this.rawData();
    if (!d) return;

    const tab = this.activeTab();
    const currentMap = this.getCurrentDataMap(d, tab);
    const labels = Object.keys(currentMap).map(k => this.formatLabel(k));
    const values = Object.values(currentMap);
    const palette = this.getPalette(tab);

    this.chartData = {
      labels,
      datasets: [{
        data: values,
        backgroundColor: values.map((_, i) => palette[i % palette.length]),
        borderWidth: 2,
        borderColor: '#ffffff',
        hoverBorderColor: '#ffffff',
        hoverOffset: 4
      }]
    };

    this.chart?.update();
  }

  private getCurrentDataMap(data: HerdComposition, tab: CompositionTab): Record<string, number> {
    switch (tab) {
      case 'species': return data.bySpecies || {};
      case 'sex': return data.bySex || {};
      case 'status': return data.byStatus || {};
      default: return data.bySpecies || {};
    }
  }

  private getPalette(tab: CompositionTab): string[] {
    switch (tab) {
      case 'species': return this.speciesPalette;
      case 'sex': return this.sexPalette;
      case 'status': return this.statusPalette;
      default: return this.speciesPalette;
    }
  }

  private formatLabel(raw: string): string {
    if (raw === '1') return 'Male';
    if (raw === '2') return 'Female';
    return raw.replace(/([A-Z])/g, ' $1').trim();
  }
}
