import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { HealthService } from '../../../services/health.service';

@Component({
  selector: 'app-assign-protocol-dialog',
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
    <h2 mat-dialog-title>Assign Protocol</h2>
    <mat-dialog-content class="!pt-4">
      <div class="mb-4 p-3 bg-blue-50 text-blue-800 rounded-md border border-blue-200">
        <div class="font-medium">Selected Protocol:</div>
        <div class="text-lg font-bold">{{ data?.protocol?.title }}</div>
      </div>
      
      <form [formGroup]="form" class="flex flex-col gap-4">
        
        <mat-form-field appearance="outline">
          <mat-label>Animal IDs (comma separated)</mat-label>
          <textarea matInput formControlName="animalIds" placeholder="e.g. GUID1, GUID2" rows="3"></textarea>
          <mat-hint>Enter one or more Animal IDs to assign this protocol to.</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Start Date</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="startDate">
          <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
          <mat-hint>The date to begin calculating scheduled events.</mat-hint>
        </mat-form-field>
        
        <div *ngIf="error" class="text-red-500 text-sm mt-2">{{ error }}</div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end" class="!pb-4 !pr-4">
      <button mat-button mat-dialog-close [disabled]="isSubmitting">Cancel</button>
      <button mat-flat-button color="primary" [disabled]="form.invalid || isSubmitting" (click)="onSubmit()">
        {{ isSubmitting ? 'Assigning...' : 'Assign Protocol' }}
      </button>
    </mat-dialog-actions>
  `
})
export class AssignProtocolDialog {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private dialogRef = inject(MatDialogRef<AssignProtocolDialog>);
  data = inject(MAT_DIALOG_DATA);

  form: FormGroup;
  isSubmitting = false;
  error = '';
  // Hardcoded MVP farm ID
  private farmId = '11111111-1111-1111-1111-111111111111';

  constructor() {
    this.form = this.fb.group({
      animalIds: ['', Validators.required],
      startDate: [new Date(), Validators.required]
    });
  }

  onSubmit() {
    if (this.form.invalid) return;

    this.isSubmitting = true;
    this.error = '';

    const val = this.form.value;
    
    // Parse animal IDs (comma separated, handle whitespace)
    const animalIdArray = val.animalIds
      .split(',')
      .map((id: string) => id.trim())
      .filter((id: string) => id.length > 0);
      
    if (animalIdArray.length === 0) {
      this.error = 'Please enter at least one valid Animal ID.';
      this.isSubmitting = false;
      return;
    }

    const request = {
      farmId: this.farmId,
      protocolId: this.data.protocol.id,
      animalIds: animalIdArray,
      startDate: new Date(val.startDate).toISOString().split('T')[0]
    };

    this.healthService.assignProtocolToAnimals(request).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.error = err.error?.detail || 'Failed to assign protocol.';
        this.isSubmitting = false;
      }
    });
  }
}
