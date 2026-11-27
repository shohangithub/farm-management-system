import { Component, ChangeDetectionStrategy, computed, inject, input, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { catchError, debounceTime, distinctUntilChanged, finalize, map, of, switchMap } from 'rxjs';
import { AnimalService } from '../../services/animal.service';
import {
  FatteningPlan,
  FatteningPlanOverrides,
  SellOrKeepAdvice,
  SellOrKeepPoint,
  adgBasisText,
  breedTargetSourceText,
  costBasisText,
  firstWeighingIsArrival,
} from '../../models/sell-or-keep.models';
import { ChartMark, placeChartMarks, shortTaka } from '../sell-or-keep-card/chart-marks';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

type Phrase = { text: string; params?: Record<string, string | number> };
type LeverKey = keyof FatteningPlanOverrides;

/** One what-if input: its default comes from the animal's own figures (the Sell-or-keep advice). */
interface Lever {
  key: LeverKey;
  label: string;
  unit: string;
  step: number;
  min: number;
  max: number;
  decimals: number;
  value: (d: SellOrKeepAdvice) => number | null | undefined;
  source: (d: SellOrKeepAdvice) => Phrase;
}

/**
 * The animal page's "Fattening plan" tab: the Sell-or-keep projection (same engine, price and actual
 * cost run-rate as the "Sell or keep?" card) with what-if levers. With no lever changed every figure
 * equals the card's. Price is per kg LIVE weight; the price resolver already applies the farm's
 * meat-yield rule when the farm uses it.
 */
@Component({
  selector: 'app-fattening-plan',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, MatIconModule, MoneyPipe, DatePipe, DecimalPipe, TranslatePipe],
  template: `
    <div class="flex flex-col gap-5 p-4 sm:p-6">
      <p *ngIf="!current() && loading()" class="text-sm text-gray-500 dark:text-gray-400">{{ 'Working out the plan…' | t }}</p>
      <div *ngIf="error()" class="flex items-center gap-2 text-sm rounded-xl px-4 py-3 bg-rose-50 text-rose-700 border border-rose-200 dark:bg-rose-900/20 dark:text-rose-300 dark:border-rose-800" role="alert">
        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]" aria-hidden="true">error</mat-icon>{{ error() }}
      </div>

      <ng-container *ngIf="current() as fp">
        <ng-container *ngIf="fp.plan as p">
          <div class="grid grid-cols-1 xl:grid-cols-[minmax(0,340px)_minmax(0,1fr)] gap-5 items-start">

            <!-- ── What-if levers ── -->
            <section class="bg-white dark:bg-surface-dark rounded-2xl shadow-sm border border-gray-200/70 dark:border-gray-800 p-4 sm:p-5" [attr.aria-label]="'What-if' | t">
              <div class="flex items-center justify-between gap-2">
                <div class="flex items-center gap-2">
                  <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-gray-400" aria-hidden="true">tune</mat-icon>
                  <h3 class="text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 m-0">{{ 'What-if' | t }}</h3>
                </div>
                <button type="button" (click)="resetAll()" [disabled]="!changedCount()"
                  class="h-8 px-3 rounded-lg text-xs font-semibold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/60 disabled:opacity-40 disabled:cursor-not-allowed inline-flex items-center gap-1.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-500">
                  <mat-icon class="!text-[16px] !w-[16px] !h-[16px]" aria-hidden="true">restart_alt</mat-icon>{{ 'Reset' | t }}
                </button>
              </div>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-1.5 mb-0">{{ 'Starts from this animal\\'s own records — the same figures as the "Sell or keep?" card. Change any value to see what happens.' | t }}</p>

              <div class="mt-4 flex flex-col gap-3.5">
                <div *ngFor="let l of levers">
                  <div class="flex items-baseline justify-between gap-2">
                    <label [for]="'lever-' + l.key" class="text-xs font-medium text-gray-700 dark:text-gray-300">{{ l.label | t }}</label>
                    <button *ngIf="isChanged(l.key)" type="button" (click)="resetOne(l.key)"
                      class="text-[11px] font-semibold text-primary-700 dark:text-primary-300 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-500 rounded">{{ 'Use default' | t }}</button>
                  </div>
                  <div class="mt-1 flex items-center gap-2">
                    <input [id]="'lever-' + l.key" type="number" inputmode="decimal" [step]="l.step" [min]="l.min" [max]="l.max"
                      [value]="display(l, fp.defaults)" (input)="onLever(l, $any($event.target).value, fp.defaults)"
                      class="w-full h-10 bg-white dark:bg-gray-900 border rounded-xl px-3 text-sm tabular-nums text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-primary-500/30 focus:border-primary-500"
                      [ngClass]="isChanged(l.key) ? 'border-primary-400 dark:border-primary-600' : 'border-gray-300 dark:border-gray-600'" />
                    <span class="text-xs text-gray-500 dark:text-gray-400 whitespace-nowrap">{{ l.unit | t }}</span>
                  </div>
                  <p class="text-[11px] text-gray-500 dark:text-gray-400 mt-1 mb-0 leading-snug">
                    <ng-container *ngIf="isChanged(l.key); else src">{{ 'Default {value} — {source}' | t: { value: format(l, l.value(fp.defaults)), source: (l.source(fp.defaults).text | t: l.source(fp.defaults).params) } }}</ng-container>
                    <ng-template #src>{{ l.source(fp.defaults).text | t: l.source(fp.defaults).params }}</ng-template>
                  </p>
                </div>
              </div>
            </section>

            <div class="flex flex-col gap-5 min-w-0 transition-opacity" [class.opacity-60]="loading()">
              <!-- ── Verdict ── -->
              <div class="rounded-xl border px-3 py-3 flex items-start gap-3" [ngClass]="verdictBox(p)">
                <mat-icon class="!text-[22px] !w-[22px] !h-[22px] shrink-0 mt-0.5" aria-hidden="true">{{ verdictIcon(p) }}</mat-icon>
                <div class="min-w-0">
                  <p class="text-base font-bold leading-snug m-0">{{ verdictTitle(p).text | t: verdictTitle(p).params }}</p>
                  <p class="text-sm mt-1 m-0 text-gray-700 dark:text-gray-300">{{ isBangla ? p.reasonBn : p.reasonEn }}</p>
                  <p *ngIf="changedCount()" class="text-xs mt-1 m-0 text-gray-600 dark:text-gray-400">{{ '{count} what-if value(s) changed — the "Sell or keep?" card still shows your records.' | t: { count: changedCount() } }}</p>
                </div>
              </div>

              <!-- ── Which growth rate ── -->
              <div class="text-xs text-gray-600 dark:text-gray-400 bg-gray-50 dark:bg-gray-900/40 rounded-lg px-3 py-2 border border-gray-100 dark:border-gray-800 flex flex-col gap-1">
                <span class="flex items-start gap-1.5">
                  <mat-icon class="!text-[14px] !w-[14px] !h-[14px] mt-px shrink-0 text-gray-400" aria-hidden="true">trending_up</mat-icon>
                  <span>
                    <ng-container *ngIf="p.projectionAdgKgPerDay != null; else noGrowth">{{ 'The projection uses {adg} kg/day ({basis}).' | t: { adg: (p.projectionAdgKgPerDay | number:'1.2-3'), basis: (adgBasis(p).text | t: adgBasis(p).params) } }}</ng-container>
                    <ng-template #noGrowth>{{ 'No gain per day yet: weigh it twice on different days, or type a gain per day.' | t }}</ng-template>
                    <ng-container *ngIf="p.lifetimeAdgKgPerDay != null && p.growthSource === 'Recent'"> {{ (sinceArrival(p) ? 'Since arrival: {adg} kg/day.' : 'Since the first weighing: {adg} kg/day.') | t: { adg: (p.lifetimeAdgKgPerDay | number:'1.2-3') } }}</ng-container>
                    {{ 'Growth slows by {pct}% over {days} days as it gets heavier.' | t: { pct: (p.growthSlowdownPercent | number:'1.0-1'), days: p.growthDeclineDays } }}
                  </span>
                </span>
                <span *ngIf="p.breedTargetAdgKg" class="flex items-start gap-1.5">
                  <mat-icon class="!text-[14px] !w-[14px] !h-[14px] mt-px shrink-0 text-gray-400" aria-hidden="true">flag</mat-icon>
                  <span>{{ breedTarget(p).text | t: breedTarget(p).params }} = {{ p.breedTargetAdgKg | number:'1.2-2' }} {{ 'kg/day' | t }}</span>
                </span>
              </div>

              <!-- ── Results ── -->
              <dl class="grid grid-cols-2 lg:grid-cols-3 gap-3 m-0">
                <div class="rounded-xl border border-gray-200/70 dark:border-gray-800 bg-white dark:bg-surface-dark p-3">
                  <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Profit if sold today' | t }}</dt>
                  <dd class="m-0 text-lg font-bold tabular-nums" [ngClass]="profitClass(p.profitTodayBdt)">{{ p.profitTodayBdt | money }}</dd>
                  <dd class="m-0 text-[11px] text-gray-500 dark:text-gray-400">{{ '{kg} kg × {price}/kg' | t: { kg: (p.weightTodayKg | number:'1.0-1'), price: (p.price.pricePerKgBdt | money:2) } }}<span *ngIf="p.price.perHeadBdt"> + {{ p.price.perHeadBdt | money }}</span> − {{ p.investedBdt | money }}</dd>
                </div>
                <div class="rounded-xl border border-gray-200/70 dark:border-gray-800 bg-white dark:bg-surface-dark p-3">
                  <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Best day to sell' | t }}</dt>
                  <dd class="m-0 text-lg font-bold tabular-nums text-gray-900 dark:text-white">
                    <ng-container *ngIf="p.bestDate; else dash">{{ p.bestDay === 0 ? ('Today' | t) : (p.bestDate | date:'d MMM yyyy') }}</ng-container>
                  </dd>
                  <dd *ngIf="p.bestDate" class="m-0 text-[11px] text-gray-500 dark:text-gray-400">{{ '{kg} kg · profit' | t: { kg: (p.bestWeightKg | number:'1.0-1') } }} <span class="font-semibold" [ngClass]="profitClass(p.profitAtBestDayBdt)">{{ p.profitAtBestDayBdt | money }}</span></dd>
                </div>
                <div class="rounded-xl border border-gray-200/70 dark:border-gray-800 bg-white dark:bg-surface-dark p-3">
                  <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Break-even day' | t }}</dt>
                  <dd class="m-0 text-lg font-bold tabular-nums text-gray-900 dark:text-white">
                    <ng-container *ngIf="p.breakEvenDate; else noEven">{{ p.breakEvenDate | date:'d MMM yyyy' }}</ng-container>
                    <ng-template #noEven>{{ ((p.profitTodayBdt ?? 0) >= 0 ? 'Already in profit' : 'Not within {days} days') | t: { days: p.horizonDays } }}</ng-template>
                  </dd>
                  <dd *ngIf="p.breakEvenDay != null" class="m-0 text-[11px] text-gray-500 dark:text-gray-400">{{ 'in {days} days' | t: { days: p.breakEvenDay } }}</dd>
                </div>
                <div class="rounded-xl border border-gray-200/70 dark:border-gray-800 bg-white dark:bg-surface-dark p-3">
                  <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Return at the best day' | t }}</dt>
                  <dd class="m-0 text-lg font-bold tabular-nums" [ngClass]="profitClass(p.roiAtBestDayPercent)">
                    <ng-container *ngIf="p.roiAtBestDayPercent != null; else dash">{{ p.roiAtBestDayPercent | number:'1.1-1' }}%</ng-container>
                  </dd>
                  <dd class="m-0 text-[11px] text-gray-500 dark:text-gray-400">{{ 'profit ÷ total cost' | t }}</dd>
                </div>
                <div class="rounded-xl border border-gray-200/70 dark:border-gray-800 bg-white dark:bg-surface-dark p-3">
                  <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Cost per kg gained' | t }}</dt>
                  <dd class="m-0 text-lg font-bold tabular-nums" [ngClass]="p.costPerKgGainBdt != null && p.costPerKgGainBdt > p.price.pricePerKgBdt ? 'text-rose-700 dark:text-rose-400' : 'text-gray-900 dark:text-white'">
                    <ng-container *ngIf="p.costPerKgGainBdt != null; else dash">{{ p.costPerKgGainBdt | money }}</ng-container>
                  </dd>
                  <dd class="m-0 text-[11px] text-gray-500 dark:text-gray-400">{{ '{cost}/day ÷ {adg} kg/day; sells for {price}/kg' | t: { cost: (p.dailyCostBdt | money), adg: (p.projectionAdgKgPerDay | number:'1.2-3'), price: (p.price.pricePerKgBdt | money) } }}</dd>
                </div>
                <div class="rounded-xl border border-gray-200/70 dark:border-gray-800 bg-white dark:bg-surface-dark p-3">
                  <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Break-even live price' | t }}</dt>
                  <dd class="m-0 text-lg font-bold tabular-nums text-gray-900 dark:text-white">
                    <ng-container *ngIf="p.breakEvenPricePerKgAtHorizonBdt != null; else dash">{{ p.breakEvenPricePerKgAtHorizonBdt | money:2 }}</ng-container>
                  </dd>
                  <dd *ngIf="horizonPoint(p) as h" class="m-0 text-[11px] text-gray-500 dark:text-gray-400">{{ 'per kg live, to sell on {date} (day {day}) at {kg} kg' | t: { date: (h.date | date:'d MMM yyyy'), day: h.day, kg: (h.weightKg | number:'1.0-1') } }}</dd>
                </div>
              </dl>

              <!-- ── Cost vs value chart ── -->
              <section *ngIf="chart() as c" class="bg-white dark:bg-surface-dark rounded-2xl shadow-sm border border-gray-200/70 dark:border-gray-800 p-4">
                <div class="flex flex-wrap items-center justify-between gap-2">
                  <h3 class="text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 m-0">{{ 'Total cost vs value' | t }}</h3>
                  <div class="flex items-center gap-4 text-xs text-gray-600 dark:text-gray-300">
                    <span class="inline-flex items-center gap-1.5"><span class="w-4 h-0.5 rounded bg-primary-600 dark:bg-primary-400" aria-hidden="true"></span>{{ 'Value if sold' | t }}</span>
                    <span class="inline-flex items-center gap-1.5"><span class="w-4 h-0.5 rounded bg-amber-600 dark:bg-amber-400" aria-hidden="true"></span>{{ 'Total cost' | t }}</span>
                  </div>
                </div>
                <div class="relative mt-2">
                  <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" class="w-full h-64 overflow-visible" role="img"
                       [attr.aria-label]="'Total cost and value over the coming days, with today, the best day and break-even marked' | t"
                       (mousemove)="onHover($event)" (mouseleave)="hover.set(null)">
                    <!-- grid + y axis -->
                    <g *ngFor="let t of c.yTicks">
                      <line [attr.x1]="PAD_L" [attr.x2]="W - PAD_R" [attr.y1]="t.y" [attr.y2]="t.y" class="stroke-gray-100 dark:stroke-gray-800" stroke-width="1" />
                      <text [attr.x]="PAD_L - 6" [attr.y]="t.y + 3" text-anchor="end" class="fill-gray-400 text-[10px] tabular-nums">{{ t.label }}</text>
                    </g>
                    <!-- x axis -->
                    <g *ngFor="let t of c.xTicks">
                      <text [attr.x]="t.x" [attr.y]="H - 4" [attr.text-anchor]="t.anchor" class="fill-gray-400 text-[10px]">{{ t.date | date:'d MMM' }}</text>
                    </g>
                    <!-- profit / loss gap -->
                    <polygon [attr.points]="c.gap" class="fill-gray-200/50 dark:fill-gray-700/30" />
                    <polyline [attr.points]="c.cost" fill="none" class="stroke-amber-600 dark:stroke-amber-400" stroke-width="2" stroke-linejoin="round" />
                    <polyline [attr.points]="c.value" fill="none" class="stroke-primary-600 dark:stroke-primary-400" stroke-width="2" stroke-linejoin="round" />
                    <!-- direct labels at the end of each line -->
                    <text [attr.x]="W - PAD_R + 4" [attr.y]="c.valueEndY + (c.valueEndY < c.costEndY ? -2 : 10)" class="fill-gray-600 dark:fill-gray-300 text-[10px] font-semibold">{{ 'Value' | t }}</text>
                    <text [attr.x]="W - PAD_R + 4" [attr.y]="c.costEndY + (c.costEndY <= c.valueEndY ? -2 : 10)" class="fill-gray-600 dark:fill-gray-300 text-[10px] font-semibold">{{ 'Cost' | t }}</text>
                    <!-- marks -->
                    <g *ngFor="let m of c.marks">
                      <line [attr.x1]="m.x" [attr.x2]="m.x" [attr.y1]="PAD_T - 6 - m.row * 12" [attr.y2]="H - PAD_B" class="stroke-gray-300 dark:stroke-gray-600" stroke-width="1" stroke-dasharray="3 3" />
                      <circle [attr.cx]="m.x" [attr.cy]="m.y" r="5" stroke-width="2"
                              [ngClass]="m.kind === 'best' ? 'fill-primary-600 dark:fill-primary-400 stroke-white dark:stroke-gray-900' : 'fill-white dark:fill-gray-900 stroke-gray-500 dark:stroke-gray-400'" />
                      <text [attr.x]="m.anchor === 'start' ? m.x - 2 : m.anchor === 'end' ? m.x + 2 : m.x" [attr.y]="PAD_T - 9 - m.row * 12" [attr.text-anchor]="m.anchor"
                            class="fill-gray-700 dark:fill-gray-200 text-[10px] font-semibold">{{ m.label | t }}</text>
                    </g>
                    <!-- hover -->
                    <g *ngIf="hoverPoint() as h">
                      <line [attr.x1]="h.x" [attr.x2]="h.x" [attr.y1]="PAD_T" [attr.y2]="H - PAD_B" class="stroke-gray-400 dark:stroke-gray-500" stroke-width="1" />
                      <circle [attr.cx]="h.x" [attr.cy]="h.yValue" r="4" class="fill-primary-600 dark:fill-primary-400 stroke-white dark:stroke-gray-900" stroke-width="2" />
                      <circle [attr.cx]="h.x" [attr.cy]="h.yCost" r="4" class="fill-amber-600 dark:fill-amber-400 stroke-white dark:stroke-gray-900" stroke-width="2" />
                    </g>
                  </svg>
                  <div *ngIf="hoverPoint() as h" class="pointer-events-none absolute top-0 z-10 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 shadow-md px-2.5 py-1.5 text-[11px] text-gray-700 dark:text-gray-200 tabular-nums"
                       [style.left.%]="h.leftPct" [style.transform]="h.leftPct > 60 ? 'translateX(-105%)' : 'translateX(8px)'">
                    <p class="font-semibold m-0">{{ h.p.date | date:'d MMM yyyy' }} (+{{ h.p.day }})</p>
                    <p class="m-0">{{ 'Weight' | t }}: {{ h.p.weightKg | number:'1.0-1' }} {{ 'kg' | t }}</p>
                    <p class="m-0">{{ 'Value if sold' | t }}: {{ h.p.valueBdt | money }}</p>
                    <p class="m-0">{{ 'Total cost' | t }}: {{ h.p.costBdt | money }}</p>
                    <p class="m-0 font-semibold" [ngClass]="profitClass(h.p.profitBdt)">{{ 'Profit' | t }}: {{ h.p.profitBdt | money }}</p>
                  </div>
                </div>
              </section>

              <!-- ── Table ── -->
              <section *ngIf="p.projection.length" class="bg-white dark:bg-surface-dark rounded-2xl shadow-sm border border-gray-200/70 dark:border-gray-800 overflow-hidden">
                <div class="flex flex-wrap items-center justify-between gap-2 px-4 py-3 border-b border-gray-100 dark:border-gray-800">
                  <h3 class="text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 m-0">{{ (daily() ? 'Day by day' : 'Every 7 days') | t }}</h3>
                  <label class="inline-flex items-center gap-2 text-xs text-gray-700 dark:text-gray-300 cursor-pointer select-none">
                    <input type="checkbox" class="accent-primary-600" [checked]="daily()" (change)="daily.set($any($event.target).checked)" />
                    {{ 'Show every day' | t }}
                  </label>
                </div>
                <div class="overflow-x-auto max-h-[480px] overflow-y-auto">
                  <table class="w-full text-sm tabular-nums">
                    <thead class="sticky top-0 bg-gray-50 dark:bg-gray-900 text-xs text-gray-500 dark:text-gray-400">
                      <tr>
                        <th scope="col" class="text-left font-medium px-3 py-2">{{ 'Day' | t }}</th>
                        <th scope="col" class="text-left font-medium px-3 py-2">{{ 'Date' | t }}</th>
                        <th scope="col" class="text-right font-medium px-3 py-2">{{ 'Weight (kg)' | t }}</th>
                        <th scope="col" class="text-right font-medium px-3 py-2">{{ 'Gain/day' | t }}</th>
                        <th scope="col" class="text-right font-medium px-3 py-2">{{ 'Cost/day' | t }}</th>
                        <th scope="col" class="text-right font-medium px-3 py-2">{{ 'Total cost' | t }}</th>
                        <th scope="col" class="text-right font-medium px-3 py-2">{{ 'Value if sold' | t }}</th>
                        <th scope="col" class="text-right font-medium px-3 py-2">{{ 'Profit' | t }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr *ngFor="let r of rows(); trackBy: trackDay" class="border-t border-gray-100 dark:border-gray-800"
                          [ngClass]="r.day === p.bestDay ? 'bg-primary-50 dark:bg-primary-900/20' : r.day === p.breakEvenDay ? 'bg-amber-50 dark:bg-amber-900/10' : ''">
                        <td class="px-3 py-1.5 text-gray-700 dark:text-gray-300">
                          {{ r.day }}
                          <span *ngIf="r.day === p.bestDay" class="ml-1 text-[10px] font-semibold text-primary-700 dark:text-primary-300">{{ 'Best day' | t }}</span>
                          <span *ngIf="r.day === p.breakEvenDay" class="ml-1 text-[10px] font-semibold text-amber-700 dark:text-amber-300">{{ 'Break-even' | t }}</span>
                        </td>
                        <td class="px-3 py-1.5 text-gray-700 dark:text-gray-300 whitespace-nowrap">{{ r.date | date:'d MMM yyyy' }}</td>
                        <td class="px-3 py-1.5 text-right text-gray-900 dark:text-white">{{ r.weightKg | number:'1.1-1' }}</td>
                        <td class="px-3 py-1.5 text-right text-gray-700 dark:text-gray-300">{{ r.adgKg | number:'1.2-3' }}</td>
                        <td class="px-3 py-1.5 text-right text-gray-700 dark:text-gray-300">{{ r.dailyCostBdt | money }}</td>
                        <td class="px-3 py-1.5 text-right text-gray-900 dark:text-white">{{ r.costBdt | money }}</td>
                        <td class="px-3 py-1.5 text-right text-gray-900 dark:text-white">{{ r.valueBdt | money }}</td>
                        <td class="px-3 py-1.5 text-right font-semibold" [ngClass]="profitClass(r.profitBdt)">{{ r.profitBdt | money }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </section>
            </div>
          </div>
        </ng-container>
      </ng-container>
      <ng-template #dash>—</ng-template>
    </div>
  `,
})
export class FatteningPlanComponent {
  readonly animalId = input.required<string>();

