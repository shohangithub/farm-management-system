import { Component, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { HealthService } from '../../../services/health.service';
import { AnimalMultiPickerComponent } from '../../../../../shared/components/animal-multi-picker/animal-multi-picker.component';
import { WorkingContextService } from '../../../../../core/services/working-context.service';
import { parseApiError } from '../../../../../core/utils/error-parser';
import { toLocalIsoDate } from '../../../../../shared/utils/local-date';
import { NotificationService } from '../../../../../shared/services/notification.service';
import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../../core/i18n/i18n.service';

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
    MatSnackBarModule,
    AnimalMultiPickerComponent,
    TranslatePipe
  ],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-gray-500">assignment</mat-icon>
            {{ 'Assign Protocol' | t }}
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{{ 'Apply {title} to animals' | t: { title: data?.protocol?.title } }}</p>
        </div>
        <button mat-dialog-close class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors" [attr.aria-label]="'Close' | t">
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
              <div class="text-xs font-bold uppercase tracking-wider opacity-80 mb-0.5">{{ 'Selected Protocol' | t }}</div>
              <div class="text-base font-bold">{{ data?.protocol?.title }}</div>
            </div>
          </div>
          
          <!-- Animals -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Select Animals' | t }} <span class="text-red-500">*</span></label>
            <app-animal-multi-picker formControlName="animalIds"></app-animal-multi-picker>
            <p class="text-xs text-red-500 mt-1" *ngIf="form.get('animalIds')?.touched && form.get('animalIds')?.invalid">
              {{ 'At least one animal must be selected.' | t }}
            </p>
          </div>

          <!-- Date Row -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Start Date' | t }} <span class="text-red-500">*</span></label>
            <input type="date" formControlName="startDate"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            <p *ngIf="form.get('startDate')?.touched && form.get('startDate')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">{{ 'This field is required.' | t }}</p>
            <p class="text-xs text-gray-500 mt-1">{{ 'The date to begin calculating scheduled events.' | t }}</p>
          </div>
          
        </div>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3">
          <button type="button" mat-dialog-close [disabled]="isSubmitting()" 
            class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
            {{ 'Cancel' | t }}
          </button>
          <button type="submit" [disabled]="form.invalid || isSubmitting()"
                  class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed">
            <span *ngIf="!isSubmitting()">{{ 'Assign Protocol' | t }}</span>
            <span *ngIf="isSubmitting()">{{ 'Assigning...' | t }}</span>
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
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(I18nService);
  data = inject(MAT_DIALOG_DATA);

  form: FormGroup;
  isSubmitting = signal(false);
  error = signal('');

  constructor() {
    this.form = this.fb.group({
      animalIds: [[], [Validators.required, Validators.minLength(1)]],
      startDate: [toLocalIsoDate(), Validators.required] // local 'yyyy-MM-dd'; a Date object shows blank
    });
  }

  onSubmit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.notify.error(this.i18n.t('Please select at least one animal and start date.'));
      return;
    }

    this.isSubmitting.set(true);
    this.error.set('');

    const val = this.form.value;
    const farmId = this.contextService.currentFarmValue?.id || '';

    if (!farmId) {
      const msg = this.i18n.t('No farm context available.');
      this.error.set(msg);
      this.notify.error(msg);
      this.isSubmitting.set(false);
      return;
    }
    
    const animalIdArray = val.animalIds;
      
    if (!animalIdArray || animalIdArray.length === 0) {
      const msg = this.i18n.t('Please select at least one animal.');
      this.error.set(msg);
      this.notify.error(msg);
      this.isSubmitting.set(false);
      return;
    }

    const request = {
      farmId: farmId,
      protocolId: this.data.protocol.id,
      animalIds: animalIdArray,
      startDate: val.startDate // already a local 'yyyy-MM-dd' string
    };

    this.healthService.assignProtocolToAnimals(request).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.notify.success(this.i18n.t('Protocol assigned successfully!'));
        this.dialogRef.close(true);
      },
      error: (err) => {
        const parsedMsg = parseApiError(err, 'Failed to assign protocol.');
        this.error.set(parsedMsg);
        this.notify.error(parsedMsg);
        this.isSubmitting.set(false);
      }
    });
  }
}
