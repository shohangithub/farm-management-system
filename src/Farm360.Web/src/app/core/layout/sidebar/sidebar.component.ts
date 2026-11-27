import { Component, Input, ChangeDetectionStrategy, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe } from '../../i18n/translate.pipe';

interface MenuItem {
  icon: string;
  label: string;
  route?: string;
  disabled?: boolean;
  badge?: string;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule, MatListModule, MatIconModule, MatTooltipModule, TranslatePipe],
  templateUrl: './sidebar.component.html',
  styleUrls: ['./sidebar.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SidebarComponent {
  @Input() isCollapsed = false;

  menuItems: MenuItem[] = [
    { icon: 'dashboard', label: 'Dashboard', route: '/dashboard' },
    { icon: 'pets', label: 'Livestock', route: '/livestock' },
    { icon: 'healing', label: 'Health', route: '/health' },
    { icon: 'wb_sunny', label: 'Weather', route: '/weather' },
    { icon: 'restaurant_menu', label: 'Feeding', route: '/feeding' },
    { icon: 'inventory', label: 'Inventory', route: '/inventory' },
    { icon: 'account_balance_wallet', label: 'Finance', route: '/finance' },
    { icon: 'summarize', label: 'Reports', route: '/reports' }
  ];

  bottomMenuItems: MenuItem[] = [
    { icon: 'workspace_premium', label: 'Subscription', route: '/settings/billing' },
    { icon: 'settings', label: 'Settings', route: '/settings' }
  ];

  private readonly router = inject(Router);
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(e => e.urlAfterRedirects),
      startWith(this.router.url)
    ),
    { initialValue: this.router.url }
  );

  /** The longest bottom-menu route that prefixes the current URL wins, so only one item is lit. */
  isBottomItemActive(item: MenuItem): boolean {
    const path = (this.url() || '').split(/[?#]/)[0];
    const matches = this.bottomMenuItems
      .filter(i => i.route && (path === i.route || path.startsWith(i.route + '/')))
      .sort((a, b) => b.route!.length - a.route!.length);
    return matches[0] === item;
  }
}
