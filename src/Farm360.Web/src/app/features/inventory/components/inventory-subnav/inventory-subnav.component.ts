import { Component, ChangeDetectionStrategy, ElementRef, afterNextRender, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

interface InventoryNavLink {
  path: string;
  icon: string;
  label: string;
}

/**
 * The Inventory section's tab strip, shown at the top of every inventory page.
 * One compact row that scrolls sideways on a phone, in the order the work happens:
 * stock → buying → daily supplies → reports → setup.
 */
@Component({
  selector: 'app-inventory-subnav',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, MatIconModule, TranslatePipe],
  template: `
    <nav [attr.aria-label]="'Inventory menu' | t"
         class="relative mb-6 flex items-center gap-1.5 border-b border-gray-200 dark:border-gray-800 pb-3 overflow-x-auto lg:flex-wrap lg:overflow-visible print:hidden">
      <a *ngFor="let link of links"
         [routerLink]="link.path"
         routerLinkActive="is-active !bg-emerald-50 dark:!bg-emerald-950/40 !text-emerald-700 dark:!text-emerald-300 !border-emerald-200 dark:!border-emerald-800/60"
         [ngClass]="link.path === '/inventory/base-units' && onPackSizes() ? 'is-active !bg-emerald-50 dark:!bg-emerald-950/40 !text-emerald-700 dark:!text-emerald-300 !border-emerald-200 dark:!border-emerald-800/60' : ''"
         class="min-h-[44px] sm:min-h-0 px-3 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 border border-transparent
                hover:bg-gray-100 dark:hover:bg-gray-800 inline-flex items-center gap-1.5 whitespace-nowrap transition-colors shrink-0">
        <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">{{ link.icon }}</mat-icon> {{ link.label | t }}
      </a>
    </nav>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InventorySubnavComponent {
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly router = inject(Router);

  /** Units and Pack Sizes are two tabs of one page, so both light up the Units tab. */
  onPackSizes(): boolean {
    return this.router.url.startsWith('/inventory/unit-conversions');
  }

  constructor() {
    // On a phone the strip scrolls sideways: bring the current page's tab into view.
    afterNextRender(() => setTimeout(() => {
      const active = (this.host.nativeElement as HTMLElement).querySelector('a.is-active') as HTMLElement | null;
      const nav = active?.parentElement;
      if (active && nav && nav.scrollWidth > nav.clientWidth) {
        nav.scrollLeft = active.offsetLeft - nav.clientWidth / 2 + active.clientWidth / 2;
      }
    }, 150));
  }

  readonly links: InventoryNavLink[] = [
    { path: '/inventory/dashboard', icon: 'dashboard', label: 'Overview' },
    { path: '/inventory/items', icon: 'list_alt', label: 'Stock Items' },
    { path: '/inventory/current-stock', icon: 'inventory_2', label: 'Current Stock' },
    { path: '/inventory/transactions', icon: 'receipt_long', label: 'Stock History' },
    { path: '/inventory/purchase-orders', icon: 'request_quote', label: 'Purchase Orders' },
    { path: '/inventory/returns', icon: 'assignment_return', label: 'Returns' },
    { path: '/inventory/suppliers', icon: 'local_shipping', label: 'Suppliers' },
    { path: '/inventory/daily-consumables', icon: 'today', label: 'Daily Supplies' },
    { path: '/inventory/consumable-plans', icon: 'event_repeat', label: 'Supply Plans' },
    { path: '/inventory/reports/movement', icon: 'query_stats', label: 'Movement Report' },
    { path: '/reports/inventory.daily-consumption', icon: 'summarize', label: 'Daily Use Report' },
    { path: '/inventory/base-units', icon: 'straighten', label: 'Units' },
  ];
}
