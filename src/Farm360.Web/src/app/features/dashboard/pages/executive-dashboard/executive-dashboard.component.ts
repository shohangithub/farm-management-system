import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { switchMap, of, filter, catchError, map } from 'rxjs';
import { Router, RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';

import { WorkingContextService } from '../../../../core/services/working-context.service';
import { DashboardService } from '../../services/dashboard.service';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ExecutiveDashboardData, InsightSeverity, InsightType, ActionableInsight } from '../../models/dashboard.model';
import { FarmGuidelinesDialogComponent } from '../../../../shared/components/farm-guidelines-dialog/farm-guidelines-dialog';
import { HerdCompositionChartComponent } from '../../widgets/herd-composition-chart/herd-composition-chart';
import { AdgTrendChartComponent } from '../../widgets/adg-trend-chart/adg-trend-chart';
import { FeedCostTrendChartComponent } from '../../widgets/feed-cost-trend-chart/feed-cost-trend-chart';
import { VaccinationComplianceChartComponent } from '../../widgets/vaccination-compliance-chart/vaccination-compliance-chart';
import { FarmSummaryCardsComponent } from '../../widgets/farm-summary-cards/farm-summary-cards';
import { RecentActivityFeedComponent } from '../../widgets/recent-activity-feed/recent-activity-feed';

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
    CurrencyPipe,
    RouterModule,
    HerdCompositionChartComponent,
    AdgTrendChartComponent,
    FeedCostTrendChartComponent,
    VaccinationComplianceChartComponent,
    FarmSummaryCardsComponent,
    RecentActivityFeedComponent
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
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  private readonly farmId$ = this.contextService.currentFarm$.pipe(
    map(farm => farm?.id)
  );

  // Fetch Dashboard data reactively when the farm changes
  private readonly dashboardData$ = this.farmId$.pipe(
    switchMap(farmId => {
      if (!farmId) {
        return of({
          totalAnimals: 0,
          sickAnimals: 0,
          feedLowStockCount: 0,
          currentMonthIncome: 0,
          currentMonthExpense: 0,
          birthsThisMonth: 0,
          deathsThisMonth: 0,
          dueVaccinations: 0,
          pregnantAnimals: 0,
          actionableInsights: []
        } as ExecutiveDashboardData);
      }
      return this.dashboardService.getExecutiveDashboard(farmId).pipe(
        catchError(err => {
          console.error('Failed to load dashboard data', err);
          return of(null);
        })
      );
    })
  );

  private readonly herdComposition$ = this.farmId$.pipe(
    switchMap(farmId => farmId ? this.dashboardService.getHerdComposition(farmId).pipe(catchError(() => of(null))) : of(null))
  );

  private readonly adgTrends$ = this.farmId$.pipe(
    switchMap(farmId => farmId ? this.dashboardService.getAdgTrends(farmId).pipe(catchError(() => of(null))) : of(null))
  );

  private readonly feedCostTrends$ = this.farmId$.pipe(
    switchMap(farmId => farmId ? this.dashboardService.getFeedCostTrends(farmId).pipe(catchError(() => of(null))) : of(null))
  );

  private readonly vaccinationCompliance$ = this.farmId$.pipe(
    switchMap(farmId => farmId ? this.dashboardService.getVaccinationCompliance(farmId).pipe(catchError(() => of(null))) : of(null))
  );

  private readonly farmSummaryCards$ = this.farmId$.pipe(
    switchMap(farmId => farmId ? this.dashboardService.getFarmSummaryCards(farmId).pipe(catchError(() => of(null))) : of(null))
  );

  private readonly recentActivityFeed$ = this.farmId$.pipe(
    switchMap(farmId => farmId ? this.dashboardService.getRecentActivityFeed(farmId).pipe(catchError(() => of(null))) : of(null))
  );

  private readonly dismissedInsightIds = signal<Set<string>>(new Set());

  public readonly dashboardData = toSignal(this.dashboardData$, { initialValue: undefined });
  public readonly herdComposition = toSignal(this.herdComposition$, { initialValue: undefined });
  public readonly adgTrends = toSignal(this.adgTrends$, { initialValue: undefined });
  public readonly feedCostTrends = toSignal(this.feedCostTrends$, { initialValue: undefined });
  public readonly vaccinationCompliance = toSignal(this.vaccinationCompliance$, { initialValue: undefined });
  public readonly farmSummaryCards = toSignal(this.farmSummaryCards$, { initialValue: undefined });
  public readonly recentActivityFeed = toSignal(this.recentActivityFeed$, { initialValue: undefined });

  public readonly isLoading = computed(() => this.dashboardData() === undefined);
  public readonly hasError = computed(() => this.dashboardData() === null);
  public readonly data = computed(() => {
    const d = this.dashboardData() as ExecutiveDashboardData | undefined;
    if (!d) return d;
    
    const dismissed = this.dismissedInsightIds();
    return {
      ...d,
      actionableInsights: d.actionableInsights.filter(i => !dismissed.has(i.id))
    };
  });

  public openGuidelines(): void {
    this.dialog.open(FarmGuidelinesDialogComponent, { disableClose: true,
      width: '90vw',
      maxWidth: '800px',
      panelClass: ['rounded-2xl', 'overflow-hidden', 'bg-transparent', 'shadow-2xl']
    });
  }

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

  public dismissInsight(insight: ActionableInsight): void {
    const current = this.dismissedInsightIds();
    const next = new Set(current);
    next.add(insight.id);
    this.dismissedInsightIds.set(next);
  }

  public reviewInsight(insight: ActionableInsight): void {
    if (insight.animalId) {
      this.router.navigate(['/livestock', insight.animalId]);
    } else if (insight.batchId) {
      this.router.navigate(['/livestock/batches', insight.batchId]);
    } else {
      this.snackBar.open('Module specific navigation not implemented yet.', 'OK', { duration: 2000 });
    }
  }
}
