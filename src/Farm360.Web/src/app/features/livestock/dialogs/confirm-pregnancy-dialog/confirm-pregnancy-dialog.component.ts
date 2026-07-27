import { Component, inject, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { AnimalService } from '../../services/animal.service';

export interface ConfirmPregnancyDialogData {
  animalId: string;
  animalTag: string;
  recordId: string;
}

@Component({
  selector: 'app-confirm-pregnancy-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './confirm-pregnancy-dialog.component.html'
})
export class ConfirmPregnancyDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<ConfirmPregnancyDialogComponent>);
  public readonly data = inject<ConfirmPregnancyDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly animalSvc = inject(AnimalService);
  private readonly destroy$ = new Subject<void>();

  readonly form = this.fb.group({
    confirmDate: [new Date().toISOString().split('T')[0], Validators.required],
    expectedCalvingDate: ['', Validators.required]
  });

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    const expected = new Date();
    expected.setDate(expected.getDate() + 283); // ~283 days for cows roughly, user can adjust
    this.form.get('expectedCalvingDate')?.setValue(expected.toISOString().split('T')[0]);
  }

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

    this.animalSvc.confirmPregnancy(this.data.animalId, this.data.recordId, {
      confirmDate: v.confirmDate!,
      expectedCalvingDate: v.expectedCalvingDate!
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        this.submitting.set(false);
        if (err.error?.errors) {
            this.error.set(Object.values(err.error.errors).flat().join('\n'));
        } else {
            this.error.set(err.error?.detail || err.error?.title || 'Pregnancy confirmation failed.');
        }
      }
    });
  }
}
