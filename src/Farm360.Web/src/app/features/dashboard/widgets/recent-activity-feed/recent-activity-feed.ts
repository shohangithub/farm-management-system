import { Component, Input } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { ActivityFeedItem } from '../../models/dashboard.model';

@Component({
  selector: 'app-recent-activity-feed',
  standalone: true,
  imports: [CommonModule, DatePipe, MatIconModule],
  template: `
    <ul *ngIf="data && data.length > 0" class="max-h-96 overflow-y-auto m-0 p-0 list-none divide-y divide-gray-100 dark:divide-gray-700/60">
      <li *ngFor="let item of data" class="flex gap-3 py-3 first:pt-0 last:pb-0">
        <span class="w-2 h-2 mt-1.5 shrink-0 rounded-full bg-primary-500" aria-hidden="true"></span>
        <div class="min-w-0">
          <p class="text-sm text-gray-800 dark:text-gray-200 m-0 break-words"><span class="font-medium text-gray-900 dark:text-white">{{item.userName}}</span> {{item.description}}</p>
          <p class="text-xs text-gray-500 dark:text-gray-400 m-0 mt-1">{{item.timestamp | date:'short'}}</p>
        </div>
      </li>
    </ul>
    <div *ngIf="!data || data.length === 0" class="text-center py-6">
      <div class="w-10 h-10 rounded-full bg-gray-100 dark:bg-gray-700/50 text-gray-500 dark:text-gray-400 flex items-center justify-center mx-auto mb-2" aria-hidden="true">
        <mat-icon class="text-[20px] w-[20px] h-[20px]">history</mat-icon>
      </div>
      <p class="text-sm font-medium text-gray-700 dark:text-gray-300 m-0">No recent activity.</p>
      <p class="text-xs text-gray-500 dark:text-gray-400 m-0 mt-1 max-w-[240px] mx-auto">Work you and your team record (animals, feed, treatments) will show up here.</p>
    </div>
  `
})
export class RecentActivityFeedComponent {
  @Input() data: ActivityFeedItem[] | null = null;
}
