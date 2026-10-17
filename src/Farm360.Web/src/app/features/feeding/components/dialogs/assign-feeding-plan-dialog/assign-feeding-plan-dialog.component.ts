import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { MatDialogRef, MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FeedingService } from '../../../services/feeding.service';
import { InventoryService } from '../../../../inventory/services/inventory.service';
import { InventoryCategory, InventoryItem } from '../../../../inventory/models/inventory.models';
import { ShedService } from '../../../../farms/services/shed.service';
import { PenService } from '../../../../farms/services/pen.service';
import { BatchService } from '../../../../livestock/services/batch.service';
import { ShedList } from '../../../../farms/models/shed.model';
import { PenList } from '../../../../farms/models/pen.model';
import {
  FeedingRuleSet, FeedFormula, DirectRationRequest, RationPreview, RationPreviewRequest
} from '../../../models/feeding.models';
import { AnimalPickerComponent } from '../../../../../shared/components/animal-picker/animal-picker.component';
import { WorkingContextService } from '../../../../../core/services/working-context.service';

interface DirectRationRow {
  /** "f:<formulaId>" or "i:<inventoryItemId>" */
  source: string;
  kgPerDay: number | null;
}

/** How the wizard was opened: from the board's row actions or the animal page. */
export interface SetRationDialogData {
  animalIds?: string[];
  ruleSetId?: string;
  /** extra: jump to adding an add-on; switch: replace the current chart. */
  mode?: 'extra' | 'switch';
}

type Who = 'animals' | 'group';

/**
 * "Set ration" in three steps — who, what, check — with a live preview of every animal's kg and
 * taka a day before anything is saved. Replaces the old Assign Feeding Plan dialog.
 */
