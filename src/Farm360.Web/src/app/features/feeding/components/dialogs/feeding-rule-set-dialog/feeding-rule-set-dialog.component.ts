import { Component, ChangeDetectionStrategy, inject, signal, OnInit, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSnackBar } from '@angular/material/snack-bar';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable } from 'rxjs';
import { FeedingService } from '../../../services/feeding.service';
import { InventoryService } from '../../../../inventory/services/inventory.service';
import { InventoryCategory, InventoryItem } from '../../../../inventory/models/inventory.models';
import { WorkingContextService } from '../../../../../core/services/working-context.service';
import {
  FeedingRuleSet,
  FeedingPlanType,
  TargetAnimalType,
  FeedingPurpose,
  FeedCategory,
  FeedCategoryNames,
  FeedFormula
} from '../../../models/feeding.models';

interface PlanTypeOption {
  value: FeedingPlanType;
  icon: string;
  title: string;
  summary: string;
  example: string;
}

interface RuleRow {
  min: number | null;
  max: number | null;
  quantity: number | null;
}

/**
 * Create / edit a feeding rule set in three steps: what it is for, how feed is decided, and the
 * table of bands. The bands table is compact (one line per band), sorted, checked for gaps and
 * overlaps as you type, and can be generated in one go ("from 80 to 450 kg every 50 kg").
 */
