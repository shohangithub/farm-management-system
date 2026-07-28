import { Component, inject, Inject, Optional } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { HealthService } from '../../../services/health.service';
import { AnimalSpecies, SPECIES_LABELS } from '../../../../livestock/models/animal.models';

@Component({
  selector: 'app-create-protocol-dialog',
  standalone: true,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    MatDialogModule, 
    MatButtonModule,
    MatIconModule,
    MatTooltipModule
  ],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-gray-500">{{ isEditMode ? 'edit' : 'library_add' }}</mat-icon>
            {{ isEditMode ? 'Edit Protocol' : 'Create Protocol' }}
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Standardize vaccination regimens</p>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Body -->
      <form [formGroup]="form" class="flex flex-col overflow-hidden">
        
        <!-- Error State -->
        <div *ngIf="error" class="mx-6 mt-4 p-3 bg-red-50 text-red-700 border border-red-200 rounded-lg text-sm whitespace-pre-wrap">
          {{ error }}
        </div>

        <div class="p-6 space-y-6 overflow-y-auto custom-scrollbar flex-1">
          
          <!-- Basic Info Section -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Protocol Title <span class="text-red-500">*</span></label>
              <input type="text" formControlName="title" placeholder="e.g. Standard Calf Vaccination"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Target Species <span class="text-red-500">*</span></label>
              <select formControlName="targetSpecies"
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                <option [ngValue]="null">Select Species</option>
                <option *ngFor="let kv of speciesOptions" [ngValue]="kv.value">{{ kv.label }}</option>
              </select>
            </div>
            
            <div class="space-y-1.5 md:col-span-2">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Description</label>
              <textarea formControlName="description" rows="2" placeholder="Optional details about this protocol..."
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow resize-none"></textarea>
            </div>
          </div>

          <div class="border-t border-gray-100 dark:border-gray-800 -mx-6 my-2"></div>

          <!-- Protocol Steps Section -->
          <div class="space-y-4">
            <div class="flex items-center justify-between">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Protocol Steps <span class="text-red-500">*</span></label>
              <button type="button" (click)="addStep()" class="text-xs font-bold text-primary-600 hover:text-primary-700 dark:text-primary-400 dark:hover:text-primary-300 flex items-center gap-1 bg-primary-50 dark:bg-primary-900/30 px-2 py-1 rounded-md hover:bg-primary-100 transition-colors">
                <mat-icon class="!w-3 !h-3 !text-[12px]">add</mat-icon> Add Step
              </button>
            </div>

            <div formArrayName="steps" class="space-y-3">
              <div *ngFor="let step of steps.controls; let i=index" [formGroupName]="i" 
                   class="p-4 bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700 rounded-xl relative group transition-colors hover:border-gray-300">
                
                <!-- Remove Button -->
                <button *ngIf="steps.length > 1" type="button" (click)="removeStep(i)" 
                        matTooltip="Remove Step"
                        class="absolute top-2 right-2 bg-red-50 dark:bg-red-900/20 text-red-600 dark:text-red-400 rounded-md w-7 h-7 flex items-center justify-center hover:bg-red-100 dark:hover:bg-red-900/40 transition-colors opacity-0 group-hover:opacity-100 z-10 shadow-sm border border-red-100">
                  <mat-icon class="!w-4 !h-4 !text-[16px]">delete_outline</mat-icon>
                </button>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-3 relative z-10">
                  <div class="space-y-1">
                    <label class="block text-[10px] font-bold uppercase tracking-wider text-gray-500">Step Name</label>
                    <input type="text" formControlName="stepName" placeholder="e.g. 1st Dose"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-md text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                  </div>
                  <div class="space-y-1">
                    <label class="block text-[10px] font-bold uppercase tracking-wider text-gray-500">Target Age (Days)</label>
                    <input type="number" formControlName="targetAgeDays" placeholder="0" min="0"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-md text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                  </div>
                  <div class="space-y-1">
                    <label class="block text-[10px] font-bold uppercase tracking-wider text-gray-500">Vaccine Name</label>
                    <input type="text" formControlName="vaccineName" placeholder="e.g. Bovi-Shield"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-md text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                  </div>
                  <div class="space-y-1">
                    <label class="block text-[10px] font-bold uppercase tracking-wider text-gray-500">Dosage</label>
                    <input type="text" formControlName="dosageInstruction" placeholder="e.g. 2ml SC"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-md text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                  </div>
                </div>
              </div>
              
              <div *ngIf="steps.length === 0" class="text-center p-6 border-2 border-dashed border-gray-200 dark:border-gray-700 rounded-xl bg-gray-50/50 dark:bg-gray-800/30">
                <p class="text-gray-500 text-xs">No steps defined. Add at least one step.</p>
                <button type="button" (click)="addStep()" class="mt-3 px-3 py-1.5 text-xs font-bold text-primary-600 bg-primary-50 dark:bg-primary-900/30 hover:bg-primary-100 rounded-md transition-colors">
                  Add First Step
                </button>
              </div>
            </div>
          </div>
          
        </div>

        <!-- Footer Actions -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
          <button type="button" mat-dialog-close [disabled]="isSubmitting" 
            class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
            Cancel
          </button>
          <button type="button" [disabled]="form.invalid || steps.length === 0 || isSubmitting" (click)="onSubmit()"
                  class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed">
            <span *ngIf="!isSubmitting">{{ isEditMode ? 'Save Changes' : 'Create Protocol' }}</span>
            <span *ngIf="isSubmitting">{{ isEditMode ? 'Saving...' : 'Creating...' }}</span>
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

  form: FormGroup;
  isSubmitting = false;
  error = '';
  
  isEditMode = false;
  protocolId: string | null = null;
  
  speciesOptions = Object.entries(SPECIES_LABELS).map(([k, v]) => ({ value: Number(k), label: v }));

  constructor(@Optional() @Inject(MAT_DIALOG_DATA) public data: any) {
    this.isEditMode = !!data;
    if (this.isEditMode) {
      this.protocolId = data.id;
    }

    let initialSpecies = null;
    if (this.isEditMode && data.targetSpecies) {
      const entry = Object.entries(SPECIES_LABELS).find(([k, v]) => v === data.targetSpecies);
      if (entry) initialSpecies = Number(entry[0]);
    }

    this.form = this.fb.group({
      title: [data?.title || '', [Validators.required, Validators.maxLength(200)]],
      targetSpecies: [initialSpecies, Validators.required],
      description: [data?.description || ''],
      steps: this.fb.array([])
    });
    
    if (this.isEditMode && data.steps && data.steps.length > 0) {
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
      targetAgeDays: [0, [Validators.required, Validators.min(0)]],
      vaccineName: ['', [Validators.required, Validators.maxLength(100)]],
      dosageInstruction: ['']
    }));
  }

  removeStep(index: number) {
    this.steps.removeAt(index);
  }

  onSubmit() {
    if (this.form.invalid || this.steps.length === 0) return;

    this.isSubmitting = true;
    this.error = '';

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
          this.isSubmitting = false;
          this.dialogRef.close(true);
        },
        error: (err) => {
          this.error = err.error?.detail || err.error?.title || 'Failed to update protocol.';
          this.isSubmitting = false;
        }
      });
    } else {
      this.healthService.createVaccinationProtocol(request).subscribe({
        next: () => {
          this.isSubmitting = false;
          this.dialogRef.close(true);
        },
        error: (err) => {
          this.error = err.error?.detail || err.error?.title || 'Failed to create protocol.';
          this.isSubmitting = false;
        }
      });
    }
  }
}
