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
import { FarmMapComponent, PickedLocation } from '../../../../shared/components/farm-map/farm-map.component';
import { CanPipe } from '../../../../shared/pipes/can.pipe';
import { NotificationService } from '../../../../shared/services/notification.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { FarmService } from '../../../farms/services/farm.service';
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
  imports: [CommonModule, RouterModule, MatIconModule, TranslatePipe, CanPipe, PageHeaderComponent, FarmMapComponent],
  templateUrl: './weather-page.component.html',
})
export class WeatherPageComponent {
  private readonly weatherService = inject(WeatherService);
  private readonly context = inject(WorkingContextService);
  private readonly i18n = inject(I18nService);
  private readonly farms = inject(FarmService);
  private readonly notify = inject(NotificationService);
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

  // ── Set / change the farm location here (users with farms.edit) ──────────────────────────────────
  /** True while an existing location is being changed (the map turns editable). */
  readonly editingLocation = signal(false);
  /** The point picked on the map or from GPS, not saved yet. */
  readonly draftLocation = signal<PickedLocation | null>(null);
  readonly savingLocation = signal(false);
  readonly locating = signal(false);
  readonly locationError = signal<string | null>(null);

  startLocationEdit(): void {
    this.draftLocation.set(null);
    this.locationError.set(null);
    this.editingLocation.set(true);
  }

  cancelLocationEdit(): void {
    this.draftLocation.set(null);
    this.locationError.set(null);
    this.editingLocation.set(false);
  }

  onLocationPicked(point: PickedLocation): void {
    this.locationError.set(null);
    this.draftLocation.set(point);
  }

  /** Fill the point from the device's GPS / browser location (stand at the farm). */
  useCurrentLocation(): void {
    this.locationError.set(null);
    if (typeof navigator === 'undefined' || !navigator.geolocation) {
      this.locationError.set(this.i18n.t('This browser cannot share its location. Copy the point from Google Maps instead.'));
      return;
    }
    this.locating.set(true);
    navigator.geolocation.getCurrentPosition(
      position => {
        this.locating.set(false);
        this.draftLocation.set({
          lat: Math.round(position.coords.latitude * 1e6) / 1e6,
          lon: Math.round(position.coords.longitude * 1e6) / 1e6,
        });
      },
      err => {
        this.locating.set(false);
        this.locationError.set(this.i18n.t(err.code === err.PERMISSION_DENIED
          ? 'Location permission was refused. Allow it in the browser, or copy the point from Google Maps.'
          : 'Could not read your location. Try again outdoors, or copy the point from Google Maps.'));
      },
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 60000 }
    );
  }

  /** Saves the picked point, then loads the weather again (the server's forecast cache is keyed by the coordinates). */
  saveLocation(): void {
    const id = this.farmId();
    const point = this.draftLocation();
    if (!id || !point || this.savingLocation()) return;
    this.savingLocation.set(true);
    this.farms.setFarmLocation(id, point.lat, point.lon).subscribe({
      next: () => {
        this.savingLocation.set(false);
        this.editingLocation.set(false);
        this.draftLocation.set(null);
        this.notify.success(this.i18n.t('Farm location saved.'));
        this.refresh();
      },
      error: err => {
        this.savingLocation.set(false);
        this.notify.error(parseApiError(err, 'Could not save the farm location.'));
      },
    });
  }

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
