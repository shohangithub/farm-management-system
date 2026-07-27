import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HealthService } from '../../services/health.service';
import { DewormingCalendarDto } from '../../models/health.models';

@Component({
  selector: 'app-deworming-calendar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="px-4 sm:px-6 lg:px-8 py-8 w-full max-w-9xl mx-auto">
      <div class="sm:flex sm:justify-between sm:items-center mb-8">
        <div class="mb-4 sm:mb-0">
          <h1 class="text-2xl md:text-3xl text-slate-800 dark:text-slate-100 font-bold">Deworming Calendar ✨</h1>
        </div>
      </div>

      <div class="bg-white dark:bg-slate-800 shadow-lg rounded-sm border border-slate-200 dark:border-slate-700">
        <header class="px-5 py-4 border-b border-slate-100 dark:border-slate-700">
          <h2 class="font-semibold text-slate-800 dark:text-slate-100">Scheduled Events</h2>
        </header>
        <div class="p-3">
          <div class="overflow-x-auto">
            <table class="table-auto w-full dark:text-slate-300">
              <thead class="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400 bg-slate-50 dark:bg-slate-900/20">
                <tr>
                  <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Date</div></th>
                  <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Animal</div></th>
                  <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Vaccine/Medicine</div></th>
                  <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Status</div></th>
                </tr>
              </thead>
              <tbody class="text-sm divide-y divide-slate-200 dark:divide-slate-700">
                <tr *ngFor="let ev of events">
                  <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                    <div>{{ ev.scheduledDate | date:'mediumDate' }}</div>
                  </td>
                  <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                    <div class="font-medium text-slate-800 dark:text-slate-100">{{ ev.animalTag }}</div>
                  </td>
                  <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                    <div class="text-left">{{ ev.vaccineName }}</div>
                  </td>
                  <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                    <div class="inline-flex font-medium rounded-full text-center px-2.5 py-0.5 bg-emerald-100 text-emerald-600 dark:bg-emerald-400/30 dark:text-emerald-400">
                      {{ ev.status }}
                    </div>
                  </td>
                </tr>
                <tr *ngIf="events.length === 0 && !isLoading">
                  <td colspan="4" class="px-2 first:pl-5 last:pr-5 py-8 text-center text-slate-500 dark:text-slate-400">
                    No deworming events scheduled.
                  </td>
                </tr>
                <tr *ngIf="isLoading">
                  <td colspan="4" class="px-2 first:pl-5 last:pr-5 py-8 text-center text-slate-500 dark:text-slate-400">
                    Loading calendar...
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
export class DewormingCalendarComponent implements OnInit {
  private healthService = inject(HealthService);
  
  events: DewormingCalendarDto[] = [];
  isLoading = false;
  // Hardcoded MVP farm ID
  private farmId = '11111111-1111-1111-1111-111111111111';

  ngOnInit() {
    this.loadEvents();
  }

  loadEvents() {
    this.isLoading = true;
    this.healthService.getDewormingCalendar(this.farmId).subscribe({
      next: (res) => {
        this.events = res.items;
        this.isLoading = false;
      },
      error: () => this.isLoading = false
    });
  }
}
