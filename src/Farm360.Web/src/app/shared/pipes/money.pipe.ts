import { Pipe, PipeTransform } from '@angular/core';
import { APP_NUMBER_LOCALE, CURRENCY_SYMBOL } from '../utils/locale';

const formatters = new Map<string, Intl.NumberFormat>();

function formatter(min: number, max: number): Intl.NumberFormat {
  const key = `${min}:${max}`;
  let f = formatters.get(key);
  if (!f) {
    f = new Intl.NumberFormat(APP_NUMBER_LOCALE, { minimumFractionDigits: min, maximumFractionDigits: max });
    formatters.set(key, f);
  }
  return f;
}

/** Formats an amount as Bangladeshi Taka: `৳ 1,23,456`. Usable outside templates too. */
export function formatMoney(value: number | string | null | undefined, decimals = 0, symbol: boolean | string = true): string {
  if (value === null || value === undefined || value === '') return '—';
  const n = typeof value === 'number' ? value : Number(value);
  if (!Number.isFinite(n)) return '—';
  const d = Math.max(0, Math.min(4, decimals));
  const body = formatter(d, d).format(Math.abs(n));
  // Non-breaking space: "৳" never wraps away from its amount in narrow table cells.
  const sym = symbol === false ? '' : (symbol === true ? CURRENCY_SYMBOL : symbol) + ' ';
  // No sign for an amount that rounds to zero ("-৳ 0" reads as a debt that isn't there).
  const negative = n < 0 && Number(Math.abs(n).toFixed(d)) !== 0;
  return `${negative ? '-' : ''}${sym}${body}`;
}

/**
 * `{{ amount | money }}` → `৳ 1,23,456`; `{{ amount | money:2 }}` → `৳ 1,23,456.50`;
 * `{{ amount | money:0:false }}` → `1,23,456`. null/undefined/NaN → `—`.
 */
@Pipe({ name: 'money', standalone: true })
export class MoneyPipe implements PipeTransform {
  transform(value: number | string | null | undefined, decimals = 0, symbol: boolean | string = true): string {
    return formatMoney(value, decimals, symbol);
  }
}
