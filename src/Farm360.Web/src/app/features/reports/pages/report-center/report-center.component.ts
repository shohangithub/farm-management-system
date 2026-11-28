import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ReportService } from '../../services/report.service';
import { CATEGORY_ICONS, DATE_RANGE_PRESETS, ReportCatalogItem, ReportCategory, ReportLanguage, ReportRunHistoryItem } from '../../models/report.models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

/**
 * The Report Center: every report in the product, grouped by module.
 *
 * Aligned with Farm360 UI Architecture:
 * - Uses <app-page-header> with breadcrumb and search action
 * - Cards sit directly on the page (no card-in-card)
 * - Uses <app-loading> overlay and <app-empty-state>
 * - High-contrast text colors: text-gray-900 dark:text-white for headers,
 *   text-gray-600 dark:text-gray-300 for descriptions, vibrant emerald accents
 * - Plain grid cards; the section header names the category
 */
@Component({
  selector: 'app-report-center',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-4 sm:p-6 max-w-7xl mx-auto space-y-6">
      <app-page-header
        [title]="'Reports' | t"
        [description]="'Open, print or download any report.' | t"
        [breadcrumbActiveNode]="'Reports' | t">
        <div actions class="flex items-center gap-3 w-full sm:w-auto">
          <div class="relative w-full sm:w-80">
            <mat-icon class="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 !text-[18px] !w-[18px] !h-[18px]">search</mat-icon>
            <input
              type="text"
              [placeholder]="'Search reports...' | t"
              [ngModel]="query()"
              (ngModelChange)="query.set($event)"
              class="w-full pl-10 pr-9 min-h-[44px] py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl text-gray-900 dark:text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all shadow-xs"
            />
            <button
              *ngIf="query()"
              (click)="query.set('')"
              class="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 flex items-center p-0.5 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700"
              [title]="'Clear search' | t"
            >
              <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">close</mat-icon>
            </button>
          </div>
        </div>
      </app-page-header>

      <!-- Category filter -->
      @if (!loading() && !error() && catalog().length > 0) {
        <div class="flex items-center gap-2 overflow-x-auto sm:flex-wrap -mx-4 px-4 sm:mx-0 sm:px-0 pb-1" role="group" [attr.aria-label]="'Filter by category' | t">
          <button type="button" (click)="category.set(null)"
                  [class]="chipClass(category() === null)">{{ 'All ({n})' | t: { n: catalog().length } }}</button>
          @for (c of categories(); track c.category) {
            <button type="button" (click)="category.set(c.category)"
                    [class]="chipClass(category() === c.category)">
              <mat-icon class="!text-[14px] !w-[14px] !h-[14px] align-middle mr-1">{{ icon(c.category) }}</mat-icon>{{ c.category | t }} ({{ c.count }})
            </button>
          }
          <button type="button" (click)="onlyFavourites.set(!onlyFavourites())"
                  [class]="chipClass(onlyFavourites())">
            <mat-icon class="!text-[14px] !w-[14px] !h-[14px] align-middle mr-1">star</mat-icon>{{ 'Favourites ({n})' | t: { n: favourites().length } }}
          </button>
        </div>
      }

      <!-- Recently opened -->
      @if (!loading() && !error() && recent().length > 0 && !query() && !category() && !onlyFavourites()) {
        <div class="flex flex-wrap items-center gap-2">
          <span class="text-[11px] font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400 mr-1">{{ 'Recently opened' | t }}</span>
          @for (r of recent(); track r.key) {
            <a [routerLink]="['/reports', r.key]"
               class="text-xs px-3 py-1 rounded-full bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 no-underline hover:underline">
              {{ reportTitle(r) }}
            </a>
          }
        </div>
      }

      <!-- Catalog -->
      <div class="relative min-h-[400px]">
        <app-loading *ngIf="loading()" [overlay]="true"></app-loading>

        <!-- Error State -->
        <div *ngIf="error()" class="flex flex-col items-center justify-center p-12 text-center bg-white dark:bg-gray-800/80 rounded-2xl border border-gray-200/70 dark:border-gray-700/60 shadow-sm">
          <div class="w-12 h-12 rounded-full bg-red-50 dark:bg-red-950/40 text-red-600 dark:text-red-400 flex items-center justify-center mb-3">
            <mat-icon class="!text-[24px] !w-[24px] !h-[24px]">error_outline</mat-icon>
          </div>
          <p class="text-sm font-semibold text-gray-900 dark:text-white mb-1">{{ 'Failed to Load Reports' | t }}</p>
          <p class="text-xs text-gray-500 dark:text-gray-400 max-w-sm mb-4">{{ error() }}</p>
          <button
            mat-stroked-button
            (click)="load()"
            class="!rounded-xl !border-gray-200 dark:!border-gray-700 !text-gray-700 dark:!text-gray-200 hover:!border-emerald-500"
          >
            <mat-icon class="mr-1.5">refresh</mat-icon> {{ 'Retry' | t }}
          </button>
        </div>

        <!-- Empty State -->
        <app-empty-state
          *ngIf="!loading() && !error() && !filtered().length"
          icon="search_off"
          [title]="'No Reports Found' | t"
          [description]="emptyMessage()"
          [actionLabel]="query() || category() || onlyFavourites() ? ('Clear filters' | t) : undefined"
          (action)="clearFilters()"
        >
        </app-empty-state>

        <!-- Grouped Reports Catalog -->
        <div *ngIf="!loading() && !error() && filtered().length > 0" class="space-y-8">
          @for (group of grouped(); track group.category) {
            <section class="space-y-4">
              <!-- Category Header -->
              <div class="flex items-center gap-2.5">
                <div class="w-7 h-7 rounded-lg bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                  <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">{{ icon(group.category) }}</mat-icon>
                </div>
                <h2 class="text-xs font-bold uppercase tracking-wider text-gray-800 dark:text-gray-200 m-0">
                  {{ group.category | t }}
                </h2>
                <span class="text-[11px] font-semibold text-gray-500 dark:text-gray-400 bg-gray-100 dark:bg-gray-800 px-2 py-0.5 rounded-full">
                  {{ group.reports.length }}
                </span>
              </div>

              <!-- Cards Grid -->
              <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                @for (report of group.reports; track report.key) {
                  <a
                    [routerLink]="['/reports', report.key]"
                    class="group relative bg-white dark:bg-gray-800/80 rounded-2xl border border-gray-200/70 dark:border-gray-700/60 shadow-sm hover:shadow-md hover:border-emerald-500/40 dark:hover:border-emerald-500/40 transition-all duration-200 overflow-hidden flex flex-col p-5 no-underline"
                  >
                    <!-- Header & Badge -->
                    <div class="flex items-start gap-3.5 mb-3 relative z-10">
                      <div class="w-10 h-10 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0">
                        <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">{{ icon(group.category) }}</mat-icon>
                      </div>
                      <button type="button"
                              class="order-last shrink-0 w-10 h-10 -mt-2 -mr-2 flex items-center justify-center rounded-full hover:bg-amber-50 dark:hover:bg-amber-950/40"
                              [attr.aria-label]="(isFavourite(report.key) ? 'Remove from favourites' : 'Add to favourites') | t"
                              [attr.aria-pressed]="isFavourite(report.key)"
                              (click)="toggleFavourite($event, report.key)">
                        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]"
                                  [class.text-amber-500]="isFavourite(report.key)"
                                  [class.text-gray-300]="!isFavourite(report.key)">
                          {{ isFavourite(report.key) ? 'star' : 'star_border' }}
                        </mat-icon>
                      </button>
                      <div class="min-w-0 flex-1">
                        <h3 class="font-bold text-gray-900 dark:text-white text-sm sm:text-base leading-snug m-0 group-hover:text-emerald-600 dark:group-hover:text-emerald-400 transition-colors">
                          {{ reportTitle(report) }}
                        </h3>
                        @if (secondaryTitle(report); as second) {
                          <p class="text-xs font-medium text-gray-500 dark:text-gray-400 m-0 mt-0.5">
                            {{ second }}
                          </p>
                        }
                      </div>
                    </div>

                    <!-- Description -->
                    <p class="text-xs text-gray-600 dark:text-gray-300 leading-relaxed mb-4 flex-1 relative z-10 m-0 line-clamp-3" [title]="reportDescription(report)">
                      {{ reportDescription(report) }}
                    </p>

                    <!-- Footer Action -->
                    <div class="flex items-center justify-end mt-auto relative z-10">
                      <span class="inline-flex items-center gap-1 text-xs font-semibold text-emerald-600 dark:text-emerald-400">
                        {{ 'Open Report' | t }}
                        <mat-icon class="!text-[15px] !w-[15px] !h-[15px]">arrow_forward</mat-icon>
                      </span>
                    </div>
                  </a>
                }
              </div>
            </section>
          }
        </div>
      </div>

      <!-- Recent runs (audit history) with "run again" -->
      @if (!loading() && !error()) {
        <div class="bg-white dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-200/70 dark:border-gray-700/60 p-5 sm:p-6">
          <div class="flex items-center gap-2 mb-3">
            <mat-icon class="text-emerald-600 !text-[18px] !w-[18px] !h-[18px]">history</mat-icon>
            <h2 class="text-xs font-bold uppercase tracking-wider text-gray-800 dark:text-gray-200 m-0">{{ 'Recent runs' | t }}</h2>
            <span class="flex-1"></span>
            <button mat-button type="button" (click)="loadRuns()" class="!text-xs">
              <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">refresh</mat-icon> {{ 'Refresh' | t }}
            </button>
          </div>

          @if (runsError()) {
            <p class="text-xs text-red-600 dark:text-red-400 m-0">{{ runsError() }}</p>
          } @else if (!runs().length) {
            <p class="text-xs text-gray-500 dark:text-gray-400 m-0">{{ 'No reports have been run yet. Runs appear here with who ran them, when, and with which parameters.' | t }}</p>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full text-xs text-left">
                <thead class="text-[10px] uppercase tracking-wider text-gray-500 dark:text-gray-400">
                  <tr>
                    <th class="py-2 pr-3">{{ 'Report' | t }}</th>
                    <th class="py-2 pr-3">{{ 'Run by' | t }}</th>
                    <th class="py-2 pr-3">{{ 'When' | t }}</th>
                    <th class="py-2 pr-3">{{ 'Parameters' | t }}</th>
                    <th class="py-2 pr-3">{{ 'Format' | t }}</th>
                    <th class="py-2 pr-3">{{ 'Result' | t }}</th>
                    <th class="py-2"></th>
                  </tr>
                </thead>
                <tbody class="text-gray-800 dark:text-gray-200">
                  @for (run of runs(); track run.id) {
                    <tr class="border-t border-gray-100 dark:border-gray-700/60 align-top">
                      <td class="py-2 pr-3 font-semibold">{{ runTitle(run) }}</td>
                      <td class="py-2 pr-3">{{ run.requestedByName }}</td>
                      <td class="py-2 pr-3 whitespace-nowrap">{{ run.requestedAtUtc | date: 'dd-MMM-yyyy HH:mm' }}</td>
                      <td class="py-2 pr-3 text-gray-600 dark:text-gray-300 max-w-[320px]">{{ describeParameters(run) }}</td>
                      <td class="py-2 pr-3">{{ run.format === 'Screen' ? ('On screen' | t) : run.format.toUpperCase() }}</td>
                      <td class="py-2 pr-3">
                        @if (run.outcome === 'Succeeded') {
                          <span class="text-emerald-700 dark:text-emerald-400">{{ '{n} rows' | t: { n: run.rowCount } }}</span>
                        } @else {
                          <span class="text-red-600 dark:text-red-400" [title]="run.failureReason ?? ''">{{ 'Failed' | t }}</span>
                        }
                      </td>
                      <td class="py-2 text-right">
                        <button mat-stroked-button type="button" class="!text-xs !rounded-lg" (click)="runAgain(run)"
                                [disabled]="!canOpen(run.reportKey)">
                          <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">replay</mat-icon> {{ 'Run again' | t }}
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class ReportCenterComponent implements OnInit {
  private readonly reports = inject(ReportService);
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);

  protected readonly query = signal('');
  protected readonly category = signal<ReportCategory | null>(null);
  protected readonly onlyFavourites = signal(false);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly catalog = signal<ReportCatalogItem[]>([]);
  protected readonly favourites = signal<string[]>(this.reports.favourites());
  protected readonly runs = signal<ReportRunHistoryItem[]>([]);
  protected readonly runsError = signal<string | null>(null);
  private readonly recentKeys = signal<string[]>(this.reports.recentlyOpened());

  protected readonly categories = computed(() => {
    const counts = new Map<ReportCategory, number>();
    for (const r of this.catalog()) {
      counts.set(r.category, (counts.get(r.category) ?? 0) + 1);
    }
    return [...counts.entries()].map(([category, count]) => ({ category, count }));
  });

  protected readonly recent = computed(() =>
    this.recentKeys()
      .map(k => this.catalog().find(r => r.key === k))
      .filter((r): r is ReportCatalogItem => !!r)
      .slice(0, 6));

  protected readonly filtered = computed(() => {
    const term = this.query().trim().toLowerCase();
    const category = this.category();
    const favourites = this.onlyFavourites() ? new Set(this.favourites()) : null;

    return this.catalog().filter(r =>
      (!category || r.category === category) &&
      (!favourites || favourites.has(r.key)) &&
      (!term ||
        r.title.toLowerCase().includes(term) ||
        r.titleBn.toLowerCase().includes(term) ||
        r.description.toLowerCase().includes(term) ||
        (r.descriptionBn ?? '').toLowerCase().includes(term) ||
        r.key.toLowerCase().includes(term)));
  });

  protected readonly emptyMessage = computed(() => {
    if (this.onlyFavourites() && !this.favourites().length) {
      return this.i18n.t('You have no favourite reports yet. Tap the star on a report to keep it here.');
    }
    if (this.query()) {
      return this.i18n.t('No report matches "{query}". Try a different search term.', { query: this.query() });
    }
    if (this.category() || this.onlyFavourites()) {
      return this.i18n.t('No reports match these filters.');
    }
    return this.i18n.t('No reports are available for your role. Ask the farm owner to grant report permissions.');
  });

  protected readonly grouped = computed(() => {
    const byCategory = new Map<ReportCategory, ReportCatalogItem[]>();

    for (const report of this.filtered()) {
      const bucket = byCategory.get(report.category) ?? [];
      bucket.push(report);
      byCategory.set(report.category, bucket);
    }

    // Favourites first within each category.
    const favourites = new Set(this.favourites());
    return [...byCategory.entries()].map(([category, reports]) => ({
      category,
      reports: [...reports].sort((a, b) => Number(favourites.has(b.key)) - Number(favourites.has(a.key))),
    }));
  });

  ngOnInit(): void {
    this.load();
    this.loadRuns();
  }

  protected loadRuns(): void {
    this.runsError.set(null);
    this.reports.getRuns(15).subscribe({
      next: runs => this.runs.set(runs),
      error: () => this.runsError.set(this.i18n.t('Recent runs could not be loaded.')),
    });
  }

  protected clearFilters(): void {
    this.query.set('');
    this.category.set(null);
    this.onlyFavourites.set(false);
  }

  protected chipClass(active: boolean): string {
    const base = 'shrink-0 whitespace-nowrap min-h-[36px] text-xs font-semibold px-3.5 py-1.5 rounded-full border transition-colors ';
    return base + (active
      ? 'bg-emerald-600 border-emerald-600 text-white'
      : 'bg-white dark:bg-gray-800 border-gray-200 dark:border-gray-700 text-gray-700 dark:text-gray-200 hover:border-emerald-500 dark:hover:border-emerald-500');
  }

  protected isFavourite(key: string): boolean {
    return this.favourites().includes(key);
  }

  protected toggleFavourite(event: Event, key: string): void {
    // The star sits inside the card link; starring must not also open the report.
    event.preventDefault();
    event.stopPropagation();
    this.favourites.set(this.reports.toggleFavourite(key));
  }

  protected canOpen(key: string): boolean {
    return this.catalog().some(r => r.key === key);
  }

  /** "When, with what": the run's own parameters in plain words. */
  protected describeParameters(run: ReportRunHistoryItem): string {
    const entries = Object.entries(run.parameters ?? {}).filter(([, v]) => v !== null && v !== '');
    if (!entries.length) {
      return '—';
    }
    const report = this.catalog().find(r => r.key === run.reportKey);
    return entries
      .map(([name, value]) => {
        const p = report?.parameters.find(x => x.name.toLowerCase() === name.toLowerCase());
        const bn = this.i18n.isBangla;
        const label = (bn ? p?.label.bn || p?.label.en : p?.label.en) ?? name;
        const isEntity = p && ['Animal', 'Batch', 'Farm', 'Shed', 'Breed', 'InventoryItem', 'MoneyAccount', 'Partner'].includes(p.type);
        // Date keywords ("today", "current-month") and ranges ("2026-11-01..2026-11-30") read as words.
        const optionLabel = p?.options?.find(o => o.value === value)?.label
          ?? DATE_RANGE_PRESETS.find(o => o.value === value)?.label;
        if (!optionLabel && typeof value === 'string' && value.includes('..')) {
          value = value.replace('..', ' – ');
        }
        const option = optionLabel ? (bn ? optionLabel.bn || optionLabel.en : optionLabel.en) : undefined;
        return `${label}: ${isEntity ? this.i18n.t('selected') : option ?? value}`;
      })
      .join(' · ');
  }

  /** Re-opens the report with exactly the parameters, language and numerals of a past run. */
  protected runAgain(run: ReportRunHistoryItem): void {
    const language: ReportLanguage = run.language === 'bn' ? 'bn' : 'en';
    this.router.navigate(['/reports', run.reportKey], {
      state: {
        rerun: {
          parameters: run.parameters ?? {},
          language,
          bengaliNumerals: run.bengaliNumerals === true,
        },
      },
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.reports.getCatalog().subscribe({
      next: items => {
        this.catalog.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(this.i18n.t('Could not load the report catalog.'));
        this.loading.set(false);
      },
    });
  }

  /** In Bangla the Bangla title leads; English stays as the small second line. */
  protected reportTitle(r: ReportCatalogItem): string {
    return this.i18n.isBangla && r.titleBn ? r.titleBn : r.title;
  }

  protected secondaryTitle(r: ReportCatalogItem): string | null {
    const second = this.i18n.isBangla ? r.title : r.titleBn;
    return second && second !== this.reportTitle(r) ? second : null;
  }

  protected reportDescription(r: ReportCatalogItem): string {
    return this.i18n.isBangla && r.descriptionBn ? r.descriptionBn : r.description;
  }

  protected runTitle(run: ReportRunHistoryItem): string {
    const report = this.catalog().find(r => r.key === run.reportKey);
    return report ? this.reportTitle(report) : run.reportTitle;
  }

  protected icon(category: ReportCategory): string {
    return CATEGORY_ICONS[category] ?? 'description';
  }
}
