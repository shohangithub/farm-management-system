/**
 * Shared plumbing for the cash-book style finance reports (Income & Expense, Full Accounts).
 *
 * The report body is laid out as an A4 "paper" document. The same stylesheet is used on screen
 * (via ViewEncapsulation.None, scoped under `.acct-paper`) and injected into the print window, so
 * what the user sees is exactly what comes out of the printer.
 */

/** Local calendar date as yyyy-MM-dd. `toISOString()` would shift to UTC — before 06:00 in Dhaka that is yesterday. */
export function toIsoDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export function addDays(date: Date, days: number): Date {
  const copy = new Date(date);
  copy.setDate(copy.getDate() + days);
  return copy;
}

export interface DatePreset {
  key: string;
  label: string;
  range: () => { from: string; to: string };
}

const today = () => new Date();

export const RANGE_PRESETS: DatePreset[] = [
  { key: 'today', label: 'Today', range: () => ({ from: toIsoDate(today()), to: toIsoDate(today()) }) },
  {
    key: 'yesterday', label: 'Yesterday',
    range: () => ({ from: toIsoDate(addDays(today(), -1)), to: toIsoDate(addDays(today(), -1)) }),
  },
  { key: 'last-7', label: 'Last 7 days', range: () => ({ from: toIsoDate(addDays(today(), -6)), to: toIsoDate(today()) }) },
  {
    key: 'this-month', label: 'This month',
    range: () => {
      const t = today();
      return { from: toIsoDate(new Date(t.getFullYear(), t.getMonth(), 1)), to: toIsoDate(t) };
    },
  },
  {
    key: 'last-month', label: 'Last month',
    range: () => {
      const t = today();
      return {
        from: toIsoDate(new Date(t.getFullYear(), t.getMonth() - 1, 1)),
        to: toIsoDate(new Date(t.getFullYear(), t.getMonth(), 0)),
      };
    },
  },
  {
    key: 'this-year', label: 'This year',
    range: () => {
      const t = today();
      return { from: toIsoDate(new Date(t.getFullYear(), 0, 1)), to: toIsoDate(t) };
    },
  },
];

/**
 * Prints one element in a clean window with only the paper stylesheet — no sidebar, no header,
 * no Material chrome. Falls back to window.print() if pop-ups are blocked.
 */
export function printPaper(element: HTMLElement, title: string): void {
  const win = window.open('', '_blank', 'width=1000,height=800');
  if (!win) {
    window.print();
    return;
  }

  win.document.open();
  win.document.write(`<!doctype html>
<html><head><meta charset="utf-8"><title>${escapeHtml(title)}</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="https://fonts.googleapis.com/css2?family=Hind+Siliguri:wght@400;500;600;700&display=swap" rel="stylesheet">
<style>${ACCOUNTS_PAPER_CSS}
  body { margin: 0; background: #fff; }
  .acct-paper { box-shadow: none !important; border: none !important; margin: 0 !important; max-width: none !important; }
</style></head>
<body>${element.outerHTML}</body></html>`);
  win.document.close();

  // Give web fonts a moment so Bengali glyphs are not printed in the fallback face.
  const go = () => {
    win.focus();
    win.print();
    win.close();
  };
  if (win.document.fonts?.ready) {
    win.document.fonts.ready.then(() => setTimeout(go, 150));
  } else {
    setTimeout(go, 600);
  }
}

function escapeHtml(text: string): string {
  return text.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c] as string));
}