@Component({
  selector: 'app-assign-feeding-plan-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, MatDialogModule, MatButtonModule, MatIconModule, AnimalPickerComponent],
  styles: [`
    .custom-scrollbar::-webkit-scrollbar { width: 6px; }
    .custom-scrollbar::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.4); border-radius: 20px; }
    .field { width: 100%; padding: 0.5rem 0.75rem; border-radius: 0.6rem; border: 1px solid rgb(209 213 219); background: white; font-size: 0.8125rem; color: rgb(17 24 39); outline: none; }
    .field:focus { border-color: rgb(16 185 129); box-shadow: 0 0 0 3px rgba(16, 185, 129, .15); }
    :host-context(.dark) .field { background: rgb(17 24 39); border-color: rgb(55 65 81); color: white; }
  `],
  template: `
    <div class="flex flex-col h-full max-h-[92vh] bg-gray-50 dark:bg-gray-900 rounded-2xl overflow-hidden">
      <!-- Header with steps -->
      <div class="px-6 pt-4 pb-3 bg-white dark:bg-gray-800 border-b border-gray-100 dark:border-gray-700 shrink-0">
        <div class="flex justify-between items-center">
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center">
              <mat-icon class="!w-5 !h-5 !text-[20px]">restaurant</mat-icon>
            </div>
            <div>
              <h2 class="text-lg font-bold text-gray-900 dark:text-white leading-tight m-0">Set Ration <span class="text-sm font-normal text-gray-400">· খাবার নির্ধারণ</span></h2>
              <p class="text-xs text-gray-500 dark:text-gray-400 m-0">Who eats, what they eat, and a check before saving</p>
            </div>
          </div>
          <button mat-icon-button (click)="close()" class="text-gray-400"><mat-icon>close</mat-icon></button>
        </div>
        <div class="flex items-center gap-2 mt-3 text-xs font-semibold">
          <ng-container *ngFor="let s of steps; let i = index">
            <button type="button" (click)="goTo(i)" [disabled]="i > maxReachable()"
              class="flex items-center gap-1.5 px-2.5 py-1 rounded-lg disabled:opacity-40"
              [ngClass]="step() === i ? 'bg-emerald-600 text-white' : 'text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700'">
              <span class="w-5 h-5 rounded-full flex items-center justify-center text-[11px]" [ngClass]="step() === i ? 'bg-white/25' : 'bg-gray-200 dark:bg-gray-700'">{{ i + 1 }}</span>
              {{ s }}
            </button>
            <mat-icon *ngIf="i < steps.length - 1" class="!text-[16px] !w-4 !h-4 text-gray-300">chevron_right</mat-icon>
          </ng-container>
        </div>
      </div>

      <div *ngIf="errorMessage()" class="mx-6 mt-4 p-3 rounded-xl bg-red-50 dark:bg-red-950/50 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-300 text-xs flex items-start gap-2">
        <mat-icon class="!w-4 !h-4 !text-[16px] shrink-0 mt-0.5">error</mat-icon>
        <div class="flex-1">{{ errorMessage() }}</div>
        <button type="button" (click)="errorMessage.set(null)"><mat-icon class="!w-4 !h-4 !text-[16px]">close</mat-icon></button>
      </div>

      <div class="flex-1 overflow-y-auto custom-scrollbar p-6" [formGroup]="form">

        <!-- STEP 1: WHO -->
        <section *ngIf="step() === 0" class="space-y-4">
          <div class="grid grid-cols-2 gap-3">
            <button type="button" (click)="who.set('animals')" class="p-3.5 rounded-xl border-2 text-left flex gap-3"
              [ngClass]="who() === 'animals' ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950/40' : 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800'">
              <mat-icon [ngClass]="who() === 'animals' ? 'text-emerald-600' : 'text-gray-400'">pets</mat-icon>
              <span><b class="block text-sm">Selected animals</b><span class="text-xs text-gray-500">Each animal gets its own amount by its weight or age.</span></span>
            </button>
            <button type="button" (click)="who.set('group')" class="p-3.5 rounded-xl border-2 text-left flex gap-3"
              [ngClass]="who() === 'group' ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950/40' : 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800'">
              <mat-icon [ngClass]="who() === 'group' ? 'text-emerald-600' : 'text-gray-400'">holiday_village</mat-icon>
              <span><b class="block text-sm">A whole shed, pen or batch</b><span class="text-xs text-gray-500">Same amount per head. Animals that move in later are fed too.</span></span>
            </button>
          </div>

          <div *ngIf="who() === 'animals'">
            <app-animal-picker formControlName="animalIds" [multiSelect]="true"></app-animal-picker>
            <p class="text-[11px] text-gray-500 mt-1">{{ animalIds().length }} selected</p>
          </div>

          <div *ngIf="who() === 'group'" class="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs">
            <label class="font-semibold">Shed
              <select class="field mt-1" [ngModel]="shedId()" [ngModelOptions]="{ standalone: true }" (ngModelChange)="selectShed($event)">
                <option [ngValue]="null">—</option>
                <option *ngFor="let s of sheds()" [ngValue]="s.id">{{ s.shedNumber }}{{ s.shedName ? ' · ' + s.shedName : '' }} ({{ s.currentOccupancy }})</option>
              </select>
            </label>
            <label class="font-semibold">Pen (optional)
              <select class="field mt-1" [ngModel]="penId()" [ngModelOptions]="{ standalone: true }" (ngModelChange)="penId.set($event)" [disabled]="!shedId()">
                <option [ngValue]="null">Whole shed</option>
                <option *ngFor="let p of pens()" [ngValue]="p.id">{{ p.penName || p.penNumber }} ({{ p.currentOccupancy }})</option>
              </select>
            </label>
            <label class="font-semibold">or Batch
              <select class="field mt-1" [ngModel]="batchId()" [ngModelOptions]="{ standalone: true }" (ngModelChange)="batchId.set($event)">
                <option [ngValue]="null">—</option>
                <option *ngFor="let b of batches()" [ngValue]="b.id">{{ b.name }}</option>
              </select>
            </label>
          </div>
        </section>

        <!-- STEP 2: WHAT -->
        <section *ngIf="step() === 1" class="space-y-5">
          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <h3 class="text-sm font-bold m-0">Feed chart <span class="font-normal text-xs text-gray-500">— amount changes with weight or age</span></h3>
              <a href="/feeding/rules" target="_blank" class="text-xs text-emerald-700 dark:text-emerald-300 font-semibold inline-flex items-center gap-1">
                <mat-icon class="!text-[14px] !w-4 !h-4">open_in_new</mat-icon> New chart
              </a>
            </div>
            <div class="rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 divide-y divide-gray-100 dark:divide-gray-700/60 max-h-56 overflow-y-auto custom-scrollbar">
              <label *ngFor="let r of charts()" class="flex items-center gap-3 px-3 py-2 cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-700/30 text-sm">
                <input type="checkbox" class="accent-emerald-600 w-4 h-4" [checked]="selectedRuleSetIds().includes(r.id)" (change)="toggleRuleSet(r.id)" />
                <span class="flex-1">
                  <span class="font-semibold text-gray-900 dark:text-white">{{ r.name }}</span>
                  <span class="block text-[11px] text-gray-500">{{ planTypeLabel(r.planType) }} · {{ r.rules.length }} row(s)</span>
                </span>
              </label>
              <div *ngIf="!charts().length" class="px-3 py-4 text-xs text-gray-400 text-center">No active feed charts yet.</div>
            </div>
            <label *ngIf="who() === 'animals' && selectedRuleSetIds().length" class="flex items-start gap-2 text-xs p-2.5 rounded-lg bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 cursor-pointer">
              <input type="checkbox" class="accent-amber-600 mt-0.5" [checked]="replace()" (change)="replace.set(!replace())" />
              <span><b>Switch</b> — stop the animals' other feed charts and use this one instead. Extras (add-ons) are kept.</span>
            </label>
          </div>

          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <h3 class="text-sm font-bold m-0">Fixed amount / extras <span class="font-normal text-xs text-gray-500">— a recipe or stock item, kg a day</span></h3>
              <button type="button" (click)="addDirect()" class="px-3 py-1.5 text-xs font-semibold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 rounded-lg border border-emerald-200 dark:border-emerald-800 inline-flex items-center gap-1">
                <mat-icon class="!text-[16px] !w-4 !h-4">add</mat-icon> Add
              </button>
            </div>
            <p class="text-[11px] text-gray-500 m-0">e.g. <b>Extra Mineral 0.1 kg/day</b> on top of the chart, or a recipe at a set amount without any chart.</p>
            <div *ngFor="let d of directRows(); let i = index" class="flex items-center gap-2">
              <select class="field flex-1" [ngModel]="d.source" [ngModelOptions]="{ standalone: true }" (ngModelChange)="updateDirect(i, { source: $event })">
                <option value="">— choose —</option>
                <optgroup label="Recipes">
                  <option *ngFor="let f of formulas()" [value]="'f:' + f.id">{{ f.title }}</option>
                </optgroup>
                <optgroup label="Stock items (fed as they are)">
                  <option *ngFor="let it of stockItems()" [value]="'i:' + it.id">{{ it.name }} — {{ it.currentStock | number:'1.0-2' }} {{ it.unitOfMeasure }} in stock</option>
                </optgroup>
              </select>
              <div class="relative w-36 shrink-0">
                <input type="number" step="0.01" min="0" class="field pr-16" placeholder="0" [ngModel]="d.kgPerDay" [ngModelOptions]="{ standalone: true }" (ngModelChange)="updateDirect(i, { kgPerDay: $event })" />
                <span class="absolute right-2 top-1/2 -translate-y-1/2 text-[11px] font-bold text-gray-400">{{ who() === 'group' ? 'kg/head' : 'kg/day' }}</span>
              </div>
              <button type="button" (click)="removeDirect(i)" class="p-1 text-gray-400 hover:text-red-600"><mat-icon class="!text-[18px]">delete</mat-icon></button>
            </div>
          </div>
        </section>

        <!-- STEP 3: CHECK -->
        <section *ngIf="step() === 2" class="space-y-4">
          <div class="grid grid-cols-2 gap-3 text-xs">
            <label class="font-semibold">Start date
              <input type="date" formControlName="startDate" class="field mt-1" />
            </label>
            <label class="font-semibold">End date (optional)
              <input type="date" formControlName="endDate" class="field mt-1" />
            </label>
          </div>

          <div *ngIf="previewLoading()" class="text-xs text-gray-500 py-6 text-center">Calculating…</div>
          <ng-container *ngIf="preview() as pv">
            <div class="grid grid-cols-3 gap-3 text-xs">
              <div class="p-3 rounded-xl bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700">Animals<b class="block text-lg">{{ pv.animals.length }}</b></div>
              <div class="p-3 rounded-xl bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700">Feed a day<b class="block text-lg">{{ pv.totalKgPerDay | number:'1.0-2' }} kg</b></div>
              <div class="p-3 rounded-xl bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700">Cost a day<b class="block text-lg">৳{{ pv.totalCostPerDayBdt | number:'1.0-0' }}</b></div>
            </div>
            <p *ngIf="who() === 'group'" class="text-[11px] text-gray-500 m-0">Group plan: each head gets the amount below; {{ pv.groupHeadCount }} animal(s) are in it today.</p>

            <div class="rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 overflow-x-auto">
              <table class="w-full text-xs">
                <thead class="bg-gray-50 dark:bg-gray-900/50 text-[10px] uppercase tracking-wider text-gray-500">
                  <tr><th class="text-left px-3 py-2">Animal</th><th class="text-left px-3 py-2">Weight · age</th><th class="text-left px-3 py-2">Gets</th><th class="text-right px-3 py-2">kg/day</th><th class="text-right px-3 py-2">৳/day</th></tr>
                </thead>
                <tbody>
                  <tr *ngIf="!pv.animals.length"><td colspan="5" class="px-3 py-6 text-center text-gray-400">No animals in this selection.</td></tr>
                  <tr *ngFor="let a of pv.animals" class="border-t border-gray-100 dark:border-gray-700/60 align-top">
                    <td class="px-3 py-2 font-semibold">{{ a.tag }}</td>
                    <td class="px-3 py-2 text-gray-500">{{ a.weightKg ? (a.weightKg | number:'1.0-1') + ' kg' : 'no weight' }}<span *ngIf="a.ageDays !== null && a.ageDays !== undefined"> · {{ a.ageDays }} d</span></td>
                    <td class="px-3 py-2">
                      <div *ngFor="let it of a.items" [class.line-through]="a.alreadyOn.includes(it.name)">
                        {{ it.name }}: <b>{{ it.kgPerDay | number:'1.0-3' }} kg</b> <span class="text-gray-400">{{ it.feedName }}</span>
                      </div>
                      <div *ngFor="let n of a.alreadyOn" class="text-[11px] text-gray-500">Already on "{{ n }}" — kept as it is.</div>
                      <div *ngFor="let n of a.willEnd" class="text-[11px] text-amber-700 dark:text-amber-400">Ends "{{ n }}".</div>
                      <div *ngFor="let w of a.warnings" class="text-[11px] text-amber-700 dark:text-amber-400">⚠ {{ w }}</div>
                    </td>
                    <td class="px-3 py-2 text-right font-mono">{{ a.totalKgPerDay | number:'1.0-3' }}</td>
                    <td class="px-3 py-2 text-right font-mono">{{ a.totalCostPerDayBdt | number:'1.0-0' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </ng-container>
        </section>
      </div>

      <!-- Footer -->
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-t border-gray-100 dark:border-gray-700 flex items-center justify-between gap-3 shrink-0">
        <span class="text-xs text-red-500">{{ stepIssue() }}</span>
        <div class="flex gap-3">
          <button type="button" (click)="step() === 0 ? close() : goTo(step() - 1)" [disabled]="isSubmitting()"
            class="px-5 py-2.5 text-sm font-semibold text-gray-700 dark:text-gray-300 border border-gray-300 dark:border-gray-600 rounded-xl">
            {{ step() === 0 ? 'Cancel' : 'Back' }}
          </button>
          <button *ngIf="step() < 2" type="button" (click)="goTo(step() + 1)" [disabled]="!!stepIssue()"
            class="px-5 py-2.5 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 rounded-xl">Next</button>
          <button *ngIf="step() === 2" type="button" (click)="submit()" [disabled]="isSubmitting() || previewLoading() || !preview()?.animals?.length"
            class="px-5 py-2.5 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 rounded-xl inline-flex items-center min-w-[120px] justify-center">
            <mat-icon *ngIf="isSubmitting()" class="animate-spin !w-5 !h-5 !text-[20px] mr-2">refresh</mat-icon>
            Save ration
          </button>
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AssignFeedingPlanDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<AssignFeedingPlanDialogComponent>);
  private readonly data = inject<SetRationDialogData | null>(MAT_DIALOG_DATA, { optional: true });
  private readonly feedingService = inject(FeedingService);
  private readonly inventoryService = inject(InventoryService);
  private readonly shedService = inject(ShedService);
  private readonly penService = inject(PenService);
  private readonly batchService = inject(BatchService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly contextService = inject(WorkingContextService);
  private readonly destroyRef = inject(DestroyRef);

  readonly steps = ['Who', 'What', 'Check & save'];
  readonly step = signal(0);
  readonly who = signal<Who>('animals');

  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly ruleSets = signal<FeedingRuleSet[]>([]);
  readonly formulas = signal<FeedFormula[]>([]);
  readonly stockItems = signal<InventoryItem[]>([]);
  readonly sheds = signal<ShedList[]>([]);
  readonly pens = signal<PenList[]>([]);
  readonly batches = signal<{ id: string; name: string }[]>([]);

  readonly animalIds = signal<string[]>([]);
  readonly shedId = signal<string | null>(null);
  readonly penId = signal<string | null>(null);
  readonly batchId = signal<string | null>(null);
  readonly selectedRuleSetIds = signal<string[]>([]);
  readonly directRows = signal<DirectRationRow[]>([]);
  readonly replace = signal(false);

  readonly preview = signal<RationPreview | null>(null);
  readonly previewLoading = signal(false);

  /** Charts the user made; one-line "Direct · …" rule sets are created for extras and reused behind the scenes. */
  readonly charts = computed(() => this.ruleSets().filter(r => !r.name.startsWith('Direct · ')));

  private readonly today = new Date().toISOString().split('T')[0];

  readonly form = this.fb.group({
    animalIds: [[] as string[]],
    startDate: [this.today],
    endDate: [null as string | null],
  });

  readonly validDirect = computed(() => this.directRows().filter(d => d.source && (d.kgPerDay ?? 0) > 0));

  readonly whoIssue = computed(() => {
    if (this.who() === 'animals') return this.animalIds().length ? '' : 'Choose at least one animal.';
    return this.shedId() || this.batchId() ? '' : 'Choose a shed or a batch.';
  });

  readonly whatIssue = computed(() => {
    if (!this.selectedRuleSetIds().length && !this.validDirect().length) return 'Choose a feed chart or add a fixed amount.';
    if (this.directRows().some(d => (d.source && !((d.kgPerDay ?? 0) > 0)) || (!d.source && (d.kgPerDay ?? 0) > 0)))
      return 'Each fixed amount needs both a feed and kg.';
    return '';
  });

  readonly stepIssue = computed(() => this.step() === 0 ? this.whoIssue() : this.step() === 1 ? this.whatIssue() : '');
  readonly maxReachable = computed(() => this.whoIssue() ? 0 : this.whatIssue() ? 1 : 2);

  ngOnInit(): void {
    const farmId = this.contextService.currentFarmValue?.id;

    this.form.controls.animalIds.valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(v => this.animalIds.set(v ?? []));

    if (this.data?.animalIds?.length) {
      this.form.patchValue({ animalIds: this.data.animalIds });
      this.animalIds.set(this.data.animalIds);
      this.step.set(1);
    }
    if (this.data?.mode === 'extra') this.addDirect();
    if (this.data?.mode === 'switch') this.replace.set(true);

    this.feedingService.getRuleSets().subscribe(res => {
      this.ruleSets.set(res.filter(r => r.isActive));
      if (this.data?.ruleSetId) this.selectedRuleSetIds.set([this.data.ruleSetId]);
    });
    this.feedingService.getFormulas(1, 500).subscribe(res =>
      this.formulas.set(res.items.filter(f => !f.title.endsWith('(direct item)'))));
    this.inventoryService.getItems({ farmId, pageSize: 500 }).subscribe(res =>
      this.stockItems.set(res.items.filter(i => ![InventoryCategory.Equipment, InventoryCategory.Medicine, InventoryCategory.Vaccine].includes(i.category))));

    if (farmId) {
      this.shedService.getShedsByFarm(farmId).subscribe(s => this.sheds.set(s));
      this.batchService.getBatches(farmId, undefined, 1, 200).subscribe(b =>
        this.batches.set(b.items.map(x => ({ id: x.id, name: x.name }))));
    }
  }

  selectShed(id: string | null): void {
    this.shedId.set(id);
    this.penId.set(null);
    this.pens.set([]);
    if (id) this.penService.getPensByShed(id).subscribe(p => this.pens.set(p));
  }

  goTo(i: number): void {
    if (i > this.maxReachable()) return;
    this.step.set(i);
    if (i === 2) this.loadPreview();
  }

  planTypeLabel(type: string): string {
    return ({ WeightQuantity: 'By weight — fixed kg', WeightPercentage: 'By weight — % of body weight', AgeBased: 'By age', FixedQuantity: 'Same for every animal' } as Record<string, string>)[type] ?? type;
  }

  toggleRuleSet(id: string): void {
    this.selectedRuleSetIds.update(ids => ids.includes(id) ? ids.filter(x => x !== id) : [...ids, id]);
  }

  addDirect(): void {
    this.directRows.update(rows => [...rows, { source: '', kgPerDay: null }]);
  }

  updateDirect(i: number, change: Partial<DirectRationRow>): void {
    this.directRows.update(rows => rows.map((r, k) => k === i ? { ...r, ...change } : r));
  }

  removeDirect(i: number): void {
    this.directRows.update(rows => rows.filter((_, k) => k !== i));
  }

  private directRations(): DirectRationRequest[] {
    return this.validDirect().map(d => ({
      formulaId: d.source.startsWith('f:') ? d.source.substring(2) : null,
      inventoryItemId: d.source.startsWith('i:') ? d.source.substring(2) : null,
      kgPerDay: +d.kgPerDay!,
    }));
  }

  private request(farmId: string): RationPreviewRequest {
    const group = this.who() === 'group';
    return {
      farmId,
      animalIds: group ? [] : this.animalIds(),
      ruleSetIds: this.selectedRuleSetIds(),
      directRations: this.directRations(),
      shedId: group ? this.shedId() : null,
      penId: group ? this.penId() : null,
      batchId: group ? this.batchId() : null,
      replaceExistingCharts: !group && this.replace(),
    };
  }

  private loadPreview(): void {
    const farmId = this.contextService.currentFarmValue?.id;
    if (!farmId) return;
    this.previewLoading.set(true);
    this.preview.set(null);
    this.feedingService.previewRation(this.request(farmId)).subscribe({
      next: p => { this.preview.set(p); this.previewLoading.set(false); },
      error: err => { this.errorMessage.set(err.error?.detail || 'Could not calculate the preview.'); this.previewLoading.set(false); }
    });
  }

  close(): void {
    this.dialogRef.close();
  }

  submit(): void {
    const farmId = this.contextService.currentFarmValue?.id;
    if (!farmId) return;
    const req = this.request(farmId);
    const raw = this.form.getRawValue();
    const planType = this.ruleSets().find(r => req.ruleSetIds!.includes(r.id))?.planType ?? 'FixedQuantity';

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.feedingService.assignPlan({
      farmId,
      feedingRuleSetIds: req.ruleSetIds ?? [],
      planType: planType as FeedingRuleSet['planType'],
      startDate: raw.startDate || this.today,
      endDate: raw.endDate || null,
      animalIds: req.animalIds ?? [],
      directRations: req.directRations ?? [],
      shedId: req.shedId,
      penId: req.penId,
      batchId: req.batchId,
      replaceExistingCharts: req.replaceExistingCharts,
    }).subscribe({
      next: () => {
        this.snackBar.open('Ration saved', 'Close', { duration: 3000 });
        this.dialogRef.close(true);
      },
      error: (err) => {
        let msg = 'Failed to save the ration';
        if (err.error?.errors) msg = Object.values(err.error.errors).flat().join(' ');
        else if (err.error?.detail) msg = err.error.detail;
        else if (err.error?.title) msg = err.error.title;
        this.errorMessage.set(msg);
        this.isSubmitting.set(false);
      }
    });
  }
}
