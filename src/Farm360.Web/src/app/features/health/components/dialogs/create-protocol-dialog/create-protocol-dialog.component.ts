import { Component, inject, Inject, Optional, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { HealthService } from '../../../services/health.service';
import { AnimalSpecies, SPECIES_LABELS } from '../../../../livestock/models/animal.models';
import { parseApiError } from '../../../../../core/utils/error-parser';

@Component({
  selector: 'app-create-protocol-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    MatDialogModule, 
    MatButtonModule, 
    MatIconModule, 
    MatTooltipModule,
    MatSnackBarModule
  ],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-gray-500">list_alt</mat-icon>
            {{ isEditMode ? 'Edit Vaccination Protocol' : 'Create Vaccination Protocol' }}
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Define standardized vaccination schedule by target age</p>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Content -->
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden">
        
        <!-- Error State -->
        <div *ngIf="error()" class="mx-6 mt-4 p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-lg text-sm whitespace-pre-wrap flex items-start gap-2">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
          <span>{{ error() }}</span>
        </div>

        <div class="p-6 space-y-5 overflow-y-auto custom-scrollbar flex-1">
          
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div class="space-y-1.5 md:col-span-2">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Protocol Title <span class="text-red-500">*</span></label>
              <input type="text" formControlName="title" placeholder="e.g. Standard Cattle Vaccination Schedule"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Target Species <span class="text-red-500">*</span></label>
              <select formControlName="targetSpecies"
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                <option [ngValue]="null" disabled>-- Select Species --</option>
                <option *ngFor="let s of speciesOptions" [ngValue]="s.value">{{ s.label }}</option>
              </select>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Description</label>
            <textarea formControlName="description" rows="2" placeholder="Brief summary of protocol goals..."
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow resize-none"></textarea>
          </div>

          <!-- Protocol Steps Section -->
          <div class="pt-2 border-t border-gray-100 dark:border-gray-800">
            <div class="flex items-center justify-between mb-3">
              <h3 class="text-sm font-bold text-gray-900 dark:text-white uppercase tracking-wider m-0">Protocol Steps</h3>
              <button type="button" (click)="addStep()"
                      class="text-xs font-semibold text-primary-600 hover:text-primary-700 dark:text-primary-400 flex items-center gap-1">
                <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">add</mat-icon>
                Add Step
              </button>
            </div>

            <div formArrayName="steps" class="space-y-3">
              <div *ngFor="let step of steps.controls; let i = index" [formGroupName]="i"
                   class="p-4 bg-gray-50 dark:bg-gray-800/40 border border-gray-200 dark:border-gray-700/60 rounded-xl relative">
                
                <button *ngIf="steps.length > 1" type="button" (click)="removeStep(i)"
                        class="absolute top-2 right-2 p-1 text-gray-400 hover:text-red-500 rounded-full transition-colors">
                  <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">delete_outline</mat-icon>
                </button>

                <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
                  <div class="space-y-1">
                    <label class="block text-[11px] font-semibold text-gray-500">Step Name <span class="text-red-500">*</span></label>
                    <input type="text" formControlName="stepName" placeholder="e.g. Primary Dose"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                  </div>

                  <div class="space-y-1">
                    <label class="block text-[11px] font-semibold text-gray-500">Target Age (Days) <span class="text-red-500">*</span></label>
                    <input type="number" formControlName="targetAgeDays" min="0" placeholder="30"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                  </div>

                  <div class="space-y-1">
                    <label class="block text-[11px] font-semibold text-gray-500">Vaccine Name <span class="text-red-500">*</span></label>
                    <input type="text" formControlName="vaccineName" placeholder="e.g. Anthrax Vaccine"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                  </div>
                </div>

                <div class="mt-2 space-y-1">
                  <label class="block text-[11px] font-semibold text-gray-500">Dosage Instructions</label>
                  <input type="text" formControlName="dosageInstruction" placeholder="e.g. 2ml Subcutaneous"
                         class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Footer Actions -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
          <button type="button" mat-dialog-close [disabled]="isSubmitting()"
            class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || steps.length === 0 || isSubmitting()"
                  class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
            <mat-icon *ngIf="isSubmitting()" class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon>
            <span>{{ isSubmitting() ? 'Saving...' : (isEditMode ? 'Update Protocol' : 'Create Protocol') }}</span>
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
export class CreateProtocolDialogComponent {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private dialogRef = inject(MatDialogRef<CreateProtocolDialogComponent>);
  private snackBar = inject(MatSnackBar);

  form: FormGroup;
  isSubmitting = signal(false);
  error = signal('');
  isEditMode = false;
  protocolId?: string;

  speciesOptions = Object.entries(SPECIES_LABELS).map(([k, v]) => ({ value: Number(k), label: v }));

  constructor(@Optional() @Inject(MAT_DIALOG_DATA) public data: any) {
    this.isEditMode = !!data;
    if (this.isEditMode) {
      this.protocolId = data.id;
    }

    this.form = this.fb.group({
      title: [data?.title || '', [Validators.required, Validators.maxLength(200)]],
      targetSpecies: [data?.targetSpecies ?? null, Validators.required],
      description: [data?.description || ''],
      steps: this.fb.array([])
    });
    
    if (this.isEditMode && data.steps?.length > 0) {
      data.steps.forEach((step: any) => {
        this.steps.push(this.fb.group({
          stepName: [step.stepName, [Validators.required, Validators.maxLength(100)]],
          targetAgeDays: [step.targetAgeDays, [Validators.required, Validators.min(0)]],
          vaccineName: [step.vaccineName, [Validators.required, Validators.maxLength(100)]],
          dosageInstruction: [step.dosageInstruction || '']
        }));
      });
    } else {
      this.addStep();
    }
  }

  get steps() {
    return this.form.get('steps') as FormArray;
  }

  addStep() {
    this.steps.push(this.fb.group({
      stepName: ['', [Validators.required, Validators.maxLength(100)]],
      targetAgeDays: [null, [Validators.required, Validators.min(0)]],
      vaccineName: ['', [Validators.required, Validators.maxLength(100)]],
      dosageInstruction: ['']
    }));
  }

  removeStep(index: number) {
    this.steps.removeAt(index);
  }

  onSubmit() {
    if (this.form.invalid || this.steps.length === 0) {
      this.form.markAllAsTouched();
      this.snackBar.open('Please fill in all protocol fields and add at least one step.', 'Close', {
        duration: 4000,
        panelClass: ['snack-error']
      });
      return;
    }

    this.isSubmitting.set(true);
    this.error.set('');

    const val = this.form.value;
    
    const request = {
      id: this.protocolId,
      title: val.title,
      targetSpecies: Number(val.targetSpecies),
      description: val.description,
      steps: val.steps
    };

    if (this.isEditMode) {
      this.healthService.updateVaccinationProtocol(this.protocolId!, request).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.snackBar.open('Protocol updated successfully!', 'Close', {
            duration: 3000,
            panelClass: ['snack-success']
          });
          this.dialogRef.close(true);
        },
        error: (err) => {
          const parsedMsg = parseApiError(err, 'Failed to update protocol.');
          this.error.set(parsedMsg);
          this.snackBar.open(parsedMsg, 'Close', {
            duration: 5000,
            panelClass: ['snack-error']
          });
          this.isSubmitting.set(false);
        }
      });
    } else {
      this.healthService.createVaccinationProtocol(request).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.snackBar.open('Protocol created successfully!', 'Close', {
            duration: 3000,
            panelClass: ['snack-success']
          });
          this.dialogRef.close(true);
        },
        error: (err) => {
          const parsedMsg = parseApiError(err, 'Failed to create protocol.');
          this.error.set(parsedMsg);
          this.snackBar.open(parsedMsg, 'Close', {
            duration: 5000,
            panelClass: ['snack-error']
          });
          this.isSubmitting.set(false);
        }
      });
    }
  }
}
