import { Component, inject, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { AnimalService } from '../../services/animal.service';

export interface RecordCalvingDialogData {
  animalId: string;
  animalTag: string;
  recordId: string;
}

@Component({
  selector: 'app-record-calving-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './record-calving-dialog.component.html'
})
export class RecordCalvingDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<RecordCalvingDialogComponent>);
  public readonly data = inject<RecordCalvingDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly animalSvc = inject(AnimalService);
  private readonly destroy$ = new Subject<void>();

  readonly form = this.fb.group({
    calvingDate: [new Date().toISOString().split('T')[0], Validators.required],
    outcome: ['Live Birth', Validators.required],
    calvesCount: [1, [Validators.required, Validators.min(1)]]
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

    this.animalSvc.recordCalving(this.data.animalId, this.data.recordId, {
      calvingDate: v.calvingDate!,
      outcome: v.outcome!,
      calvesCount: v.calvesCount!
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        this.submitting.set(false);
        if (err.error?.errors) {
            this.error.set(Object.values(err.error.errors).flat().join('\n'));
        } else {
            this.error.set(err.error?.detail || err.error?.title || 'Calving record failed.');
        }
      }
    });
  }
}
