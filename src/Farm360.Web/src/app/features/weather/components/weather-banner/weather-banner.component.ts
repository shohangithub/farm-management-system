import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of, switchMap } from 'rxjs';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { WeatherService } from '../../services/weather.service';
import { FarmWeather } from '../../models/weather.model';
import { BAND_LABEL, BAND_PILL, bandAtLeast, round } from '../../weather-ui';

/** Which tasks get the "hot day: do it in the morning" hint: vaccinations/medicines and bath, transport or weighing care tasks. */
const HEAT_SENSITIVE_CARE = /bath|wash|shower|transport|move|moving|load|truck|weigh|sell|market|গোসল|পরিবহন|ওজন|স্থানান্তর|বিক্রি/i;

export function isHeatSensitiveTask(task: { kind: string; taskName: string; stepName?: string | null }): boolean {
  if (task.kind === 'Medicine') return true;
  return HEAT_SENSITIVE_CARE.test(`${task.taskName} ${task.stepName ?? ''}`);
}

/** True when today's midday heat stress is moderate or worse. */
export function isHotMidday(weather: FarmWeather | null | undefined): boolean {
  return !!weather?.available && bandAtLeast(weather.report?.middayBand, 'Moderate');
}

/**
 * Small weather line for "Today's tasks": now, today's heat stress and, on a hot day, the tip to do
 * vaccination, bathing and transport in the morning. Hidden when there is no weather; never blocks work.
 */
@Component({
  selector: 'app-weather-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterModule, MatIconModule, TranslatePipe],
  template: `
    @if (weather()?.available && weather()?.report; as r) {
      <a routerLink="/weather"
        class="mb-4 flex flex-wrap items-center gap-x-3 gap-y-2 px-4 py-3 rounded-2xl border transition-colors print:hidden"
        [ngClass]="hot()
          ? 'bg-amber-50 border-amber-200 hover:border-amber-300 dark:bg-amber-500/10 dark:border-amber-500/30'
          : 'bg-white border-gray-200/70 hover:border-gray-300 dark:bg-gray-800/80 dark:border-gray-700/60'">
        <mat-icon class="text-primary-600 dark:text-primary-400" aria-hidden="true">{{ r.current.icon }}</mat-icon>
        <span class="text-sm font-semibold tabular-nums text-gray-900 dark:text-white">{{ round(r.current.temperatureC) }}° · {{ r.current.condition | t }}</span>
        <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-semibold ring-1 ring-inset" [ngClass]="pill()">
          {{ 'Midday heat stress: {band} (THI {thi})' | t: { band: (bandLabel() | t), thi: r.middayThi ?? r.current.thi } }}
        </span>
        @if (hot()) {
          <span class="basis-full sm:basis-auto text-sm text-amber-900 dark:text-amber-200 inline-flex items-center gap-1">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px]" aria-hidden="true">wb_twilight</mat-icon>
            {{ 'Hot day: vaccinate, bathe and move animals in the morning (best before 10:00).' | t }}
          </span>
        }
        <mat-icon class="ml-auto !text-[18px] !w-[18px] !h-[18px] text-gray-400" aria-hidden="true">chevron_right</mat-icon>
      </a>
    }
  `,
})
export class WeatherBannerComponent {
  private readonly weatherService = inject(WeatherService);
  private readonly context = inject(WorkingContextService);
  private readonly farm = toSignal(this.context.currentFarm$, { initialValue: null });

  readonly weather = toSignal(
    toObservable(computed(() => this.farm()?.id ?? '')).pipe(
      switchMap(id => id ? this.weatherService.getFarmWeather(id) : of<FarmWeather | null>(null))),
    { initialValue: null });

  private readonly band = computed(() => this.weather()?.report?.middayBand ?? this.weather()?.report?.current.band ?? 'Comfortable');
  readonly hot = computed(() => isHotMidday(this.weather()));
  readonly pill = computed(() => BAND_PILL[this.band()]);
  readonly bandLabel = computed(() => BAND_LABEL[this.band()]);
  readonly round = round;
}
