import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { FeedingService } from '../../services/feeding.service';
import { FcrAnalytics, FeedConsumptionLog, FeedingSchedule } from '../../models/feeding.models';
import { LogConsumptionDialogComponent } from '../../components/dialogs/log-consumption-dialog/log-consumption-dialog.component';
import { CreateScheduleDialogComponent } from '../../components/dialogs/create-schedule-dialog/create-schedule-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-feeding-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Smart Feeding & Nutrition Intelligence"
      description="Optimized ration formulation, daily consumption tracking, and Feed Conversion Ratio (FCR) analytics."
      breadcrumbActiveNode="Feeding Dashboard">
      <div actions class="flex items-center gap-2">
        <button (click)="openLogConsumptionDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">edit_note</mat-icon> Log Daily Feed
        </button>
        <button (click)="openCreateScheduleDialog()"
          class="px-4 py-2 text-sm font-semibold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 hover:bg-emerald-100 dark:hover:bg-emerald-900/50 border border-emerald-200 dark:border-emerald-800 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">schedule</mat-icon> Assign Schedule
        </button>
      </div>
    </app-page-header>

    <div class="space-y-6">
      <!-- KPI Summary Cards Grid -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl p-5 rounded-2xl border border-gray-100 dark:border-gray-800/50 shadow-sm flex items-center gap-4 relative overflow-hidden group">
          <mat-icon class="absolute -right-3 -bottom-3 text-[80px] text-emerald-500/5 rotate-[-10deg] pointer-events-none">scale</mat-icon>
          <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20 group-hover:scale-110 transition-transform duration-300">
            <mat-icon class="!w-6 !h-6 !text-[24px]">scale</mat-icon>
          </div>
          <div>
            <div class="text-[11px] uppercase tracking-wider font-bold text-gray-400">Total Consumed</div>
            <div class="text-2xl font-extrabold text-gray-900 dark:text-white mt-0.5">
              {{ fcrAnalytics()?.totalFeedConsumedKg || 0 | number:'1.0-1' }} <span class="text-xs font-medium text-gray-400">kg</span>
            </div>
          </div>
        </div>

        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl p-5 rounded-2xl border border-gray-100 dark:border-gray-800/50 shadow-sm flex items-center gap-4 relative overflow-hidden group">
          <mat-icon class="absolute -right-3 -bottom-3 text-[80px] text-teal-500/5 rotate-[-10deg] pointer-events-none">analytics</mat-icon>
          <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-teal-500 to-cyan-600 text-white flex items-center justify-center shadow-md shadow-teal-500/20 group-hover:scale-110 transition-transform duration-300">
            <mat-icon class="!w-6 !h-6 !text-[24px]">analytics</mat-icon>
          </div>
          <div>
            <div class="text-[11px] uppercase tracking-wider font-bold text-gray-400">FCR Ratio</div>
            <div class="text-2xl font-extrabold text-gray-900 dark:text-white mt-0.5">
              {{ fcrAnalytics()?.fcrValue || 0 }} <span class="text-[10px] text-emerald-600 dark:text-emerald-400 font-semibold">(Target &lt; 7.0)</span>
            </div>
          </div>
        </div>

        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl p-5 rounded-2xl border border-gray-100 dark:border-gray-800/50 shadow-sm flex items-center gap-4 relative overflow-hidden group">
          <mat-icon class="absolute -right-3 -bottom-3 text-[80px] text-amber-500/5 rotate-[-10deg] pointer-events-none">payments</mat-icon>
          <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-amber-500 to-orange-600 text-white flex items-center justify-center shadow-md shadow-amber-500/20 group-hover:scale-110 transition-transform duration-300">
            <mat-icon class="!w-6 !h-6 !text-[24px]">payments</mat-icon>
          </div>
          <div>
            <div class="text-[11px] uppercase tracking-wider font-bold text-gray-400">Feed Expenditure</div>
            <div class="text-2xl font-extrabold text-gray-900 dark:text-white mt-0.5">
              ৳ {{ fcrAnalytics()?.totalFeedCostBdt || 0 | number:'1.0-0' }}
            </div>
          </div>
        </div>

        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl p-5 rounded-2xl border border-gray-100 dark:border-gray-800/50 shadow-sm flex items-center gap-4 relative overflow-hidden group">
          <mat-icon class="absolute -right-3 -bottom-3 text-[80px] text-blue-500/5 rotate-[-10deg] pointer-events-none">trending_up</mat-icon>
          <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 text-white flex items-center justify-center shadow-md shadow-blue-500/20 group-hover:scale-110 transition-transform duration-300">
            <mat-icon class="!w-6 !h-6 !text-[24px]">trending_up</mat-icon>
          </div>
          <div>
            <div class="text-[11px] uppercase tracking-wider font-bold text-gray-400">Cost / kg Gain</div>
            <div class="text-2xl font-extrabold text-gray-900 dark:text-white mt-0.5">
              ৳ {{ fcrAnalytics()?.costPerKgGainBdt || 0 | number:'1.0-1' }}
            </div>
          </div>
        </div>
      </div>

      <!-- Quick Module Tabs -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <a routerLink="../ingredients" class="p-4 bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl border border-gray-100 dark:border-gray-800/50 hover:border-emerald-500 transition-all flex items-center justify-between group shadow-sm">
          <div class="flex items-center gap-3">
            <div class="w-9 h-9 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 flex items-center justify-center group-hover:scale-110 transition-transform">
              <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">restaurant</mat-icon>
            </div>
            <span class="font-semibold text-gray-900 dark:text-white text-sm">Ingredients Catalog</span>
          </div>
          <mat-icon class="text-gray-400 group-hover:translate-x-1 transition-transform !text-[18px] !w-[18px] !h-[18px]">chevron_right</mat-icon>
        </a>

        <a routerLink="../formulas" class="p-4 bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl border border-gray-100 dark:border-gray-800/50 hover:border-emerald-500 transition-all flex items-center justify-between group shadow-sm">
          <div class="flex items-center gap-3">
            <div class="w-9 h-9 rounded-xl bg-teal-50 dark:bg-teal-950/40 text-teal-600 flex items-center justify-center group-hover:scale-110 transition-transform">
              <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">science</mat-icon>
            </div>
            <span class="font-semibold text-gray-900 dark:text-white text-sm">Ration Formulas</span>
          </div>
          <mat-icon class="text-gray-400 group-hover:translate-x-1 transition-transform !text-[18px] !w-[18px] !h-[18px]">chevron_right</mat-icon>
        </a>

        <a routerLink="../schedules" class="p-4 bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl border border-gray-100 dark:border-gray-800/50 hover:border-emerald-500 transition-all flex items-center justify-between group shadow-sm">
          <div class="flex items-center gap-3">
            <div class="w-9 h-9 rounded-xl bg-blue-50 dark:bg-blue-950/40 text-blue-600 flex items-center justify-center group-hover:scale-110 transition-transform">
              <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">schedule</mat-icon>
            </div>
            <span class="font-semibold text-gray-900 dark:text-white text-sm">Feeding Schedules</span>
          </div>
          <mat-icon class="text-gray-400 group-hover:translate-x-1 transition-transform !text-[18px] !w-[18px] !h-[18px]">chevron_right</mat-icon>
        </a>

        <a routerLink="../logs" class="p-4 bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl border border-gray-100 dark:border-gray-800/50 hover:border-emerald-500 transition-all flex items-center justify-between group shadow-sm">
          <div class="flex items-center gap-3">
            <div class="w-9 h-9 rounded-xl bg-purple-50 dark:bg-purple-950/40 text-purple-600 flex items-center justify-center group-hover:scale-110 transition-transform">
              <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">history</mat-icon>
            </div>
            <span class="font-semibold text-gray-900 dark:text-white text-sm">Feeding Records</span>
          </div>
          <mat-icon class="text-gray-400 group-hover:translate-x-1 transition-transform !text-[18px] !w-[18px] !h-[18px]">chevron_right</mat-icon>
        </a>
      </div>

      <!-- Content Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Active Schedules -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl border border-gray-100 dark:border-gray-800/50 p-6 shadow-sm relative overflow-hidden">
          <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

          <div class="flex items-center justify-between mb-4">
            <h2 class="font-bold text-gray-900 dark:text-white text-base flex items-center gap-2">
              <mat-icon class="text-emerald-600 !text-[20px] !w-[20px] !h-[20px]">schedule</mat-icon> Active Feeding Schedules
            </h2>
            <a routerLink="../schedules" class="text-xs font-semibold text-emerald-600 hover:underline">View All</a>
          </div>

          <app-empty-state
            *ngIf="!isLoading() && activeSchedules().length === 0"
            icon="event_busy"
            title="No active schedules"
            description="Assign a ration formula to establish daily feeding routines."
            actionLabel="Assign Schedule"
            (action)="openCreateScheduleDialog()">
          </app-empty-state>

          <div *ngIf="!isLoading() && activeSchedules().length > 0" class="space-y-3">
            @for (schedule of activeSchedules(); track schedule.id) {
              <div class="p-3.5 rounded-xl bg-gray-50/80 dark:bg-gray-900/50 border border-gray-100 dark:border-gray-800 flex items-center justify-between">
                <div>
                  <div class="font-semibold text-gray-900 dark:text-white text-sm">{{ schedule.title }}</div>
                  <div class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                    Formula: <strong class="text-gray-700 dark:text-gray-300">{{ schedule.formulaTitle }}</strong> • {{ schedule.targetQuantityKgPerHead }} kg/head
                  </div>
                </div>
                <span class="px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800">
                  {{ schedule.frequencyName }}
                </span>
              </div>
            }
          </div>
        </div>

        <!-- Recent Logs -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl border border-gray-100 dark:border-gray-800/50 p-6 shadow-sm relative overflow-hidden">
          <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

          <div class="flex items-center justify-between mb-4">
            <h2 class="font-bold text-gray-900 dark:text-white text-base flex items-center gap-2">
              <mat-icon class="text-emerald-600 !text-[20px] !w-[20px] !h-[20px]">history</mat-icon> Recent Daily Logs
            </h2>
            <a routerLink="../logs" class="text-xs font-semibold text-emerald-600 hover:underline">View All</a>
          </div>

          <app-empty-state
            *ngIf="!isLoading() && recentLogs().length === 0"
            icon="history_toggle_off"
            title="No daily logs"
            description="Start logging daily feed distribution to track consumption and costs."
            actionLabel="Log Daily Feed"
            (action)="openLogConsumptionDialog()">
          </app-empty-state>

          <div *ngIf="!isLoading() && recentLogs().length > 0" class="space-y-3">
            @for (log of recentLogs(); track log.id) {
              <div class="p-3.5 rounded-xl bg-gray-50/80 dark:bg-gray-900/50 border border-gray-100 dark:border-gray-800 flex items-center justify-between">
                <div>
                  <div class="font-semibold text-gray-900 dark:text-white text-sm flex items-center gap-2">
                    <span>{{ log.logDate }}</span>
                    <span class="text-xs font-normal text-gray-400">({{ log.headCount }} heads)</span>
                  </div>
                  <div class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                    Offered: {{ log.totalFeedOfferedKg }} kg | Wastage: {{ log.totalRefusalKg }} kg
                  </div>
                </div>
                <div class="text-right">
                  <div class="font-bold text-emerald-600 dark:text-emerald-400 text-sm">৳ {{ log.totalCostBdt | number:'1.0-0' }}</div>
                  <div class="text-[11px] text-gray-400 font-medium">{{ log.netConsumptionKg }} kg net</div>
                </div>
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeedingDashboardComponent {
  private readonly feedingService = inject(FeedingService);
  private readonly dialog = inject(MatDialog);
  private readonly contextService = inject(WorkingContextService);

  readonly isLoading = signal(true);
  readonly fcrAnalytics = signal<FcrAnalytics | null>(null);
  readonly activeSchedules = signal<FeedingSchedule[]>([]);
  readonly recentLogs = signal<FeedConsumptionLog[]>([]);

  readonly activeFarmId = signal<string | null>(null);

  constructor() {
    this.contextService.currentFarm$.pipe(
      takeUntilDestroyed()
    ).subscribe(farm => {
      const farmId = farm?.id || null;
      this.activeFarmId.set(farmId);
      if (farmId) {
        this.loadDashboardData(farmId);
      } else {
        this.fcrAnalytics.set(null);
        this.activeSchedules.set([]);
        this.recentLogs.set([]);
        this.isLoading.set(false);
      }
    });
  }

  loadDashboardData(farmId: string): void {
    this.isLoading.set(true);

    this.feedingService.getFcrAnalytics(farmId).subscribe({
      next: (res) => this.fcrAnalytics.set(res),
      error: () => {}
    });

    this.feedingService.getSchedules(farmId).subscribe({
      next: (res) => this.activeSchedules.set(res.slice(0, 5)),
      error: () => {}
    });

    this.feedingService.getConsumptionLogs(farmId).subscribe({
      next: (res) => {
        this.recentLogs.set(res.slice(0, 5));
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  openLogConsumptionDialog(): void {
    const farmId = this.activeFarmId();
    if (!farmId) return;

    const dialogRef = this.dialog.open(LogConsumptionDialogComponent, {
      width: '720px',
      data: { farmId }
    });

    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadDashboardData(farmId);
    });
  }

  openCreateScheduleDialog(): void {
    const farmId = this.activeFarmId();
    if (!farmId) return;
    
    const dialogRef = this.dialog.open(CreateScheduleDialogComponent, {
      width: '720px',
      data: { farmId }
    });

    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadDashboardData(farmId);
    });
  }
}
