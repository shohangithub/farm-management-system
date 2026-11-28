import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import { MoneyPipe } from '../../../../../shared/pipes/money.pipe';
import { PaidFromComponent, buildPaidFromGroup } from '../../../../finance/components/paid-from/paid-from.component';

export interface CompleteReturnDialogData {
  returnNumber: string;
  itemCount: number;
  totalAmountBdt: number;
}

/** Result: the money account a cash refund goes into (null = the farm's default account). */
export interface CompleteReturnDialogResult {
  moneyAccountId: string | null;
}

/**
 * Confirms completing a purchase return and asks which account a cash refund from the supplier goes
 * into. A return against an unpaid (credit) order reduces the bill instead and moves no money.
 */
@Component({
  selector: 'app-complete-return-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatIconModule, TranslatePipe, MoneyPipe, PaidFromComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden flex flex-col max-h-[90vh]">
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800">
        <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
          <mat-icon class="text-amber-500">assignment_return</mat-icon>{{ 'Complete Purchase Return' | t }}
        </h2>
      </div>
      <div class="p-6 space-y-4 overflow-y-auto">
        <p class="text-sm text-gray-700 dark:text-gray-300 m-0">
          {{ 'Return {ret}: {n} item(s) leave stock now; the supplier refunds {amount}.' | t: { ret: data.returnNumber, n: data.itemCount, amount: (data.totalAmountBdt | money:2) } }}
        </p>
        <app-paid-from [group]="refundTo" direction="in"></app-paid-from>
        <p class="text-[11px] text-gray-500 dark:text-gray-400 m-0">
          {{ 'If the order is still unpaid on credit, the refund is taken off the supplier bill instead and no money moves.' | t }}
        </p>
      </div>
      <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-2">
        <button type="button" mat-dialog-close class="px-4 py-2 text-sm font-semibold rounded-xl border border-gray-300 dark:border-gray-700 text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800">{{ 'Cancel' | t }}</button>
        <button type="button" (click)="confirm()" class="px-4 py-2 text-sm font-semibold rounded-xl text-white bg-primary-600 hover:bg-primary-700">{{ 'Complete Return' | t }}</button>
      </div>
    </div>
  `,
})
export class CompleteReturnDialogComponent {
  readonly data = inject<CompleteReturnDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<CompleteReturnDialogComponent, CompleteReturnDialogResult>);
  readonly refundTo = buildPaidFromGroup(inject(FormBuilder));

  confirm(): void {
    this.dialogRef.close({ moneyAccountId: this.refundTo.value.moneyAccountId ?? null });
  }
}
