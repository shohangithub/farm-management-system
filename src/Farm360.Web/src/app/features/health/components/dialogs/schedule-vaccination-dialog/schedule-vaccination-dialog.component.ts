import { Component, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { HealthService } from '../../../services/health.service';
import { InventoryService } from '../../../../inventory/services/inventory.service';
import { InventoryItem, InventoryCategory } from '../../../../inventory/models/inventory.models';
import { AnimalPickerComponent } from '../../../../../shared/components/animal-picker/animal-picker.component';
import { parseApiError } from '../../../../../core/utils/error-parser';

@Component({
  selector: 'app-schedule-vaccination-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    MatDialogModule, 
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule,
    AnimalPickerComponent
  ],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-gray-500">vaccines</mat-icon>
            Schedule Vaccination
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Plan upcoming animal vaccinations</p>
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
          
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Select Animal <span class="text-red-500">*</span></label>
            <app-animal-picker formControlName="animalId"></app-animal-picker>
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
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Vaccine Name <span class="text-red-500">*</span></label>
              <input type="text" formControlName="vaccineName" placeholder="e.g. FMD Vaccine"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>
            
            <div class="space-y-1.5 md:col-span-1">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Dosage Qty</label>
              <input type="number" formControlName="dosageQuantity" min="0" step="0.1" placeholder="Qty"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>

            <div class="grid grid-cols-2 gap-2 md:col-span-2">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Batch Number</label>
              <input type="text" formControlName="batchNumber" placeholder="Optional batch #"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Scheduled Date <span class="text-red-500">*</span></label>
              <input type="date" formControlName="scheduledDate"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>
          </div>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Notes</label>
            <textarea formControlName="notes" rows="3" placeholder="Booster details or special instructions..."
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
                  class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
            <mat-icon *ngIf="isSubmitting()" class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon>
            <span>{{ isSubmitting() ? 'Scheduling...' : 'Schedule' }}</span>
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
export class ScheduleVaccinationDialog {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private inventoryService = inject(InventoryService);
  private dialogRef = inject(MatDialogRef<ScheduleVaccinationDialog>);
  private snackBar = inject(MatSnackBar);

  form: FormGroup;
  isSubmitting = signal(false);
  error = signal('');
  inventoryItems = signal<InventoryItem[]>([]);

  constructor() {
    this.form = this.fb.group({
      animalId: ['', Validators.required],
      vaccineName: ['', Validators.required],
      batchNumber: [''],
      scheduledDate: [new Date(), Validators.required],
      notes: [''],
      inventoryItemId: [null],
      dosageQuantity: [null, [Validators.min(0)]]
    });
  }

  ngOnInit() {
    this.inventoryService.getItems({ category: InventoryCategory.Vaccine, pageSize: 100 }).subscribe({
      next: (res) => this.inventoryItems.set(res.items)
    });

    this.form.get('inventoryItemId')?.valueChanges.subscribe(id => {
      if (id) {
        const item = this.inventoryItems().find(i => i.id === id);
        if (item) {
          this.form.patchValue({ vaccineName: item.name });
        }
      }
    });
  }

  onSubmit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snackBar.open('Please select an animal and enter the vaccine name.', 'Close', {
        duration: 4000,
        panelClass: ['snack-error']
      });
      return;
    }

    this.isSubmitting.set(true);
    this.error.set('');

    const val = this.form.value;
    const formattedDate = new Date(val.scheduledDate).toISOString().split('T')[0];

    this.healthService.scheduleVaccination(
      val.animalId, 
      val.vaccineName, 
      val.batchNumber, 
      formattedDate, 
      val.notes,
      val.inventoryItemId,
      val.dosageQuantity
    ).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.snackBar.open('Vaccination scheduled successfully!', 'Close', {
          duration: 3000,
          panelClass: ['snack-success']
        });
        this.dialogRef.close(true);
      },
      error: (err) => {
        const parsedMsg = parseApiError(err, 'Failed to schedule vaccination.');
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
