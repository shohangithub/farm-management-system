import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AnimalPickerComponent } from '../../../../shared/components/animal-picker/animal-picker.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { ShedService } from '../../../farms/services/shed.service';
import { BatchService } from '../../../livestock/services/batch.service';
import { ShedList } from '../../../farms/models/shed.model';
import { ReportService } from '../../../reports/services/report.service';
import { FeedingService } from '../../services/feeding.service';
import { FeedRequirement } from '../../models/feeding.models';
import { FeedingSubnavComponent } from '../../components/feeding-subnav/feeding-subnav.component';

type Scope = 'all' | 'shed' | 'batch' | 'animals';

/**
 * Feed purchase plan: pick how many days and which animals — the page works out every item the
 * rations need (recipes broken into their ingredients), what is in stock, and how many bags to buy.
 */
@Component({
  selector: 'app-feed-planner',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatIconModule, MatButtonModule, PageHeaderComponent, AnimalPickerComponent, FeedingSubnavComponent],
  template: `
    <app-page-header title="Feed Purchase Plan · খাদ্য ক্রয় পরিকল্পনা"
      description="What to buy for the next days — for one animal, several, a shed, a batch or the whole farm."
      breadcrumbActiveNode="Purchase Plan">
      <div actions class="flex gap-2">
        <button mat-stroked-button (click)="export('pdf')" [disabled]="!result()" class="!rounded-xl !text-xs"><mat-icon class="!text-[16px]">picture_as_pdf</mat-icon> PDF</button>
        <button mat-stroked-button (click)="export('xlsx')" [disabled]="!result()" class="!rounded-xl !text-xs"><mat-icon class="!text-[16px]">file_download</mat-icon> Excel</button>
        <button mat-stroked-button (click)="print()" [disabled]="!result()" class="!rounded-xl !text-xs"><mat-icon class="!text-[16px]">print</mat-icon> Print</button>
      </div>
    </app-page-header>

    <div class="px-6 py-4 mx-auto max-w-7xl">
      <app-feeding-subnav></app-feeding-subnav>

      <!-- Inputs -->
      <div class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 p-5 space-y-4 print:hidden">
        <div class="flex flex-wrap items-end gap-4">
          <div>
            <p class="text-xs font-bold text-gray-600 dark:text-gray-300 m-0 mb-1.5">For how many days?</p>
            <div class="flex items-center gap-1.5">
              <button *ngFor="let d of dayOptions" (click)="days = d; run()" class="px-3.5 py-2 rounded-xl text-sm font-bold border"
                [ngClass]="days === d ? 'bg-emerald-600 text-white border-emerald-600' : 'border-gray-300 dark:border-gray-600 hover:border-emerald-400'">{{ d }}</button>
              <input type="number" min="1" max="366" [(ngModel)]="days" (change)="run()" class="field w-20" title="Other number of days" />
              <span class="text-xs text-gray-500">days</span>
            </div>
          </div>
          <label class="text-xs font-bold text-gray-600 dark:text-gray-300 flex flex-col gap-1.5">Starting
            <input type="date" [(ngModel)]="fromDate" (change)="run()" class="field" />
          </label>
          <label class="flex items-center gap-1.5 text-xs pb-2">
            <input type="checkbox" [(ngModel)]="projectGrowth" (change)="run()" class="accent-emerald-600" />
            Animals grow during the period (use daily gain)
          </label>
        </div>

        <div>
          <p class="text-xs font-bold text-gray-600 dark:text-gray-300 m-0 mb-1.5">For which animals?</p>
          <div class="flex flex-wrap gap-2 text-xs font-semibold">
            <button *ngFor="let s of scopes" (click)="scope.set(s.value); s.value === 'all' ? run() : null" class="px-3 py-1.5 rounded-lg border"
              [ngClass]="scope() === s.value ? 'bg-emerald-50 dark:bg-emerald-950/40 border-emerald-400 text-emerald-800 dark:text-emerald-300' : 'border-gray-300 dark:border-gray-600'">{{ s.label }}</button>
          </div>
          <div class="mt-3 max-w-xl">
            <select *ngIf="scope() === 'shed'" [(ngModel)]="shedId" (change)="run()" class="field w-full">
              <option [ngValue]="null">Choose a shed…</option>
              <option *ngFor="let s of sheds()" [ngValue]="s.id">{{ s.shedNumber }}{{ s.shedName ? ' · ' + s.shedName : '' }} ({{ s.currentOccupancy }})</option>
            </select>
            <select *ngIf="scope() === 'batch'" [(ngModel)]="batchId" (change)="run()" class="field w-full">
              <option [ngValue]="null">Choose a batch…</option>
              <option *ngFor="let b of batches()" [ngValue]="b.id">{{ b.name }}</option>
            </select>
            <div *ngIf="scope() === 'animals'" class="space-y-2">
              <app-animal-picker [multiSelect]="true" [(ngModel)]="animalIds"></app-animal-picker>
              <button mat-flat-button (click)="run()" [disabled]="!animalIds.length" class="!rounded-xl !bg-emerald-600 !text-white !text-xs">Calculate for {{ animalIds.length }} animal(s)</button>
            </div>
          </div>
        </div>
      </div>

      <div *ngIf="error()" class="mt-4 p-3 rounded-xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 text-sm text-rose-700">{{ error() }}</div>
      <div *ngIf="loading()" class="py-10 text-center text-sm text-gray-500">Calculating…</div>

      <ng-container *ngIf="result() as r">
        <!-- Summary -->
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 mt-5">
          <div class="p-4 rounded-2xl bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">Period</p>
            <h3 class="text-lg font-black m-0 mt-1">{{ r.days }} days</h3>
            <p class="text-[11px] text-gray-500 m-0">{{ r.fromDate | date:'d MMM' }} – {{ r.toDate | date:'d MMM yyyy' }}</p>
          </div>
          <div class="p-4 rounded-2xl bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">Animals</p>
            <h3 class="text-lg font-black m-0 mt-1">{{ r.animals }}</h3>
            <p *ngIf="r.animalsWithoutRation" class="text-[11px] text-rose-600 m-0">+{{ r.animalsWithoutRation }} without a ration</p>
          </div>
          <div class="p-4 rounded-2xl bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">Feed value</p>
            <h3 class="text-lg font-black m-0 mt-1">৳{{ r.totalCostOfNeedBdt | number:'1.0-0' }}</h3>
          </div>
          <div class="p-4 rounded-2xl bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800">
            <p class="text-[11px] font-bold uppercase tracking-wider text-emerald-700 m-0">To buy</p>
            <h3 class="text-lg font-black m-0 mt-1 text-emerald-700 dark:text-emerald-300">৳{{ r.totalCostToBuyBdt | number:'1.0-0' }}</h3>
            <p class="text-[11px] text-emerald-700/80 m-0">after what is in stock</p>
          </div>
        </div>

        <div *ngFor="let n of r.notes" class="mt-2 text-[11px] text-gray-500 flex items-start gap-1"><mat-icon class="!text-[14px] !w-4 !h-4">info</mat-icon>{{ n }}</div>

        <!-- Shopping list -->
        <div class="mt-5 bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 overflow-x-auto">
          <div class="px-5 py-3 border-b border-gray-100 dark:border-gray-700 flex items-center justify-between">
            <h3 class="text-sm font-black m-0">Shopping list — items</h3>
            <span class="text-[11px] text-gray-500">Recipes are broken into their ingredients</span>
          </div>
          <table class="w-full text-sm">
            <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
              <tr>
                <th class="text-left px-4 py-2">Item</th>
                <th class="text-right px-3 py-2">Per day</th>
                <th class="text-right px-3 py-2">Needed</th>
                <th class="text-right px-3 py-2">In stock</th>
                <th class="text-right px-3 py-2">To buy</th>
                <th class="text-left px-3 py-2">Packs</th>
                <th class="text-right px-4 py-2">৳ to buy</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngIf="!r.items.length"><td colspan="7" class="px-4 py-8 text-center text-gray-400">Nothing needed — no rations for these animals in this period.</td></tr>
              <tr *ngFor="let i of r.items" class="border-t border-gray-100 dark:border-gray-700/60 align-top">
                <td class="px-4 py-2">
                  <span class="font-semibold">{{ i.name }}</span>
                  <span *ngIf="!i.linkedToStock" class="ml-1 text-[10px] text-amber-600">not linked to stock</span>
                  <span class="block text-[11px] text-gray-500">{{ i.usedIn.join(', ') }}</span>
                </td>
                <td class="px-3 py-2 text-right font-mono">{{ i.perDay | number:'1.0-3' }} {{ i.baseUnit }}</td>
                <td class="px-3 py-2 text-right font-mono">{{ i.total | number:'1.0-2' }} {{ i.baseUnit }}</td>
                <td class="px-3 py-2 text-right font-mono text-gray-500">{{ i.linkedToStock ? (i.inStock | number:'1.0-2') : '—' }}</td>
                <td class="px-3 py-2 text-right font-mono font-bold" [ngClass]="i.toBuy > 0 ? 'text-rose-600' : 'text-emerald-600'">
                  {{ i.toBuy > 0 ? (i.toBuy | number:'1.0-2') + ' ' + i.baseUnit : 'enough' }}
                </td>
                <td class="px-3 py-2 text-xs">{{ i.packsToBuy ? i.packsToBuy + ' × ' + i.packName : (i.toBuy > 0 && i.linkedToStock ? '—' : '') }}</td>
                <td class="px-4 py-2 text-right font-mono">{{ i.costToBuyBdt | number:'1.0-0' }}</td>
              </tr>
            </tbody>
          </table>
          <p class="px-5 py-2 text-[11px] text-gray-500 m-0 border-t border-gray-100 dark:border-gray-700">
            Packs use each item's "usually bought in" unit — set it on the item, and the bag sizes in <a routerLink="/inventory/unit-conversions" class="underline">Inventory → Unit Conversions</a>.
          </p>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-2 gap-5 mt-5">
          <!-- Recipes -->
          <div class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 overflow-x-auto">
            <h3 class="px-5 py-3 text-sm font-black m-0 border-b border-gray-100 dark:border-gray-700">Recipes to mix</h3>
            <table class="w-full text-sm">
              <tbody>
                <tr *ngFor="let x of r.recipes" class="border-t border-gray-50 dark:border-gray-700/50">
                  <td class="px-5 py-2 font-semibold">{{ x.recipe }}</td>
                  <td class="px-3 py-2 text-right font-mono text-gray-500">{{ x.perDayKg | number:'1.0-2' }} kg/day</td>
                  <td class="px-3 py-2 text-right font-mono font-bold">{{ x.totalKg | number:'1.0-1' }} kg</td>
                  <td class="px-5 py-2 text-right font-mono">৳{{ x.costBdt | number:'1.0-0' }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Per animal -->
          <div class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 overflow-x-auto max-h-96 overflow-y-auto">
            <h3 class="px-5 py-3 text-sm font-black m-0 border-b border-gray-100 dark:border-gray-700">By animal</h3>
            <table class="w-full text-sm">
              <tbody>
                <tr *ngFor="let a of r.perAnimal" class="border-t border-gray-50 dark:border-gray-700/50">
                  <td class="px-5 py-2"><span class="font-semibold">{{ a.tag }}</span><span class="text-[11px] text-gray-500"> {{ a.shed || '' }} {{ a.weightKg ? '· ' + (a.weightKg | number:'1.0-0') + ' kg' : '' }}</span></td>
                  <td class="px-3 py-2 text-right font-mono text-gray-500">{{ a.perDayKg | number:'1.0-2' }} kg/day</td>
                  <td class="px-3 py-2 text-right font-mono font-bold">{{ a.totalKg | number:'1.0-1' }} kg</td>
                  <td class="px-5 py-2 text-right font-mono">৳{{ a.costBdt | number:'1.0-0' }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </ng-container>
    </div>
  `,
  styles: [`
    .field { padding: 0.45rem 0.65rem; border-radius: 0.6rem; border: 1px solid rgb(209 213 219); background: white; font-size: 0.8125rem; color: rgb(17 24 39); outline: none; }
    .field:focus { border-color: rgb(16 185 129); }
    :host-context(.dark) .field { background: rgb(17 24 39); border-color: rgb(55 65 81); color: white; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FeedPlannerComponent {
  private readonly feeding = inject(FeedingService);
  private readonly context = inject(WorkingContextService);
  private readonly shedService = inject(ShedService);
  private readonly batchService = inject(BatchService);
  private readonly reports = inject(ReportService);

  readonly dayOptions = [7, 10, 15, 30];
  readonly scopes: { value: Scope; label: string }[] = [
    { value: 'all', label: 'Whole farm' },
    { value: 'shed', label: 'A shed' },
    { value: 'batch', label: 'A batch' },
    { value: 'animals', label: 'Selected animals' },
  ];

  days = 15;
  fromDate = new Date().toISOString().split('T')[0];
  projectGrowth = true;
  shedId: string | null = null;
  batchId: string | null = null;
  animalIds: string[] = [];

  readonly scope = signal<Scope>('all');
  readonly sheds = signal<ShedList[]>([]);
  readonly batches = signal<{ id: string; name: string }[]>([]);
  readonly result = signal<FeedRequirement | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    const farmId = this.context.currentFarmValue?.id;
    if (farmId) {
      this.shedService.getShedsByFarm(farmId).subscribe(s => this.sheds.set(s));
      this.batchService.getBatches(farmId, undefined, 1, 200).subscribe(b => this.batches.set(b.items.map(x => ({ id: x.id, name: x.name }))));
    }
    this.run();
  }

  run(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId || !(this.days > 0)) return;
    const s = this.scope();
    if ((s === 'shed' && !this.shedId) || (s === 'batch' && !this.batchId) || (s === 'animals' && !this.animalIds.length)) return;

    this.loading.set(true);
    this.error.set(null);
    this.feeding.getFeedRequirement({
      farmId,
      days: Math.min(366, Math.max(1, Math.round(this.days))),
      fromDate: this.fromDate || null,
      animalIds: s === 'animals' ? this.animalIds : null,
      shedId: s === 'shed' ? this.shedId : null,
      batchId: s === 'batch' ? this.batchId : null,
      projectGrowth: this.projectGrowth,
      includeConsumables: true,
    }).subscribe({
      next: r => { this.result.set(r); this.loading.set(false); },
      error: err => { this.error.set(parseApiError(err, 'Could not calculate the plan.')); this.loading.set(false); },
    });
  }

  export(format: 'pdf' | 'xlsx'): void {
    const s = this.scope();
    this.reports.export('feeding.requirement', format, {
      parameters: {
        farmId: this.context.currentFarmValue?.id ?? null,
        days: String(this.days),
        fromDate: this.fromDate,
        shedId: s === 'shed' ? this.shedId : null,
        batchId: s === 'batch' ? this.batchId : null,
        animalId: s === 'animals' && this.animalIds.length === 1 ? this.animalIds[0] : null,
        projectGrowth: String(this.projectGrowth),
      }
    }).subscribe(res => {
      if (res.body) this.reports.saveBlob(res.body, this.reports.fileNameFrom(res.headers, `Feed_Purchase_Plan_${this.days}d.${format}`));
    });
  }

  print(): void {
    window.print();
  }
}
