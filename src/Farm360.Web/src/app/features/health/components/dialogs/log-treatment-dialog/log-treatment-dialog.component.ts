import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { HealthService } from '../../../services/health.service';

@Component({
  selector: 'app-log-treatment-dialog',
  standalone: true,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    MatDialogModule, 
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule
  ],
  template: `
    <h2 mat-dialog-title>Log Medical Treatment</h2>
    <mat-dialog-content class="!pt-4">
      <form [formGroup]="form" class="flex flex-col gap-4">
        
        <mat-form-field appearance="outline">
          <mat-label>Animal ID</mat-label>
          <input matInput formControlName="animalId" placeholder="e.g. GUID">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Diagnosis</mat-label>
          <input matInput formControlName="diagnosis" placeholder="e.g. Mastitis">
        </mat-form-field>

        <div class="grid grid-cols-2 gap-4">
          <mat-form-field appearance="outline">
            <mat-label>Medication Name</mat-label>
            <input matInput formControlName="medicationName">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Dosage</mat-label>
            <input matInput formControlName="dosageAmount" type="number">
            <span matTextSuffix class="ml-2 mr-2">{{ form.get('dosageUnit')?.value || 'unit' }}</span>
          </mat-form-field>
        </div>

        <div class="grid grid-cols-2 gap-4">
          <mat-form-field appearance="outline">
            <mat-label>Milk Withdrawal (Days)</mat-label>
            <input matInput formControlName="milkWithdrawalDays" type="number">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Meat Withdrawal (Days)</mat-label>
            <input matInput formControlName="meatWithdrawalDays" type="number">
          </mat-form-field>
        </div>
        
        <div class="grid grid-cols-2 gap-4">
          <mat-form-field appearance="outline">
            <mat-label>Start Date</mat-label>
            <input matInput [matDatepicker]="picker" formControlName="startDate">
            <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
            <mat-datepicker #picker></mat-datepicker>
          </mat-form-field>
          
          <mat-form-field appearance="outline">
            <mat-label>Cost (BDT)</mat-label>
            <input matInput formControlName="costBdt" type="number">
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline">
          <mat-label>Veterinarian Name (Optional)</mat-label>
          <input matInput formControlName="veterinarianName">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Notes</mat-label>
          <textarea matInput formControlName="notes" rows="2"></textarea>
        </mat-form-field>
        
        <div *ngIf="error" class="text-red-500 text-sm mt-2">{{ error }}</div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end" class="!pb-4 !pr-4">
      <button mat-button mat-dialog-close [disabled]="isSubmitting">Cancel</button>
      <button mat-flat-button color="primary" [disabled]="form.invalid || isSubmitting" (click)="onSubmit()">
        {{ isSubmitting ? 'Saving...' : 'Save Treatment' }}
      </button>
    </mat-dialog-actions>
  `
})
export class LogTreatmentDialog {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private dialogRef = inject(MatDialogRef<LogTreatmentDialog>);

  form: FormGroup;
  isSubmitting = false;
  error = '';
  // Hardcoded MVP farm ID
  private farmId = '11111111-1111-1111-1111-111111111111';

  constructor() {
    this.form = this.fb.group({
      animalId: ['', Validators.required],
      diagnosis: ['', Validators.required],
      medicationName: ['', Validators.required],
      dosageAmount: [1, [Validators.required, Validators.min(0.1)]],
      dosageUnit: ['ml', Validators.required],
      milkWithdrawalDays: [0, [Validators.required, Validators.min(0)]],
      meatWithdrawalDays: [0, [Validators.required, Validators.min(0)]],
      startDate: [new Date(), Validators.required],
      costBdt: [0, [Validators.required, Validators.min(0)]],
      veterinarianName: [''],
      notes: ['']
    });
  }

  onSubmit() {
    if (this.form.invalid) return;

    this.isSubmitting = true;
    this.error = '';

    const val = this.form.value;
    const request = {
      farmId: this.farmId,
      animalId: val.animalId,
      diagnosis: val.diagnosis,
      medicationName: val.medicationName,
      dosageAmount: val.dosageAmount,
      dosageUnit: val.dosageUnit,
      milkWithdrawalDays: val.milkWithdrawalDays,
      meatWithdrawalDays: val.meatWithdrawalDays,
      startDate: new Date(val.startDate).toISOString().split('T')[0],
      costBdt: val.costBdt,
      veterinarianName: val.veterinarianName,
      notes: val.notes
    };

    this.healthService.logTreatment(request).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.error = err.error?.detail || 'Failed to log treatment. Please try again.';
        this.isSubmitting = false;
      }
    });
  }
}
