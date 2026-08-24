import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { InventoryItem } from '../../models/inventory.models';

export interface StockWriteOffDialogData {
  item: InventoryItem;
}

@Component({
  selector: 'app-stock-write-off-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatDatepickerModule,
    MatNativeDateModule
  ],
  templateUrl: './stock-write-off-dialog.html',
  styleUrl: './stock-write-off-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StockWriteOffDialog implements OnInit {
  private dialogRef = inject(MatDialogRef<StockWriteOffDialog>);
  private data = inject<StockWriteOffDialogData>(MAT_DIALOG_DATA);
  private fb = inject(FormBuilder);
  private inventoryService = inject(InventoryService);
  private workingContextService = inject(WorkingContextService);

  readonly isSubmitting = signal(false);
  readonly error = signal<string | null>(null);

  form!: FormGroup;

  get item() { return this.data.item; }

  ngOnInit(): void {
    this.form = this.fb.group({
      quantity: [null, [Validators.required, Validators.min(0.01), Validators.max(this.item.currentStock)]],
      reason: ['', [Validators.required, Validators.maxLength(100)]],
      transactionDate: [new Date(), Validators.required],
      notes: ['', Validators.maxLength(500)]
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const farmId = this.workingContextService.currentFarmValue?.id;
    if (!farmId) return;

    this.isSubmitting.set(true);
    this.error.set(null);

    const val = this.form.value;
    
    // Sanitize payload per rule
    const payload = {
      farmId,
      inventoryItemId: this.item.id,
      quantity: val.quantity,
      reason: val.reason === '' ? null : val.reason,
      transactionDate: val.transactionDate.toISOString().split('T')[0],
      notes: val.notes === '' ? null : val.notes
    };

    this.inventoryService.recordStockWriteOff(payload).subscribe({
      next: (result) => this.dialogRef.close(result),
      error: (err: any) => {
        this.error.set(parseApiError(err));
        this.isSubmitting.set(false);
      }
    });
  }

  onCancel(): void {
    this.dialogRef.close();
  }
}
