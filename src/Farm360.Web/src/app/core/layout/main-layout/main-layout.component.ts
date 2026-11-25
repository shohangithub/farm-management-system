import { Component, ViewChild, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterModule } from '@angular/router';
import { MatSidenavModule, MatSidenav } from '@angular/material/sidenav';
import { MatIconModule } from '@angular/material/icon';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { HeaderComponent } from '../header/header.component';
import { BreakpointObserver } from '@angular/cdk/layout';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { SubscriptionService } from '../../services/subscription.service';
import { AuthService } from '../../services/auth.service';
import { TranslatePipe } from '../../i18n/translate.pipe';

import { WorkingContextService } from '../../services/working-context.service';
@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    CommonModule, 
    RouterOutlet, 
    RouterModule,
    MatSidenavModule, 
    MatIconModule,
    SidebarComponent, 
    HeaderComponent,
    TranslatePipe
  ],
  templateUrl: './main-layout.component.html'
})
export class MainLayoutComponent implements OnInit, OnDestroy {
  @ViewChild('sidenav') sidenav!: MatSidenav;
  private breakpointObserver = inject(BreakpointObserver);
  public subscriptionService = inject(SubscriptionService);
  private authService = inject(AuthService);
  readonly workingContext = inject(WorkingContextService);

  /** Signed in with the shared, read-only demo account. */
  readonly isDemoUser = computed(() => this.authService.currentUserSignal()?.isDemo === true);

  isSidebarCollapsed = false;
  isMobile = false;
  isBannerDismissed = signal(false);
  private destroy$ = new Subject<void>();

  ngOnInit() {
    this.breakpointObserver
      .observe(['(max-width: 768px)'])
      .pipe(takeUntil(this.destroy$))
      .subscribe((result) => {
        this.isMobile = result.matches;
        if (this.isMobile) {
          // On mobile, sidebar is not collapsed, it's just hidden (over mode)
          this.isSidebarCollapsed = false;
        }
      });

    // Fetch subscription status for banner
    this.subscriptionService.getCurrentSubscription()
      .pipe(takeUntil(this.destroy$))
      .subscribe({ error: () => {} });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /** Leaves the demo and opens sign-up. */
  startFreeTrial(): void {
    this.authService.logout('/register');
  }

  toggleSidebar() {
    if (this.isMobile) {
      this.sidenav.toggle();
    } else {
      this.isSidebarCollapsed = !this.isSidebarCollapsed;
    }
  }
}
