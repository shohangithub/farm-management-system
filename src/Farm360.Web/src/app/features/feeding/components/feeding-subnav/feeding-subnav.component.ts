import { Component, ChangeDetectionStrategy, inject, ElementRef, AfterViewInit, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

interface FeedingNavLink {
  path: string;
  icon: string;
  label: string;
  /** What the page is for, shown on hover. */
  hint: string;
  /** Older page kept for reference: shown quieter than the rest. */
  legacy?: boolean;
}

interface FeedingNavGroup {
  title: string;
  links: FeedingNavLink[];
}

/**
 * The Feeding menu as one compact tab strip, in three groups:
 * 1. Daily work: record and confirm what was fed.
 * 2. Plans & setup: who gets which ration, the feed charts and the recipes.
 * 3. Reports & records: what was fed, what it cost, what to buy.
 * On phones it is a single row that scrolls sideways (the open page is scrolled into view);
 * on wide screens the groups wrap with a small label in front of each.
 */
@Component({
  selector: 'app-feeding-subnav',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, MatIconModule, MatTooltipModule, TranslatePipe],
  styles: [`
    .strip { scrollbar-width: none; -webkit-overflow-scrolling: touch; }
    .strip::-webkit-scrollbar { display: none; }
  `],
  template: `
    <nav [attr.aria-label]="'Feeding menu' | t" class="mb-6 print:hidden">
      <div #strip
           class="strip relative flex flex-nowrap items-center gap-x-1 gap-y-1.5 overflow-x-auto p-1.5
                  rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 shadow-xs
                  lg:flex-wrap lg:overflow-visible">
        <ng-container *ngFor="let group of groups; let first = first">
          <span *ngIf="!first" aria-hidden="true" class="shrink-0 w-px h-6 mx-1 bg-gray-200 dark:bg-gray-700 lg:hidden"></span>
          <div role="group" [attr.aria-label]="group.title | t"
               class="flex shrink-0 items-center gap-1 lg:pr-3 lg:mr-1 lg:border-r lg:last:border-r-0 border-gray-200 dark:border-gray-700">
            <span class="hidden lg:inline px-1.5 text-[10px] font-bold uppercase tracking-wider text-gray-400 dark:text-gray-500 whitespace-nowrap">
              {{ group.title | t }}
            </span>
            <a *ngFor="let link of group.links"
               [routerLink]="link.path"
               [matTooltip]="link.hint | t"
               matTooltipShowDelay="400"
               routerLinkActive="is-active !bg-primary-600 !text-white dark:!bg-primary-600 dark:!text-white shadow-sm"
               ariaCurrentWhenActive="page"
               [class.text-gray-700]="!link.legacy" [class.dark:text-gray-200]="!link.legacy"
               [class.text-gray-400]="link.legacy" [class.dark:text-gray-500]="link.legacy" [class.italic]="link.legacy"
               class="h-10 lg:h-8 px-3 lg:px-2.5 rounded-xl text-xs font-semibold inline-flex items-center gap-1.5 whitespace-nowrap transition-colors
                      hover:bg-primary-50 hover:text-primary-700 dark:hover:bg-primary-950/40 dark:hover:text-primary-300">
              <mat-icon class="!text-[16px] !w-[16px] !h-[16px] lg:hidden xl:inline-block">{{ link.icon }}</mat-icon>
              {{ link.label | t }}
            </a>
          </div>
        </ng-container>
      </div>
    </nav>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeedingSubnavComponent implements AfterViewInit {
  protected readonly i18n = inject(I18nService);
  private readonly strip = viewChild<ElementRef<HTMLElement>>('strip');

  readonly groups: FeedingNavGroup[] = [
    {
      title: 'Daily work',
      links: [
        { path: '/feeding/sheet', icon: 'edit_calendar', label: 'Record Daily Feed',
          hint: 'Daily Feed Sheet: pick animals, enter any feeds/recipes and amounts given today. No plan needed.' },
        { path: '/feeding/today', icon: 'today', label: 'Confirm Planned Feed',
          hint: 'Entries created automatically from feeding plans. Confirm, adjust or skip them.' },
        { path: '/feeding/routine', icon: 'checklist', label: 'Feeding Routine',
          hint: 'Morning / noon / evening checklist of what to feed each shed.' },
      ]
    },
    {
      title: 'Plans & setup',
      links: [
        { path: '/feeding/board', icon: 'grid_view', label: 'Feeding Plans',
          hint: 'Who eats what: give a feed chart to an animal, shed or batch, and pause or stop plans.' },
        { path: '/feeding/rules', icon: 'stacked_line_chart', label: 'Feed Charts',
          hint: 'How much to feed by weight or age (rations).' },
        { path: '/feeding/recipes', icon: 'blender', label: 'Recipes & Ingredients',
          hint: 'Feed mixes (formulas) and their ingredients.' },
        { path: '/feeding/plans', icon: 'event_note', label: 'Plan History',
          hint: 'Every feeding plan, including stopped and finished ones.' },
      ]
    },
    {
      title: 'Reports & records',
      links: [
        { path: '/feeding/dashboard', icon: 'insights', label: 'Feeding Overview',
          hint: 'Feed used and cost at a glance.' },
        { path: '/feeding/nutrition', icon: 'monitor_heart', label: 'Nutrition & Growth',
          hint: 'Protein, energy and dry matter an animal ate vs. its needs, and its growth.' },
        { path: '/feeding/planner', icon: 'shopping_cart', label: 'Feed Purchase Plan',
          hint: 'What feed to buy for the next 7 / 10 / 15 / 30 days.' },
        { path: '/feeding/reconciliations', icon: 'fact_check', label: 'Stock Check',
          hint: 'Reconciliation: compare planned feed with stock actually used.' },
      ]
    },
  ];

  ngAfterViewInit(): void {
    // On phones the strip scrolls sideways: bring the open page's tab into view without moving the page.
    setTimeout(() => {
      const strip = this.strip()?.nativeElement;
      const active = strip?.querySelector<HTMLElement>('a.is-active');
      if (!strip || !active || strip.scrollWidth <= strip.clientWidth) return;
      strip.scrollLeft = Math.max(0, active.offsetLeft - (strip.clientWidth - active.offsetWidth) / 2);
    });
  }
}