  private readonly svc = inject(AnimalService);
  private readonly i18n = inject(I18nService);
  readonly isBangla = this.i18n.isBangla;

  /** What-if values the user changed; anything absent uses the animal's own figure. */
  readonly overrides = signal<FatteningPlanOverrides>({});
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly daily = signal(false);
  readonly hover = signal<number | null>(null);

  readonly W = 640;
  readonly H = 260;
  readonly PAD_L = 56;
  readonly PAD_R = 40;
  /** Room above the plot for the marker labels (two stacked rows). */
  readonly PAD_T = 34;
  readonly PAD_B = 20;

  readonly levers: Lever[] = [
    {
      key: 'pricePerKg', label: 'Sale price per kg live weight', unit: '৳/kg', step: 1, min: 1, max: 100000, decimals: 2,
      value: d => d.price.pricePerKgBdt,
      source: d => ({ text: 'price: {source}', params: { source: this.isBangla ? d.price.explanationBn : d.price.explanationEn } }),
    },
    {
      key: 'perHead', label: 'Extra per head (by-products)', unit: '৳', step: 100, min: 0, max: 10000000, decimals: 0,
      value: d => d.price.perHeadBdt,
      source: d => ({ text: d.price.perHeadBdt > 0 ? 'by-products sold separately (farm valuation rule)' : 'none under the farm valuation rule' }),
    },
    {
      key: 'adg', label: 'Gain per day', unit: 'kg/day', step: 0.01, min: -5, max: 5, decimals: 3,
      value: d => d.adgKgPerDay,
      source: d => adgBasisText(d),
    },
    {
      key: 'growthSlowdownPercent', label: 'Growth slowdown', unit: '%', step: 5, min: 0, max: 100, decimals: 0,
      value: d => d.growthSlowdownPercent,
      source: d => ({ text: 'heavier animals gain less: the gain per day falls by this much over {days} days', params: { days: d.growthDeclineDays } }),
    },
    {
      key: 'feedPerDay', label: 'Feed cost per day today (rises with weight)', unit: '৳/day', step: 1, min: 0, max: 1000000, decimals: 0,
      value: d => d.feedPerDayBdt,
      source: d => costBasisText(d.feedCostBasis, 'feed', d.feedWindowDays),
    },
    {
      key: 'labourPerDay', label: 'Labour & overhead per day', unit: '৳/day', step: 1, min: 0, max: 1000000, decimals: 0,
      value: d => d.labourOverheadPerDayBdt,
      source: d => costBasisText(d.labourOverheadCostBasis, 'labour', d.healthWindowDays),
    },
    {
      key: 'healthPerDay', label: 'Health cost per day', unit: '৳/day', step: 1, min: 0, max: 1000000, decimals: 0,
      value: d => d.healthPerDayBdt,
      source: d => costBasisText(d.healthCostBasis, 'health', d.healthWindowDays),
    },
    {
      key: 'horizonDays', label: 'Days ahead', unit: 'days', step: 10, min: 1, max: 730, decimals: 0,
      value: d => d.horizonDays,
      source: () => ({ text: 'same as the "Sell or keep?" card' }),
    },
  ];

