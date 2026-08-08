import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FeedingService } from '../../services/feeding.service';
import { FeedConsumptionLog } from '../../models/feeding.models';
import { LogConsumptionDialogComponent } from '../../components/dialogs/log-consumption-dialog/log-consumption-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-consumption-log',
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
      title="Daily Feed Consumption Logs"
      description="Historical feed offered, refusal/wastage, and daily ration expenditure records."
      breadcrumbActiveNode="Consumption Logs">
      <div actions>
        <button (click)="openLogDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">edit_note</mat-icon> Log Daily Consumption
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!isLoading() && logs().length === 0"
        icon="history"
        title="No consumption logs"
        description="Log daily feed distribution to track consumption and costs over time."
        actionLabel="Log Daily Feed"
        (action)="openLogDialog()">
      </app-empty-state>

      <!-- Table View -->
      <div *ngIf="!isLoading() && logs().length > 0" class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr class="bg-gray-50/80 dark:bg-gray-900/50 border-b border-gray-100 dark:border-gray-800 text-[11px] uppercase tracking-wider font-bold text-gray-400">
              <th class="py-3.5 px-4">Log Date</th>
              <th class="py-3.5 px-4">Formula Used</th>
              <th class="py-3.5 px-4">Head Count</th>
              <th class="py-3.5 px-4">Offered (kg)</th>
              <th class="py-3.5 px-4">Wastage (kg)</th>
              <th class="py-3.5 px-4">Net Consumed</th>
              <th class="py-3.5 px-4">Total Cost</th>
              <th class="py-3.5 px-4">Notes</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100 dark:divide-gray-800 text-sm">
            @for (log of logs(); track log.id) {
              <tr class="hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors">
                <td class="py-3.5 px-4 font-semibold text-gray-900 dark:text-white">{{ log.logDate }}</td>
                <td class="py-3.5 px-4 font-semibold text-emerald-600 dark:text-emerald-400">{{ log.formulaTitle }}</td>
                <td class="py-3.5 px-4 text-gray-600 dark:text-gray-400 font-medium">{{ log.headCount }} heads</td>
                <td class="py-3.5 px-4 text-gray-600 dark:text-gray-400">{{ log.totalFeedOfferedKg }} kg</td>
                <td class="py-3.5 px-4 font-medium text-amber-600 dark:text-amber-400">{{ log.totalRefusalKg }} kg</td>
                <td class="py-3.5 px-4 font-bold text-gray-900 dark:text-white">{{ log.netConsumptionKg }} kg</td>
                <td class="py-3.5 px-4 font-bold text-emerald-600 dark:text-emerald-400">৳ {{ log.totalCostBdt | number:'1.0-0' }}</td>
                <td class="py-3.5 px-4 text-xs text-gray-400 max-w-xs truncate">{{ log.notes || '-' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConsumptionLogComponent implements OnInit {
  private readonly feedingService = inject(FeedingService);
  private readonly dialog = inject(MatDialog);

  readonly isLoading = signal(true);
  readonly logs = signal<FeedConsumptionLog[]>([]);
  readonly activeFarmId = '00000000-0000-0000-0000-000000000001';

  ngOnInit(): void {
    this.loadLogs();
  }

  loadLogs(): void {
    this.isLoading.set(true);
    this.feedingService.getConsumptionLogs(this.activeFarmId).subscribe({
      next: (res) => {
        this.logs.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  openLogDialog(): void {
    const dialogRef = this.dialog.open(LogConsumptionDialogComponent, {
      width: '720px',
      data: { farmId: this.activeFarmId }
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadLogs();
    });
  }
}
