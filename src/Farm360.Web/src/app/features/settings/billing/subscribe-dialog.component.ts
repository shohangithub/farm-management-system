import { Component, Inject, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { SubscriptionPlan, SubscribeRequest } from '../../../core/models/subscription.model';
import { SubscriptionService } from '../../../core/services/subscription.service';
import { parseApiError } from '../../../core/utils/error-parser';

export interface SubscribeDialogData {
  plan: SubscriptionPlan;
  initialCycle: 'Monthly' | 'Yearly' | 'OneTime';
}

@Component({
  selector: 'app-subscribe-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    .custom-scrollbar::-webkit-scrollbar { width: 6px; }
    .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
    .custom-scrollbar::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.4); border-radius: 20px; }
    .custom-scrollbar:hover::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.7); }
  `],
  template: `
    <div class="bg-white dark:bg-gray-800 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[92vh] w-[95vw] max-w-[560px]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-700 bg-gray-50/70 dark:bg-gray-800/60 flex items-center justify-between shrink-0">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20">
            <mat-icon class="!text-[22px] !w-[22px] !h-[22px]">workspace_premium</mat-icon>
          </div>
          <div>
            <h2 class="text-base font-bold text-gray-900 dark:text-white flex items-center gap-1.5 m-0">
              Subscribe to {{ data.plan.name }}
            </h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Select your billing cycle and preferred payment method</p>
          </div>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Form & Scrollable Body -->
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden flex-1">
        
        <!-- Error Banner -->
        <div *ngIf="error()" class="mx-6 mt-4 p-3.5 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-xl text-xs whitespace-pre-wrap flex items-start gap-2.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
          <span>{{ error() }}</span>
        </div>

        <div class="p-6 space-y-5 overflow-y-auto custom-scrollbar flex-1">
          
          <!-- 1. Billing Frequency Choice -->
          <div>
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400 mb-2">
              Billing Frequency <span class="text-red-500">*</span>
            </label>
            <div class="grid grid-cols-3 gap-2.5">
              <!-- Monthly -->
              <button type="button" (click)="setCycle('Monthly')"
                [class.border-emerald-500]="selectedCycle() === 'Monthly'"
                [class.bg-emerald-50]="selectedCycle() === 'Monthly'"
                [class.dark:bg-emerald-950/30]="selectedCycle() === 'Monthly'"
                [class.text-emerald-700]="selectedCycle() === 'Monthly'"
                [class.dark:text-emerald-300]="selectedCycle() === 'Monthly'"
                class="p-3 border rounded-xl text-left transition-all border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600 flex flex-col justify-between">
                <div>
                  <span class="text-xs font-semibold block">Monthly</span>
                  <span class="text-sm font-bold block mt-1">৳{{ data.plan.monthlyPrice | number }}</span>
                </div>
                <span class="text-[10px] text-gray-500 dark:text-gray-400 mt-1 block">Billed monthly</span>
              </button>

              <!-- Yearly -->
              <button type="button" (click)="setCycle('Yearly')"
                [class.border-emerald-500]="selectedCycle() === 'Yearly'"
                [class.bg-emerald-50]="selectedCycle() === 'Yearly'"
                [class.dark:bg-emerald-950/30]="selectedCycle() === 'Yearly'"
                [class.text-emerald-700]="selectedCycle() === 'Yearly'"
                [class.dark:text-emerald-300]="selectedCycle() === 'Yearly'"
                class="p-3 border rounded-xl text-left transition-all border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600 flex flex-col justify-between relative overflow-hidden">
                <span class="absolute top-1 right-1 text-[9px] font-bold px-1.5 py-0.5 bg-emerald-500 text-white rounded-full">Save ~17%</span>
                <div>
                  <span class="text-xs font-semibold block">Yearly</span>
                  <span class="text-sm font-bold block mt-1">৳{{ data.plan.yearlyPrice | number }}</span>
                </div>
                <span class="text-[10px] text-gray-500 dark:text-gray-400 mt-1 block">Billed annually</span>
              </button>

              <!-- One-Time / Lifetime -->
              <button type="button" (click)="setCycle('OneTime')"
                [class.border-emerald-500]="selectedCycle() === 'OneTime'"
                [class.bg-emerald-50]="selectedCycle() === 'OneTime'"
                [class.dark:bg-emerald-950/30]="selectedCycle() === 'OneTime'"
                [class.text-emerald-700]="selectedCycle() === 'OneTime'"
                [class.dark:text-emerald-300]="selectedCycle() === 'OneTime'"
                class="p-3 border rounded-xl text-left transition-all border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600 flex flex-col justify-between relative overflow-hidden">
                <span class="absolute top-1 right-1 text-[9px] font-bold px-1.5 py-0.5 bg-amber-500 text-white rounded-full">Lifetime</span>
                <div>
                  <span class="text-xs font-semibold block">One-Time</span>
                  <span class="text-sm font-bold block mt-1">৳{{ data.plan.oneTimePrice | number }}</span>
                </div>
                <span class="text-[10px] text-gray-500 dark:text-gray-400 mt-1 block">Never expires</span>
              </button>
            </div>
          </div>

          <!-- Total Bill Summary Card -->
          <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-900/50 border border-gray-100 dark:border-gray-800 flex items-center justify-between">
            <div>
              <span class="text-xs text-gray-500 dark:text-gray-400 block">Total Amount Due</span>
              <span class="text-xl font-extrabold text-gray-900 dark:text-white">৳{{ currentPrice() | number }} <span class="text-xs font-medium text-gray-400">BDT</span></span>
            </div>
            <div class="text-right">
              <span class="text-xs font-semibold text-emerald-600 dark:text-emerald-400 block">Access: {{ data.plan.name }}</span>
              <span class="text-[11px] text-gray-500">{{ selectedCycle() }} cycle</span>
            </div>
          </div>

          <!-- 2. Payment Method Selector -->
          <div>
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400 mb-2">
              Payment Method <span class="text-red-500">*</span>
            </label>
            <div class="grid grid-cols-2 sm:grid-cols-4 gap-2">
              <button type="button" *ngFor="let m of paymentMethods"
                (click)="setMethod(m.id)"
                [class.border-primary-500]="selectedMethod() === m.id"
                [class.bg-primary-50]="selectedMethod() === m.id"
                [class.dark:bg-primary-950/30]="selectedMethod() === m.id"
                [class.text-primary-700]="selectedMethod() === m.id"
                [class.dark:text-primary-300]="selectedMethod() === m.id"
                class="p-2.5 border rounded-xl text-center text-xs font-semibold transition-all border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600 flex flex-col items-center gap-1.5">
                <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-gray-600 dark:text-gray-400">{{ m.icon }}</mat-icon>
                <span>{{ m.label }}</span>
              </button>
            </div>
          </div>

          <!-- Payment Instructions based on method -->
          <div class="p-3.5 rounded-xl bg-blue-50/70 dark:bg-blue-950/30 border border-blue-100 dark:border-blue-900/40 text-xs text-blue-800 dark:text-blue-300 flex items-start gap-2.5">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-blue-600 dark:text-blue-400 mt-0.5 shrink-0">info</mat-icon>
            <div>
              <p class="font-semibold mb-1" *ngIf="selectedMethod() === 'bKash'">bKash Merchant Payment</p>
              <p class="font-semibold mb-1" *ngIf="selectedMethod() === 'Nagad'">Nagad Payment</p>
              <p class="font-semibold mb-1" *ngIf="selectedMethod() === 'Card'">Debit / Credit Card</p>
              <p class="font-semibold mb-1" *ngIf="selectedMethod() === 'Bank'">Bank Transfer</p>

              <p class="text-[11px] leading-relaxed text-blue-700 dark:text-blue-400" *ngIf="selectedMethod() === 'bKash' || selectedMethod() === 'Nagad'">
                Send <strong>৳{{ currentPrice() | number }}</strong> via Make Payment to <strong>01700-FARM360</strong>. Then enter the 8-10 character Transaction ID (TrxID) below to instantly activate your plan.
              </p>
              <p class="text-[11px] leading-relaxed text-blue-700 dark:text-blue-400" *ngIf="selectedMethod() === 'Card'">
                Instant card payment verification via SSLCommerz / VISA / Mastercard. Enter transaction approval code or reference below.
              </p>
              <p class="text-[11px] leading-relaxed text-blue-700 dark:text-blue-400" *ngIf="selectedMethod() === 'Bank'">
                Deposit to: Eastern Bank Ltd, A/C: 1020304050, Title: Farm360 Agro Ltd. Enter the deposit slip reference below.
              </p>
            </div>
          </div>

          <!-- 3. Transaction ID / Reference Input -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">
              Transaction ID / Payment Reference <span class="text-red-500">*</span>
            </label>
            <input type="text" formControlName="paymentReference" placeholder="e.g. BL90XYZ123 or TRX-10029"
              class="block w-full px-3.5 py-2.5 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-900 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-shadow">
            <p class="text-[11px] text-gray-400">Your plan will be activated immediately upon submission.</p>
          </div>

        </div>

        <!-- Footer Actions -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/70 flex justify-end gap-3 shrink-0">
          <button type="button" mat-dialog-close [disabled]="isLoading()"
            class="px-4 py-2 text-xs font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-xl hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors shadow-sm">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || isLoading()"
            class="px-5 py-2 text-xs font-semibold text-white bg-gradient-to-r from-emerald-600 to-teal-600 rounded-xl hover:from-emerald-700 hover:to-teal-700 transition-all shadow-md shadow-emerald-500/20 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
            <mat-icon *ngIf="isLoading()" class="animate-spin !w-[16px] !h-[16px] !text-[16px]">autorenew</mat-icon>
            <span>{{ isLoading() ? 'Processing...' : 'Confirm & Activate Plan' }}</span>
          </button>
        </div>
      </form>
    </div>
  `
})
export class SubscribeDialogComponent {
  private fb = inject(FormBuilder);
  private subscriptionService = inject(SubscriptionService);
  private dialogRef = inject(MatDialogRef<SubscribeDialogComponent>);

  constructor(@Inject(MAT_DIALOG_DATA) public data: SubscribeDialogData) {
    this.selectedCycle.set(data.initialCycle || 'Monthly');
  }

  selectedCycle = signal<'Monthly' | 'Yearly' | 'OneTime'>('Monthly');
  selectedMethod = signal<string>('bKash');
  isLoading = signal<boolean>(false);
  error = signal<string | null>(null);

  paymentMethods = [
    { id: 'bKash', label: 'bKash', icon: 'account_balance_wallet' },
    { id: 'Nagad', label: 'Nagad', icon: 'payments' },
    { id: 'Card', label: 'Card (Visa/MC)', icon: 'credit_card' },
    { id: 'Bank', label: 'Bank Transfer', icon: 'account_balance' }
  ];

  form = this.fb.group({
    paymentReference: ['', [Validators.required, Validators.minLength(4)]],
    notes: ['']
  });

  setCycle(cycle: 'Monthly' | 'Yearly' | 'OneTime') {
    this.selectedCycle.set(cycle);
  }

  setMethod(method: string) {
    this.selectedMethod.set(method);
  }

  currentPrice(): number {
    switch (this.selectedCycle()) {
      case 'Yearly': return this.data.plan.yearlyPrice;
      case 'OneTime': return this.data.plan.oneTimePrice;
      default: return this.data.plan.monthlyPrice;
    }
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.error.set(null);

    const tierMap: Record<string, number> = {
      'Starter': 1,
      'Standard': 2,
      'Professional': 3,
      'Enterprise': 4
    };

    const cycleMap: Record<string, number> = {
      'Monthly': 2,
      'Yearly': 3,
      'OneTime': 4
    };

    const payload: SubscribeRequest = {
      tier: tierMap[this.data.plan.tier] ?? 2,
      billingCycle: cycleMap[this.selectedCycle()] ?? 2,
      paymentMethod: this.selectedMethod(),
      paymentReference: this.form.value.paymentReference?.trim() || undefined,
      notes: `Subscription to ${this.data.plan.name} (${this.selectedCycle()})`
    };

    this.subscriptionService.subscribe(payload).subscribe({
      next: (updatedStatus) => {
        this.isLoading.set(false);
        this.dialogRef.close(updatedStatus);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.error.set(parseApiError(err, 'Failed to activate subscription. Please check your reference and try again.'));
      }
    });
  }
}
