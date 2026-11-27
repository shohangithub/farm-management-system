import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { map, of, startWith, switchMap } from 'rxjs';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { FarmMapComponent } from '../../../../shared/components/farm-map/farm-map.component';
import { WeatherService } from '../../services/weather.service';
import { FarmWeather, HeatStressBand, WeatherAdvice, WeatherDay, WeatherWindow } from '../../models/weather.model';
import {
  BANDS, BAND_LABEL, BAND_PILL, BAND_SWATCH, SEVERITY_ICON_CLASS, adviceAction, adviceTitle, clockOf, dateOf, round, shortDate,
} from '../../weather-ui';

type State = { loading: true } | { loading: false; data: FarmWeather | null };

interface AdviceGroup {
  window: WeatherWindow;
  label: string;
  items: WeatherAdvice[];
}

/**
 * The Weather page: conditions now, the best work hours, a 48-hour strip coloured by cattle heat stress,
 * 7 day cards and all advice grouped by today / tomorrow / later. Phone first; prints on one or two pages.
 */
@Component({
  selector: 'app-weather-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterModule, MatIconModule, TranslatePipe, PageHeaderComponent, FarmMapComponent],
  templateUrl: './weather-page.component.html',
})
export class WeatherPageComponent {
  private readonly weatherService = inject(WeatherService);
  private readonly context = inject(WorkingContextService);
  private readonly i18n = inject(I18nService);
  private readonly farm = toSignal(this.context.currentFarm$, { initialValue: null });
  private readonly refreshTick = signal(0);

  readonly farmId = computed(() => this.farm()?.id ?? '');
  readonly farmName = computed(() => this.farm()?.name ?? '');

  private readonly state = toSignal(
    toObservable(computed(() => ({ id: this.farmId(), tick: this.refreshTick() }))).pipe(
      switchMap(({ id, tick }) => id
        ? this.weatherService.getFarmWeather(id, tick > 0).pipe(map((data): State => ({ loading: false, data })), startWith<State>({ loading: true }))
        : of<State>({ loading: false, data: null }))),
    { initialValue: { loading: true } as State });

  readonly loading = computed(() => this.state().loading);
  readonly weather = computed(() => { const s = this.state(); return s.loading ? null : s.data; });
  readonly report = computed(() => this.weather()?.report ?? null);

  readonly adviceGroups = computed<AdviceGroup[]>(() => {
    const advice = this.report()?.advice ?? [];
    const groups: AdviceGroup[] = [
      { window: 'today', label: 'Today', items: advice.filter(a => a.window === 'today') },
      { window: 'tomorrow', label: 'Tomorrow', items: advice.filter(a => a.window === 'tomorrow') },
      { window: 'later', label: 'Later this week', items: advice.filter(a => a.window === 'later') },
    ];
    return groups.filter(g => g.items.length > 0);
  });

  readonly bands = BANDS;
  readonly round = round;
  readonly clock = clockOf;

  refresh(): void { this.refreshTick.update(v => v + 1); }
  print(): void { window.print(); }

  bandPill(band: HeatStressBand | null | undefined): string { return BAND_PILL[band ?? 'Comfortable']; }
  bandSwatch(band: HeatStressBand): string { return BAND_SWATCH[band]; }
  bandLabel(band: HeatStressBand | null | undefined): string { return BAND_LABEL[band ?? 'Comfortable']; }
  severityClass(a: WeatherAdvice): string { return SEVERITY_ICON_CLASS[a.severity]; }
  title(a: WeatherAdvice): string { return adviceTitle(a); }
  action(a: WeatherAdvice): string { return adviceAction(a); }

  /** "Today", "Tomorrow" or "Mon 1 Dec". */
  dayName(day: WeatherDay): string {
    const today = this.report()?.today ?? '';
    if (day.date === today) return this.i18n.t('Today');
    const tomorrow = this.addDays(today, 1);
    if (day.date === tomorrow) return this.i18n.t('Tomorrow');
    return shortDate(day.date, this.i18n.isBangla);
  }

  /** Date range of a "later" advice item ("Mon 1 Dec" or "Mon 1 Dec – Wed 3 Dec"). */
  range(a: WeatherAdvice): string {
    const from = shortDate(a.fromDate, this.i18n.isBangla);
    return a.toDate && a.toDate !== a.fromDate ? `${from} – ${shortDate(a.toDate, this.i18n.isBangla)}` : from;
  }

  /** The hour strip label: "16:00", and the day under the first hour of each new day. */
  isNewDay(index: number): boolean {
    const hours = this.report()?.hourly ?? [];
    return index === 0 || dateOf(hours[index].time) !== dateOf(hours[index - 1].time);
  }

  hourDay(time: string): string {
    const date = dateOf(time);
    if (date === this.report()?.today) return this.i18n.t('Today');
    return shortDate(date, this.i18n.isBangla);
  }

  fetchedAt(): string {
    const at = this.weather()?.fetchedAtUtc;
    if (!at) return '';
    const date = new Date(at.endsWith('Z') ? at : at + 'Z');
    return new Intl.DateTimeFormat(this.i18n.isBangla ? 'bn-BD-u-nu-latn' : 'en-GB', { hour: '2-digit', minute: '2-digit' }).format(date);
  }

  private addDays(isoDate: string, days: number): string {
    const [y, m, d] = isoDate.split('-').map(Number);
    const date = new Date(y, (m || 1) - 1, (d || 1) + days);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  }
}
