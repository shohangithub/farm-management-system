import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { Investor, CreateInvestorRequest, UpdateInvestorRequest } from '../../models/finance.model';

@Component({
  selector: 'app-investor-form-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatIconModule
  ],
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-teal-600 dark:text-teal-400">
              {{ isEditMode ? 'edit' : 'person_add' }}
            </mat-icon>
            {{ isEditMode ? 'Edit Investor' : 'Register New Investor' }}
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">
            {{ isEditMode ? 'Update investor details and agreed profit share' : 'Add an equity investor and record their starting capital' }}
          </p>
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
          
          <!-- Investor Name -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Investor Full Name <span class="text-red-500">*</span>
            </label>
            <input type="text" formControlName="name" placeholder="e.g. Tariq Ahmed"
                   class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
          </div>

          <!-- Initial Investment & Date (Only for New Investor) -->
          <div *ngIf="!isEditMode" class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
                Initial Capital (BDT) <span class="text-red-500">*</span>
              </label>
              <input type="number" formControlName="initialInvestmentBdt" step="1000" min="0" placeholder="e.g. 500000"
                     class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
                Investment Date <span class="text-red-500">*</span>
              </label>
              <input type="date" formControlName="investmentDate"
                     class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
            </div>
          </div>

          <!-- Reference ID (For Initial Investment) -->
          <div *ngIf="!isEditMode" class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Receipt / Cheque / Ref No. (Optional)
            </label>
            <input type="text" formControlName="referenceId" placeholder="e.g. CHQ-98124 or BANK-TRF"
                   class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
          </div>

          <!-- Profit Share Ratio -->
          <div class="space-y-1.5">
            <div class="flex items-center justify-between">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
                Agreed Profit Share % (Optional)
              </label>
              <span class="text-[11px] text-teal-600 dark:text-teal-400 font-medium">Leave blank for dynamic capital share</span>
            </div>
            <input type="number" formControlName="agreedProfitSharePercentage" step="0.1" min="0" max="100" placeholder="e.g. 25.0 (Optional)"
                   class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
            <p class="text-[11px] text-gray-400 dark:text-gray-500 m-0">
              If left blank, profit/loss will be allocated automatically based on this investor's share of total farm capital pool.
            </p>
          </div>

          <!-- Contact Details -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
                Phone Number (Optional)
              </label>
              <input type="text" formControlName="phone" placeholder="e.g. +880 1711 000000"
                     class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
                Email Address (Optional)
              </label>
              <input type="email" formControlName="email" placeholder="e.g. investor@example.com"
                     class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
            </div>
          </div>

          <!-- National ID -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              National ID / NID / Passport (Optional)
            </label>
            <input type="text" formControlName="nationalId" placeholder="e.g. 198545892301"
                   class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
          </div>

          <!-- Notes -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Terms & Notes (Optional)
            </label>
            <textarea formControlName="notes" rows="2" placeholder="Partnership terms, contract notes, payout agreements..."
                      class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow"></textarea>
          </div>

        </div>

        <!-- Footer Actions -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-end gap-3 shrink-0">
          <button mat-dialog-close type="button"
                  class="px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-xl text-sm font-semibold text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors">
            Cancel
          </button>
          
          <button type="submit" [disabled]="form.invalid || isLoading()"
                  class="px-5 py-2 rounded-xl text-sm font-semibold text-white bg-gradient-to-r from-teal-500 to-emerald-600 hover:from-teal-600 hover:to-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed transition-all shadow-sm shadow-teal-500/20 flex items-center gap-2">
            <mat-icon *ngIf="isLoading()" class="!text-[18px] !w-[18px] !h-[18px] animate-spin">sync</mat-icon>
            <span>{{ isEditMode ? 'Save Changes' : 'Register Investor' }}</span>
          </button>
        </div>

      </form>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InvestorFormDialogComponent implements OnInit {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<InvestorFormDialogComponent>);
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);
  readonly data: Investor | null = inject(MAT_DIALOG_DATA, { optional: true });

  readonly isEditMode = !!this.data;
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);

  form!: FormGroup;

  ngOnInit(): void {
    const today = new Date().toISOString().split('T')[0];

    this.form = this.fb.group({
      name: [this.data?.name || '', [Validators.required, Validators.maxLength(200)]],
      initialInvestmentBdt: [{ value: 0, disabled: this.isEditMode }, [Validators.required, Validators.min(0)]],
      investmentDate: [{ value: this.data ? this.data.investmentDate.split('T')[0] : today, disabled: this.isEditMode }, [Validators.required]],
      agreedProfitSharePercentage: [this.data?.agreedProfitSharePercentage ?? null, [Validators.min(0), Validators.max(100)]],
      referenceId: [''],
      phone: [this.data?.phone || '', [Validators.maxLength(50)]],
      email: [this.data?.email || '', [Validators.email, Validators.maxLength(150)]],
      nationalId: [this.data?.nationalId || '', [Validators.maxLength(50)]],
      notes: [this.data?.notes || '', [Validators.maxLength(1000)]]
    });
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    const farmId = this.workingContextService.currentFarmValue?.id;
    if (!farmId) {
      this.error.set('No active farm selected in context.');
      return;
    }

    this.isLoading.set(true);
    this.error.set(null);

    const raw = this.form.getRawValue();

    if (this.isEditMode && this.data) {
      const updateReq: UpdateInvestorRequest = {
        name: raw.name,
        agreedProfitSharePercentage: raw.agreedProfitSharePercentage ?? null,
        email: raw.email || null,
        phone: raw.phone || null,
        nationalId: raw.nationalId || null,
        notes: raw.notes || null
      };

      this.financeService.updateInvestor(farmId, this.data.id, updateReq).subscribe({
        next: (investor) => {
          this.isLoading.set(false);
          this.dialogRef.close(investor);
        },
        error: (err) => {
          this.isLoading.set(false);
          this.error.set(parseApiError(err));
        }
      });
    } else {
      const createReq: CreateInvestorRequest = {
        name: raw.name,
        initialInvestmentBdt: Number(raw.initialInvestmentBdt) || 0,
        investmentDate: new Date(raw.investmentDate).toISOString(),
        agreedProfitSharePercentage: raw.agreedProfitSharePercentage ?? null,
        email: raw.email || null,
        phone: raw.phone || null,
        nationalId: raw.nationalId || null,
        notes: raw.notes || null,
        referenceId: raw.referenceId || null
      };

      this.financeService.createInvestor(farmId, createReq).subscribe({
        next: (investor) => {
          this.isLoading.set(false);
          this.dialogRef.close(investor);
        },
        error: (err) => {
          this.isLoading.set(false);
          this.error.set(parseApiError(err));
        }
      });
    }
  }
}