  private readonly request = computed(() => ({ id: this.animalId(), overrides: this.overrides() }));

  readonly plan = toSignal(
    toObservable(this.request).pipe(
      debounceTime(350),
      distinctUntilChanged((a, b) => a.id === b.id && JSON.stringify(a.overrides) === JSON.stringify(b.overrides)),
      switchMap(({ id, overrides }) => {
        this.loading.set(true);
        this.error.set(null);
        return this.svc.getFatteningPlan(id, overrides).pipe(
          map(p => p as FatteningPlan | null),
          catchError((err: HttpErrorResponse) => {
            this.error.set(err?.error?.detail || err?.error?.title || this.i18n.t('The plan could not be worked out. Check the values and try again.'));
            return of(null);
          }),
          finalize(() => this.loading.set(false)),
        );
      }),
    ),
    { initialValue: null },
  );

  private lastGood: FatteningPlan | null = null;
  /** The last successful plan: a failed recalculation does not blank the tab. */
  readonly current = computed(() => {
    const p = this.plan();
    if (p) this.lastGood = p;
    return this.lastGood;
  });

  readonly changedCount = computed(() => Object.keys(this.overrides()).length);

  /** Every 7th day (plus the last day, the best day and break-even), or every day. */
  readonly rows = computed((): SellOrKeepPoint[] => {
    const p = this.current()?.plan;
    if (!p) return [];
    const pts = p.projection;
    if (this.daily()) return pts;
    const last = pts.length - 1;
    return pts.filter(r => r.day % 7 === 0 || r.day === last || r.day === p.bestDay || r.day === p.breakEvenDay);
  });

