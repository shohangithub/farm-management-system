import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

interface FeedingNavLink {
  path: string;
  icon: string;
  label: string;
  labelBn: string;
  /** What the page is for, shown on hover. */
  hint: string;
}

interface FeedingNavGroup {
  title: string;
  titleBn: string;
  links: FeedingNavLink[];
}

/**
 * The Feeding menu in three labelled groups, so each page is easy to find:
 * 1. Daily work: record and confirm what was fed.
 * 2. Plans & setup: who gets which ration, the feed charts and the recipes.
 * 3. Reports & records: what was fed, what it cost, what to buy.
 */
@Component({
  selector: 'app-feeding-subnav',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, MatIconModule, MatTooltipModule],
  template: `
    <nav aria-label="Feeding menu"
         class="mb-6 rounded-2xl border border-gray-200 dark:border-gray-700/60 bg-white dark:bg-gray-900/40 shadow-xs print:hidden
                grid grid-cols-1 md:grid-cols-3 divide-y md:divide-y-0 md:divide-x divide-gray-100 dark:divide-gray-800">
      <section *ngFor="let group of groups" class="p-3 min-w-0">
        <h2 class="m-0 mb-2 px-1 flex items-baseline gap-1.5 text-[11px] font-bold uppercase tracking-wider text-gray-400 dark:text-gray-500">
          {{ group.title }}
          <span class="normal-case tracking-normal font-medium text-gray-400/80 dark:text-gray-500/80">{{ group.titleBn }}</span>
        </h2>
        <div class="flex flex-wrap gap-1.5">
          <a *ngFor="let link of group.links"
             [routerLink]="link.path"
             [matTooltip]="link.labelBn + ' — ' + link.hint"
             matTooltipShowDelay="300"
             routerLinkActive="!bg-emerald-600 !text-white !border-emerald-600 dark:!bg-emerald-600 dark:!text-white shadow-sm"
             class="h-8 px-2.5 rounded-lg text-xs font-semibold inline-flex items-center gap-1.5 whitespace-nowrap transition-colors
                    text-gray-700 dark:text-gray-300 bg-gray-50 dark:bg-gray-800/60 border border-gray-200/80 dark:border-gray-700/60
                    hover:bg-emerald-50 hover:text-emerald-700 hover:border-emerald-200 dark:hover:bg-emerald-950/40 dark:hover:text-emerald-300">
            <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">{{ link.icon }}</mat-icon>
            {{ link.label }}
          </a>
        </div>
      </section>
    </nav>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeedingSubnavComponent {
  readonly groups: FeedingNavGroup[] = [
    {
      title: 'Daily work',
      titleBn: 'দৈনিক কাজ',
      links: [
        { path: '/feeding/sheet', icon: 'edit_calendar', label: 'Record Daily Feed', labelBn: 'দৈনিক খাবার লিখুন',
          hint: 'Daily Feed Sheet: pick animals, enter any feeds/recipes and amounts given today. No plan needed.' },
        { path: '/feeding/today', icon: 'today', label: 'Confirm Planned Feed', labelBn: 'পরিকল্পিত খাবার নিশ্চিত',
          hint: 'Entries created automatically from feeding plans. Confirm, adjust or skip them.' },
        { path: '/feeding/routine', icon: 'checklist', label: 'Feeding Routine', labelBn: 'খাওয়ানোর রুটিন',
          hint: 'Morning / noon / evening checklist of what to feed each shed.' },
      ]
    },
    {
      title: 'Plans & setup',
      titleBn: 'পরিকল্পনা ও সেটআপ',
      links: [
        { path: '/feeding/board', icon: 'grid_view', label: 'Feeding Plans', labelBn: 'খাওয়ানোর পরিকল্পনা',
          hint: 'Who eats what: give a feed chart to an animal, shed or batch, and pause or stop plans.' },
        { path: '/feeding/plans', icon: 'event_note', label: 'Plan History', labelBn: 'পরিকল্পনার ইতিহাস',
          hint: 'Every feeding plan, including stopped and finished ones.' },
        { path: '/feeding/rules', icon: 'stacked_line_chart', label: 'Feed Charts', labelBn: 'খাদ্য তালিকা',
          hint: 'How much to feed by weight or age (rations).' },
        { path: '/feeding/recipes', icon: 'blender', label: 'Recipes & Ingredients', labelBn: 'রেসিপি ও উপকরণ',
          hint: 'Feed mixes (formulas) and their ingredients.' },
      ]
    },
    {
      title: 'Reports & records',
      titleBn: 'রিপোর্ট ও রেকর্ড',
      links: [
        { path: '/feeding/dashboard', icon: 'insights', label: 'Feeding Overview', labelBn: 'সারসংক্ষেপ',
          hint: 'Feed used and cost at a glance.' },
        { path: '/feeding/logs', icon: 'history', label: 'Old Feed Records', labelBn: 'পুরনো খাবারের রেকর্ড',
          hint: 'Older shed-level feed records (read only).' },
        { path: '/feeding/nutrition', icon: 'monitor_heart', label: 'Nutrition & Growth', labelBn: 'পুষ্টি ও বৃদ্ধি',
          hint: 'Protein, energy and dry matter an animal ate vs. its needs, and its growth.' },
        { path: '/feeding/planner', icon: 'shopping_cart', label: 'Feed Purchase Plan', labelBn: 'খাদ্য ক্রয় পরিকল্পনা',
          hint: 'What feed to buy for the next 7 / 10 / 15 / 30 days.' },
        { path: '/feeding/reconciliations', icon: 'fact_check', label: 'Stock Check', labelBn: 'মজুদ মিলকরণ',
          hint: 'Reconciliation: compare planned feed with stock actually used.' },
      ]
    },
  ];
}
