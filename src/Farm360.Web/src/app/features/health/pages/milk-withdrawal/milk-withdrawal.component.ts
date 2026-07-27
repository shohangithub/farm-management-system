import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HealthService } from '../../services/health.service';
import { MilkWithdrawalDto } from '../../models/health.models';

@Component({
  selector: 'app-milk-withdrawal',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="px-4 sm:px-6 lg:px-8 py-8 w-full max-w-9xl mx-auto">
      <div class="sm:flex sm:justify-between sm:items-center mb-8">
        <div class="mb-4 sm:mb-0">
          <h1 class="text-2xl md:text-3xl text-slate-800 dark:text-slate-100 font-bold">Milk Withdrawal Tracking ✨</h1>
        </div>
      </div>

      <!-- Info Alert -->
      <div class="mb-6 bg-amber-50 dark:bg-amber-900/30 text-amber-800 dark:text-amber-400 p-4 rounded-sm border border-amber-200 dark:border-amber-800 flex items-start">
        <svg class="w-6 h-6 fill-current shrink-0 mr-3" viewBox="0 0 24 24">
          <path d="M12 2a10 10 0 1010 10A10.011 10.011 0 0012 2zm0 18a8 8 0 118-8 8.009 8.009 0 01-8 8z" />
          <path d="M12 7a1 1 0 00-1 1v5a1 1 0 002 0V8a1 1 0 00-1-1zm0 8a1 1 0 101 1 1.002 1.002 0 00-1-1z" />
        </svg>
        <div>
          <div class="font-medium mb-1">Attention</div>
          <div class="text-sm">The animals listed below are currently under a milk withdrawal period due to medical treatment. Their milk must NOT be added to the bulk tank.</div>
        </div>
      </div>

      <div class="bg-white dark:bg-slate-800 shadow-lg rounded-sm border border-slate-200 dark:border-slate-700">
        <header class="px-5 py-4 border-b border-slate-100 dark:border-slate-700">
          <h2 class="font-semibold text-slate-800 dark:text-slate-100">Active Withdrawal Periods <span class="text-slate-400 dark:text-slate-500 font-medium">{{ withdrawals.length }}</span></h2>
        </header>
        <div class="p-3">
          <div class="overflow-x-auto">
            <table class="table-auto w-full dark:text-slate-300">
              <thead class="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400 bg-slate-50 dark:bg-slate-900/20">
                <tr>
                  <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Animal Tag</div></th>
                  <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Medication</div></th>
                  <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Treatment Started</div></th>
                  <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-center">Withdrawal Days</div></th>
                  <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Safe To Milk Date</div></th>
                </tr>
              </thead>
              <tbody class="text-sm divide-y divide-slate-200 dark:divide-slate-700">
                <tr *ngFor="let w of withdrawals">
                  <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                    <div class="font-medium text-slate-800 dark:text-slate-100">{{ w.animalTag }}</div>
                  </td>
                  <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                    <div class="text-left">{{ w.medicationName }}</div>
                  </td>
                  <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                    <div class="text-left">{{ w.treatmentStartDate | date:'mediumDate' }}</div>
                  </td>
                  <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                    <div class="text-center font-medium">{{ w.milkWithdrawalDays }}</div>
                  </td>
                  <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                    <div class="text-left font-medium text-rose-500">{{ w.safeToMilkDate | date:'mediumDate' }}</div>
                  </td>
                </tr>
                <tr *ngIf="withdrawals.length === 0 && !isLoading">
                  <td colspan="5" class="px-2 first:pl-5 last:pr-5 py-8 text-center text-slate-500 dark:text-slate-400">
                    No animals currently under milk withdrawal.
                  </td>
                </tr>
                <tr *ngIf="isLoading">
                  <td colspan="5" class="px-2 first:pl-5 last:pr-5 py-8 text-center text-slate-500 dark:text-slate-400">
                    Loading records...
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
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
