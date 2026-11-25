import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges, ViewChild, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { RouterModule } from '@angular/router';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { HerdComposition } from '../../models/dashboard.model';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { translate } from '../../../../core/i18n/i18n.service';

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
  imports: [CommonModule, MatIconModule, MatButtonModule, BaseChartDirective, RouterModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col h-full">
      <!-- Filter Pill Tabs -->
      <div class="flex flex-wrap items-center justify-between gap-2 pb-3 mb-2 border-b border-gray-100 dark:border-gray-700/60">
        <span id="herd-group-label" class="text-xs font-medium text-gray-500 dark:text-gray-400">{{ 'Group by' | t }}</span>
        <div class="inline-flex p-1 bg-gray-100 dark:bg-gray-900/40 rounded-xl text-xs" role="group" aria-labelledby="herd-group-label">
          <button *ngFor="let tab of tabs"
            type="button"
            (click)="setTab(tab.key)"
            [attr.aria-pressed]="activeTab() === tab.key"
            [ngClass]="activeTab() === tab.key
              ? 'bg-white dark:bg-gray-700 shadow-sm text-primary-700 dark:text-primary-300'
              : 'text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200'"
            class="min-h-[32px] px-3 font-medium rounded-lg transition-colors cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500">
            {{ tab.label | t }}
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
            <span class="text-3xl font-semibold tabular-nums text-gray-900 dark:text-white tracking-tight leading-none">
              {{ totalCount() }}
            </span>
            <span class="text-xs font-medium text-gray-500 dark:text-gray-400 mt-1">
              {{ (activeTab() === 'status' ? 'All-time animals' : 'Head on farm') | t }}
            </span>
          </div>
        </div>

        <!-- Custom Legend Pills -->
        <div class="flex flex-wrap items-center justify-center gap-2 pt-3 border-t border-gray-100 dark:border-gray-700/60 max-h-[88px] overflow-y-auto">
          <div 
            *ngFor="let item of legendItems()" 
            class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-gray-50 dark:bg-gray-900/30 text-xs">
            <span class="w-2.5 h-2.5 rounded-full flex-shrink-0" [style.backgroundColor]="item.color"></span>
            <span class="font-medium text-gray-700 dark:text-gray-300">{{ item.label }}</span>
            <span class="font-bold text-gray-900 dark:text-white">{{ item.count }}</span>
            <span class="text-gray-500 dark:text-gray-400">({{ item.percentage }}%)</span>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <ng-template #emptyState>
        <div class="flex flex-col items-center justify-center flex-1 py-10 text-center">
          <div class="w-12 h-12 rounded-full bg-gray-100 dark:bg-gray-700/50 text-gray-500 dark:text-gray-400 flex items-center justify-center mb-3" aria-hidden="true">
            <mat-icon>donut_large</mat-icon>
          </div>
          <p class="text-sm font-medium text-gray-800 dark:text-gray-200 m-0">{{ 'No demographics data' | t }}</p>
          <p class="text-sm text-gray-500 dark:text-gray-400 max-w-[260px] m-0 mt-1">{{ 'Register livestock to view demographic composition and distribution.' | t }}</p>
          <a routerLink="/livestock/register" class="mt-4 inline-flex items-center gap-1 min-h-[44px] px-3 rounded-lg text-sm font-semibold text-primary-700 dark:text-primary-400 hover:bg-primary-50 dark:hover:bg-primary-500/10 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500">
            {{ 'Register animals' | t }}
            <mat-icon class="text-[18px] w-[18px] h-[18px]" aria-hidden="true">arrow_forward</mat-icon>
          </a>
        </div>
      </ng-template>
    </div>
  `
})
export class HerdCompositionChartComponent implements OnChanges {
  @Input() data: HerdComposition | null = null;
  @ViewChild(BaseChartDirective) chart: BaseChartDirective | undefined;

  public readonly tabs: { key: CompositionTab; label: string }[] = [
    { key: 'species', label: 'Species' },
    { key: 'sex', label: 'Sex' },
    { key: 'status', label: 'Status' }
  ];

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
      borderWidth: 0,
      spacing: 2,
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
            return ` ${label}: ${translate('{value} head ({percentage}%)', { value, percentage })}`;
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
        borderWidth: 0,
        spacing: 2,
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
    if (raw === '1') return translate('Male');
    if (raw === '2') return translate('Female');
    return translate(raw.replace(/([A-Z])/g, ' $1').trim());
  }
}
