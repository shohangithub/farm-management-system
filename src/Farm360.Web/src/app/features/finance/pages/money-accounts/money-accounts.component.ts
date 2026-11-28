import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { FinanceSubnavComponent } from '../../components/finance-subnav/finance-subnav.component';
import { MoneyAccountDialogComponent } from '../../components/money-account-dialog/money-account-dialog.component';
import { TransferDialogComponent } from '../../components/transfer-dialog/transfer-dialog.component';
import { InvestorTransactionDialogComponent } from '../../components/investor-transaction-dialog/investor-transaction-dialog';
import { FinanceService } from '../../services/finance.service';
import { MoneyAccount, MoneyAccountStatement, moneyAccountIcon, moneyAccountName } from '../../models/finance.model';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { NotificationService } from '../../../../shared/services/notification.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { CanPipe } from '../../../../shared/pipes/can.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

/**
 * Money accounts / টাকার খাতা — where the farm's money is: cash in hand, bank accounts and mobile
 * wallets, each with its balance and its own cash book. Every taka spent comes out of one of
 * these; every taka received goes into one.
 */
@Component({
  selector: 'app-money-accounts',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, RouterModule, MatIconModule, MatDialogModule, MatMenuModule, MatTooltipModule,
    PageHeaderComponent, LoadingComponent, ErrorStateComponent, FinanceSubnavComponent, MoneyPipe, CanPipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header [title]="'Money accounts' | t"
      [description]="'Where the farm\\'s money is: cash in hand, bank and mobile wallets. Every spend comes out of one; every receipt goes into one.' | t"
      [breadcrumbActiveNode]="'Money accounts' | t">
      <div actions class="flex flex-wrap items-center gap-2">
        @if ('finance.accounts' | can) {
          <button type="button" (click)="transfer()" class="px-4 py-2 rounded-xl text-sm font-semibold border border-gray-200 dark:border-gray-700 text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 flex items-center gap-1.5">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">swap_horiz</mat-icon>{{ 'Transfer between accounts' | t }}
          </button>
          <button type="button" (click)="openAccount()" class="px-4 py-2 rounded-xl text-sm font-semibold text-white bg-primary-600 hover:bg-primary-700 flex items-center gap-1.5">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon>{{ 'New account' | t }}
          </button>
        }
      </div>
    </app-page-header>

    <div class="px-4 sm:px-6 py-4 mx-auto max-w-7xl space-y-6">
      <app-finance-subnav></app-finance-subnav>

      @if (loading()) { <app-loading [overlay]="true"></app-loading> }
      @if (loadError(); as err) { <app-error-state [message]="err" (retry)="load()"></app-error-state> }

      @if (!loading() && !loadError()) {
        <!-- Total -->
        <div class="rounded-2xl p-5 border border-primary-100 dark:border-primary-900/50 bg-primary-50/60 dark:bg-primary-950/30 flex flex-wrap items-center justify-between gap-4">
          <div>
            <p class="text-xs font-bold uppercase tracking-wider text-primary-700 dark:text-primary-300 m-0">{{ 'Total cash & bank' | t }}</p>
            <p class="text-3xl font-black m-0 mt-1" [ngClass]="total() < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-gray-900 dark:text-white'">{{ total() | money }}</p>
            <p class="text-xs text-gray-600 dark:text-gray-300 m-0 mt-1">{{ 'The same figure as Cash & Bank on the balance sheet, split by where the money is kept.' | t }}</p>
          </div>
          @if ('finance.admin' | can) {
            <button type="button" (click)="addPartnerMoney(null)" class="px-4 py-2 rounded-xl text-sm font-semibold text-white bg-success-600 hover:bg-success-700 flex items-center gap-1.5">
              <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add_card</mat-icon>{{ 'Add partner money' | t }}
            </button>
          }
        </div>

        <!-- Accounts -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          @for (a of visibleAccounts(); track a.id) {
            <div (click)="select(a)" role="button" tabindex="0" (keydown.enter)="select(a)"
              class="rounded-2xl p-4 border cursor-pointer transition-all bg-white dark:bg-gray-800/80"
              [ngClass]="selected()?.id === a.id ? 'border-primary-400 dark:border-primary-600 ring-2 ring-primary-200 dark:ring-primary-800' : 'border-gray-100 dark:border-gray-800 hover:border-primary-200 dark:hover:border-primary-800'"
              [class.opacity-60]="!a.isActive">
              <div class="flex items-start justify-between gap-2">
                <div class="flex items-center gap-3 min-w-0">
                  <div class="w-10 h-10 rounded-xl flex items-center justify-center bg-primary-100 dark:bg-primary-900/40 text-primary-700 dark:text-primary-300 shrink-0">
                    <mat-icon>{{ icon(a.kind) }}</mat-icon>
                  </div>
                  <div class="min-w-0">
                    <p class="text-sm font-bold text-gray-900 dark:text-white m-0 truncate">{{ name(a.name) }}</p>
                    <p class="text-[11px] text-gray-500 dark:text-gray-400 m-0 truncate">
                      {{ kindLabel(a.kind) | t }}@if (a.providerName) { · {{ a.providerName }} }@if (a.accountNumber) { · {{ a.accountNumber }} }
                    </p>
                  </div>
                </div>
                <div class="flex items-center gap-1 shrink-0">
                  @if (a.isDefault) {
                    <span class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-primary-100 dark:bg-primary-900/50 text-primary-700 dark:text-primary-200" [matTooltip]="'Postings that do not name an account land here.' | t">{{ 'Default' | t }}</span>
                  }
                  @if (!a.isActive) {
                    <span class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300">{{ 'Closed' | t }}</span>
                  }
                  @if ('finance.accounts' | can) {
                    <button type="button" (click)="$event.stopPropagation()" [matMenuTriggerFor]="menu" [attr.aria-label]="'More' | t" class="p-1 rounded-lg text-gray-400 hover:text-gray-700 dark:hover:text-gray-200">
                      <mat-icon class="!text-[20px]">more_vert</mat-icon>
                    </button>
                    <mat-menu #menu="matMenu">
                      <button mat-menu-item (click)="openAccount(a)"><mat-icon>edit</mat-icon><span>{{ 'Edit' | t }}</span></button>
                      @if (a.isActive) {
                        <button mat-menu-item (click)="transfer(a.id)"><mat-icon>swap_horiz</mat-icon><span>{{ 'Transfer from here' | t }}</span></button>
                      }
                      @if (a.isActive && !a.isDefault) {
                        <button mat-menu-item (click)="makeDefault(a)"><mat-icon>star</mat-icon><span>{{ 'Make default' | t }}</span></button>
                        <button mat-menu-item (click)="setActive(a, false)"><mat-icon>block</mat-icon><span>{{ 'Close account' | t }}</span></button>
                      }
                      @if (!a.isActive) {
                        <button mat-menu-item (click)="setActive(a, true)"><mat-icon>restart_alt</mat-icon><span>{{ 'Reopen account' | t }}</span></button>
                      }
                    </mat-menu>
                  }
                </div>
              </div>
              <p class="text-2xl font-black m-0 mt-3" [ngClass]="a.balanceBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-gray-900 dark:text-white'">{{ a.balanceBdt | money }}</p>
              @if (a.balanceBdt < 0) {
                <p class="text-[11px] text-rose-600 dark:text-rose-400 m-0 mt-1">{{ 'Below zero: more was paid out of this account than came in. Add partner money or transfer into it.' | t }}</p>
              }
            </div>
          }
        </div>
        @if (hasClosed()) {
          <label class="inline-flex items-center gap-2 text-xs text-gray-600 dark:text-gray-300">
            <input type="checkbox" [ngModel]="showClosed()" (ngModelChange)="showClosed.set($event)"> {{ 'Show closed accounts' | t }}
          </label>
        }

        <!-- Cash book -->
        @if (selected(); as acc) {
          <div class="rounded-2xl border border-gray-100 dark:border-gray-800 bg-white dark:bg-gray-800/80 overflow-hidden">
            <div class="px-4 sm:px-5 py-4 border-b border-gray-100 dark:border-gray-800 flex flex-wrap items-center gap-3 justify-between">
              <div>
                <h3 class="text-base font-bold text-gray-900 dark:text-white m-0 flex items-center gap-2">
                  <mat-icon class="text-primary-600 dark:text-primary-400">menu_book</mat-icon>
                  {{ 'Cash book: {account}' | t: { account: name(acc.name) } }}
                </h3>
                <p class="text-xs text-gray-500 dark:text-gray-400 m-0">{{ 'Every taka in and out of this account, with the running balance.' | t }}</p>
              </div>
              <div class="flex flex-wrap items-center gap-2 text-xs">
                <input type="date" [ngModel]="from()" (ngModelChange)="from.set($event); loadStatement()" class="px-2 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
                <span class="text-gray-500">{{ 'to' | t }}</span>
                <input type="date" [ngModel]="to()" (ngModelChange)="to.set($event); loadStatement()" class="px-2 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
                <a [routerLink]="['/reports', 'finance.money-account-cash-book']" [queryParams]="{ accountId: acc.id, period: from() + '..' + to() }"
                  class="px-3 py-1.5 rounded-lg font-semibold text-primary-700 dark:text-primary-200 border border-primary-200 dark:border-primary-800 hover:bg-primary-50 dark:hover:bg-gray-700 flex items-center gap-1">
                  <mat-icon class="!text-[16px] !w-4 !h-4">picture_as_pdf</mat-icon>{{ 'PDF / Excel' | t }}
                </a>
              </div>
            </div>
            @if (statement(); as s) {
              <div class="grid grid-cols-2 sm:grid-cols-4 gap-px bg-gray-100 dark:bg-gray-800 text-center">
                <div class="bg-white dark:bg-gray-900 p-3"><p class="text-[11px] uppercase font-bold text-gray-500 m-0">{{ 'Opening' | t }}</p><p class="text-sm font-bold m-0 text-gray-900 dark:text-white">{{ s.openingBdt | money }}</p></div>
                <div class="bg-white dark:bg-gray-900 p-3"><p class="text-[11px] uppercase font-bold text-gray-500 m-0">{{ 'Money in' | t }}</p><p class="text-sm font-bold m-0 text-success-700 dark:text-success-400">{{ s.totalInBdt | money }}</p></div>
                <div class="bg-white dark:bg-gray-900 p-3"><p class="text-[11px] uppercase font-bold text-gray-500 m-0">{{ 'Money out' | t }}</p><p class="text-sm font-bold m-0 text-rose-600 dark:text-rose-400">{{ s.totalOutBdt | money }}</p></div>
                <div class="bg-white dark:bg-gray-900 p-3"><p class="text-[11px] uppercase font-bold text-gray-500 m-0">{{ 'Closing' | t }}</p><p class="text-sm font-bold m-0" [ngClass]="s.closingBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-gray-900 dark:text-white'">{{ s.closingBdt | money }}</p></div>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full text-sm">
                  <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
                    <tr>
                      <th class="text-left px-3 py-2">{{ 'Date' | t }}</th>
                      <th class="text-left px-3 py-2">{{ 'Particulars' | t }}</th>
                      <th class="text-right px-3 py-2">{{ 'In' | t }}</th>
                      <th class="text-right px-3 py-2">{{ 'Out' | t }}</th>
                      <th class="text-right px-3 py-2">{{ 'Balance' | t }}</th>
                      <th class="px-2 py-2"></th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                    @for (l of s.lines; track l.transactionId + l.date) {
                      <tr class="hover:bg-gray-50 dark:hover:bg-gray-800/60">
                        <td class="px-3 py-2 whitespace-nowrap text-gray-600 dark:text-gray-300">{{ l.date | date:'d MMM yy' }}</td>
                        <td class="px-3 py-2">
                          <p class="m-0 text-gray-900 dark:text-white">
                            @if (l.isTransfer && l.counterAccountName) {
                              {{ (l.inBdt > 0 ? 'Transfer from {account}' : 'Transfer to {account}') | t: { account: name(l.counterAccountName) } }}
                            } @else { {{ l.description }} }
                          </p>
                          <p class="m-0 text-[11px] text-gray-500 dark:text-gray-400">{{ isBn ? l.categoryNameBn : l.categoryName }} · {{ l.voucherNo }}</p>
                        </td>
                        <td class="px-3 py-2 text-right font-mono text-success-700 dark:text-success-400">{{ l.inBdt ? (l.inBdt | money) : '' }}</td>
                        <td class="px-3 py-2 text-right font-mono text-rose-600 dark:text-rose-400">{{ l.outBdt ? (l.outBdt | money) : '' }}</td>
                        <td class="px-3 py-2 text-right font-mono font-semibold" [ngClass]="l.balanceBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-gray-900 dark:text-white'">{{ l.balanceBdt | money }}</td>
                        <td class="px-2 py-2 text-right">
                          @if (l.isTransfer && ('finance.accounts' | can)) {
                            <button type="button" (click)="deleteTransfer(l.transactionId)" [matTooltip]="'Delete this transfer (both sides)' | t" class="p-1 rounded text-gray-400 hover:text-rose-600">
                              <mat-icon class="!text-[18px]">delete</mat-icon>
                            </button>
                          }
                        </td>
                      </tr>
                    } @empty {
                      <tr><td colspan="6" class="px-3 py-8 text-center text-gray-500 dark:text-gray-400">{{ 'No money moved through this account in the period.' | t }}</td></tr>
                    }
                  </tbody>
                </table>
              </div>
            } @else if (statementLoading()) {
              <p class="p-6 text-center text-sm text-gray-500">{{ 'Loading…' | t }}</p>
            }
          </div>
        }
      }
    </div>
  `,
})
export class MoneyAccountsComponent implements OnInit {
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(I18nService);

  readonly accounts = signal<MoneyAccount[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly selected = signal<MoneyAccount | null>(null);
  readonly statement = signal<MoneyAccountStatement | null>(null);
  readonly statementLoading = signal(false);
  readonly showClosed = signal(false);
  readonly from = signal(toLocalIsoDate(new Date(new Date().getFullYear(), new Date().getMonth(), 1)));
  readonly to = signal(toLocalIsoDate());
  readonly name = moneyAccountName;
  readonly icon = moneyAccountIcon;
  readonly isBn = this.i18n.isBangla;

  readonly total = computed(() => this.accounts().reduce((sum, a) => sum + a.balanceBdt, 0));
  readonly hasClosed = computed(() => this.accounts().some(a => !a.isActive));
  readonly visibleAccounts = computed(() => this.accounts().filter(a => a.isActive || this.showClosed() || a.balanceBdt !== 0));

  ngOnInit(): void {
    this.load();
  }

  kindLabel(kind: string): string {
    return kind === 'Bank' ? 'Bank account' : kind === 'MobileWallet' ? 'Mobile wallet' : 'Cash';
  }

  load(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) { this.loading.set(false); return; }
    this.loading.set(true);
    this.loadError.set(null);
    this.finance.getMoneyAccounts(farmId, true).subscribe({
      next: list => {
        this.accounts.set(list);
        this.loading.set(false);
        const keep = this.selected() && list.find(a => a.id === this.selected()!.id);
        this.select(keep || list.find(a => a.isDefault) || list[0] || null);
      },
      error: err => { this.loading.set(false); this.loadError.set(parseApiError(err)); },
    });
  }

  select(account: MoneyAccount | null): void {
    this.selected.set(account);
    this.loadStatement();
  }

  loadStatement(): void {
    const farmId = this.context.currentFarmValue?.id;
    const account = this.selected();
    if (!farmId || !account) { this.statement.set(null); return; }
    this.statementLoading.set(true);
    this.statement.set(null);
    this.finance.getMoneyAccountStatement(farmId, account.id, this.from(), this.to()).subscribe({
      next: s => { this.statement.set(s); this.statementLoading.set(false); },
      error: err => { this.statementLoading.set(false); this.notify.error(parseApiError(err)); },
    });
  }

  openAccount(account?: MoneyAccount): void {
    this.dialog.open(MoneyAccountDialogComponent, { width: '560px', maxWidth: '95vw', data: { account: account ?? null } })
      .afterClosed().subscribe(done => { if (done) this.load(); });
  }

  transfer(fromAccountId?: string): void {
    this.dialog.open(TransferDialogComponent, { width: '560px', maxWidth: '95vw', data: { fromAccountId: fromAccountId ?? null } })
      .afterClosed().subscribe(done => { if (done) this.load(); });
  }

  addPartnerMoney(accountId: string | null): void {
    this.dialog.open(InvestorTransactionDialogComponent, {
      width: '560px', maxWidth: '95vw',
      data: { defaultType: 'CapitalContribution', moneyAccountId: accountId ?? this.selected()?.id ?? null },
    }).afterClosed().subscribe(done => { if (done) this.load(); });
  }

  makeDefault(account: MoneyAccount): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) return;
    this.finance.setDefaultMoneyAccount(farmId, account.id).subscribe({
      next: () => { this.notify.success(this.i18n.t('Default account changed.')); this.load(); },
      error: err => this.notify.error(parseApiError(err)),
    });
  }

  setActive(account: MoneyAccount, isActive: boolean): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) return;
    this.finance.setMoneyAccountActive(farmId, account.id, isActive).subscribe({
      next: () => this.load(),
      error: err => this.notify.error(parseApiError(err)),
    });
  }

  deleteTransfer(transactionId: string): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId || !confirm(this.i18n.t('Delete this transfer? Both sides are removed and the two balances go back.'))) return;
    this.finance.deleteMoneyTransfer(farmId, transactionId).subscribe({
      next: () => this.load(),
      error: err => this.notify.error(parseApiError(err)),
    });
  }
}