  /** Scaled SVG geometry for total cost vs value. */
  readonly chart = computed(() => {
    const p = this.current()?.plan;
    const pts = p?.projection ?? [];
    if (!p || pts.length < 2) return null;
    const all = pts.flatMap(r => [r.costBdt, r.valueBdt]);
    const [lo, hi, step] = niceRange(Math.min(...all), Math.max(...all), 4);
    const horizon = pts[pts.length - 1].day;
    const plotW = this.W - this.PAD_L - this.PAD_R;
    const x = (d: number) => this.PAD_L + (d / horizon) * plotW;
    const y = (v: number) => this.PAD_T + ((hi - v) / (hi - lo || 1)) * (this.H - this.PAD_T - this.PAD_B);
    const line = (f: (r: SellOrKeepPoint) => number) => pts.map(r => `${x(r.day).toFixed(1)},${y(f(r)).toFixed(1)}`).join(' ');

    const yTicks: { y: number; label: string }[] = [];
    for (let v = lo; v <= hi + step / 2; v += step) yTicks.push({ y: y(v), label: shortTaka(v) });

    const xCount = 5;
    const xTicks = Array.from({ length: xCount }, (_, i) => {
      const d = Math.round((horizon * i) / (xCount - 1));
      return { x: x(d), date: pts[d].date, anchor: i === 0 ? 'start' : i === xCount - 1 ? 'end' : 'middle' };
    });

    const marks: ChartMark[] = [{ day: 0, x: x(0), y: y(pts[0].valueBdt), label: 'Today', kind: 'today' }];
    if (p.breakEvenDay != null) marks.push({ day: p.breakEvenDay, x: x(p.breakEvenDay), y: y(pts[p.breakEvenDay].valueBdt), label: 'Break-even', kind: 'even' });
    if (p.bestDay != null) marks.push({ day: p.bestDay, x: x(p.bestDay), y: y(pts[p.bestDay].valueBdt), label: 'Best day', kind: 'best' });

    const end = pts[pts.length - 1];
    return {
      horizon,
      value: line(r => r.valueBdt),
      cost: line(r => r.costBdt),
      gap: [...pts.map(r => `${x(r.day).toFixed(1)},${y(r.valueBdt).toFixed(1)}`), ...[...pts].reverse().map(r => `${x(r.day).toFixed(1)},${y(r.costBdt).toFixed(1)}`)].join(' '),
      valueEndY: y(end.valueBdt),
      costEndY: y(end.costBdt),
      yTicks,
      xTicks,
      marks: placeChartMarks(marks, this.PAD_L, this.W - this.PAD_R),
      x, y,
    };
  });

