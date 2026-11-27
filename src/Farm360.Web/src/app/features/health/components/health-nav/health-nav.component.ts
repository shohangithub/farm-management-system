import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

/**
 * One-line section menu shared by every health page. On a phone it scrolls sideways
 * instead of wrapping, so it never pushes the page content down.
 */
@Component({
  selector: 'app-health-nav',
  standalone: true,
  imports: [RouterModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
<nav class="-mx-4 sm:mx-0 mb-5 no-print" [attr.aria-label]="'Health sections' | t">
  <div class="flex gap-1.5 overflow-x-auto md:flex-wrap md:overflow-visible px-4 sm:px-0 pb-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
    @for (link of links; track link.path) {
      <a [routerLink]="'/health/' + link.path" routerLinkActive="" #rla="routerLinkActive"
         [attr.aria-current]="rla.isActive ? 'page' : null"
         class="shrink-0 inline-flex items-center gap-1 h-10 px-3 rounded-full text-[13px] font-semibold border transition-colors whitespace-nowrap"
         [class]="rla.isActive
           ? 'bg-primary-600 border-primary-600 text-white'
           : 'bg-white dark:bg-gray-900 border-gray-200 dark:border-gray-800 text-gray-600 dark:text-gray-300 hover:border-gray-300 hover:text-gray-900 dark:hover:text-white'">
        <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">{{ link.icon }}</mat-icon>
        {{ link.label | t }}
      </a>
    }
  </div>
</nav>
  `
})
export class HealthNavComponent {
  readonly links = [
    { path: 'dashboard', label: 'Overview', icon: 'space_dashboard' },
    { path: 'today', label: "Today's tasks", icon: 'task_alt' },
    { path: 'vaccinations', label: 'Vaccinations', icon: 'vaccines' },
    { path: 'treatments', label: 'Treatments', icon: 'medical_services' },
    { path: 'incidents', label: 'Incidents', icon: 'coronavirus' },
    { path: 'mortality-records', label: 'Deaths', icon: 'heart_broken' },
    { path: 'vet-visits', label: 'Vet Visits', icon: 'local_hospital' },
    { path: 'vaccination-protocols', label: 'Protocols', icon: 'library_books' },
    { path: 'deworming-calendar', label: 'Deworming', icon: 'event_note' },
    { path: 'milk-withdrawal', label: 'Withdrawal', icon: 'block' },
    { path: 'analytics', label: 'Trends', icon: 'insights' },
  ];
}
