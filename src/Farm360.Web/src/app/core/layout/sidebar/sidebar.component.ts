import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

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
  imports: [CommonModule, RouterModule, MatListModule, MatIconModule, MatTooltipModule],
  templateUrl: './sidebar.component.html'
})
export class SidebarComponent {
  @Input() isCollapsed = false;

  menuItems: MenuItem[] = [
    { icon: 'dashboard', label: 'Dashboard', route: '/dashboard', disabled: true, badge: 'soon' },
    { icon: 'pets', label: 'Livestock', route: '/livestock' },
    { icon: 'group_work', label: 'Batches', route: '/livestock/batches' },
    { icon: 'healing', label: 'Health', route: '/health' },
    { icon: 'agriculture', label: 'Feeding', disabled: true, badge: 'soon' },
    { icon: 'inventory', label: 'Inventory', disabled: true, badge: 'soon' },
    { icon: 'account_balance_wallet', label: 'Finance', disabled: true, badge: 'soon' }
  ];

  bottomMenuItems: MenuItem[] = [
    { icon: 'business', label: 'Organizations', route: '/organizations' },
    { icon: 'settings', label: 'Master Data', route: '/settings/master-data' }
  ];
}
