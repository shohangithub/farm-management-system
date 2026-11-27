import { CanPipe } from '../../../../shared/pipes/can.pipe';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import {
  Component, inject, signal, ChangeDetectionStrategy, computed,
  ViewChild
} from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { AnimalService } from '../../services/animal.service';
import { DocumentService } from '../../../../core/services/document.service';
import { ShedService } from '../../../farms/services/shed.service';
import { PenService } from '../../../farms/services/pen.service';
import { BreedService } from '../../services/breed.service';
import { HealthService } from '../../../health/services/health.service';
import { FinanceService } from '../../../finance/services/finance.service';
import { ShedList } from '../../../farms/models/shed.model';
import { PenList } from '../../../farms/models/pen.model';
import { AnimalCostLedger } from '../../../finance/models/finance.model';
import { AnimalHealthHistoryDto, VaccinationStatus, TreatmentStatus } from '../../../health/models/health.models';
import {
  AnimalDto, AnimalStatus, AnimalSex, AnimalSpecies, TagType,
  SPECIES_LABELS, STATUS_LABELS, SEX_LABELS, DisposalReason, isOnFarm,
} from '../../models/animal.models';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { UndoExitDialogComponent } from '../../dialogs/undo-exit-dialog/undo-exit-dialog.component';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { ReverseAnimalExitResult } from '../../models/animal.models';
import { RecordMortalityDialog } from '../../../health/components/dialogs/record-mortality-dialog/record-mortality-dialog.component';
import { FarmService } from '../../../farms/services/farm.service';
import { BatchService } from '../../services/batch.service';
import { BatchDto } from '../../models/batch.models';
import { TransferAnimalDialogComponent } from '../../dialogs/transfer-animal-dialog/transfer-animal-dialog.component';
import { QuarantineDialogComponent } from '../../dialogs/quarantine-dialog/quarantine-dialog.component';
import { MatingDialogComponent } from '../../dialogs/mating-dialog/mating-dialog.component';
import { ConfirmPregnancyDialogComponent } from '../../dialogs/confirm-pregnancy-dialog/confirm-pregnancy-dialog.component';
import { RecordCalvingDialogComponent } from '../../dialogs/record-calving-dialog/record-calving-dialog.component';
import { AssignBatchDialogComponent } from '../../dialogs/assign-batch-dialog/assign-batch-dialog.component';
import { RecordBcsDialogComponent } from '../../dialogs/record-bcs-dialog/record-bcs-dialog.component';
import { RecordWeightDialogComponent } from '../../dialogs/record-weight-dialog/record-weight-dialog.component';
import { UploadPhotoDialogComponent } from '../../dialogs/upload-photo-dialog/upload-photo-dialog.component';
import { RecordSaleDialogComponent } from '../../dialogs/record-sale-dialog/record-sale-dialog.component';
import { AnimalIntelligenceDialogComponent } from '../../dialogs/animal-intelligence-dialog/animal-intelligence-dialog.component';
import { AnimalEditDialogComponent } from '../../components/animal-edit-dialog/animal-edit-dialog';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, forkJoin, tap, filter } from 'rxjs';
import { of } from 'rxjs';
import { NgxEchartsModule } from 'ngx-echarts';
import type { EChartsOption } from 'echarts';
import { RecentlyViewedService } from '../../services/recently-viewed.service';
import { CattleProfitProjectionComponent } from '../../../intelligence/components/projections/cattle-profit-projection.component';
import { IntelligenceService } from '../../../intelligence/services/intelligence.service';
import { FeedingService } from '../../../feeding/services/feeding.service';
import { AssignFeedingPlanDialogComponent, SetRationDialogData } from '../../../feeding/components/dialogs/assign-feeding-plan-dialog/assign-feeding-plan-dialog.component';
import { AnimalFeedingSummary, DailyFeedingEntryStatus } from '../../../feeding/models/feeding.models';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { ReportService } from '../../../reports/services/report.service';
import { formatAnimalAge } from '../../utils/animal-age';