  readonly hoverPoint = computed(() => {
    const c = this.chart();
    const d = this.hover();
    const pts = this.current()?.plan.projection ?? [];
    if (!c || d === null || !pts[d]) return null;
    const p = pts[d];
    const px = c.x(p.day);
    return { p, x: px, yValue: c.y(p.valueBdt), yCost: c.y(p.costBdt), leftPct: (px / this.W) * 100 };
  });

  onHover(event: MouseEvent): void {
    const c = this.chart();
    const el = event.currentTarget as SVGSVGElement | null;
    if (!c || !el) return;
    const rect = el.getBoundingClientRect();
    const vx = ((event.clientX - rect.left) / rect.width) * this.W;
    const frac = (vx - this.PAD_L) / (this.W - this.PAD_L - this.PAD_R);
    this.hover.set(Math.max(0, Math.min(c.horizon, Math.round(frac * c.horizon))));
  }

  display(l: Lever, d: SellOrKeepAdvice): string {
    const v = this.overrides()[l.key] ?? l.value(d);
    return v == null ? '' : String(round(v, l.decimals));
  }

  format(l: Lever, v: number | null | undefined): string {
    if (v == null) return '—';
    return round(v, l.decimals).toLocaleString('en-IN', { maximumFractionDigits: l.decimals });
  }

