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
import { CATEGORY_ICONS, ReportCatalogItem, ReportCategory, ReportLanguage, ReportRunHistoryItem } from '../../models/report.models';

/**
 * The Report Center: every report in the product, grouped by module.
 *
 * Aligned with Farm360 UI Architecture:
 * - Uses <app-page-header> with breadcrumb and search action
 * - Uses standard glassmorphic card container
 * - Uses <app-loading> overlay and <app-empty-state>
 * - High-contrast text colors: text-gray-900 dark:text-white for headers,
 *   text-gray-600 dark:text-gray-300 for descriptions, vibrant emerald accents
 * - Grid cards with gradient badges and watermark icon
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
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-4 sm:p-6 max-w-7xl mx-auto space-y-6">
      <app-page-header
        title="Reports Center"
        description="Printable, exportable analytical and operational reports across every module."
        breadcrumbActiveNode="Reports">
        <div actions class="flex items-center gap-3">
          <div class="relative w-64 sm:w-80">
            <mat-icon class="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 !text-[18px] !w-[18px] !h-[18px]">search</mat-icon>
            <input
              type="text"
              placeholder="Search reports..."
              [ngModel]="query()"
              (ngModelChange)="query.set($event)"
              class="w-full pl-10 pr-9 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl text-gray-900 dark:text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all shadow-xs"
            />
            <button
              *ngIf="query()"
              (click)="query.set('')"
              class="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 flex items-center p-0.5 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700"
              title="Clear search"
            >
              <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">close</mat-icon>
            </button>
          </div>
        </div>
      </app-page-header>

      <!-- Category filter -->
      @if (!loading() && !error() && catalog().length > 0) {
        <div class="flex flex-wrap items-center gap-2" role="group" aria-label="Filter by category">
          <button type="button" (click)="category.set(null)"
                  [class]="chipClass(category() === null)">All ({{ catalog().length }})</button>
          @for (c of categories(); track c.category) {
            <button type="button" (click)="category.set(c.category)"
                    [class]="chipClass(category() === c.category)">
              <mat-icon class="!text-[14px] !w-[14px] !h-[14px] align-middle mr-1">{{ icon(c.category) }}</mat-icon>{{ c.category }} ({{ c.count }})
            </button>
          }
          <button type="button" (click)="onlyFavourites.set(!onlyFavourites())"
                  [class]="chipClass(onlyFavourites())">
            <mat-icon class="!text-[14px] !w-[14px] !h-[14px] align-middle mr-1">star</mat-icon>Favourites ({{ favourites().length }})
          </button>
        </div>
      }

      <!-- Recently opened -->
      @if (!loading() && !error() && recent().length > 0 && !query() && !category() && !onlyFavourites()) {
        <div class="flex flex-wrap items-center gap-2">
          <span class="text-[11px] font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400 mr-1">Recently opened</span>
          @for (r of recent(); track r.key) {
            <a [routerLink]="['/reports', r.key]"
               class="text-xs px-3 py-1 rounded-full bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 no-underline hover:underline">
              {{ r.title }}
            </a>
          }
        </div>
      }

      <!-- Glassmorphic Main Container -->
      <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative p-5 sm:p-6 min-h-[400px]">
        <app-loading *ngIf="loading()" [overlay]="true"></app-loading>

        <!-- Error State -->
        <div *ngIf="error()" class="flex flex-col items-center justify-center p-12 text-center">
          <div class="w-12 h-12 rounded-full bg-red-50 dark:bg-red-950/40 text-red-600 dark:text-red-400 flex items-center justify-center mb-3">
            <mat-icon class="!text-[24px] !w-[24px] !h-[24px]">error_outline</mat-icon>
          </div>
          <p class="text-sm font-semibold text-gray-900 dark:text-white mb-1">Failed to Load Reports</p>
          <p class="text-xs text-gray-500 dark:text-gray-400 max-w-sm mb-4">{{ error() }}</p>
          <button
            mat-stroked-button
            (click)="load()"
            class="!rounded-xl !border-gray-200 dark:!border-gray-700 !text-gray-700 dark:!text-gray-200 hover:!border-emerald-500"
          >
            <mat-icon class="mr-1.5">refresh</mat-icon> Retry
          </button>
        </div>

        <!-- Empty State -->
        <app-empty-state
          *ngIf="!loading() && !error() && !filtered().length"
          icon="search_off"
          title="No Reports Found"
          [description]="emptyMessage()"
          [actionLabel]="query() || category() || onlyFavourites() ? 'Clear filters' : undefined"
          (action)="clearFilters()"
        >
        </app-empty-state>

        <!-- Grouped Reports Catalog -->
        <div *ngIf="!loading() && !error() && filtered().length > 0" class="space-y-8">
          @for (group of grouped(); track group.category) {
            <section class="space-y-4">
              <!-- Category Header -->
              <div class="flex items-center gap-2.5 pb-2.5 border-b border-gray-100 dark:border-gray-700/60">
                <div class="w-7 h-7 rounded-lg bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                  <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">{{ icon(group.category) }}</mat-icon>
                </div>
                <h2 class="text-xs font-bold uppercase tracking-wider text-gray-800 dark:text-gray-200 m-0">
                  {{ group.category }}
                </h2>
                <span class="text-[11px] font-semibold text-gray-500 dark:text-gray-400 bg-gray-100 dark:bg-gray-800 px-2 py-0.5 rounded-full">
                  {{ group.reports.length }}
                </span>
              </div>

              <!-- Cards Grid -->
              <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
                @for (report of group.reports; track report.key) {
                  <a
                    [routerLink]="['/reports', report.key]"
                    class="group relative bg-white dark:bg-gray-900 rounded-2xl border border-gray-100 dark:border-gray-800 shadow-sm hover:shadow-xl hover:border-emerald-500/40 dark:hover:border-emerald-500/40 transition-all duration-300 overflow-hidden transform hover:-translate-y-1 flex flex-col p-5 no-underline"
                  >
                    <!-- Background Watermark -->
                    <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-emerald-500/5 dark:text-emerald-400/5 rotate-[-10deg] pointer-events-none transition-transform duration-500 group-hover:scale-110">
                      {{ icon(group.category) }}
                    </mat-icon>

                    <!-- Header & Badge -->
                    <div class="flex items-start gap-3.5 mb-3 relative z-10">
                      <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20 shrink-0 group-hover:scale-105 transition-transform duration-300">
                        <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">{{ icon(group.category) }}</mat-icon>
                      </div>
                      <button type="button"
                              class="order-last shrink-0 p-1 rounded-full hover:bg-amber-50 dark:hover:bg-amber-950/40"
                              [attr.aria-label]="isFavourite(report.key) ? 'Remove from favourites' : 'Add to favourites'"
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
                          {{ report.title }}
                        </h3>
                        @if (report.titleBn && report.titleBn !== report.title) {
                          <p class="text-xs font-semibold text-emerald-600 dark:text-emerald-400 m-0 mt-0.5">
                            {{ report.titleBn }}
                          </p>
                        }
                      </div>
                    </div>

                    <!-- Description -->
                    <p class="text-xs text-gray-600 dark:text-gray-300 leading-relaxed mb-4 flex-1 relative z-10 m-0">
                      {{ report.description }}
                    </p>

                    <!-- Footer Action -->
                    <div class="pt-3 border-t border-gray-50 dark:border-gray-800/80 flex items-center justify-between mt-auto relative z-10">
                      <span class="text-[10px] font-bold uppercase tracking-wider text-gray-400 dark:text-gray-500">
                        {{ group.category }}
                      </span>
                      <span class="inline-flex items-center gap-1 text-xs font-semibold text-emerald-600 dark:text-emerald-400 group-hover:translate-x-1 transition-transform">
                        Open Report
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
        <div class="bg-white/80 dark:bg-gray-800/80 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 sm:p-6">
          <div class="flex items-center gap-2 mb-3">
            <mat-icon class="text-emerald-600 !text-[18px] !w-[18px] !h-[18px]">history</mat-icon>
            <h2 class="text-xs font-bold uppercase tracking-wider text-gray-800 dark:text-gray-200 m-0">Recent runs</h2>
            <span class="flex-1"></span>
            <button mat-button type="button" (click)="loadRuns()" class="!text-xs">
              <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">refresh</mat-icon> Refresh
            </button>
          </div>

          @if (runsError()) {
            <p class="text-xs text-red-600 dark:text-red-400 m-0">{{ runsError() }}</p>
          } @else if (!runs().length) {
            <p class="text-xs text-gray-500 dark:text-gray-400 m-0">No reports have been run yet. Runs appear here with who ran them, when, and with which parameters.</p>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full text-xs text-left">
                <thead class="text-[10px] uppercase tracking-wider text-gray-500 dark:text-gray-400">
                  <tr>
                    <th class="py-2 pr-3">Report</th>
                    <th class="py-2 pr-3">Run by</th>
                    <th class="py-2 pr-3">When</th>
                    <th class="py-2 pr-3">Parameters</th>
                    <th class="py-2 pr-3">Format</th>
                    <th class="py-2 pr-3">Result</th>
                    <th class="py-2"></th>
                  </tr>
                </thead>
                <tbody class="text-gray-800 dark:text-gray-200">
                  @for (run of runs(); track run.id) {
                    <tr class="border-t border-gray-100 dark:border-gray-700/60 align-top">
                      <td class="py-2 pr-3 font-semibold">{{ run.reportTitle }}</td>
                      <td class="py-2 pr-3">{{ run.requestedByName }}</td>
                      <td class="py-2 pr-3 whitespace-nowrap">{{ run.requestedAtUtc | date: 'dd-MMM-yyyy HH:mm' }}</td>
                      <td class="py-2 pr-3 text-gray-600 dark:text-gray-300 max-w-[320px]">{{ describeParameters(run) }}</td>
                      <td class="py-2 pr-3">{{ run.format === 'Screen' ? 'On screen' : run.format.toUpperCase() }}</td>
                      <td class="py-2 pr-3">
                        @if (run.outcome === 'Succeeded') {
                          <span class="text-emerald-700 dark:text-emerald-400">{{ run.rowCount }} rows</span>
                        } @else {
                          <span class="text-red-600 dark:text-red-400" [title]="run.failureReason ?? ''">Failed</span>
                        }
                      </td>
                      <td class="py-2 text-right">
                        <button mat-stroked-button type="button" class="!text-xs !rounded-lg" (click)="runAgain(run)"
                                [disabled]="!canOpen(run.reportKey)">
                          <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">replay</mat-icon> Run again
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
      return 'You have no favourite reports yet. Tap the star on a report to keep it here.';
    }
    if (this.query()) {
      return `No report matches "${this.query()}". Try a different search term.`;
    }
    if (this.category() || this.onlyFavourites()) {
      return 'No reports match these filters.';
    }
    return 'No reports are available for your role. Ask the farm owner to grant report permissions.';
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
      error: () => this.runsError.set('Recent runs could not be loaded.'),
    });
  }

  protected clearFilters(): void {
    this.query.set('');
    this.category.set(null);
    this.onlyFavourites.set(false);
  }

  protected chipClass(active: boolean): string {
    const base = 'text-xs font-semibold px-3 py-1.5 rounded-full border transition-colors ';
    return base + (active
      ? 'bg-emerald-600 border-emerald-600 text-white'
      : 'bg-white dark:bg-gray-800 border-gray-200 dark:border-gray-700 text-gray-700 dark:text-gray-200 hover:border-emerald-500');
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
        const label = p?.label.en ?? name;
        const isEntity = p && ['Animal', 'Batch', 'Farm', 'Shed', 'Breed', 'InventoryItem'].includes(p.type);
        const option = p?.options?.find(o => o.value === value)?.label.en;
        return `${label}: ${isEntity ? 'selected' : option ?? value}`;
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
        this.error.set('Could not load the report catalog.');
        this.loading.set(false);
      },
    });
  }

  protected icon(category: ReportCategory): string {
    return CATEGORY_ICONS[category] ?? 'description';
  }
}
