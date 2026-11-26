import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FeedingService } from '../../services/feeding.service';
import { InventoryService } from '../../../inventory/services/inventory.service';
import { InventoryCategory, InventoryItem } from '../../../inventory/models/inventory.models';
import {
  DailyFeedSheetResult, FeedFormula, FeedSheetAnimal, FeedSheetFed, FeedSheetQuantityMode, FeedSheetSplit
} from '../../models/feeding.models';
import { UnitPickerComponent, UnitSelection } from '../../../inventory/components/unit-picker/unit-picker.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { FeedingSubnavComponent } from '../../components/feeding-subnav/feeding-subnav.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { NotificationService } from '../../../../shared/services/notification.service';
import { ConfirmService } from '../../../../shared/services/confirm.service';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { parseApiError } from '../../../../core/utils/error-parser';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

/** One feed on the sheet, as edited on screen. `source` is "f:<formulaId>" or "i:<itemId>". */
interface SheetLine {
  key: number;
  source: string;
  quantity: number | null;
  mode: FeedSheetQuantityMode;
  unit: UnitSelection | null;
}

type GroupBy = 'shed' | 'batch' | 'none';

interface AnimalGroup {
  key: string;
  label: string;
  animals: FeedSheetAnimal[];
}

/**
 * Daily Feed Sheet: record what animals were actually fed on a day, in any mix of recipes and stock
 * items, without a feeding plan. Pick the animals (a whole shed or batch in one tap), add the feeds,
 * or copy the last day's feeds, and save. Stock, cost and every report follow straight away.
 */
