import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { ReportService } from '../../services/report.service';
import { ReportPaperComponent } from '../../components/report-paper/report-paper.component';
import { ReportParameterPanelComponent } from '../../components/report-parameter-panel/report-parameter-panel.component';
import {
  ReportCatalogItem,
  ReportDataSet,
  ReportExportFormat,
  ReportLanguage,
  RunReportRequest,
} from '../../models/report.models';

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
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="viewer">
      <aside class="params no-print">
        <a mat-button routerLink="/reports" class="back">
          <mat-icon>arrow_back</mat-icon> All reports
        </a>

        <h2 class="name">{{ definition()?.title ?? 'Report' }}</h2>
        @if (definition()?.description) {
          <p class="desc">{{ definition()?.description }}</p>
        }

        @if (definition(); as def) {
          <app-report-parameter-panel
            [parameters]="def.parameters"
            [initial]="lastParameters()"
            [busy]="running()"
            (run)="run($event)" />
        }
      </aside>

      <main class="canvas">
        <div class="toolbar no-print">
          @if (data(); as d) {
            <span class="meta">{{ d.totalRowCount }} rows · run {{ d.meta.runId.slice(0, 8) }}</span>
          }
          <span class="spacer"></span>
          <button mat-stroked-button [disabled]="!data()" (click)="print()">
            <mat-icon>print</mat-icon> Print
          </button>
          <button mat-flat-button color="primary" [disabled]="!data() || exporting()"
                  [matMenuTriggerFor]="exportMenu">
            <mat-icon>download</mat-icon> Export
          </button>
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
          <div class="state error no-print">
            <mat-icon>error_outline</mat-icon>
            <p>{{ error() }}</p>
          </div>
        }

        @if (data(); as d) {
          <app-report-paper [data]="d" [language]="language()" />
        } @else if (!running() && !error()) {
          <div class="state no-print">
            <mat-icon>description</mat-icon>
            <p>Set the parameters and run the report.</p>
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
    .state.error { color: var(--mat-sys-error, #b3261e); }

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
  private readonly reports = inject(ReportService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly definition = signal<ReportCatalogItem | null>(null);
  protected readonly data = signal<ReportDataSet | null>(null);
  protected readonly running = signal(false);
  protected readonly exporting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly language = signal<ReportLanguage>('en');
  protected readonly lastParameters = signal<Record<string, string | null>>({});

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
          this.error.set(`No report with key "${this.key}".`);
          return;
        }

        const remembered = this.reports.recallParameters(this.key);
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
        }
      },
      error: () => this.error.set('Could not load the report definition.'),
    });
  }

  protected run(request: RunReportRequest): void {
    this.running.set(true);
    this.error.set(null);
    this.language.set(request.language);
    this.lastRequest = request;

    this.reports.run(this.key, request).subscribe({
      next: result => {
        this.data.set(result);
        this.running.set(false);

        if (result.totalRowCount === 0) {
          this.snackBar.open('No data for the selected criteria.', 'Dismiss', { duration: 4000 });
        }
      },
      error: err => {
        // The API returns a specific message for a bad parameter or an oversized result; showing
        // it beats a generic failure the user cannot act on.
        this.error.set(err?.error?.message ?? 'The report could not be run.');
        this.running.set(false);
      },
    });
  }

  protected exportAs(format: ReportExportFormat): void {
    if (!this.lastRequest) {
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
      error: () => {
        this.snackBar.open('Export failed.', 'Dismiss', { duration: 4000 });
        this.exporting.set(false);
      },
    });
  }

  protected print(): void {
    window.print();
  }
}
