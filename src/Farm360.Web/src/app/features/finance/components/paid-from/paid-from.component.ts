import { ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, Input, OnChanges, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FinanceService } from '../../services/finance.service';
import { Investor, MoneyAccount, PaidFromMode, PaidFromPayload, moneyAccountName } from '../../models/finance.model';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { CreditTermsFieldsComponent, buildCreditTermsGroup, toCreditTerms } from '../credit-terms-fields/credit-terms-fields.component';
import { TransferDialogComponent } from '../transfer-dialog/transfer-dialog.component';
import { InvestorTransactionDialogComponent } from '../investor-transaction-dialog/investor-transaction-dialog';

/**
 * The form group behind {@link PaidFromComponent}.
 * @param requireParty false when the server can fill in the credit party (the PO's supplier, the sale's buyer).
 */
export function buildPaidFromGroup(fb: FormBuilder, requireParty = true): FormGroup {
  const credit = buildCreditTermsGroup(fb, requireParty);
  const group = fb.group({
    mode: ['account' as PaidFromMode],
    moneyAccountId: [null as string | null],
    partnerId: [null as string | null],
    credit,
  });
  group.get('mode')!.valueChanges.subscribe(mode => {
    credit.get('onCredit')!.setValue(mode === 'credit');
    const partner = group.get('partnerId')!;
    partner.setValidators(mode === 'partner' ? [Validators.required] : []);
    partner.updateValueAndValidity({ emitEvent: false });
  });
  return group;
}

/** The request fields for the chosen source: an account, credit terms, or the partner who paid. */
export function toPaidFrom(value: Record<string, any> | null | undefined): PaidFromPayload {
  const mode = (value?.['mode'] ?? 'account') as PaidFromMode;
  const accountId = (value?.['moneyAccountId'] as string | null) || null;
  if (mode === 'credit') {
    const credit = toCreditTerms({ ...(value?.['credit'] ?? {}), onCredit: true });
    return { moneyAccountId: accountId, paidByPartnerId: null, credit: credit ? { ...credit, moneyAccountId: accountId } : null };
  }
  if (mode === 'partner') {
    return { moneyAccountId: null, paidByPartnerId: (value?.['partnerId'] as string | null) || null, credit: null };
  }
  return { moneyAccountId: accountId, paidByPartnerId: null, credit: null };
}

/**
 * "Paid from" — one selector on every money-moving form:
 * a money account (cash in hand, bank, wallet), OR on credit (বাকি) where the deal allows it,
 * OR paid directly by a partner (books that partner's capital and the spend; cash nets to zero).
 * Money-in forms (direction "in") only ask which account the money goes into.
 * When a spend is more than the chosen account holds it warns — it never blocks — and offers
 * "Add partner money" and "Transfer between accounts".
 */
