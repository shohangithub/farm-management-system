import { translate } from '../../../core/i18n/i18n.service';

/**
 * Animal age in whole calendar months from date of birth — the one definition used across the
 * livestock screens (mirrors the backend `AnimalAge` helper used by the reports).
 *
 * An animal born on 31 Jan is one month old on 28/29 Feb. Days ÷ 30 and days ÷ 365 drifted from
 * each other and could print "12m"; the month remainder here is always 0–11.
 */
export function completedMonths(dob: string | Date, asOf: Date = new Date()): number {
  const birth = toDate(dob);
  if (!birth || asOf <= birth) return 0;

  let months = (asOf.getFullYear() - birth.getFullYear()) * 12 + (asOf.getMonth() - birth.getMonth());
  const lastDayOfMonth = new Date(asOf.getFullYear(), asOf.getMonth() + 1, 0).getDate();
  if (asOf.getDate() < birth.getDate() && asOf.getDate() < lastDayOfMonth) {
    months--;
  }
  return Math.max(0, months);
}

/** Whole days of life (never negative). */
export function ageInDays(dob: string | Date, asOf: Date = new Date()): number {
  const birth = toDate(dob);
  if (!birth) return 0;
  const a = Date.UTC(asOf.getFullYear(), asOf.getMonth(), asOf.getDate());
  const b = Date.UTC(birth.getFullYear(), birth.getMonth(), birth.getDate());
  return Math.max(0, Math.floor((a - b) / 86_400_000));
}

/** Compact label: "12d" under a month, "7mo" under a year, otherwise "2y" or "2y 3m". */
export function formatAnimalAge(dob: string | Date, asOf: Date = new Date()): string {
  const months = completedMonths(dob, asOf);
  if (months === 0) return translate('{n}d', { n: ageInDays(dob, asOf) });
  if (months < 12) return translate('{n}mo', { n: months });
  const years = Math.floor(months / 12);
  const rem = months % 12;
  return rem > 0 ? translate('{y}y {m}m', { y: years, m: rem }) : translate('{y}y', { y: years });
}

/** Parses an ISO date ("2024-01-31" or a full timestamp) as a local calendar date. */
function toDate(value: string | Date): Date | null {
  if (value instanceof Date) return isNaN(value.getTime()) ? null : value;
  if (!value) return null;
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (m) return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  const d = new Date(value);
  return isNaN(d.getTime()) ? null : d;
}
