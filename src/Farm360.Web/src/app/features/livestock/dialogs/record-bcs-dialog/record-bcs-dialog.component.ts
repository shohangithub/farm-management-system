import { Component, inject, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { AnimalService } from '../../services/animal.service';

export interface RecordBcsDialogData {
  animalId: string;
  animalTag: string;
}

@Component({
  selector: 'app-record-bcs-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './record-bcs-dialog.component.html'
})
export class RecordBcsDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<RecordBcsDialogComponent>);
  public readonly data = inject<RecordBcsDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly animalSvc = inject(AnimalService);
  private readonly destroy$ = new Subject<void>();

  readonly form = this.fb.group({
    score: [null as number | null, [Validators.required, Validators.min(1), Validators.max(5)]],
    recordedDate: [new Date().toISOString().split('T')[0], Validators.required],
    notes: ['', Validators.maxLength(500)]
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

    this.animalSvc.recordBcs(this.data.animalId, v.score!, v.recordedDate!, v.notes || undefined)
      .pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        this.submitting.set(false);
        if (err.error?.errors) {
            this.error.set(Object.values(err.error.errors).flat().join('\n'));
        } else {
            this.error.set(err.error?.detail || err.error?.title || 'Recording BCS failed.');
        }
      }
    });
  }
}
