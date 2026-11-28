import { Component, inject, ChangeDetectionStrategy, signal, computed, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { AnimalService } from '../../services/animal.service';
import { RecordWeightRequest } from '../../models/animal.models';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import {
  TAPE_DIVISOR, TapeUnit, WeightMode, estimateTapeKg, isPlausibleInch,
  loadTapeUnit, loadWeightMode, saveTapeUnit, saveWeightMode, toInches
} from '../../utils/tape-weight';

export interface RecordWeightDialogData {
  animalId: string;
  animalTag: string;
}

/** The live tape estimate, worked out in inches whatever unit was entered. */
export interface TapePreview {
  lengthInch: number;
  girthInch: number;
  kg: number;
}

@Component({
  selector: 'app-record-weight-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule, TranslatePipe],
  templateUrl: './record-weight-dialog.component.html'
})
export class RecordWeightDialogComponent implements OnDestroy {
  private readonly dialogRef = inject(MatDialogRef<RecordWeightDialogComponent>);
  public readonly data = inject<RecordWeightDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(I18nService);
  private readonly animalSvc = inject(AnimalService);
  private readonly destroy$ = new Subject<void>();

  readonly divisor = TAPE_DIVISOR;

  readonly form = this.fb.group({
    weightKg: [null as number | null],
    length: [null as number | null],
    girth: [null as number | null],
    recordedDate: [toLocalIsoDate(), Validators.required],
    notes: ['', Validators.maxLength(500)]
  });

  readonly mode = signal<WeightMode>(loadWeightMode());
  readonly unit = signal<TapeUnit>(loadTapeUnit());
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  private readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  /** Length and girth in inches, or null when not entered. */
  private readonly inches = computed(() => {
    const v = this.formValue();
    const unit = this.unit();
    return {
      length: v.length != null && v.length > 0 ? toInches(v.length, unit) : null,
      girth: v.girth != null && v.girth > 0 ? toInches(v.girth, unit) : null
    };
  });

  readonly lengthOutOfRange = computed(() => { const l = this.inches().length; return l != null && !isPlausibleInch(l); });
  readonly girthOutOfRange = computed(() => { const g = this.inches().girth; return g != null && !isPlausibleInch(g); });

  readonly preview = computed<TapePreview | null>(() => {
    const { length, girth } = this.inches();
    if (!isPlausibleInch(length) || !isPlausibleInch(girth)) return null;
    return { lengthInch: length!, girthInch: girth!, kg: estimateTapeKg(length!, girth!) };
  });

  constructor() {
    this.applyValidators();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  setMode(mode: WeightMode): void {
    if (this.mode() === mode) return;
    this.mode.set(mode);
    saveWeightMode(mode);
    this.error.set(null);
    this.applyValidators();
  }

  setUnit(unit: TapeUnit): void {
    this.unit.set(unit);
    saveTapeUnit(unit);
  }

  private applyValidators(): void {
    const tape = this.mode() === 'Tape';
    const kg = this.form.controls.weightKg;
    const length = this.form.controls.length;
    const girth = this.form.controls.girth;
    kg.setValidators(tape ? [] : [Validators.required, Validators.min(0.1), Validators.max(2000)]);
    length.setValidators(tape ? [Validators.required, Validators.min(0.1)] : []);
    girth.setValidators(tape ? [Validators.required, Validators.min(0.1)] : []);
    for (const c of [kg, length, girth]) c.updateValueAndValidity({ emitEvent: false });
  }

  submit(): void {
    const tape = this.mode() === 'Tape';
    if (this.form.invalid || (tape && !this.preview())) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const v = this.form.getRawValue();
    const p = this.preview();
    // Tape: send only the measurements (inches); the server computes the kg.
    const request: RecordWeightRequest = tape
      ? { method: 'Tape', lengthInch: p!.lengthInch, girthInch: p!.girthInch, recordedDate: v.recordedDate!, notes: v.notes || undefined }
      : { method: 'Scale', weightKg: v.weightKg!, recordedDate: v.recordedDate!, notes: v.notes || undefined };

    this.animalSvc.recordWeight(this.data.animalId, request).pipe(takeUntil(this.destroy$)).subscribe({
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
