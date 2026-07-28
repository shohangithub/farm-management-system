import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable } from 'rxjs';
import { FeedingService } from '../../../services/feeding.service';
import { FeedCategory, FeedCategoryNames, FeedIngredient } from '../../../models/feeding.models';
import { parseApiError } from '../../../../../core/utils/error-parser';

@Component({
  selector: 'app-create-ingredient-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  template: `
    <div class="bg-white dark:bg-gray-800 rounded-2xl overflow-hidden">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex items-center justify-between">
        <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2">
          <div class="w-8 h-8 rounded-lg bg-emerald-50 dark:bg-emerald-950/50 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
            <mat-icon class="!w-5 !h-5 !text-[20px]">restaurant</mat-icon>
          </div>
          <span>{{ isEdit ? 'Edit Feed Ingredient' : 'New Feed Ingredient' }}</span>
        </h2>
        <button mat-icon-button (click)="dialogRef.close()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Content -->
      <div class="p-6">
        @if (error()) {
          <div class="mb-4 p-3 rounded-xl bg-red-50 dark:bg-red-950/30 text-red-600 dark:text-red-400 text-xs border border-red-200 dark:border-red-800 font-medium">
            {{ error() }}
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col gap-4">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Ingredient Name</mat-label>
              <input matInput formControlName="name" placeholder="e.g. Maize Silage" required />
              <mat-error>Name is required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Category</mat-label>
              <mat-select formControlName="category" required>
                @for (cat of categories; track cat.value) {
                  <mat-option [value]="cat.value">{{ cat.label }}</mat-option>
                }
              </mat-select>
              <mat-error>Category is required</mat-error>
            </mat-form-field>
          </div>

          <div class="grid grid-cols-3 gap-3 p-3 bg-gray-50/80 dark:bg-gray-900/50 rounded-xl border border-gray-100 dark:border-gray-800">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>DM %</mat-label>
              <input matInput type="number" formControlName="dryMatterPct" step="0.1" />
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>CP %</mat-label>
              <input matInput type="number" formControlName="crudeProteinPct" step="0.1" />
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>ME (MJ/kg)</mat-label>
              <input matInput type="number" formControlName="metabolizableEnergyMjPerKg" step="0.1" />
            </mat-form-field>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Unit Cost (BDT)</mat-label>
              <input matInput type="number" formControlName="unitCostBdt" step="0.5" required />
              <mat-error>Unit cost is required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Unit</mat-label>
              <input matInput formControlName="unit" placeholder="kg" />
            </mat-form-field>
          </div>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Description / Notes</mat-label>
            <textarea matInput formControlName="description" rows="2"></textarea>
          </mat-form-field>
        </form>
      </div>

      <!-- Actions -->
      <div class="px-6 py-4 bg-gray-50/50 dark:bg-gray-900/30 border-t border-gray-100 dark:border-gray-800 flex justify-end gap-2">
        <button class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors" [disabled]="isSubmitting()" (click)="dialogRef.close()">
          Cancel
        </button>
        <button class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5 disabled:opacity-50" [disabled]="form.invalid || isSubmitting()" (click)="onSubmit()">
          <mat-spinner *ngIf="isSubmitting()" diameter="16"></mat-spinner>
          <span>{{ isEdit ? 'Update Ingredient' : 'Save Ingredient' }}</span>
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreateIngredientDialogComponent {
  readonly dialogRef = inject(MatDialogRef<CreateIngredientDialogComponent>);
  readonly data = inject<FeedIngredient | null>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly feedingService = inject(FeedingService);
  private readonly snackBar = inject(MatSnackBar);

  readonly isSubmitting = signal(false);
  readonly error = signal('');
  readonly isEdit = !!this.data;

  readonly categories = [
    { value: FeedCategory.Forage, label: FeedCategoryNames[FeedCategory.Forage] },
    { value: FeedCategory.Concentrate, label: FeedCategoryNames[FeedCategory.Concentrate] },
    { value: FeedCategory.Mineral, label: FeedCategoryNames[FeedCategory.Mineral] },
    { value: FeedCategory.Additive, label: FeedCategoryNames[FeedCategory.Additive] },
    { value: FeedCategory.Silage, label: FeedCategoryNames[FeedCategory.Silage] },
    { value: FeedCategory.Byproduct, label: FeedCategoryNames[FeedCategory.Byproduct] },
  ];

  readonly form = this.fb.group({
    name: [this.data?.name || '', [Validators.required, Validators.maxLength(150)]],
    category: [this.data?.category || FeedCategory.Concentrate, [Validators.required]],
    dryMatterPct: [this.data?.dryMatterPct ?? 88, [Validators.required, Validators.min(0), Validators.max(100)]],
    crudeProteinPct: [this.data?.crudeProteinPct ?? 18, [Validators.required, Validators.min(0), Validators.max(100)]],
    metabolizableEnergyMjPerKg: [this.data?.metabolizableEnergyMjPerKg ?? 11.5, [Validators.required, Validators.min(0)]],
    crudeFiberPct: [this.data?.crudeFiberPct ?? 0],
    calciumPct: [this.data?.calciumPct ?? 0],
    phosphorusPct: [this.data?.phosphorusPct ?? 0],
    unitCostBdt: [this.data?.unitCostBdt ?? 45, [Validators.required, Validators.min(0)]],
    unit: [this.data?.unit || 'kg', [Validators.required]],
    description: [this.data?.description || '']
  });

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.error.set('');

    const formVal = this.form.getRawValue();
    const val = {
      ...formVal,
      description: formVal.description ? formVal.description : null
    };

    const request$: Observable<any> = this.isEdit && this.data
      ? this.feedingService.updateIngredient(this.data.id, val as any)
      : this.feedingService.createIngredient(val as any);

    request$.subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.snackBar.open(`Feed ingredient ${this.isEdit ? 'updated' : 'created'} successfully.`, 'OK', { duration: 4000 });
        this.dialogRef.close(true);
      },
      error: (err: any) => {
        this.isSubmitting.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }
}
