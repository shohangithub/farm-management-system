/** GET /api/v1/farms/{farmId}/weather — see Farm360.Application.Weather.FarmWeatherDto. */

export type HeatStressBand = 'Comfortable' | 'Mild' | 'Moderate' | 'Severe' | 'Danger';
export type WeatherAdviceSeverity = 'Good' | 'Info' | 'Warning' | 'Danger';
export type WeatherWindow = 'today' | 'tomorrow' | 'later';

export interface WeatherCurrent {
  time: string;
  temperatureC: number;
  feelsLikeC: number;
  humidityPercent: number;
  precipitationMm: number;
  windSpeedKmh: number;
  windGustsKmh: number;
  weatherCode: number;
  condition: string;
  conditionBn: string;
  icon: string;
  thi: number;
  band: HeatStressBand;
}

export interface WeatherHour {
  time: string;
  temperatureC: number;
  humidityPercent: number;
  rainChance?: number | null;
  thi: number;
  band: HeatStressBand;
}

export interface WeatherDay {
  date: string;
  maxTemperatureC: number;
  minTemperatureC: number;
  rainMm: number;
  rainChance?: number | null;
  weatherCode: number;
  condition: string;
  conditionBn: string;
  icon: string;
  windMaxKmh: number;
  gustMaxKmh?: number | null;
  humidityPercent?: number | null;
  peakThi: number;
  peakBand: HeatStressBand;
  hotFrom?: string | null;
  hotTo?: string | null;
}

export interface WeatherAdvice {
  code: string;
  rule: string;
  severity: WeatherAdviceSeverity;
  icon: string;
  /** English text = translation key; `{name}` placeholders come from params. */
  title: string;
  action: string;
  params: Record<string, string>;
  window: WeatherWindow;
  fromDate: string;
  toDate: string;
}

export interface WeatherBestHours {
  date: string;
  isToday: boolean;
  hours: WeatherHour[];
}

export interface WeatherReport {
  localNow: string;
  today: string;
  current: WeatherCurrent;
  peak48Hours?: { time: string; thi: number; band: HeatStressBand } | null;
  middayThi?: number | null;
  middayBand?: HeatStressBand | null;
  hourly: WeatherHour[];
  daily: WeatherDay[];
  advice: WeatherAdvice[];
  topAdvice: WeatherAdvice[];
  bestHours?: WeatherBestHours | null;
  alerts: { kind: string; window: WeatherWindow; date: string; value: string }[];
}

export interface FarmWeather {
  farmId: string;
  branchId: string;
  needsLocation: boolean;
  available: boolean;
  provider?: string | null;
  location?: { latitude: number; longitude: number } | null;
  fetchedAtUtc?: string | null;
  report?: WeatherReport | null;
}
