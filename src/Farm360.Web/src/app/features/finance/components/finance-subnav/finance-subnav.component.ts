import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter, map } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

interface FinanceNavLink {
  path: string;
  icon: string;
  label: string;
  exact?: boolean;
}

interface FinanceNavGroup {
  label: string;
  icon: string;
  links: FinanceNavLink[];
}

/**
 * The Finance section's tab bar — one place to add a page instead of editing every page.
 * Everyday pages are direct tabs; statements and setup pages sit in two dropdowns so the
 * bar stays short on every screen size.
 */
@Component({
  selector: 'app-finance-subnav',
  standalone: true,
  imports: [TranslatePipe, CommonModule, RouterLink, RouterLinkActive, MatIconModule, MatMenuModule],
  template: `
    <nav class="flex items-center gap-1.5 border-b border-gray-200 dark:border-gray-800 pb-3 overflow-x-auto print:hidden"
         [attr.aria-label]="'Finance' | t">
      <a *ngFor="let link of everyday"
         [routerLink]="link.path"
         routerLinkActive="!bg-emerald-50 dark:!bg-emerald-950/40 !text-emerald-700 dark:!text-emerald-300 !border-emerald-200 dark:!border-emerald-800/60"
         [routerLinkActiveOptions]="{ exact: !!link.exact }"
         class="min-h-[44px] px-3.5 rounded-xl text-sm font-semibold text-gray-600 dark:text-gray-400 border border-transparent hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors shrink-0">
        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">{{ link.icon }}</mat-icon> {{ link.label | t }}
      </a>

      <span class="w-px h-6 bg-gray-200 dark:bg-gray-800 mx-1 shrink-0" aria-hidden="true"></span>

      <ng-container *ngFor="let group of groups">
        <button type="button" [matMenuTriggerFor]="menu"
                class="min-h-[44px] px-3.5 rounded-xl text-sm font-semibold border flex items-center gap-2 whitespace-nowrap transition-colors shrink-0"
                [ngClass]="activeIn(group)
                  ? 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800/60'
                  : 'text-gray-600 dark:text-gray-400 border-transparent hover:bg-gray-100 dark:hover:bg-gray-800'">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">{{ group.icon }}</mat-icon>
          {{ (activeLink(group)?.label || group.label) | t }}
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] -mr-1">expand_more</mat-icon>
        </button>
        <mat-menu #menu="matMenu" xPosition="before">
          <a mat-menu-item *ngFor="let link of group.links" [routerLink]="link.path"
             [class.!text-emerald-700]="isActive(link)">
            <mat-icon>{{ link.icon }}</mat-icon>
            <span>{{ link.label | t }}</span>
          </a>
        </mat-menu>
      </ng-container>
    </nav>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FinanceSubnavComponent {
  private router = inject(Router);

  private readonly url = toSignal(
    this.router.events.pipe(filter(e => e instanceof NavigationEnd), map(() => this.router.url)),
    { initialValue: this.router.url }
  );

  readonly everyday: FinanceNavLink[] = [
    { path: '/finance', icon: 'dashboard', label: 'Overview', exact: true },
    { path: '/finance/transactions', icon: 'receipt_long', label: 'Ledger' },
    { path: '/finance/credit', icon: 'request_quote', label: 'Payables & Receivables' },
  ];

  readonly groups: FinanceNavGroup[] = [
    {
      label: 'Statements', icon: 'description', links: [
        { path: '/finance/reports/income-statement', icon: 'query_stats', label: 'Profit & Loss' },
        { path: '/finance/reports/balance-sheet', icon: 'account_balance_wallet', label: 'Balance Sheet' },
        { path: '/finance/reports/trial-balance', icon: 'balance', label: 'Trial Balance' },
        { path: '/finance/reports/monthly-pnl', icon: 'calendar_view_month', label: 'Monthly P&L' },
        { path: '/finance/reports/income-expense', icon: 'swap_vert', label: 'Income & Expense' },
        { path: '/finance/reports/full-accounts', icon: 'menu_book', label: 'Full Accounts' },
      ]
    },
    {
      label: 'Setup', icon: 'tune', links: [
        { path: '/finance/accounts-setup', icon: 'tune', label: 'Accounts Setup' },
        { path: '/finance/fixed-assets', icon: 'agriculture', label: 'Fixed Assets' },
        { path: '/finance/loans', icon: 'account_balance', label: 'Loans & Liabilities' },
        { path: '/finance/investors', icon: 'groups', label: 'Investors' },
        { path: '/finance/accounting-health', icon: 'fact_check', label: 'Accounting Health' },
      ]
    },
  ];

  isActive(link: FinanceNavLink): boolean {
    const path = (this.url() || '').split(/[?#]/)[0];
    // Farm Shares is reached from the Investors page, so it highlights Investors.
    if (link.path === '/finance/investors' && path.startsWith('/finance/shares')) return true;
    return path === link.path || path.startsWith(link.path + '/');
  }

  activeLink(group: FinanceNavGroup): FinanceNavLink | undefined {
    return group.links.find(l => this.isActive(l));
  }

  activeIn(group: FinanceNavGroup): boolean {
    return !!this.activeLink(group);
  }
}
