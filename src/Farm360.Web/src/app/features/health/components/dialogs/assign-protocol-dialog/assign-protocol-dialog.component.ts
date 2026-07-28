import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { HealthService } from '../../../services/health.service';
import { AnimalMultiPickerComponent } from '../../../../../shared/components/animal-multi-picker/animal-multi-picker.component';
import { WorkingContextService } from '../../../../../core/services/working-context.service';

@Component({
  selector: 'app-assign-protocol-dialog',
  standalone: true,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    MatDialogModule, 
    MatButtonModule,
    MatIconModule,
    AnimalMultiPickerComponent
  ],
  template: `
    <div class="p-6">
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-xl font-bold text-gray-900 dark:text-white flex items-center m-0">
          <div class="w-10 h-10 rounded-full bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center mr-3 border border-blue-200 dark:border-blue-800">
            <mat-icon class="text-blue-600 dark:text-blue-400">assignment</mat-icon>
          </div>
          Assign Protocol
        </h2>
        <button mat-icon-button mat-dialog-close class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <div class="mb-6 p-4 bg-blue-50 dark:bg-blue-900/20 text-blue-800 dark:text-blue-300 rounded-xl border border-blue-200 dark:border-blue-800/50">
        <div class="text-sm font-medium opacity-80 mb-1">Selected Protocol</div>
        <div class="text-lg font-bold">{{ data?.protocol?.title }}</div>
      </div>
      
      <form [formGroup]="form" class="space-y-5">
        
        <div>
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Select Animals <span class="text-red-500">*</span></label>
          <app-animal-multi-picker formControlName="animalIds"></app-animal-multi-picker>
        </div>

        <div>
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Start Date <span class="text-red-500">*</span></label>
          <input type="date" formControlName="startDate"
                 class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:text-white transition-all duration-200">
          <p class="text-xs text-gray-500 mt-1">The date to begin calculating scheduled events.</p>
        </div>
        
        <div *ngIf="error" class="bg-red-50 border-l-4 border-red-500 p-4 rounded-md shadow-sm">
          <div class="flex">
            <mat-icon class="text-red-500 mr-2">error</mat-icon>
            <p class="text-sm text-red-700 font-medium">{{ error }}</p>
          </div>
        </div>

        <div class="flex justify-end gap-3 pt-4 border-t border-gray-100 dark:border-gray-800">
          <button type="button" mat-dialog-close [disabled]="isSubmitting" class="px-5 py-2 text-sm font-bold text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 rounded-xl transition-all shadow-sm">
            Cancel
          </button>
          <button type="button" [disabled]="form.invalid || isSubmitting" (click)="onSubmit()"
                  class="px-5 py-2 text-sm font-bold text-white bg-blue-600 hover:bg-blue-700 rounded-xl transition-all shadow-sm shadow-blue-500/30 flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed">
            <mat-icon *ngIf="isSubmitting" class="animate-spin !text-[18px] !w-[18px] !h-[18px]">refresh</mat-icon>
            <span>{{ isSubmitting ? 'Assigning...' : 'Assign Protocol' }}</span>
          </button>
        </div>
      </form>
    </div>
  `
})
export class AssignProtocolDialog {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private dialogRef = inject(MatDialogRef<AssignProtocolDialog>);
  data = inject(MAT_DIALOG_DATA);

  form: FormGroup;
  isSubmitting = false;
  error = '';

  constructor() {
    this.form = this.fb.group({
      animalIds: [[], [Validators.required, Validators.minLength(1)]],
      startDate: [new Date(), Validators.required]
    });
  }

  onSubmit() {
    if (this.form.invalid) return;

    this.isSubmitting = true;
    this.error = '';

    const val = this.form.value;
    const farmId = this.contextService.currentFarmValue?.id || '';

    if (!farmId) {
      this.error = 'No farm context available.';
      this.isSubmitting = false;
      return;
    }
    
    const animalIdArray = val.animalIds;
      
    if (!animalIdArray || animalIdArray.length === 0) {
      this.error = 'Please select at least one animal.';
      this.isSubmitting = false;
      return;
    }

    const request = {
      farmId: farmId,
      protocolId: this.data.protocol.id,
      animalIds: animalIdArray,
      startDate: new Date(val.startDate).toISOString().split('T')[0]
    };

    this.healthService.assignProtocolToAnimals(request).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.error = err.error?.detail || 'Failed to assign protocol.';
        this.isSubmitting = false;
      }
    });
  }
}
