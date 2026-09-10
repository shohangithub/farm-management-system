import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of } from 'rxjs';

import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import {
  ConsumableUsagePlan,
  ConsumableUsagePlanStatus,
  ConsumableUsagePlanStatusNames
} from '../../models/inventory.models';
import { ConsumablePlanDialogComponent } from '../../components/dialogs/consumable-plan-dialog/consumable-plan-dialog.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-consumable-plan-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Daily Consumable Plans"
      description="Define recurring daily supply plans (shampoo, mosquito coils, Dettol, sanitation chemicals) to automate stock tracking and costs."
      breadcrumbActiveNode="Consumable Plans">
      <div actions class="flex items-center gap-2">
        <a routerLink="/inventory/daily-consumables"
          class="px-3.5 py-2 text-xs font-semibold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 rounded-xl border border-gray-200 dark:border-gray-700 transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-teal-500">today</mat-icon> Today's Consumables
        </a>
        <button (click)="openCreatePlanDialog()"
          class="px-4 py-2 text-xs font-semibold text-white bg-teal-600 hover:bg-teal-700 rounded-xl transition-all shadow-md shadow-teal-500/20 inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> New Consumable Plan
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative min-h-[400px]">
      <app-loading *ngIf="loading()" [overlay]="true"></app-loading>

      <!-- Filters Toolbar -->
      <div class="p-4 border-b border-gray-100 dark:border-gray-800 flex flex-col sm:flex-row items-center gap-3 bg-gray-50/50 dark:bg-gray-900/30">
        <div class="relative w-full sm:w-72">
          <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 !text-[18px] !w-[18px] !h-[18px]">search</mat-icon>
          <input [ngModel]="searchTerm()" (ngModelChange)="onSearchChange($event)"
                 type="text" placeholder="Search plans..."
                 class="w-full pl-9 pr-4 py-2 text-xs rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-teal-500 shadow-sm" />
        </div>

        <div class="flex items-center gap-2 w-full sm:w-auto sm:ml-auto">
          <select [ngModel]="selectedStatus()" (ngModelChange)="onStatusChange($event)"
                  class="px-3 py-2 text-xs rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-teal-500 shadow-sm">
            <option value="">All Statuses</option>
            <option [value]="ConsumableUsagePlanStatus.Active">Active</option>
            <option [value]="ConsumableUsagePlanStatus.Paused">Paused</option>
            <option [value]="ConsumableUsagePlanStatus.Completed">Completed</option>
          </select>
        </div>
      </div>

      <!-- Plans Cards Grid -->
      <div class="p-4 sm:p-6">
        @if (!loading() && plans().length === 0) {
          <app-empty-state
            icon="cleaning_services"
            title="No Consumable Plans Found"
            description="Create your first recurring consumable plan to start tracking daily items like shampoo, coils, and cleaning supplies."
            actionLabel="Create Plan"
            (action)="openCreatePlanDialog()">
          </app-empty-state>
        } @else {
          <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
            @for (plan of plans(); track plan.id) {
              <div class="bg-white dark:bg-gray-800/90 rounded-2xl border border-gray-100 dark:border-gray-700/60 shadow-sm hover:shadow-md hover:border-teal-500/30 transition-all flex flex-col relative overflow-hidden group">
                
                <!-- Watermark Background Icon -->
                <mat-icon class="absolute -right-4 -bottom-4 text-[100px] !w-[100px] !h-[100px] text-teal-500/5 rotate-[-10deg] pointer-events-none">cleaning_services</mat-icon>

                <!-- Card Header -->
                <div class="p-4 border-b border-gray-100 dark:border-gray-700/50 flex items-start justify-between relative z-10">
                  <div class="flex items-start gap-3">
                    <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-teal-500 to-emerald-600 text-white flex items-center justify-center shadow-md shadow-teal-500/20 shrink-0">
                      <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">sanitizer</mat-icon>
                    </div>
                    <div>
                      <h3 class="font-bold text-gray-900 dark:text-white text-sm leading-snug group-hover:text-teal-600 dark:group-hover:text-teal-400 transition-colors m-0">
                        {{ plan.name }}
                      </h3>
                      <p class="text-[11px] text-gray-400 dark:text-gray-500 mt-0.5 mb-0">
                        {{ plan.itemsCount }} item{{ plan.itemsCount !== 1 ? 's' : '' }} • Est. ৳{{ plan.totalDailyEstimatedCostBdt | number:'1.2-2' }} / day
                      </p>
                    </div>
                  </div>

                  <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider"
                    [ngClass]="{
                      'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800': plan.status === ConsumableUsagePlanStatus.Active,
                      'bg-amber-50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-400 border border-amber-200 dark:border-amber-800': plan.status === ConsumableUsagePlanStatus.Paused,
                      'bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400 border border-gray-200 dark:border-gray-700': plan.status === ConsumableUsagePlanStatus.Completed
                    }">
                    {{ plan.statusName }}
                  </span>
                </div>

                <!-- Card Body (Items preview) -->
                <div class="p-4 flex-1 relative z-10 space-y-2">
                  @if (plan.description) {
                    <p class="text-xs text-gray-500 dark:text-gray-400 mb-2 italic line-clamp-2">{{ plan.description }}</p>
                  }

                  <div class="space-y-1.5 max-h-32 overflow-y-auto custom-scrollbar pr-1">
                    @for (item of plan.items; track item.id) {
                      <div class="flex items-center justify-between text-xs py-1 px-2 rounded-lg bg-gray-50 dark:bg-gray-900/40 border border-gray-100 dark:border-gray-800">
                        <span class="font-medium text-gray-700 dark:text-gray-300 truncate max-w-[150px]">{{ item.itemName }}</span>
                        <div class="flex items-center gap-1.5 shrink-0">
                          <span class="font-bold text-teal-600 dark:text-teal-400">{{ item.plannedQuantityPerDay }} {{ item.unitOfMeasure }}</span>
                          <span class="text-[10px] text-gray-400">/day</span>
                        </div>
                      </div>
                    }
                  </div>
                </div>

                <!-- Card Footer Actions -->
                <div class="p-3 bg-gray-50/80 dark:bg-gray-800/80 border-t border-gray-100 dark:border-gray-700/50 flex items-center justify-between relative z-10">
                  <span class="text-[11px] text-gray-400">From {{ plan.startDate }}</span>
                  <div class="flex items-center gap-1">
                    <button (click)="openEditPlanDialog(plan)"
                      class="p-1.5 text-gray-500 hover:text-teal-600 dark:hover:text-teal-400 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors" title="Edit Plan">
                      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">edit</mat-icon>
                    </button>
                    <button (click)="deletePlan(plan)"
                      class="p-1.5 text-gray-500 hover:text-red-600 dark:hover:text-red-400 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors" title="Delete Plan">
                      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">delete</mat-icon>
                    </button>
                  </div>
                </div>

              </div>
            }
          </div>
        }
      </div>

    </div>
  `,
  styles: [`
    .custom-scrollbar {
      -webkit-overflow-scrolling: touch;
    }
    .custom-scrollbar::-webkit-scrollbar {
      width: 4px;
    }
    .custom-scrollbar::-webkit-scrollbar-thumb {
      background-color: rgba(156, 163, 175, 0.3);
      border-radius: 10px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConsumablePlanListComponent {
  private readonly inventoryService = inject(InventoryService);
  private readonly contextService = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly loading = signal(true);
  readonly searchTerm = signal('');
  readonly selectedStatus = signal<ConsumableUsagePlanStatus | ''>('');
  private readonly refreshTrigger = signal(0);
  readonly ConsumableUsagePlanStatus = ConsumableUsagePlanStatus;

  readonly currentFarmId = toSignal(
    this.contextService.currentFarm$.pipe(
      switchMap(farm => of(farm?.id || null))
    ),
    { initialValue: this.contextService.currentFarmValue?.id || null }
  );

  // Reactively fetch plans when context, search, status, or refresh changes
  readonly plans = toSignal(
    toObservable(computed(() => ({
      farmId: this.currentFarmId(),
      search: this.searchTerm(),
      status: this.selectedStatus(),
      refresh: this.refreshTrigger()
    }))).pipe(
      switchMap(state => {
        if (!state.farmId) {
          this.loading.set(false);
          return of([]);
        }
        this.loading.set(true);
        const statusVal = state.status ? state.status as ConsumableUsagePlanStatus : undefined;
        return this.inventoryService.getConsumablePlans(
          state.farmId,
          1,
          50,
          statusVal,
          state.search || undefined
        ).pipe(
          catchError(() => of({ items: [] })),
          switchMap(res => {
            this.loading.set(false);
            return of(res.items);
          })
        );
      })
    ),
    { initialValue: [] }
  );

  onSearchChange(term: string): void {
    this.searchTerm.set(term);
  }

  onStatusChange(status: any): void {
    this.selectedStatus.set(status);
  }

  openCreatePlanDialog(): void {
    const farmId = this.currentFarmId();
    if (!farmId) {
      this.snackBar.open('Please select an active farm first.', 'Close', { duration: 3000 });
      return;
    }

    const ref = this.dialog.open(ConsumablePlanDialogComponent, {
      data: { farmId },
      width: '640px',
      panelClass: 'custom-dialog-container',
      disableClose: true
    });

    ref.afterClosed().subscribe(saved => {
      if (saved) {
        this.refreshTrigger.update(v => v + 1);
      }
    });
  }

  openEditPlanDialog(plan: ConsumableUsagePlan): void {
    const farmId = this.currentFarmId();
    if (!farmId) return;

    const ref = this.dialog.open(ConsumablePlanDialogComponent, {
      data: { plan, farmId },
      width: '640px',
      panelClass: 'custom-dialog-container',
      disableClose: true
    });

    ref.afterClosed().subscribe(saved => {
      if (saved) {
        this.refreshTrigger.update(v => v + 1);
      }
    });
  }

  deletePlan(plan: ConsumableUsagePlan): void {
    const ref = this.dialog.open(ConfirmationDialogComponent, {
      data: {
        title: 'Delete Consumable Plan',
        message: `Are you sure you want to delete the plan "${plan.name}"? Past daily entries will be retained.`,
        confirmText: 'Delete Plan',
        isDestructive: true
      }
    });

    ref.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.inventoryService.deleteConsumablePlan(plan.id).subscribe({
          next: () => {
            this.snackBar.open('Plan deleted successfully.', 'Close', { duration: 3000 });
            this.refreshTrigger.update(v => v + 1);
          },
          error: () => {
            this.snackBar.open('Failed to delete plan.', 'Close', { duration: 3000 });
          }
        });
      }
    });
  }
}
