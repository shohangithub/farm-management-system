import { Component, inject, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { BatchService } from '../../services/batch.service';
import { BatchDto } from '../../models/batch.models';

export interface AssignBatchDialogData {
  animalId: string;
  animalTag: string;
  currentBatchId: string | null;
  availableBatches: BatchDto[];
}

@Component({
  selector: 'app-assign-batch-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './assign-batch-dialog.component.html'
})
export class AssignBatchDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<AssignBatchDialogComponent>);
  public readonly data = inject<AssignBatchDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly batchSvc = inject(BatchService);
  private readonly destroy$ = new Subject<void>();

  readonly form = this.fb.group({
    batchId: [this.data.currentBatchId || '', Validators.required]
  });

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const v = this.form.getRawValue();

    this.batchSvc.assignAnimalsToBatch(v.batchId!, [this.data.animalId]).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        this.submitting.set(false);
        if (err.error?.errors) {
            this.error.set(Object.values(err.error.errors).flat().join('\n'));
        } else {
            this.error.set(err.error?.detail || err.error?.title || 'Batch assignment failed.');
        }
      }
    });
  }
}
