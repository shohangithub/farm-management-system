import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FeedingService } from '../../../services/feeding.service';
import { FeedingRuleSet, FeedingPlanType } from '../../../models/feeding.models';
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

      <!-- Form Content -->
      <div class="flex-1 p-6" [formGroup]="form">
        <div class="space-y-5">
          
          <div>
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">
              Search Animal (Tag, Breed) <span class="text-red-500">*</span>
            </label>
            <app-animal-picker formControlName="animalId"></app-animal-picker>
            <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">Search and select the target animal.</p>
          </div>

          <div>
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">
              Feeding Rule Set <span class="text-red-500">*</span>
            </label>
            <select formControlName="ruleSetId"
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-sm font-medium">
              <option value="" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">-- Select Rule Set --</option>
              <option *ngFor="let rule of ruleSets()" [value]="rule.id" class="bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
                {{ rule.name }} ({{ rule.planType }})
              </option>
            </select>
          </div>

          <div>
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">
              Expected Daily Feed (kg) <span class="text-red-500">*</span>
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
        <button type="button" (click)="submit()" [disabled]="form.invalid || isSubmitting()"
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
  private readonly feedingService = inject(FeedingService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly contextService = inject(WorkingContextService);

  readonly isSubmitting = signal(false);
  readonly ruleSets = signal<FeedingRuleSet[]>([]);



  readonly form = this.fb.group({
    animalId: ['', [Validators.required]],
    ruleSetId: ['', [Validators.required]],
    expectedDailyFeedKg: [null as number | null, [Validators.required, Validators.min(0.01)]]
  });

  ngOnInit(): void {
    this.feedingService.getRuleSets().subscribe(res => {
      this.ruleSets.set(res.filter(r => r.isActive));
    });
  }

  close(): void {
    this.dialogRef.close();
  }

  submit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    const rawValue = this.form.getRawValue();
    const farmId = this.contextService.currentFarmValue?.id;
    if (!farmId) {
      this.snackBar.open('No active farm context found.', 'Close', { duration: 3000 });
      return;
    }

    const selectedRuleSet = this.ruleSets().find(r => r.id === rawValue.ruleSetId);
    if (!selectedRuleSet) return;

    this.feedingService.assignPlan({
      farmId: farmId,
      feedingRuleSetId: rawValue.ruleSetId!,
      planType: selectedRuleSet.planType,
      startDate: new Date().toISOString().split('T')[0],
      animalId: rawValue.animalId!,
      expectedDailyFeedKg: rawValue.expectedDailyFeedKg!
    }).subscribe({
      next: () => {
        this.snackBar.open('Feeding plan assigned successfully', 'Close', { duration: 3000 });
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.snackBar.open(err.error?.detail || 'Failed to assign plan', 'Close', { duration: 5000 });
        this.isSubmitting.set(false);
      }
    });
  }
}
