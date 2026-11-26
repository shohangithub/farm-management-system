import { Component, inject, ChangeDetectionStrategy, signal, computed, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { HealthService } from '../../services/health.service';
import { MedicalTreatmentDto, MilkWithdrawalDto } from '../../models/health.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

import { HealthNavComponent } from '../../components/health-nav/health-nav.component';
import { forkJoin } from 'rxjs';
import { map } from 'rxjs/operators';

interface MeatWithdrawalRow { tag: string; medicine: string; startDate: string; safeDate: Date; }

@Component({
  selector: 'app-milk-withdrawal',
  standalone: true,
  imports: [ErrorStateComponent, CommonModule, MatIconModule, PageHeaderComponent, EmptyStateComponent, LoadingComponent, TranslatePipe, HealthNavComponent],
  template: `
<app-page-header
  [title]="'Withdrawal Periods' | t"
  [breadcrumbActiveNode]="'Withdrawal Periods' | t"
  [description]="'Recently treated animals whose meat or milk is not safe yet.' | t">
  <div actions class="flex flex-wrap items-center gap-2">
    @if (meatRows().length || withdrawals().length) {
      <button type="button" (click)="exportPdf()" [disabled]="isExporting()"
        class="inline-flex items-center gap-1.5 h-10 px-4 text-sm font-semibold rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors disabled:opacity-50">
        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">{{ isExporting() ? 'hourglass_empty' : 'download' }}</mat-icon>
        {{ (isExporting() ? 'Preparing PDF...' : 'Download PDF') | t }}
      </button>
    }
  </div>
</app-page-header>

<app-health-nav></app-health-nav>

<div class="relative min-h-[200px]">
  <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>
  <app-error-state *ngIf="!isLoading() && loadError()" class="block" [message]="loadError()!" (retry)="retryLoad()"></app-error-state>

  <div *ngIf="!isLoading() && !loadError() && !meatRows().length && !withdrawals().length">
    <app-empty-state
      icon="check_circle"
      [title]="'All clear' | t"
      [description]="'No animal is waiting out a medicine withdrawal period.' | t">
    </app-empty-state>
  </div>

  <div #reportSheet id="milkWithdrawalContainer" *ngIf="!isLoading() && !loadError() && (meatRows().length || withdrawals().length)" class="space-y-4">
    <!-- Meat: matters on every farm that sells animals -->
    <section class="bg-white dark:bg-gray-800/80 rounded-2xl border border-gray-200/70 dark:border-gray-700/60 shadow-sm overflow-hidden">
      <header class="px-4 sm:px-5 py-4 flex items-start gap-3 border-b border-gray-100 dark:border-gray-700/60">
        <div class="w-10 h-10 shrink-0 rounded-xl flex items-center justify-center bg-amber-50 text-amber-600 dark:bg-amber-500/10 dark:text-amber-400">
          <mat-icon class="!w-5 !h-5 !text-[20px]">block</mat-icon>
        </div>
        <div>
          <h2 class="font-semibold text-[15px] text-gray-900 dark:text-white">{{ 'Meat: do not sell for slaughter yet' | t }}</h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{{ 'Medicine may still be in the meat until the safe date.' | t }}</p>
        </div>
      </header>
      <p *ngIf="!meatRows().length" class="px-5 py-4 text-sm text-gray-500 dark:text-gray-400">{{ 'No animal is under meat withdrawal.' | t }}</p>
      <div *ngIf="meatRows().length" class="overflow-x-auto">
        <table class="w-full text-sm text-left">
          <thead class="text-xs text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-900/40">
            <tr>
              <th scope="col" class="px-4 py-3 font-semibold">{{ 'Animal Tag' | t }}</th>
              <th scope="col" class="px-4 py-3 font-semibold">{{ 'Medicine' | t }}</th>
              <th scope="col" class="px-4 py-3 font-semibold">{{ 'Treated on' | t }}</th>
              <th scope="col" class="px-4 py-3 font-semibold">{{ 'Safe to sell from' | t }}</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let r of meatRows()" class="border-t border-gray-100 dark:border-gray-700/60">
              <td class="px-4 py-3 font-semibold text-gray-900 dark:text-white whitespace-nowrap">{{ r.tag }}</td>
              <td class="px-4 py-3 text-gray-700 dark:text-gray-300">{{ r.medicine }}</td>
              <td class="px-4 py-3 text-gray-600 dark:text-gray-400 whitespace-nowrap">{{ r.startDate | date:'mediumDate' }}</td>
              <td class="px-4 py-3 font-semibold text-amber-700 dark:text-amber-300 whitespace-nowrap">{{ r.safeDate | date:'mediumDate' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- Milk: only matters for milking animals -->
    <section class="bg-white dark:bg-gray-800/80 rounded-2xl border border-gray-200/70 dark:border-gray-700/60 shadow-sm overflow-hidden">
      <header class="px-4 sm:px-5 py-4 flex items-start gap-3 border-b border-gray-100 dark:border-gray-700/60">
        <div class="w-10 h-10 shrink-0 rounded-xl flex items-center justify-center bg-blue-50 text-blue-600 dark:bg-blue-500/10 dark:text-blue-400">
          <mat-icon class="!w-5 !h-5 !text-[20px]">water_drop</mat-icon>
        </div>
        <div>
          <h2 class="font-semibold text-[15px] text-gray-900 dark:text-white">{{ 'Milk: keep out of the bulk tank' | t }}</h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{{ 'Only for milking animals.' | t }}</p>
        </div>
      </header>
      <p *ngIf="!withdrawals().length" class="px-5 py-4 text-sm text-gray-500 dark:text-gray-400">{{ 'No animal is under milk withdrawal.' | t }}</p>
      <div *ngIf="withdrawals().length" class="overflow-x-auto">
        <table class="w-full text-sm text-left">
          <thead class="text-xs text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-900/40">
            <tr>
              <th scope="col" class="px-4 py-3 font-semibold">{{ 'Animal Tag' | t }}</th>
              <th scope="col" class="px-4 py-3 font-semibold">{{ 'Medicine' | t }}</th>
              <th scope="col" class="px-4 py-3 font-semibold">{{ 'Treated on' | t }}</th>
              <th scope="col" class="px-4 py-3 font-semibold">{{ 'Safe To Milk Date' | t }}</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let w of withdrawals()" class="border-t border-gray-100 dark:border-gray-700/60">
              <td class="px-4 py-3 font-semibold text-gray-900 dark:text-white whitespace-nowrap">{{ w.animalTag }}</td>
              <td class="px-4 py-3 text-gray-700 dark:text-gray-300">{{ w.medicationName }}</td>
              <td class="px-4 py-3 text-gray-600 dark:text-gray-400 whitespace-nowrap">{{ w.treatmentStartDate | date:'mediumDate' }}</td>
              <td class="px-4 py-3 font-semibold text-blue-700 dark:text-blue-300 whitespace-nowrap">{{ w.safeToMilkDate | date:'mediumDate' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MilkWithdrawalComponent {
  private readonly notify = inject(NotificationService);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private pdfExportService = inject(PdfExportService);
  private readonly i18n = inject(I18nService);

  @ViewChild('reportSheet') reportSheet?: ElementRef<HTMLElement>;
  readonly isExporting = signal(false);
  
  isLoading = signal(true);
  readonly loadError = signal<string | null>(null);

  retryLoad(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  private refreshTrigger = signal(0);
  private currentFarmId = toSignal(this.contextService.currentFarm$, { initialValue: null });

  private fetchParams = computed(() => ({
    farmId: this.currentFarmId()?.id || '',
    refresh: this.refreshTrigger()
  }));

  /** Milk rows come from the withdrawal report; meat rows are worked out from recent treatments. */
  private readonly result = toSignal(
    toObservable(this.fetchParams).pipe(
      tap(() => { this.isLoading.set(true); this.loadError.set(null); }),
      switchMap(({ farmId }) => !farmId
        ? of({ milk: [] as MilkWithdrawalDto[], meat: [] as MeatWithdrawalRow[] })
        : forkJoin({
            milk: this.healthService.getMilkWithdrawals(farmId),
            treatments: this.healthService.getTreatments({ farmId, pageNumber: 1, pageSize: 100, sortBy: 'startdate', sortDesc: true })
          }).pipe(
            map(({ milk, treatments }) => ({ milk: milk as MilkWithdrawalDto[], meat: this.toMeatRows(treatments.items) })),
            catchError(err => {
              this.loadError.set(parseApiError(err, 'Could not load withdrawal periods.'));
              return of({ milk: [] as MilkWithdrawalDto[], meat: [] as MeatWithdrawalRow[] });
            })
          )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { milk: [] as MilkWithdrawalDto[], meat: [] as MeatWithdrawalRow[] } }
  );

  readonly withdrawals = computed(() => this.result().milk);
  readonly meatRows = computed(() => this.result().meat);

  private toMeatRows(items: MedicalTreatmentDto[]): MeatWithdrawalRow[] {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return (items || [])
      .filter(t => t.meatWithdrawalDays > 0 && !!t.startDate)
      .map(t => {
        const safe = new Date(t.startDate);
        safe.setDate(safe.getDate() + t.meatWithdrawalDays);
        return { tag: t.animalTagId || t.animalId.slice(0, 8), medicine: t.medicationName, startDate: t.startDate, safeDate: safe };
      })
      .filter(r => r.safeDate >= today)
      .sort((a, b) => a.safeDate.getTime() - b.safeDate.getTime());
  }

  loadWithdrawals() {
    this.refreshTrigger.update(v => v + 1);
  }

  async exportPdf(): Promise<void> {
    const el = this.reportSheet?.nativeElement;
    if (!el) return;

    const milkCount = this.withdrawals().length;
    const meatCount = this.meatRows().length;
    const farm = this.contextService.currentFarmValue;
    const org = this.contextService.currentOrgValue;

    this.isExporting.set(true);
    try {
      await this.pdfExportService.exportElement(el, {
        filename: `Farm360_Withdrawal_Periods_${toLocalIsoDate()}`,
        orientation: 'landscape',
        header: {
          title: this.i18n.t('Withdrawal Periods'),
          subtitle: this.i18n.t('Animals whose meat or milk is not safe yet after medicine'),
          farmName: farm?.name || this.i18n.t('Primary Farm'),
          orgName: org?.name || 'Farm360',
          currency: 'BDT (৳)',
          metaFields: [
            { label: this.i18n.t('Meat withdrawal'), value: `${meatCount}` },
            { label: this.i18n.t('Milk withdrawal'), value: `${milkCount}` }
          ]
        },
        showSignatures: true
      });
    } catch (err) {
      this.notify.error(parseApiError(err, 'Could not export the PDF. Please try again.'));
    } finally {
      this.isExporting.set(false);
    }
  }
}
