import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FeedingService } from '../../../services/feeding.service';
import { FeedingRuleSet, FeedingPlanType, AnimalFeedingPlan } from '../../../models/feeding.models';
import { AnimalPickerComponent } from '../../../../../shared/components/animal-picker/animal-picker.component';
import { WorkingContextService } from '../../../../../core/services/working-context.service';

@Component({
  selector: 'app-assign-feeding-plan-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    AnimalPickerComponent
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
            <mat-icon class="!w-5 !h-5 !text-[20px]">assignment_ind</mat-icon>
          </div>
          <div>
            <h2 class="text-lg font-bold text-gray-900 dark:text-white leading-tight m-0">
              Assign Feeding Plan
            </h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Enroll an animal into a smart feeding rule set</p>
          </div>
        </div>
        <button mat-icon-button (click)="close()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 rounded-full transition-colors">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Error Banner (RFC 7807) -->
      <div *ngIf="errorMessage()" class="mx-6 mt-4 p-3.5 rounded-xl bg-red-50 dark:bg-red-950/50 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-300 text-xs flex items-start gap-2.5">
        <mat-icon class="!w-4 !h-4 !text-[16px] text-red-600 dark:text-red-400 shrink-0 mt-0.5">error</mat-icon>
        <div class="flex-1 font-medium leading-relaxed">{{ errorMessage() }}</div>
        <button type="button" (click)="errorMessage.set(null)" class="text-red-400 hover:text-red-600">
          <mat-icon class="!w-4 !h-4 !text-[16px]">close</mat-icon>
        </button>
      </div>

      <!-- Form Content -->
      <div class="flex-1 overflow-y-auto custom-scrollbar p-6" [formGroup]="form">
        <div class="space-y-5">
          
          <div>
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">
              Search Animal (Tag, Breed) <span class="text-red-500">*</span>
            </label>
            <app-animal-picker formControlName="animalIds" [multiSelect]="true"></app-animal-picker>
            <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">Search and select the target animal(s).</p>
          </div>

          <div>
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">
              Feeding Rule Set(s) <span class="text-red-500">*</span>
            </label>
            <mat-select formControlName="ruleSetIds" multiple
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-sm font-medium">
              <mat-option *ngFor="let rule of ruleSets()" [value]="rule.id" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
                {{ rule.name }} ({{ rule.planType }})
              </mat-option>
            </mat-select>
          </div>

          <!-- Duplicate Conflict Warning -->
          <div *ngIf="duplicateWarnings().length > 0" class="p-3.5 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800/60 text-amber-900 dark:text-amber-200 text-xs flex items-start gap-2.5">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-amber-600 dark:text-amber-400 shrink-0 mt-0.5">warning</mat-icon>
            <div>
              <div class="font-bold mb-1">Duplicate Plan Conflict Detected:</div>
              <ul class="list-disc list-inside space-y-0.5 pl-0 m-0">
                <li *ngFor="let w of duplicateWarnings()">{{ w }}</li>
              </ul>
              <p class="mt-1.5 mb-0 text-[11px] text-amber-700 dark:text-amber-300">
                An animal cannot have multiple active plans under the same rule set. Please deselect the conflicting animals or rules before continuing.
              </p>
            </div>
          </div>

          <div>
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">
              Expected Daily Feed (kg)
            </label>
            <input formControlName="expectedDailyFeedKg" type="number" step="0.01" placeholder="e.g. 5.5"
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-sm font-medium placeholder-gray-400 dark:placeholder-gray-500" />
            <p class="mt-1.5 text-xs text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 p-2.5 rounded-xl border border-emerald-200 dark:border-emerald-800/60 flex items-center gap-1.5">
              <mat-icon class="!w-4 !h-4 !text-[16px] text-emerald-600 dark:text-emerald-400 shrink-0">auto_awesome</mat-icon> 
              Normally calculated automatically based on animal weight & rule conditions.
            </p>
          </div>

        </div>
      </div>

      <!-- Footer Actions -->
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3 z-10 shadow-sm shrink-0 mt-auto">
        <button type="button" (click)="close()" [disabled]="isSubmitting()"
          class="px-5 py-2.5 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-xl hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors">
          Cancel
        </button>
        <button type="button" (click)="submit()" [disabled]="form.invalid || duplicateWarnings().length > 0 || isSubmitting()"
          class="px-5 py-2.5 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed rounded-xl transition-colors shadow-sm inline-flex items-center justify-center min-w-[120px]">
          <mat-icon *ngIf="isSubmitting()" class="animate-spin !w-5 !h-5 !text-[20px] mr-2">refresh</mat-icon>
          Assign Plan
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AssignFeedingPlanDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<AssignFeedingPlanDialogComponent>);
  private readonly data = inject<{ ruleSetId?: string } | null>(MAT_DIALOG_DATA, { optional: true });
  private readonly feedingService = inject(FeedingService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly contextService = inject(WorkingContextService);

  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly ruleSets = signal<FeedingRuleSet[]>([]);
  readonly activePlans = signal<AnimalFeedingPlan[]>([]);

  readonly form = this.fb.group({
    animalIds: [[] as string[], [Validators.required, Validators.minLength(1)]],
    ruleSetIds: [[] as string[], [Validators.required, Validators.minLength(1)]],
    expectedDailyFeedKg: [null as number | null, [Validators.min(0.01)]]
  });

  readonly duplicateWarnings = computed(() => {
    const selectedAnimals = this.form.controls.animalIds.value || [];
    const selectedRules = this.form.controls.ruleSetIds.value || [];
    const active = this.activePlans();
    const rules = this.ruleSets();
    const warnings: string[] = [];

    for (const ruleId of selectedRules) {
      const rName = rules.find(r => r.id === ruleId)?.name || 'Rule Set';
      for (const animalId of selectedAnimals) {
        const match = active.find(p => p.animalId === animalId && p.ruleSetId === ruleId && p.isActive);
        if (match) {
          warnings.push(`Animal ${match.animalTag} is already enrolled in "${rName}".`);
        }
      }
    }
    return warnings;
  });

  ngOnInit(): void {
    const farmId = this.contextService.currentFarmValue?.id;

    this.feedingService.getRuleSets().subscribe(res => {
      this.ruleSets.set(res.filter(r => r.isActive));
      if (this.data?.ruleSetId) {
        this.form.patchValue({ ruleSetIds: [this.data.ruleSetId] });
      }
    });

    if (farmId) {
      this.feedingService.getFeedingPlans(farmId).subscribe(plans => {
        this.activePlans.set(plans.filter(p => p.isActive));
      });
    }
  }

  close(): void {
    this.dialogRef.close();
  }

  submit(): void {
    if (this.form.invalid || this.duplicateWarnings().length > 0) return;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    const rawValue = this.form.getRawValue();
    const farmId = this.contextService.currentFarmValue?.id;
    if (!farmId) {
      this.snackBar.open('No active farm context found.', 'Close', { duration: 3000 });
      this.isSubmitting.set(false);
      return;
    }

    const selectedRuleSet = this.ruleSets().find(r => rawValue.ruleSetIds!.includes(r.id));
    if (!selectedRuleSet) {
      this.isSubmitting.set(false);
      return;
    }

    this.feedingService.assignPlan({
      farmId: farmId,
      feedingRuleSetIds: rawValue.ruleSetIds!,
      planType: selectedRuleSet.planType,
      startDate: new Date().toISOString().split('T')[0],
      animalIds: rawValue.animalIds!,
      expectedDailyFeedKg: rawValue.expectedDailyFeedKg ?? undefined
    }).subscribe({
      next: () => {
        this.snackBar.open('Feeding plan(s) assigned successfully', 'Close', { duration: 3000 });
        this.dialogRef.close(true);
      },
      error: (err) => {
        let msg = 'Failed to assign plan';
        if (err.error?.errors) {
          msg = Object.values(err.error.errors).flat().join(' ');
        } else if (err.error?.detail) {
          msg = err.error.detail;
        } else if (err.error?.title) {
          msg = err.error.title;
        }
        this.errorMessage.set(msg);
        this.snackBar.open(msg, 'Close', { duration: 6000 });
        this.isSubmitting.set(false);
      }
    });
  }
}

