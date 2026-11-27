import { Component, inject, Inject, Optional, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { HealthService } from '../../../services/health.service';
import { InventoryService } from '../../../../inventory/services/inventory.service';
import { InventoryItem, InventoryCategory, UnitConversion } from '../../../../inventory/models/inventory.models';
import { AnimalSpecies, SPECIES_LABELS } from '../../../../livestock/models/animal.models';
import { parseApiError } from '../../../../../core/utils/error-parser';
import { NotificationService } from '../../../../../shared/services/notification.service';
import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../../core/i18n/i18n.service';

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
    MatSnackBarModule,
    TranslatePipe
  ],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-gray-500">list_alt</mat-icon>
            {{ (isEditMode ? 'Edit Vaccination Protocol' : 'Create Vaccination Protocol') | t }}
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">{{ 'Define standardized vaccination schedule by target age' | t }}</p>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors" [attr.aria-label]="'Close' | t">
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
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Protocol Title' | t }} <span class="text-red-500">*</span></label>
              <input type="text" formControlName="title" [placeholder]="'e.g. Standard Cattle Vaccination Schedule' | t"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
              <p *ngIf="form.get('title')?.touched && form.get('title')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">{{ 'This field is required.' | t }}</p>
              <p *ngIf="form.get('title')?.touched && form.get('title')?.hasError('maxlength')" class="text-xs text-red-600 dark:text-red-400 mt-1">{{ 'Must be {max} characters or fewer.' | t: { max: 200 } }}</p>
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Target Species' | t }} <span class="text-red-500">*</span></label>
              <select formControlName="targetSpecies"
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                <option [ngValue]="null" disabled>{{ '-- Select Species --' | t }}</option>
                <option *ngFor="let s of speciesOptions" [ngValue]="s.value">{{ s.label | t }}</option>
              </select>
              <p *ngIf="form.get('targetSpecies')?.touched && form.get('targetSpecies')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">{{ 'This field is required.' | t }}</p>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Description' | t }}</label>
            <textarea formControlName="description" rows="2" [placeholder]="'Brief summary of protocol goals...' | t"
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow resize-none"></textarea>
          </div>

          <div class="flex items-center gap-2 p-3 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-xl">
            <input type="checkbox" id="isDeworming" formControlName="isDeworming"
                   class="w-4 h-4 text-primary-600 bg-white border-gray-300 rounded focus:ring-primary-500 dark:focus:ring-primary-600 dark:ring-offset-gray-800 dark:bg-gray-700 dark:border-gray-600">
            <label for="isDeworming" class="text-sm font-medium text-blue-900 dark:text-blue-300 cursor-pointer">
              {{ 'This is a Deworming Protocol' | t }}
            </label>
          </div>

          <label class="flex items-start gap-2 p-3 bg-primary-50/60 dark:bg-primary-500/5 border border-primary-200 dark:border-primary-800/60 rounded-xl cursor-pointer">
            <input type="checkbox" formControlName="autoStartForPurchased"
                   class="mt-0.5 w-4 h-4 text-primary-600 bg-white border-gray-300 rounded focus:ring-primary-500">
            <span>
              <span class="block text-sm font-medium text-gray-900 dark:text-white">{{ 'Start automatically for new purchased animals' | t }}</span>
              <span class="block text-xs text-gray-500 dark:text-gray-400">{{ 'Every animal registered as Purchased gets these tasks, counted from its acquisition date.' | t }}</span>
            </span>
          </label>

          <!-- Protocol Steps Section -->
          <div class="pt-2 border-t border-gray-100 dark:border-gray-800">
            <div class="flex items-center justify-between mb-3">
              <h3 class="text-sm font-bold text-gray-900 dark:text-white uppercase tracking-wider m-0">{{ 'Protocol Steps' | t }}</h3>
              <button type="button" (click)="addStep()"
                      class="text-xs font-semibold text-primary-600 hover:text-primary-700 dark:text-primary-400 flex items-center gap-1">
                <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">add</mat-icon>
                {{ 'Add Step' | t }}
              </button>
            </div>

            <div formArrayName="steps" class="space-y-3">
              <div *ngFor="let step of steps.controls; let i = index" [formGroupName]="i"
                   class="p-4 bg-gray-50 dark:bg-gray-800/40 border border-gray-200 dark:border-gray-700/60 rounded-xl relative">
                
                <button *ngIf="steps.length > 1" type="button" (click)="removeStep(i)"
                        class="absolute top-2 right-2 p-1 text-gray-400 hover:text-red-500 rounded-full transition-colors" [attr.aria-label]="'Remove step' | t">
                  <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">delete_outline</mat-icon>
                </button>

                <div class="flex flex-wrap items-center gap-2 mb-3 pr-8">
                  <div class="inline-flex rounded-lg border border-gray-300 dark:border-gray-600 overflow-hidden" role="group" [attr.aria-label]="'Step type' | t">
                    <button type="button" (click)="setKind(i, 'Medicine')"
                      class="h-9 px-3 text-xs font-semibold"
                      [ngClass]="step.get('kind')?.value !== 'CareTask' ? 'bg-primary-600 text-white' : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300'">
                      {{ 'Medicine / vaccine' | t }}
                    </button>
                    <button type="button" (click)="setKind(i, 'CareTask')"
                      class="h-9 px-3 text-xs font-semibold border-l border-gray-300 dark:border-gray-600"
                      [ngClass]="step.get('kind')?.value === 'CareTask' ? 'bg-primary-600 text-white' : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300'">
                      {{ 'Care task' | t }}
                    </button>
                  </div>
                  <span *ngIf="step.get('repeatEveryDays')?.value" class="inline-flex items-center gap-0.5 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300">
                    <mat-icon class="!text-[13px] !w-[13px] !h-[13px]">autorenew</mat-icon>
                    {{ 'repeats every {days} days' | t: { days: step.get('repeatEveryDays')?.value } }}
                  </span>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
                  <div class="space-y-1">
                    <label class="block text-[11px] font-semibold text-gray-500">{{ 'Step Name' | t }} <span class="text-red-500">*</span></label>
                    <input type="text" formControlName="stepName" [placeholder]="'e.g. Primary Dose' | t"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                    <p *ngIf="step.get('stepName')?.touched && step.get('stepName')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">{{ 'This field is required.' | t }}</p>
                    <p *ngIf="step.get('stepName')?.touched && step.get('stepName')?.hasError('maxlength')" class="text-xs text-red-600 dark:text-red-400 mt-1">{{ 'Must be {max} characters or fewer.' | t: { max: 100 } }}</p>
                  </div>

                  <div class="space-y-1">
                    <label class="block text-[11px] font-semibold text-gray-500">{{ 'Target Age (Days)' | t }} <span class="text-red-500">*</span></label>
                    <input type="number" formControlName="targetAgeDays" min="0" placeholder="30"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                    <p *ngIf="step.get('targetAgeDays')?.touched && step.get('targetAgeDays')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">{{ 'This field is required.' | t }}</p>
                    <p *ngIf="step.get('targetAgeDays')?.touched && step.get('targetAgeDays')?.hasError('min')" class="text-xs text-red-600 dark:text-red-400 mt-1">{{ 'Must be at least {min}.' | t: { min: 0 } }}</p>
                  </div>

                  <div class="space-y-1" *ngIf="step.get('kind')?.value !== 'CareTask'">
                    <label class="block text-[11px] font-semibold text-gray-500">{{ 'Link Inventory Item' | t }}</label>
                    <select formControlName="inventoryItemId" (change)="onInventoryItemChange(i)"
                            class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                      <option [ngValue]="null">{{ '-- Not Tracked / Custom --' | t }}</option>
                      <option *ngFor="let item of inventoryItems()" [ngValue]="item.id">
                        {{ item.name }} ({{ item.categoryName | t }})
                      </option>
                    </select>
                  </div>

                  <div class="space-y-1">
                    <label class="block text-[11px] font-semibold text-gray-500">{{ (step.get('kind')?.value === 'CareTask' ? 'Task' : (form.get('isDeworming')?.value ? 'Medicine Name' : 'Vaccine Name')) | t }} <span class="text-red-500">*</span></label>
                    <input type="text" formControlName="vaccineName" [placeholder]="(form.get('isDeworming')?.value ? 'e.g. Ivermectin' : 'e.g. Anthrax Vaccine') | t"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                    <p *ngIf="step.get('vaccineName')?.touched && step.get('vaccineName')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">{{ 'This field is required.' | t }}</p>
                    <p *ngIf="step.get('vaccineName')?.touched && step.get('vaccineName')?.hasError('maxlength')" class="text-xs text-red-600 dark:text-red-400 mt-1">{{ 'Must be {max} characters or fewer.' | t: { max: 100 } }}</p>
                  </div>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-3 mt-2">
                  <div class="space-y-1">
                    <label class="block text-[11px] font-semibold text-gray-500">{{ 'Dosage Instructions' | t }}</label>
                    <input type="text" formControlName="dosageInstruction" [placeholder]="'e.g. 2ml Subcutaneous' | t"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                  </div>
                  
                  <div class="space-y-1" *ngIf="step.get('kind')?.value !== 'CareTask'">
                    <label class="block text-[11px] font-semibold text-gray-500" [title]="'Quantity deducted from inventory per administration' | t">{{ 'Deduct Quantity' | t }}</label>
                    <input type="number" formControlName="dosageQuantity" step="0.01" min="0" [placeholder]="'e.g. 2' | t"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 disabled:opacity-50">
                    <p *ngIf="step.get('dosageQuantity')?.touched && step.get('dosageQuantity')?.hasError('min')" class="text-xs text-red-600 dark:text-red-400 mt-1">{{ 'Must be at least {min}.' | t: { min: 0 } }}</p>
                  </div>
                </div>

                <p *ngIf="step.get('kind')?.value !== 'CareTask' && !step.get('inventoryItemId')?.value" class="mt-2 mb-0 text-xs text-gray-500 dark:text-gray-400 flex items-center gap-1">
                  <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">link</mat-icon>
                  {{ 'Link your stock item so the dose is taken from stock when given.' | t }}
                </p>

                <div class="grid grid-cols-2 md:grid-cols-4 gap-3 mt-3">
                  <div class="space-y-1">
                    <label class="block text-[11px] font-semibold text-gray-500">{{ 'Repeat every (days)' | t }}</label>
                    <input type="number" formControlName="repeatEveryDays" min="1" step="1" [placeholder]="'No repeat' | t"
                           class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                    <p *ngIf="step.get('repeatEveryDays')?.touched && step.get('repeatEveryDays')?.hasError('min')" class="text-xs text-red-600 dark:text-red-400 mt-1">{{ 'Must be at least {min}.' | t: { min: 1 } }}</p>
                  </div>
                </div>

                <!-- Dose by body weight: "X units per Y kg" -->
                <div *ngIf="step.get('kind')?.value !== 'CareTask'" class="mt-3 p-3 rounded-lg border border-dashed border-gray-300 dark:border-gray-600">
                  <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0 mb-2">{{ 'Dose by body weight (optional)' | t }}</p>
                  <div class="grid grid-cols-2 md:grid-cols-6 gap-3 items-end">
                    <div class="space-y-1">
                      <label class="block text-[11px] font-semibold text-gray-500">{{ 'Dose' | t }}</label>
                      <input type="number" formControlName="doseAmount" min="0" step="0.01" placeholder="1" class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                    </div>
                    <div class="space-y-1">
                      <label class="block text-[11px] font-semibold text-gray-500">{{ 'Unit' | t }}</label>
                      <input type="text" formControlName="doseUnit" maxlength="30" [placeholder]="'tablet' | t" class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                    </div>
                    <div class="space-y-1">
                      <label class="block text-[11px] font-semibold text-gray-500">{{ 'per kg' | t }}</label>
                      <input type="number" formControlName="dosePerKg" min="0" step="1" placeholder="150" class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                    </div>
                    <div class="space-y-1">
                      <label class="block text-[11px] font-semibold text-gray-500">{{ 'Round up to' | t }}</label>
                      <select formControlName="doseRounding" class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                        <option value="None">{{ 'No rounding' | t }}</option>
                        <option value="Half">{{ 'Nearest 0.5' | t }}</option>
                        <option value="Whole">{{ 'Whole unit' | t }}</option>
                      </select>
                    </div>
                    <div class="space-y-1">
                      <label class="block text-[11px] font-semibold text-gray-500">{{ 'Min' | t }}</label>
                      <input type="number" formControlName="doseMin" min="0" step="0.01" class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                    </div>
                    <div class="space-y-1">
                      <label class="block text-[11px] font-semibold text-gray-500">{{ 'Max' | t }}</label>
                      <input type="number" formControlName="doseMax" min="0" step="0.01" class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                    </div>
                  </div>
                  <div *ngIf="step.get('inventoryItemId')?.value && (itemUnits()[step.get('inventoryItemId')?.value] || []).length > 1" class="mt-3 space-y-1 max-w-xs">
                    <label class="block text-[11px] font-semibold text-gray-500">{{ 'The dose is counted in (stock unit)' | t }}</label>
                    <select formControlName="doseUnitId" class="block w-full px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500">
                      <option [ngValue]="null">{{ 'Base unit of the item' | t }}</option>
                      <option *ngFor="let u of itemUnits()[step.get('inventoryItemId')?.value]" [ngValue]="u.id">{{ u.name }}</option>
                    </select>
                  </div>
                  <p *ngIf="doseExample(i) as ex" class="mt-2 mb-0 text-xs text-gray-600 dark:text-gray-300">{{ ex }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Footer Actions -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
          <button type="button" mat-dialog-close [disabled]="isSubmitting()"
            class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
            {{ 'Cancel' | t }}
          </button>
          <button type="submit" [disabled]="form.invalid || steps.length === 0 || isSubmitting()"
                  class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
            <mat-icon *ngIf="isSubmitting()" class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon>
            <span>{{ (isSubmitting() ? 'Saving...' : (isEditMode ? 'Update Protocol' : 'Create Protocol')) | t }}</span>
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
  private inventoryService = inject(InventoryService);
  private dialogRef = inject(MatDialogRef<CreateProtocolDialogComponent>);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(I18nService);

  form: FormGroup;
  isSubmitting = signal(false);
  error = signal('');
  isEditMode = false;
  protocolId?: string;
  inventoryItems = signal<InventoryItem[]>([]);

  speciesOptions = Object.entries(SPECIES_LABELS).map(([k, v]) => ({ value: k, label: v }));

  constructor(@Optional() @Inject(MAT_DIALOG_DATA) public data: any) {
    this.isEditMode = !!data;
    if (this.isEditMode) {
      this.protocolId = data.id;
    }
    
    this.loadInventoryItems();

    this.form = this.fb.group({
      title: [data?.title || '', [Validators.required, Validators.maxLength(200)]],
      targetSpecies: [data?.targetSpecies ?? null, Validators.required],
      description: [data?.description || ''],
      isDeworming: [{ value: data?.isDeworming || false, disabled: this.isEditMode || data?.lockIsDeworming }],
      autoStartForPurchased: [data?.autoStartForPurchased || false],
      steps: this.fb.array([])
    });
    
    if (this.isEditMode && data.steps?.length > 0) {
      data.steps.forEach((step: any) => {
        this.steps.push(this.stepGroup(step));
        if (step.inventoryItemId) this.loadItemUnits(step.inventoryItemId);
      });
    } else {
      this.addStep();
    }
  }

  get steps() {
    return this.form.get('steps') as FormArray;
  }

  addStep() {
    this.steps.push(this.stepGroup());
  }

  /** One step's form: name, day, medicine or care task, stock link, repeat and weight-based dose. */
  private stepGroup(step?: any): FormGroup {
    return this.fb.group({
      stepName: [step?.stepName ?? '', [Validators.required, Validators.maxLength(100)]],
      targetAgeDays: [step?.targetAgeDays ?? null, [Validators.required, Validators.min(0)]],
      vaccineName: [step?.vaccineName ?? '', [Validators.required, Validators.maxLength(100)]],
      dosageInstruction: [step?.dosageInstruction || '', [Validators.maxLength(250)]],
      inventoryItemId: [step?.inventoryItemId || null],
      dosageQuantity: [{ value: step?.dosageQuantity ?? null, disabled: !step?.inventoryItemId }, [Validators.min(0)]],
      kind: [step?.kind || 'Medicine'],
      repeatEveryDays: [step?.repeatEveryDays ?? null, [Validators.min(1)]],
      doseAmount: [step?.doseAmount ?? null, [Validators.min(0)]],
      dosePerKg: [step?.dosePerKg ?? null, [Validators.min(0)]],
      doseUnit: [step?.doseUnit ?? '', [Validators.maxLength(30)]],
      doseRounding: [step?.doseRounding || 'None'],
      doseMin: [step?.doseMin ?? null, [Validators.min(0)]],
      doseMax: [step?.doseMax ?? null, [Validators.min(0)]],
      doseUnitId: [step?.doseUnitId ?? null],
      actionLink: [step?.actionLink ?? null],
    });
  }

  setKind(index: number, kind: 'Medicine' | 'CareTask') {
    const step = this.steps.at(index) as FormGroup;
    step.get('kind')?.setValue(kind);
    if (kind === 'CareTask') {
      step.patchValue({ inventoryItemId: null, dosageQuantity: null, doseAmount: null, dosePerKg: null, doseUnitId: null });
      step.get('dosageQuantity')?.disable();
    }
  }

  /** "Example: a 290 kg animal gets 2 tablet" for the weight-based dose being entered. */
  doseExample(index: number): string | null {
    const v = (this.steps.at(index) as FormGroup).getRawValue();
    const amount = Number(v.doseAmount), perKg = Number(v.dosePerKg);
    if (!(amount > 0) || !(perKg > 0)) return null;
    const weight = 290;
    let dose = weight / perKg * amount;
    if (v.doseRounding === 'Whole') dose = Math.ceil(dose);
    else if (v.doseRounding === 'Half') dose = Math.ceil(dose * 2) / 2;
    else dose = Math.round(dose * 100) / 100;
    if (v.doseMin != null && v.doseMin !== '' && dose < Number(v.doseMin)) dose = Number(v.doseMin);
    if (v.doseMax != null && v.doseMax !== '' && dose > Number(v.doseMax)) dose = Number(v.doseMax);
    return this.i18n.t('Example: a {kg} kg animal gets {dose} {unit}', { kg: weight, dose, unit: v.doseUnit ? this.i18n.t(v.doseUnit) : '' });
  }

  /** Stock units per linked item (for a dose counted in a pack or other unit of the item). */
  readonly itemUnits = signal<Record<string, UnitConversion[]>>({});

  private loadItemUnits(itemId: string) {
    if (this.itemUnits()[itemId]) return;
    this.inventoryService.getItemUnits(itemId).subscribe({
      next: res => this.itemUnits.update(m => ({ ...m, [itemId]: res.units ?? [] })),
      error: () => this.itemUnits.update(m => ({ ...m, [itemId]: [] })),
    });
  }

  onInventoryItemChange(index: number) {
    const stepForm = this.steps.at(index) as FormGroup;
    const itemId = stepForm.get('inventoryItemId')?.value;
    const qtyCtrl = stepForm.get('dosageQuantity');
    
    stepForm.get('doseUnitId')?.setValue(null);
    if (itemId) {
      this.loadItemUnits(itemId);
      qtyCtrl?.enable();
      const item = this.inventoryItems().find(i => i.id === itemId);
      if (item && !stepForm.get('vaccineName')?.value) {
        stepForm.get('vaccineName')?.setValue(item.name);
      }
    } else {
      qtyCtrl?.disable();
      qtyCtrl?.setValue(null);
    }
  }

  removeStep(index: number) {
    this.steps.removeAt(index);
  }

  onSubmit() {
    if (this.form.invalid || this.steps.length === 0) {
      this.form.markAllAsTouched();
      this.notify.error(this.i18n.t('Please fill in all protocol fields and add at least one step.'));
      return;
    }

    this.isSubmitting.set(true);
    this.error.set('');

    const val = this.form.value;
    
    const request = {
      id: this.protocolId,
      title: val.title,
      targetSpecies: val.targetSpecies,
      description: val.description,
      isDeworming: this.form.get('isDeworming')?.value || false,
      autoStartForPurchased: !!val.autoStartForPurchased,
      steps: this.steps.controls.map(c => {
        const s = (c as FormGroup).getRawValue();
        const num = (x: any) => x === null || x === undefined || x === '' ? null : Number(x);
        const care = s.kind === 'CareTask';
        const weightBased = !care && num(s.doseAmount) && num(s.dosePerKg);
        return {
          ...s,
          inventoryItemId: care ? null : s.inventoryItemId,
          dosageQuantity: !care && s.inventoryItemId ? num(s.dosageQuantity) : null,
          repeatEveryDays: num(s.repeatEveryDays),
          doseAmount: weightBased ? num(s.doseAmount) : null,
          dosePerKg: weightBased ? num(s.dosePerKg) : null,
          doseMin: weightBased ? num(s.doseMin) : null,
          doseMax: weightBased ? num(s.doseMax) : null,
          doseUnit: weightBased ? (s.doseUnit || null) : null,
          doseRounding: weightBased ? s.doseRounding : 'None',
          doseUnitId: !care && s.inventoryItemId ? s.doseUnitId : null,
        };
      })
    };

    if (this.isEditMode) {
      this.healthService.updateVaccinationProtocol(this.protocolId!, request).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.notify.success(this.i18n.t('Protocol updated successfully!'));
          this.dialogRef.close(true);
        },
        error: (err) => {
          const parsedMsg = parseApiError(err, 'Failed to update protocol.');
          this.error.set(parsedMsg);
          this.notify.error(parsedMsg);
          this.isSubmitting.set(false);
        }
      });
    } else {
      this.healthService.createVaccinationProtocol(request).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.notify.success(this.i18n.t('Protocol created successfully!'));
          this.dialogRef.close(true);
        },
        error: (err) => {
          const parsedMsg = parseApiError(err, 'Failed to create protocol.');
          this.error.set(parsedMsg);
          this.notify.error(parsedMsg);
          this.isSubmitting.set(false);
        }
      });
    }
  }

  private loadInventoryItems() {
    // Load both Medicine and Vaccine for flexibility
    this.inventoryService.getItems({ pageSize: 500 }).subscribe({
      next: (res) => {
        const relevant = res.items.filter(i => 
          i.category === InventoryCategory.Medicine || 
          i.category === InventoryCategory.Vaccine
        );
        this.inventoryItems.set(relevant);
      },
      error: (err) => console.error('Failed to load inventory items', err)
    });
  }
}
