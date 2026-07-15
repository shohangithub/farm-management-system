import { Component } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';

/**
 * Root shell component — sidebar navigation + topbar + router outlet.
 * All feature pages are rendered inside the <router-outlet>.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="app-shell">

      <!-- ── Sidebar ─────────────────────────────────────── -->
      <aside class="app-sidebar">
        <!-- Logo -->
        <div class="sidebar-logo">
          <span class="logo-icon">🌾</span>
          <span class="logo-text">Farm360</span>
        </div>

        <!-- Navigation -->
        <nav class="sidebar-nav">
          <a routerLink="/livestock"
             routerLinkActive="nav-item--active"
             class="nav-item"
             title="Livestock">
            <span class="nav-icon">🐄</span>
            <span class="nav-label">Livestock</span>
          </a>

          <!-- Placeholders for future modules -->
          <a class="nav-item nav-item--disabled" title="Health Records (coming soon)">
            <span class="nav-icon">🏥</span>
            <span class="nav-label">Health</span>
            <span class="nav-badge">soon</span>
          </a>
          <a class="nav-item nav-item--disabled" title="Feeding (coming soon)">
            <span class="nav-icon">🌾</span>
            <span class="nav-label">Feeding</span>
            <span class="nav-badge">soon</span>
          </a>
          <a class="nav-item nav-item--disabled" title="Finance (coming soon)">
            <span class="nav-icon">💰</span>
            <span class="nav-label">Finance</span>
            <span class="nav-badge">soon</span>
          </a>
          <a class="nav-item nav-item--disabled" title="Reports (coming soon)">
            <span class="nav-icon">📊</span>
            <span class="nav-label">Reports</span>
            <span class="nav-badge">soon</span>
          </a>
        </nav>

        <!-- Bottom -->
        <div class="sidebar-bottom">
          <div class="nav-item" style="cursor:default">
            <span class="nav-icon">⚙️</span>
            <span class="nav-label">Settings</span>
          </div>
        </div>
      </aside>

      <!-- ── Main ───────────────────────────────────────── -->
      <main class="app-main">
        <!-- Topbar -->
        <header class="app-topbar">
          <div style="flex:1"></div>
          <div class="d-flex align-center gap-3">
            <div class="topbar-tenant">
              <span class="text-xs text-muted">Tenant</span>
              <span class="text-sm fw-500">— connected</span>
            </div>
            <div class="user-avatar">F</div>
          </div>
        </header>

        <!-- Page content -->
        <div class="app-content">
          <router-outlet></router-outlet>
        </div>
      </main>

    </div>
  `,
  styles: [`
    /* Sidebar */
    .sidebar-logo {
      display: flex; align-items: center; gap: 10px;
      padding: 20px 16px;
      border-bottom: 1px solid var(--border-subtle);
      overflow: hidden;
    }
    .logo-icon { font-size: 1.5rem; flex-shrink: 0; }
    .logo-text  { font-size: 1rem; font-weight: 700; color: var(--text-primary); white-space: nowrap; }

    .sidebar-nav {
      flex: 1; padding: 12px 8px;
      display: flex; flex-direction: column; gap: 2px;
      overflow-y: auto;
    }
    .sidebar-bottom { padding: 12px 8px; border-top: 1px solid var(--border-subtle); }

    .nav-item {
      display: flex; align-items: center; gap: 10px;
      padding: 9px 10px; border-radius: 8px;
      font-size: 0.875rem; font-weight: 500;
      color: var(--text-secondary);
      text-decoration: none;
      transition: all var(--transition-fast);
      cursor: pointer;
      position: relative;
      overflow: hidden;

      &:hover:not(.nav-item--disabled) {
        background: var(--bg-hover); color: var(--text-primary);
      }
    }
    .nav-item--active {
      background: var(--bg-active) !important;
      color: var(--color-primary-light) !important;
    }
    .nav-item--disabled {
      opacity: 0.4; cursor: not-allowed;
    }
    .nav-icon  { font-size: 1.1rem; flex-shrink: 0; width: 22px; text-align: center; }
    .nav-label { flex: 1; white-space: nowrap; }
    .nav-badge {
      font-size: 0.6rem; font-weight: 600;
      background: var(--bg-overlay);
      color: var(--text-muted);
      padding: 1px 5px; border-radius: 4px;
      text-transform: uppercase;
    }

    /* Topbar */
    .topbar-tenant {
      display: flex; flex-direction: column;
      align-items: flex-end;
      line-height: 1.2;
    }
    .user-avatar {
      width: 32px; height: 32px;
      border-radius: 50%;
      background: var(--color-primary);
      color: #fff;
      display: flex; align-items: center; justify-content: center;
      font-size: 0.875rem; font-weight: 700;
    }
  `],
})
export class AppComponent {}