@Component({
  selector: 'app-paid-from',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatIconModule, MatTooltipModule, MoneyPipe, TranslatePipe, CreditTermsFieldsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (available()) {
      <div [formGroup]="group" class="space-y-3 rounded-xl border border-gray-200 dark:border-gray-700 p-3 bg-gray-50/60 dark:bg-gray-800/40">
        <div class="flex items-center justify-between gap-2">
          <label class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400 flex items-center gap-1">
            <mat-icon class="!w-4 !h-4 !text-[16px]">account_balance_wallet</mat-icon>
            {{ (direction === 'in' ? 'Money goes into' : 'Paid from') | t }}
          </label>
        </div>

        @if (direction === 'out' && modes().length > 1) {
          <div class="grid gap-1 p-1 rounded-xl bg-gray-100 dark:bg-gray-900 text-xs font-bold" [ngClass]="modes().length === 3 ? 'grid-cols-3' : 'grid-cols-2'">
            @for (m of modes(); track m) {
              <button type="button" (click)="setMode(m)"
                class="py-2 px-1 rounded-lg flex items-center justify-center gap-1 transition-colors"
                [ngClass]="mode() === m ? 'bg-white dark:bg-gray-700 text-primary-700 dark:text-primary-200 shadow-sm' : 'text-gray-500 dark:text-gray-400'">
                <mat-icon class="!w-4 !h-4 !text-[16px]">{{ m === 'account' ? 'account_balance_wallet' : m === 'credit' ? 'schedule' : 'handshake' }}</mat-icon>
                <span class="truncate">{{ (m === 'account' ? 'Money account' : m === 'credit' ? 'On credit (বাকি)' : 'Paid by partner') | t }}</span>
              </button>
            }
          </div>
        }

        @if (mode() === 'account' || (mode() === 'credit' && paidNow() > 0)) {
          <div class="space-y-1">
            @if (mode() === 'credit') {
              <span class="block text-[11px] font-semibold text-gray-500">{{ 'Advance paid from' | t }}</span>
            }
            <select formControlName="moneyAccountId"
              class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
              @for (a of accounts(); track a.id) {
                <option [ngValue]="a.id">{{ name(a.name) }} — {{ a.balanceBdt | money }}</option>
              }
            </select>
          </div>
        }

        @if (shortBy(); as s) {
          <div class="p-3 rounded-lg bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800/60 space-y-2">
            <p class="text-xs text-amber-800 dark:text-amber-200 m-0 flex items-start gap-1.5">
              <mat-icon class="!w-4 !h-4 !text-[16px] shrink-0">warning</mat-icon>
              <span>{{ '{account} has {balance}; this spends {amount}.' | t: { account: name(s.name), balance: (s.balance | money), amount: (s.amount | money) } }}
                {{ 'You can still save; the account will show below zero until money is added.' | t }}</span>
            </p>
            <div class="flex flex-wrap gap-2">
              <button type="button" (click)="addPartnerMoney(s.accountId, s.amount - s.balance)"
                class="px-3 py-1.5 rounded-lg text-xs font-bold text-white bg-primary-600 hover:bg-primary-700 flex items-center gap-1">
                <mat-icon class="!w-4 !h-4 !text-[16px]">add_card</mat-icon>{{ 'Add partner money' | t }}
              </button>
              <button type="button" (click)="transfer(s.accountId, s.amount - s.balance)"
                class="px-3 py-1.5 rounded-lg text-xs font-bold text-primary-700 dark:text-primary-200 border border-primary-300 dark:border-primary-700 bg-white dark:bg-gray-800 hover:bg-primary-50 dark:hover:bg-gray-700 flex items-center gap-1">
                <mat-icon class="!w-4 !h-4 !text-[16px]">swap_horiz</mat-icon>{{ 'Transfer between accounts' | t }}
              </button>
            </div>
          </div>
        }

        @if (mode() === 'credit') {
          <app-credit-terms-fields [group]="creditGroup" [fixed]="true" [kind]="creditKind" [total]="amount"
            [requireParty]="requireParty" [partyPlaceholder]="partyPlaceholder"></app-credit-terms-fields>
        }

        @if (mode() === 'partner') {
          <div class="space-y-1">
            <select formControlName="partnerId"
              class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
              <option [ngValue]="null" disabled>{{ 'Choose the partner who paid…' | t }}</option>
              @for (p of partners(); track p.id) {
                <option [ngValue]="p.id">{{ p.name }}</option>
              }
            </select>
            <p class="text-[11px] text-gray-500 dark:text-gray-400 m-0 leading-snug">
              {{ 'The partner paid out of their own pocket. This is booked as their capital put into the farm and the payment made from it, on the same day — the farm\\'s cash does not change, and their share grows.' | t }}
            </p>
            @if (partners().length === 0) {
              <p class="text-[11px] text-amber-700 dark:text-amber-300 m-0">{{ 'No partners yet. Add the partners (the owner too) on the Partners page first.' | t }}</p>
            }
          </div>
        }
      </div>
    }
  `,
})
export class PaidFromComponent implements OnInit, OnChanges {
  @Input({ required: true }) group!: FormGroup;
  /** out: the farm pays (expense, purchase, repayment). in: the farm receives (sale, income, loan received). */
  @Input() direction: 'out' | 'in' = 'out';
  /** Offer "On credit (বাকি)" (only where the deal can be on credit). */
  @Input() allowCredit = false;
  /** Offer "Paid by partner" (payments only). */
  @Input() allowPartner = true;
  /** The amount of the deal, for the balance warning. */
  @Input() amount: number | null = null;
  @Input() creditKind: 'Payable' | 'Receivable' = 'Payable';
  @Input() requireParty = true;
  @Input() partyPlaceholder = 'Name';

  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly accounts = signal<MoneyAccount[]>([]);
  readonly partners = signal<Investor[]>([]);
  /** False when the user cannot see money accounts: the form then saves to the farm's default account. */
  readonly available = signal(false);
  readonly mode = signal<PaidFromMode>('account');
  readonly paidNow = signal(0);
  private readonly accountId = signal<string | null>(null);
  private readonly amountValue = signal(0);
  private readonly allowCreditSig = signal(false);
  private readonly allowPartnerSig = signal(true);
  private readonly directionSig = signal<'out' | 'in'>('out');
  readonly name = moneyAccountName;

  readonly modes = computed<PaidFromMode[]>(() => {
    if (this.directionSig() === 'in') return ['account'];
    const list: PaidFromMode[] = ['account'];
    if (this.allowCreditSig()) list.push('credit');
    if (this.allowPartnerSig()) list.push('partner');
    return list;
  });

  readonly shortBy = computed(() => {
    if (this.directionSig() !== 'out') return null;
    const spend = this.mode() === 'account' ? this.amountValue() : this.mode() === 'credit' ? this.paidNow() : 0;
    const account = this.accounts().find(a => a.id === this.accountId());
    if (!account || spend <= 0 || spend <= account.balanceBdt + 0.005) return null;
    return { accountId: account.id, name: account.name, balance: account.balanceBdt, amount: spend };
  });

  get creditGroup(): FormGroup {
    return this.group.get('credit') as FormGroup;
  }

  ngOnChanges(): void {
    this.amountValue.set(Number(this.amount) || 0);
    this.allowCreditSig.set(this.allowCredit);
    this.allowPartnerSig.set(this.allowPartner);
    this.directionSig.set(this.direction);
    if (this.group && !this.modes().includes(this.mode())) this.setMode('account');
  }

  ngOnInit(): void {
    this.mode.set((this.group.value.mode ?? 'account') as PaidFromMode);
    this.accountId.set(this.group.value.moneyAccountId ?? null);
    this.group.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(v => {
      this.mode.set((v.mode ?? 'account') as PaidFromMode);
      this.accountId.set(v.moneyAccountId ?? null);
      this.paidNow.set(Number(v.credit?.paidNowBdt) || 0);
    });
    this.loadAccounts();
    if (this.direction === 'out' && this.allowPartner) this.loadPartners();
  }

  setMode(mode: PaidFromMode): void {
    this.group.get('mode')!.setValue(mode);
  }

  addPartnerMoney(accountId: string, amount: number): void {
    this.dialog.open(InvestorTransactionDialogComponent, {
      width: '560px', maxWidth: '95vw',
      data: { defaultType: 'CapitalContribution', moneyAccountId: accountId, amountBdt: Math.ceil(amount) },
    }).afterClosed().subscribe(done => { if (done) this.loadAccounts(); });
  }

  transfer(accountId: string, amount: number): void {
    this.dialog.open(TransferDialogComponent, {
      width: '560px', maxWidth: '95vw',
      data: { toAccountId: accountId, amountBdt: Math.ceil(amount) },
    }).afterClosed().subscribe(done => { if (done) this.loadAccounts(); });
  }

  private loadAccounts(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) return;
    this.finance.getMoneyAccounts(farmId, false).subscribe({
      next: list => {
        this.accounts.set(list);
        this.available.set(list.length > 0);
        const current = this.group.value.moneyAccountId as string | null;
        if (!current || !list.some(a => a.id === current)) {
          this.group.patchValue({ moneyAccountId: list.find(a => a.isDefault)?.id ?? list[0]?.id ?? null });
        }
        this.cdr.markForCheck();
      },
      // No finance access (e.g. a farm worker registering an animal): the server uses the default account.
      error: () => { this.available.set(false); this.cdr.markForCheck(); },
    });
  }

  private loadPartners(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) return;
    this.finance.getInvestors(farmId, false).subscribe({
      next: list => { this.partners.set(list); this.cdr.markForCheck(); },
      error: () => this.partners.set([]),
    });
  }
}
