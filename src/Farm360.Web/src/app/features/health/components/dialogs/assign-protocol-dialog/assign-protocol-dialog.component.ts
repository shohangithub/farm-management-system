import { Component, inject, signal, ChangeDetectionStrategy } from '@angular/core';
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
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    MatDialogModule, 
    MatButtonModule,
    MatIconModule,
    AnimalMultiPickerComponent
  ],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-gray-500">assignment</mat-icon>
            Assign Protocol
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">Apply {{ data?.protocol?.title }} to animals</p>
        </div>
        <button mat-dialog-close class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Body -->
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden">
        
        <!-- Error State -->
        <div *ngIf="error()" class="mx-6 mt-4 p-3 bg-red-50 text-red-700 border border-red-200 rounded-lg text-sm whitespace-pre-wrap">
          {{ error() }}
        </div>

        <div class="p-6 space-y-5 overflow-y-auto">
          
          <div class="p-4 bg-primary-50 dark:bg-primary-900/20 text-primary-800 dark:text-primary-300 rounded-xl border border-primary-200 dark:border-primary-800/50 flex items-center gap-3">
            <div class="p-2 bg-white dark:bg-primary-900/50 rounded-lg shadow-sm shrink-0">
               <mat-icon class="!text-primary-600 dark:!text-primary-400">vaccines</mat-icon>
            </div>
            <div>
              <div class="text-xs font-bold uppercase tracking-wider opacity-80 mb-0.5">Selected Protocol</div>
              <div class="text-base font-bold">{{ data?.protocol?.title }}</div>
            </div>
          </div>
          
          <!-- Animals -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Select Animals <span class="text-red-500">*</span></label>
            <app-animal-multi-picker formControlName="animalIds"></app-animal-multi-picker>
            <p class="text-xs text-red-500 mt-1" *ngIf="form.get('animalIds')?.touched && form.get('animalIds')?.invalid">
              At least one animal must be selected.
            </p>
          </div>

          <!-- Date Row -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Start Date <span class="text-red-500">*</span></label>
            <input type="date" formControlName="startDate"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            <p class="text-xs text-gray-500 mt-1">The date to begin calculating scheduled events.</p>
          </div>
          
        </div>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3">
          <button type="button" mat-dialog-close [disabled]="isSubmitting()" 
            class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || isSubmitting()"
                  class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed">
            <span *ngIf="!isSubmitting()">Assign Protocol</span>
            <span *ngIf="isSubmitting()">Assigning...</span>
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .custom-scrollbar::-webkit-scrollbar {
      width: 6px;
    }
    .custom-scrollbar::-webkit-scrollbar-track {
      background: transparent;
    }
    .custom-scrollbar::-webkit-scrollbar-thumb {
      background-color: rgba(156, 163, 175, 0.5);
      border-radius: 20px;
    }
    .custom-scrollbar:hover::-webkit-scrollbar-thumb {
      background-color: rgba(156, 163, 175, 0.8);
    }
  `]
})
export class AssignProtocolDialog {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private dialogRef = inject(MatDialogRef<AssignProtocolDialog>);
  data = inject(MAT_DIALOG_DATA);

  form: FormGroup;
  isSubmitting = signal(false);
  error = signal('');

  constructor() {
    this.form = this.fb.group({
      animalIds: [[], [Validators.required, Validators.minLength(1)]],
      startDate: [new Date(), Validators.required]
    });
  }

  onSubmit() {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.error.set('');

    const val = this.form.value;
    const farmId = this.contextService.currentFarmValue?.id || '';

    if (!farmId) {
      this.error.set('No farm context available.');
      this.isSubmitting.set(false);
      return;
    }
    
    const animalIdArray = val.animalIds;
      
    if (!animalIdArray || animalIdArray.length === 0) {
      this.error.set('Please select at least one animal.');
      this.isSubmitting.set(false);
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
        this.isSubmitting.set(false);
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.error.set(err.error?.detail || 'Failed to assign protocol.');
        this.isSubmitting.set(false);
      }
    });
  }
}
