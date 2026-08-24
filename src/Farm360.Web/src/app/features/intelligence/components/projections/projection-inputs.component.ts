import { Component, ChangeDetectionStrategy, input, output, computed, signal, effect, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, FormGroup, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FatteningProjectionInputs, ProjectionDefaults } from '../../models/projection.model';

@Component({
  selector: 'app-projection-inputs',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatIconModule, MatTooltipModule],
  template: `
    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 relative overflow-hidden h-full">
      <div class="flex items-center gap-3 mb-6">
        <div class="w-10 h-10 rounded-xl bg-indigo-50 dark:bg-indigo-500/10 flex items-center justify-center text-indigo-600 dark:text-indigo-400">
          <mat-icon>tune</mat-icon>
        </div>
        <div>
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white m-0">Projection Levers</h3>
          <p class="text-sm text-gray-500 dark:text-gray-400 m-0">Adjust variables to simulate outcomes</p>
        </div>
      </div>

      <form [formGroup]="form" class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <!-- Live Weight -->
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Starting Live Weight (Kg)</mat-label>
          <input matInput type="number" formControlName="startingLiveWeightKg">
          <mat-icon matSuffix class="text-gray-400" [matTooltip]="defaults()?.startingLiveWeightKg?.notes || ''">info</mat-icon>
        </mat-form-field>
        
        <!-- Purchase Price -->
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Purchase Price (BDT)</mat-label>
          <input matInput type="number" formControlName="purchasePriceBdt">
        </mat-form-field>

        <!-- Current Meat Price -->
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Meat Price (BDT/Kg)</mat-label>
          <input matInput type="number" formControlName="currentMeatPriceBdtPerKg">
        </mat-form-field>

        <!-- Fattening Period -->
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Fattening Period (Days)</mat-label>
          <input matInput type="number" formControlName="fatteningPeriodDays">
        </mat-form-field>
        
        <!-- Daily Live Weight Gain -->
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Daily Target Gain (Kg)</mat-label>
          <input matInput type="number" formControlName="dailyLiveWeightGainKg" step="0.1">
        </mat-form-field>

        <!-- Daily Feed Qty -->
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Daily Feed (Kg)</mat-label>
          <input matInput type="number" formControlName="dailyFeedQuantityKgAtStart" step="0.1">
        </mat-form-field>
        
        <!-- Feed Price -->
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Feed Price (BDT/Kg)</mat-label>
          <input matInput type="number" formControlName="feedPriceBdtPerKg" step="0.1">
        </mat-form-field>

        <!-- Meat Yield Ratios (Advanced) -->
        <mat-form-field appearance="outline" class="w-full">
          <mat-label>Initial Meat Yield (%)</mat-label>
          <input matInput type="number" formControlName="initialMeatYieldRatio" step="0.01">
        </mat-form-field>
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
    this.form = this.fb.group({
      startingLiveWeightKg: [0, [Validators.required, Validators.min(0)]],
      purchasePriceBdt: [0, [Validators.required, Validators.min(0)]],
      currentMeatPriceBdtPerKg: [0, [Validators.required, Validators.min(0)]],
      initialMeatYieldRatio: [0.55, [Validators.required, Validators.min(0), Validators.max(1)]],
      dailyLiveWeightGainKg: [0, [Validators.required, Validators.min(0)]],
      meatYieldOnDailyGainRatio: [0.65, [Validators.required, Validators.min(0), Validators.max(1)]],
      dailyFeedQuantityKgAtStart: [0, [Validators.required, Validators.min(0)]],
      feedPriceBdtPerKg: [0, [Validators.required, Validators.min(0)]],
      dailyGrassCostBdt: [0, [Validators.required, Validators.min(0)]],
      dailyOtherCostBdt: [0, [Validators.required, Validators.min(0)]],
      monthlyLaborCostBdt: [0, [Validators.required, Validators.min(0)]],
      fatteningPeriodDays: [90, [Validators.required, Validators.min(1)]]
    });

    effect(() => {
      const defs = this.defaults();
      if (defs) {
        this.form.patchValue({
          startingLiveWeightKg: defs.startingLiveWeightKg.value,
          purchasePriceBdt: defs.purchasePriceBdt.value,
          currentMeatPriceBdtPerKg: defs.currentMeatPriceBdtPerKg.value,
          initialMeatYieldRatio: defs.initialMeatYieldRatio.value,
          dailyLiveWeightGainKg: defs.dailyLiveWeightGainKg.value,
          meatYieldOnDailyGainRatio: defs.meatYieldOnDailyGainRatio.value,
          dailyFeedQuantityKgAtStart: defs.dailyFeedQuantityKgAtStart.value,
          feedPriceBdtPerKg: defs.feedPriceBdtPerKg.value,
          dailyGrassCostBdt: defs.dailyGrassCostBdt.value,
          dailyOtherCostBdt: defs.dailyOtherCostBdt.value,
          monthlyLaborCostBdt: defs.monthlyLaborCostBdt.value,
          fatteningPeriodDays: defs.fatteningPeriodDays.value
        }, { emitEvent: false });
        
        // Initial emit
        if (this.form.valid) {
          this.inputsChanged.emit(this.form.value as FatteningProjectionInputs);
        }
      }
    });

    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => {
        if (this.form.valid) {
          this.inputsChanged.emit(value as FatteningProjectionInputs);
        }
      });
  }
}
