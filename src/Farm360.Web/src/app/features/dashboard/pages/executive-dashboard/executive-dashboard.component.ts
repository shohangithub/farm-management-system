import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { switchMap, of, filter, catchError, map } from 'rxjs';

import { WorkingContextService } from '../../../../core/services/working-context.service';
import { DashboardService } from '../../services/dashboard.service';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ExecutiveDashboardData, InsightSeverity, InsightType } from '../../models/dashboard.model';

@Component({
  selector: 'app-executive-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    MatButtonModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    CurrencyPipe
  ],
  templateUrl: './executive-dashboard.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block h-full flex flex-col'
  }
})
export class ExecutiveDashboardComponent {
  private readonly contextService = inject(WorkingContextService);
  private readonly dashboardService = inject(DashboardService);

  private readonly farmId$ = this.contextService.currentFarm$.pipe(
    map(farm => farm?.id)
  );

  // Fetch Dashboard data reactively when the farm changes
  private readonly dashboardData$ = this.farmId$.pipe(
    filter((farmId): farmId is string => !!farmId),
    switchMap(farmId => this.dashboardService.getExecutiveDashboard(farmId).pipe(
      catchError(err => {
        console.error('Failed to load dashboard data', err);
        return of(null);
      })
    ))
  );

  public readonly dashboardData = toSignal(this.dashboardData$, { initialValue: undefined });
  public readonly isLoading = computed(() => this.dashboardData() === undefined);
  public readonly hasError = computed(() => this.dashboardData() === null);
  public readonly data = computed(() => this.dashboardData() as ExecutiveDashboardData | undefined);

  public getSeverityIcon(severity: InsightSeverity): string {
    switch (severity) {
      case InsightSeverity.Critical: return 'warning';
      case InsightSeverity.High: return 'error_outline';
      case InsightSeverity.Medium: return 'info';
      case InsightSeverity.Low: return 'lightbulb_outline';
      default: return 'info';
    }
  }

  public getSeverityColorClass(severity: InsightSeverity): string {
    switch (severity) {
      case InsightSeverity.Critical: return 'text-rose-500 bg-rose-50 dark:bg-rose-500/10 border-rose-200 dark:border-rose-500/20';
      case InsightSeverity.High: return 'text-orange-500 bg-orange-50 dark:bg-orange-500/10 border-orange-200 dark:border-orange-500/20';
      case InsightSeverity.Medium: return 'text-blue-500 bg-blue-50 dark:bg-blue-500/10 border-blue-200 dark:border-blue-500/20';
      case InsightSeverity.Low: return 'text-emerald-500 bg-emerald-50 dark:bg-emerald-500/10 border-emerald-200 dark:border-emerald-500/20';
      default: return 'text-gray-500 bg-gray-50 dark:bg-gray-500/10 border-gray-200 dark:border-gray-500/20';
    }
  }
  
  public getNetProfit(): number {
    const d = this.data();
    if (!d) return 0;
    return d.currentMonthIncome - d.currentMonthExpense;
  }
}
