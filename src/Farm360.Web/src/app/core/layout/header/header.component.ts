import { Component, EventEmitter, Output, inject, ChangeDetectionStrategy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ContextSelectorComponent } from '../context-selector/context-selector.component';
import { GlobalSearchComponent } from '../../../shared/components/global-search/global-search.component';
import { AuthService } from '../../services/auth.service';
import { IntelligenceSignalRService } from '../../services/intelligence-signalr.service';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { MatBadgeModule } from '@angular/material/badge';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatDividerModule,
    MatBadgeModule,
    MatTooltipModule,
    ContextSelectorComponent,
    GlobalSearchComponent
  ],
  templateUrl: './header.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HeaderComponent implements OnInit {
  @Output() toggleSidebar = new EventEmitter<void>();
  isDarkMode = signal(false);

  ngOnInit(): void {
    const savedTheme = localStorage.getItem('theme-preference');
    const isDark = savedTheme === 'dark' || (!savedTheme && window.matchMedia('(prefers-color-scheme: dark)').matches);
    this.isDarkMode.set(isDark);
    if (isDark) {
      document.documentElement.classList.add('dark');
      document.documentElement.style.colorScheme = 'dark';
      document.body.style.colorScheme = 'dark';
    } else {
      document.documentElement.classList.remove('dark');
      document.documentElement.style.colorScheme = 'light';
      document.body.style.colorScheme = 'light';
    }
  }

  onToggleSidebar(): void {
    this.toggleSidebar.emit();
  }

  toggleTheme(): void {
    const nextMode = !this.isDarkMode();
    this.isDarkMode.set(nextMode);
    if (nextMode) {
      document.documentElement.classList.add('dark');
      document.documentElement.style.colorScheme = 'dark';
      document.body.style.colorScheme = 'dark';
      localStorage.setItem('theme-preference', 'dark');
    } else {
      document.documentElement.classList.remove('dark');
      document.documentElement.style.colorScheme = 'light';
      document.body.style.colorScheme = 'light';
      localStorage.setItem('theme-preference', 'light');
    }
  }

  private authService = inject(AuthService);
  readonly signalRService = inject(IntelligenceSignalRService);

  logout(): void {
    this.authService.logout();
  }

  onNotificationsOpen(): void {
    this.signalRService.markAllAsRead();
  }
}
