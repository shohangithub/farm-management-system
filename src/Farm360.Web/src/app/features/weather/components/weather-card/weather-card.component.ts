import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { map, of, startWith, switchMap } from 'rxjs';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { WeatherService } from '../../services/weather.service';
import { FarmWeather, WeatherAdvice } from '../../models/weather.model';
import { BAND_LABEL, BAND_PILL, SEVERITY_ICON_CLASS, adviceAction, adviceTitle, round } from '../../weather-ui';

type State = { loading: true } | { loading: false; data: FarmWeather | null };

/**
 * Dashboard card: now, today's max/min, the cattle heat-stress band and the 1–2 most urgent tips.
 * Without a farm location it asks for one; when the provider fails it says "Weather unavailable".
 */
@Component({
  selector: 'app-weather-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterModule, MatIconModule, TranslatePipe],
  template: `
    @if (farmId()) {
      <section aria-labelledby="weather-card-heading"
        class="bg-white dark:bg-gray-800/80 rounded-2xl border border-gray-200/70 dark:border-gray-700/60 shadow-sm overflow-hidden">
        <div class="px-4 py-3 flex items-center gap-3 border-b border-gray-100 dark:border-gray-700/60">
          <div class="w-10 h-10 rounded-xl flex items-center justify-center bg-primary-50 text-primary-600 dark:bg-primary-500/10 dark:text-primary-300" aria-hidden="true">
            <mat-icon class="!w-5 !h-5 !text-[20px]">{{ weather()?.report?.current?.icon || 'wb_sunny' }}</mat-icon>
          </div>
          <h2 id="weather-card-heading" class="text-base font-bold text-gray-900 dark:text-white m-0 flex-1">{{ 'Farm weather' | t }}</h2>
          @if (weather()?.available) {
            <a routerLink="/weather" class="inline-flex items-center gap-1 h-11 px-3 rounded-xl text-sm font-semibold text-primary-700 dark:text-primary-300 hover:bg-primary-50 dark:hover:bg-primary-500/10">
              {{ 'Full forecast' | t }} <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">chevron_right</mat-icon>
            </a>
          }
        </div>

        @if (loading()) {
          <div class="p-4 animate-pulse flex gap-4" aria-busy="true">
            <div class="h-14 w-24 rounded-lg bg-gray-100 dark:bg-gray-700"></div>
            <div class="flex-1 space-y-2"><div class="h-3 w-40 rounded bg-gray-100 dark:bg-gray-700"></div><div class="h-3 w-56 rounded bg-gray-100 dark:bg-gray-700/60"></div></div>
          </div>
        } @else if (weather(); as w) {
          @if (w.needsLocation) {
            <div class="p-4 flex flex-wrap items-center gap-3">
              <mat-icon class="text-gray-400" aria-hidden="true">location_off</mat-icon>
              <p class="m-0 flex-1 min-w-[12rem] text-sm text-gray-700 dark:text-gray-200">{{ "Set your farm's location to see weather" | t }}</p>
              <a [routerLink]="['/organizations/branches', w.branchId, 'farms', w.farmId, 'edit']"
                class="inline-flex items-center gap-1 h-11 px-4 rounded-xl border border-primary-200 dark:border-primary-800 text-primary-700 dark:text-primary-300 text-sm font-semibold hover:bg-primary-50 dark:hover:bg-primary-500/10">
                <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">edit_location_alt</mat-icon> {{ 'Set location' | t }}
              </a>
            </div>
          } @else if (w.available && w.report; as r) {
            <div class="p-4 space-y-4">
              <div class="flex flex-wrap items-center gap-x-6 gap-y-3">
                <div class="flex items-center gap-3">
                  <span class="text-4xl font-semibold tracking-tight tabular-nums text-gray-900 dark:text-white">{{ round(r.current.temperatureC) }}°</span>
                  <div class="text-sm leading-tight">
                    <p class="m-0 font-semibold text-gray-800 dark:text-gray-100">{{ r.current.condition | t }}</p>
                    <p class="m-0 text-gray-500 dark:text-gray-400">{{ 'Feels like {t}°' | t: { t: round(r.current.feelsLikeC) } }}</p>
                  </div>
                </div>
                <dl class="grid grid-cols-3 gap-x-5 gap-y-1 text-sm m-0">
                  <div><dt class="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{{ 'Humidity' | t }}</dt><dd class="m-0 font-semibold tabular-nums text-gray-900 dark:text-white">{{ round(r.current.humidityPercent) }}%</dd></div>
                  @if (r.daily[0]; as d) {
                    <div><dt class="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{{ 'Max' | t }}</dt><dd class="m-0 font-semibold tabular-nums text-gray-900 dark:text-white">{{ round(d.maxTemperatureC) }}°</dd></div>
                    <div><dt class="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{{ 'Min' | t }}</dt><dd class="m-0 font-semibold tabular-nums text-gray-900 dark:text-white">{{ round(d.minTemperatureC) }}°</dd></div>
                  }
                </dl>
                <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold ring-1 ring-inset" [ngClass]="pill()">
                  <mat-icon class="!text-[16px] !w-[16px] !h-[16px]" aria-hidden="true">thermostat</mat-icon>
                  {{ 'Heat stress: {band} (THI {thi})' | t: { band: (bandLabel() | t), thi: r.current.thi } }}
                </span>
              </div>

              @if (r.topAdvice.length) {
                <ul class="m-0 p-0 list-none space-y-2">
                  @for (a of r.topAdvice; track a.code + a.fromDate) {
                    <li class="flex gap-3 p-3 rounded-xl bg-gray-50/70 dark:bg-gray-900/30 border border-gray-100 dark:border-gray-700/60">
                      <span class="w-8 h-8 shrink-0 rounded-full flex items-center justify-center" [ngClass]="severityClass(a)" aria-hidden="true">
                        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">{{ a.icon }}</mat-icon>
                      </span>
                      <div class="min-w-0">
                        <p class="m-0 text-sm font-semibold text-gray-900 dark:text-white">
                          {{ title(a) }}
                          <span class="ml-1 text-[11px] font-semibold text-gray-500 dark:text-gray-400">· {{ (a.window === 'today' ? 'Today' : 'Tomorrow') | t }}</span>
                        </p>
                        <p class="m-0 mt-0.5 text-sm text-gray-600 dark:text-gray-300">{{ action(a) }}</p>
                      </div>
                    </li>
                  }
                </ul>
              }
            </div>
          } @else {
            <p class="m-0 p-4 text-sm text-gray-500 dark:text-gray-400 inline-flex items-center gap-2">
              <mat-icon class="!text-[18px] !w-[18px] !h-[18px]" aria-hidden="true">cloud_off</mat-icon> {{ 'Weather unavailable' | t }}
            </p>
          }
        } @else {
          <p class="m-0 p-4 text-sm text-gray-500 dark:text-gray-400 inline-flex items-center gap-2">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px]" aria-hidden="true">cloud_off</mat-icon> {{ 'Weather unavailable' | t }}
          </p>
        }
      </section>
    }
  `,
})
export class WeatherCardComponent {
  private readonly weatherService = inject(WeatherService);
  private readonly context = inject(WorkingContextService);
  private readonly i18n = inject(I18nService);
  private readonly farm = toSignal(this.context.currentFarm$, { initialValue: null });

  readonly farmId = computed(() => this.farm()?.id ?? '');

  private readonly state = toSignal(
    toObservable(this.farmId).pipe(
      switchMap(id => id
        ? this.weatherService.getFarmWeather(id).pipe(map((data): State => ({ loading: false, data })), startWith<State>({ loading: true }))
        : of<State>({ loading: false, data: null }))),
    { initialValue: { loading: true } as State });

  readonly loading = computed(() => this.state().loading);
  readonly weather = computed(() => { const s = this.state(); return s.loading ? null : s.data; });

  readonly pill = computed(() => BAND_PILL[this.weather()?.report?.current.band ?? 'Comfortable']);
  readonly bandLabel = computed(() => BAND_LABEL[this.weather()?.report?.current.band ?? 'Comfortable']);

  readonly round = round;
  title(a: WeatherAdvice): string { return adviceTitle(a); }
  action(a: WeatherAdvice): string { return adviceAction(a); }
  severityClass(a: WeatherAdvice): string { return SEVERITY_ICON_CLASS[a.severity]; }
}
