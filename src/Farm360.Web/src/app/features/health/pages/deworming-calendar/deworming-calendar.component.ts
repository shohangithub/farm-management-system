import { Component, inject, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { HealthService } from '../../services/health.service';
import { DewormingCalendarDto } from '../../models/health.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { MatDialog } from '@angular/material/dialog';
import { CreateProtocolDialogComponent } from '../../components/dialogs/create-protocol-dialog/create-protocol-dialog.component';

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
    <button class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm flex items-center gap-1.5" (click)="openScheduleDialog()">
      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> Schedule Deworming
    </button>
    <button class="px-4 py-2 text-sm font-semibold text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 rounded-lg transition-colors shadow-sm flex items-center gap-1.5" (click)="loadEvents()">
      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">refresh</mat-icon> Refresh
    </button>
  </div>
</app-page-header>

<div class="bg-white/80 dark:bg-surface-dark/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
  <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

  <div class="relative overflow-x-auto">
    <table class="w-full text-sm text-left" *ngIf="events().length > 0">
      <thead class="text-xs text-gray-700 uppercase bg-gray-50 border-b border-gray-100 dark:bg-gray-800 dark:text-gray-400 dark:border-gray-700 font-bold tracking-wider">
        <tr>
          <th scope="col" class="px-6 py-4">Date</th>
          <th scope="col" class="px-6 py-4">Animal</th>
          <th scope="col" class="px-6 py-4">Vaccine / Medicine</th>
          <th scope="col" class="px-6 py-4">Status</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let ev of events()" class="border-b border-gray-50 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
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
      *ngIf="!isLoading() && events().length === 0"
      icon="event_note"
      title="No Deworming Events"
      description="There are no deworming events scheduled for this farm."
      actionLabel="Refresh"
      (action)="loadEvents()">
    </app-empty-state>
  </div>
</div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DewormingCalendarComponent {
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private dialog = inject(MatDialog);
  
  isLoading = signal(true);
  private refreshTrigger = signal(0);
  private currentFarmId = toSignal(this.contextService.currentFarm$, { initialValue: null });

  private fetchParams = computed(() => ({
    farmId: this.currentFarmId()?.id || '',
    refresh: this.refreshTrigger()
  }));

  private eventsResult = toSignal(
    toObservable(this.fetchParams).pipe(
      tap(() => this.isLoading.set(true)),
      switchMap(({ farmId }) => {
        if (!farmId) return of({ items: [], totalCount: 0 });
        return this.healthService.getDewormingCalendar(farmId).pipe(
          catchError(() => of({ items: [], totalCount: 0 }))
        );
      }),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { items: [], totalCount: 0 } }
  );

  events = computed(() => this.eventsResult().items);

  loadEvents() {
    this.refreshTrigger.update(v => v + 1);
  }

  openScheduleDialog() {
    const dialogRef = this.dialog.open(CreateProtocolDialogComponent, { disableClose: true,
      width: '800px',
      data: {
        title: 'Deworming: ',
        isDeworming: true,
        lockIsDeworming: true,
        steps: [{
          stepName: 'First Dose',
          targetAgeDays: 0,
          vaccineName: '',
          dosageInstruction: ''
        }]
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      // NOTE: Creating protocol doesn't automatically assign it.
      // After protocol creation, the user will need to assign it.
      // We could add a prompt here to navigate to protocol list to assign it.
    });
  }
}
