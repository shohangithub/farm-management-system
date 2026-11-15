import { Component, ChangeDetectionStrategy, input, output, effect, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, FormGroup, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FatteningProjectionInputs, ProjectionDefaults, ProjectionDefaultValue } from '../../models/projection.model';

/** Form field name → defaults key. Yield fields are edited as % in the form but sent as 0–1 ratios. */
const PERCENT_FIELDS = ['initialMeatYieldRatio', 'meatYieldOnDailyGainRatio'] as const;

@Component({
  selector: 'app-projection-inputs',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatIconModule, MatTooltipModule],
  template: `
    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 relative overflow-hidden h-full">
      <div class="flex items-start justify-between gap-3 mb-6">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-indigo-50 dark:bg-indigo-500/10 flex items-center justify-center text-indigo-600 dark:text-indigo-400">
            <mat-icon>tune</mat-icon>
          </div>
          <div>
            <h3 class="text-lg font-semibold text-gray-900 dark:text-white m-0">Projection Levers</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400 m-0">Projection starts today, from the animal's latest weight. Adjust any value to simulate outcomes.</p>
          </div>
        </div>
        <button type="button" (click)="resetToDefaults()" [disabled]="!defaults()"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg border border-gray-200 dark:border-gray-700 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-40 shrink-0">
          <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">restart_alt</mat-icon> Reset
        </button>
      </div>

      <form [formGroup]="form">
        <p class="text-[11px] font-bold uppercase tracking-wider text-gray-400 mb-2">Animal &amp; market</p>
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-x-4">
          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Starting Live Weight (kg)</mat-label>
            <input matInput type="number" formControlName="startingLiveWeightKg" min="0.1" step="0.1">
            <mat-icon matSuffix class="text-gray-400 cursor-help" [matTooltip]="hint(defaults()?.startingLiveWeightKg)">info</mat-icon>
            <mat-error>Must be greater than 0</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Investment to Date (BDT)</mat-label>
            <input matInput type="number" formControlName="purchasePriceBdt" min="0">
            <mat-icon matSuffix class="text-gray-400 cursor-help" [matTooltip]="hint(defaults()?.purchasePriceBdt)">info</mat-icon>
            <mat-error>Cannot be negative</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Meat Price (BDT/kg)</mat-label>
            <input matInput type="number" formControlName="currentMeatPriceBdtPerKg" min="0">
            <mat-icon matSuffix class="text-gray-400 cursor-help" [matTooltip]="hint(defaults()?.currentMeatPriceBdtPerKg)">info</mat-icon>
            <mat-error>Cannot be negative</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Fattening Period (days)</mat-label>
            <input matInput type="number" formControlName="fatteningPeriodDays" min="1" max="1095" step="1">
            <mat-error>Whole number of days, 1–1095</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Daily Live-Weight Gain (kg)</mat-label>
            <input matInput type="number" formControlName="dailyLiveWeightGainKg" min="0" step="0.05">
            <mat-icon matSuffix class="text-gray-400 cursor-help" [matTooltip]="hint(defaults()?.dailyLiveWeightGainKg)">info</mat-icon>
            <mat-error>Cannot be negative</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Meat Yield / Dressing (%)</mat-label>
            <input matInput type="number" formControlName="initialMeatYieldRatio" min="0" max="100" step="1">
            <mat-icon matSuffix class="text-gray-400 cursor-help" [matTooltip]="hint(defaults()?.initialMeatYieldRatio)">info</mat-icon>
            <mat-error>Between 0 and 100</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Meat Yield on Daily Gain (%)</mat-label>
            <input matInput type="number" formControlName="meatYieldOnDailyGainRatio" min="0" max="100" step="1">
            <mat-icon matSuffix class="text-gray-400 cursor-help" matTooltip="Share of each day's live-weight gain that becomes meat. Shown in the daily 'Meat Gain' column.">info</mat-icon>
            <mat-error>Between 0 and 100</mat-error>
          </mat-form-field>
        </div>

        <p class="text-[11px] font-bold uppercase tracking-wider text-gray-400 mb-2 mt-1">Running costs</p>
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-x-4">
          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Daily Feed at Start (kg)</mat-label>
            <input matInput type="number" formControlName="dailyFeedQuantityKgAtStart" min="0" step="0.1">
            <mat-icon matSuffix class="text-gray-400 cursor-help" [matTooltip]="hint(defaults()?.dailyFeedQuantityKgAtStart, 'Scales with body weight as the animal grows.')">info</mat-icon>
            <mat-error>Cannot be negative</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Feed Price (BDT/kg)</mat-label>
            <input matInput type="number" formControlName="feedPriceBdtPerKg" min="0" step="0.1">
            <mat-icon matSuffix class="text-gray-400 cursor-help" [matTooltip]="hint(defaults()?.feedPriceBdtPerKg)">info</mat-icon>
            <mat-error>Cannot be negative</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Grass Cost (BDT/day)</mat-label>
            <input matInput type="number" formControlName="dailyGrassCostBdt" min="0">
            <mat-error>Cannot be negative</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Other Cost (BDT/day)</mat-label>
            <input matInput type="number" formControlName="dailyOtherCostBdt" min="0">
            <mat-error>Cannot be negative</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Labour Cost (BDT/month)</mat-label>
            <input matInput type="number" formControlName="monthlyLaborCostBdt" min="0">
            <mat-icon matSuffix class="text-gray-400 cursor-help" matTooltip="Charged per day as monthly ÷ 30.">info</mat-icon>
            <mat-error>Cannot be negative</mat-error>
          </mat-form-field>
        </div>
      </form>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectionInputsComponent {
  private fb = inject(FormBuilder);
  private destroyRef = inject(DestroyRef);

  defaults = input<ProjectionDefaults | null>(null);
  inputsChanged = output<FatteningProjectionInputs>();

  form: FormGroup;

  constructor() {
    const nonNegative = [Validators.required, Validators.min(0)];
    const percent = [Validators.required, Validators.min(0), Validators.max(100)];
    this.form = this.fb.group({
      // The backend rejects a starting weight of 0 (feed scales by weight / starting weight).
      startingLiveWeightKg: [null, [Validators.required, Validators.min(0.1)]],
      purchasePriceBdt: [null, nonNegative],
      currentMeatPriceBdtPerKg: [null, nonNegative],
      initialMeatYieldRatio: [null, percent],
      dailyLiveWeightGainKg: [null, nonNegative],
      meatYieldOnDailyGainRatio: [null, percent],
      dailyFeedQuantityKgAtStart: [null, nonNegative],
      feedPriceBdtPerKg: [null, nonNegative],
      dailyGrassCostBdt: [null, nonNegative],
      dailyOtherCostBdt: [null, nonNegative],
      monthlyLaborCostBdt: [null, nonNegative],
      fatteningPeriodDays: [null, [Validators.required, Validators.min(1), Validators.max(1095), Validators.pattern(/^\d+$/)]]
    });

    effect(() => {
      if (this.defaults()) this.resetToDefaults();
    });

    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.emitIfValid());
  }

  resetToDefaults(): void {
    const defs = this.defaults();
    if (!defs) return;
    this.form.patchValue({
      startingLiveWeightKg: defs.startingLiveWeightKg.value,
      purchasePriceBdt: defs.purchasePriceBdt.value,
      currentMeatPriceBdtPerKg: defs.currentMeatPriceBdtPerKg.value,
      initialMeatYieldRatio: this.toPercent(defs.initialMeatYieldRatio.value),
      dailyLiveWeightGainKg: defs.dailyLiveWeightGainKg.value,
      meatYieldOnDailyGainRatio: this.toPercent(defs.meatYieldOnDailyGainRatio.value),
      dailyFeedQuantityKgAtStart: defs.dailyFeedQuantityKgAtStart.value,
      feedPriceBdtPerKg: defs.feedPriceBdtPerKg.value,
      dailyGrassCostBdt: defs.dailyGrassCostBdt.value,
      dailyOtherCostBdt: defs.dailyOtherCostBdt.value,
      monthlyLaborCostBdt: defs.monthlyLaborCostBdt.value,
      fatteningPeriodDays: defs.fatteningPeriodDays.value
    }, { emitEvent: false });
    this.emitIfValid();
  }

  hint(d: ProjectionDefaultValue<number> | null | undefined, extra = ''): string {
    const source = d?.sourceLabel ? `Default: ${d.sourceLabel}` : '';
    return [source, extra].filter(Boolean).join(' — ');
  }

  private emitIfValid(): void {
    if (!this.form.valid) return;
    const raw = this.form.getRawValue();
    const out: Record<string, number> = {};
    for (const [key, value] of Object.entries(raw)) out[key] = Number(value);
    for (const key of PERCENT_FIELDS) out[key] = out[key] / 100;
    this.inputsChanged.emit(out as unknown as FatteningProjectionInputs);
  }

  private toPercent(ratio: number): number {
    return Math.round(ratio * 10000) / 100;
  }
}
