import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FeedingService } from '../../services/feeding.service';
import { FeedingCycleReconciliation, ReconciliationStatus } from '../../models/feeding.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-reconciliation-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Feeding Reconciliations"
      description="Review and approve daily feed consumption variances to maintain accurate inventory."
      breadcrumbActiveNode="Reconciliations">
      <div actions>
        <button (click)="loadReconciliations()"
          class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">refresh</mat-icon> Refresh
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative min-h-[400px]">
      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!isLoading() && reconciliations().length === 0"
        icon="fact_check"
        title="No Reconciliations Found"
        description="There are no pending or past feeding reconciliations to review."
        actionLabel="Refresh Data"
        (action)="loadReconciliations()">
      </app-empty-state>

      <!-- Reconciliations Table -->
      <div *ngIf="!isLoading() && reconciliations().length > 0" class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr class="bg-gray-50/80 dark:bg-gray-900/50 text-gray-500 dark:text-gray-400 text-[11px] uppercase tracking-wider font-bold border-b border-gray-200 dark:border-gray-700">
              <th class="px-6 py-4">Cycle Date</th>
              <th class="px-6 py-4">Status</th>
              <th class="px-6 py-4 text-right">Expected Total (kg)</th>
              <th class="px-6 py-4 text-right">Actual Total (kg)</th>
              <th class="px-6 py-4 text-right">Variance (kg)</th>
              <th class="px-6 py-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100 dark:divide-gray-800 text-sm">
            @for (rec of reconciliations(); track rec.id) {
              <tr class="hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors">
                <td class="px-6 py-4 font-bold text-gray-900 dark:text-white">
                  {{ rec.cycleDate | date:'mediumDate' }}
                </td>
                <td class="px-6 py-4">
                  <span class="inline-flex items-center px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider shadow-sm"
                    [ngClass]="{
                      'bg-orange-50 text-orange-700 border border-orange-200': rec.status === 'Pending',
                      'bg-blue-50 text-blue-700 border border-blue-200': rec.status === 'Reviewed',
                      'bg-emerald-50 text-emerald-700 border border-emerald-200': rec.status === 'Approved',
                      'bg-red-50 text-red-700 border border-red-200': rec.status === 'Rejected'
                    }">
                    {{ rec.status }}
                  </span>
                </td>
                <td class="px-6 py-4 text-right text-gray-600 dark:text-gray-300">
                  {{ rec.totalExpectedKg | number:'1.2-2' }} kg
                </td>
                <td class="px-6 py-4 text-right font-bold text-gray-900 dark:text-white">
                  {{ rec.totalActualKg | number:'1.2-2' }} kg
                </td>
                <td class="px-6 py-4 text-right font-extrabold"
                  [ngClass]="getVarianceColor(rec.varianceKg)">
                  {{ rec.varianceKg > 0 ? '+' : '' }}{{ rec.varianceKg | number:'1.2-2' }} kg
                </td>
                <td class="px-6 py-4 text-right">
                  <div class="flex items-center justify-end gap-2" *ngIf="rec.status === 'Pending' || rec.status === 'Reviewed'">
                    <button (click)="approve(rec)"
                      class="px-3 py-1.5 text-xs font-semibold text-emerald-700 bg-emerald-50 hover:bg-emerald-600 hover:text-white rounded-lg border border-emerald-200 transition-all shadow-sm inline-flex items-center gap-1">
                      <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">check_circle</mat-icon> Approve
                    </button>
                    <button (click)="reject(rec)"
                      class="px-3 py-1.5 text-xs font-semibold text-red-700 bg-red-50 hover:bg-red-600 hover:text-white rounded-lg border border-red-200 transition-all shadow-sm inline-flex items-center gap-1">
                      <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">cancel</mat-icon> Reject
                    </button>
                  </div>
                  <div *ngIf="rec.status === 'Approved' || rec.status === 'Rejected'" class="text-xs text-gray-500 italic">
                    Processed by Manager
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReconciliationListComponent implements OnInit {
  private readonly feedingService = inject(FeedingService);
  private readonly snackBar = inject(MatSnackBar);

  readonly isLoading = signal(true);
  readonly reconciliations = signal<FeedingCycleReconciliation[]>([]);

  // Placeholder farm ID
  private readonly farmId = '00000000-0000-0000-0000-000000000000';

  ngOnInit(): void {
    this.loadReconciliations();
  }

  loadReconciliations(): void {
    this.isLoading.set(true);
    this.feedingService.getReconciliations(this.farmId).subscribe({
      next: (res) => {
        // Sort pending ones first, then by date descending
        const sorted = res.sort((a, b) => {
          if (a.status === 'Pending' && b.status !== 'Pending') return -1;
          if (a.status !== 'Pending' && b.status === 'Pending') return 1;
          return new Date(b.cycleDate).getTime() - new Date(a.cycleDate).getTime();
        });
        this.reconciliations.set(sorted);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  getVarianceColor(variance: number): string {
    if (variance === 0) return 'text-gray-500 dark:text-gray-400';
    // If we used more feed than expected (negative variance relative to remaining stock, or positive variance relative to expected consumption)
    // Assuming variance = actual - expected. So positive means overfed.
    return variance > 0 ? 'text-red-500' : 'text-emerald-500';
  }

  approve(rec: FeedingCycleReconciliation): void {
    if (!confirm(`Are you sure you want to approve the reconciliation for ${rec.cycleDate}? This will deduct ${rec.totalActualKg} kg from inventory.`)) {
      return;
    }

    this.feedingService.approveReconciliation(rec.id).subscribe({
      next: () => {
        this.snackBar.open('Reconciliation approved', 'Close', { duration: 3000 });
        this.loadReconciliations();
      },
      error: (err) => {
        this.snackBar.open(err.error?.detail || 'Failed to approve', 'Close', { duration: 5000 });
      }
    });
  }

  reject(rec: FeedingCycleReconciliation): void {
    const reason = prompt('Please enter a reason for rejection:');
    if (reason === null) return;
    if (reason.trim() === '') {
      this.snackBar.open('A reason is required to reject a reconciliation', 'Close', { duration: 5000 });
      return;
    }

    this.feedingService.rejectReconciliation(rec.id, reason).subscribe({
      next: () => {
        this.snackBar.open('Reconciliation rejected', 'Close', { duration: 3000 });
        this.loadReconciliations();
      },
      error: (err) => {
        this.snackBar.open(err.error?.detail || 'Failed to reject', 'Close', { duration: 5000 });
      }
    });
  }
}