  onLever(l: Lever, raw: string, d: SellOrKeepAdvice): void {
    const n = raw === '' ? NaN : Number(raw);
    const next = { ...this.overrides() };
    const def = l.value(d);
    if (!Number.isFinite(n) || n < l.min || n > l.max || (def != null && round(n, l.decimals) === round(def, l.decimals))) {
      delete next[l.key];
    } else {
      next[l.key] = n;
    }
    this.overrides.set(next);
  }

  isChanged(key: LeverKey): boolean {
    return this.overrides()[key] != null;
  }

  resetOne(key: LeverKey): void {
    const next = { ...this.overrides() };
    delete next[key];
    this.overrides.set(next);
  }

  resetAll(): void {
    this.overrides.set({});
  }

  adgBasis(p: SellOrKeepAdvice): Phrase {
    return adgBasisText(p, true);
  }

  sinceArrival(p: SellOrKeepAdvice): boolean {
    return firstWeighingIsArrival(p);
  }

  breedTarget(p: SellOrKeepAdvice): Phrase {
    return breedTargetSourceText(p);
  }

  horizonPoint(p: SellOrKeepAdvice): SellOrKeepPoint | null {
    return p.projection.length ? p.projection[p.projection.length - 1] : null;
  }

  verdictTitle(p: SellOrKeepAdvice): Phrase {
    switch (p.verdict) {
      case 'SellNow': return { text: 'Sell now' };
      case 'Keep': return { text: 'Keep about {days} more days', params: { days: p.bestDay ?? 0 } };
      case 'LosingMoney': return { text: 'Losing money: check feed or health' };
      default: return { text: 'Weigh it first' };
    }
  }

