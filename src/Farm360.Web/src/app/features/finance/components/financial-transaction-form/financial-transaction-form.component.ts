import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-financial-transaction-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatIconModule],
  templateUrl: './financial-transaction-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    .custom-scrollbar::-webkit-scrollbar { width: 6px; }
    .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
    .custom-scrollbar::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.5); border-radius: 20px; }
    .custom-scrollbar:hover::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.8); }
  `]
})
export class FinancialTransactionFormComponent {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<FinancialTransactionFormComponent>);
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);

  readonly isLoading = signal(false);
  readonly error = signal('');

  readonly form = this.fb.group({
    type: ['Income', Validators.required],
    category: ['LivestockSale', Validators.required],
    amountBdt: [0, [Validators.required, Validators.min(0.01)]],
    transactionDate: [new Date().toISOString().substring(0, 10), Validators.required],
    referenceId: [''],
    notes: ['']
  });

  readonly categories = [
    { value: 'LivestockSale', label: 'Livestock Sale' },
    { value: 'InventoryPurchase', label: 'Inventory Purchase' },
    { value: 'VeterinaryService', label: 'Veterinary Service' },
    { value: 'UtilityBill', label: 'Utility Bill' },
    { value: 'Other', label: 'Other' }
  ];

  onSubmit(): void {
    if (this.form.invalid) return;

    const farmId = this.workingContextService.currentFarmValue?.id;
    if (!farmId) {
      this.error.set('No active farm selected.');
      return;
    }

    this.isLoading.set(true);
    this.error.set('');

    const value = this.form.value;
    
    // Explicit null mapping for referenceId/notes handled in the service payload, but we pass raw strings
    this.financeService.createTransaction(farmId, {
      type: value.type as string,
      category: value.category as string,
      amountBdt: Number(value.amountBdt),
      transactionDate: new Date(value.transactionDate as string).toISOString(),
      referenceId: value.referenceId || '',
      notes: value.notes || ''
    })
    .pipe(finalize(() => this.isLoading.set(false)))
    .subscribe({
      next: (result) => this.dialogRef.close(result),
      error: (err) => this.error.set(parseApiError(err))
    });
  }
}
