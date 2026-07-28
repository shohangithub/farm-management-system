import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { HealthService } from '../../services/health.service';
import { MilkWithdrawalDto } from '../../models/health.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-milk-withdrawal',
  standalone: true,
  imports: [CommonModule, MatIconModule, PageHeaderComponent, EmptyStateComponent, LoadingComponent],
  template: `
<app-page-header 
  title="Milk Withdrawal Tracking" 
  description="Monitor milk withdrawal periods after medical treatments."
  icon="water_drop" 
  iconColor="text-amber-600">
  <div actions class="flex gap-2">
    <button class="px-4 py-2 text-sm font-semibold text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 rounded-lg transition-colors shadow-sm flex items-center gap-1.5" (click)="loadWithdrawals()">
      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">refresh</mat-icon> Refresh
    </button>
  </div>
</app-page-header>

<!-- Info Alert -->
<div class="mb-6 bg-amber-50 dark:bg-amber-900/30 text-amber-800 dark:text-amber-400 p-4 rounded-xl border border-amber-200 dark:border-amber-800 flex items-start shadow-sm">
  <mat-icon class="mr-3 mt-0.5">warning</mat-icon>
  <div>
    <div class="font-medium mb-1">Attention</div>
    <div class="text-sm">The animals listed below are currently under a milk withdrawal period due to medical treatment. Their milk must NOT be added to the bulk tank.</div>
  </div>
</div>

<div class="bg-white/80 dark:bg-surface-dark/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
  <app-loading *ngIf="isLoading" [overlay]="true"></app-loading>

  <div class="relative overflow-x-auto">
    <table class="w-full text-sm text-left" *ngIf="withdrawals.length > 0">
      <thead class="text-xs text-gray-700 uppercase bg-gray-50 border-b border-gray-100 dark:bg-gray-800 dark:text-gray-400 dark:border-gray-700 font-bold tracking-wider">
        <tr>
          <th scope="col" class="px-6 py-4">Animal Tag</th>
          <th scope="col" class="px-6 py-4">Medication</th>
          <th scope="col" class="px-6 py-4">Treatment Started</th>
          <th scope="col" class="px-6 py-4 text-center">Withdrawal Days</th>
          <th scope="col" class="px-6 py-4">Safe To Milk Date</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let w of withdrawals" class="border-b border-gray-50 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
          <td class="px-6 py-4 font-medium text-gray-900 dark:text-white">
            {{ w.animalTag }}
          </td>
          <td class="px-6 py-4 text-gray-700 dark:text-gray-300">
            {{ w.medicationName }}
          </td>
          <td class="px-6 py-4 text-gray-600 dark:text-gray-400">
            {{ w.treatmentStartDate | date:'mediumDate' }}
          </td>
          <td class="px-6 py-4 text-center">
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400">
              {{ w.milkWithdrawalDays }} Days
            </span>
          </td>
          <td class="px-6 py-4 text-rose-600 dark:text-rose-400 font-medium">
            {{ w.safeToMilkDate | date:'mediumDate' }}
          </td>
        </tr>
      </tbody>
    </table>
    
    <app-empty-state 
      *ngIf="!isLoading && withdrawals.length === 0"
      icon="check_circle"
      title="No Active Withdrawals"
      description="There are currently no animals under a milk withdrawal period."
      actionLabel="Refresh"
      (action)="loadWithdrawals()">
    </app-empty-state>
  </div>
</div>
  `
})
export class MilkWithdrawalComponent implements OnInit {
  private healthService = inject(HealthService);
  
  withdrawals: MilkWithdrawalDto[] = [];
  isLoading = false;
  // Hardcoded MVP farm ID
  private farmId = '11111111-1111-1111-1111-111111111111';

  ngOnInit() {
    this.loadWithdrawals();
  }

  loadWithdrawals() {
    this.isLoading = true;
    this.healthService.getMilkWithdrawals(this.farmId).subscribe({
      next: (res) => {
        this.withdrawals = res;
        this.isLoading = false;
      },
      error: () => this.isLoading = false
    });
  }
}
