import { Component, inject, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { AnimalService } from '../../services/animal.service';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface RecordWeightDialogData {
  animalId: string;
  animalTag: string;
}

@Component({
  selector: 'app-record-weight-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule, TranslatePipe],
  templateUrl: './record-weight-dialog.component.html'
})
export class RecordWeightDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<RecordWeightDialogComponent>);
  public readonly data = inject<RecordWeightDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(I18nService);
  private readonly animalSvc = inject(AnimalService);
  private readonly destroy$ = new Subject<void>();

  readonly form = this.fb.group({
    weightKg: [null as number | null, [Validators.required, Validators.min(1)]],
    recordedDate: [toLocalIsoDate(), Validators.required],
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

    this.animalSvc.recordWeight(this.data.animalId, {
      weightKg: v.weightKg!,
      recordedDate: v.recordedDate!,
      notes: v.notes || undefined
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        this.submitting.set(false);
        if (err.error?.errors) {
            this.error.set(Object.values(err.error.errors).flat().join('\n'));
        } else {
            this.error.set(err.error?.detail || err.error?.title || this.i18n.t('Recording weight failed.'));
        }
      }
    });
  }
}
