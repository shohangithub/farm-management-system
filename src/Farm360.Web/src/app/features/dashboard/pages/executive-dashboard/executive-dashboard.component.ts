import { CanPipe } from '../../../../shared/pipes/can.pipe';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { HealthTasksCardComponent } from '../../../health/components/health-tasks-card/health-tasks-card.component';
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
import { AuthService } from '../../../../core/services/auth.service';
import { DashboardService } from '../../services/dashboard.service';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { ExecutiveDashboardData, InsightSeverity, InsightType, ActionableInsight } from '../../models/dashboard.model';
import { FarmGuidelinesDialogComponent } from '../../../../shared/components/farm-guidelines-dialog/farm-guidelines-dialog';
import { HerdCompositionChartComponent } from '../../widgets/herd-composition-chart/herd-composition-chart';
import { AdgTrendChartComponent } from '../../widgets/adg-trend-chart/adg-trend-chart';
import { FeedCostTrendChartComponent } from '../../widgets/feed-cost-trend-chart/feed-cost-trend-chart';
import { VaccinationComplianceChartComponent } from '../../widgets/vaccination-compliance-chart/vaccination-compliance-chart';
import { FarmSummaryCardsComponent } from '../../widgets/farm-summary-cards/farm-summary-cards';
import { RecentActivityFeedComponent } from '../../widgets/recent-activity-feed/recent-activity-feed';
import { SetupChecklistComponent } from '../../widgets/setup-checklist/setup-checklist';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

/** Visual tone of a KPI: neutral (brand), or a state colour only when the meaning is clear. */
type KpiTone = 'neutral' | 'good' | 'warn' | 'bad';

interface KpiCard {
  label: string;
  icon: string;
  value: number;
  /** Format the value as money instead of a plain count. */
  money?: boolean;
  /** Tint of the icon circle. */
  tone: KpiTone;
  hint: string;
  hintIcon?: string;
  hintTone: KpiTone;
  /** Net profit card: show income / expense instead of a hint line. */
  split?: { income: number; expense: number };
}

const KPI_ICON_CLASS: Record<KpiTone, string> = {
  neutral: 'bg-primary-50 text-primary-700 dark:bg-primary-500/10 dark:text-primary-400',
  good: 'bg-success-50 text-success-700 dark:bg-success-500/10 dark:text-success-400',
  warn: 'bg-amber-50 text-amber-700 dark:bg-amber-500/10 dark:text-amber-400',
  bad: 'bg-rose-50 text-rose-700 dark:bg-rose-500/10 dark:text-rose-400',
};

const KPI_HINT_CLASS: Record<KpiTone, string> = {
  neutral: 'text-gray-500 dark:text-gray-400',
  good: 'text-success-700 dark:text-success-400',
  warn: 'text-amber-700 dark:text-amber-400',
  bad: 'text-rose-700 dark:text-rose-400',
};

