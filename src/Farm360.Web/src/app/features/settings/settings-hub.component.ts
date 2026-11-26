import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

interface HubLink {
  route: string;
  icon: string;
  title: string;
  description: string;
}

@Component({
  selector: 'app-settings-hub',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, HasPermissionDirective, TranslatePipe, PageHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header
      [title]="'Settings' | t"
      [description]="'Set up your farm, your team and your account.' | t">
    </app-page-header>

    <div class="max-w-5xl space-y-8">

      <!-- Farm setup -->
      <section aria-labelledby="settings-farm" class="space-y-3">
        <h2 id="settings-farm" class="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide m-0">{{ 'Farm setup' | t }}</h2>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <ng-container *ngFor="let link of farmLinks">
            <ng-container *ngTemplateOutlet="card; context: { $implicit: link }"></ng-container>
          </ng-container>
        </div>
      </section>

      <!-- People & account -->
      <section aria-labelledby="settings-account" class="space-y-3">
        <h2 id="settings-account" class="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide m-0">{{ 'People & account' | t }}</h2>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <ng-container *hasPermission="'users.view'">
            <ng-container *ngTemplateOutlet="card; context: { $implicit: teamLink }"></ng-container>
          </ng-container>
          <ng-container *ngFor="let link of accountLinks">
            <ng-container *ngTemplateOutlet="card; context: { $implicit: link }"></ng-container>
          </ng-container>
        </div>
      </section>

    </div>

    <ng-template #card let-link>
      <a [routerLink]="link.route"
         class="group flex items-center gap-4 bg-white dark:bg-gray-800/80 rounded-2xl p-5 shadow-sm border border-gray-200 dark:border-gray-800 hover:border-primary-300 dark:hover:border-primary-500/40 hover:shadow-md transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500">
        <span class="w-11 h-11 shrink-0 rounded-xl bg-primary-50 dark:bg-primary-500/10 text-primary-700 dark:text-primary-400 flex items-center justify-center" aria-hidden="true">
          <mat-icon>{{ link.icon }}</mat-icon>
        </span>
        <span class="flex-1 min-w-0">
          <span class="block text-base font-semibold text-gray-900 dark:text-white">{{ link.title | t }}</span>
          <span class="block text-sm text-gray-500 dark:text-gray-400 mt-0.5">{{ link.description | t }}</span>
        </span>
        <mat-icon class="shrink-0 text-gray-300 dark:text-gray-600 group-hover:text-primary-600 dark:group-hover:text-primary-400 transition-colors" aria-hidden="true">chevron_right</mat-icon>
      </a>
    </ng-template>
  `
})
export class SettingsHubComponent {
  readonly farmLinks: HubLink[] = [
    { route: '/organizations', icon: 'business', title: 'Business & farms', description: 'Your business, branches, farms and sheds.' },
    { route: '/livestock/breeds', icon: 'pets', title: 'Breeds', description: 'The breeds you keep and their growth targets.' },
    { route: '/livestock/batches', icon: 'group_work', title: 'Batches', description: 'Groups of animals you feed and treat together.' },
    { route: '/settings/farm-rules', icon: 'rule', title: 'Farm rules', description: 'Meat prices, animal values and how costs are shared.' },
  ];

  readonly teamLink: HubLink = {
    route: '/settings/team', icon: 'groups', title: 'Team', description: 'Invite people and choose what they can do.'
  };

  readonly accountLinks: HubLink[] = [
    { route: '/settings/profile', icon: 'person_outline', title: 'My Profile', description: 'Your name, email and password.' },
    { route: '/settings/billing', icon: 'workspace_premium', title: 'Subscription & Plans', description: 'Your plan, free trial and invoices.' },
  ];
}