export const ACCOUNTS_PAPER_CSS = `
.acct-paper {
  --ink: #111827; --muted: #6b7280; --line: #d1d5db; --soft: #f3f4f6;
  --inc: #047857; --inc-bg: #ecfdf5; --exp: #b91c1c; --exp-bg: #fef2f2;
  --cap: #6d28d9; --cap-bg: #f5f3ff; --fin: #b45309; --fin-bg: #fffbeb; --brand: #0f766e;
  background: #fff; color: var(--ink);
  font-family: 'Hind Siliguri', 'Nirmala UI', 'Inter', 'Segoe UI', Arial, sans-serif; font-size: 12px; line-height: 1.45;
  max-width: 1060px; margin: 0 auto; padding: 28px 32px; border-radius: 14px;
  border: 1px solid #e5e7eb; box-shadow: 0 1px 3px rgba(0,0,0,.06);
}
.acct-paper * { box-sizing: border-box; }
.acct-paper .num { font-variant-numeric: tabular-nums; white-space: nowrap; text-align: right; }
.acct-paper .center { text-align: center; }
.acct-paper .muted { color: var(--muted); }
.acct-paper .bold { font-weight: 700; }
.acct-paper .inc { color: var(--inc); }
.acct-paper .exp { color: var(--exp); }
.acct-paper .cap { color: var(--cap); }

/* Letterhead */
.acct-paper .ph { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px;
  border-bottom: 3px double var(--ink); padding-bottom: 12px; margin-bottom: 14px; }
.acct-paper .ph-org { font-size: 20px; font-weight: 800; letter-spacing: .2px; color: var(--brand); }
.acct-paper .ph-farm { font-size: 13px; font-weight: 600; color: var(--ink); }
.acct-paper .ph-title { text-align: right; }
.acct-paper .ph-title h1 { margin: 0; font-size: 18px; font-weight: 800; }
.acct-paper .ph-title h2 { margin: 2px 0 0; font-size: 12px; font-weight: 600; color: var(--muted); letter-spacing: .4px; text-transform: uppercase; }
.acct-paper .ph-period { display: inline-block; margin-top: 6px; padding: 3px 10px; border: 1px solid var(--ink);
  border-radius: 999px; font-weight: 700; font-size: 11.5px; }

/* Info grid */
.acct-paper .info { width: 100%; border-collapse: collapse; margin-bottom: 14px; border: 1px solid var(--line); }
.acct-paper .info td { padding: 6px 10px; border: 1px solid var(--line); }
.acct-paper .info .k { background: var(--soft); font-weight: 600; width: 18%; color: #374151; }
.acct-paper .info .v { font-weight: 700; width: 32%; }

/* KPI tiles */
.acct-paper .kpis { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; margin-bottom: 16px; }
.acct-paper .kpi { border: 1px solid var(--line); border-radius: 10px; padding: 8px 10px; background: #fff; }
.acct-paper .kpi .l { font-size: 10.5px; color: var(--muted); font-weight: 600; text-transform: uppercase; letter-spacing: .3px; }
.acct-paper .kpi .v { font-size: 16px; font-weight: 800; margin-top: 2px; font-variant-numeric: tabular-nums; }
.acct-paper .kpi .s { font-size: 10.5px; color: var(--muted); }
.acct-paper .kpi.inc { border-left: 4px solid var(--inc); }
.acct-paper .kpi.exp { border-left: 4px solid var(--exp); }
.acct-paper .kpi.cap { border-left: 4px solid var(--cap); }
.acct-paper .kpi.bal { border-left: 4px solid var(--brand); }

/* Section titles */
.acct-paper .sec { display: flex; align-items: center; justify-content: space-between; margin: 18px 0 6px;
  padding: 6px 10px; background: var(--ink); color: #fff; border-radius: 6px; font-weight: 700; font-size: 12.5px; }
.acct-paper .sec small { font-weight: 500; opacity: .8; }
.acct-paper .sec.inc { background: var(--inc); }
.acct-paper .sec.exp { background: var(--exp); }
.acct-paper .sec.cap { background: var(--cap); }
.acct-paper .sec.fin { background: var(--fin); }
.acct-paper .sec.brand { background: var(--brand); }

/* Tables */
.acct-paper table.t { width: 100%; border-collapse: collapse; margin-bottom: 4px; }
.acct-paper table.t th { background: var(--soft); font-weight: 700; font-size: 11px; color: #374151;
  border: 1px solid var(--line); padding: 5px 7px; text-align: left; }
.acct-paper table.t td { border: 1px solid var(--line); padding: 4px 7px; vertical-align: top; }
.acct-paper table.t tbody tr:nth-child(even) td { background: #fafafa; }
.acct-paper table.t tfoot td { font-weight: 800; background: var(--soft); border-top: 2px solid var(--ink); }
.acct-paper table.t tr.grand td { background: #e5e7eb; border-bottom: 3px double var(--ink); font-size: 12.5px; }
.acct-paper table.t tr.sub td { background: #f9fafb; font-weight: 700; }
.acct-paper table.t tr.opening td { background: #eff6ff; font-weight: 700; }
.acct-paper table.t th.inc-h { background: var(--inc-bg); color: var(--inc); text-align: center; font-size: 12px; }
.acct-paper table.t th.exp-h { background: var(--exp-bg); color: var(--exp); text-align: center; font-size: 12px; }
.acct-paper table.t .sl { width: 30px; text-align: center; color: var(--muted); }
.acct-paper .note { font-size: 10.5px; color: var(--muted); margin-top: 1px; }
.acct-paper .badge { display: inline-block; padding: 0 6px; border-radius: 999px; font-size: 9.5px; font-weight: 700;
  border: 1px solid currentColor; margin-left: 4px; vertical-align: 1px; }
.acct-paper .badge.Capital { color: var(--cap); }
.acct-paper .badge.Financing { color: var(--fin); }
.acct-paper .badge.Asset { color: var(--brand); }
.acct-paper .badge.Trade { color: #b45309; }
.acct-paper .badge.auto { color: #0369a1; }
.acct-paper .cnt { color: var(--muted); font-size: 10.5px; margin-left: 3px; }

/* Bars for cost analysis */
.acct-paper .bar { height: 8px; background: var(--soft); border-radius: 4px; overflow: hidden; min-width: 80px; }
.acct-paper .bar > span { display: block; height: 100%; background: var(--exp); border-radius: 4px; }
.acct-paper .bar.labour > span { background: #7c3aed; }

.acct-paper .two { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.acct-paper .callout { border: 1px dashed var(--fin); background: var(--fin-bg); color: #92400e; border-radius: 8px;
  padding: 7px 10px; font-size: 11px; margin: 6px 0; }
.acct-paper .result { display: flex; justify-content: space-between; align-items: center; padding: 10px 12px;
  border: 2px solid var(--ink); border-radius: 8px; margin-top: 8px; font-weight: 800; font-size: 14px; }
.acct-paper .result.profit { border-color: var(--inc); color: var(--inc); background: var(--inc-bg); }
.acct-paper .result.loss { border-color: var(--exp); color: var(--exp); background: var(--exp-bg); }
.acct-paper .empty { text-align: center; color: var(--muted); padding: 14px; border: 1px dashed var(--line); border-radius: 8px; }

/* Signatures & footer */
.acct-paper .sig { display: grid; grid-template-columns: repeat(3, 1fr); gap: 40px; margin-top: 48px; }
.acct-paper .sig div { border-top: 1px solid var(--ink); padding-top: 4px; text-align: center; font-weight: 700; font-size: 11.5px; }
.acct-paper .sig small { display: block; font-weight: 500; color: var(--muted); }
.acct-paper .foot { margin-top: 18px; padding-top: 6px; border-top: 1px solid var(--line); display: flex;
  justify-content: space-between; font-size: 10px; color: var(--muted); }

@media (max-width: 760px) {
  .acct-paper { padding: 16px 12px; }
  .acct-paper .kpis { grid-template-columns: repeat(2, 1fr); }
  .acct-paper .two { grid-template-columns: 1fr; }
  .acct-paper .ph { flex-direction: column; }
  .acct-paper .ph-title { text-align: left; }
  .acct-paper .tw { overflow-x: auto; }
}

@media print {
  @page { size: A4; margin: 12mm 10mm; }
  .acct-paper { font-size: 10.5px; padding: 0; }
  .acct-paper .sec, .acct-paper table.t tr { break-inside: avoid; }
  .acct-paper .sec { break-after: avoid; }
  .acct-paper table.t thead { display: table-header-group; }
  .acct-paper .sig { break-inside: avoid; }
  .acct-paper .no-print { display: none !important; }
}
`;
