import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, Validators, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FeedingService } from '../../../services/feeding.service';
import {
  FeedingRuleSet,
  FeedingPlanType,
  TargetAnimalType,
  FeedingPurpose,
  FeedCategory,
  FeedCategoryNames
} from '../../../models/feeding.models';

@Component({
  selector: 'app-feeding-rule-set-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule
  ],
  styles: [`
    .custom-scrollbar::-webkit-scrollbar { width: 6px; }
    .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
    .custom-scrollbar::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.4); border-radius: 20px; }
    .custom-scrollbar:hover::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.7); }
  `],
  template: `
    <div class="p-0 flex flex-col h-full max-h-[85vh] bg-gray-50 dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl">
      <!-- Header -->
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-b border-gray-100 dark:border-gray-700 flex justify-between items-center z-10 shadow-sm shrink-0">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20">
            <mat-icon class="!w-5 !h-5 !text-[20px]">rule</mat-icon>
          </div>
          <div>
            <h2 class="text-lg font-bold text-gray-900 dark:text-white leading-tight m-0">
              {{ isEditing ? 'Edit' : 'Create' }} Feeding Rule Set
            </h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Configure automated feed calculations & logic conditions</p>
          </div>
        </div>
        <button mat-icon-button (click)="close()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 rounded-full transition-colors">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Form Content -->
      <div class="flex-1 overflow-y-auto custom-scrollbar p-6" [formGroup]="form">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-5 mb-6">
          
          <div class="md:col-span-2">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">
              Rule Set Name <span class="text-red-500">*</span>
            </label>
            <input formControlName="name" type="text" placeholder="e.g. Standard Holstein Lactation"
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-sm font-medium placeholder-gray-400 dark:placeholder-gray-500" />
          </div>

          <div>
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">
              Plan Type <span class="text-red-500">*</span>
            </label>
            <select formControlName="planType"
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-sm font-medium">
              <option value="FixedQuantity" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Fixed Quantity per Head</option>
              <option value="WeightPercentage" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Percentage of Body Weight</option>
              <option value="AgeBased" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Age Based Rules</option>
            </select>
          </div>

          <div>
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">
              Target Animal Type <span class="text-red-500">*</span>
            </label>
            <select formControlName="targetAnimalType"
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-sm font-medium">
              <option value="Cattle" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Cattle</option>
              <option value="Goat" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Goat</option>
              <option value="Sheep" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Sheep</option>
              <option value="Buffalo" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Buffalo</option>
            </select>
          </div>

          <div>
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">
              Feeding Purpose <span class="text-red-500">*</span>
            </label>
            <select formControlName="feedingPurpose"
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-sm font-medium">
              <option value="Maintenance" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Maintenance</option>
              <option value="Growth" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Growth</option>
              <option value="Gestation" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Gestation</option>
              <option value="Lactation" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Lactation</option>
              <option value="Finishing" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Finishing</option>
              <option value="Starter" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Starter</option>
              <option value="Transition" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">Transition</option>
            </select>
          </div>
          
          <div class="flex items-center mt-6">
            <mat-checkbox formControlName="isActive" color="primary" class="text-gray-900 dark:text-white text-sm font-semibold">Active Rule Set</mat-checkbox>
          </div>

          <div class="md:col-span-2">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">Base Notes</label>
            <textarea formControlName="baseNotes" rows="2" placeholder="Optional context or instructions..."
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-sm font-medium placeholder-gray-400 dark:placeholder-gray-500"></textarea>
          </div>
        </div>

        <hr class="border-gray-200 dark:border-gray-700 my-6" />

        <!-- Rules Explanation Banner -->
        <div class="mb-5 p-4 rounded-xl bg-emerald-50/80 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800/80 text-emerald-900 dark:text-emerald-200 flex items-start gap-3 shadow-sm">
          <mat-icon class="text-emerald-600 dark:text-emerald-400 mt-0.5 shrink-0 !w-5 !h-5 !text-[20px]">lightbulb</mat-icon>
          <div class="text-xs space-y-1">
            <div class="font-bold text-sm text-emerald-950 dark:text-emerald-100 flex items-center gap-1.5">
              <span>Understanding Rule Conditions</span>
              <span class="text-[10px] px-2 py-0.5 rounded-full font-extrabold uppercase bg-emerald-200 dark:bg-emerald-800 text-emerald-900 dark:text-emerald-100">
                {{ form.get('planType')?.value }} Mode
              </span>
            </div>
            
            <ng-container [ngSwitch]="form.get('planType')?.value">
              <p *ngSwitchCase="'FixedQuantity'">
                <strong>Fixed Quantity per Head:</strong> Every enrolled animal gets a fixed, static ration amount. Specify the <strong>Feed Category</strong> and exact amount in <strong>kg</strong> per animal per day.
                <br/><span class="text-emerald-700 dark:text-emerald-300 font-medium">Example: 5.0 kg of Concentrate per cow daily.</span>
              </p>

              <p *ngSwitchCase="'WeightPercentage'">
                <strong>Percentage of Body Weight:</strong> Automatically scales daily feed based on animal live weight. Define weight intervals (<strong>Min/Max Weight</strong>) and the feed amount as a <strong>% of body weight</strong>.
                <br/><span class="text-emerald-700 dark:text-emerald-300 font-medium">Example: For 300kg – 500kg cattle, feed 3.0% of body weight (e.g., 400kg cow receives 12kg feed).</span>
              </p>

              <p *ngSwitchCase="'AgeBased'">
                <strong>Age Based Rules:</strong> Adjusts feed automatically as the animal matures. Define age brackets in days (<strong>Min/Max Age in Days</strong>) and fixed daily feed amount in <strong>kg</strong>.
                <br/><span class="text-emerald-700 dark:text-emerald-300 font-medium">Example: 0 – 60 Days (Calf Starter): 1.5 kg/day | 61 – 180 Days: 3.5 kg/day.</span>
              </p>
            </ng-container>
          </div>
        </div>

        <!-- Rules Array -->
        <div class="flex justify-between items-center mb-4">
          <h3 class="text-base font-bold text-gray-900 dark:text-white m-0">Rule Conditions</h3>
          <button type="button" (click)="addRule()"
            class="px-3 py-1.5 text-xs font-semibold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 hover:bg-emerald-100 dark:hover:bg-emerald-900/50 rounded-lg transition-colors inline-flex items-center gap-1 border border-emerald-200 dark:border-emerald-800">
            <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">add</mat-icon> Add Condition
          </button>
        </div>

        <div formArrayName="rules" class="space-y-4">
          <div *ngFor="let rule of rules.controls; let i = index" [formGroupName]="i" 
            class="p-4 bg-white dark:bg-gray-800/90 border border-gray-200 dark:border-gray-700 rounded-xl relative group shadow-sm">
            
            <button type="button" (click)="removeRule(i)" class="absolute -top-3 -right-3 w-7 h-7 bg-red-100 dark:bg-red-950 text-red-600 dark:text-red-400 rounded-full flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity shadow-sm hover:bg-red-200 dark:hover:bg-red-900">
              <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">close</mat-icon>
            </button>

            <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
              <!-- Dynamic Fields based on Plan Type -->
              <ng-container *ngIf="form.get('planType')?.value === 'WeightPercentage'">
                <div>
                  <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-400 mb-1">Min Weight (kg)</label>
                  <input formControlName="minWeightKg" type="number" step="0.01" placeholder="e.g. 250"
                    class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 text-sm font-medium" />
                </div>
                <div>
                  <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-400 mb-1">Max Weight (kg)</label>
                  <input formControlName="maxWeightKg" type="number" step="0.01" placeholder="e.g. 450"
                    class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 text-sm font-medium" />
                </div>
              </ng-container>

              <ng-container *ngIf="form.get('planType')?.value === 'AgeBased'">
                <div>
                  <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-400 mb-1">Min Age (Days)</label>
                  <input formControlName="minAgeDays" type="number" placeholder="e.g. 0"
                    class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 text-sm font-medium" />
                </div>
                <div>
                  <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-400 mb-1">Max Age (Days)</label>
                  <input formControlName="maxAgeDays" type="number" placeholder="e.g. 60"
                    class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 text-sm font-medium" />
                </div>
              </ng-container>

              <ng-container *ngIf="form.get('planType')?.value === 'FixedQuantity'">
                <div class="md:col-span-2 hidden md:block"></div> <!-- Spacer -->
              </ng-container>

              <div>
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-400 mb-1">Feed Category <span class="text-red-500">*</span></label>
                <select formControlName="feedType"
                  class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 text-sm font-medium">
                  @for (cat of categoryOptions; track cat.value) {
                    <option [value]="cat.value" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">{{ cat.label }}</option>
                  }
                </select>
              </div>

              <div>
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-400 mb-1">Quantity/Pct <span class="text-red-500">*</span></label>
                <div class="relative">
                  <input formControlName="quantityValue" type="number" step="0.001" placeholder="e.g. 3.5"
                    class="w-full px-3 py-2 pr-8 rounded-lg border border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 text-sm font-medium" />
                  <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs font-bold text-gray-500 dark:text-gray-400">
                    {{ form.get('planType')?.value === 'WeightPercentage' ? '%' : 'kg' }}
                  </span>
                </div>
              </div>

            </div>
          </div>
        </div>

        <div *ngIf="rules.length === 0" class="text-center py-6 border-2 border-dashed border-gray-200 dark:border-gray-700 rounded-xl bg-gray-50/50 dark:bg-gray-800/50">
          <mat-icon class="text-gray-400 dark:text-gray-500 !w-8 !h-8 !text-[32px] mb-2">library_add</mat-icon>
          <p class="text-sm font-medium text-gray-500 dark:text-gray-400">No conditions added. Add at least one rule condition.</p>
        </div>

      </div>

      <!-- Footer Actions -->
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3 z-10 shadow-sm shrink-0">
        <button type="button" (click)="close()" [disabled]="isSubmitting()"
          class="px-5 py-2.5 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-xl hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors">
          Cancel
        </button>
        <button type="button" (click)="submit()" [disabled]="form.invalid || isSubmitting() || rules.length === 0"
          class="px-5 py-2.5 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed rounded-xl transition-colors shadow-sm inline-flex items-center justify-center min-w-[120px]">
          <mat-icon *ngIf="isSubmitting()" class="animate-spin !w-5 !h-5 !text-[20px] mr-2">refresh</mat-icon>
          {{ isEditing ? 'Update Configuration' : 'Create Rule Set' }}
        </button>
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

  readonly isEditing = !!this.data;
  readonly isSubmitting = signal(false);

  readonly categoryOptions = [
    { value: FeedCategory.Forage, label: FeedCategoryNames[FeedCategory.Forage] },
    { value: FeedCategory.Concentrate, label: FeedCategoryNames[FeedCategory.Concentrate] },
    { value: FeedCategory.Mineral, label: FeedCategoryNames[FeedCategory.Mineral] },
    { value: FeedCategory.Additive, label: FeedCategoryNames[FeedCategory.Additive] },
    { value: FeedCategory.Silage, label: FeedCategoryNames[FeedCategory.Silage] },
    { value: FeedCategory.Byproduct, label: FeedCategoryNames[FeedCategory.Byproduct] }
  ];

  readonly form = this.fb.group({
    name: ['', [Validators.required]],
    planType: [FeedingPlanType.FixedQuantity, [Validators.required]],
    targetAnimalType: [TargetAnimalType.Cattle, [Validators.required]],
    feedingPurpose: [FeedingPurpose.Maintenance, [Validators.required]],
    isActive: [true],
    baseNotes: [''],
    rules: this.fb.array([])
  });

  get rules(): FormArray {
    return this.form.get('rules') as FormArray;
  }

  ngOnInit(): void {
    if (this.isEditing) {
      this.form.patchValue({
        name: this.data.name,
        planType: this.data.planType,
        targetAnimalType: this.data.targetAnimalType,
        feedingPurpose: this.data.feedingPurpose,
        isActive: this.data.isActive,
        baseNotes: this.data.baseNotes
      });

      this.data.rules?.forEach(rule => {
        this.rules.push(this.createRuleFormGroup(rule));
      });
    } else {
      // Add one default rule
      this.addRule();
    }
  }

  createRuleFormGroup(rule?: any): FormGroup {
    return this.fb.group({
      minWeightKg: [rule?.minWeightKg || null],
      maxWeightKg: [rule?.maxWeightKg || null],
      minAgeDays: [rule?.minAgeDays || null],
      maxAgeDays: [rule?.maxAgeDays || null],
      feedType: [rule?.feedType || FeedCategory.Forage, [Validators.required]],
      quantityValue: [rule?.quantityValue || null, [Validators.required, Validators.min(0.001)]]
    });
  }

  addRule(): void {
    this.rules.push(this.createRuleFormGroup());
  }

  removeRule(index: number): void {
    this.rules.removeAt(index);
  }

  close(): void {
    this.dialogRef.close();
  }

  submit(): void {
    if (this.form.invalid || this.rules.length === 0) return;

    this.isSubmitting.set(true);
    
    // Sanitize empty strings to null for API
    const rawValue = this.form.getRawValue();
    const rules = rawValue.rules.map((r: any) => ({
      minWeightKg: r.minWeightKg === "" ? null : r.minWeightKg,
      maxWeightKg: r.maxWeightKg === "" ? null : r.maxWeightKg,
      minAgeDays: r.minAgeDays === "" ? null : r.minAgeDays,
      maxAgeDays: r.maxAgeDays === "" ? null : r.maxAgeDays,
      feedType: r.feedType,
      quantityValue: r.quantityValue
    }));

    const request = {
      name: rawValue.name!,
      planType: rawValue.planType!,
      targetAnimalType: rawValue.targetAnimalType!,
      feedingPurpose: rawValue.feedingPurpose!,
      isActive: rawValue.isActive ?? true,
      baseNotes: rawValue.baseNotes === "" ? undefined : rawValue.baseNotes ?? undefined,
      rules: rules
    };

    if (this.isEditing) {
      this.feedingService.updateRuleSet(this.data.id, request).subscribe({
        next: () => {
          this.snackBar.open('Rule set updated successfully', 'Close', { duration: 3000 });
          this.dialogRef.close(true);
        },
        error: (err) => {
          this.snackBar.open(err.error?.detail || 'Failed to update rule set', 'Close', { duration: 5000 });
          this.isSubmitting.set(false);
        }
      });
    } else {
      this.feedingService.createRuleSet(request).subscribe({
        next: () => {
          this.snackBar.open('Rule set created successfully', 'Close', { duration: 3000 });
          this.dialogRef.close(true);
        },
        error: (err) => {
          this.snackBar.open(err.error?.detail || 'Failed to create rule set', 'Close', { duration: 5000 });
          this.isSubmitting.set(false);
        }
      });
    }
  }
}
