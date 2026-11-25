import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FarmSummaryCard } from '../../models/dashboard.model';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-farm-summary-cards',
  standalone: true,
  imports: [MoneyPipe, CommonModule, TranslatePipe],
  template: `
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 lg:gap-6">
      <div *ngFor="let card of data" class="bg-white dark:bg-gray-800/80 p-5 rounded-2xl shadow-sm border border-gray-200/70 dark:border-gray-700/60 min-w-0">
        <h3 class="text-sm font-semibold text-gray-900 dark:text-white m-0 truncate">{{card.farmName}}</h3>
        <dl class="mt-3 mb-0 space-y-2">
          <div class="flex justify-between gap-3">
            <dt class="text-sm text-gray-600 dark:text-gray-400">{{ 'Animals on Farm' | t }}</dt>
            <dd class="m-0 text-sm font-semibold tabular-nums text-gray-900 dark:text-white">{{card.animalCount}}</dd>
          </div>
          <div class="flex justify-between gap-3">
            <dt class="text-sm text-gray-600 dark:text-gray-400">{{ 'Sick' | t }}</dt>
            <dd class="m-0 text-sm font-semibold tabular-nums"
                [ngClass]="card.sickCount > 0 ? 'text-rose-700 dark:text-rose-400' : 'text-gray-900 dark:text-white'">{{card.sickCount}}</dd>
          </div>
          <div class="flex justify-between gap-3">
            <dt class="text-sm text-gray-600 dark:text-gray-400">{{ 'Revenue' | t }}</dt>
            <dd class="m-0 text-sm font-semibold tabular-nums text-emerald-700 dark:text-emerald-400">{{card.monthlyRevenue | money:2 }}</dd>
          </div>
        </dl>
      </div>
    </div>
  `
})
export class FarmSummaryCardsComponent {
  @Input() data: FarmSummaryCard[] | null = null;
}