@Component({
  selector: 'app-feeding-rule-set-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule
  ],
  styles: [`
    .custom-scrollbar::-webkit-scrollbar { width: 6px; }
    .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
    .custom-scrollbar::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.4); border-radius: 20px; }
    .field { width: 100%; padding: 0.5rem 0.75rem; border-radius: 0.6rem; border: 1px solid rgb(209 213 219); background: white; font-size: 0.875rem; color: rgb(17 24 39); outline: none; }
    .field:focus { border-color: rgb(16 185 129); box-shadow: 0 0 0 3px rgba(16, 185, 129, .15); }
    .cell { width: 100%; padding: 0.375rem 0.5rem; border-radius: 0.5rem; border: 1px solid rgb(229 231 235); background: white; font-size: 0.8125rem; color: rgb(17 24 39); outline: none; }
    .cell:focus { border-color: rgb(16 185 129); }
    .cell.invalid { border-color: rgb(248 113 113); background: rgb(254 242 242); }
    :host-context(.dark) .field, :host-context(.dark) .cell { background: rgb(17 24 39); border-color: rgb(55 65 81); color: white; }
    .step { width: 1.5rem; height: 1.5rem; border-radius: 999px; background: rgb(16 185 129); color: white; font-size: .75rem; font-weight: 800; display: inline-flex; align-items: center; justify-content: center; }
    input[type=number]::-webkit-inner-spin-button { opacity: .4; }
  `],
  template: `
    <div class="flex flex-col h-full max-h-[90vh] bg-gray-50 dark:bg-gray-900 rounded-2xl overflow-hidden">
      <!-- Header -->
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-b border-gray-100 dark:border-gray-700 flex justify-between items-center shrink-0">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center">
            <mat-icon class="!w-5 !h-5 !text-[20px]">rule</mat-icon>
          </div>
          <div>
            <h2 class="text-lg font-bold text-gray-900 dark:text-white leading-tight m-0">{{ isEditing ? 'Edit' : 'New' }} Feed Chart <span class="text-sm font-normal text-gray-400">· খাদ্য তালিকা</span></h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 m-0">How much each animal should eat per day</p>
          </div>
        </div>
        <button mat-icon-button (click)="close()" class="text-gray-400"><mat-icon>close</mat-icon></button>
      </div>

      <div class="flex-1 overflow-y-auto custom-scrollbar p-6 space-y-7" [formGroup]="form">

        <!-- Step 1: basics -->
        <section class="space-y-3">
          <h3 class="flex items-center gap-2 text-sm font-bold text-gray-900 dark:text-white m-0"><span class="step">1</span> Name &amp; animals</h3>
          <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div class="md:col-span-3">
              <input formControlName="name" type="text" class="field" placeholder="Chart name, e.g. Fattening bull — concentrate" />
              <p *ngIf="form.get('name')?.touched && form.get('name')?.invalid" class="text-[11px] text-red-500 mt-1 mb-0">Give the rule set a name.</p>
            </div>
            <label class="text-xs font-semibold text-gray-600 dark:text-gray-300">Animal
              <select formControlName="targetAnimalType" class="field mt-1">
                <option *ngFor="let a of animalOptions" [value]="a">{{ a }}</option>
              </select>
            </label>
            <label class="text-xs font-semibold text-gray-600 dark:text-gray-300">Purpose
              <select formControlName="feedingPurpose" class="field mt-1">
                <option *ngFor="let p of purposeOptions" [value]="p">{{ p }}</option>
              </select>
            </label>
            <div class="flex items-end pb-2">
              <mat-checkbox formControlName="isActive" color="primary">Active (can be used in plans)</mat-checkbox>
            </div>
          </div>
        </section>

        <!-- Step 2: how feed is decided -->
        <section class="space-y-3">
          <h3 class="flex items-center gap-2 text-sm font-bold text-gray-900 dark:text-white m-0"><span class="step">2</span> How is the daily feed decided?</h3>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <button type="button" *ngFor="let p of planTypes" (click)="setPlanType(p.value)"
              class="text-left p-3.5 rounded-xl border-2 transition-all flex gap-3"
              [ngClass]="planType === p.value
                ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950/40'
                : 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 hover:border-emerald-300'">
              <mat-icon class="shrink-0" [ngClass]="planType === p.value ? 'text-emerald-600' : 'text-gray-400'">{{ p.icon }}</mat-icon>
              <span>
                <span class="block text-sm font-bold text-gray-900 dark:text-white">{{ p.title }}</span>
                <span class="block text-xs text-gray-600 dark:text-gray-300 mt-0.5">{{ p.summary }}</span>
                <span class="block text-[11px] text-emerald-700 dark:text-emerald-400 mt-1">{{ p.example }}</span>
              </span>
            </button>
          </div>
        </section>

        <!-- Step 3: the bands -->
        <section class="space-y-3">
          <div class="flex items-center justify-between flex-wrap gap-2">
            <h3 class="flex items-center gap-2 text-sm font-bold text-gray-900 dark:text-white m-0">
              <span class="step">3</span> {{ hasRange ? 'Feed for each ' + (isAge ? 'age' : 'weight') + ' range' : 'Daily feed per animal' }}
            </h3>
            <div class="flex items-center gap-2">
              <button *ngIf="hasRange" type="button" (click)="showGenerator.set(!showGenerator())"
                class="px-3 py-1.5 text-xs font-semibold rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 inline-flex items-center gap-1">
                <mat-icon class="!text-[16px] !w-4 !h-4">auto_awesome</mat-icon> Quick fill
              </button>
              <button *ngIf="hasRange && rules.length > 1" type="button" (click)="sortRules()"
                class="px-3 py-1.5 text-xs font-semibold rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 inline-flex items-center gap-1">
                <mat-icon class="!text-[16px] !w-4 !h-4">sort</mat-icon> Sort
              </button>
            </div>
          </div>

          <!-- Same ration for every row -->
          <div class="p-3 rounded-xl bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 flex flex-wrap items-center gap-3 text-xs">
            <span class="font-semibold text-gray-700 dark:text-gray-200">Ration for all rows:</span>
            <select class="cell !w-auto min-w-[200px]" [ngModel]="defaultFormulaId" [ngModelOptions]="{ standalone: true }" (ngModelChange)="applyFormulaToAll($event)">
              <option [ngValue]="null">— choose a formula —</option>
              <optgroup label="Formulas / rations">
                <option *ngFor="let f of formulas()" [ngValue]="f.id">{{ f.title }}{{ f.totalCostPerKgBdt ? ' (৳' + (f.totalCostPerKgBdt | number:'1.0-2') + '/kg)' : '' }}</option>
              </optgroup>
              <optgroup *ngIf="stockItems().length" label="Stock items (fed as they are)">
                <option *ngFor="let it of stockItems()" [ngValue]="'i:' + it.id">{{ it.name }} (৳{{ it.weightedAverageCostBdt | number:'1.0-2' }}/{{ it.unitOfMeasure }})</option>
              </optgroup>
            </select>
            <span class="text-gray-400">or</span>
            <select class="cell !w-auto" [ngModel]="defaultFeedType" [ngModelOptions]="{ standalone: true }" (ngModelChange)="applyFeedTypeToAll($event)">
              <option *ngFor="let c of categoryOptions" [ngValue]="c.value">{{ c.label }} (no formula)</option>
            </select>
            <span class="text-gray-400">— you can still change a single row below.</span>
          </div>

          <!-- Quick fill -->
          <div *ngIf="showGenerator()" class="p-3 rounded-xl bg-emerald-50/70 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800 text-xs flex flex-wrap items-end gap-3">
            <label class="font-semibold">From ({{ unitLabel }})
              <input type="number" class="cell mt-1 !w-24" [(ngModel)]="gen.from" [ngModelOptions]="{ standalone: true }" />
            </label>
            <label class="font-semibold">To ({{ unitLabel }})
              <input type="number" class="cell mt-1 !w-24" [(ngModel)]="gen.to" [ngModelOptions]="{ standalone: true }" />
            </label>
            <label class="font-semibold">Every ({{ unitLabel }})
              <input type="number" class="cell mt-1 !w-20" [(ngModel)]="gen.step" [ngModelOptions]="{ standalone: true }" />
            </label>
            <label class="font-semibold">First {{ quantityLabel }}
              <input type="number" step="0.01" class="cell mt-1 !w-24" [(ngModel)]="gen.startQty" [ngModelOptions]="{ standalone: true }" />
            </label>
            <label class="font-semibold">Increase per row
              <input type="number" step="0.01" class="cell mt-1 !w-24" [(ngModel)]="gen.increment" [ngModelOptions]="{ standalone: true }" />
            </label>
            <button type="button" (click)="generate()" class="px-3 py-2 rounded-lg bg-emerald-600 text-white font-semibold">Create {{ generatorCount() }} rows</button>
            <p class="w-full m-0 text-gray-500">Replaces the rows below. Example: 81 → 450 kg every 50 kg, starting at 1.5 kg and adding 0.5 kg per row.</p>
          </div>

          <!-- Table -->
          <div class="rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 overflow-x-auto">
            <table class="w-full text-sm" formArrayName="rules">
              <thead class="bg-gray-50 dark:bg-gray-900/50 text-[11px] uppercase tracking-wider text-gray-500">
                <tr>
                  <th class="px-3 py-2 text-left w-8">#</th>
                  <ng-container *ngIf="hasRange">
                    <th class="px-2 py-2 text-left">From ({{ unitLabel }})</th>
                    <th class="px-2 py-2 text-left">To ({{ unitLabel }})</th>
                  </ng-container>
                  <th class="px-2 py-2 text-left">Formula / ration</th>
                  <th class="px-2 py-2 text-left w-36">{{ quantityLabel }} <span class="text-red-500">*</span></th>
                  <th class="px-2 py-2 text-left">Works out to</th>
                  <th class="w-10"></th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let rule of rules.controls; let i = index" [formGroupName]="i" class="border-t border-gray-100 dark:border-gray-700/60 align-top">
                  <td class="px-3 py-2 text-xs text-gray-400 pt-3.5">{{ i + 1 }}</td>
                  <ng-container *ngIf="hasRange">
                    <td class="px-2 py-2">
                      <input type="number" class="cell" [ngClass]="{ invalid: rowError(i) }"
                        [formControlName]="isAge ? 'minAgeDays' : 'minWeightKg'" placeholder="0" />
                    </td>
                    <td class="px-2 py-2">
                      <input type="number" class="cell" [ngClass]="{ invalid: rowError(i) }"
                        [formControlName]="isAge ? 'maxAgeDays' : 'maxWeightKg'" placeholder="no limit" />
                    </td>
                  </ng-container>
                  <td class="px-2 py-2 min-w-[180px]">
                    <select formControlName="formulaId" class="cell">
                      <option [ngValue]="null">No formula — by category</option>
                      <optgroup label="Formulas / rations">
                        <option *ngFor="let f of formulas()" [ngValue]="f.id">{{ f.title }}</option>
                      </optgroup>
                      <optgroup *ngIf="stockItems().length" label="Stock items (fed as they are)">
                        <option *ngFor="let it of stockItems()" [ngValue]="'i:' + it.id">{{ it.name }}</option>
                      </optgroup>
                    </select>
                    <select *ngIf="!rule.get('formulaId')?.value" formControlName="feedType" class="cell mt-1">
                      <option *ngFor="let c of categoryOptions" [ngValue]="c.value">{{ c.label }}</option>
                    </select>
                  </td>
                  <td class="px-2 py-2">
                    <div class="relative">
                      <input type="number" step="0.01" formControlName="quantityValue" class="cell pr-9"
                        [ngClass]="{ invalid: rule.get('quantityValue')?.touched && rule.get('quantityValue')?.invalid }" placeholder="0" />
                      <span class="absolute right-2 top-1/2 -translate-y-1/2 text-[11px] font-bold text-gray-400">{{ isPercent ? '%' : 'kg' }}</span>
                    </div>
                  </td>
                  <td class="px-2 py-2 text-[11px] text-gray-500 dark:text-gray-400 pt-3">{{ worksOut(i) }}</td>
                  <td class="px-2 py-2 text-right">
                    <button type="button" (click)="duplicateRule(i)" title="Copy this row below" class="p-1 text-gray-400 hover:text-emerald-600">
                      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">content_copy</mat-icon>
                    </button>
                    <button type="button" (click)="removeRule(i)" title="Remove row" class="p-1 text-gray-400 hover:text-red-600" [disabled]="rules.length === 1">
                      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">delete</mat-icon>
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
            <div *ngIf="hasRange || rules.length === 0" class="border-t border-gray-100 dark:border-gray-700/60 p-2">
              <button type="button" (click)="addRule()" class="w-full py-2 text-xs font-semibold text-emerald-700 dark:text-emerald-300 rounded-lg hover:bg-emerald-50 dark:hover:bg-emerald-950/40 inline-flex items-center justify-center gap-1">
                <mat-icon class="!text-[16px] !w-4 !h-4">add</mat-icon>
                {{ hasRange ? 'Add next range' + (nextStartHint() !== null ? ' (starts at ' + nextStartHint() + ' ' + unitLabel + ')' : '') : 'Add row' }}
              </button>
            </div>
          </div>

          <!-- Checks -->
          <div *ngIf="issues().length" class="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 text-xs text-amber-800 dark:text-amber-200 space-y-1">
            <div class="font-bold flex items-center gap-1"><mat-icon class="!text-[16px] !w-4 !h-4">warning</mat-icon> Please check</div>
            <div *ngFor="let issue of issues()">• {{ issue }}</div>
          </div>
          <div *ngIf="!issues().length && hasRange && rules.length > 1" class="text-xs text-emerald-700 dark:text-emerald-400 flex items-center gap-1">
            <mat-icon class="!text-[16px] !w-4 !h-4">check_circle</mat-icon>
            Ranges cover {{ coverage() }} with no gaps or overlaps.
          </div>
        </section>

        <!-- Notes -->
        <section>
          <label class="text-xs font-semibold text-gray-600 dark:text-gray-300">Notes (optional)
            <textarea formControlName="baseNotes" rows="2" class="field mt-1" placeholder="e.g. Split into two meals, morning and evening"></textarea>
          </label>
        </section>
      </div>

      <!-- Footer -->
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-t border-gray-100 dark:border-gray-700 flex items-center justify-between gap-3 shrink-0">
        <span class="text-xs text-gray-500">{{ rules.length }} row(s)<span *ngIf="blockingIssue()" class="text-red-500"> · {{ blockingIssue() }}</span></span>
        <div class="flex gap-3">
          <button type="button" (click)="close()" [disabled]="isSubmitting()"
            class="px-5 py-2.5 text-sm font-semibold text-gray-700 dark:text-gray-300 border border-gray-300 dark:border-gray-600 rounded-xl">Cancel</button>
          <button type="button" (click)="submit()" [disabled]="isSubmitting()"
            class="px-5 py-2.5 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 rounded-xl inline-flex items-center min-w-[130px] justify-center">
            <mat-icon *ngIf="isSubmitting()" class="animate-spin !w-5 !h-5 !text-[20px] mr-2">refresh</mat-icon>
            {{ isEditing ? 'Save changes' : 'Create feed chart' }}
          </button>
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeedingRuleSetDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<FeedingRuleSetDialogComponent>);
  private readonly data = inject<FeedingRuleSet>(MAT_DIALOG_DATA);
  private readonly feedingService = inject(FeedingService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly inventoryService = inject(InventoryService);
  private readonly context = inject(WorkingContextService);

  readonly isEditing = !!this.data;
  readonly isSubmitting = signal(false);
  readonly formulas = signal<FeedFormula[]>([]);
  /** Stock items that can be fed as they are (a one-ingredient "direct item" formula is made on save). */
  readonly stockItems = signal<InventoryItem[]>([]);
  readonly showGenerator = signal(false);
  /** Bumped on every form change so the OnPush checks below re-evaluate. */
  readonly revision = signal(0);

  readonly animalOptions = Object.values(TargetAnimalType);
  readonly purposeOptions = Object.values(FeedingPurpose);

  readonly planTypes: PlanTypeOption[] = [
    { value: FeedingPlanType.WeightQuantity, icon: 'monitor_weight', title: 'By weight — fixed kg', summary: 'Heavier animals get more. You set kg per day for each weight range.', example: 'e.g. 151–200 kg → 2 kg/day' },
    { value: FeedingPlanType.WeightPercentage, icon: 'percent', title: 'By weight — % of body weight', summary: 'Feed is a % of each animal’s live weight, so it grows automatically.', example: 'e.g. 3% → a 400 kg bull gets 12 kg/day' },
    { value: FeedingPlanType.AgeBased, icon: 'cake', title: 'By age', summary: 'Feed changes as the animal gets older. You set kg per day for each age range.', example: 'e.g. 0–60 days → 1.5 kg/day' },
    { value: FeedingPlanType.FixedQuantity, icon: 'straighten', title: 'Same for every animal', summary: 'One fixed amount per head per day, whatever the size or age.', example: 'e.g. 5 kg/day for each cow' },
  ];

  readonly categoryOptions = [
    FeedCategory.Concentrate, FeedCategory.Forage, FeedCategory.Silage,
    FeedCategory.Mineral, FeedCategory.Additive, FeedCategory.Byproduct
  ].map(value => ({ value, label: FeedCategoryNames[value] }));

  defaultFormulaId: string | null = null;
  defaultFeedType: FeedCategory = FeedCategory.Concentrate;
  gen = { from: 81, to: 450, step: 50, startQty: 1.5, increment: 0.5 };

  readonly form = this.fb.group({
    name: ['', [Validators.required]],
    planType: [FeedingPlanType.WeightQuantity, [Validators.required]],
    targetAnimalType: [TargetAnimalType.Cattle, [Validators.required]],
    feedingPurpose: [FeedingPurpose.Growth, [Validators.required]],
    isActive: [true],
    baseNotes: [''],
    rules: this.fb.array([])
  });

  get rules(): FormArray {
    return this.form.get('rules') as FormArray;
  }

  get planType(): string {
    return this.form.get('planType')?.value ?? FeedingPlanType.WeightQuantity;
  }

  get isAge(): boolean { return this.planType === FeedingPlanType.AgeBased; }
  get isPercent(): boolean { return this.planType === FeedingPlanType.WeightPercentage; }
  get hasRange(): boolean { return this.planType !== FeedingPlanType.FixedQuantity; }
  get unitLabel(): string { return this.isAge ? 'days' : 'kg'; }
  get quantityLabel(): string { return this.isPercent ? '% of body weight' : 'kg / head / day'; }

  ngOnInit(): void {
    this.feedingService.getFormulas(1, 500).subscribe({
      next: (res) => { this.formulas.set(res.items); this.revision.update(v => v + 1); }
    });

    this.inventoryService.getItems({ farmId: this.context.currentFarmValue?.id, pageSize: 500 }).subscribe({
      next: res => {
        this.stockItems.set(res.items.filter(i => ![InventoryCategory.Equipment, InventoryCategory.Medicine, InventoryCategory.Vaccine].includes(i.category)));
        this.revision.update(v => v + 1);
      }
    });

    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.revision.update(v => v + 1));

    if (this.isEditing) {
      this.form.patchValue({
        name: this.data.name,
        planType: this.data.planType,
        targetAnimalType: this.data.targetAnimalType,
        feedingPurpose: this.data.feedingPurpose,
        isActive: this.data.isActive,
        baseNotes: this.data.baseNotes
      });
      const sorted = [...(this.data.rules ?? [])].sort((a, b) => this.startOf(a) - this.startOf(b));
      sorted.forEach(rule => this.rules.push(this.createRuleFormGroup(rule)));
      const formulaIds = new Set(sorted.map(r => r.formulaId ?? null));
      if (formulaIds.size === 1) this.defaultFormulaId = [...formulaIds][0];
      if (this.rules.length === 0) this.addRule();
    } else {
      this.addRule();
    }
  }

  createRuleFormGroup(rule?: any): FormGroup {
    return this.fb.group({
      minWeightKg: [rule?.minWeightKg ?? null],
      maxWeightKg: [rule?.maxWeightKg ?? null],
      minAgeDays: [rule?.minAgeDays ?? null],
      maxAgeDays: [rule?.maxAgeDays ?? null],
      feedType: [rule?.feedType ?? this.defaultFeedType, [Validators.required]],
      formulaId: [rule?.formulaId ?? this.defaultFormulaId ?? null],
      quantityValue: [rule?.quantityValue ?? null, [Validators.required, Validators.min(0.001)]]
    });
  }

  setPlanType(value: FeedingPlanType): void {
    if (this.planType === value) return;
    this.form.patchValue({ planType: value });
    if (value === FeedingPlanType.FixedQuantity) {
      // One amount for everyone: a single row, no ranges.
      while (this.rules.length > 1) this.rules.removeAt(this.rules.length - 1);
    }
    if (value === FeedingPlanType.WeightPercentage) {
      this.rules.controls.forEach(c => { if ((c.get('quantityValue')?.value ?? 0) > 10) c.patchValue({ quantityValue: null }); });
    }
  }

  // ── Rows ────────────────────────────────────────────────────────────────

  private row(i: number): RuleRow {
    const v = this.rules.at(i).value;
    return {
      min: this.num(this.isAge ? v.minAgeDays : v.minWeightKg),
      max: this.num(this.isAge ? v.maxAgeDays : v.maxWeightKg),
      quantity: this.num(v.quantityValue),
    };
  }

  private num(v: unknown): number | null {
    return v === null || v === undefined || v === '' || isNaN(+v) ? null : +v;
  }

  private startOf(rule: { minWeightKg?: number; maxWeightKg?: number; minAgeDays?: number; maxAgeDays?: number }): number {
    return this.isAge
      ? (rule.minAgeDays ?? rule.maxAgeDays ?? 0)
      : (rule.minWeightKg ?? rule.maxWeightKg ?? 0);
  }

  /** Where the next range should start: one above the highest "to" so far. */
  nextStartHint(): number | null {
    this.revision();
    const maxes = this.rules.controls.map((_, i) => this.row(i).max).filter((m): m is number => m !== null);
    return maxes.length ? Math.max(...maxes) + 1 : null;
  }

  addRule(): void {
    const last = this.rules.length ? this.rules.at(this.rules.length - 1).value : null;
    const group = this.createRuleFormGroup();
    if (last) {
      // Carry on from the previous row: same ration, next range, same width.
      group.patchValue({ formulaId: last.formulaId ?? null, feedType: last.feedType });
      const start = this.nextStartHint();
      if (this.hasRange && start !== null) {
        const prev = this.row(this.rules.length - 1);
        const width = prev.min !== null && prev.max !== null ? prev.max - prev.min : null;
        group.patchValue(this.isAge
          ? { minAgeDays: start, maxAgeDays: width !== null ? start + width : null }
          : { minWeightKg: start, maxWeightKg: width !== null ? start + width : null });
      }
    }
    this.rules.push(group);
  }

  duplicateRule(i: number): void {
    const copy = this.createRuleFormGroup(this.rules.at(i).value);
    this.rules.insert(i + 1, copy);
  }

  removeRule(index: number): void {
    if (this.rules.length > 1) this.rules.removeAt(index);
  }

  sortRules(): void {
    const values = this.rules.controls.map(c => c.value).sort((a, b) => this.startOf(a) - this.startOf(b));
    this.rules.clear();
    values.forEach(v => this.rules.push(this.createRuleFormGroup(v)));
  }

  applyFormulaToAll(formulaId: string | null): void {
    this.defaultFormulaId = formulaId;
    this.rules.controls.forEach(c => c.patchValue({ formulaId }));
  }

  applyFeedTypeToAll(feedType: FeedCategory): void {
    this.defaultFeedType = feedType;
    this.defaultFormulaId = null;
    this.rules.controls.forEach(c => c.patchValue({ feedType, formulaId: null }));
  }

  generatorCount(): number {
    const { from, to, step } = this.gen;
    return step > 0 && to >= from ? Math.ceil((to - from + 1) / step) : 0;
  }

  generate(): void {
    const { from, to, step, startQty, increment } = this.gen;
    const count = this.generatorCount();
    if (!count || count > 100) {
      this.snackBar.open('Check the From / To / Every values.', 'Close', { duration: 3000 });
      return;
    }
    this.rules.clear();
    for (let n = 0; n < count; n++) {
      const min = from + n * step;
      const max = Math.min(min + step - 1, to);
      const group = this.createRuleFormGroup();
      group.patchValue({
        ...(this.isAge ? { minAgeDays: min, maxAgeDays: max } : { minWeightKg: min, maxWeightKg: max }),
        quantityValue: Math.round((startQty + n * increment) * 1000) / 1000,
      });
      this.rules.push(group);
    }
    this.showGenerator.set(false);
  }

  // ── Checks & previews ──────────────────────────────────────────────────

  rowError(i: number): boolean {
    this.revision();
    const r = this.row(i);
    return r.min !== null && r.max !== null && r.min > r.max;
  }

  /** Gaps, overlaps and missing values, in plain words. */
  issues(): string[] {
    this.revision();
    const out: string[] = [];
    const unit = this.unitLabel;
    const rows = this.rules.controls.map((_, i) => ({ i, ...this.row(i) }));

    rows.filter(r => r.quantity === null || r.quantity <= 0).forEach(r => out.push(`Row ${r.i + 1}: enter the ${this.isPercent ? 'percentage' : 'quantity'}.`));
    if (this.isPercent) rows.filter(r => (r.quantity ?? 0) > 10).forEach(r => out.push(`Row ${r.i + 1}: ${r.quantity}% of body weight is very high (usually 1.5–4%).`));
    if (!this.hasRange) return out;

    rows.filter(r => r.min !== null && r.max !== null && r.min > r.max).forEach(r => out.push(`Row ${r.i + 1}: "from" (${r.min}) is above "to" (${r.max}).`));

    const sorted = rows.filter(r => r.min !== null || r.max !== null)
      .sort((a, b) => (a.min ?? -Infinity) - (b.min ?? -Infinity));
    for (let k = 1; k < sorted.length; k++) {
      const prev = sorted[k - 1];
      const cur = sorted[k];
      if (prev.max === null) {
        out.push(`Row ${prev.i + 1} has no upper limit, so it overlaps row ${cur.i + 1}.`);
      } else if (cur.min !== null && cur.min <= prev.max) {
        out.push(`Rows ${prev.i + 1} and ${cur.i + 1} overlap (${prev.min ?? 0}–${prev.max} and ${cur.min}–${cur.max ?? '…'} ${unit}).`);
      } else if (cur.min !== null && cur.min > prev.max + 1) {
        out.push(`Gap between ${prev.max} and ${cur.min} ${unit}: animals in between get no feed from this rule set.`);
      }
    }
    const unsorted = rows.some((r, k) => k > 0 && r.min !== null && rows[k - 1].min !== null && r.min < rows[k - 1].min!);
    if (unsorted && !out.length) out.push('Rows are out of order — press "Sort" to list them lightest / youngest first.');
    return out;
  }

  /** Hard problems that stop saving (shown in the footer). */
  blockingIssue(): string | null {
    this.revision();
    if (this.form.get('name')?.invalid) return 'name is required';
    if (this.rules.controls.some(c => c.get('quantityValue')?.invalid)) return 'every row needs a quantity';
    if (this.rules.controls.some((_, i) => this.rowError(i))) return 'a range has "from" above "to"';
    return null;
  }

  coverage(): string {
    this.revision();
    const rows = this.rules.controls.map((_, i) => this.row(i));
    const mins = rows.map(r => r.min).filter((v): v is number => v !== null);
    const maxes = rows.map(r => r.max);
    const low = mins.length ? Math.min(...mins) : 0;
    const high = maxes.some(m => m === null) ? 'and above' : `– ${Math.max(...(maxes as number[]))}`;
    return `${low} ${high} ${this.unitLabel}`;
  }

  /** A worked example per row: the feed for an animal in the middle of the range, and its cost. */
  worksOut(i: number): string {
    this.revision();
    const r = this.row(i);
    if (r.quantity === null || r.quantity <= 0) return '';
    const formulaId: string | null = this.rules.at(i).value.formulaId;
    const costPerKg = formulaId?.startsWith('i:')
      ? this.stockItems().find(it => it.id === formulaId.substring(2))?.weightedAverageCostBdt ?? 0
      : this.formulas().find(f => f.id === formulaId)?.totalCostPerKgBdt ?? 0;
    const money = (kg: number) => costPerKg ? ` ≈ ৳${(kg * costPerKg).toLocaleString('en-IN', { maximumFractionDigits: 0 })}/day` : '';

    if (this.isPercent) {
      const weight = r.min !== null && r.max !== null ? Math.round((r.min + r.max) / 2) : (r.min ?? r.max ?? 300);
      const kg = Math.round(weight * r.quantity) / 100;
      return `${weight} kg animal → ${kg} kg/day${money(kg)}`;
    }
    return `${r.quantity} kg/head/day${money(r.quantity)}`;
  }

  // ── Save ───────────────────────────────────────────────────────────────

  close(): void {
    this.dialogRef.close();
  }

  submit(): void {
    this.form.markAllAsTouched();
    this.revision.update(v => v + 1);
    const blocking = this.blockingIssue();
    if (blocking || this.rules.length === 0) {
      this.snackBar.open(`Cannot save yet: ${blocking ?? 'add at least one row'}.`, 'Close', { duration: 4000 });
      return;
    }

    this.isSubmitting.set(true);
    const raw = this.form.getRawValue();
    const keepWeight = this.planType === FeedingPlanType.WeightPercentage || this.planType === FeedingPlanType.WeightQuantity;
    const rules = raw.rules.map((r: any) => ({
      // Only the range that belongs to the chosen plan type is sent.
      minWeightKg: keepWeight ? this.num(r.minWeightKg) ?? undefined : undefined,
      maxWeightKg: keepWeight ? this.num(r.maxWeightKg) ?? undefined : undefined,
      minAgeDays: this.isAge ? this.num(r.minAgeDays) ?? undefined : undefined,
      maxAgeDays: this.isAge ? this.num(r.maxAgeDays) ?? undefined : undefined,
      feedType: r.feedType as FeedCategory,
      // "i:<id>" = a stock item fed as it is; the server wraps it in a direct-item formula.
      formulaId: r.formulaId && !String(r.formulaId).startsWith('i:') ? r.formulaId : undefined,
      inventoryItemId: r.formulaId && String(r.formulaId).startsWith('i:') ? String(r.formulaId).substring(2) : undefined,
      quantityValue: +r.quantityValue
    }));

    const request = {
      name: raw.name!,
      planType: raw.planType!,
      targetAnimalType: raw.targetAnimalType!,
      feedingPurpose: raw.feedingPurpose!,
      isActive: raw.isActive ?? true,
      baseNotes: raw.baseNotes ? raw.baseNotes : undefined,
      rules
    };

    const call: Observable<unknown> = this.isEditing
      ? this.feedingService.updateRuleSet(this.data.id, request)
      : this.feedingService.createRuleSet(request);
    call.subscribe({
      next: () => {
        this.snackBar.open(this.isEditing ? 'Feed chart updated' : 'Feed chart created', 'Close', { duration: 3000 });
        this.dialogRef.close(true);
      },
      error: (err: { error?: { detail?: string } }) => {
        this.snackBar.open(err.error?.detail || 'Failed to save the rule set', 'Close', { duration: 5000 });
        this.isSubmitting.set(false);
      }
    });
  }
}