  verdictIcon(p: SellOrKeepAdvice): string {
    return { SellNow: 'sell', Keep: 'schedule', LosingMoney: 'trending_down', WeighFirst: 'scale' }[p.verdict] ?? 'scale';
  }

  verdictBox(p: SellOrKeepAdvice): string {
    switch (p.verdict) {
      case 'LosingMoney': return 'bg-rose-50 dark:bg-rose-950/20 border-rose-200 dark:border-rose-900/50 text-rose-900 dark:text-rose-200';
      case 'WeighFirst': return 'bg-amber-50 dark:bg-amber-950/20 border-amber-200 dark:border-amber-900/50 text-amber-900 dark:text-amber-200';
      default: return 'bg-primary-50 dark:bg-primary-900/20 border-primary-200 dark:border-primary-800/60 text-primary-900 dark:text-primary-200';
    }
  }

  profitClass(v: number | null | undefined): string {
    if (v == null || v === 0) return 'text-gray-900 dark:text-white';
    return v > 0 ? 'text-success-700 dark:text-success-400' : 'text-rose-700 dark:text-rose-400';
  }

  trackDay(_: number, r: SellOrKeepPoint): number {
    return r.day;
  }
}

function round(v: number, decimals: number): number {
  const f = 10 ** decimals;
  return Math.round(v * f) / f;
}

/** A rounded axis range with about <paramref name="ticks"/> steps of 1, 2, 2.5 or 5 × 10ⁿ. */
function niceRange(min: number, max: number, ticks: number): [number, number, number] {
  if (min === max) { min -= 1; max += 1; }
  const raw = (max - min) / ticks;
  const mag = 10 ** Math.floor(Math.log10(raw));
  const step = [1, 2, 2.5, 5, 10].map(m => m * mag).find(s => s >= raw) ?? 10 * mag;
  return [Math.floor(min / step) * step, Math.ceil(max / step) * step, step];
}
