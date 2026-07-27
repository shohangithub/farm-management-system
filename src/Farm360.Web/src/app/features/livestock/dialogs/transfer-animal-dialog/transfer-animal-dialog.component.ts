import { Component, inject, OnInit, signal, ChangeDetectionStrategy, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { AnimalService } from '../../services/animal.service';
import { ShedService } from '../../../farms/services/shed.service';
import { PenService } from '../../../farms/services/pen.service';
import { ShedList } from '../../../farms/models/shed.model';
import { PenList } from '../../../farms/models/pen.model';

export interface TransferAnimalDialogData {
  animalId: string;
  farmId: string;
  currentShedId?: string;
  currentPenId?: string;
  animalTag: string;
}

@Component({
  selector: 'app-transfer-animal-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './transfer-animal-dialog.component.html'
})
export class TransferAnimalDialogComponent implements OnInit, OnDestroy {
  private readonly dialogRef = inject(MatDialogRef<TransferAnimalDialogComponent>);
  public readonly data = inject<TransferAnimalDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly animalSvc = inject(AnimalService);
  private readonly shedSvc = inject(ShedService);
  private readonly penSvc = inject(PenService);
  private readonly destroy$ = new Subject<void>();

  readonly form = this.fb.group({
    shedId: [this.data.currentShedId || ''],
    penId: [this.data.currentPenId || ''],
    transferDate: [new Date().toISOString().split('T')[0], Validators.required],
    reason: ['']
  });

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  readonly sheds = signal<ShedList[]>([]);
  readonly pens = signal<PenList[]>([]);
  readonly loadingSheds = signal(false);
  readonly loadingPens = signal(false);

  ngOnInit(): void {
    this.loadingSheds.set(true);
    this.shedSvc.getShedsByFarm(this.data.farmId).pipe(takeUntil(this.destroy$)).subscribe({
      next: (sheds) => {
        this.sheds.set(sheds);
        this.loadingSheds.set(false);
        if (this.data.currentShedId) {
          this.loadPensForTransfer(this.data.currentShedId);
        }
      },
      error: () => this.loadingSheds.set(false)
    });

    this.form.get('shedId')?.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(shedId => {
      this.form.get('penId')?.setValue('');
      if (shedId) {
        this.loadPensForTransfer(shedId);
      } else {
        this.pens.set([]);
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadPensForTransfer(shedId: string): void {
    this.loadingPens.set(true);
    this.penSvc.getPensByShed(shedId).pipe(takeUntil(this.destroy$)).subscribe({
      next: (pens) => {
        this.pens.set(pens);
        this.loadingPens.set(false);
      },
      error: () => this.loadingPens.set(false)
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const v = this.form.getRawValue();
    this.animalSvc.transfer(this.data.animalId, {
      toShedId: v.shedId || undefined,
      toPenId: v.penId || undefined,
      transferDate: v.transferDate!,
      reason: v.reason || undefined
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        this.submitting.set(false);
        if (err.error?.errors) {
            this.error.set(Object.values(err.error.errors).flat().join('\n'));
        } else {
            this.error.set(err.error?.detail || err.error?.title || 'Transfer failed.');
        }
      }
    });
  }
}
