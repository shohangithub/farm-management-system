import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { HealthService } from '../../services/health.service';
import { DewormingCalendarDto } from '../../models/health.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-deworming-calendar',
  standalone: true,
  imports: [CommonModule, MatIconModule, PageHeaderComponent, EmptyStateComponent, LoadingComponent],
  template: `
<app-page-header 
  title="Deworming Calendar" 
  description="Manage scheduled deworming and vaccination events."
  icon="event_note" 
  iconColor="text-emerald-600">
  <div actions class="flex gap-2">
    <button class="px-4 py-2 text-sm font-semibold text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 rounded-lg transition-colors shadow-sm flex items-center gap-1.5" (click)="loadEvents()">
      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">refresh</mat-icon> Refresh
    </button>
  </div>
</app-page-header>

<div class="bg-white/80 dark:bg-surface-dark/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
  <app-loading *ngIf="isLoading" [overlay]="true"></app-loading>

  <div class="relative overflow-x-auto">
    <table class="w-full text-sm text-left" *ngIf="events.length > 0">
      <thead class="text-xs text-gray-700 uppercase bg-gray-50 border-b border-gray-100 dark:bg-gray-800 dark:text-gray-400 dark:border-gray-700 font-bold tracking-wider">
        <tr>
          <th scope="col" class="px-6 py-4">Date</th>
          <th scope="col" class="px-6 py-4">Animal</th>
          <th scope="col" class="px-6 py-4">Vaccine / Medicine</th>
          <th scope="col" class="px-6 py-4">Status</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let ev of events" class="border-b border-gray-50 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
          <td class="px-6 py-4 text-gray-600 dark:text-gray-400">
            {{ ev.scheduledDate | date:'mediumDate' }}
          </td>
          <td class="px-6 py-4 font-medium text-gray-900 dark:text-white">
            {{ ev.animalTag }}
          </td>
          <td class="px-6 py-4 text-gray-700 dark:text-gray-300">
            {{ ev.vaccineName }}
          </td>
          <td class="px-6 py-4">
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium" 
                  [ngClass]="{
                    'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400': ev.status === 'Completed' || ev.status === 'Administered',
                    'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-400': ev.status === 'Scheduled',
                    'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400': ev.status === 'Overdue' || ev.status === 'Missed'
                  }">
              {{ ev.status }}
            </span>
          </td>
        </tr>
      </tbody>
    </table>
    
    <app-empty-state 
      *ngIf="!isLoading && events.length === 0"
      icon="event_note"
      title="No Deworming Events"
      description="There are no deworming events scheduled for this farm."
      actionLabel="Refresh"
      (action)="loadEvents()">
    </app-empty-state>
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
