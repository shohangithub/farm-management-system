import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { FeedFormula, FeedIngredient, TargetAnimalType, TargetAnimalTypeNames } from '../../../models/feeding.models';
import { parseApiError } from '../../../../../core/utils/error-parser';

@Component({
  selector: 'app-create-formula-dialog',
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
    <div class="bg-white dark:bg-gray-800 rounded-2xl overflow-hidden max-w-3xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex items-center justify-between shrink-0">
        <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2">
          <div class="w-8 h-8 rounded-lg bg-teal-50 dark:bg-teal-950/50 text-teal-600 dark:text-teal-400 flex items-center justify-center">
            <mat-icon class="!w-5 !h-5 !text-[20px]">science</mat-icon>
          </div>
          <span>{{ isEdit ? 'Edit Feed Formula' : 'New Feed Formula' }}</span>
        </h2>
        <button mat-icon-button (click)="dialogRef.close()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Content -->
      <div class="p-6 overflow-y-auto flex-1">
        @if (error()) {
          <div class="mb-4 p-3 rounded-xl bg-red-50 dark:bg-red-950/30 text-red-600 dark:text-red-400 text-xs border border-red-200 dark:border-red-800 font-medium">
            {{ error() }}
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col gap-4">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Formula Title</mat-label>
              <input matInput formControlName="title" placeholder="e.g. High Yield Lactating Dairy Ration" required />
              <mat-error>Title is required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Target Species</mat-label>
              <mat-select formControlName="targetSpecies" required>
                @for (species of speciesList; track species.value) {
                  <mat-option [value]="species.value">{{ species.label }}</mat-option>
                }
              </mat-select>
              <mat-error>Species is required</mat-error>
            </mat-form-field>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Target Stage / Production Goal</mat-label>
              <input matInput formControlName="targetStage" placeholder="e.g. Early Lactation / Peak Milk" />
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Description</mat-label>
              <input matInput formControlName="description" placeholder="Notes on mixing or target BCS" />
            </mat-form-field>
          </div>

          <!-- Formula Ingredients Breakdown -->
          <div class="p-4 rounded-xl border border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-900/30">
            <div class="flex items-center justify-between mb-3">
              <h3 class="text-xs font-bold text-gray-500 uppercase tracking-wider">Ingredient Ratio Mix</h3>
              <button type="button" (click)="addIngredientRow()" class="px-3 py-1 text-xs font-semibold text-emerald-700 bg-emerald-50 hover:bg-emerald-100 dark:bg-emerald-950/40 dark:text-emerald-400 rounded-lg transition-colors inline-flex items-center gap-1">
                <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">add</mat-icon> Add Ingredient
              </button>
            </div>

            <div formArrayName="ingredients" class="flex flex-col gap-3">
              @for (item of ingredientsArray.controls; track $index) {
                <div [formGroupName]="$index" class="flex items-center gap-3 flex-wrap sm:flex-nowrap">
                  <mat-form-field appearance="outline" class="flex-1 min-w-[200px]">
                    <mat-label>Ingredient</mat-label>
                    <mat-select formControlName="ingredientId" required>
                      @for (ing of availableIngredients(); track ing.id) {
                        <mat-option [value]="ing.id">{{ ing.name }} ({{ ing.unitCostBdt }} BDT/kg)</mat-option>
                      }
                    </mat-select>
                  </mat-form-field>

                  <mat-form-field appearance="outline" class="w-32 shrink-0">
                    <mat-label>Ratio (%)</mat-label>
                    <input matInput type="number" formControlName="percentage" min="1" max="100" required />
                  </mat-form-field>

                  <button mat-icon-button type="button" color="warn" class="shrink-0" (click)="removeIngredientRow($index)" [disabled]="ingredientsArray.length <= 1">
                    <mat-icon>delete</mat-icon>
                  </button>
                </div>
              }
            </div>
          </div>
        </form>
      </div>

      <!-- Actions -->
      <div class="px-6 py-4 bg-gray-50/50 dark:bg-gray-900/30 border-t border-gray-100 dark:border-gray-800 flex justify-end gap-2 shrink-0">
        <button class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors" [disabled]="isSubmitting()" (click)="dialogRef.close()">
          Cancel
        </button>
        <button class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5 disabled:opacity-50" [disabled]="form.invalid || isSubmitting()" (click)="onSubmit()">
          <mat-spinner *ngIf="isSubmitting()" diameter="16"></mat-spinner>
          <span>{{ isEdit ? 'Update Formula' : 'Create Formula' }}</span>
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreateFormulaDialogComponent implements OnInit {
  readonly dialogRef = inject(MatDialogRef<CreateFormulaDialogComponent>);
  readonly data = inject<FeedFormula | null>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly feedingService = inject(FeedingService);
  private readonly snackBar = inject(MatSnackBar);

  readonly isSubmitting = signal(false);
  readonly error = signal('');
  readonly availableIngredients = signal<FeedIngredient[]>([]);
  readonly isEdit = !!this.data;

  readonly speciesList = [
    { value: TargetAnimalType.Cattle, label: TargetAnimalTypeNames[TargetAnimalType.Cattle] },
    { value: TargetAnimalType.Goat, label: TargetAnimalTypeNames[TargetAnimalType.Goat] },
    { value: TargetAnimalType.Sheep, label: TargetAnimalTypeNames[TargetAnimalType.Sheep] },
    { value: TargetAnimalType.Buffalo, label: TargetAnimalTypeNames[TargetAnimalType.Buffalo] },
  ];

  readonly form = this.fb.group({
    title: [this.data?.title || '', [Validators.required, Validators.maxLength(200)]],
    targetSpecies: [this.data?.targetSpecies || TargetAnimalType.Cattle, [Validators.required]],
    targetStage: [this.data?.targetStage || ''],
    description: [this.data?.description || ''],
    ingredients: this.fb.array([])
  });

  get ingredientsArray(): FormArray {
    return this.form.get('ingredients') as FormArray;
  }

  ngOnInit(): void {
    this.loadIngredients();

    if (this.data && this.data.ingredients && this.data.ingredients.length > 0) {
      this.data.ingredients.forEach(i => {
        this.ingredientsArray.push(this.fb.group({
          ingredientId: [i.ingredientId, Validators.required],
          percentage: [i.percentage, [Validators.required, Validators.min(1), Validators.max(100)]]
        }));
      });
    } else {
      this.addIngredientRow();
    }
  }

  private loadIngredients(): void {
    this.feedingService.getIngredients(true).subscribe({
      next: (res) => this.availableIngredients.set(res),
      error: (err: any) => this.error.set(parseApiError(err))
    });
  }

  addIngredientRow(): void {
    this.ingredientsArray.push(this.fb.group({
      ingredientId: ['', Validators.required],
      percentage: [25, [Validators.required, Validators.min(1), Validators.max(100)]]
    }));
  }

  removeIngredientRow(index: number): void {
    if (this.ingredientsArray.length > 1) {
      this.ingredientsArray.removeAt(index);
    }
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.error.set('');

    const formVal = this.form.getRawValue();
    const val = {
      ...formVal,
      targetStage: formVal.targetStage ? formVal.targetStage : null,
      description: formVal.description ? formVal.description : null,
      status: this.isEdit && this.data ? this.data.status : 1
    };

    const request$: Observable<any> = this.isEdit && this.data
      ? this.feedingService.updateFormula(this.data.id, val as any)
      : this.feedingService.createFormula(val as any);

    request$.subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.snackBar.open(`Feed formula ${this.isEdit ? 'updated' : 'created'} successfully.`, 'OK', { duration: 4000 });
        this.dialogRef.close(true);
      },
      error: (err: any) => {
        this.isSubmitting.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }
}
