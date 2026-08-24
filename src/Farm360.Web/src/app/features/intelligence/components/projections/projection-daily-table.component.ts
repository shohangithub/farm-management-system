import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { ProfitProjectionResponse } from '../../models/projection.model';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-projection-daily-table',
  standalone: true,
  imports: [CommonModule, DecimalPipe, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6">
      <div class="flex items-center justify-between mb-4">
        <div class="flex items-center gap-3">
          <div class="bg-gradient-to-br from-blue-500 to-indigo-600 text-white flex items-center justify-center p-2 rounded-lg shadow-md shadow-blue-500/20">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">table_rows</mat-icon>
          </div>
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white m-0">Daily Breakdown</h3>
        </div>
      </div>
      
      <div class="overflow-x-auto max-h-[500px]">
        <table class="w-full text-left text-sm text-gray-600 dark:text-gray-300 relative">
          <thead class="text-xs uppercase bg-gray-50 dark:bg-gray-700 text-gray-500 dark:text-gray-400 sticky top-0 z-10 shadow-sm">
            <tr>
              <th scope="col" class="px-4 py-3 rounded-tl-lg">Day</th>
              <th scope="col" class="px-4 py-3">Weight (kg)</th>
              <th scope="col" class="px-4 py-3">Meat Yield (kg)</th>
              <th scope="col" class="px-4 py-3">Feed (kg)</th>
              <th scope="col" class="px-4 py-3">Feed Cost</th>
              <th scope="col" class="px-4 py-3">Cum. Inv.</th>
              <th scope="col" class="px-4 py-3">Meat Val.</th>
              <th scope="col" class="px-4 py-3 rounded-tr-lg">Net Profit</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let day of data()?.days; let i = index" 
                class="border-b border-gray-100 dark:border-gray-800/50 hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors"
                [ngClass]="{'bg-emerald-50/30 dark:bg-emerald-900/10': day.day === data()?.summary?.breakEvenDay}">
              <td class="px-4 py-3 font-medium text-gray-900 dark:text-white">
                {{ day.day }}
                <span *ngIf="day.day === data()?.summary?.breakEvenDay" class="ml-2 inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-400">
                  Break-Even
                </span>
              </td>
              <td class="px-4 py-3">{{ day.liveWeightKg | number:'1.1-2' }}</td>
              <td class="px-4 py-3 text-emerald-600 dark:text-emerald-400">{{ day.meatWeightKg | number:'1.2-2' }}</td>
              <td class="px-4 py-3">{{ day.feedQtyKg | number:'1.1-2' }}</td>
              <td class="px-4 py-3">{{ day.feedCostBdt | number:'1.0-0' }}</td>
              <td class="px-4 py-3">{{ day.totalInvestmentBdt | number:'1.0-0' }}</td>
              <td class="px-4 py-3 font-medium text-indigo-600 dark:text-indigo-400">{{ day.meatValueBdt | number:'1.0-0' }}</td>
              <td class="px-4 py-3 font-medium" 
                  [ngClass]="day.profitLossBdt >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">
                {{ day.profitLossBdt | number:'1.0-0' }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class ProjectionDailyTableComponent {
  data = input<ProfitProjectionResponse | null>();
}
