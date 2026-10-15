import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

interface FinanceNavLink {
  path: string;
  icon: string;
  label: string;
  exact?: boolean;
}

/** The Finance section's tab bar — one place to add a page instead of editing every page. */
@Component({
  selector: 'app-finance-subnav',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, MatIconModule],
  template: `
    <nav class="flex items-center gap-2 border-b border-gray-200 dark:border-gray-700/60 pb-3 overflow-x-auto print:hidden">
      <a *ngFor="let link of links"
         [routerLink]="link.path"
         routerLinkActive="!bg-emerald-50 dark:!bg-emerald-950/40 !text-emerald-700 dark:!text-emerald-300 border-emerald-200 dark:border-emerald-800/60"
         [routerLinkActiveOptions]="{ exact: !!link.exact }"
         class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 border border-transparent hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
        <mat-icon class="text-base">{{ link.icon }}</mat-icon> {{ link.label }}
      </a>
    </nav>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FinanceSubnavComponent {
  readonly links: FinanceNavLink[] = [
    { path: '/finance', icon: 'dashboard', label: 'Overview', exact: true },
    { path: '/finance/transactions', icon: 'receipt_long', label: 'General Ledger' },
    { path: '/finance/reports/income-statement', icon: 'query_stats', label: 'Profit & Loss' },
    { path: '/finance/reports/balance-sheet', icon: 'account_balance_wallet', label: 'Balance Sheet' },
    { path: '/finance/reports/trial-balance', icon: 'balance', label: 'Trial Balance' },
    { path: '/finance/reports/monthly-pnl', icon: 'calendar_view_month', label: 'Monthly P&L' },
    { path: '/finance/reports/income-expense', icon: 'swap_vert', label: 'Income & Expense' },
    { path: '/finance/reports/full-accounts', icon: 'menu_book', label: 'Full Accounts' },
    { path: '/finance/fixed-assets', icon: 'agriculture', label: 'Fixed Assets' },
    { path: '/finance/accounts-setup', icon: 'tune', label: 'Accounts Setup' },
    { path: '/finance/credit', icon: 'request_quote', label: 'Payables & Receivables' },
    { path: '/finance/accounting-health', icon: 'fact_check', label: 'Accounting Health' },
    { path: '/finance/loans', icon: 'account_balance', label: 'Loans & Liabilities' },
    { path: '/finance/investors', icon: 'groups', label: 'Investors & Equity', exact: true },
  ];
}
