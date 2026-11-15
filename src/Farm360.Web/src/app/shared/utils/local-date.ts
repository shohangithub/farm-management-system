/**
 * Local calendar date as `yyyy-MM-dd`.
 *
 * `date.toISOString().split('T')[0]` converts to UTC first — in Dhaka (UTC+6) any time before
 * 06:00 comes out as the previous day, and `new Date(y, m, 1)` (local midnight) always does.
 * Use this whenever a date picker value or an API date parameter is built from a `Date`.
 */
export function toLocalIsoDate(date: Date = new Date()): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/**
 * First day of the fiscal year containing `date`, for a year starting in `startMonth` (1–12;
 * Bangladesh's July–June year when missing). Mirrors `FiscalYear.StartOf` on the server.
 */
export function fiscalYearStart(date: Date, startMonth?: number | null): Date {
  const month = startMonth && startMonth >= 1 && startMonth <= 12 ? startMonth : 7;
  const start = new Date(date.getFullYear(), month - 1, 1);
  return start > date ? new Date(date.getFullYear() - 1, month - 1, 1) : start;
}
