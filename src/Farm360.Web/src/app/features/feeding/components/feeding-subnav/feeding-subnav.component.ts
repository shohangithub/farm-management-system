import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

interface FeedingNavLink {
  path: string;
  icon: string;
  label: string;
  labelBn: string;
}

/**
 * The Feeding section in the order the work happens: today's feeding, who eats what, then the
 * setup (feed charts and recipes) and the records.
 */
@Component({
  selector: 'app-feeding-subnav',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, MatIconModule],
  template: `
    <nav class="flex items-center gap-1.5 border-b border-gray-200 dark:border-gray-700/60 pb-3 mb-5 overflow-x-auto print:hidden">
      <ng-container *ngFor="let link of links; let i = index">
        <span *ngIf="i === 4 || i === 6" class="w-px h-6 bg-gray-200 dark:bg-gray-700 mx-1 shrink-0"></span>
        <a [routerLink]="link.path"
           routerLinkActive="!bg-emerald-50 dark:!bg-emerald-950/40 !text-emerald-700 dark:!text-emerald-300 !border-emerald-200 dark:!border-emerald-800/60"
           class="px-3.5 py-1.5 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 border border-transparent hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">{{ link.icon }}</mat-icon>
          <span class="flex flex-col leading-tight">
            <span>{{ link.label }}</span>
            <span class="text-[10px] font-normal opacity-70">{{ link.labelBn }}</span>
          </span>
        </a>
      </ng-container>
    </nav>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeedingSubnavComponent {
  readonly links: FeedingNavLink[] = [
    { path: '/feeding/today', icon: 'today', label: 'Today', labelBn: 'আজকের খাবার' },
    { path: '/feeding/board', icon: 'grid_view', label: 'Who Eats What', labelBn: 'কে কী খায়' },
    { path: '/feeding/planner', icon: 'shopping_cart', label: 'Purchase Plan', labelBn: 'ক্রয় পরিকল্পনা' },
    { path: '/feeding/nutrition', icon: 'monitor_heart', label: 'Nutrition & Growth', labelBn: 'পুষ্টি ও বৃদ্ধি' },
    { path: '/feeding/rules', icon: 'stacked_line_chart', label: 'Feed Charts', labelBn: 'খাদ্য তালিকা' },
    { path: '/feeding/recipes', icon: 'blender', label: 'Recipes', labelBn: 'রেসিপি' },
    { path: '/feeding/dashboard', icon: 'insights', label: 'Overview', labelBn: 'সারসংক্ষেপ' },
    { path: '/feeding/logs', icon: 'history', label: 'Feed Records', labelBn: 'খাবারের রেকর্ড' },
    { path: '/feeding/reconciliations', icon: 'fact_check', label: 'Reconciliation', labelBn: 'মিলকরণ' },
  ];
}