@Component({
  selector: 'app-executive-dashboard',
  standalone: true,
  imports: [HasPermissionDirective, CanPipe, MoneyPipe, HealthTasksCardComponent, 
    CommonModule,
    MatIconModule,
    MatButtonModule,
    PageHeaderComponent,
    ErrorStateComponent,
    RouterModule,
    HerdCompositionChartComponent,
    AdgTrendChartComponent,
    FeedCostTrendChartComponent,
    VaccinationComplianceChartComponent,
    FarmSummaryCardsComponent,
    RecentActivityFeedComponent,
    SetupChecklistComponent,
    TranslatePipe
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
  private readonly i18n = inject(I18nService);

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
              catchError(err => of<State>({ data: null, error: parseApiError(err, this.i18n.t(fallbackMessage)) })),
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

  /** Placeholder count for the KPI skeleton while the dashboard loads. */
  public readonly kpiSkeletons = [0, 1, 2, 3, 4, 5, 6, 7];

  private readonly authService = inject(AuthService);
  private readonly farmName = toSignal(this.contextService.currentFarm$.pipe(map(farm => farm?.name ?? null)), { initialValue: null });

  /** "Good morning, Rafiul. Here's Green Acres today." Built from data already in memory. */
  public readonly greeting = computed(() => {
    const hour = new Date().getHours();
    const greeting = this.i18n.t(hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening');
    const name = (this.authService.currentUserSignal()?.fullName ?? '').trim().split(/\s+/)[0];
    const farm = this.farmName() || this.i18n.t('your farm');
    return name
      ? this.i18n.t("{greeting}, {name}. Here's {farm} today.", { greeting, name, farm })
      : this.i18n.t("{greeting}. Here's {farm} today.", { greeting, farm });
  });

  /** Count of KPI signals that need someone to act (sick, deaths, low stock, due vaccinations). */
  public readonly attentionCount = computed(() => {
    const d = this.data();
    if (!d) return 0;
    return [d.sickAnimals, d.deathsThisMonth, d.feedLowStockCount, d.dueVaccinations].filter(v => v > 0).length;
  });

  public readonly kpis = computed<KpiCard[]>(() => {
    const d = this.data();
    if (!d) return [];
    const net = d.currentMonthIncome - d.currentMonthExpense;
    return [
      {
        label: 'Animals on Farm', icon: 'pets', value: d.totalAnimals, tone: 'neutral',
        hint: 'Including quarantined', hintTone: 'neutral'
      },
      d.sickAnimals > 0
        ? { label: 'Sick Animals', icon: 'medical_services', value: d.sickAnimals, tone: 'bad', hint: 'Needs attention', hintIcon: 'warning', hintTone: 'bad' }
        : { label: 'Sick Animals', icon: 'medical_services', value: d.sickAnimals, tone: 'neutral', hint: 'All healthy', hintIcon: 'check_circle', hintTone: 'good' },
      d.feedLowStockCount > 0
        ? { label: 'Low Stock Items', icon: 'inventory_2', value: d.feedLowStockCount, tone: 'warn', hint: 'Reorder required', hintIcon: 'shopping_cart', hintTone: 'warn' }
        : { label: 'Low Stock Items', icon: 'inventory_2', value: d.feedLowStockCount, tone: 'neutral', hint: 'Stock healthy', hintIcon: 'check_circle', hintTone: 'good' },
      {
        label: 'Net profit this month', icon: 'account_balance_wallet', value: net, money: true,
        tone: net < 0 ? 'bad' : 'neutral', hint: '', hintTone: 'neutral',
        split: { income: d.currentMonthIncome, expense: d.currentMonthExpense }
      },
      {
        label: 'Births this month', icon: 'child_care', value: d.birthsThisMonth, tone: 'neutral',
        hint: 'Newborns recorded', hintTone: 'neutral'
      },
      d.deathsThisMonth > 0
        ? { label: 'Deaths this month', icon: 'heart_broken', value: d.deathsThisMonth, tone: 'bad', hint: 'Attention needed', hintIcon: 'warning', hintTone: 'bad' }
        : { label: 'Deaths this month', icon: 'heart_broken', value: d.deathsThisMonth, tone: 'neutral', hint: 'No mortalities', hintIcon: 'check_circle', hintTone: 'good' },
      d.dueVaccinations > 0
        ? { label: 'Due Vaccinations', icon: 'vaccines', value: d.dueVaccinations, tone: 'warn', hint: 'Action required', hintIcon: 'schedule', hintTone: 'warn' }
        : { label: 'Due Vaccinations', icon: 'vaccines', value: d.dueVaccinations, tone: 'neutral', hint: 'Up to date', hintIcon: 'check_circle', hintTone: 'good' },
      {
        label: 'Pregnant Animals', icon: 'favorite', value: d.pregnantAnimals, tone: 'neutral',
        hint: d.pregnantAnimals > 0 ? 'Active pregnancies' : 'No active pregnancies',
        hintIcon: d.pregnantAnimals > 0 ? 'monitor_heart' : undefined, hintTone: 'neutral'
      }
    ];
  });

  public kpiIconClass(tone: KpiTone): string {
    return KPI_ICON_CLASS[tone];
  }

  public kpiHintClass(tone: KpiTone): string {
    return KPI_HINT_CLASS[tone];
  }

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
      case InsightSeverity.Critical: return 'text-rose-600 bg-rose-50 dark:text-rose-400 dark:bg-rose-500/10';
      case InsightSeverity.High: return 'text-amber-600 bg-amber-50 dark:text-amber-400 dark:bg-amber-500/10';
      case InsightSeverity.Medium: return 'text-sky-600 bg-sky-50 dark:text-sky-400 dark:bg-sky-500/10';
      case InsightSeverity.Low: return 'text-emerald-600 bg-emerald-50 dark:text-emerald-400 dark:bg-emerald-500/10';
      default: return 'text-gray-500 bg-gray-50 dark:text-gray-400 dark:bg-gray-500/10';
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
