import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportCell, ReportDataSet, ReportLanguage } from '../../models/report.models';

interface RenderDetailCell {
  f: string;
  rowSpan: number;
}

interface RenderBand {
  kind: 'group' | 'detail' | 'subtotal' | 'grandtotal';
  label?: string;
  serial?: number;
  cells?: ReportCell[];
  detailCells?: RenderDetailCell[];
  /** Column index that carries the band label (sub-total / grand-total rows). */
  labelAt?: number;
}

/**
 * Renders a ReportDataSet as an A4 sheet.
 *
 * The geometry (210 x 297 mm, 10 mm padding, 8pt dense bordered table, grey header fill,
 * right-aligned numerics) is lifted from the FrostTrack daily-stock-book stylesheet and matches
 * the server's QuestPDF document band for band, so what the user reads on screen is what the
 * printed PDF will say. Cells print the server-formatted string, never a re-formatted value.
 */
@Component({
  selector: 'app-report-paper',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sheet" [class.landscape]="isLandscape()">
      <!-- Letterhead -->
      <header class="letterhead" [class.with-logo]="showLogo()">
        @if (showLogo()) {
          <div class="logo-slot">
            <img [src]="data().meta.logoUrl" alt="" class="logo" (error)="failedLogo.set(data().meta.logoUrl ?? null)" />
          </div>
        }
        <div class="centre">
          <div class="org">{{ data().meta.organizationName }}</div>
          @if (data().meta.farmName) {
            <div class="farm">{{ data().meta.farmName }}</div>
          }
          @if (data().meta.contactLine) {
            <div class="contact">{{ data().meta.contactLine }}</div>
          }
          <div class="title">{{ data().title }}</div>
          @if (data().meta.subject) {
            <div class="subject">{{ data().meta.subject }}</div>
          }
        </div>
        @if (showLogo()) {
          <div class="logo-slot" aria-hidden="true"></div>
        }
      </header>

      <!-- Parameter echo: a printed page without its filters is not evidence -->
      @if (metaFields().length) {
        <table class="params">
          <tbody>
            @for (pair of metaPairs(); track $index) {
              <tr>
                <td class="k">{{ pair[0].label }}</td>
                <td class="v">{{ pair[0].value }}</td>
                <td class="k">{{ pair[1]?.label }}</td>
                <td class="v">{{ pair[1]?.value }}</td>
              </tr>
            }
          </tbody>
        </table>
      }

      @if (data().summary; as summary) {
        <section class="summary">
          <div class="summary-title">{{ summary.title }}</div>
          @if (summary.figures.length) {
            <div class="figures">
              @for (fig of summary.figures; track $index) {
                <div class="figure">
                  <div class="fig-label">{{ fig.label }}</div>
                  <div class="fig-value" [class.good]="fig.tone === 'good'" [class.bad]="fig.tone === 'bad'">{{ fig.value }}</div>
                </div>
              }
            </div>
          }
          @if (summary.insights.length) {
            <div class="insights">
              @if (summary.insightsTitle) {
                <div class="insights-title">{{ summary.insightsTitle }}</div>
              }
              <ul>
                @for (line of summary.insights; track $index) {
                  <li>{{ line }}</li>
                }
              </ul>
            </div>
          }
        </section>
      }

      @if (!data().rows.length) {
        <p class="empty">{{ t('No data available for the selected criteria.', 'নির্বাচিত শর্তে কোনো তথ্য পাওয়া যায়নি।') }}</p>
      } @else {
        <table class="detail">
          <thead>
            <tr>
              <th class="serial">#</th>
              @for (col of data().columns; track col.field) {
                <th [class]="'a-' + col.align.toLowerCase()">{{ header(col.header) }}</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (band of bands(); track $index) {
              @switch (band.kind) {
                @case ('group') {
                  <tr class="group">
                    <td [attr.colspan]="data().columns.length + 1">{{ band.label }}</td>
                  </tr>
                }
                @case ('detail') {
                  <tr>
                    <td class="serial">{{ band.serial }}</td>
                    @for (cell of band.detailCells; track $index; let i = $index) {
                      @if (cell.rowSpan > 0) {
                        <td [class]="'a-' + data().columns[i].align.toLowerCase()"
                            [class.merged-cell]="cell.rowSpan > 1"
                            [attr.rowspan]="cell.rowSpan > 1 ? cell.rowSpan : null">
                          {{ cell.f }}
                        </td>
                      }
                    }
                  </tr>
                }
                @case ('subtotal') {
                  <tr class="subtotal">
                    <td class="serial"></td>
                    @for (col of data().columns; track col.field; let i = $index) {
                      <td [class]="'a-' + col.align.toLowerCase()">
                        {{ i === band.labelAt ? band.label : band.cells?.[i]?.f }}
                      </td>
                    }
                  </tr>
                }
                @case ('grandtotal') {
                  <tr class="grandtotal">
                    <td class="serial"></td>
                    @for (col of data().columns; track col.field; let i = $index) {
                      <td [class]="'a-' + col.align.toLowerCase()">
                        {{ i === band.labelAt ? band.label : band.cells?.[i]?.f }}
                      </td>
                    }
                  </tr>
                }
              }
            }
          </tbody>
        </table>
      }

      @if (data().notes?.length) {
        <section class="notes">
          <div class="notes-title">{{ t('How to read this report', 'প্রতিবেদনটি যেভাবে পড়বেন') }}</div>
          <ul>
            @for (note of data().notes; track $index) {
              <li>{{ note }}</li>
            }
          </ul>
        </section>
      }

      @if (data().page.showSignatureBlock) {
        <div class="signatures">
          <span>{{ t('Prepared by', 'প্রস্তুতকারী') }}</span><span>{{ t('Checked by', 'যাচাইকারী') }}</span><span>{{ t('Approved by', 'অনুমোদনকারী') }}</span>
        </div>
      }

      <footer class="foot">
        <span>{{ data().meta.reportKey }} · {{ t('run', 'রান') }} {{ shortRunId() }}</span>
        <span>{{ data().totalRowCount }} {{ data().totalRowCount === 1 ? t('row', 'সারি') : t('rows', 'সারি') }}</span>
      </footer>
    </div>
  `,
  styles: [`
    /* A4 geometry. The paper is always white, in both themes — a report is a piece of paper. */
    .sheet {
      width: 210mm; min-height: 297mm; padding: 10mm; margin: 0 auto 16px;
      background: #fff; color: #000; box-sizing: border-box;
      font-family: 'Noto Sans Bengali', Arial, Helvetica, sans-serif;
      font-size: 8pt; line-height: 1.35;
      box-shadow: 0 1px 10px rgba(0,0,0,.18);
      -webkit-print-color-adjust: exact; print-color-adjust: exact;
    }
    .sheet.landscape { width: 297mm; min-height: 210mm; }

    .letterhead { text-align: center; border-bottom: 1px solid #000; padding-bottom: 4px; }
    .letterhead.with-logo { display: flex; align-items: center; gap: 4mm; }
    .letterhead .centre { flex: 1; min-width: 0; }
    .letterhead .logo-slot { width: 24mm; flex: 0 0 24mm; display: flex; align-items: center; }
    .letterhead .logo { max-height: 18mm; max-width: 24mm; object-fit: contain; }
    .letterhead .org { font-size: 13pt; font-weight: 700; }
    .letterhead .farm { font-size: 8pt; }
    .letterhead .contact { font-size: 7.5pt; color: #555; }
    .letterhead .title { font-size: 12pt; font-weight: 700; margin-top: 4px; }
    .letterhead .subject { font-size: 8pt; color: #555; }

    table { width: 100%; border-collapse: collapse; }

    .params { border: 1px solid #000; margin: 8px 0; }
    .params td { border: .5px solid #000; padding: 3px 5px; }
    .params .k { font-weight: 700; background: #f0f0f0; width: 15%; }
    .params .v { width: 35%; }

    .detail th, .detail td { border: .5px solid #000; padding: 2.5px 4px; vertical-align: top; }
    .detail th { background: #e8e8e8; font-weight: 700; text-align: left; }
    .detail .serial { width: 10mm; text-align: center; }
    .detail td.merged-cell { vertical-align: middle; text-align: center; font-weight: 600; background: #fafafa; }
    .a-left { text-align: left; }
    .a-center { text-align: center; }
    .a-right { text-align: right; }

    .group td { background: #f2f2f2; font-weight: 700; }
    .subtotal td { background: #f2f2f2; font-weight: 700; }
    .grandtotal td { background: #f2f2f2; font-weight: 700; border-top: 1.5px solid #000; }

    .empty { text-align: center; padding: 30px; color: #666; }

    .summary { border: 1px solid #000; margin: 0 0 8px; break-inside: avoid; }
    .summary-title { background: #e8e8e8; font-weight: 700; font-size: 9pt; padding: 3px 6px; border-bottom: .5px solid #000; }
    .figures { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); }
    .figure { border-right: .5px solid #bbb; border-bottom: .5px solid #bbb; padding: 4px 6px; }
    .fig-label { font-size: 7pt; color: #555; }
    .fig-value { font-size: 10pt; font-weight: 700; }
    .fig-value.good { color: #1b5e20; }
    .fig-value.bad { color: #b71c1c; }
    .insights { padding: 4px 8px 5px; }
    .insights-title { font-weight: 700; margin-bottom: 2px; }
    .insights ul { margin: 0; padding-left: 14px; }
    .insights li { margin: 1px 0; }

    .notes { margin-top: 8px; border: 0.5pt solid #999; padding: 5px 8px; font-size: 7.5pt; color: #333; break-inside: avoid; }
    .notes-title { font-weight: 700; font-size: 8pt; margin-bottom: 2px; color: #000; }
    .notes ul { margin: 0; padding-left: 14px; }
    .notes li { margin: 1px 0; }
    .signatures {
      display: flex; justify-content: space-between; margin-top: 18mm; gap: 12mm;
    }
    .signatures span {
      flex: 1; border-top: .5px solid #000; padding-top: 3px;
      text-align: center; font-size: 7pt;
    }

    .foot {
      display: flex; justify-content: space-between;
      margin-top: 10px; padding-top: 3px; border-top: .5px solid #000;
      font-size: 7pt; color: #666;
    }

    @media print {
      .sheet { box-shadow: none; margin: 0; }
      .detail thead { display: table-header-group; }
      .detail tr { page-break-inside: avoid; }
    }
  `],
})
export class ReportPaperComponent {
  readonly data = input.required<ReportDataSet>();
  readonly language = input<ReportLanguage>('en');

  protected readonly isLandscape = computed(() => this.data().page.orientation === 'Landscape');

  /** A logo URL that failed to load: the letterhead falls back to text only rather than a broken image. */
  protected readonly failedLogo = signal<string | null>(null);

  protected readonly showLogo = computed(() => {
    const url = this.data().meta.logoUrl;
    return !!url && url !== this.failedLogo();
  });

  protected readonly metaFields = computed(() => this.data().meta.parameters ?? []);

  /** Two label/value pairs per row, matching the server's parameter box. */
  protected readonly metaPairs = computed(() => {
    const meta = this.data().meta;
    const fields = [
      ...this.metaFields(),
      { label: this.t('Generated by', 'প্রস্তুতকারী'), value: meta.generatedByName },
      // The server formats this in the farm's zone ("16-Nov-2026 14:05 BST (UTC+6)"), so the
      // screen, the PDF and the spreadsheet all print the same local time.
      { label: this.t('Generated at', 'প্রস্তুতের সময়'), value: meta.generatedAtDisplay ?? meta.generatedAtLocal ?? this.utcStamp(meta.generatedAtUtc) },
    ];

    const pairs: (typeof fields)[] = [];
    for (let i = 0; i < fields.length; i += 2) {
      pairs.push(fields.slice(i, i + 2));
    }
    return pairs;
  });

  protected readonly shortRunId = computed(() => (this.data().meta.runId ?? '').replace(/-/g, '').slice(0, 8));

  /**
   * Flattens rows into display bands, inserting a group header when the key changes and a
   * sub-total after each group — the same sequence the PDF renderer emits, so the two agree.
   */
  protected readonly bands = computed<RenderBand[]>(() => {
    const data = this.data();
    const out: RenderBand[] = [];
    const printed = new Set<string>();
    let currentGroup: string | null = null;
    let serial = 0;

    const currentDetailBlock: RenderBand[] = [];

    const flushDetailBlock = () => {
      if (currentDetailBlock.length === 0) return;
      const numCols = data.columns.length;
      for (const b of currentDetailBlock) {
        b.detailCells = new Array(numCols);
      }

      for (let c = 0; c < numCols; c++) {
        const isMerge = !!data.columns[c].mergeRepeating;
        if (!isMerge) {
          for (const b of currentDetailBlock) {
            b.detailCells![c] = { f: b.cells?.[c]?.f ?? '', rowSpan: 1 };
          }
        } else {
          let start = 0;
          while (start < currentDetailBlock.length) {
            const val = currentDetailBlock[start].cells?.[c]?.f ?? '';
            let end = start + 1;
            while (end < currentDetailBlock.length && (currentDetailBlock[end].cells?.[c]?.f ?? '') === val) {
              end++;
            }
            const span = end - start;
            currentDetailBlock[start].detailCells![c] = { f: val, rowSpan: span };
            for (let k = start + 1; k < end; k++) {
              currentDetailBlock[k].detailCells![c] = { f: val, rowSpan: 0 };
            }
            start = end;
          }
        }
      }
      currentDetailBlock.length = 0;
    };

    const pushSubtotal = (key: string) => {
      flushDetailBlock();
      if (printed.has(key)) {
        return;
      }
      printed.add(key);
      const group = data.groups.find(g => g.groupKey === key);
      // A group with nothing to total (a statement section, a status list) gets no empty band.
      if (group && group.totals.some(c => c.v !== null && c.v !== undefined)) {
        out.push({
          kind: 'subtotal',
          label: `${this.t('Sub-total', 'উপ-মোট')} — ${group.groupLabel} (${group.rowCount})`,
          cells: group.totals,
          labelAt: this.firstEmpty(group.totals),
        });
      }
    };

    for (const row of data.rows) {
      if (row.groupKey && row.groupKey !== currentGroup) {
        if (currentGroup) {
          pushSubtotal(currentGroup);
        } else {
          flushDetailBlock();
        }
        currentGroup = row.groupKey;
        const group = data.groups.find(g => g.groupKey === row.groupKey);
        out.push({ kind: 'group', label: group?.groupLabel ?? row.groupKey });
      }

      const band: RenderBand = { kind: 'detail', serial: ++serial, cells: row.cells };
      out.push(band);
      currentDetailBlock.push(band);
    }

    flushDetailBlock();

    if (currentGroup) {
      pushSubtotal(currentGroup);
    }

    if (data.hasGrandTotals) {
      const caption = data.grandTotalCaption ?? data.grandTotalLabel ?? this.t('Grand total', 'সর্বমোট');
      out.push({
        kind: 'grandtotal',
        label: `${caption} (${data.totalRowCount} ${this.t('rows', 'সারি')})`,
        cells: data.grandTotals,
        labelAt: this.firstEmpty(data.grandTotals),
      });
    }

    return out;
  });

  protected header(text: { en: string; bn: string }): string {
    return this.language() === 'bn' && text.bn ? text.bn : text.en;
  }

  protected t(en: string, bn: string): string {
    return this.language() === 'bn' ? bn : en;
  }

  /** The label goes in the first column without a figure, so it never covers a total. */
  private firstEmpty(cells: { v: unknown; f: string }[]): number {
    const index = this.data().columns.findIndex((_, i) => !cells?.[i]?.f);
    return index < 0 ? 0 : index;
  }

  /** Fallback for datasets from an older server: explicit about being UTC. */
  private utcStamp(iso: string): string {
    const d = new Date(iso);
    return isNaN(d.getTime()) ? iso : d.toISOString().slice(0, 16).replace('T', ' ') + ' UTC';
  }
}
