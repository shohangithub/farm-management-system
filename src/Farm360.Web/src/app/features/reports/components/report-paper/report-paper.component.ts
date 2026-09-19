import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportCell, ReportDataSet, ReportLanguage } from '../../models/report.models';

interface RenderBand {
  kind: 'group' | 'detail' | 'subtotal' | 'grandtotal';
  label?: string;
  serial?: number;
  cells?: ReportCell[];
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
      <header class="letterhead">
        <div class="org">{{ data().meta.organizationName }}</div>
        @if (data().meta.farmName) {
          <div class="farm">{{ data().meta.farmName }}</div>
        }
        <div class="title">{{ data().title }}</div>
        @if (data().meta.subject) {
          <div class="subject">{{ data().meta.subject }}</div>
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

      @if (!data().rows.length) {
        <p class="empty">No data available for the selected criteria.</p>
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
                    @for (col of data().columns; track col.field; let i = $index) {
                      <td [class]="'a-' + col.align.toLowerCase()">{{ band.cells?.[i]?.f }}</td>
                    }
                  </tr>
                }
                @case ('subtotal') {
                  <tr class="subtotal">
                    <td class="serial"></td>
                    @for (col of data().columns; track col.field; let i = $index) {
                      <td [class]="'a-' + col.align.toLowerCase()">
                        {{ i === 0 ? band.label : band.cells?.[i]?.f }}
                      </td>
                    }
                  </tr>
                }
                @case ('grandtotal') {
                  <tr class="grandtotal">
                    <td class="serial"></td>
                    @for (col of data().columns; track col.field; let i = $index) {
                      <td [class]="'a-' + col.align.toLowerCase()">
                        {{ i === 0 ? band.label : band.cells?.[i]?.f }}
                      </td>
                    }
                  </tr>
                }
              }
            }
          </tbody>
        </table>
      }

      @if (data().page.showSignatureBlock) {
        <div class="signatures">
          <span>Prepared by</span><span>Checked by</span><span>Approved by</span>
        </div>
      }

      <footer class="foot">
        <span>{{ data().meta.reportKey }} · run {{ shortRunId() }}</span>
        <span>{{ data().totalRowCount }} rows</span>
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
    .letterhead .org { font-size: 13pt; font-weight: 700; }
    .letterhead .farm { font-size: 8pt; }
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
    .a-left { text-align: left; }
    .a-center { text-align: center; }
    .a-right { text-align: right; }

    .group td { background: #f2f2f2; font-weight: 700; }
    .subtotal td { background: #f2f2f2; font-weight: 700; }
    .grandtotal td { background: #f2f2f2; font-weight: 700; border-top: 1.5px solid #000; }

    .empty { text-align: center; padding: 30px; color: #666; }

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

  protected readonly metaFields = computed(() => this.data().meta.parameters ?? []);

  /** Two label/value pairs per row, matching the server's parameter box. */
  protected readonly metaPairs = computed(() => {
    const meta = this.data().meta;
    const fields = [
      ...this.metaFields(),
      { label: 'Generated by', value: meta.generatedByName },
      { label: 'Generated at', value: new Date(meta.generatedAtUtc).toISOString().slice(0, 16).replace('T', ' ') + ' UTC' },
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

    const pushSubtotal = (key: string) => {
      if (printed.has(key)) {
        return;
      }
      printed.add(key);
      const group = data.groups.find(g => g.groupKey === key);
      if (group) {
        out.push({
          kind: 'subtotal',
          label: `Sub-total — ${group.groupLabel} (${group.rowCount})`,
          cells: group.totals,
        });
      }
    };

    for (const row of data.rows) {
      if (row.groupKey && row.groupKey !== currentGroup) {
        if (currentGroup) {
          pushSubtotal(currentGroup);
        }
        currentGroup = row.groupKey;
        const group = data.groups.find(g => g.groupKey === row.groupKey);
        out.push({ kind: 'group', label: group?.groupLabel ?? row.groupKey });
      }

      out.push({ kind: 'detail', serial: ++serial, cells: row.cells });
    }

    if (currentGroup) {
      pushSubtotal(currentGroup);
    }

    if (data.hasGrandTotals) {
      out.push({
        kind: 'grandtotal',
        label: `GRAND TOTAL (${data.totalRowCount} rows)`,
        cells: data.grandTotals,
      });
    }

    return out;
  });

  protected header(text: { en: string; bn: string }): string {
    return this.language() === 'bn' && text.bn ? text.bn : text.en;
  }
}
