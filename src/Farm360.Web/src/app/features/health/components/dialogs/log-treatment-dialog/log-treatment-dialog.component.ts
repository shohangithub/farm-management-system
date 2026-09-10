import { Component, inject, signal, computed, ChangeDetectionStrategy, DestroyRef, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Observable } from 'rxjs';
import { map, startWith } from 'rxjs/operators';
import { HealthService } from '../../../services/health.service';
import { InventoryService } from '../../../../inventory/services/inventory.service';
import { InventoryItem, InventoryCategory } from '../../../../inventory/models/inventory.models';
import { AnimalPickerComponent } from '../../../../../shared/components/animal-picker/animal-picker.component';
import { WorkingContextService } from '../../../../../core/services/working-context.service';
import { parseApiError } from '../../../../../core/utils/error-parser';
import { MedicalTreatmentDto, TreatmentStatus } from '../../../models/health.models';

@Component({
  selector: 'app-log-treatment-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    MatDialogModule, 
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatAutocompleteModule,
    MatSnackBarModule,
    AnimalPickerComponent
  ],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-primary-600">medical_services</mat-icon>
            {{ isEditMode() ? 'Edit Medical Treatment' : 'Log Medical Treatment' }}
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">
            {{ isEditMode() ? 'Update diagnosis, medication, dosage, and cost details' : 'Record animal medical treatment and medication' }}
          </p>
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

        <div class="p-6 space-y-4 overflow-y-auto custom-scrollbar flex-1">
          
          <!-- Animal Selection -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Selected Animal <span class="text-red-500">*</span>
            </label>
            <app-animal-picker formControlName="animalId"></app-animal-picker>
          </div>

          <!-- Status & End Date (Visible in Edit Mode) -->
          <div *ngIf="isEditMode()" class="grid grid-cols-1 md:grid-cols-2 gap-4 p-3.5 bg-amber-50/60 dark:bg-amber-950/20 rounded-xl border border-amber-200/70 dark:border-amber-800/40">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-amber-900 dark:text-amber-200">Treatment Status</label>
              <select formControlName="status"
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                <option [value]="treatmentStatus.Ongoing">Ongoing</option>
                <option [value]="treatmentStatus.Completed">Completed</option>
                <option [value]="treatmentStatus.Failed">Failed</option>
                <option [value]="treatmentStatus.Discontinued">Discontinued</option>
              </select>
            </div>
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-amber-900 dark:text-amber-200">End Date (Optional)</label>
              <input type="date" formControlName="endDate"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>
          </div>

          <!-- Diagnosis -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Diagnosis <span class="text-red-500">*</span></label>
            <input type="text" formControlName="diagnosis" placeholder="e.g. Mastitis, Foot Rot..."
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
          </div>

          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div class="space-y-1.5 md:col-span-1">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Inventory Item</label>
              <select formControlName="inventoryItemId"
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                <option [value]="null">-- Select (Optional) --</option>
                @for (item of inventoryItems(); track item.id) {
                  <option [value]="item.id">{{ item.name }} ({{ item.currentStock }} {{ item.unitOfMeasure }})</option>
                }
              </select>
            </div>
            
            <div class="space-y-1.5 md:col-span-2">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Medication Name <span class="text-red-500">*</span></label>
              <input type="text" formControlName="medicationName" placeholder="e.g. Oxytet 20%"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>

            <div class="space-y-1.5 md:col-span-1">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Consumption Qty</label>
              <input type="number" formControlName="consumptionQuantity" min="0" step="0.1" placeholder="Stock to deduct"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>

            <div class="grid grid-cols-2 gap-2 md:col-span-2">
              <div class="space-y-1.5">
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Dosage <span class="text-red-500">*</span></label>
                <input type="number" formControlName="dosageAmount" min="0.1" step="0.1"
                       class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
              </div>
              <div class="space-y-1.5">
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Unit <span class="text-red-500">*</span></label>
                <select formControlName="dosageUnit"
                        class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                  <option value="ml">ml</option>
                  <option value="mg">mg</option>
                  <option value="g">g</option>
                  <option value="bolus">bolus</option>
                  <option value="tablets">tablets</option>
                </select>
              </div>
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Milk Withdrawal (Days)</label>
              <input type="number" formControlName="milkWithdrawalDays" min="0" placeholder="0"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Meat Withdrawal (Days)</label>
              <input type="number" formControlName="meatWithdrawalDays" min="0" placeholder="0"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Start Date <span class="text-red-500">*</span></label>
              <input type="date" formControlName="startDate"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Treatment Cost (BDT)</label>
              <input type="number" formControlName="costBdt" min="0" placeholder="0"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Veterinarian Name</label>
            <input type="text" formControlName="veterinarianName" placeholder="Dr. John Doe" [matAutocomplete]="autoVet"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            <mat-autocomplete #autoVet="matAutocomplete">
              <mat-option *ngFor="let option of filteredVetNames$ | async" [value]="option">
                {{ option }}
              </mat-option>
            </mat-autocomplete>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Notes / Administration Route</label>
            <textarea formControlName="notes" rows="2" placeholder="e.g. IM injection in neck area..."
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow resize-none"></textarea>
          </div>
        </div>

        <!-- Footer Actions -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
          <button type="button" mat-dialog-close [disabled]="isSubmitting()"
            class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || isSubmitting()"
                  class="px-5 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
            <mat-icon *ngIf="isSubmitting()" class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon>
            <span>{{ isSubmitting() ? 'Saving...' : (isEditMode() ? 'Update Treatment' : 'Save Treatment') }}</span>
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
export class LogTreatmentDialog implements OnInit {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private inventoryService = inject(InventoryService);
  private contextService = inject(WorkingContextService);
  private dialogRef = inject(MatDialogRef<LogTreatmentDialog>);
  private snackBar = inject(MatSnackBar);
  private destroyRef = inject(DestroyRef);
  public data: { treatment?: MedicalTreatmentDto } = inject(MAT_DIALOG_DATA, { optional: true }) ?? {};

  readonly treatmentStatus = TreatmentStatus;
  readonly isEditMode = computed(() => !!this.data?.treatment);

  form: FormGroup;
  isSubmitting = signal(false);
  error = signal('');
  inventoryItems = signal<InventoryItem[]>([]);
  knownVetNames = signal<string[]>([]);
  filteredVetNames$!: Observable<string[]>;

  constructor() {
    this.form = this.fb.group({
      animalId: ['', Validators.required],
      diagnosis: ['', Validators.required],
      medicationName: ['', Validators.required],
      dosageAmount: [1, [Validators.required, Validators.min(0.1)]],
      dosageUnit: ['ml', Validators.required],
      milkWithdrawalDays: [0, [Validators.required, Validators.min(0)]],
      meatWithdrawalDays: [0, [Validators.required, Validators.min(0)]],
      startDate: [new Date().toISOString().split('T')[0], Validators.required],
      endDate: [null],
      costBdt: [0, [Validators.required, Validators.min(0)]],
      veterinarianName: [''],
      status: [TreatmentStatus.Ongoing],
      notes: [''],
      inventoryItemId: [null],
      consumptionQuantity: [null, [Validators.min(0)]]
    });
  }

  ngOnInit() {
    this.filteredVetNames$ = this.form.get('veterinarianName')!.valueChanges.pipe(
      startWith(''),
      map(value => this._filterVets(value || ''))
    );

    const farmId = this.contextService.currentFarmValue?.id;
    if (farmId) {
      this.healthService.getVetVisits({ farmId, pageSize: 100 }).pipe(
        takeUntilDestroyed(this.destroyRef)
      ).subscribe({
        next: (res) => {
          const uniqueNames = Array.from(new Set(res.items.map(v => v.vetName).filter(Boolean)));
          this.knownVetNames.update(names => Array.from(new Set([...names, ...uniqueNames])));
        },
        error: () => {}
      });

      this.healthService.getTreatments({ farmId, pageSize: 100 }).pipe(
        takeUntilDestroyed(this.destroyRef)
      ).subscribe({
        next: (res) => {
          const uniqueNames = Array.from(new Set(res.items.map(t => t.veterinarianName).filter(Boolean))) as string[];
          this.knownVetNames.update(names => Array.from(new Set([...names, ...uniqueNames])));
        },
        error: () => {}
      });
    }

    this.inventoryService.getItems({ category: InventoryCategory.Medicine, pageSize: 100 }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (res) => this.inventoryItems.set(res.items)
    });

    this.form.get('inventoryItemId')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(id => {
      if (id && !this.isEditMode()) {
        const item = this.inventoryItems().find(i => i.id === id);
        if (item) {
          this.form.patchValue({ 
            medicationName: item.name,
            consumptionQuantity: this.form.get('dosageAmount')?.value
          });
        }
      }
    });

    // Populate existing values if editing
    if (this.data?.treatment) {
      const t = this.data.treatment;
      this.form.patchValue({
        animalId: t.animalId,
        diagnosis: t.diagnosis,
        medicationName: t.medicationName,
        dosageAmount: t.dosageAmount,
        dosageUnit: t.dosageUnit,
        milkWithdrawalDays: t.milkWithdrawalDays,
        meatWithdrawalDays: t.meatWithdrawalDays,
        startDate: t.startDate ? new Date(t.startDate).toISOString().split('T')[0] : '',
        endDate: t.endDate ? new Date(t.endDate).toISOString().split('T')[0] : null,
        costBdt: t.costBdt,
        veterinarianName: t.veterinarianName || '',
        status: t.status || TreatmentStatus.Ongoing,
        notes: t.notes || '',
        inventoryItemId: t.inventoryItemId || null,
        consumptionQuantity: t.consumptionQuantity || null
      });
    }
  }

  private _filterVets(value: string): string[] {
    const filterValue = (value || '').toLowerCase();
    return this.knownVetNames().filter(name => name.toLowerCase().includes(filterValue));
  }

  onSubmit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snackBar.open('Please fill in all required fields correctly.', 'Close', {
        duration: 4000,
        panelClass: ['snack-error']
      });
      return;
    }

    this.isSubmitting.set(true);
    this.error.set('');

    const val = this.form.value;

    if (this.isEditMode()) {
      const updateRequest = {
        diagnosis: val.diagnosis,
        medicationName: val.medicationName,
        dosageAmount: val.dosageAmount,
        dosageUnit: val.dosageUnit,
        milkWithdrawalDays: val.milkWithdrawalDays,
        meatWithdrawalDays: val.meatWithdrawalDays,
        startDate: val.startDate,
        endDate: val.endDate ? val.endDate : null,
        costBdt: val.costBdt,
        veterinarianName: val.veterinarianName?.trim() || null,
        notes: val.notes?.trim() || null,
        status: val.status || TreatmentStatus.Ongoing,
        inventoryItemId: val.inventoryItemId || null,
        consumptionQuantity: val.consumptionQuantity || null
      };

      this.healthService.updateTreatment(this.data.treatment!.id, updateRequest).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.snackBar.open('Treatment updated successfully!', 'Close', {
            duration: 3000,
            panelClass: ['snack-success']
          });
          this.dialogRef.close(true);
        },
        error: (err) => {
          const parsedMsg = parseApiError(err, 'Failed to update treatment. Please try again.');
          this.error.set(parsedMsg);
          this.snackBar.open(parsedMsg, 'Close', {
            duration: 5000,
            panelClass: ['snack-error']
          });
          this.isSubmitting.set(false);
        }
      });
      return;
    }

    const farmId = this.contextService.currentFarmValue?.id || '';
    if (!farmId) {
      const msg = 'No farm context available.';
      this.error.set(msg);
      this.snackBar.open(msg, 'Close', { duration: 4000, panelClass: ['snack-error'] });
      this.isSubmitting.set(false);
      return;
    }

    const createRequest = {
      farmId: farmId,
      animalId: val.animalId,
      diagnosis: val.diagnosis,
      medicationName: val.medicationName,
      dosageAmount: val.dosageAmount,
      dosageUnit: val.dosageUnit,
      milkWithdrawalDays: val.milkWithdrawalDays,
      meatWithdrawalDays: val.meatWithdrawalDays,
      startDate: new Date(val.startDate).toISOString().split('T')[0],
      costBdt: val.costBdt,
      veterinarianName: val.veterinarianName?.trim() || null,
      notes: val.notes?.trim() || null,
      inventoryItemId: val.inventoryItemId || null,
      consumptionQuantity: val.consumptionQuantity || null
    };

    this.healthService.logTreatment(createRequest).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.snackBar.open('Treatment logged successfully!', 'Close', {
          duration: 3000,
          panelClass: ['snack-success']
        });
        this.dialogRef.close(true);
      },
      error: (err) => {
        const parsedMsg = parseApiError(err, 'Failed to log treatment. Please try again.');
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
