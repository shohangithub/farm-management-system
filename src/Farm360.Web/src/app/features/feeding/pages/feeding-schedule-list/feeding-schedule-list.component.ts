import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FeedingService } from '../../services/feeding.service';
import { FeedingSchedule } from '../../models/feeding.models';
import { CreateScheduleDialogComponent } from '../../components/dialogs/create-schedule-dialog/create-schedule-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-feeding-schedule-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Feeding Schedules"
      description="Manage daily feeding schedules assigned to sheds, pens, and animal batches."
      breadcrumbActiveNode="Feeding Schedules">
      <div actions>
        <button (click)="openCreateScheduleDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> Assign New Schedule
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!isLoading() && schedules().length === 0"
        icon="event_busy"
        title="No schedules assigned"
        description="Assign a ration formula to establish daily feeding routines for your animals."
        actionLabel="Assign Schedule"
        (action)="openCreateScheduleDialog()">
      </app-empty-state>

      <!-- Schedules Grid -->
      <div *ngIf="!isLoading() && schedules().length > 0" class="p-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        @for (s of schedules(); track s.id) {
          <div class="group relative bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 shadow-sm hover:shadow-xl transition-all duration-300 overflow-hidden transform hover:-translate-y-1 flex flex-col justify-between">
            <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-blue-500/5 rotate-[-10deg] pointer-events-none transition-transform duration-500 group-hover:scale-110">schedule</mat-icon>

            <!-- Card Header -->
            <div class="p-5 flex items-start justify-between border-b border-gray-50 dark:border-gray-700/50 relative z-10">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 text-white flex items-center justify-center shadow-md shadow-blue-500/20 group-hover:scale-110 transition-transform duration-300">
                  <mat-icon class="!w-5 !h-5 !text-[20px]">schedule</mat-icon>
                </div>
                <div>
                  <h3 class="font-bold text-gray-900 dark:text-white text-base leading-tight group-hover:text-blue-600 transition-colors">{{ s.title }}</h3>
                  <span class="inline-flex items-center mt-1 text-xs font-semibold text-emerald-600 dark:text-emerald-400">
                    Formula: {{ s.formulaTitle }}
                  </span>
                </div>
              </div>

              <span class="inline-flex items-center px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider shadow-sm"
                [ngClass]="s.isActive ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' : 'bg-gray-50 text-gray-700 border border-gray-200'">
                {{ s.isActive ? 'Active' : 'Inactive' }}
              </span>
            </div>

            <!-- Card Body -->
            <div class="p-5 flex-1 relative z-10">
              <div class="grid grid-cols-2 gap-4 p-3 bg-gray-50/80 dark:bg-gray-900/50 rounded-xl border border-gray-100 dark:border-gray-800">
                <div>
                  <div class="text-[10px] uppercase tracking-wider font-bold text-gray-400">Target Qty</div>
                  <div class="font-bold text-gray-900 dark:text-white text-sm mt-0.5">{{ s.targetQuantityKgPerHead }} kg / head</div>
                </div>
                <div>
                  <div class="text-[10px] uppercase tracking-wider font-bold text-gray-400">Frequency</div>
                  <div class="font-bold text-gray-900 dark:text-white text-sm mt-0.5">{{ s.frequencyName }}</div>
                </div>
              </div>

              @if (s.notes) {
                <p class="text-xs text-gray-500 dark:text-gray-400 mt-3 italic">"{{ s.notes }}"</p>
              }
            </div>

            <!-- Footer Action -->
            <div class="p-3 bg-gray-50/80 dark:bg-gray-800/80 border-t border-gray-100 dark:border-gray-700/50 flex items-center justify-between relative z-10">
              <span class="text-xs text-gray-400 font-medium">Starts: {{ s.startDate }}</span>
              <button (click)="openEditDialog(s)" class="px-3 py-1.5 text-xs font-semibold text-gray-700 dark:text-gray-300 hover:bg-white dark:hover:bg-gray-700 rounded-lg border border-gray-200 dark:border-gray-700 transition-colors shadow-sm inline-flex items-center gap-1">
                <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">edit</mat-icon> Edit Schedule
              </button>
            </div>
          </div>
        }
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeedingScheduleListComponent implements OnInit {
  private readonly feedingService = inject(FeedingService);
  private readonly dialog = inject(MatDialog);

  readonly isLoading = signal(true);
  readonly schedules = signal<FeedingSchedule[]>([]);
  readonly activeFarmId = '00000000-0000-0000-0000-000000000001';

  ngOnInit(): void {
    this.loadSchedules();
  }

  loadSchedules(): void {
    this.isLoading.set(true);
    this.feedingService.getSchedules(this.activeFarmId).subscribe({
      next: (res) => {
        this.schedules.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  openCreateScheduleDialog(): void {
    const dialogRef = this.dialog.open(CreateScheduleDialogComponent, {
      width: '600px',
      data: { farmId: this.activeFarmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadSchedules();
    });
  }

  openEditDialog(schedule: FeedingSchedule): void {
    const dialogRef = this.dialog.open(CreateScheduleDialogComponent, {
      width: '600px',
      data: { schedule, farmId: this.activeFarmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadSchedules();
    });
  }
}
