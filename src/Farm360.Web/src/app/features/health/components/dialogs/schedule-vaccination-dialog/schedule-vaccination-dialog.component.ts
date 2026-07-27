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
  selector: 'app-schedule-vaccination-dialog',
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
    <h2 mat-dialog-title>Schedule Vaccination</h2>
    <mat-dialog-content class="!pt-4">
      <form [formGroup]="form" class="flex flex-col gap-4">
        
        <mat-form-field appearance="outline">
          <mat-label>Animal ID</mat-label>
          <input matInput formControlName="animalId" placeholder="e.g. GUID">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Vaccine Name</mat-label>
          <input matInput formControlName="vaccineName" placeholder="e.g. FMD Vaccine">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Batch Number</mat-label>
          <input matInput formControlName="batchNumber">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Scheduled Date</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="scheduledDate">
          <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Notes (Optional)</mat-label>
          <textarea matInput formControlName="notes" rows="2"></textarea>
        </mat-form-field>
        
        <div *ngIf="error" class="text-red-500 text-sm mt-2">{{ error }}</div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end" class="!pb-4 !pr-4">
      <button mat-button mat-dialog-close [disabled]="isSubmitting">Cancel</button>
      <button mat-flat-button color="primary" [disabled]="form.invalid || isSubmitting" (click)="onSubmit()">
        {{ isSubmitting ? 'Scheduling...' : 'Schedule Vaccine' }}
      </button>
    </mat-dialog-actions>
  `
})
export class ScheduleVaccinationDialog {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private dialogRef = inject(MatDialogRef<ScheduleVaccinationDialog>);

  form: FormGroup;
  isSubmitting = false;
  error = '';
  private farmId = '11111111-1111-1111-1111-111111111111';

  constructor() {
    this.form = this.fb.group({
      animalId: ['', Validators.required],
      vaccineName: ['', Validators.required],
      batchNumber: ['', Validators.required],
      scheduledDate: [new Date(), Validators.required],
      notes: ['']
    });
  }

  onSubmit() {
    if (this.form.invalid) return;

    this.isSubmitting = true;
    this.error = '';

    const val = this.form.value;
    const formattedDate = new Date(val.scheduledDate).toISOString().split('T')[0];

    this.healthService.scheduleVaccination(
      val.animalId, 
      val.vaccineName, 
      val.batchNumber, 
      formattedDate, 
      val.notes
    ).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.error = err.error?.detail || 'Failed to schedule vaccination.';
        this.isSubmitting = false;
      }
    });
  }
}
