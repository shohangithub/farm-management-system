import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { switchMap, of, catchError, map, startWith, combineLatest, BehaviorSubject, Observable } from 'rxjs';
import { parseApiError } from '../../../../core/utils/error-parser';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { Router, RouterModule } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';

import { WorkingContextService } from '../../../../core/services/working-context.service';
import { DashboardService } from '../../services/dashboard.service';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ExecutiveDashboardData, InsightSeverity, InsightType, ActionableInsight } from '../../models/dashboard.model';
import { FarmGuidelinesDialogComponent } from '../../../../shared/components/farm-guidelines-dialog/farm-guidelines-dialog';
import { HerdCompositionChartComponent } from '../../widgets/herd-composition-chart/herd-composition-chart';
import { AdgTrendChartComponent } from '../../widgets/adg-trend-chart/adg-trend-chart';
import { FeedCostTrendChartComponent } from '../../widgets/feed-cost-trend-chart/feed-cost-trend-chart';
import { VaccinationComplianceChartComponent } from '../../widgets/vaccination-compliance-chart/vaccination-compliance-chart';
import { FarmSummaryCardsComponent } from '../../widgets/farm-summary-cards/farm-summary-cards';
import { RecentActivityFeedComponent } from '../../widgets/recent-activity-feed/recent-activity-feed';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';

@Component({
  selector: 'app-executive-dashboard',
  standalone: true,
  imports: [MoneyPipe, 
    CommonModule,
    MatIconModule,
    MatButtonModule,
    PageHeaderComponent,
    LoadingComponent,
    ErrorStateComponent,
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
  private readonly dialog = inject(MatDialog);

  private readonly farmId$ = this.contextService.currentFarm$.pipe(
    map(farm => farm?.id)
  );

  private static readonly EMPTY_DASHBOARD: ExecutiveDashboardData = {
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
  } as ExecutiveDashboardData;

  /**
   * Loads one dashboard section for the active farm with its own error + retry, so a failing
   * widget shows an error card instead of silently looking empty.
   */
  private loadSection<T>(load: (farmId: string) => Observable<T>, fallbackMessage: string, noFarmValue: T | null = null) {
    type State = { data: T | null | undefined; error: string | null };
    const retry$ = new BehaviorSubject<void>(undefined);
    const state = toSignal(
      combineLatest([this.farmId$, retry$]).pipe(
        switchMap(([farmId]) => farmId
          ? load(farmId).pipe(
              map((data): State => ({ data, error: null })),
              catchError(err => of<State>({ data: null, error: parseApiError(err, fallbackMessage) })),
              startWith<State>({ data: undefined, error: null })
            )
          : of<State>({ data: noFarmValue, error: null }))
      ),
      { initialValue: { data: undefined, error: null } as State }
    );
    return {
      data: computed(() => state().data),
      error: computed(() => state().error),
      retry: () => retry$.next()
    };
  }

  public readonly main = this.loadSection(id => this.dashboardService.getExecutiveDashboard(id), 'Could not load the dashboard.', ExecutiveDashboardComponent.EMPTY_DASHBOARD);
  public readonly herd = this.loadSection(id => this.dashboardService.getHerdComposition(id), 'Could not load herd composition.');
  public readonly adg = this.loadSection(id => this.dashboardService.getAdgTrends(id), 'Could not load growth trends.');
  public readonly feedCost = this.loadSection(id => this.dashboardService.getFeedCostTrends(id), 'Could not load feed cost trends.');
  public readonly vaccination = this.loadSection(id => this.dashboardService.getVaccinationCompliance(id), 'Could not load vaccination compliance.');
  public readonly summaryCards = this.loadSection(id => this.dashboardService.getFarmSummaryCards(id), 'Could not load the farm summary.');
  public readonly activity = this.loadSection(id => this.dashboardService.getRecentActivityFeed(id), 'Could not load recent activity.');

  private readonly dismissedInsightIds = signal<Set<string>>(new Set());

  public readonly dashboardData = this.main.data;
  public readonly herdComposition = this.herd.data;
  public readonly adgTrends = this.adg.data;
  public readonly feedCostTrends = this.feedCost.data;
  public readonly vaccinationCompliance = this.vaccination.data;
  public readonly farmSummaryCards = this.summaryCards.data;
  public readonly recentActivityFeed = this.activity.data;

  public readonly isLoading = computed(() => this.dashboardData() === undefined);
  public readonly hasError = computed(() => this.main.error() !== null);
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
      return;
    }
    if (insight.batchId) {
      this.router.navigate(['/livestock/batches', insight.batchId]);
      return;
    }
    // actionData may carry an in-app route; otherwise route by the insight's subject.
    if (insight.actionData?.startsWith('/')) {
      this.router.navigateByUrl(insight.actionData);
      return;
    }
    this.router.navigateByUrl(this.routeForInsight(insight));
  }

  private routeForInsight(insight: ActionableInsight): string {
    const text = `${insight.title} ${insight.message}`.toLowerCase();
    if (/vaccin|deworm|treat|sick|health|disease|mortality|withdrawal/.test(text)) return '/health';
    if (/feed|ration|nutrition|fodder|silage/.test(text)) return '/feeding';
    if (/stock|inventory|reorder|supplier|purchase/.test(text)) return '/inventory';
    if (/profit|cost|expense|income|revenue|cash|loan|finance|margin/.test(text)) return '/finance';
    if (/weight|adg|growth|pregnan|calving|breeding|heat|animal|herd/.test(text)) return '/livestock';
    return '/reports';
  }
}
