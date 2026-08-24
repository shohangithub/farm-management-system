import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { AnimalPickerComponent } from '../../../../shared/components/animal-picker/animal-picker.component';

@Component({
  selector: 'app-expense-form-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatIconModule,
    AnimalPickerComponent
  ],
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-rose-500">arrow_upward</mat-icon>
            Record Expense
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Log a cost to the farm ledger</p>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Form -->
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden">
        
        <!-- Error State -->
        <div *ngIf="error()" class="mx-6 mt-4 p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-lg text-sm whitespace-pre-wrap flex items-start gap-2">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
          <span>{{ error() }}</span>
        </div>

        <!-- Scrollable Body -->
        <div class="p-6 space-y-4 overflow-y-auto custom-scrollbar flex-1">
          
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Category <span class="text-red-500">*</span>
            </label>
            <select formControlName="category" class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
              <option value="AnimalPurchase">Animal Purchase</option>
              <option value="FeedCost">Feed Cost</option>
              <option value="VeterinaryCost">Veterinary Cost</option>
              <option value="MedicineCost">Medicine Cost</option>
              <option value="LaborCost">Labor Cost</option>
              <option value="Utilities">Utilities</option>
              <option value="Transport">Transport</option>
              <option value="InventoryPurchase">Inventory Purchase</option>
              <option value="MiscellaneousExpense">Miscellaneous</option>
            </select>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Amount (BDT) <span class="text-red-500">*</span>
            </label>
            <input type="number" formControlName="amountBdt" step="0.01" min="0" placeholder="e.g. 1500"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Transaction Date <span class="text-red-500">*</span>
            </label>
            <input type="date" formControlName="transactionDate"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Description <span class="text-red-500">*</span>
            </label>
            <input type="text" formControlName="description" placeholder="Brief description of the expense"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Link to Animal (Optional)
            </label>
            <app-animal-picker formControlName="animalId"></app-animal-picker>
            <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">Costs linked to an animal accumulate in its ledger.</p>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Notes / Reference (Optional)
            </label>
            <textarea formControlName="notes" rows="2" placeholder="Invoice numbers, supplier info..."
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow"></textarea>
          </div>

        </div>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
          <button type="button" mat-dialog-close [disabled]="isLoading()"
            class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || isLoading()"
                  class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
            <mat-icon *ngIf="isLoading()" class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon>
            <span>{{ isLoading() ? 'Saving...' : 'Save Expense' }}</span>
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .custom-scrollbar::-webkit-scrollbar { width: 6px; }
    .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
    .custom-scrollbar::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.5); border-radius: 20px; }
    .custom-scrollbar:hover::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.8); }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExpenseFormDialogComponent implements OnInit {
  private dialogRef = inject(MatDialogRef<ExpenseFormDialogComponent>);
  private fb = inject(FormBuilder);
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);

  form!: FormGroup;
  isLoading = signal(false);
  error = signal('');

  ngOnInit(): void {
    const today = new Date().toISOString().split('T')[0];
    
    this.form = this.fb.group({
      category: ['FeedCost', Validators.required],
      amountBdt: [null, [Validators.required, Validators.min(0.01)]],
      transactionDate: [today, Validators.required],
      description: ['', Validators.required],
      animalId: [null],
      notes: ['']
    });
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    const farmId = this.workingContextService.currentFarmValue?.id;
    if (!farmId) {
      this.error.set('No active farm selected.');
      return;
    }

    this.isLoading.set(true);
    this.error.set('');

    const payload = this.form.value;
    
    this.financeService.recordExpense(farmId, payload).subscribe({
      next: (result) => {
        this.isLoading.set(false);
        this.dialogRef.close(result);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }
}
