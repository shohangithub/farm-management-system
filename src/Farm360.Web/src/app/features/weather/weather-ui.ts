import { HeatStressBand, WeatherAdvice, WeatherAdviceSeverity } from './models/weather.model';
import { translate } from '../../core/i18n/i18n.service';

/**
 * Shared look of the weather feature. Colours: success only for good news (comfortable / good day),
 * amber for warnings, rose for danger; the brand colour (primary) stays for links and actions.
 */
export const BAND_PILL: Record<HeatStressBand, string> = {
  Comfortable: 'bg-success-50 text-success-800 ring-success-200 dark:bg-success-500/10 dark:text-success-300 dark:ring-success-500/30',
  Mild: 'bg-amber-50 text-amber-800 ring-amber-200 dark:bg-amber-500/10 dark:text-amber-300 dark:ring-amber-500/30',
  Moderate: 'bg-amber-100 text-amber-900 ring-amber-300 dark:bg-amber-500/20 dark:text-amber-200 dark:ring-amber-500/40',
  Severe: 'bg-rose-100 text-rose-800 ring-rose-300 dark:bg-rose-500/15 dark:text-rose-300 dark:ring-rose-500/40',
  Danger: 'bg-rose-600 text-white ring-rose-700 dark:bg-rose-600 dark:text-white dark:ring-rose-500',
};

/** Solid swatch per band (hour strip bars, legend dots). */
export const BAND_SWATCH: Record<HeatStressBand, string> = {
  Comfortable: 'bg-success-500',
  Mild: 'bg-amber-300',
  Moderate: 'bg-amber-500',
  Severe: 'bg-rose-500',
  Danger: 'bg-rose-700',
};

export const BAND_LABEL: Record<HeatStressBand, string> = {
  Comfortable: 'Comfortable',
  Mild: 'Mild',
  Moderate: 'Moderate',
  Severe: 'Severe',
  Danger: 'Danger',
};

export const BANDS: HeatStressBand[] = ['Comfortable', 'Mild', 'Moderate', 'Severe', 'Danger'];

const BAND_RANK: Record<HeatStressBand, number> = { Comfortable: 0, Mild: 1, Moderate: 2, Severe: 3, Danger: 4 };

/** True when the band is at least `min` (e.g. moderate or worse). */
export function bandAtLeast(band: HeatStressBand | null | undefined, min: HeatStressBand): boolean {
  return !!band && BAND_RANK[band] >= BAND_RANK[min];
}

export const SEVERITY_ICON_CLASS: Record<WeatherAdviceSeverity, string> = {
  Good: 'bg-success-50 text-success-700 dark:bg-success-500/10 dark:text-success-300',
  Info: 'bg-gray-100 text-gray-600 dark:bg-gray-700/60 dark:text-gray-300',
  Warning: 'bg-amber-50 text-amber-700 dark:bg-amber-500/10 dark:text-amber-300',
  Danger: 'bg-rose-50 text-rose-700 dark:bg-rose-500/10 dark:text-rose-300',
};

/** Translated advice title / action with its parameters filled in. */
export function adviceTitle(a: WeatherAdvice): string {
  return translate(a.title, a.params);
}

export function adviceAction(a: WeatherAdvice): string {
  return translate(a.action, a.params);
}

/** "16:00" from the server's farm-local "2026-11-27T16:00:00" (no time-zone shift). */
export function clockOf(isoLocal: string): string {
  return (isoLocal ?? '').slice(11, 16);
}

/** "2026-11-27" (date part of a farm-local time or a date). */
export function dateOf(isoLocal: string): string {
  return (isoLocal ?? '').slice(0, 10);
}

/** "Fri 28 Nov" in the interface language, digits kept Latin. */
export function shortDate(isoDate: string, bangla: boolean, withWeekday = true): string {
  const [y, m, d] = isoDate.slice(0, 10).split('-').map(Number);
  const date = new Date(y, (m || 1) - 1, d || 1);
  return new Intl.DateTimeFormat(bangla ? 'bn-BD-u-nu-latn' : 'en-GB', {
    weekday: withWeekday ? 'short' : undefined, day: 'numeric', month: 'short',
  }).format(date);
}

export function round(n: number | null | undefined): string {
  return n == null ? '–' : String(Math.round(n));
}
