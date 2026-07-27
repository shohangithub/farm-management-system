import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSelectModule } from '@angular/material/select';
import { HealthService } from '../../../services/health.service';
import { CauseOfDeath } from '../../../models/health.models';
import { AnimalPickerComponent } from '../../../../../shared/components/animal-picker/animal-picker.component';
import { WorkingContextService } from '../../../../../core/services/working-context.service';

@Component({
  selector: 'app-record-mortality-dialog',
  standalone: true,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    MatDialogModule, 
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatSelectModule,
    AnimalPickerComponent
  ],
  template: `
    <h2 mat-dialog-title>Record Mortality</h2>
    <mat-dialog-content class="!pt-4">
      <form [formGroup]="form" class="flex flex-col gap-4">
        
        <div class="mb-2">
          <mat-label class="text-sm font-medium text-gray-700">Select Animal</mat-label>
          <app-animal-picker formControlName="animalId"></app-animal-picker>
        </div>

        <div class="grid grid-cols-2 gap-4">
          <mat-form-field appearance="outline">
            <mat-label>Date of Death</mat-label>
            <input matInput [matDatepicker]="picker" formControlName="deathDate">
            <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
            <mat-datepicker #picker></mat-datepicker>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Cause of Death</mat-label>
            <mat-select formControlName="causeOfDeath">
              <mat-option *ngFor="let cause of causes" [value]="cause">{{ cause }}</mat-option>
            </mat-select>
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" *ngIf="form.get('causeOfDeath')?.value === 'Disease'">
          <mat-label>Disease Name</mat-label>
          <input matInput formControlName="diseaseName">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Estimated Economic Loss (BDT)</mat-label>
          <input matInput formControlName="estimatedEconomicLossBdt" type="number">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Post-Mortem Notes</mat-label>
          <textarea matInput formControlName="postMortemNotes" rows="3"></textarea>
        </mat-form-field>
        
        <div *ngIf="error" class="text-red-500 text-sm mt-2">{{ error }}</div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end" class="!pb-4 !pr-4">
      <button mat-button mat-dialog-close [disabled]="isSubmitting">Cancel</button>
      <button mat-flat-button color="warn" [disabled]="form.invalid || isSubmitting" (click)="onSubmit()">
        {{ isSubmitting ? 'Recording...' : 'Record Death' }}
      </button>
    </mat-dialog-actions>
  `
})
export class RecordMortalityDialog {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private dialogRef = inject(MatDialogRef<RecordMortalityDialog>);

  form: FormGroup;
  isSubmitting = false;
  error = '';
  causes = Object.values(CauseOfDeath);

  constructor() {
    this.form = this.fb.group({
      animalId: ['', Validators.required],
      deathDate: [new Date(), Validators.required],
      causeOfDeath: [CauseOfDeath.Unknown, Validators.required],
      diseaseName: [''],
      postMortemNotes: [''],
      estimatedEconomicLossBdt: [0, Validators.min(0)]
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

    const request = {
      farmId: farmId,
      animalId: val.animalId,
      deathDate: new Date(val.deathDate).toISOString().split('T')[0],
      causeOfDeath: val.causeOfDeath,
      diseaseName: val.diseaseName,
      postMortemNotes: val.postMortemNotes,
      estimatedEconomicLossBdt: val.estimatedEconomicLossBdt,
      recordedByUserId: '00000000-0000-0000-0000-000000000000' // Mock user ID for MVP
    };

    this.healthService.recordMortality(request).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.error = err.error?.detail || 'Failed to record mortality.';
        this.isSubmitting = false;
      }
    });
  }
}
