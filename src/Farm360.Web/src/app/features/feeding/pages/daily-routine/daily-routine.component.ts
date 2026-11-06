import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { ShedService } from '../../../farms/services/shed.service';
import { ShedList } from '../../../farms/models/shed.model';
import { FeedingService } from '../../services/feeding.service';
import { DailyFeedingRoutine, FeedingRuleSet, RoutineAnimal } from '../../models/feeding.models';
import { FeedingSubnavComponent } from '../../components/feeding-subnav/feeding-subnav.component';

interface ShedBlock { shed: string; animals: RoutineAnimal[]; totalKg: number; }

/**
 * The day's feeding routine worked out from the feed charts and recipes: how much of each recipe
 * to mix (and what goes in it), how much to carry out at each feeding time, and what every animal
 * gets — shed by shed, ready to print for the feeding staff.
 */
@Component({
  selector: 'app-daily-routine',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatIconModule, MatButtonModule, PageHeaderComponent, FeedingSubnavComponent],
  template: `
    <app-page-header title="Daily Feeding Routine · দৈনিক খাবার রুটিন"
      description="From your feed charts and recipes: what to mix, what to carry at each feeding time, and what each animal gets."
      breadcrumbActiveNode="Daily Routine">
      <div actions class="flex gap-2">
        <button mat-stroked-button (click)="print()" [disabled]="!result()" class="!rounded-xl !text-xs"><mat-icon class="!text-[16px]">print</mat-icon> Print</button>
      </div>
    </app-page-header>

    <div class="px-6 py-4 mx-auto max-w-7xl">
      <div class="print:hidden"><app-feeding-subnav></app-feeding-subnav></div>

      <!-- Inputs -->
      <div class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 p-4 flex flex-wrap items-end gap-4 print:hidden">
        <label class="text-xs font-bold text-gray-600 dark:text-gray-300 flex flex-col gap-1.5">Day
          <div class="flex items-center gap-1">
            <button type="button" (click)="shift(-1)" class="p-1.5 rounded-lg border border-gray-300 dark:border-gray-600"><mat-icon class="!text-[16px] !w-4 !h-4">chevron_left</mat-icon></button>
            <input type="date" [(ngModel)]="date" (change)="run()" class="field" />
            <button type="button" (click)="shift(1)" class="p-1.5 rounded-lg border border-gray-300 dark:border-gray-600"><mat-icon class="!text-[16px] !w-4 !h-4">chevron_right</mat-icon></button>
          </div>
        </label>
        <label class="text-xs font-bold text-gray-600 dark:text-gray-300 flex flex-col gap-1.5">Shed
          <select [(ngModel)]="shedId" (change)="run()" class="field min-w-[12rem]">
            <option [ngValue]="null">Whole farm</option>
            <option *ngFor="let s of sheds()" [ngValue]="s.id">{{ s.shedNumber }}{{ s.shedName ? ' · ' + s.shedName : '' }}</option>
          </select>
        </label>
        <label class="text-xs font-bold text-gray-600 dark:text-gray-300 flex flex-col gap-1.5">Feed chart
          <select [(ngModel)]="ruleSetId" (change)="run()" class="field min-w-[14rem]">
            <option [ngValue]="null">All active charts</option>
            <option *ngFor="let c of charts()" [ngValue]="c.id">{{ c.name }}</option>
          </select>
        </label>
        <label *ngIf="ruleSetId" class="flex items-center gap-1.5 text-xs pb-2" title="See the routine before assigning the chart">
          <input type="checkbox" [(ngModel)]="applyToUnassigned" (change)="run()" class="accent-emerald-600" />
          Also animals without a ration
        </label>
        <p class="text-[11px] text-gray-500 m-0 pb-2 max-w-md">
          Each animal is placed in its chart band by its latest weight (grown by its daily gain) or age on this day — the same way the daily entries are made.
        </p>
      </div>

      <div *ngIf="error()" class="mt-4 p-3 rounded-xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 text-sm text-rose-700">{{ error() }}</div>
      <div *ngIf="loading()" class="py-10 text-center text-sm text-gray-500">Working out the routine…</div>

      <ng-container *ngIf="result() as r">
        <h2 class="hidden print:block text-lg font-black m-0 mb-2">Feeding routine — {{ r.date | date:'EEEE, d MMM yyyy' }}{{ shedName() ? ' · ' + shedName() : '' }}{{ chartName() ? ' · ' + chartName() : '' }}</h2>

        <!-- Summary -->
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 mt-5 print:mt-0">
          <div class="tile"><p class="tile-label">Day</p><h3 class="tile-value">{{ r.date | date:'EEE, d MMM' }}</h3></div>
          <div class="tile"><p class="tile-label">Animals fed</p><h3 class="tile-value">{{ r.animals }}</h3>
            <p *ngIf="r.animalsWithoutRation" class="text-[11px] text-rose-600 m-0">+{{ r.animalsWithoutRation }} without a ration</p></div>
          <div class="tile"><p class="tile-label">Total feed</p><h3 class="tile-value">{{ r.totalKg | number:'1.0-2' }} kg</h3></div>
          <div class="tile"><p class="tile-label">Feed cost</p><h3 class="tile-value">৳{{ r.totalCostBdt | number:'1.0-0' }}</h3></div>
        </div>
        <div *ngFor="let n of r.notes" class="mt-2 text-[11px] text-gray-500 flex items-start gap-1"><mat-icon class="!text-[14px] !w-4 !h-4">info</mat-icon>{{ n }}</div>

        <div *ngIf="!r.perAnimal.length" class="mt-6 p-8 text-center text-sm text-gray-500 bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700">
          No rations for this day. Set them from <a routerLink="/feeding/board" class="underline">Who Eats What</a>.
        </div>

        <ng-container *ngIf="r.perAnimal.length">
          <!-- 1. Feeding times -->
          <section class="card mt-5">
            <div class="card-head"><h3 class="card-title">1 · Feeding times — carry out</h3><span class="text-[11px] text-gray-500">kg of each recipe per feed</span></div>
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead class="thead">
                  <tr>
                    <th class="text-left px-4 py-2">Recipe</th>
                    <th *ngFor="let s of r.sessionOrder" class="text-right px-3 py-2">{{ s }}</th>
                    <th class="text-right px-4 py-2">Day total</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let x of r.recipes" class="row">
                    <td class="px-4 py-2 font-semibold">{{ x.recipe }} <span class="text-[11px] font-normal text-gray-500">· {{ x.animals }} animal(s)</span></td>
                    <td *ngFor="let s of r.sessionOrder" class="px-3 py-2 text-right font-mono">{{ sessionKg(x.sessions, s) }}</td>
                    <td class="px-4 py-2 text-right font-mono font-bold">{{ x.totalKg | number:'1.0-2' }} kg</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>

          <!-- 2. Mixing sheet -->
          <section class="card mt-5 break-inside-avoid">
            <div class="card-head"><h3 class="card-title">2 · Mixing sheet — what goes in</h3><span class="text-[11px] text-gray-500">today's total of each recipe, by ingredient</span></div>
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-4 p-4">
              <div *ngFor="let x of r.recipes" class="rounded-xl border border-gray-100 dark:border-gray-700 overflow-hidden break-inside-avoid">
                <div class="px-4 py-2 bg-emerald-50 dark:bg-emerald-950/30 flex items-center justify-between">
                  <span class="font-bold text-sm">{{ x.recipe }}<span *ngIf="x.directItem" class="ml-1 text-[10px] font-normal text-gray-500">(fed as it is)</span></span>
                  <span class="font-mono font-black text-emerald-700 dark:text-emerald-300">{{ x.totalKg | number:'1.0-2' }} kg</span>
                </div>
                <table class="w-full text-xs">
                  <thead class="thead"><tr><th class="text-left px-3 py-1.5">Ingredient</th><th class="text-right px-2 py-1.5">%</th><th class="text-right px-2 py-1.5">Kg</th><th class="text-right px-3 py-1.5">Stock</th></tr></thead>
                  <tbody>
                    <tr *ngFor="let i of x.ingredients" class="row">
                      <td class="px-3 py-1.5">{{ i.ingredient }}</td>
                      <td class="px-2 py-1.5 text-right font-mono text-gray-500">{{ i.percent | number:'1.0-2' }}</td>
                      <td class="px-2 py-1.5 text-right font-mono font-bold">{{ i.kg | number:'1.0-3' }}</td>
                      <td class="px-3 py-1.5 text-right font-mono" [ngClass]="i.isShort ? 'text-rose-600 font-bold' : 'text-gray-500'">
                        <ng-container *ngIf="i.stockItem; else unlinked">
                          {{ i.inStock | number:'1.0-2' }} {{ i.unit }}<span *ngIf="i.isShort"> · short</span>
                        </ng-container>
                        <ng-template #unlinked><span class="text-amber-600 font-sans">not linked</span></ng-template>
                      </td>
                    </tr>
                    <tr *ngIf="!x.ingredients.length"><td colspan="4" class="px-3 py-2 text-gray-400">No ingredients in this recipe.</td></tr>
                  </tbody>
                </table>
                <div class="px-3 py-1.5 text-[11px] text-gray-500 border-t border-gray-100 dark:border-gray-700">৳{{ x.costBdt | number:'1.0-0' }} · {{ x.animals }} animal(s)</div>
              </div>
            </div>
          </section>

          <!-- 3. Per animal, shed by shed -->
          <section class="card mt-5">
            <div class="card-head"><h3 class="card-title">3 · Each animal</h3><span class="text-[11px] text-gray-500">chart band from weight / age on this day</span></div>
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead class="thead">
                  <tr>
                    <th class="text-left px-4 py-2">Animal</th>
                    <th class="text-left px-3 py-2">Chart · band</th>
                    <th class="text-left px-3 py-2">Recipe</th>
                    <th class="text-right px-3 py-2">Per feed</th>
                    <th class="text-right px-3 py-2">Kg/day</th>
                    <th class="text-right px-4 py-2">৳</th>
                  </tr>
                </thead>
                <tbody *ngFor="let b of shedBlocks()">
                  <tr class="bg-gray-50 dark:bg-gray-900/40">
                    <td colspan="6" class="px-4 py-1.5 text-xs font-black text-gray-700 dark:text-gray-300">{{ b.shed }} · {{ b.animals.length }} animal(s) · {{ b.totalKg | number:'1.0-2' }} kg</td>
                  </tr>
                  <ng-container *ngFor="let a of b.animals">
                    <tr *ngFor="let l of a.lines; let first = first" class="row align-top" [class.border-t-2]="first">
                      <td class="px-4 py-1.5">
                        <ng-container *ngIf="first">
                          <span class="font-bold">{{ a.tag }}</span>
                          <span class="block text-[11px] text-gray-500">{{ a.pen ? a.pen + ' · ' : '' }}{{ a.weightKg ? (a.weightKg | number:'1.0-0') + ' kg' : 'no weight' }}</span>
                        </ng-container>
                      </td>
                      <td class="px-3 py-1.5">
                        {{ l.chart }} <span class="text-[11px] text-gray-500">· {{ l.band }}</span>
                        <span *ngIf="l.fit === 'BelowRange' || l.fit === 'AboveRange'" class="ml-1 text-[10px] text-amber-600">outside chart</span>
                        <span *ngIf="l.kind === 'Not assigned'" class="ml-1 text-[10px] font-bold text-sky-600">not assigned yet</span>
                        <span *ngIf="l.kind === 'Extra' || l.kind === 'Group'" class="ml-1 text-[10px] text-gray-400">{{ l.kind === 'Extra' ? 'extra' : 'shed ration' }}</span>
                      </td>
                      <td class="px-3 py-1.5">{{ l.recipe }}</td>
                      <td class="px-3 py-1.5 text-right font-mono text-xs">{{ l.sessions > 1 ? l.sessions + ' × ' + (l.kgPerSession | number:'1.0-3') : (l.kgPerSession | number:'1.0-3') }} kg</td>
                      <td class="px-3 py-1.5 text-right font-mono font-bold">{{ l.kgPerDay | number:'1.0-3' }}</td>
                      <td class="px-4 py-1.5 text-right font-mono text-gray-500">{{ l.costBdt | number:'1.0-0' }}</td>
                    </tr>
                  </ng-container>
                </tbody>
              </table>
            </div>
          </section>
        </ng-container>
      </ng-container>
    </div>
  `,
  styles: [`
    .field { padding: 0.45rem 0.65rem; border-radius: 0.6rem; border: 1px solid rgb(209 213 219); background: white; font-size: 0.8125rem; color: rgb(17 24 39); outline: none; }
    .field:focus { border-color: rgb(16 185 129); }
    :host-context(.dark) .field { background: rgb(17 24 39); border-color: rgb(55 65 81); color: white; }
    .tile { padding: 1rem; border-radius: 1rem; background: white; border: 1px solid rgb(243 244 246); }
    :host-context(.dark) .tile { background: rgb(31 41 55); border-color: rgb(55 65 81); }
    .tile-label { font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: .05em; color: rgb(107 114 128); margin: 0; }
    .tile-value { font-size: 1.125rem; font-weight: 900; margin: .25rem 0 0; }
    .card { background: white; border-radius: 1rem; border: 1px solid rgb(243 244 246); overflow: hidden; }
    :host-context(.dark) .card { background: rgb(31 41 55); border-color: rgb(55 65 81); }
    .card-head { padding: .75rem 1.25rem; border-bottom: 1px solid rgb(243 244 246); display: flex; align-items: center; justify-content: space-between; gap: .5rem; flex-wrap: wrap; }
    :host-context(.dark) .card-head { border-color: rgb(55 65 81); }
    .card-title { font-size: .875rem; font-weight: 900; margin: 0; }
    .thead { background: rgb(249 250 251); font-size: 11px; text-transform: uppercase; letter-spacing: .05em; color: rgb(107 114 128); }
    :host-context(.dark) .thead { background: rgb(17 24 39 / .4); }
    .row { border-top: 1px solid rgb(243 244 246); }
    :host-context(.dark) .row { border-color: rgb(55 65 81 / .6); }
    @media print { .card, .tile { border-color: #ccc; } }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DailyRoutineComponent {
  private readonly feeding = inject(FeedingService);
  private readonly context = inject(WorkingContextService);
  private readonly shedService = inject(ShedService);

  date = new Date().toISOString().split('T')[0];
  shedId: string | null = null;
  ruleSetId: string | null = null;
  applyToUnassigned = false;

  /** Active feed charts (direct rations are per-animal extras, not charts). */
  readonly charts = signal<FeedingRuleSet[]>([]);

  readonly sheds = signal<ShedList[]>([]);
  readonly result = signal<DailyFeedingRoutine | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  /** Animals grouped by shed, in the order the staff walk them. */
  readonly shedBlocks = computed<ShedBlock[]>(() => {
    const blocks = new Map<string, ShedBlock>();
    for (const a of this.result()?.perAnimal ?? []) {
      const key = a.shed || 'No shed';
      const b = blocks.get(key) ?? { shed: key, animals: [], totalKg: 0 };
      b.animals.push(a);
      b.totalKg += a.totalKg;
      blocks.set(key, b);
    }
    return [...blocks.values()];
  });

  readonly shedName = signal<string | null>(null);
  readonly chartName = signal<string | null>(null);

  constructor() {
    const farmId = this.context.currentFarmValue?.id;
    if (farmId) this.shedService.getShedsByFarm(farmId).subscribe(s => this.sheds.set(s));
    this.feeding.getRuleSets().subscribe(sets => this.charts.set(
      sets.filter(s => s.isActive && !s.name.startsWith('Direct · ')).sort((a, b) => a.name.localeCompare(b.name))));
    this.run();
  }

  shift(days: number): void {
    const d = new Date(this.date + 'T00:00:00');
    d.setDate(d.getDate() + days);
    this.date = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    this.run();
  }

  run(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) return;
    const shed = this.sheds().find(s => s.id === this.shedId);
    this.shedName.set(shed ? shed.shedNumber + (shed.shedName ? ' · ' + shed.shedName : '') : null);
    this.chartName.set(this.charts().find(c => c.id === this.ruleSetId)?.name ?? null);
    this.loading.set(true);
    this.error.set(null);
    this.feeding.getDailyRoutine({
      farmId, date: this.date || null, shedId: this.shedId,
      ruleSetId: this.ruleSetId, applyToUnassigned: !!this.ruleSetId && this.applyToUnassigned,
    }).subscribe({
      next: r => { this.result.set(r); this.loading.set(false); },
      error: err => { this.error.set(parseApiError(err, 'Could not work out the routine.')); this.loading.set(false); },
    });
  }

  sessionKg(sessions: { session: string; kg: number }[], name: string): string {
    const s = sessions.find(x => x.session === name);
    return s ? `${(Math.round(s.kg * 1000) / 1000).toLocaleString()} kg` : '—';
  }

  print(): void {
    window.print();
  }
}
