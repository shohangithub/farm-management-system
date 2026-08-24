import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FarmSummaryCard } from '../../models/dashboard.model';

@Component({
  selector: 'app-farm-summary-cards',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
      <div *ngFor="let card of data" class="bg-white p-4 rounded-lg shadow-sm border border-gray-100">
        <h3 class="text-gray-500 text-sm font-medium">{{card.farmName}}</h3>
        <div class="mt-2 space-y-1">
          <div class="flex justify-between">
            <span class="text-sm text-gray-600">Total Animals</span>
            <span class="text-sm font-semibold text-gray-900">{{card.animalCount}}</span>
          </div>
          <div class="flex justify-between">
            <span class="text-sm text-gray-600">Sick</span>
            <span class="text-sm font-semibold text-red-600">{{card.sickCount}}</span>
          </div>
          <div class="flex justify-between">
            <span class="text-sm text-gray-600">Revenue</span>
            <span class="text-sm font-semibold text-emerald-600">৳{{card.monthlyRevenue | number}}</span>
          </div>
        </div>
      </div>
    </div>
  `
})
export class FarmSummaryCardsComponent {
  @Input() data: FarmSummaryCard[] | null = null;
}
