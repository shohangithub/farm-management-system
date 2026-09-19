import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ReportService } from '../../services/report.service';
import { CATEGORY_ICONS, ReportCatalogItem, ReportCategory } from '../../models/report.models';

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
          [description]="query() ? 'No report matches &quot;' + query() + '&quot;. Try a different search term.' : 'No reports are currently available.'"
          [actionLabel]="query() ? 'Clear Search' : undefined"
          (action)="query.set('')"
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
    </div>
  `
})
export class ReportCenterComponent implements OnInit {
  private readonly reports = inject(ReportService);

  protected readonly query = signal('');
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  private readonly catalog = signal<ReportCatalogItem[]>([]);

  protected readonly filtered = computed(() => {
    const term = this.query().trim().toLowerCase();
    if (!term) {
      return this.catalog();
    }

    return this.catalog().filter(r =>
      r.title.toLowerCase().includes(term) ||
      r.titleBn.toLowerCase().includes(term) ||
      r.description.toLowerCase().includes(term) ||
      r.key.toLowerCase().includes(term));
  });

  protected readonly grouped = computed(() => {
    const byCategory = new Map<ReportCategory, ReportCatalogItem[]>();

    for (const report of this.filtered()) {
      const bucket = byCategory.get(report.category) ?? [];
      bucket.push(report);
      byCategory.set(report.category, bucket);
    }

    return [...byCategory.entries()].map(([category, reports]) => ({ category, reports }));
  });

  ngOnInit(): void {
    this.load();
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
