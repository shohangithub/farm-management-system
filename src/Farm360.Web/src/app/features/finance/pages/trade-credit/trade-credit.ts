import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, catchError, combineLatest, of, switchMap, tap } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { FinanceService } from '../../services/finance.service';
import { TradeBill, TradeBillKind, TradeBillPayment, TradeCreditOverview, TradeCreditSide, categoryLabel } from '../../models/finance.model';
import { TradeBillDialogComponent, TradeBillDialogMode } from '../../components/trade-bill-dialog/trade-bill-dialog.component';
import { ConfirmService } from '../../../../shared/services/confirm.service';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { formatMoney } from '../../../../shared/pipes/money.pipe';
import { DocumentService } from '../../../../core/services/document.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

/**
 * Payables & Receivables — the credit book. Who owes the farm (customers), whom the farm owes
 * (suppliers), how overdue it is, and the actions that settle it. Every payment and write-off is
 * posted to the ledger, and the totals are proven against Accounts Payable / Receivable.
 */
@Component({
  selector: 'app-trade-credit',
  standalone: true,
  imports: [TranslatePipe, MoneyPipe, CommonModule, FormsModule, RouterLink, MatIconModule, MatButtonModule, PageHeaderComponent, DecimalPipe, DatePipe],
  templateUrl: './trade-credit.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TradeCreditComponent {
  private readonly confirmService = inject(ConfirmService);
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);
  private readonly documents = inject(DocumentService);
  private readonly i18n = inject(I18nService);

  readonly overview = signal<TradeCreditOverview | null>(null);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  /** The payment just recorded, so the notice can offer its receipt straight away. */
  readonly lastPaymentId = signal<string | null>(null);
  /** Document being fetched (payment or bill id), to show a spinner on its button. */
  readonly printing = signal<string | null>(null);

  readonly tab = signal<TradeBillKind>('Receivable');
  readonly includeSettled = signal(false);
  readonly search = signal('');
  readonly party = signal<string | null>(null);
  readonly overdueOnly = signal(false);
  readonly expanded = signal<string | null>(null);

  private readonly refresh$ = new BehaviorSubject<void>(undefined);

  readonly side = computed<TradeCreditSide | null>(() => {
    const o = this.overview();
    if (!o) return null;
    return this.tab() === 'Payable' ? o.payables : o.receivables;
  });

  readonly bills = computed<TradeBill[]>(() => {
    const term = this.search().trim().toLowerCase();
    const party = this.party()?.toLowerCase() ?? null;
    return (this.overview()?.bills ?? [])
      .filter(b => b.kind === this.tab())
      .filter(b => !party || b.partyName.toLowerCase() === party)
      .filter(b => !this.overdueOnly() || b.daysOverdue > 0)
      .filter(b => !term
        || b.partyName.toLowerCase().includes(term)
        || b.referenceNo.toLowerCase().includes(term)
        || b.description.toLowerCase().includes(term)
        || (b.partyPhone ?? '').includes(term));
  });

  readonly agingMax = computed(() => Math.max(1, ...(this.side()?.aging ?? []).map(a => a.amountBdt)));

  constructor() {
    combineLatest([this.refresh$, this.context.currentFarm$])
      .pipe(
        tap(() => {
          this.isLoading.set(true);
          this.error.set(null);
        }),
        switchMap(([, farm]) => {
          if (!farm) return of(null);
          return this.finance.getTradeCredit(farm.id, this.includeSettled()).pipe(
            catchError(err => {
              this.error.set(parseApiError(err, this.i18n.t('Failed to load payables and receivables.')));
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => {
        this.overview.set(result);
        this.isLoading.set(false);
      });
  }

  reload(): void {
    this.refresh$.next();
  }

  setTab(kind: TradeBillKind): void {
    this.tab.set(kind);
    this.party.set(null);
    this.expanded.set(null);
  }

  toggleSettled(value: boolean): void {
    this.includeSettled.set(value);
    this.reload();
  }

  toggle(bill: TradeBill): void {
    this.expanded.set(this.expanded() === bill.id ? null : bill.id);
  }

  filterParty(name: string | null): void {
    this.party.set(this.party() === name ? null : name);
  }

  open(mode: TradeBillDialogMode, bill: TradeBill): void {
    this.dialog
      .open(TradeBillDialogComponent, { width: '560px', maxWidth: '95vw', data: { mode, bill } })
      .afterClosed()
      .subscribe(result => {
        if (!result) return;
        this.lastPaymentId.set(mode === 'payment' ? this.newPaymentId(bill, result) : null);
        this.notice.set(
          this.i18n.t(mode === 'payment' ? (bill.kind === 'Payable' ? 'Payment recorded and posted to the cash book.' : 'Collection recorded and posted to the cash book.')
          : mode === 'write-off' ? 'Balance written off and posted to the ledger.'
          : 'Credit terms updated.'));
        this.reload();
      });
  }

  /** The payment the dialog just recorded: the one on the returned bill that was not there before. */
  private newPaymentId(before: TradeBill, result: unknown): string | null {
    const after = (result as Partial<TradeBill> | null)?.payments;
    if (!Array.isArray(after)) return null;
    const known = new Set(before.payments.map(p => p.id));
    return after.find(p => !known.has(p.id))?.id ?? null;
  }

  /** Money receipt (collection) or payment voucher (supplier payment) as a PDF in a new tab. */
  async printReceipt(paymentId: string): Promise<void> {
    this.printing.set(paymentId);
    try {
      await this.documents.openPaymentReceipt(paymentId);
    } finally {
      this.printing.set(null);
    }
  }

  /** A receivable raised for an animal sale points at the animal, so its invoice can be printed. */
  isAnimalSale(bill: TradeBill): boolean {
    return bill.kind === 'Receivable' && bill.category === 'AnimalSale' && !!bill.sourceDocumentId;
  }

  async printSaleInvoice(bill: TradeBill): Promise<void> {
    this.printing.set(bill.id);
    try {
      await this.documents.openSaleInvoice(bill.sourceDocumentId);
    } finally {
      this.printing.set(null);
    }
  }

  async voidPayment(bill: TradeBill, payment: TradeBillPayment): Promise<void> {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) return;
    if (!(await this.confirmService.confirmAsync({ message: this.i18n.t(bill.kind === 'Payable' ? 'Void the {amount} payment to {party} on {date}?\n\nIts cash-book entry is removed and the bill balance goes back up.' : 'Void the {amount} collection from {party} on {date}?\n\nIts cash-book entry is removed and the bill balance goes back up.', { amount: formatMoney(payment.amountBdt), party: bill.partyName, date: payment.paymentDate }), confirmText: this.i18n.t('Void'), destructive: true }))) {
      return;
    }
    this.finance.voidBillPayment(farmId, bill.id, payment.id).subscribe({
      next: () => {
        this.lastPaymentId.set(null);
        this.notice.set(this.i18n.t('Payment voided.'));
        this.reload();
      },
      error: err => this.error.set(parseApiError(err, this.i18n.t('Could not void the payment.'))),
    });
  }

  async cancel(bill: TradeBill): Promise<void> {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) return;
    if (!(await this.confirmService.confirmAsync({ message: this.i18n.t(bill.kind === 'Payable' ? 'Cancel the credit bill for {party}?\n\nThe purchase stays in the books, but as paid in cash on {date}.' : 'Cancel the credit bill for {party}?\n\nThe sale stays in the books, but as received in cash on {date}.', { party: bill.partyName, date: bill.billDate }), confirmText: this.i18n.t('Cancel bill'), cancelText: this.i18n.t('Keep'), destructive: true }))) {
      return;
    }
    this.finance.cancelTradeBill(farmId, bill.id).subscribe({
      next: () => {
        this.lastPaymentId.set(null);
        this.notice.set(this.i18n.t('Bill cancelled — the deal is now treated as settled in cash.'));
        this.reload();
      },
      error: err => this.error.set(parseApiError(err, this.i18n.t('Could not cancel the bill.'))),
    });
  }

  statusClass(bill: TradeBill): string {
    if (bill.daysOverdue > 0) return 'bg-rose-100 dark:bg-rose-900/40 text-rose-700 dark:text-rose-300';
    switch (bill.status) {
      case 'Paid': return 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300';
      case 'PartiallyPaid': return 'bg-sky-100 dark:bg-sky-900/40 text-sky-700 dark:text-sky-300';
      case 'Overpaid': return 'bg-violet-100 dark:bg-violet-900/40 text-violet-700 dark:text-violet-300';
      default: return 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300';
    }
  }

  statusLabel(bill: TradeBill): string {
    if (bill.daysOverdue > 0) return this.i18n.t('{days} day(s) overdue', { days: bill.daysOverdue });
    switch (bill.status) {
      case 'PartiallyPaid': return this.i18n.t('Part paid');
      case 'Overpaid': return this.i18n.t(bill.kind === 'Payable' ? 'Advance to recover' : 'Customer credit');
      default: return this.i18n.t(bill.status);
    }
  }

  /** Bangla shows the head's Bangla name; English keeps the name the server sent. */
  categoryName(bill: TradeBill): string {
    return this.i18n.isBangla ? categoryLabel(bill.category) : bill.categoryName;
  }

  agingClass(key: string): string {
    return ({
      current: 'bg-emerald-500',
      '1-30': 'bg-amber-400',
      '31-60': 'bg-orange-500',
      '61-90': 'bg-rose-500',
      '90+': 'bg-rose-700',
    } as Record<string, string>)[key] ?? 'bg-gray-400';
  }
}
