import { Component, inject, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { AnimalService } from '../../services/animal.service';
import { CreditTermsFieldsComponent, buildCreditTermsGroup, toCreditTerms } from '../../../finance/components/credit-terms-fields/credit-terms-fields.component';

export interface RecordSaleDialogData {
  animalId: string;
  animalTag: string;
  latestWeightKg?: number | null;
}

@Component({
  selector: 'app-record-sale-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule, CreditTermsFieldsComponent],
  templateUrl: './record-sale-dialog.component.html'
})
export class RecordSaleDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<RecordSaleDialogComponent>);
  public readonly data = inject<RecordSaleDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly animalSvc = inject(AnimalService);
  private readonly destroy$ = new Subject<void>();

  readonly form = this.fb.group({
    price: [null as number | null, [Validators.required, Validators.min(0.01)]],
    date: [new Date().toISOString().split('T')[0], Validators.required],
    buyer: ['', Validators.maxLength(100)],
    weight: [this.data.latestWeightKg || null, Validators.min(0)]
  });

  /** Paid in full now, or on credit (the buyer pays later). The buyer name is used if no customer is given. */
  readonly credit = buildCreditTermsGroup(this.fb, false);

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
    const credit = toCreditTerms(this.credit.getRawValue());
    if (credit && !credit.partyName && !v.buyer) {
      this.submitting.set(false);
      this.error.set('Enter the buyer name for a sale on credit.');
      return;
    }

    this.animalSvc.sell(this.data.animalId, {
      salePriceBdt: v.price!,
      saleDate: v.date!,
      buyerName: v.buyer || undefined,
      saleWeightKg: v.weight || undefined,
      credit
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        this.submitting.set(false);
        if (err.error?.errors) {
            this.error.set(Object.values(err.error.errors).flat().join('\n'));
        } else {
            this.error.set(err.error?.detail || err.error?.title || 'Recording sale failed.');
        }
      }
    });
  }
}
