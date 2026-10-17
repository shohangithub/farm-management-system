import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AnimalPickerComponent } from '../../../../shared/components/animal-picker/animal-picker.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { ReportService } from '../../../reports/services/report.service';
import { FeedingService } from '../../services/feeding.service';
import { AnimalNutrition, NutritionDay } from '../../models/feeding.models';
import { FeedingSubnavComponent } from '../../components/feeding-subnav/feeding-subnav.component';

/**
 * Nutrition & Growth for one animal: protein, energy and dry matter it actually ate against what
 * its weight and growth need, how much it gained, and what to change in the ration.
 */
@Component({
  selector: 'app-nutrition-growth',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatButtonModule, PageHeaderComponent, AnimalPickerComponent, FeedingSubnavComponent],
  template: `
    <app-page-header title="Nutrition & Growth · পুষ্টি ও বৃদ্ধি"
      description="What a cow ate — protein, energy, dry matter, minerals — against what its weight needs, and how well it grew."
      breadcrumbActiveNode="Nutrition & Growth">
      <div actions class="flex gap-2">
        <button mat-stroked-button (click)="export('pdf')" [disabled]="!data()" class="!rounded-xl !text-xs"><mat-icon class="!text-[16px]">picture_as_pdf</mat-icon> PDF</button>
        <button mat-stroked-button (click)="export('xlsx')" [disabled]="!data()" class="!rounded-xl !text-xs"><mat-icon class="!text-[16px]">file_download</mat-icon> Excel</button>
        <button mat-stroked-button (click)="print()" [disabled]="!data()" class="!rounded-xl !text-xs"><mat-icon class="!text-[16px]">print</mat-icon> Print</button>
      </div>
    </app-page-header>

    <div class="px-6 py-4 mx-auto max-w-7xl">
      <app-feeding-subnav></app-feeding-subnav>

      <!-- Inputs -->
      <div class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 p-5 flex flex-wrap items-end gap-4 print:hidden">
        <div class="w-72">
          <p class="text-xs font-bold text-gray-600 dark:text-gray-300 m-0 mb-1.5">Animal</p>
          <app-animal-picker [(ngModel)]="animalId" (ngModelChange)="run()"></app-animal-picker>
        </div>
        <div>
          <p class="text-xs font-bold text-gray-600 dark:text-gray-300 m-0 mb-1.5">Period</p>
          <div class="flex items-center gap-1.5">
            <button *ngFor="let d of presets" (click)="setDays(d)" class="px-3 py-2 rounded-xl text-xs font-bold border"
              [ngClass]="days === d ? 'bg-emerald-600 text-white border-emerald-600' : 'border-gray-300 dark:border-gray-600'">{{ d }} days</button>
            <input type="date" [(ngModel)]="from" (change)="days = 0; run()" class="field" />
            <span class="text-xs text-gray-500">to</span>
            <input type="date" [(ngModel)]="to" (change)="days = 0; run()" class="field" />
          </div>
        </div>
      </div>

      <div *ngIf="error()" class="mt-4 p-3 rounded-xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 text-sm text-rose-700">{{ error() }}</div>
      <div *ngIf="loading()" class="py-10 text-center text-sm text-gray-500">Calculating…</div>
      <div *ngIf="!animalId && !loading()" class="py-10 text-center text-sm text-gray-400">Choose an animal to see its nutrition and growth.</div>

      <ng-container *ngIf="data() as d">
        <!-- Verdict -->
        <div class="mt-5 p-4 rounded-2xl border flex items-start gap-3" [ngClass]="verdictClass(d.summary.verdict)">
          <mat-icon class="shrink-0">{{ d.summary.verdict === 'Well balanced' ? 'verified' : d.summary.verdict === 'No data' ? 'help' : 'warning' }}</mat-icon>
          <div class="text-sm">
            <b>{{ d.tag }} — {{ d.summary.verdict }}</b>
            <span class="text-xs opacity-80"> · {{ d.from | date:'d MMM' }} – {{ d.to | date:'d MMM yyyy' }}, fed {{ d.summary.fedDays }} of {{ d.summary.days }} days</span>
            <ul class="mt-1 mb-0 pl-4 text-xs space-y-0.5">
              <li *ngFor="let a of d.summary.advice">{{ a }}</li>
            </ul>
          </div>
        </div>

        <!-- Growth -->
        <h3 class="mt-6 mb-2 text-xs font-black uppercase tracking-wider text-gray-500">Growth</h3>
        <div class="grid grid-cols-2 md:grid-cols-5 gap-3">
          <div class="card"><p class="lbl">Weight</p><h3 class="val">{{ d.summary.startWeightKg ?? '—' }} → {{ d.summary.endWeightKg ?? '—' }} <small>kg</small></h3></div>
          <div class="card"><p class="lbl">Gain</p><h3 class="val" [class.text-rose-600]="(d.summary.gainKg ?? 0) < 0">{{ d.summary.gainKg ?? '—' }} <small>kg</small></h3></div>
          <div class="card"><p class="lbl">Daily gain (ADG)</p><h3 class="val">{{ d.summary.adgKgPerDay != null ? (d.summary.adgKgPerDay | number:'1.2-2') : '—' }} <small>kg/day</small></h3>
            <p class="sub" *ngIf="d.summary.expectedAdgFromEnergy != null">energy eaten supports ~{{ d.summary.expectedAdgFromEnergy | number:'1.2-2' }}</p></div>
          <div class="card"><p class="lbl">Feed conversion</p><h3 class="val">{{ d.summary.feedConversionKgDmPerKgGain ?? '—' }} <small>kg DM / kg gain</small></h3><p class="sub">good fattening: 6–8</p></div>
          <div class="card"><p class="lbl">Total feed</p><h3 class="val">{{ d.summary.totalFeedKg | number:'1.0-1' }} <small>kg</small></h3><p class="sub">{{ d.summary.totalDryMatterKg | number:'1.0-1' }} kg dry matter</p></div>
        </div>

        <!-- Daily nutrition vs need -->
        <h3 class="mt-6 mb-2 text-xs font-black uppercase tracking-wider text-gray-500">Daily nutrition (average)</h3>
        <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
          <div class="card">
            <p class="lbl">Protein</p>
            <h3 class="val">{{ d.summary.avgProteinGPerDay | number:'1.0-0' }} <small>g/day</small></h3>
            <div class="bar"><div [style.width.%]="cap(d.summary.avgProteinPercentOfNeed)" [ngClass]="barClass(d.summary.avgProteinPercentOfNeed)"></div></div>
            <p class="sub">{{ d.summary.avgProteinPercentOfNeed ?? '—' }}% of need · diet {{ d.summary.dietProteinPercent }}% CP</p>
          </div>
          <div class="card">
            <p class="lbl">Energy</p>
            <h3 class="val">{{ d.summary.avgEnergyMjPerDay | number:'1.0-1' }} <small>MJ/day</small></h3>
            <div class="bar"><div [style.width.%]="cap(d.summary.avgEnergyPercentOfNeed)" [ngClass]="barClass(d.summary.avgEnergyPercentOfNeed)"></div></div>
            <p class="sub">{{ d.summary.avgEnergyPercentOfNeed ?? '—' }}% of need · {{ d.summary.dietEnergyMjPerKgDm }} MJ/kg DM</p>
          </div>
          <div class="card">
            <p class="lbl">Dry matter</p>
            <h3 class="val">{{ d.summary.avgDryMatterKgPerDay | number:'1.0-2' }} <small>kg/day</small></h3>
            <p class="sub">{{ d.summary.dryMatterPercentOfBodyWeight != null ? (d.summary.dryMatterPercentOfBodyWeight + '% of body weight (aim 2.2–3%)') : '—' }}</p>
          </div>
          <div class="card">
            <p class="lbl">Fed (as given)</p>
            <h3 class="val">{{ d.summary.avgFeedKgPerDay | number:'1.0-2' }} <small>kg/day</small></h3>
            <p class="sub">{{ totals().ca | number:'1.0-1' }} g Ca · {{ totals().p | number:'1.0-1' }} g P a day</p>
          </div>
        </div>

        <div *ngFor="let n of d.notes" class="mt-2 text-[11px] text-amber-700 flex items-start gap-1"><mat-icon class="!text-[14px] !w-4 !h-4">info</mat-icon>{{ n }}</div>

        <!-- Day by day -->
        <div class="mt-6 bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 overflow-x-auto">
          <h3 class="px-5 py-3 text-sm font-black m-0 border-b border-gray-100 dark:border-gray-700">Day by day</h3>
          <table class="w-full text-xs">
            <thead class="bg-gray-50 dark:bg-gray-900/40 text-[10px] uppercase tracking-wider text-gray-500">
              <tr>
                <th class="text-left px-3 py-2">Date</th><th class="text-right px-2 py-2">Weight</th><th class="text-right px-2 py-2">Feed kg</th>
                <th class="text-right px-2 py-2">DM kg</th><th class="text-right px-2 py-2">Protein g</th><th class="text-right px-2 py-2">Need</th>
                <th class="text-right px-2 py-2">Energy MJ</th><th class="text-right px-2 py-2">Need</th><th class="text-right px-2 py-2">Ca g</th>
                <th class="text-right px-2 py-2">P g</th><th class="text-left px-3 py-2">Recipes</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let r of d.days" class="border-t border-gray-50 dark:border-gray-700/50" [class.opacity-50]="!r.feedKg">
                <td class="px-3 py-1.5 whitespace-nowrap">{{ r.date | date:'d MMM' }}</td>
                <td class="px-2 py-1.5 text-right font-mono">
                  <span [class.font-bold]="r.weightIsMeasured">{{ r.weightKg != null ? (r.weightKg | number:'1.0-1') : '—' }}</span>
                  <mat-icon *ngIf="r.weightIsMeasured" class="!text-[12px] !w-3 !h-3 text-emerald-600 align-middle" title="Weighed this day">scale</mat-icon>
                </td>
                <td class="px-2 py-1.5 text-right font-mono">{{ r.feedKg | number:'1.0-2' }}</td>
                <td class="px-2 py-1.5 text-right font-mono">{{ r.dryMatterKg | number:'1.0-2' }}</td>
                <td class="px-2 py-1.5 text-right font-mono" [ngClass]="pctClass(r.proteinPercentOfNeed)">{{ r.proteinG | number:'1.0-0' }}</td>
                <td class="px-2 py-1.5 text-right font-mono text-gray-400">{{ r.proteinNeedG ?? '—' }}</td>
                <td class="px-2 py-1.5 text-right font-mono" [ngClass]="pctClass(r.energyPercentOfNeed)">{{ r.energyMj | number:'1.0-1' }}</td>
                <td class="px-2 py-1.5 text-right font-mono text-gray-400">{{ r.energyNeedMj ?? '—' }}</td>
                <td class="px-2 py-1.5 text-right font-mono">{{ r.calciumG | number:'1.0-1' }}</td>
                <td class="px-2 py-1.5 text-right font-mono">{{ r.phosphorusG | number:'1.0-1' }}</td>
                <td class="px-3 py-1.5 text-gray-500">{{ r.recipes }}</td>
              </tr>
            </tbody>
          </table>
          <p class="px-5 py-2 text-[11px] text-gray-500 m-0 border-t border-gray-100 dark:border-gray-700">
            Needs are field guides for growing cattle: dry matter 2.5% of body weight; energy = maintenance (≈35 MJ at 300 kg) + 35 MJ per kg of gain;
            protein 14% of dry matter under 250 kg, 12% to 400 kg, 11% above. Ingredient protein and energy are read per kg of dry matter.
            Weights between weighings are estimated (bold = weighed).
          </p>
        </div>
      </ng-container>
    </div>
  `,
  styles: [`
    .field { padding: 0.45rem 0.65rem; border-radius: 0.6rem; border: 1px solid rgb(209 213 219); background: white; font-size: 0.8125rem; color: rgb(17 24 39); outline: none; }
    :host-context(.dark) .field { background: rgb(17 24 39); border-color: rgb(55 65 81); color: white; }
    .card { padding: 1rem; border-radius: 1rem; background: white; border: 1px solid rgb(243 244 246); }
    :host-context(.dark) .card { background: rgb(31 41 55); border-color: rgb(55 65 81); }
    .lbl { font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: .05em; color: rgb(107 114 128); margin: 0; }
    .val { font-size: 1.2rem; font-weight: 900; margin: .25rem 0 0; }
    .val small { font-size: .7rem; font-weight: 500; color: rgb(107 114 128); }
    .sub { font-size: 11px; color: rgb(107 114 128); margin: .25rem 0 0; }
    .bar { height: 6px; border-radius: 999px; background: rgb(229 231 235); margin-top: .4rem; overflow: hidden; }
    .bar > div { height: 100%; border-radius: 999px; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NutritionGrowthComponent {
  private readonly feeding = inject(FeedingService);
  private readonly reports = inject(ReportService);
  private readonly route = inject(ActivatedRoute);

  readonly presets = [30, 60, 90];
  days = 30;
  from = this.isoDaysAgo(29);
  to = this.isoDaysAgo(0);
  animalId: string | null = this.route.snapshot.queryParamMap.get('animalId');

  readonly data = signal<AnimalNutrition | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly totals = computed(() => {
    const fed = (this.data()?.days ?? []).filter(d => d.feedKg > 0);
    const n = fed.length || 1;
    return { ca: fed.reduce((s, d) => s + d.calciumG, 0) / n, p: fed.reduce((s, d) => s + d.phosphorusG, 0) / n };
  });

  constructor() {
    if (this.animalId) this.run();
  }

  private isoDaysAgo(n: number): string {
    return new Date(Date.now() - n * 86400000).toISOString().split('T')[0];
  }

  setDays(d: number): void {
    this.days = d;
    this.from = this.isoDaysAgo(d - 1);
    this.to = this.isoDaysAgo(0);
    this.run();
  }

  run(): void {
    const id = Array.isArray(this.animalId) ? this.animalId[0] : this.animalId;
    if (!id || !this.from || !this.to) return;
    this.loading.set(true);
    this.error.set(null);
    this.feeding.getAnimalNutrition(id, this.from, this.to).subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: err => { this.error.set(parseApiError(err, 'Could not load the nutrition report.')); this.loading.set(false); },
    });
  }

  cap(v: number | null | undefined): number {
    return Math.min(100, Math.max(0, (v ?? 0) / 1.5));
  }

  barClass(v: number | null | undefined): string {
    if (v == null) return 'bg-gray-300';
    return v < 90 ? 'bg-rose-500' : v > 125 ? 'bg-amber-500' : 'bg-emerald-500';
  }

  pctClass(v: number | null | undefined): string {
    if (v == null) return '';
    return v < 90 ? 'text-rose-600' : v > 125 ? 'text-amber-600' : 'text-emerald-700';
  }

  verdictClass(v: string): string {
    return v === 'Well balanced' ? 'bg-emerald-50 dark:bg-emerald-950/30 border-emerald-200 dark:border-emerald-800 text-emerald-900 dark:text-emerald-100'
      : v === 'No data' ? 'bg-gray-50 dark:bg-gray-800 border-gray-200 dark:border-gray-700 text-gray-700'
      : 'bg-amber-50 dark:bg-amber-950/30 border-amber-200 dark:border-amber-800 text-amber-900 dark:text-amber-100';
  }

  export(format: 'pdf' | 'xlsx'): void {
    const d = this.data();
    if (!d) return;
    this.reports.export('feeding.nutrition-growth', format, {
      parameters: { animalId: d.animalId, period: `${this.from}..${this.to}` }
    }).subscribe(res => {
      if (res.body) this.reports.saveBlob(res.body, this.reports.fileNameFrom(res.headers, `Nutrition_Growth_${d.tag}.${format}`));
    });
  }

  print(): void {
    window.print();
  }

  trackDay = (_: number, d: NutritionDay) => d.date;
}