import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { NotificationService } from '../../../../shared/services/notification.service';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { APP_DATE_LOCALE } from '../../../../shared/utils/locale';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
@Component({
  selector: 'app-animal-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CanPipe, ErrorStateComponent, MoneyPipe, 
    CommonModule, RouterModule, FormsModule,
    PageHeaderComponent, LoadingComponent, DatePipe, DecimalPipe,
    MatButtonModule, MatIconModule, MatDialogModule, MatTabsModule, MatSnackBarModule, MatMenuModule, MatDividerModule, MatTooltipModule, HasPermissionDirective,
    NgxEchartsModule,
    CattleProfitProjectionComponent, TranslatePipe
  ],
  templateUrl: './animal-detail.component.html',
  styleUrl: './animal-detail.component.scss'
})
export class AnimalDetailComponent {
  private readonly svc = inject(AnimalService);
  readonly documents = inject(DocumentService);
  private readonly route = inject(ActivatedRoute);
  private readonly shedSvc = inject(ShedService);
  private readonly penSvc = inject(PenService);
  private readonly farmSvc = inject(FarmService);
  private readonly healthSvc = inject(HealthService);
  private readonly financeSvc = inject(FinanceService);
  private readonly feedingSvc = inject(FeedingService);
  private readonly batchSvc = inject(BatchService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly recentSvc = inject(RecentlyViewedService);
  private readonly pdfService = inject(PdfExportService);
  private readonly contextService = inject(WorkingContextService);
  private readonly reportService = inject(ReportService);
  private readonly breedSvc = inject(BreedService);
  private readonly intelligenceSvc = inject(IntelligenceService);
  private readonly i18n = inject(I18nService);
  readonly couldNotLoadTitle = this.i18n.t("Couldn't load animal");

  readonly isExporting = signal(false);
  readonly AnimalStatus = AnimalStatus;
  readonly AnimalSex = AnimalSex;
  readonly VaccinationStatus = VaccinationStatus;
  readonly TreatmentStatus = TreatmentStatus;
  readonly DailyFeedingEntryStatus = DailyFeedingEntryStatus;

  readonly Math = Math;

  private refreshTrigger = signal(0);
  /** Fallback live rate until the farm's valuation rate loads (FarmBusinessRules default). */
  private static readonly FALLBACK_LIVE_RATE = 400;
  /** Rate the user typed into the card; null = use the farm's valuation rate. */
  private readonly liveWeightPriceOverride = signal<number | null>(null);
  readonly liveWeightPrice = computed(() =>
    this.liveWeightPriceOverride()
    ?? this.animalDataResult()?.projectionDefaults?.liveWeightValuePerKgBdt?.value
    ?? AnimalDetailComponent.FALLBACK_LIVE_RATE);
  readonly liveWeightPriceSource = computed(() =>
    this.liveWeightPriceOverride() !== null
      ? 'Manual rate'
      : this.animalDataResult()?.projectionDefaults?.liveWeightValuePerKgBdt?.sourceLabel ?? 'Default live rate');
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  private routeId = toSignal(this.route.paramMap.pipe(map(params => params.get('id'))), { initialValue: null });

  private fetchParams = computed(() => ({
    id: this.routeId(),
    refresh: this.refreshTrigger()
  }));

  private animalDataResult = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(params => !!params.id),
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(({ id }) =>
        this.svc.getById(id!).pipe(
          switchMap(animal => {
            // Parallel fetch related data
            return forkJoin({
              animal: of(animal),
              breedName: animal.breedId ? this.breedSvc.getBreedById(animal.breedId).pipe(map(b => b.name), catchError(() => of(null))) : of(null),
              ledger: animal.farmId ? this.financeSvc.getAnimalCostLedger(animal.farmId, animal.id).pipe(catchError(() => of(null))) : of(null),
              projectionDefaults: this.intelligenceSvc.getProjectionDefaults(animal.id).pipe(catchError(() => of(null))),
              healthHistory: this.healthSvc.getAnimalHealthHistory(animal.id).pipe(catchError(() => of(null))),
              feedingSummary: this.feedingSvc.getAnimalFeedingSummary(animal.id).pipe(catchError(() => of(null))),
              farmName: animal.farmId ? this.farmSvc.getFarmById(animal.farmId).pipe(map(f => f.farmName), catchError(() => of(null))) : of(null),
              availableBatches: animal.farmId ? this.batchSvc.getBatches(animal.farmId).pipe(map(b => b.items), catchError(() => of([]))) : of([]),
              shedName: animal.shedId ? this.shedSvc.getShedById(animal.shedId).pipe(map(s => s.shedName), catchError(() => of(null))) : of(null),
              penName: animal.penId ? this.penSvc.getPenById(animal.penId).pipe(map(p => p.penNumber), catchError(() => of(null))) : of(null),
              sheds: animal.farmId ? this.shedSvc.getShedsByFarm(animal.farmId).pipe(catchError(() => of([]))) : of([]),
            }).pipe(
              tap(result => {
                // Add to recently viewed
                if (result.animal) {
                  if (result.breedName) {
                    result.animal.breedName = result.breedName;
                  }
                  this.recentSvc.add({
                    id: result.animal.id,
                    tagId: result.animal.tagId,
                    tagType: result.animal.tagType,
                    species: result.animal.species,
                    breedId: result.animal.breedId,
                    breedName: result.breedName || result.animal.breedName || '',
                    sex: result.animal.sex,
                    dateOfBirth: result.animal.dateOfBirth,
                    status: result.animal.status,
                    farmId: result.animal.farmId,
                    batchId: result.animal.batchId,
                    shedId: result.animal.shedId,
                    shedName: result.animal.shedId ? result.sheds.find((s: ShedList) => s.id === result.animal.shedId)?.shedName : undefined,
                    penId: result.animal.penId,
                    penName: result.animal.penId ? result.animal.penId : undefined,
                    latestWeightKg: result.animal.latestWeightKg,
                    latestWeightDate: result.animal.latestWeightDate,
                    adgKgPerDay: result.animal.adgKgPerDay,
                    latestBcs: result.animal.latestBcs,
                    primaryPhotoUrl: result.animal.primaryPhotoUrl,
                    hasHealthAlert: false, // Approximate for now
                    createdAtUtc: result.animal.createdAtUtc
                  });
                }
              }),
              switchMap(data => {
                // If there are sheds, fetch pens for all sheds to resolve movement history names
                if (data.sheds.length > 0) {
                  const penRequests = data.sheds.map(s => this.penSvc.getPensByShed(s.id).pipe(catchError(() => of([]))));
                  return forkJoin(penRequests).pipe(
                    map(pensArrays => {
                      const allPens = pensArrays.flat();
                      return { ...data, pens: allPens };
                    })
                  );
                }
                return of({ ...data, pens: [] });
              })
            );
          }),
          catchError(err => {
            this.error.set(parseApiError(err, this.i18n.t('Could not load this animal.')));
            return of(null);
          })
        )
      ),
      tap(() => this.loading.set(false))
    ),
    { initialValue: null }
  );

  readonly animal = computed(() => this.animalDataResult()?.animal ?? null);
  readonly ledger = computed(() => this.animalDataResult()?.ledger ?? null);
  readonly healthHistory = computed(() => this.animalDataResult()?.healthHistory ?? null);
  readonly feedingSummary = computed(() => this.animalDataResult()?.feedingSummary ?? null);
  readonly feedingEntries = computed(() => this.animalDataResult()?.feedingSummary?.entries ?? []);
  readonly activeFeedingPlan = computed(() => this.animalDataResult()?.feedingSummary?.activePlan ?? null);
  readonly feedingPlans = computed(() => this.animalDataResult()?.feedingSummary?.plans ?? []);
  /** Every plan feeding this animal today — the base rule set plus any direct add-ons. */
  readonly activeFeedingPlans = computed(() => this.feedingPlans().filter(p => String(p.status) === 'Active'));
  readonly activeDailyKg = computed(() => this.activeFeedingPlans().reduce((sum, p) => sum + (p.currentConcentrateKgPerDay ?? 0), 0));
  readonly farmName = computed(() => this.animalDataResult()?.farmName ?? null);
  readonly shedName = computed(() => this.animalDataResult()?.shedName ?? null);
  readonly penName = computed(() => this.animalDataResult()?.penName ?? null);
  readonly breedName = computed(() => this.animalDataResult()?.breedName || this.animal()?.breedName || '');

  readonly sortedWeightsDesc = computed(() => {
    const records = this.animal()?.weightRecords ?? [];
    return [...records].sort((a, b) => b.recordedDate.localeCompare(a.recordedDate));
  });

  readonly recentWeightsTable = computed(() => {
    return this.sortedWeightsDesc().slice(0, 5);
  });

  readonly weightChartSvg = computed(() => {
    const records = this.animal()?.weightRecords ?? [];
    if (records.length < 2) return null;
    const sorted = [...records].sort((a, b) => a.recordedDate.localeCompare(b.recordedDate));
    const width = 450;
    const height = 110;
    const padX = 35;
    const padY = 22;

    const weights = sorted.map(r => r.weightKg);
    const minW = Math.min(...weights);
    const maxW = Math.max(...weights);
    const range = maxW - minW || 1;

    const points = sorted.map((r, i) => {
      const x = padX + (i / (sorted.length - 1)) * (width - padX * 2);
      const y = height - padY - ((r.weightKg - minW) / range) * (height - padY * 2);
      return { x: Math.round(x * 10) / 10, y: Math.round(y * 10) / 10, weight: r.weightKg, date: r.recordedDate };
    });

    const polylinePoints = points.map(p => `${p.x},${p.y}`).join(' ');
    const areaPath = `M ${points[0].x},${height - 10} ` + 
                     points.map(p => `L ${p.x},${p.y}`).join(' ') + 
                     ` L ${points[points.length - 1].x},${height - 10} Z`;

    return { points, polylinePoints, areaPath, minW, maxW, width, height };
  });

  readonly feedingStatusFilter = signal<string>('ALL');
  readonly filteredFeedingEntries = computed(() => {
    const entries = this.feedingEntries();
    const filter = this.feedingStatusFilter();
    if (filter === 'ALL') return entries;
    return entries.filter(e => e.status.toString() === filter);
  });

  // Financial calculations
  // The cost ledger is the source of truth: its feed bucket is re-synced from feed allocations
  // (which cover group-fed animals too) every time it is read. The feeding summary only sees the
  // animal's own daily entries, so it is a fallback for when no ledger exists.
  readonly acquisitionCost = computed(() => {
    const l = this.ledger();
    if (l) return l.acquisitionCostBdt || 0;
    return this.animal()?.acquisitionPriceBdt || 0;
  });

  readonly totalTreatmentCost = computed(() => {
    const l = this.ledger();
    if (l) return l.totalVetCostBdt || 0;

    // Fallback if ledger not initialized
    const history = this.healthHistory();
    if (!history || !history.treatments) return 0;
    return history.treatments.reduce((sum, t) => sum + (t.costBdt || 0), 0);
  });

  readonly totalFeedCost = computed(() => {
    const l = this.ledger();
    if (l) return l.totalFeedCostBdt || 0;
    return this.feedingSummary()?.totalFeedCostBdt || 0;
  });

  /** Labour + allocated overhead — part of total cost, so shown in the breakdown too. */
  readonly laborOverheadCost = computed(() => {
    const l = this.ledger();
    return l ? (l.totalLaborCostBdt || 0) + (l.totalOverheadBdt || 0) : 0;
  });

  readonly totalCost = computed(() =>
    this.acquisitionCost() + this.totalFeedCost() + this.totalTreatmentCost() + this.laborOverheadCost());

  readonly totalRevenue = computed(() => {
    const l = this.ledger();
    if (l && l.saleRevenueBdt) return l.saleRevenueBdt;

    const a = this.animal();
    if (!a) return 0;
    return a.salePriceBdt || 0;
  });

  /** A live valuation needs a recorded weight; without one the "profit" would just be −cost. */
  readonly hasValuationWeight = computed(() => (this.animal()?.latestWeightKg ?? 0) > 0);

  readonly currentEstimatedValue = computed(() => {
    const a = this.animal();
    if (!a) return 0;
    return (a.latestWeightKg || 0) * this.liveWeightPrice();
  });

  readonly isSold = computed(() => this.animal()?.status === AnimalStatus.Sold);
  readonly isDead = computed(() => this.animal()?.status === AnimalStatus.Dead);

  /**
   * Farm rule: an animal is "on farm" only while Active or Quarantined. Sold / Dead
   * (and Slaughtered / Transferred) have left the farm: history stays readable, but
   * actions that record new facts are hidden and profit is final, not projected.
   */
  readonly isOnFarm = computed(() => isOnFarm(this.animal()?.status));
  readonly isExited = computed(() => !!this.animal() && !this.isOnFarm());

  /** Exit date: saleDate (Sold) or deathDate (Dead); backend also supplies exitDate. */
  readonly exitDate = computed(() => {
    const a = this.animal();
    if (!a) return null;
    return a.exitDate ?? (a.status === AnimalStatus.Sold ? a.saleDate : a.status === AnimalStatus.Dead ? a.deathDate : null) ?? null;
  });

  readonly disposalReasonLabel = computed(() => {
    const r = this.animal()?.disposalReason;
    switch (r) {
      case DisposalReason.NaturalDeath: return this.i18n.t('Natural death');
      case DisposalReason.Disease: return this.i18n.t('Disease');
      case DisposalReason.Accident: return this.i18n.t('Accident');
      case DisposalReason.Slaughter: return this.i18n.t('Slaughter');
      case DisposalReason.Sale: return this.i18n.t('Sale');
      case DisposalReason.Unknown: return this.i18n.t('Unknown cause');
      default: return r ? String(r) : null;
    }
  });

  /** Realised revenue for an exited animal (a dead animal earns nothing). */
  readonly realisedRevenue = computed(() => this.isDead() ? 0 : this.totalRevenue());

  readonly netProfit = computed(() => {
    if (this.isDead()) {
      // Died: no revenue — the whole investment is the loss.
      return -this.totalCost();
    }
    if (this.isExited()) {
      return this.realisedRevenue() - this.totalCost();
    }
    return this.currentEstimatedValue() - this.totalCost();
  });

  /** Live-weight price at which selling today exactly recovers the cost to date. */
  readonly breakEvenLiveRate = computed(() => {
    const w = this.animal()?.latestWeightKg ?? 0;
    return w > 0 ? this.totalCost() / w : 0;
  });

  onLiveWeightPriceChange(value: number | string | null): void {
    const n = Number(value);
    this.liveWeightPriceOverride.set(value === null || value === '' || !Number.isFinite(n) || n < 0 ? null : n);
  }

  resetLiveWeightPrice(): void {
    this.liveWeightPriceOverride.set(null);
  }

  readonly weightChartOptions = computed<EChartsOption | null>(() => {
    const a = this.animal();
    if (!a || !a.weightRecords || a.weightRecords.length === 0) return null;

    // Sort chronologically for chart
    const sorted = [...a.weightRecords].sort((x, y) => 
      new Date(x.recordedDate).getTime() - new Date(y.recordedDate).getTime()
    );

    const dates = sorted.map(w => this.datePipe.transform(w.recordedDate, 'MMM d, yyyy') || '');
    const weights = sorted.map(w => w.weightKg);

    return {
      tooltip: {
        trigger: 'axis',
        formatter: `{b} <br/> <b>{c} ${this.i18n.t('kg')}</b>`,
        backgroundColor: 'rgba(255, 255, 255, 0.9)',
        borderColor: '#e5e7eb',
        textStyle: { color: '#111827' }
      },
      grid: {
        left: '2%',
        right: '4%',
        bottom: '3%',
        top: '10%',
        containLabel: true
      },
      xAxis: {
        type: 'category',
        boundaryGap: false,
        data: dates,
        axisLine: { lineStyle: { color: '#9ca3af' } },
        axisLabel: { color: '#6b7280' }
      },
      yAxis: {
        type: 'value',
        name: this.i18n.t('Weight (kg)'),
        nameTextStyle: { color: '#6b7280', padding: [0, 0, 0, 10] },
        axisLine: { show: false },
        axisTick: { show: false },
        splitLine: { lineStyle: { type: 'dashed', color: '#e5e7eb' } },
        axisLabel: { color: '#6b7280' }
      },
      series: [
        {
          name: this.i18n.t('Weight'),
          type: 'line',
          data: weights,
          smooth: true,
          symbolSize: 8,
          itemStyle: { color: '#10b981' },
          lineStyle: { width: 3, color: '#10b981' },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [
                { offset: 0, color: 'rgba(16, 185, 129, 0.3)' },
                { offset: 1, color: 'rgba(16, 185, 129, 0.0)' }
              ]
            }
          }
        }
      ]
    };
  });

  private datePipe = new DatePipe(APP_DATE_LOCALE);

  readonly availableBatches = computed(() => this.animalDataResult()?.availableBatches || []);
  readonly sheds = computed(() => this.animalDataResult()?.sheds || []);
  readonly pens = computed(() => this.animalDataResult()?.pens || []);

  readonly resolvedMovements = computed(() => {
    const a = this.animal();
    if (!a || !a.movements) return [];
    return a.movements.map(m => ({
      ...m,
      shedName: m.shedId ? this.sheds().find(s => s.id === m.shedId)?.shedName ?? this.i18n.t('Unknown Shed') : '—',
      penName: m.penId ? this.pens().find(p => p.id === m.penId)?.penNumber ?? this.i18n.t('Unknown Pen') : '—'
    }));
  });

  statusLabel = computed(() => {
    const s = this.animal()?.status;
    return s !== undefined ? this.i18n.t((STATUS_LABELS as any)[s] || '—') : '—';
  });

  statusClass = computed(() => {
    const status = this.animal()?.status;
    switch (status) {
      case AnimalStatus.Active: return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
      case AnimalStatus.Quarantined: return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
      case AnimalStatus.Sold: return 'bg-blue-50 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400 border border-blue-200 dark:border-blue-800';
      case AnimalStatus.Dead: return 'bg-red-50 text-red-700 dark:bg-red-900/20 dark:text-red-400 border border-red-200 dark:border-red-800';
      default: return 'bg-gray-50 text-gray-700 dark:bg-gray-900/20 dark:text-gray-400 border border-gray-200 dark:border-gray-800';
    }
  });

  load(): void {
    this.refreshTrigger.update(v => v + 1);
  }
  /** Set, change (switch chart) or add an extra to this animal's ration. */
  openSetRation(mode?: 'extra' | 'switch'): void {
    if (!this.isOnFarm()) return; // left the farm — no new facts
    const animal = this.animal();
    if (!animal) return;
    this.dialog.open(AssignFeedingPlanDialogComponent, {
      disableClose: true, width: '95vw', maxWidth: '860px',
      data: { animalIds: [animal.id], mode } satisfies SetRationDialogData
    }).afterClosed().subscribe(saved => {
      if (saved) this.refreshTrigger.update(v => v + 1);
    });
  }


  sortedWeights() {
    const a = this.animal();
    if (!a) return [];
    return [...a.weightRecords].sort((x, y) => y.recordedDate.localeCompare(x.recordedDate));
  }

  speciesLabel(s: AnimalSpecies): string {
    switch (s) {
      case AnimalSpecies.CattleBeef: return this.i18n.t('Cattle (Beef)');
      case AnimalSpecies.CattleDairy: return this.i18n.t('Cattle (Dairy)');
      case AnimalSpecies.Goat: return this.i18n.t('Goat');
      case AnimalSpecies.Sheep: return this.i18n.t('Sheep');
      default: return this.i18n.t('Unknown');
    }
  }

  speciesEmoji(s: AnimalSpecies): string {
    switch (s) {
      case AnimalSpecies.CattleBeef: return '🐄';
      case AnimalSpecies.CattleDairy: return '🐄';
      case AnimalSpecies.Goat: return '🐐';
      case AnimalSpecies.Sheep: return '🐑';
      default: return '🐾';
    }
  }

  sexLabel(s: AnimalSex): string {
    return this.i18n.t(s === AnimalSex.Male ? 'Male' : 'Female');
  }

  tagTypeLabel(t: TagType): string {
    switch (t) {
      case TagType.Manual: return this.i18n.t('Manual/Collar');
      case TagType.EarTag: return this.i18n.t('Ear Tag');
      case TagType.Rfid: return this.i18n.t('RFID');
      default: return this.i18n.t('Unknown');
    }
  }

  ageLabel(dob: string): string {
    return this.i18n.t('{age} old', { age: formatAnimalAge(dob) });
  }

  onQuarantine(): void {
    if (!this.isOnFarm()) return; // left the farm — no new facts
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(QuarantineDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId }
    });
    dialogRef.afterClosed().subscribe(res => { if (res) this.load(); });
  }

  onRelease(): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, { disableClose: true,
      data: {
        title: this.i18n.t('Release from Quarantine'),
        message: this.i18n.t('Are you sure you want to release {tag} from quarantine?', { tag: a.tagId }),
        confirmButtonText: this.i18n.t('Release'),
        isDestructive: false
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.svc.releaseFromQuarantine(a.id).subscribe({ next: () => this.load() });
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/livestock']);
  }

  onEdit(): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(AnimalEditDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '600px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: a
    });
    dialogRef.afterClosed().subscribe(res => { if (res) this.load(); });
  }

  onSell(): void {
    if (!this.isOnFarm()) return; // left the farm — no new facts
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(RecordSaleDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId, latestWeightKg: a.latestWeightKg }
    });
    dialogRef.afterClosed().subscribe(res => { if (res) this.load(); });
  }

  /** Undo a sale or death recorded by mistake: the animal comes back with its plans, doses and accounts restored. */
  onUndoExit(): void {
    const a = this.animal();
    if (!a || !(this.isSold() || this.isDead())) return;
    const dialogRef = this.dialog.open(UndoExitDialogComponent, {
      maxWidth: '95vw', width: '520px', disableClose: true, autoFocus: false,
      data: { animalId: a.id, animalTag: a.tagId, kind: this.isSold() ? 'sold' : 'died', exitDate: this.exitDate() }
    });
    dialogRef.afterClosed().subscribe((res: ReverseAnimalExitResult | undefined) => {
      if (!res) return;
      const refund = res.receivedFromBuyerBdt > 0
        ? ' ' + this.i18n.t('৳{amount} received from the buyer was removed from the accounts — hand it back.', { amount: res.receivedFromBuyerBdt.toLocaleString() })
        : '';
      this.notify.success(this.i18n.t('{tag} is back on the farm. Check Confirm Planned Feed for its pending feed entries.', { tag: a.tagId }) + refund, { duration: 10000 });
      this.load();
    });
  }

  /** Records the death (mortality record + Dead status) with this animal preselected. */
  onRecordDeath(): void {
    if (!this.isOnFarm()) return; // already left the farm
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(RecordMortalityDialog, {
      maxWidth: '95vw', width: '560px', disableClose: true, autoFocus: false,
      data: { animalId: a.id }
    });
    dialogRef.afterClosed().subscribe(res => { if (res) this.load(); });
  }

  onTransfer(): void {
    if (!this.isOnFarm()) return; // left the farm — no new facts
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(TransferAnimalDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: {
        animalId: a.id,
        farmId: a.farmId,
        animalTag: a.tagId,
        currentShedId: a.shedId,
        currentPenId: a.penId
      }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.notify.success(this.i18n.t('Location assigned successfully!'));
        this.load();
      }
    });
  }

  onRecordMating(): void {
    if (!this.isOnFarm()) return; // left the farm — no new facts
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(MatingDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId, farmId: a.farmId }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.notify.success(this.i18n.t('Mating recorded successfully!'));
        this.load();
      }
    });
  }

  onConfirmPregnancy(recordId: string): void {
    if (!this.isOnFarm()) return; // left the farm — no new facts
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(ConfirmPregnancyDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId, recordId }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.notify.success(this.i18n.t('Pregnancy confirmed successfully!'));
        this.load();
      }
    });
  }

  onRecordCalving(recordId: string): void {
    if (!this.isOnFarm()) return; // left the farm — no new facts
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(RecordCalvingDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId, recordId }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.notify.success(this.i18n.t('Calving recorded successfully!'));
        this.load();
      }
    });
  }

  onRecordBcs(): void {
    if (!this.isOnFarm()) return; // left the farm — no new facts
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(RecordBcsDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.notify.success(this.i18n.t('BCS recorded successfully!'));
        this.load();
      }
    });
  }

  onAssignBatch(): void {
    if (!this.isOnFarm()) return; // left the farm — no new facts
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(AssignBatchDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: {
        animalId: a.id,
        animalTag: a.tagId,
        currentBatchId: a.batchId,
        availableBatches: this.availableBatches()
      }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.notify.success(this.i18n.t('Animal assigned to batch successfully!'));
        this.load();
      }
    });
  }

  onRecordWeight(): void {
    if (!this.isOnFarm()) return; // left the farm — no new facts
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(RecordWeightDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.notify.success(this.i18n.t('Weight recorded successfully!'));
        this.load();
      }
    });
  }

  onUploadPhoto(): void {
    const a = this.animal();
    if (!a) return;
    const dialogRef = this.dialog.open(UploadPhotoDialogComponent, { maxWidth: '95vw', disableClose: true,
      width: '450px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, animalTag: a.tagId }
    });
    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.notify.success(this.i18n.t('Photo uploaded successfully!'));
        this.load();
      }
    });
  }

  onOpenIntelligence(): void {
    const a = this.animal();
    if (!a) return;
    this.dialog.open(AnimalIntelligenceDialogComponent, { disableClose: true,
      width: '90vw',
      maxWidth: '1200px',
      autoFocus: false,
      panelClass: 'bg-transparent',
      data: { animalId: a.id, tagId: a.tagId }
    });
  }

  async exportDossierPdf(): Promise<void> {
    const element = document.getElementById('dossierPrintSheet');
    const a = this.animal();
    if (!element || !a) return;

    this.isExporting.set(true);
    element.style.display = 'block';
    element.style.position = 'fixed';
    element.style.left = '-99999px';
    element.style.top = '0';
    element.style.width = '1000px';

    try {
      const tag = a.tagId || 'Animal';
      const farmName = this.farmName() || this.contextService.currentFarmValue?.name || 'Primary Farm';
      await this.pdfService.exportElement(element, {
        filename: `Animal_Dossier_${tag}_${toLocalIsoDate()}`,
        orientation: 'portrait',
        format: 'a4',
        scale: 2,
        quality: 0.95,
        header: {
          title: this.i18n.t('Livestock Dossier: {tag}', { tag }),
          subtitle: this.i18n.t('Species: {species} | Breed: {breed} | Sex: {sex}', { species: this.speciesLabel(a.species), breed: this.breedName() || this.i18n.t('Standard'), sex: this.sexLabel(a.sex) }),
          farmName,
          dateRange: this.i18n.t('Generated: {date}', { date: new Date().toLocaleDateString('en-GB') }),
          currency: this.i18n.t('BDT (৳)'),
          metaFields: [
            { label: this.i18n.t('Status'), value: this.statusLabel() },
            { label: this.i18n.t('Category'), value: this.speciesLabel(a.species) }
          ]
        },
        showSignatures: true
      });
      this.notify.success(this.i18n.t('Animal Dossier exported successfully!'));
    } catch (err) {
      console.error('Failed to export dossier', err);
      this.notify.error(this.i18n.t('Failed to generate Dossier PDF.'));
    } finally {
      element.style.display = 'none';
      this.isExporting.set(false);
    }
  }

  exportAnimalReport(reportKey: string, format: 'pdf' | 'xlsx' | 'csv'): void {
    const a = this.animal();
    if (!a) return;

    this.isExporting.set(true);
    const params: Record<string, string | null> = {
      animalId: a.id,
      pricePerKg: this.liveWeightPrice() ? this.liveWeightPrice().toString() : '400'
    };

    this.reportService.export(reportKey, format, { parameters: params }).subscribe({
      next: (res) => {
        this.isExporting.set(false);
        if (res.body) {
          const tag = (a.tagId || 'Animal').replace(/[\s\W]+/g, '_');
          const dateStr = toLocalIsoDate();
          const keyShort = reportKey.split('.').pop() || 'report';
          const defaultName = `Farm360_${tag}_${keyShort}_${dateStr}.${format}`;
          const fn = this.reportService.fileNameFrom(res.headers, defaultName);
          this.reportService.saveBlob(res.body, fn);
          this.notify.success(this.i18n.t('Report exported successfully!'));
        }
      },
      error: (err) => {
        this.isExporting.set(false);
        console.error('Failed to export report', err);
        this.notify.error(this.i18n.t('Failed to export report. Please try again.'));
      }
    });
  }

  openInReportCenter(reportKey: string = 'livestock.animal-growth'): void {
    const a = this.animal();
    if (!a) return;

    this.router.navigate([`/reports/${reportKey}`], {
      queryParams: { animalId: a.id }
    });
  }

  private handleError(err: any): void {
    let msg = this.i18n.t('An unexpected error occurred.');
    if (err?.error?.errors) {
      msg = Object.values(err.error.errors).flat().join('\n');
    } else if (err?.error?.detail) {
      msg = err.error.detail;
    } else if (err?.error?.title) {
      msg = err.error.title;
    }
    this.notify.error(msg);
  }
}
