import { Component, Input } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivityFeedItem } from '../../models/dashboard.model';

@Component({
  selector: 'app-recent-activity-feed',
  standalone: true,
  imports: [CommonModule, DatePipe],
  template: `
    <div class="space-y-4 max-h-96 overflow-y-auto">
      <div *ngFor="let item of data" class="flex gap-4 p-3 bg-gray-50 rounded-lg">
        <div class="w-2 h-2 mt-2 rounded-full bg-blue-500"></div>
        <div>
          <p class="text-sm text-gray-900"><span class="font-medium">{{item.userName}}</span> {{item.description}}</p>
          <p class="text-xs text-gray-500 mt-1">{{item.timestamp | date:'short'}}</p>
        </div>
      </div>
      <div *ngIf="!data || data.length === 0" class="text-sm text-gray-500 text-center py-4">
        No recent activity.
      </div>
    </div>
  `
})
export class RecentActivityFeedComponent {
  @Input() data: ActivityFeedItem[] | null = null;
}
