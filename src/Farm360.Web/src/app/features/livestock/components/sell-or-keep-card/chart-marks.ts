/** A marked day on a projection chart (today, best day, break-even). */
export interface ChartMark {
  /** Day number (0 = today). */
  day: number;
  x: number;
  y: number;
  /** English source text, translated with the `t` pipe. */
  label: string;
  kind: 'today' | 'best' | 'even';
}

/** A mark with where its label goes: row 0 is nearest the plot, row 1 is stacked above it. */
export interface PlacedMark extends ChartMark {
  row: number;
  anchor: 'start' | 'middle' | 'end';
}

/**
 * Places the labels of the chart's marks so they never print over each other.
 * <ul>
 * <li>Today and best day on the same day become one mark, "Today = best day".</li>
 * <li>Marks closer than <paramref name="minGapFraction"/> of the plot width are stacked on alternate rows.</li>
 * <li>A label near either edge is anchored to that edge so it stays inside the chart.</li>
 * </ul>
 */
export function placeChartMarks(marks: ChartMark[], plotLeft: number, plotRight: number, minGapFraction = 0.08): PlacedMark[] {
  const sorted = [...marks].sort((a, b) => a.x - b.x || order(a.kind) - order(b.kind));

  // Merge today and best day when they are the same day.
  const merged: ChartMark[] = [];
  for (const m of sorted) {
    const same = merged.find(p => p.day === m.day);
    if (same) {
      if ((same.kind === 'today' && m.kind === 'best') || (same.kind === 'best' && m.kind === 'today')) {
        same.label = 'Today = best day';
        same.kind = 'best';
      }
      continue;
    }
    merged.push({ ...m });
  }

  const width = Math.max(1, plotRight - plotLeft);
  const minGap = width * minGapFraction;
  const placed: PlacedMark[] = [];
  for (const m of merged) {
    const prev = placed[placed.length - 1];
    const row = prev && m.x - prev.x < minGap ? (prev.row + 1) % 2 : 0;
    const anchor: PlacedMark['anchor'] = m.x - plotLeft < width * 0.06 ? 'start' : plotRight - m.x < width * 0.06 ? 'end' : 'middle';
    placed.push({ ...m, row, anchor });
  }
  return placed;
}

function order(kind: ChartMark['kind']): number {
  return kind === 'today' ? 0 : kind === 'even' ? 1 : 2;
}

/** Short taka for an axis: ৳-7.3k, ৳12k, ৳850. */
export function shortTaka(v: number): string {
  const abs = Math.abs(v);
  const sign = v < 0 ? '-' : '';
  if (abs >= 100_000) return `${sign}৳${Math.round(abs / 1000)}k`;
  if (abs >= 1000) return `${sign}৳${(abs / 1000).toFixed(1).replace(/\.0$/, '')}k`;
  return `${sign}৳${Math.round(abs)}`;
}
