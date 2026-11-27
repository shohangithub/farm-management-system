import { Component, ChangeDetectionStrategy, computed, effect, inject, input, output, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { catchError, of } from 'rxjs';
import { AnimalService } from '../../services/animal.service';
import { SellOrKeepAdvice, SellOrKeepPoint } from '../../models/sell-or-keep.models';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

interface ChartMark { x: number; y: number; label: string; kind: 'today' | 'best' | 'even'; }

/**
 * "Sell or keep?" card on the animal page: the verdict and its reason, a small profit curve for the
 * coming days (today, best day and break-even marked), the key numbers and where the price came from.
 * All figures come from GET /livestock/animals/{id}/sell-or-keep (the same service as the report).
 */
@Component({
  selector: 'app-sell-or-keep-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterModule, MatIconModule, MoneyPipe, DatePipe, DecimalPipe, TranslatePipe],
  template: `
    <section class="bg-white dark:bg-surface-dark rounded-2xl shadow-sm border border-gray-200/70 dark:border-gray-800 p-4 sm:p-5"
             [attr.aria-label]="'Sell or keep?' | t">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div class="flex items-center gap-2 min-w-0">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-gray-400" aria-hidden="true">event_available</mat-icon>
          <h3 class="text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 m-0">{{ 'Sell or keep?' | t }}</h3>
          <span *ngIf="advice() as a" class="ml-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300 border border-gray-200 dark:border-gray-700"
                [title]="'How far the advice can be trusted: more recent weighings on a straight line = higher.' | t">
            {{ 'Confidence: {level}' | t: { level: (confidenceLabel(a.confidence) | t) } }}
          </span>
        </div>
        <div class="flex flex-wrap items-center gap-2 no-print">
          <button *ngIf="showWhatIf()" type="button" (click)="whatIf.emit()"
            class="h-8 px-3 rounded-lg text-xs font-semibold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/60 inline-flex items-center gap-1.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-500">
            <mat-icon class="!text-[16px] !w-[16px] !h-[16px]" aria-hidden="true">tune</mat-icon>{{ 'Try other prices' | t }}
          </button>
          <a *ngIf="advice()" routerLink="/reports/livestock.sell-or-keep" [queryParams]="{ farmId: farmId(), animalId: animalId(), autoRun: 'true' }"
            class="h-8 px-3 rounded-lg text-xs font-semibold text-primary-700 dark:text-primary-300 bg-primary-50 dark:bg-primary-900/20 border border-primary-200/70 dark:border-primary-800/60 hover:bg-primary-100 dark:hover:bg-primary-900/40 inline-flex items-center gap-1.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-500">
            <mat-icon class="!text-[16px] !w-[16px] !h-[16px]" aria-hidden="true">description</mat-icon>{{ 'Full report' | t }}
          </a>
        </div>
      </div>

      <p *ngIf="loading()" class="text-sm text-gray-500 dark:text-gray-400 mt-3">{{ 'Working out the best day to sell…' | t }}</p>
      <p *ngIf="!loading() && !advice()" class="text-sm text-gray-500 dark:text-gray-400 mt-3">{{ 'Advice is not available for this animal.' | t }}</p>

      <ng-container *ngIf="advice() as a">
        <!-- Verdict -->
        <div class="mt-3 rounded-xl border px-3 py-3 flex items-start gap-3" [ngClass]="verdictBox()">
          <mat-icon class="!text-[22px] !w-[22px] !h-[22px] shrink-0 mt-0.5" aria-hidden="true">{{ verdictIcon() }}</mat-icon>
          <div class="min-w-0">
            <p class="text-base font-bold leading-snug m-0">{{ verdictTitle().text | t: verdictTitle().params }}</p>
            <p class="text-sm mt-1 m-0 text-gray-700 dark:text-gray-300">{{ reason() }}</p>
          </div>
        </div>

        <!-- Profit curve -->
        <div *ngIf="chart() as c" class="mt-4">
          <p class="text-xs text-gray-500 dark:text-gray-400 mb-1">{{ 'Profit if sold on each of the next {days} days' | t: { days: c.horizon } }}</p>
          <div class="relative">
            <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" class="w-full h-40 overflow-visible" role="img"
                 [attr.aria-label]="'Profit curve: today, best day and break-even are marked' | t"
                 (mousemove)="onHover($event)" (mouseleave)="hover.set(null)">
              <!-- zero line -->
              <line [attr.x1]="PAD_L" [attr.x2]="W - PAD_R" [attr.y1]="c.zeroY" [attr.y2]="c.zeroY" class="stroke-gray-300 dark:stroke-gray-600" stroke-width="1" stroke-dasharray="4 4" />
              <text [attr.x]="PAD_L - 4" [attr.y]="c.zeroY + 3" text-anchor="end" class="fill-gray-400 text-[10px]">0</text>
              <text [attr.x]="PAD_L - 4" [attr.y]="c.topY + 3" text-anchor="end" class="fill-gray-400 text-[10px]">{{ c.maxLabel }}</text>
              <text [attr.x]="PAD_L - 4" [attr.y]="c.bottomY + 3" text-anchor="end" class="fill-gray-400 text-[10px]">{{ c.minLabel }}</text>
              <!-- curve -->
              <polyline [attr.points]="c.points" fill="none" class="stroke-primary-600 dark:stroke-primary-400" stroke-width="2" stroke-linejoin="round" stroke-linecap="round" />
              <!-- marks -->
              <g *ngFor="let m of c.marks">
                <line [attr.x1]="m.x" [attr.x2]="m.x" [attr.y1]="PAD_T" [attr.y2]="H - PAD_B" class="stroke-gray-200 dark:stroke-gray-700" stroke-width="1" />
                <circle [attr.cx]="m.x" [attr.cy]="m.y" r="5" stroke-width="2"
                        [ngClass]="m.kind === 'best' ? 'fill-primary-600 dark:fill-primary-400 stroke-white dark:stroke-gray-900' : 'fill-white dark:fill-gray-900 stroke-gray-500 dark:stroke-gray-400'" />
                <text [attr.x]="m.x" [attr.y]="H - 2" [attr.text-anchor]="m.kind === 'today' ? 'start' : 'middle'" class="fill-gray-600 dark:fill-gray-300 text-[10px] font-semibold">{{ m.label | t }}</text>
              </g>
              <!-- hover -->
              <g *ngIf="hoverPoint() as h">
                <line [attr.x1]="h.x" [attr.x2]="h.x" [attr.y1]="PAD_T" [attr.y2]="H - PAD_B" class="stroke-gray-400 dark:stroke-gray-500" stroke-width="1" />
                <circle [attr.cx]="h.x" [attr.cy]="h.y" r="4" class="fill-primary-600 dark:fill-primary-400 stroke-white dark:stroke-gray-900" stroke-width="2" />
              </g>
            </svg>
            <div *ngIf="hoverPoint() as h" class="pointer-events-none absolute top-0 z-10 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 shadow-md px-2.5 py-1.5 text-[11px] text-gray-700 dark:text-gray-200 tabular-nums"
                 [style.left.%]="h.leftPct" [style.transform]="h.leftPct > 60 ? 'translateX(-105%)' : 'translateX(8px)'">
              <p class="font-semibold m-0">{{ h.p.date | date:'d MMM yyyy' }} (+{{ h.p.day }})</p>
              <p class="m-0">{{ 'Weight' | t }}: {{ h.p.weightKg | number:'1.0-1' }} {{ 'kg' | t }}</p>
              <p class="m-0">{{ 'Cost so far' | t }}: {{ h.p.costBdt | money }}</p>
              <p class="m-0">{{ 'Value' | t }}: {{ h.p.valueBdt | money }}</p>
              <p class="m-0 font-semibold" [ngClass]="h.p.profitBdt >= 0 ? 'text-success-700 dark:text-success-400' : 'text-rose-700 dark:text-rose-400'">{{ 'Profit' | t }}: {{ h.p.profitBdt | money }}</p>
            </div>
          </div>
        </div>

        <!-- Key numbers -->
        <dl class="mt-4 grid grid-cols-2 md:grid-cols-4 gap-x-4 gap-y-3">
          <div>
            <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Invested so far' | t }}</dt>
            <dd class="text-base font-semibold text-gray-900 dark:text-white tabular-nums">{{ a.investedBdt | money }}</dd>
          </div>
          <div>
            <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Profit if sold today' | t }}</dt>
            <dd class="text-base font-semibold tabular-nums" [ngClass]="profitClass(a.profitTodayBdt)">{{ a.profitTodayBdt | money }}</dd>
          </div>
          <div>
            <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Best day to sell' | t }}</dt>
            <dd class="text-base font-semibold text-gray-900 dark:text-white tabular-nums">
              <ng-container *ngIf="a.bestDate; else dash">{{ a.bestDate | date:'d MMM' }} · {{ a.bestWeightKg | number:'1.0-0' }} {{ 'kg' | t }}</ng-container>
            </dd>
          </div>
          <div>
            <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Profit at best day' | t }}</dt>
            <dd class="text-base font-semibold tabular-nums" [ngClass]="profitClass(a.profitAtBestDayBdt)">{{ a.profitAtBestDayBdt | money }}</dd>
          </div>
          <div>
            <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Gain per day' | t }}</dt>
            <dd class="text-sm font-semibold text-gray-900 dark:text-white tabular-nums">
              <ng-container *ngIf="a.adgKgPerDay != null; else dash">{{ a.adgKgPerDay | number:'1.2-2' }} {{ 'kg' | t }}</ng-container>
              <span *ngIf="a.breedTargetAdgKg" class="block text-xs font-normal text-gray-500 dark:text-gray-400">{{ growthLabel(a) | t: { target: (a.breedTargetAdgKg | number:'1.2-2') } }}</span>
            </dd>
          </div>
          <div>
            <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Cost per day' | t }}</dt>
            <dd class="text-sm font-semibold text-gray-900 dark:text-white tabular-nums">{{ a.dailyCostBdt | money }}
              <span class="block text-xs font-normal text-gray-500 dark:text-gray-400">{{ 'feed {feed} · other {other}' | t: { feed: (a.feedPerDayBdt | money), other: (a.labourOverheadPerDayBdt + a.healthPerDayBdt | money) } }}</span>
            </dd>
          </div>
          <div>
            <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Earned per day kept' | t }}</dt>
            <dd class="text-sm font-semibold tabular-nums" [ngClass]="profitClass(a.marginPerDayKeptBdt)">{{ a.marginPerDayKeptBdt | money }}</dd>
          </div>
          <div>
            <dt class="text-xs text-gray-500 dark:text-gray-400">{{ 'Break-even' | t }}</dt>
            <dd class="text-sm font-semibold text-gray-900 dark:text-white tabular-nums">
              <ng-container *ngIf="a.breakEvenDate; else noBreakEven">{{ a.breakEvenDate | date:'d MMM yyyy' }}</ng-container>
              <ng-template #noBreakEven>{{ ((a.profitTodayBdt ?? 0) >= 0 ? 'Already in profit' : 'Not within the horizon') | t }}</ng-template>
            </dd>
          </div>
        </dl>

        <!-- Price source & health -->
        <div class="mt-4 flex flex-col gap-1.5 text-xs text-gray-600 dark:text-gray-400 bg-gray-50 dark:bg-gray-900/40 rounded-lg px-3 py-2 border border-gray-100 dark:border-gray-800">
          <span class="flex items-start gap-1.5">
            <mat-icon class="!text-[14px] !w-[14px] !h-[14px] mt-px shrink-0 text-gray-400" aria-hidden="true">sell</mat-icon>
            <span>{{ 'Price {price}/kg live weight — {source}' | t: { price: (a.price.pricePerKgBdt | money), source: (priceSource().text | t: priceSource().params) } }}</span>
          </span>
          <span *ngIf="a.frequentlySick" class="flex items-start gap-1.5 text-rose-700 dark:text-rose-400">
            <mat-icon class="!text-[14px] !w-[14px] !h-[14px] mt-px shrink-0" aria-hidden="true">healing</mat-icon>
            <span>{{ 'Often sick: {count} treatments in 90 days ({cost}). Its growth is less certain, so sell sooner.' | t: { count: a.treatmentsLast90Days, cost: (a.healthCostLast90DaysBdt | money) } }}</span>
          </span>
          <span *ngIf="a.latestWeightDate" class="flex items-start gap-1.5">
            <mat-icon class="!text-[14px] !w-[14px] !h-[14px] mt-px shrink-0 text-gray-400" aria-hidden="true">scale</mat-icon>
            <span>{{ 'Last weighed {kg} kg on {date}; about {today} kg today.' | t: { kg: (a.latestWeightKg | number:'1.0-1'), date: (a.latestWeightDate | date:'d MMM yyyy'), today: (a.weightTodayKg | number:'1.0-0') } }}</span>
          </span>
        </div>
      </ng-container>
      <ng-template #dash>—</ng-template>
    </section>
  `,
})
export class SellOrKeepCardComponent {
  readonly animalId = input.required<string>();
  readonly farmId = input<string | null>(null);
  /** Changes whenever the page reloads the animal (new weight, cost…), so the advice is fetched again. */
  readonly refresh = input<number>(0);
  readonly showWhatIf = input<boolean>(true);
  readonly whatIf = output<void>();

  private readonly svc = inject(AnimalService);
  private readonly i18n = inject(I18nService);

  readonly advice = signal<SellOrKeepAdvice | null>(null);
  readonly loading = signal(true);
  readonly hover = signal<number | null>(null);

  readonly W = 600;
  readonly H = 160;
  readonly PAD_L = 52;
  readonly PAD_R = 12;
  readonly PAD_T = 10;
  readonly PAD_B = 18;

  constructor() {
    effect(onCleanup => {
      const id = this.animalId();
      this.refresh();
      this.loading.set(true);
      const sub = this.svc.getSellOrKeep(id).pipe(catchError(() => of(null))).subscribe(a => {
        this.advice.set(a);
        this.loading.set(false);
      });
      onCleanup(() => sub.unsubscribe());
    });
  }

  readonly reason = computed(() => {
    const a = this.advice();
    return a ? (this.i18n.isBangla ? a.reasonBn : a.reasonEn) : '';
  });

  readonly verdictTitle = computed((): { text: string; params?: Record<string, string | number> } => {
    const a = this.advice();
    switch (a?.verdict) {
      case 'SellNow': return { text: 'Sell now' };
      case 'Keep': return { text: 'Keep about {days} more days', params: { days: a.bestDay ?? 0 } };
      case 'LosingMoney': return { text: 'Losing money: check feed or health' };
      default: return { text: 'Weigh it first' };
    }
  });

  readonly verdictIcon = computed(() => ({ SellNow: 'sell', Keep: 'schedule', LosingMoney: 'trending_down', WeighFirst: 'scale' }[this.advice()?.verdict ?? 'WeighFirst']));

  readonly verdictBox = computed(() => {
    switch (this.advice()?.verdict) {
      case 'LosingMoney': return 'bg-rose-50 dark:bg-rose-950/20 border-rose-200 dark:border-rose-900/50 text-rose-900 dark:text-rose-200';
      case 'WeighFirst': return 'bg-amber-50 dark:bg-amber-950/20 border-amber-200 dark:border-amber-900/50 text-amber-900 dark:text-amber-200';
      default: return 'bg-primary-50 dark:bg-primary-900/20 border-primary-200 dark:border-primary-800/60 text-primary-900 dark:text-primary-200';
    }
  });

  readonly priceSource = computed((): { text: string; params?: Record<string, string | number> } => {
    const a = this.advice();
    switch (a?.price.source) {
      case 'OwnSales': return { text: 'based on your own sales of {count} {breed}', params: { count: a.price.salesCount, breed: a.breedName } };
      case 'BreedExpected': return { text: 'the expected price set for {breed}', params: { breed: a.breedName } };
      case 'FarmLiveRate': return { text: "the farm's live-weight rate" };
      default: return { text: 'meat price × dressing %' };
    }
  });

  /** Scaled SVG geometry for the profit curve. */
  readonly chart = computed(() => {
    const a = this.advice();
    const pts = a?.projection ?? [];
    if (!a || pts.length < 2) return null;
    const profits = pts.map(p => p.profitBdt);
    const max = Math.max(0, ...profits);
    const min = Math.min(0, ...profits);
    const span = max - min || 1;
    const horizon = pts[pts.length - 1].day;
    const x = (d: number) => this.PAD_L + (d / horizon) * (this.W - this.PAD_L - this.PAD_R);
    const y = (v: number) => this.PAD_T + ((max - v) / span) * (this.H - this.PAD_T - this.PAD_B);
    const marks: ChartMark[] = [{ x: x(0), y: y(pts[0].profitBdt), label: 'Today', kind: 'today' }];
    if (a.breakEvenDay != null) marks.push({ x: x(a.breakEvenDay), y: y(pts[a.breakEvenDay].profitBdt), label: 'Break-even', kind: 'even' });
    if (a.bestDay != null && a.bestDay > 0) marks.push({ x: x(a.bestDay), y: y(pts[a.bestDay].profitBdt), label: 'Best day', kind: 'best' });
    const short = (v: number) => (Math.abs(v) >= 1000 ? `${Math.round(v / 1000)}k` : `${Math.round(v)}`);
    return {
      horizon,
      points: pts.map(p => `${x(p.day).toFixed(1)},${y(p.profitBdt).toFixed(1)}`).join(' '),
      marks,
      zeroY: y(0),
      topY: y(max),
      bottomY: y(min),
      maxLabel: max > 0 ? short(max) : '',
      minLabel: min < 0 ? short(min) : '',
      x, y,
    };
  });

  readonly hoverPoint = computed(() => {
    const c = this.chart();
    const d = this.hover();
    const pts = this.advice()?.projection ?? [];
    if (!c || d === null || !pts[d]) return null;
    const p: SellOrKeepPoint = pts[d];
    const px = c.x(p.day);
    return { p, x: px, y: c.y(p.profitBdt), leftPct: (px / this.W) * 100 };
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

  confidenceLabel(c: string): string {
    return c === 'High' ? 'High' : c === 'Medium' ? 'Medium' : 'Low';
  }

  growthLabel(a: SellOrKeepAdvice): string {
    switch (a.growthVsTarget) {
      case 'Below': return 'below the breed target of {target}';
      case 'Above': return 'above the breed target of {target}';
      case 'At': return 'at the breed target of {target}';
      default: return 'breed target {target}';
    }
  }

  profitClass(v: number | null | undefined): string {
    if (v == null || v === 0) return 'text-gray-900 dark:text-white';
    return v > 0 ? 'text-success-700 dark:text-success-400' : 'text-rose-700 dark:text-rose-400';
  }
}