@Component({
  selector: 'app-daily-feed-sheet',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, MatButtonModule, MatIconModule, MatTooltipModule,
    UnitPickerComponent, PageHeaderComponent, FeedingSubnavComponent, MoneyPipe, TranslatePipe],
  styles: [`
    .field { width: 100%; padding: 0.45rem 0.65rem; border-radius: 0.6rem; border: 1px solid rgb(209 213 219); background: white; font-size: 0.8125rem; color: rgb(17 24 39); outline: none; }
    .field:focus { border-color: rgb(16 185 129); box-shadow: 0 0 0 3px rgba(16, 185, 129, .15); }
    :host-context(.dark) .field { background: rgb(17 24 39); border-color: rgb(55 65 81); color: white; }
    .card { background: white; border: 1px solid rgb(243 244 246); border-radius: 1rem; }
    :host-context(.dark) .card { background: rgb(31 41 55); border-color: rgb(55 65 81); }
  `],
  template: `
    <app-page-header [title]="'Record Daily Feed' | t"
      [description]="'Write down what your animals ate today. No feeding plan needed.' | t"
      [breadcrumbActiveNode]="'Record Daily Feed' | t">
    </app-page-header>

    <div class="space-y-4">
      <app-feeding-subnav></app-feeding-subnav>

      <div *ngIf="!farmId()" class="card p-6 text-sm text-gray-500">{{ 'Choose a farm at the top of the page to start.' | t }}</div>

      <ng-container *ngIf="farmId()">
        <!-- Date -->
        <div class="card p-3 flex flex-wrap items-center gap-2 text-xs">
          <button mat-icon-button (click)="shiftDay(-1)" [attr.aria-label]="'Previous day' | t"><mat-icon>chevron_left</mat-icon></button>
          <input type="date" class="field !w-44" [max]="today" [ngModel]="date()" (ngModelChange)="setDate($event)" />
          <button mat-icon-button (click)="shiftDay(1)" [disabled]="date() >= today" [attr.aria-label]="'Next day' | t"><mat-icon>chevron_right</mat-icon></button>
          <button mat-stroked-button class="!rounded-xl" (click)="setDate(today)" [disabled]="date() === today">{{ 'Today' | t }}</button>
          <span class="ml-auto text-gray-500">{{ '{n} animals on the farm · {fed} already fed this day' | t: { n: animals().length, fed: fedCount() } }}</span>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-5 gap-4">
          <!-- ── Animals ─────────────────────────────────────────── -->
          <section class="card p-4 lg:col-span-2 flex flex-col min-h-[420px]">
            <div class="flex items-center justify-between gap-2 mb-3">
              <h3 class="text-sm font-bold text-gray-900 dark:text-white m-0">1. {{ 'Animals' | t }} @if (!i18n.isBangla) {<span class="font-normal text-gray-400">· পশু</span>}</h3>
              <span class="text-xs font-semibold text-emerald-700 dark:text-emerald-300">{{ '{n} selected' | t: { n: selected().size } }}</span>
            </div>

            <div class="flex flex-wrap gap-2 mb-2 text-xs">
              <input type="search" class="field flex-1 min-w-[8rem]" [placeholder]="'Search tag…' | t" [ngModel]="search()" (ngModelChange)="search.set($event)" />
              <select class="field !w-auto" [ngModel]="groupBy()" (ngModelChange)="groupBy.set($event)" [attr.aria-label]="'Group animals by' | t">
                <option value="shed">{{ 'By shed' | t }}</option>
                <option value="batch">{{ 'By batch' | t }}</option>
                <option value="none">{{ 'No groups' | t }}</option>
              </select>
            </div>
            <div class="flex gap-2 mb-2 text-xs">
              <button class="text-emerald-700 dark:text-emerald-300 font-semibold hover:underline" (click)="selectAll(true)">{{ 'Select all' | t }}</button>
              <span class="text-gray-300">|</span>
              <button class="text-gray-500 font-semibold hover:underline" (click)="selectAll(false)">{{ 'Clear' | t }}</button>
              <span class="text-gray-300">|</span>
              <button class="text-gray-500 font-semibold hover:underline" (click)="selectNotFed()" [matTooltip]="'Select every animal with nothing recorded yet this day' | t">{{ 'Not fed yet' | t }}</button>
            </div>

            <div *ngIf="loading()" class="text-xs text-gray-400 py-6 text-center">{{ 'Loading…' | t }}</div>
            <div *ngIf="!loading() && animals().length === 0" class="text-xs text-gray-400 py-6 text-center">{{ 'No animals were on the farm on this day.' | t }}</div>

            <div class="overflow-y-auto max-h-[60vh] -mx-1 px-1 space-y-3">
              <div *ngFor="let g of groups()">
                <label *ngIf="groupBy() !== 'none'" class="flex items-center gap-2 py-1 text-xs font-bold text-gray-700 dark:text-gray-200 cursor-pointer">
                  <input type="checkbox" class="accent-emerald-600" [checked]="groupState(g) === 'all'" [indeterminate]="groupState(g) === 'some'" (change)="toggleGroup(g)" />
                  {{ g.label }} <span class="font-normal text-gray-400">({{ g.animals.length }})</span>
                </label>
                <label *ngFor="let a of g.animals"
                  class="flex items-start gap-2 px-2 py-1.5 rounded-lg cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-700/40 text-xs"
                  [class.bg-emerald-50]="selected().has(a.animalId)" [class.dark:bg-emerald-950/30]="selected().has(a.animalId)">
                  <input type="checkbox" class="accent-emerald-600 mt-0.5" [checked]="selected().has(a.animalId)" (change)="toggle(a.animalId)" />
                  <span class="flex-1 min-w-0">
                    <span class="font-semibold text-gray-900 dark:text-white">{{ a.tagId }}</span>
                    <span class="text-gray-500"> · {{ a.breed }} · {{ a.weightKg ? (a.weightKg | number:'1.0-0') + ' ' + ('kg' | t) : ('no weight' | t) }}</span>
                    <span class="block text-[11px] text-gray-400 truncate" *ngIf="a.activePlans.length">{{ 'On plan: {plans}' | t: { plans: a.activePlans.join(', ') } }}</span>
                  </span>
                  <span *ngIf="fedTotal(a) as t" class="shrink-0 text-[11px] font-semibold text-emerald-700 dark:text-emerald-300" [matTooltip]="'Already recorded this day' | t">
                    ✓ {{ t | money }}
                  </span>
                </label>
              </div>
            </div>
          </section>

          <!-- ── Feeds ───────────────────────────────────────────── -->
          <section class="card p-4 lg:col-span-3 space-y-3">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <h3 class="text-sm font-bold text-gray-900 dark:text-white m-0">2. {{ 'What they ate' | t }} @if (!i18n.isBangla) {<span class="font-normal text-gray-400">· কী খেয়েছে</span>}</h3>
              <div class="flex gap-2">
                <button mat-stroked-button class="!rounded-xl !text-xs" (click)="copyLastDay()" [disabled]="selected().size === 0 || copying()"
                  [matTooltip]="'Fill the sheet with what these animals ate on their last fed day' | t">
                  <mat-icon class="!text-[16px] !w-4 !h-4">content_copy</mat-icon> {{ (copying() ? 'Copying…' : 'Same as last day') | t }}
                </button>
                <button mat-stroked-button class="!rounded-xl !text-xs" (click)="addLine()">
                  <mat-icon class="!text-[16px] !w-4 !h-4">add</mat-icon> {{ 'Add feed' | t }}
                </button>
              </div>
            </div>

            <p *ngIf="copiedFrom()" class="text-[11px] text-gray-500 m-0">{{ 'Copied from {date}. Feeds every animal got the same amount of are "each"; feeds that varied by animal are a "total" split by weight again (scaled to the animals selected now). Change anything that was different today.' | t: { date: (copiedFrom() | date:'EEE dd MMM yyyy') } }}</p>

            <div *ngFor="let line of lines(); let i = index" class="rounded-xl border border-gray-100 dark:border-gray-700 p-3 grid grid-cols-12 gap-2 items-end text-xs">
              <label class="col-span-12 sm:col-span-5">
                <span class="text-gray-500">{{ 'Feed' | t }}</span>
                <select class="field mt-1" [ngModel]="line.source" (ngModelChange)="patch(i, { source: $event, unit: null })">
                  <option value="">{{ '— choose —' | t }}</option>
                  <optgroup [label]="'Recipes' | t">
                    <option *ngFor="let f of formulas()" [value]="'f:' + f.id">{{ f.title }}</option>
                  </optgroup>
                  <optgroup [label]="'Stock items (fed as they are)' | t">
                    <option *ngFor="let it of feedItems()" [value]="'i:' + it.id">{{ it.name }} — {{ it.currentStock | number:'1.0-2' }} {{ it.unitOfMeasure }}</option>
                  </optgroup>
                  <optgroup [label]="'Supplements & medicines' | t">
                    <option *ngFor="let it of supplementItems()" [value]="'i:' + it.id">{{ it.name }} — {{ it.currentStock | number:'1.0-2' }} {{ it.unitOfMeasure }}</option>
                  </optgroup>
                </select>
              </label>
              <label class="col-span-4 sm:col-span-2">
                <span class="text-gray-500">{{ 'Quantity' | t }}</span>
                <input type="number" min="0" step="0.01" class="field mt-1" [ngModel]="line.quantity" (ngModelChange)="patch(i, { quantity: $event })" />
              </label>
              <div class="col-span-4 sm:col-span-2">
                <span class="text-gray-500">{{ 'Unit' | t }}</span>
                <div class="mt-1">
                  <app-unit-picker *ngIf="isItem(line); else kg" [itemId]="itemId(line)" [quantity]="line.quantity" [useDefault]="false"
                    (selectionChange)="patch(i, { unit: $event })"></app-unit-picker>
                  <ng-template #kg><div class="field text-gray-500">{{ 'kg' | t }}</div></ng-template>
                </div>
              </div>
              <label class="col-span-3 sm:col-span-2">
                <span class="text-gray-500">{{ 'Amount is' | t }}</span>
                <select class="field mt-1" [ngModel]="line.mode" (ngModelChange)="patch(i, { mode: $event })">
                  <option value="PerHead">{{ 'each' | t }}</option>
                  <option value="GroupTotal">{{ 'total' | t }}</option>
                </select>
              </label>
              <div class="col-span-1 flex justify-end">
                <button mat-icon-button (click)="removeLine(i)" [attr.aria-label]="'Remove feed' | t"><mat-icon class="text-gray-400">delete</mat-icon></button>
              </div>
              <p *ngIf="lineSummary(line) as s" class="col-span-12 m-0 text-[11px] text-gray-500">{{ s }}</p>
            </div>

            <div *ngIf="hasGroupTotal()" class="flex flex-wrap items-center gap-3 text-xs rounded-xl bg-gray-50 dark:bg-gray-900/40 p-3">
              <span class="font-semibold text-gray-700 dark:text-gray-200">{{ 'Split totals' | t }}</span>
              <label class="flex items-center gap-1"><input type="radio" class="accent-emerald-600" name="split" value="ByWeight" [ngModel]="split()" (ngModelChange)="split.set($event)" /> {{ 'by body weight (heavier eat more)' | t }}</label>
              <label class="flex items-center gap-1"><input type="radio" class="accent-emerald-600" name="split" value="Equal" [ngModel]="split()" (ngModelChange)="split.set($event)" /> {{ 'equally' | t }}</label>
            </div>

            <!-- Plans -->
            <div *ngIf="selectedOnPlan().length" class="rounded-xl border border-amber-200 dark:border-amber-800 bg-amber-50 dark:bg-amber-950/30 p-3 text-xs text-amber-900 dark:text-amber-200 space-y-2">
              <div>
                {{ '{n} selected animal(s) are on a feeding plan. When you save, their planned feed for this day is skipped, so nothing is counted twice.' | t: { n: selectedOnPlan().length } }}
              </div>
              <div *ngIf="selectedWithOwnPlan().length" class="flex flex-wrap items-center gap-2">
                <span>{{ 'Feeding them by sheet from now on?' | t }}</span>
                <button mat-stroked-button class="!rounded-xl !text-xs" (click)="endPlans()" [disabled]="busy()">
                  {{ 'End their own plans ({n})' | t: { n: selectedWithOwnPlan().length } }}
                </button>
              </div>
            </div>

            <label class="block text-xs">
              <span class="text-gray-500">{{ 'Note (optional)' | t }}</span>
              <input type="text" class="field mt-1" maxlength="200" [placeholder]="'e.g. Green grass ran out, gave extra straw' | t" [ngModel]="notes()" (ngModelChange)="notes.set($event)" />
            </label>

            <div class="flex flex-wrap items-center justify-between gap-3 pt-1">
              <span class="text-xs text-gray-500">{{ saveHint() }}</span>
              <button mat-flat-button color="primary" class="!rounded-xl" (click)="save()" [disabled]="!canSave() || busy()">
                <mat-icon>task_alt</mat-icon> {{ (busy() ? 'Saving…' : 'Save & confirm') | t }}
              </button>
            </div>
          </section>
        </div>

        <!-- ── Already recorded ─────────────────────────────────── -->
        <section class="card p-4" *ngIf="recorded().length">
          <h3 class="text-sm font-bold text-gray-900 dark:text-white m-0 mb-1">{{ 'Recorded for {date}' | t: { date: (date() | date:'EEE dd MMM yyyy') } }} @if (!i18n.isBangla) {<span class="font-normal text-gray-400">· যা রেকর্ড হয়েছে</span>}</h3>
          <p class="text-[11px] text-gray-500 m-0 mb-3">{{ 'To correct a feed, remove it here and add the right one above. The stock is returned and the cost is reversed.' | t }}{{ selected().size ? ' ' + ('Showing the selected animals.' | t) : '' }}</p>
          <div class="overflow-x-auto">
            <table class="w-full text-xs">
              <thead class="text-gray-500 text-left">
                <tr><th class="py-1 pr-3">{{ 'Animal' | t }}</th><th class="py-1 pr-3">{{ 'Feed' | t }}</th><th class="py-1 pr-3 text-right">{{ 'Quantity' | t }}</th><th class="py-1 pr-3 text-right">{{ 'Cost' | t }}</th><th class="py-1 pr-3">{{ 'From' | t }}</th><th></th></tr>
              </thead>
              <tbody>
                <tr *ngFor="let r of recorded()" class="border-t border-gray-100 dark:border-gray-700">
                  <td class="py-1.5 pr-3 font-semibold">{{ r.tag }}</td>
                  <td class="py-1.5 pr-3">{{ r.fed.feed }} <span *ngIf="!r.fed.confirmed" class="text-amber-600">{{ '(planned, not confirmed)' | t }}</span></td>
                  <td class="py-1.5 pr-3 text-right">{{ r.fed.quantity | number:'1.0-3' }} {{ r.fed.unit }}</td>
                  <td class="py-1.5 pr-3 text-right">{{ r.fed.costBdt | money:2 }}</td>
                  <td class="py-1.5 pr-3 text-gray-500">{{ r.fed.source | t }}</td>
                  <td class="py-1.5 text-right">
                    <button *ngIf="r.fed.canRemove" mat-icon-button (click)="removeRecorded(r.tag, r.fed)" [disabled]="busy()" [matTooltip]="'Remove this feed and return its stock' | t">
                      <mat-icon class="!text-[18px] text-red-500">undo</mat-icon>
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </ng-container>
    </div>
  `,
})
export class DailyFeedSheetComponent implements OnInit {
  private readonly feeding = inject(FeedingService);
  private readonly inventory = inject(InventoryService);
  private readonly context = inject(WorkingContextService);
  private readonly notify = inject(NotificationService);
  private readonly confirmService = inject(ConfirmService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly i18n = inject(I18nService);

  readonly today = toLocalIsoDate();
  readonly farmId = signal<string | null>(null);
  readonly date = signal(this.today);
  readonly animals = signal<FeedSheetAnimal[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly copying = signal(false);
  readonly copiedFrom = signal<string | null>(null);

  readonly selected = signal<Set<string>>(new Set());
  readonly search = signal('');
  readonly groupBy = signal<GroupBy>('shed');

  readonly formulas = signal<FeedFormula[]>([]);
  readonly stockItems = signal<InventoryItem[]>([]);
  readonly feedItems = computed(() => this.stockItems().filter(i => i.category !== InventoryCategory.Medicine));
  readonly supplementItems = computed(() => this.stockItems().filter(i => i.category === InventoryCategory.Medicine));

  private nextKey = 1;
  readonly lines = signal<SheetLine[]>([this.newLine()]);
  readonly split = signal<FeedSheetSplit>('ByWeight');
  readonly notes = signal('');

  readonly fedCount = computed(() => this.animals().filter(a => a.fed.some(f => f.confirmed)).length);

  readonly groups = computed<AnimalGroup[]>(() => {
    const term = this.search().trim().toLowerCase();
    const list = this.animals().filter(a => !term || a.tagId.toLowerCase().includes(term));
    const by = this.groupBy();
    if (by === 'none') return [{ key: 'all', label: this.i18n.t('All animals'), animals: list }];
    const map = new Map<string, AnimalGroup>();
    for (const a of list) {
      const key = (by === 'shed' ? a.shedId : a.batchId) ?? 'none';
      const label = (by === 'shed' ? (a.shedName ? this.i18n.t('Shed {name}', { name: a.shedName }) : null) : (a.batchName ? this.i18n.t('Batch {name}', { name: a.batchName }) : null))
        ?? this.i18n.t(by === 'shed' ? 'No shed' : 'No batch');
      if (!map.has(key)) map.set(key, { key, label, animals: [] });
      map.get(key)!.animals.push(a);
    }
    return [...map.values()].sort((x, y) => x.label.localeCompare(y.label));
  });

  readonly selectedAnimals = computed(() => this.animals().filter(a => this.selected().has(a.animalId)));
  readonly selectedOnPlan = computed(() => this.selectedAnimals().filter(a => a.activePlans.length > 0));
  readonly selectedWithOwnPlan = computed(() => this.selectedAnimals().filter(a => a.hasOwnPlan));
  readonly hasGroupTotal = computed(() => this.lines().some(l => l.mode === 'GroupTotal'));
  readonly validLines = computed(() => this.lines().filter(l => l.source && (Number(l.quantity) || 0) > 0));
  readonly canSave = computed(() => this.selected().size > 0 && this.validLines().length > 0
    && this.validLines().length === this.lines().filter(l => l.source || l.quantity).length);

  readonly saveHint = computed(() => {
    const n = this.selected().size;
    const f = this.validLines().length;
    if (!n) return this.i18n.t('Choose the animals that were fed.');
    if (!f) return this.i18n.t('Add what they ate.');
    return this.i18n.t('{f} feed(s) for {n} animal(s). Stock is checked first; if anything is short, nothing is saved.', { f, n });
  });

  readonly recorded = computed(() => {
    const sel = this.selected();
    return this.animals()
      .filter(a => sel.size === 0 || sel.has(a.animalId))
      .flatMap(a => a.fed.map(fed => ({ tag: a.tagId, fed })));
  });

  ngOnInit(): void {
    this.context.currentFarm$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(farm => {
      this.farmId.set(farm?.id ?? null);
      this.selected.set(new Set());
      if (farm?.id) {
        this.loadOptions(farm.id);
        this.load();
      } else {
        this.animals.set([]);
      }
    });
  }

  // ── Loading ────────────────────────────────────────────────────────────
  private loadOptions(farmId: string): void {
    this.feeding.getFormulas(1, 500).subscribe(res =>
      this.formulas.set(res.items.filter(f => !f.title.endsWith('(direct item)'))));
    this.inventory.getItems({ farmId, pageSize: 500 }).subscribe(res =>
      this.stockItems.set(res.items.filter(i => ![InventoryCategory.Equipment, InventoryCategory.Vaccine].includes(i.category))));
  }

  load(): void {
    const farmId = this.farmId();
    if (!farmId) return;
    this.loading.set(true);
    this.feeding.getFeedSheet(farmId, this.date()).subscribe({
      next: sheet => {
        this.animals.set(sheet.animals);
        const present = new Set(sheet.animals.map(a => a.animalId));
        this.selected.set(new Set([...this.selected()].filter(id => present.has(id))));
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.notify.error(parseApiError(err, this.i18n.t('Could not load the animals.')));
      }
    });
  }

  setDate(value: string): void {
    if (!value) return;
    this.date.set(value > this.today ? this.today : value);
    this.copiedFrom.set(null);
    this.load();
  }

  shiftDay(days: number): void {
    const d = new Date(this.date() + 'T00:00:00');
    d.setDate(d.getDate() + days);
    this.setDate(toLocalIsoDate(d));
  }

  // ── Selection ──────────────────────────────────────────────────────────
  toggle(id: string): void {
    const next = new Set(this.selected());
    if (next.has(id)) next.delete(id); else next.add(id);
    this.selected.set(next);
  }

  groupState(g: AnimalGroup): 'all' | 'some' | 'none' {
    const sel = this.selected();
    const n = g.animals.filter(a => sel.has(a.animalId)).length;
    return n === 0 ? 'none' : n === g.animals.length ? 'all' : 'some';
  }

  toggleGroup(g: AnimalGroup): void {
    const next = new Set(this.selected());
    const add = this.groupState(g) !== 'all';
    for (const a of g.animals) {
      if (add) next.add(a.animalId); else next.delete(a.animalId);
    }
    this.selected.set(next);
  }

  selectAll(on: boolean): void {
    this.selected.set(on ? new Set(this.groups().flatMap(g => g.animals.map(a => a.animalId))) : new Set());
  }

  selectNotFed(): void {
    this.selected.set(new Set(this.animals().filter(a => !a.fed.some(f => f.confirmed)).map(a => a.animalId)));
  }

  fedTotal(a: FeedSheetAnimal): number {
    return a.fed.filter(f => f.confirmed).reduce((s, f) => s + f.costBdt, 0);
  }

  // ── Lines ──────────────────────────────────────────────────────────────
  private newLine(source = '', quantity: number | null = null): SheetLine {
    return { key: this.nextKey++, source, quantity, mode: 'PerHead', unit: null };
  }

  addLine(): void {
    this.lines.set([...this.lines(), this.newLine()]);
  }

  removeLine(index: number): void {
    const next = this.lines().filter((_, i) => i !== index);
    this.lines.set(next.length ? next : [this.newLine()]);
  }

  patch(index: number, change: Partial<SheetLine>): void {
    this.lines.set(this.lines().map((l, i) => i === index ? { ...l, ...change } : l));
  }

  isItem(line: SheetLine): boolean {
    return line.source.startsWith('i:');
  }

  itemId(line: SheetLine): string | null {
    return this.isItem(line) ? line.source.slice(2) : null;
  }

  private unitOf(line: SheetLine): string {
    if (!this.isItem(line)) return 'kg';
    return line.unit?.name || this.stockItems().find(i => i.id === line.source.slice(2))?.unitOfMeasure || '';
  }

  lineSummary(line: SheetLine): string | null {
    const q = Number(line.quantity) || 0;
    const n = this.selected().size;
    if (!line.source || q <= 0 || n === 0) return null;
    const unit = this.isItem(line) ? this.unitOf(line) : this.i18n.t('kg');
    if (line.mode === 'PerHead') return this.i18n.t('{n} × {q} = {total} {unit} in all.', { n, q, total: this.fmt(q * n), unit });
    return this.i18n.t(this.split() === 'Equal'
      ? '{q} {unit} shared equally by {n} animals (about {each} {unit} each).'
      : '{q} {unit} shared by body weight by {n} animals (about {each} {unit} each).',
      { q: this.fmt(q), unit, n, each: this.fmt(q / n) });
  }

  private fmt(v: number): string {
    return (Math.round(v * 1000) / 1000).toString();
  }

  copyLastDay(): void {
    const farmId = this.farmId();
    if (!farmId || this.selected().size === 0) return;
    this.copying.set(true);
    this.feeding.copyLastFeedSheet(farmId, this.date(), [...this.selected()]).subscribe({
      next: copy => {
        this.copying.set(false);
        if (!copy.fromDate || copy.lines.length === 0) {
          this.notify.info(this.i18n.t('No earlier feeding found for these animals in the last 60 days.'));
          return;
        }
        this.copiedFrom.set(copy.fromDate);
        this.split.set(copy.split);
        // A group total was for the animals fed that day; scale it to the animals selected now.
        const selectedCount = this.selected().size;
        this.lines.set(copy.lines.map(l => {
          const source = l.inventoryItemId ? 'i:' + l.inventoryItemId : 'f:' + l.formulaId;
          const quantity = l.mode === 'GroupTotal' && l.headsFed > 0 && l.headsFed !== selectedCount
            ? Math.round(l.quantity / l.headsFed * selectedCount * 1000) / 1000
            : l.quantity;
          return { ...this.newLine(source, quantity), mode: l.mode };
        }));
      },
      error: err => {
        this.copying.set(false);
        this.notify.error(parseApiError(err, this.i18n.t('Could not copy the last day.')));
      }
    });
  }

  // ── Save ───────────────────────────────────────────────────────────────
  save(): void {
    const farmId = this.farmId();
    if (!farmId || !this.canSave()) return;
    this.busy.set(true);
    this.feeding.saveFeedSheet({
      farmId,
      date: this.date(),
      animalIds: [...this.selected()],
      lines: this.validLines().map(l => ({
        formulaId: l.source.startsWith('f:') ? l.source.slice(2) : null,
        inventoryItemId: l.source.startsWith('i:') ? l.source.slice(2) : null,
        quantity: Number(l.quantity),
        mode: l.mode,
        unitId: this.isItem(l) ? l.unit?.unitId ?? null : null,
      })),
      split: this.split(),
      skipPlannedEntries: true,
      notes: this.notes() || null,
    }).subscribe({
      next: res => {
        this.busy.set(false);
        const notes = res.notes.length ? ' ' + res.notes.join(' ') : '';
        this.notify.success(this.resultMessage(res) + notes, res.notes.length ? { duration: 10000 } : undefined);
        this.lines.set([this.newLine()]);
        this.notes.set('');
        this.copiedFrom.set(null);
        this.load();
      },
      error: err => {
        this.busy.set(false);
        this.notify.error(parseApiError(err, this.i18n.t('The feed sheet could not be saved.')));
      }
    });
  }

  private resultMessage(res: DailyFeedSheetResult): string {
    const skipped = res.plannedEntriesSkipped ? ' ' + this.i18n.t('{n} planned entry(ies) replaced.', { n: res.plannedEntriesSkipped }) : '';
    return this.i18n.t('Saved: {animals} animal(s) fed, {records} feed record(s), cost ৳{cost}.', { animals: res.animalsFed, records: res.entriesRecorded, cost: res.totalCostBdt.toFixed(2) }) + skipped;
  }

  removeRecorded(tag: string, fed: FeedSheetFed): void {
    const farmId = this.farmId();
    if (!farmId) return;
    this.confirmService.confirm({
      title: this.i18n.t('Remove this feed?'),
      message: this.i18n.t('{feed} ({qty} {unit}) for {tag} on {date}.', { feed: fed.feed, qty: fed.quantity, unit: fed.unit, tag, date: this.date() })
        + ' ' + this.i18n.t(fed.confirmed ? 'The stock is returned and the cost reversed.' : 'The planned feed is skipped.'),
      confirmText: this.i18n.t('Remove'),
      destructive: true,
    }).subscribe(ok => {
      if (!ok) return;
      this.busy.set(true);
      this.feeding.removeFeedSheetEntry(farmId, fed.entryId).subscribe({
        next: () => {
          this.busy.set(false);
          this.notify.success(this.i18n.t('Feed removed.'));
          this.load();
        },
        error: err => {
          this.busy.set(false);
          this.notify.error(parseApiError(err, this.i18n.t('Could not remove the feed.')));
        }
      });
    });
  }

  endPlans(): void {
    const farmId = this.farmId();
    const ids = this.selectedWithOwnPlan().map(a => a.animalId);
    if (!farmId || ids.length === 0) return;
    this.confirmService.confirm({
      title: this.i18n.t('Feed these animals by sheet?'),
      message: this.i18n.t('Their own feeding plans ({n}) end today, so no planned feed is created for them any more. You can assign a plan again at any time.', { n: ids.length }),
      confirmText: this.i18n.t('End plans'),
    }).subscribe(ok => {
      if (!ok) return;
      this.busy.set(true);
      this.feeding.endFeedingPlansForAnimals(farmId, ids).subscribe({
        next: res => {
          this.busy.set(false);
          const groups = res.groupPlansStillCovering.length
            ? ' ' + this.i18n.t('Still fed by group plan(s): {plans}. Pause or stop those in Feeding → Feeding Plans if needed.', { plans: res.groupPlansStillCovering.join(', ') })
            : '';
          this.notify.success(this.i18n.t('{n} plan(s) ended.', { n: res.plansEnded }) + groups, groups ? { duration: 10000 } : undefined);
          this.load();
        },
        error: err => {
          this.busy.set(false);
          this.notify.error(parseApiError(err, this.i18n.t('Could not end the plans.')));
        }
      });
    });
  }
}
