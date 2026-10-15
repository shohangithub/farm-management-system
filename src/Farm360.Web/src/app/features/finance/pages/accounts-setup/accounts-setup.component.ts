import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, catchError, combineLatest, forkJoin, of, switchMap } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { FinanceService } from '../../services/finance.service';
import { FinanceSubnavComponent } from '../../components/finance-subnav/finance-subnav.component';
import { AccountingSettings, AccountingSetupOptions, BalanceSheetEntry, SaveBalanceSheetEntryRequest } from '../../models/finance.model';

/**
 * Accounts setup: the financial year, the date the books start from, the opening position on that
 * date, and year-end adjustments (accruals, prepayments, provisions). Opening balances are not
 * forced to balance — an incomplete opening position shows on the balance sheet as a Difference.
 */
@Component({
  selector: 'app-accounts-setup',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatIconModule, MatButtonModule, PageHeaderComponent, FinanceSubnavComponent, CurrencyPipe, DatePipe],
  template: `
    <app-page-header
      title="Accounts Setup"
      description="Financial year, books start date, opening balances and year-end adjustments for the balance sheet."
      breadcrumbActiveNode="Accounts Setup">
    </app-page-header>

    <div class="px-6 py-4 mx-auto max-w-6xl space-y-6">
      <app-finance-subnav></app-finance-subnav>

      <div *ngIf="error()" class="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-800 text-sm text-rose-700 dark:text-rose-300 flex justify-between">
        <span>{{ error() }}</span><button (click)="error.set(null)"><mat-icon class="!text-[16px]">close</mat-icon></button>
      </div>
      <div *ngIf="notice()" class="p-3 rounded-xl bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800 text-sm text-emerald-700 dark:text-emerald-300 whitespace-pre-line">{{ notice() }}</div>

      <!-- 1. Settings -->
      <section class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-800/50 p-5 space-y-4">
        <div>
          <h3 class="text-sm font-black">1. Financial year &amp; books start</h3>
          <p class="text-xs text-gray-500 mt-1">
            The books start date is the day of the opening position (for example the last audited year end). Ledger entries on or
            before it are replaced by the opening balances below; everything after it is calculated from the ledger.
            Leave it blank to run the statements from the very first entry.
          </p>
        </div>
        <div class="flex flex-wrap gap-4 items-end text-xs">
          <label class="flex flex-col gap-1">Financial year starts in
            <select [(ngModel)]="fyMonth" class="field">
              <option *ngFor="let m of months; let i = index" [ngValue]="i + 1">{{ m }}</option>
            </select>
          </label>
          <label class="flex flex-col gap-1">Books start date (opening position)
            <input type="date" [(ngModel)]="booksStart" class="field" />
          </label>
          <button mat-flat-button (click)="saveSettings()" [disabled]="busy()" class="!rounded-xl !bg-emerald-600 !text-white">Save settings</button>
          <span *ngIf="settings() && !settings()!.isConfigured" class="text-amber-600">Not saved yet — the statements use July–June and no opening balances.</span>
        </div>
      </section>

      <!-- 2. Opening balances -->
      <section class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-800/50 p-5 space-y-4">
        <div class="flex items-start justify-between gap-4 flex-wrap">
          <div>
            <h3 class="text-sm font-black">2. Opening balances <span *ngIf="settings()?.booksStartDate" class="font-normal text-gray-500">as at {{ settings()!.booksStartDate | date:'d MMM yyyy' }}</span></h3>
            <p class="text-xs text-gray-500 mt-1">
              Enter each balance as a positive amount on its normal side (cash you hold, money you owe, capital put in).
              Fixed assets owned on that date go in the <a routerLink="/finance/fixed-assets" class="underline">fixed-asset register</a> as "opening".
            </p>
          </div>
          <div class="flex gap-2">
            <button mat-stroked-button (click)="generate()" [disabled]="busy() || !settings()?.booksStartDate" class="!rounded-xl !text-xs"
                    title="Turn every ledger entry up to the books start into opening balances">
              <mat-icon class="!text-[16px]">auto_fix_high</mat-icon> Generate from ledger
            </button>
            <button mat-flat-button (click)="startEntry('Opening')" [disabled]="!settings()?.booksStartDate" class="!rounded-xl !text-xs !bg-emerald-600 !text-white">
              <mat-icon class="!text-[16px]">add</mat-icon> Opening balance
            </button>
          </div>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
          <div class="p-3 rounded-xl bg-gray-50 dark:bg-gray-900/40">Assets <span class="block font-mono font-bold text-sm">{{ openingTotals().assets | currency:'BDT ':'symbol':'1.2-2' }}</span></div>
          <div class="p-3 rounded-xl bg-gray-50 dark:bg-gray-900/40">Liabilities + equity <span class="block font-mono font-bold text-sm">{{ openingTotals().claims | currency:'BDT ':'symbol':'1.2-2' }}</span></div>
          <div class="p-3 rounded-xl" [ngClass]="openingTotals().difference === 0 ? 'bg-emerald-50 dark:bg-emerald-950/30 text-emerald-700' : 'bg-rose-50 dark:bg-rose-950/30 text-rose-700'">
            Difference (excl. opening fixed assets)
            <span class="block font-mono font-bold text-sm">{{ openingTotals().difference | currency:'BDT ':'symbol':'1.2-2' }}</span>
          </div>
        </div>

        <ng-container *ngTemplateOutlet="entryTable; context: { $implicit: openingEntries() }"></ng-container>
      </section>

      <!-- 3. Adjustments -->
      <section class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-800/50 p-5 space-y-4">
        <div class="flex items-start justify-between gap-4 flex-wrap">
          <div>
            <h3 class="text-sm font-black">3. Year-end adjustments</h3>
            <p class="text-xs text-gray-500 mt-1">
              Accrued expenses (a bill for this year not yet received), prepayments and provisions. Each one moves a balance-sheet line
              and an income or expense account. Set a reversal date (usually the first day of the next year) so it undoes itself
              when the real bill is posted.
            </p>
          </div>
          <button mat-flat-button (click)="startEntry('Adjustment')" class="!rounded-xl !text-xs !bg-emerald-600 !text-white">
            <mat-icon class="!text-[16px]">add</mat-icon> Adjustment
          </button>
        </div>
        <ng-container *ngTemplateOutlet="entryTable; context: { $implicit: adjustments() }"></ng-container>
      </section>

      <!-- Entry form -->
      <div *ngIf="entry() as e" class="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" (click)="entry.set(null)">
        <div class="bg-white dark:bg-gray-800 rounded-2xl p-5 w-full max-w-lg space-y-3 text-xs" (click)="$event.stopPropagation()">
          <h3 class="text-sm font-black">{{ editingId() ? 'Edit' : 'New' }} {{ e.entryType === 'Opening' ? 'opening balance' : 'adjustment' }}</h3>
          <label class="flex flex-col gap-1">Balance-sheet line
            <select [(ngModel)]="e.accountCode" class="field">
              <option *ngFor="let a of (e.entryType === 'Opening' ? options()?.openingAccounts : options()?.adjustmentAccounts)" [value]="a.code">
                {{ a.code }} — {{ a.name }} ({{ a.accountClass }})
              </option>
            </select>
          </label>
          <label *ngIf="e.entryType === 'Adjustment'" class="flex flex-col gap-1">Income / expense account (other side)
            <select [(ngModel)]="e.profitAccountCode" class="field">
              <option *ngFor="let a of options()?.profitAndLossAccounts" [value]="a.code">{{ a.code }} — {{ a.name }}</option>
            </select>
          </label>
          <div class="grid grid-cols-2 gap-3">
            <label *ngIf="e.entryType === 'Adjustment'" class="flex flex-col gap-1">Date
              <input type="date" [(ngModel)]="e.entryDate" class="field" />
            </label>
            <label *ngIf="e.entryType === 'Adjustment'" class="flex flex-col gap-1">Reverse on (optional)
              <input type="date" [(ngModel)]="e.reversalDate" class="field" />
            </label>
            <label class="flex flex-col gap-1">Amount (BDT)
              <input type="number" [(ngModel)]="e.amountBdt" class="field" />
              <span class="text-[10px] text-gray-400">Positive increases the line; negative reduces it.</span>
            </label>
          </div>
          <label class="flex flex-col gap-1">Description
            <input [(ngModel)]="e.description" class="field" placeholder="e.g. Cash in hand per audited accounts" />
          </label>
          <label class="flex flex-col gap-1">Notes
            <input [(ngModel)]="e.notes" class="field" />
          </label>
          <div class="flex justify-end gap-2 pt-2">
            <button mat-stroked-button (click)="entry.set(null)" class="!rounded-xl">Cancel</button>
            <button mat-flat-button (click)="saveEntry(e)" [disabled]="busy()" class="!rounded-xl !bg-emerald-600 !text-white">Save</button>
          </div>
        </div>
      </div>

      <ng-template #entryTable let-rows>
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
              <tr>
                <th class="text-left px-3 py-2">Code</th><th class="text-left px-3 py-2">Date</th><th class="text-left px-3 py-2">Line</th>
                <th class="text-left px-3 py-2">Description</th><th class="text-right px-3 py-2">Amount</th><th></th>
              </tr>
            </thead>
            <tbody>
              <tr *ngIf="!rows.length"><td colspan="6" class="px-3 py-6 text-center text-xs text-gray-400">Nothing entered.</td></tr>
              <tr *ngFor="let r of rows" class="border-t border-gray-100 dark:border-gray-700/60">
                <td class="px-3 py-2 font-mono text-xs text-gray-500">{{ r.code }}</td>
                <td class="px-3 py-2 text-xs">
                  {{ r.entryDate | date:'d MMM yyyy' }}
                  <span *ngIf="r.reversalDate" class="block text-[11px] text-gray-400">reverses {{ r.reversalDate | date:'d MMM yyyy' }}</span>
                </td>
                <td class="px-3 py-2 text-xs">
                  <span class="font-mono text-gray-400">{{ r.accountCode }}</span> {{ r.accountName }}
                  <span *ngIf="r.profitAccountName" class="block text-[11px] text-gray-500">against {{ r.profitAccountCode }} {{ r.profitAccountName }}</span>
                </td>
                <td class="px-3 py-2 text-xs">{{ r.description }}</td>
                <td class="px-3 py-2 text-right font-mono" [class.text-rose-600]="r.amountBdt < 0">{{ r.amountBdt | currency:'BDT ':'symbol':'1.2-2' }}</td>
                <td class="px-3 py-2 text-right whitespace-nowrap">
                  <button (click)="editEntry(r)" class="p-1 text-gray-500 hover:text-emerald-600"><mat-icon class="!text-[18px]">edit</mat-icon></button>
                  <button (click)="deleteEntry(r)" class="p-1 text-gray-500 hover:text-rose-600"><mat-icon class="!text-[18px]">delete</mat-icon></button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </ng-template>

      <p class="text-xs text-gray-500">
        When done, check the <a routerLink="/finance/reports/balance-sheet" class="underline font-semibold">balance sheet</a> —
        a Difference row means the opening position is incomplete.
      </p>
    </div>
  `,
  styles: [`
    .field { padding: 0.375rem 0.625rem; border-radius: 0.5rem; border: 1px solid rgb(209 213 219); background: transparent; font-size: 0.75rem; outline: none; }
    .field:focus { border-color: rgb(16 185 129); }
    :host-context(.dark) .field { border-color: rgb(55 65 81); }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountsSetupComponent {
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly destroyRef = inject(DestroyRef);

  readonly months = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

  readonly settings = signal<AccountingSettings | null>(null);
  readonly options = signal<AccountingSetupOptions | null>(null);
  readonly entries = signal<BalanceSheetEntry[]>([]);
  readonly entry = signal<SaveBalanceSheetEntryRequest | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  fyMonth = 7;
  booksStart = '';

  readonly openingEntries = computed(() => this.entries().filter(e => e.entryType === 'Opening'));
  readonly adjustments = computed(() => this.entries().filter(e => e.entryType === 'Adjustment'));

  readonly openingTotals = computed(() => {
    const assetCodes = new Set((this.options()?.openingAccounts ?? []).filter(a => a.accountClass === 'Asset').map(a => a.code));
    let assets = 0;
    let claims = 0;
    for (const e of this.openingEntries()) {
      if (assetCodes.has(e.accountCode)) assets += e.amountBdt; else claims += e.amountBdt;
    }
    return { assets, claims, difference: Math.round((assets - claims) * 100) / 100 };
  });

  private readonly refresh$ = new BehaviorSubject<void>(undefined);

  constructor() {
    combineLatest([this.refresh$, this.context.currentFarm$]).pipe(
      switchMap(([, farm]) => !farm ? of(null) : forkJoin({
        settings: this.finance.getAccountingSettings(farm.id),
        options: this.finance.getAccountingOptions(farm.id),
        entries: this.finance.getBalanceSheetEntries(farm.id),
      }).pipe(catchError(err => { this.error.set(parseApiError(err, 'Failed to load the accounts setup.')); return of(null); }))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(r => {
      if (!r) return;
      this.settings.set(r.settings);
      this.options.set(r.options);
      this.entries.set(r.entries);
      this.fyMonth = r.settings.fiscalYearStartMonth;
      this.booksStart = r.settings.booksStartDate ? r.settings.booksStartDate.substring(0, 10) : '';
    });
  }

  private get farmId(): string | null {
    return this.context.currentFarmValue?.id ?? null;
  }

  saveSettings(): void {
    const farmId = this.farmId;
    if (!farmId) return;
    this.busy.set(true);
    this.finance.saveAccountingSettings(farmId, { fiscalYearStartMonth: +this.fyMonth, booksStartDate: this.booksStart || null }).subscribe({
      next: () => this.done('Settings saved. Opening balances and opening fixed assets are dated on the books start.'),
      error: err => this.fail(err, 'Could not save the settings.'),
    });
  }

  generate(): void {
    const farmId = this.farmId;
    if (!farmId || !confirm('Create opening balances from every ledger entry up to the books start date?')) return;
    this.busy.set(true);
    this.finance.generateOpeningEntries(farmId).subscribe({
      next: r => this.done([`${r.created} opening balance(s) created from the ledger.`, ...r.warnings].join('\n')),
      error: err => this.fail(err, 'Could not generate the opening balances.'),
    });
  }

  startEntry(type: 'Opening' | 'Adjustment'): void {
    const opts = this.options();
    this.editingId.set(null);
    this.entry.set({
      entryType: type,
      entryDate: type === 'Opening' ? (this.settings()?.booksStartDate ?? '').substring(0, 10) : new Date().toISOString().split('T')[0],
      accountCode: (type === 'Opening' ? opts?.openingAccounts[0]?.code : opts?.adjustmentAccounts[0]?.code) ?? '',
      amountBdt: 0,
      description: '',
      profitAccountCode: type === 'Adjustment' ? (opts?.profitAndLossAccounts[0]?.code ?? null) : null,
      reversalDate: null,
      notes: null,
    });
  }

  editEntry(r: BalanceSheetEntry): void {
    this.editingId.set(r.id);
    this.entry.set({
      entryType: r.entryType as 'Opening' | 'Adjustment',
      entryDate: r.entryDate.substring(0, 10),
      accountCode: r.accountCode,
      amountBdt: r.amountBdt,
      description: r.description,
      profitAccountCode: r.profitAccountCode ?? null,
      reversalDate: r.reversalDate ? r.reversalDate.substring(0, 10) : null,
      notes: r.notes ?? null,
    });
  }

  saveEntry(e: SaveBalanceSheetEntryRequest): void {
    const farmId = this.farmId;
    if (!farmId) return;
    const body: SaveBalanceSheetEntryRequest = { ...e, amountBdt: +e.amountBdt, reversalDate: e.reversalDate || null };
    const id = this.editingId();
    this.busy.set(true);
    (id ? this.finance.updateBalanceSheetEntry(farmId, id, body) : this.finance.createBalanceSheetEntry(farmId, body)).subscribe({
      next: saved => this.done(`${saved.code} saved.`, () => this.entry.set(null)),
      error: err => this.fail(err, 'Could not save the entry.'),
    });
  }

  deleteEntry(r: BalanceSheetEntry): void {
    const farmId = this.farmId;
    if (!farmId || !confirm(`Delete ${r.code} (${r.description})?`)) return;
    this.busy.set(true);
    this.finance.deleteBalanceSheetEntry(farmId, r.id).subscribe({
      next: () => this.done(`${r.code} deleted.`),
      error: err => this.fail(err, 'Could not delete the entry.'),
    });
  }

  private done(message: string, after?: () => void): void {
    this.busy.set(false);
    this.error.set(null);
    this.notice.set(message);
    after?.();
    this.refresh$.next();
  }

  private fail(err: unknown, fallback: string): void {
    this.busy.set(false);
    this.error.set(parseApiError(err, fallback));
  }
}
