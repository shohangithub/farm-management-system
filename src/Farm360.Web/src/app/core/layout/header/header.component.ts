import { Component, EventEmitter, Output, inject, ChangeDetectionStrategy, OnInit, signal, computed } from '@angular/core';
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
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { WorkingContextService } from '../../services/working-context.service';
import { ContextSelectorDialogComponent } from '../context-selector/context-selector-dialog.component';

import { I18nService } from '../../i18n/i18n.service';
import { TranslatePipe } from '../../i18n/translate.pipe';
@Component({
  selector: 'app-header',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    RouterModule,
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatDividerModule,
    MatBadgeModule,
    MatTooltipModule,
    MatDialogModule,
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
  readonly i18n = inject(I18nService);
  private dialog = inject(MatDialog);
  readonly signalRService = inject(IntelligenceSignalRService);
  readonly currentOrg$ = inject(WorkingContextService).currentOrg$;

  readonly user = this.authService.currentUserSignal;
  readonly displayName = computed(() => {
    const u = this.user();
    return u?.fullName?.trim() || u?.email || u?.phone || 'Account';
  });
  readonly initials = computed(() => {
    const name = this.displayName();
    const parts = name.split(/[\s@._-]+/).filter(Boolean);
    const letters = parts.length > 1 ? parts[0][0] + parts[1][0] : name.slice(0, 2);
    return letters.toUpperCase();
  });

  openContextDialog(): void {
    this.dialog.open(ContextSelectorDialogComponent, { width: '420px', maxWidth: '95vw', autoFocus: false });
  }

  logout(): void {
    this.authService.logout();
  }

  onNotificationsOpen(): void {
    this.signalRService.markAllAsRead();
  }
}
