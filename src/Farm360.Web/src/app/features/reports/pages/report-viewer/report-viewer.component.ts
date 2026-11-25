import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { ReportRerun, ReportService } from '../../services/report.service';
import { ReportPaperComponent } from '../../components/report-paper/report-paper.component';
import { ReportParameterPanelComponent } from '../../components/report-parameter-panel/report-parameter-panel.component';
import {
  ReportCatalogItem,
  ReportDataSet,
  ReportExportFormat,
  ReportLanguage,
  RunReportRequest,
} from '../../models/report.models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

/**
 * Runs one report and shows it as an A4 sheet, with print and export.
 *
 * Parameters on the left, paper on the right — the layout of every reporting tool the accounts
 * office already knows. The sheet is rendered from the server's dataset, so it matches the PDF.
 */
@Component({
  selector: 'app-report-viewer',
  standalone: true,
  imports: [
    CommonModule, RouterLink, MatButtonModule, MatIconModule, MatMenuModule,
    MatProgressBarModule, MatSidenavModule, ReportPaperComponent, ReportParameterPanelComponent,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="viewer">
      <aside class="params no-print">
        <a mat-button routerLink="/reports" class="back">
          <mat-icon>arrow_back</mat-icon> {{ 'All reports' | t }}
        </a>

        <h2 class="name">{{ reportTitle() }}</h2>
        @if (reportDescription(); as desc) {
          <p class="desc">{{ desc }}</p>
        }

        @if (definition(); as def) {
          <app-report-parameter-panel
            [parameters]="def.parameters"
            [initial]="lastParameters()"
            [initialLanguage]="language()"
            [initialBengaliNumerals]="bengaliNumerals()"
            [busy]="running()"
            (run)="run($event)" />
        }
      </aside>

      <main class="canvas">
        <div class="toolbar no-print">
          @if (data(); as d) {
            <span class="meta">{{ (d.totalRowCount === 1 ? '{n} row · run {id}' : '{n} rows · run {id}') | t: { n: d.totalRowCount, id: d.meta.runId.slice(0, 8) } }}</span>
          }
          <span class="spacer"></span>
          <button mat-stroked-button [disabled]="!data() || running()" (click)="print()">
            <mat-icon>print</mat-icon> {{ 'Print' | t }}
          </button>
          @if (canExport()) {
            <!-- Disabled while anything is generating: an export of a half-finished run, or a
                 second export queued behind the first, is never what the user meant. -->
            <button mat-flat-button color="primary" [disabled]="!data() || exporting() || running()"
                    [matMenuTriggerFor]="exportMenu">
              <mat-icon>{{ exporting() ? 'hourglass_top' : 'download' }}</mat-icon>
              {{ (exporting() ? 'Exporting…' : 'Export') | t }}
            </button>
          }
          <mat-menu #exportMenu="matMenu">
            <button mat-menu-item (click)="exportAs('pdf')">
              <mat-icon>picture_as_pdf</mat-icon> PDF
            </button>
            <button mat-menu-item (click)="exportAs('xlsx')">
              <mat-icon>table_chart</mat-icon> Excel
            </button>
            <button mat-menu-item (click)="exportAs('csv')">
              <mat-icon>description</mat-icon> CSV
            </button>
          </mat-menu>
        </div>

        @if (running() || exporting()) {
          <mat-progress-bar mode="indeterminate" class="no-print"></mat-progress-bar>
        }

        @if (error()) {
          <div class="state error no-print" role="alert">
            <mat-icon>error_outline</mat-icon>
            <p class="title">{{ errorTitle() }}</p>
            <p>{{ error() }}</p>
          </div>
        }

        @if (data(); as d) {
          @if (d.totalRowCount === 0) {
            <div class="state empty no-print">
              <mat-icon>inbox</mat-icon>
              <p class="title">{{ (language() === 'bn' ? 'কোনো তথ্য পাওয়া যায়নি' : 'Nothing to show for these choices') | t }}</p>
              <p>{{ (language() === 'bn' ? 'সময়কাল বা ফিল্টার পরিবর্তন করে আবার চেষ্টা করুন।' : 'Try a wider period or fewer filters, then run the report again.') | t }}</p>
            </div>
          }
          <app-report-paper [data]="d" [language]="language()" />
        } @else if (!running() && !error()) {
          <div class="state no-print">
            <mat-icon>description</mat-icon>
            <p>{{ 'Set the parameters and run the report.' | t }}</p>
          </div>
        }
      </main>
    </div>
  `,
  styles: [`
    .viewer { display: grid; grid-template-columns: 320px 1fr; min-height: 100%; }

    .params { border-right: 1px solid var(--mat-sys-outline-variant, #e5e7eb);
              overflow-y: auto; padding-bottom: 24px; background: #ffffff; }
    :host-context(.dark) .params { border-color: rgba(75, 85, 99, 0.4); background: #111827; }
    .params .back { margin: 8px; color: #4b5563; }
    :host-context(.dark) .params .back { color: #d1d5db; }
    .params .name { margin: 4px 16px 0; font-size: 16px; font-weight: 700; color: #111827; }
    :host-context(.dark) .params .name { color: #f9fafb; }
    .params .desc { margin: 4px 16px 0; font-size: 12px; color: #4b5563; }
    :host-context(.dark) .params .desc { color: #9ca3af; }

    /* Canvas background so the white sheet reads as paper */
    .canvas { background: #f8fafc; padding: 16px; overflow-x: auto; }
    :host-context(.dark) .canvas { background: #0f172a; }

    .toolbar { display: flex; align-items: center; gap: 8px; margin-bottom: 12px; }
    .toolbar .spacer { flex: 1; }
    .toolbar .meta { font-size: 12px; color: #4b5563; }
    :host-context(.dark) .toolbar .meta { color: #9ca3af; }

    .state { display: flex; flex-direction: column; align-items: center; gap: 8px;
             padding: 60px; opacity: .7; color: #4b5563; }
    :host-context(.dark) .state { color: #9ca3af; }
    .state.error { color: var(--mat-sys-error, #b3261e); opacity: 1; }
    .state .title { font-weight: 700; margin: 0; }
    .state p { margin: 0; text-align: center; max-width: 520px; }
    .state.empty { padding: 24px; }

    @media (max-width: 900px) {
      .viewer { grid-template-columns: 1fr; }
      .params { border-right: none; border-bottom: 1px solid var(--mat-sys-outline-variant, #ddd); }
    }

    /* Printing sends the sheet alone; chrome must not appear on paper. */
    @media print {
      .no-print { display: none !important; }
      .viewer { display: block; }
      .canvas { background: #fff; padding: 0; }
    }
  `],
})
export class ReportViewerComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly reports = inject(ReportService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(I18nService);

  protected readonly definition = signal<ReportCatalogItem | null>(null);
  protected readonly data = signal<ReportDataSet | null>(null);
  protected readonly running = signal(false);
  protected readonly exporting = signal(false);
  protected readonly error = signal<string | null>(null);
  /** A Bangla interface defaults the report to Bangla until the user picks a language. */
  protected readonly language = signal<ReportLanguage>(this.i18n.isBangla ? 'bn' : 'en');
  protected readonly lastParameters = signal<Record<string, string | null>>({});
  protected readonly bengaliNumerals = signal(false);
  protected readonly canExport = signal(false);
  protected readonly errorTitle = signal(this.i18n.t('The report could not be run'));

  /** A past run handed over by "Run again" in the Report Center. */
  private readonly rerun: ReportRerun | null =
    (this.router.getCurrentNavigation()?.extras.state?.['rerun'] as ReportRerun | undefined) ?? null;

  private key = '';
  private lastRequest: RunReportRequest | null = null;

  ngOnInit(): void {
    this.key = this.route.snapshot.paramMap.get('key') ?? '';

    // The catalog is small and already cached by the browser; one call keeps this page
    // independent of how the user arrived at it (deep link, refresh, or the Report Center).
    this.reports.getCatalog().subscribe({
      next: items => {
        const found = items.find(i => i.key === this.key) ?? null;
        this.definition.set(found);

        if (!found) {
          this.errorTitle.set(this.i18n.t('Report not available'));
          this.error.set(this.i18n.t('This report does not exist, or your role does not include permission to run it.'));
          return;
        }

        this.canExport.set(found.canExport === true);
        this.reports.markOpened(this.key);

        const remembered = this.rerun ?? this.reports.recallParameters(this.key);
        const params: Record<string, string | null> = { ...(remembered?.parameters ?? {}) };
        const queryParams = this.route.snapshot.queryParamMap;
        for (const p of found.parameters) {
          if (queryParams.has(p.name)) {
            params[p.name] = queryParams.get(p.name);
          }
        }

        this.lastParameters.set(params);
        if (remembered) {
          this.language.set(remembered.language);
          this.bengaliNumerals.set(remembered.bengaliNumerals === true);
        }

        if (this.rerun) {
          this.run({ parameters: params, language: this.language(), bengaliNumerals: this.bengaliNumerals() });
          return;
        }

        if (queryParams.get('autoRun') === 'true' || queryParams.get('autoRun') === '1') {
          const runParams: Record<string, string | null> = {};
          for (const p of found.parameters) {
            runParams[p.name] = params[p.name] || p.defaultValue || null;
          }
          this.run({
            parameters: runParams,
            language: this.language(),
            bengaliNumerals: false
          });
        }
      },
      error: () => {
        this.errorTitle.set(this.i18n.t('Could not reach the server'));
        this.error.set(this.i18n.t('The report list could not be loaded. Check your connection and refresh the page.'));
      },
    });
  }

  /** Turns an API failure into something the user can act on. */
  private describeError(err: unknown): { title: string; message: string } {
    const e = err as { status?: number; error?: { message?: string; detail?: string } } | null;
    const serverMessage = e?.error?.message ?? e?.error?.detail;
    switch (e?.status) {
      case 400:
        return { title: this.i18n.t('Check the parameters'), message: serverMessage ?? this.i18n.t('One of the parameters is not valid.') };
      case 403:
        return { title: this.i18n.t('Not permitted'), message: this.i18n.t('Your role does not include permission to run or export this report. Ask the farm owner to grant it.') };
      case 404:
        return { title: this.i18n.t('Report not available'), message: serverMessage ?? this.i18n.t('This report no longer exists.') };
      case 0:
        return { title: this.i18n.t('No connection'), message: this.i18n.t('The server could not be reached. Check your connection and try again.') };
      default:
        return { title: this.i18n.t('Something went wrong'), message: this.i18n.t('The report could not be produced. Try again; if it keeps failing, tell your administrator.') };
    }
  }

  protected run(request: RunReportRequest): void {
    this.running.set(true);
    this.error.set(null);
    this.language.set(request.language);
    this.bengaliNumerals.set(request.bengaliNumerals);
    this.lastRequest = request;

    this.reports.run(this.key, request).subscribe({
      next: result => {
        this.data.set(result);
        this.running.set(false);
      },
      error: err => {
        // The API returns a specific message for a bad parameter or an oversized result; showing
        // it beats a generic failure the user cannot act on.
        const { title, message } = this.describeError(err);
        this.errorTitle.set(title);
        this.error.set(message);
        this.data.set(null);
        this.running.set(false);
      },
    });
  }

  protected exportAs(format: ReportExportFormat): void {
    if (!this.lastRequest || this.exporting() || this.running()) {
      return;
    }

    this.exporting.set(true);

    this.reports.export(this.key, format, this.lastRequest).subscribe({
      next: response => {
        if (response.body) {
          const fallback = `${this.key.replace(/\./g, '-')}.${format}`;
          this.reports.saveBlob(response.body, this.reports.fileNameFrom(response.headers, fallback));
        }
        this.exporting.set(false);
      },
      error: err => {
        this.snackBar.open(this.i18n.t('Export failed: {message}', { message: this.describeError(err).message }), this.i18n.t('Dismiss'), { duration: 6000 });
        this.exporting.set(false);
      },
    });
  }

  /** In Bangla the catalogue's Bangla title and description lead. */
  protected reportTitle(): string {
    const def = this.definition();
    if (!def) return this.i18n.t('Report');
    return this.i18n.isBangla && def.titleBn ? def.titleBn : def.title;
  }

  protected reportDescription(): string {
    const def = this.definition();
    if (!def) return '';
    return this.i18n.isBangla && def.descriptionBn ? def.descriptionBn : def.description;
  }

  protected print(): void {
    window.print();
  }
}
