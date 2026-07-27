import { Component, inject, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { AnimalService } from '../../services/animal.service';

export interface MatingDialogData {
  animalId: string;
  animalTag: string;
}

@Component({
  selector: 'app-mating-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './mating-dialog.component.html'
})
export class MatingDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<MatingDialogComponent>);
  public readonly data = inject<MatingDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly animalSvc = inject(AnimalService);
  private readonly destroy$ = new Subject<void>();

  readonly form = this.fb.group({
    date: [new Date().toISOString().split('T')[0], Validators.required],
    isAI: [false],
    sireAnimalId: [''],
    sireExternalId: ['']
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

    this.animalSvc.recordMating(this.data.animalId, {
      matingDate: v.date!,
      isArtificialInsemination: v.isAI || false,
      sireAnimalId: v.sireAnimalId || undefined,
      sireExternalId: v.sireExternalId || undefined
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        this.submitting.set(false);
        if (err.error?.errors) {
            this.error.set(Object.values(err.error.errors).flat().join('\n'));
        } else {
            this.error.set(err.error?.detail || err.error?.title || 'Mating record failed.');
        }
      }
    });
  }
}
