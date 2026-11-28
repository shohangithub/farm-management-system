/**
 * Tape-measure live-weight estimate — a preview mirror of the server's TapeWeight
 * (Farm360.Domain.Livestock.TapeWeight). The server recomputes the kg itself; this is only
 * for showing the farmer the number before saving.
 *
 * weight (kg) = length (in) × girth (in)² ÷ 660, rounded to 0.1 kg.
 */
export const TAPE_DIVISOR = 660;
export const TAPE_MIN_INCH = 20;
export const TAPE_MAX_INCH = 120;
export const CM_PER_INCH = 2.54;

export type WeightMode = 'Scale' | 'Tape';
export type TapeUnit = 'in' | 'cm';

/** Converts an entered measurement to inches (2 decimals, as the server stores it). */
export function toInches(value: number, unit: TapeUnit): number {
  const inches = unit === 'cm' ? value / CM_PER_INCH : value;
  return Math.round(inches * 100) / 100;
}

export function isPlausibleInch(inches: number | null | undefined): boolean {
  return inches != null && inches >= TAPE_MIN_INCH && inches <= TAPE_MAX_INCH;
}

/** kg rounded to 1 decimal, half away from zero (matches the server). */
export function estimateTapeKg(lengthInch: number, girthInch: number): number {
  const raw = (lengthInch * girthInch * girthInch) / TAPE_DIVISOR;
  return Math.round(raw * 10) / 10;
}

const MODE_KEY = 'farm360_weight_mode';
const UNIT_KEY = 'farm360_tape_unit';

export function loadWeightMode(): WeightMode {
  try {
    return localStorage.getItem(MODE_KEY) === 'Tape' ? 'Tape' : 'Scale';
  } catch {
    return 'Scale';
  }
}

export function saveWeightMode(mode: WeightMode): void {
  try {
    localStorage.setItem(MODE_KEY, mode);
  } catch {
    /* storage unavailable: the choice just isn't remembered */
  }
}

export function loadTapeUnit(): TapeUnit {
  try {
    return localStorage.getItem(UNIT_KEY) === 'cm' ? 'cm' : 'in';
  } catch {
    return 'in';
  }
}

export function saveTapeUnit(unit: TapeUnit): void {
  try {
    localStorage.setItem(UNIT_KEY, unit);
  } catch {
    /* storage unavailable */
  }
}
