import { Component, ChangeDetectionStrategy, inject, signal, OnInit, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FeedingService } from '../../services/feeding.service';
import { AnimalFeedingPlan } from '../../models/feeding.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { AssignFeedingPlanDialogComponent } from '../../components/dialogs/assign-feeding-plan-dialog/assign-feeding-plan-dialog.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-animal-feeding-plan-list',
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
      title="Animal Feeding Plans"
      description="View and manage individual feeding plans assigned to animals."
      breadcrumbActiveNode="Feeding Plans">
      <div actions>
        <button (click)="openAssignDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> Assign Plan
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!isLoading() && plans().length === 0"
        icon="assignment"
        title="No Plans Assigned"
        description="No animals are currently assigned to any feeding plans."
        actionLabel="Assign Plan"
        (action)="openAssignDialog()">
      </app-empty-state>

      <!-- Plans Table -->
      <div *ngIf="!isLoading() && plans().length > 0" class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr class="bg-gray-50/80 dark:bg-gray-900/50 text-gray-500 dark:text-gray-400 text-[11px] uppercase tracking-wider font-bold border-b border-gray-200 dark:border-gray-700">
              <th class="px-6 py-4">Animal ID</th>
              <th class="px-6 py-4">Rule Set</th>
              <th class="px-6 py-4">Status</th>
              <th class="px-6 py-4 text-right">Expected Daily Feed</th>
              <th class="px-6 py-4 text-center">Assigned On</th>
              <th class="px-6 py-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100 dark:divide-gray-800 text-sm">
            @for (plan of plans(); track plan.id) {
              <tr class="hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors group">
                <td class="px-6 py-4 font-bold text-gray-900 dark:text-white">
                  <div class="flex items-center gap-2">
                    <div class="w-8 h-8 rounded bg-emerald-50 dark:bg-emerald-900/30 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                      <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">pets</mat-icon>
                    </div>
                    {{ plan.animalTag }}
                  </div>
                </td>
                <td class="px-6 py-4">
                  <div class="font-medium text-gray-900 dark:text-gray-100">{{ plan.ruleSetName }}</div>
                </td>
                <td class="px-6 py-4">
                  <span class="inline-flex items-center px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider shadow-sm"
                    [ngClass]="plan.isActive ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' : 'bg-red-50 text-red-700 border border-red-200'">
                    {{ plan.isActive ? 'Active' : 'Canceled' }}
                  </span>
                </td>
                <td class="px-6 py-4 text-right font-extrabold text-emerald-600 dark:text-emerald-400">
                  {{ plan.expectedDailyFeedKg | number:'1.2-2' }} kg
                </td>
                <td class="px-6 py-4 text-center text-gray-500 dark:text-gray-400">
                  {{ plan.assignedOn | date:'mediumDate' }}
                </td>
                <td class="px-6 py-4 text-right">
                  <button *ngIf="plan.isActive" (click)="cancelPlan(plan)"
                    class="px-3 py-1.5 text-xs font-semibold text-red-700 hover:text-white bg-red-50 hover:bg-red-600 rounded-lg border border-red-200 transition-all shadow-sm inline-flex items-center gap-1">
                    <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">cancel</mat-icon> Cancel
                  </button>
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
export class AnimalFeedingPlanListComponent implements OnInit {
  private readonly feedingService = inject(FeedingService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly contextService = inject(WorkingContextService);
  private readonly destroyRef = inject(DestroyRef);

  readonly isLoading = signal(true);
  readonly plans = signal<AnimalFeedingPlan[]>([]);

  ngOnInit(): void {
    this.contextService.currentFarm$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(farm => {
        if (farm) {
          this.loadPlans(farm.id);
        } else {
          this.plans.set([]);
          this.isLoading.set(false);
        }
      });
  }

  loadPlans(farmId?: string): void {
    farmId = farmId || this.contextService.currentFarmValue?.id;
    if (!farmId) {
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.feedingService.getFeedingPlans(farmId).subscribe({
      next: (res) => {
        this.plans.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  openAssignDialog(): void {
    const dialogRef = this.dialog.open(AssignFeedingPlanDialogComponent, { 
      width: '95vw', 
      maxWidth: '600px' 
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadPlans();
    });
  }

  cancelPlan(plan: AnimalFeedingPlan): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      width: '450px',
      panelClass: ['!rounded-2xl', '!bg-white', 'dark:!bg-gray-900'],
      data: {
        title: 'Cancel Feeding Plan',
        message: 'Are you sure you want to cancel this feeding plan? Future daily entries will no longer be generated.',
        confirmButtonText: 'Cancel Plan',
        cancelButtonText: 'Keep Plan',
        isDestructive: true
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.feedingService.cancelPlan(plan.id).subscribe({
          next: () => {
            this.snackBar.open('Feeding plan canceled', 'Close', { duration: 3000 });
            this.loadPlans();
          },
          error: (err) => {
            this.snackBar.open(err.error?.detail || 'Failed to cancel plan', 'Close', { duration: 5000 });
          }
        });
      }
    });
  }
}

