import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from './core/services/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet],
  template: `
    @if (!authService.isInitialized()) {
      <div class="fixed inset-0 bg-gray-900 text-white flex flex-col items-center justify-center z-50">
        <div class="w-12 h-12 border-4 border-emerald-500 border-t-transparent rounded-full animate-spin mb-4"></div>
        <div class="text-xs font-semibold tracking-widest uppercase text-emerald-400">Restoring Farm360 Session...</div>
      </div>
    } @else {
      <router-outlet></router-outlet>
    }
  `
})
export class AppComponent {
  readonly authService = inject(AuthService);
}
