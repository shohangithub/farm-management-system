import { InventorySubnavComponent } from '../../components/inventory-subnav/inventory-subnav.component';
import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of, forkJoin } from 'rxjs';

import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import {
  DailyConsumableEntry,
  DailyConsumableEntryStatus,
  DailyConsumableSummary
} from '../../models/inventory.models';
import { ConfirmConsumableEntryDialogComponent } from '../../components/dialogs/confirm-consumable-entry-dialog/confirm-consumable-entry-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { NotificationService } from '../../../../shared/services/notification.service';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

interface GroupedPlanEntries {
  planId: string;
  planName: string;
  entries: DailyConsumableEntry[];
  totalExpected: number;
  totalActual: number;
  confirmedCount: number;
}

@Component({
  selector: 'app-daily-consumable-dashboard',
  standalone: true,
  imports: [InventorySubnavComponent, MoneyPipe, 
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    LoadingComponent,
    TranslatePipe
  ],
  template: `
    <app-page-header
      [title]="'Daily Supplies' | t"
      [description]="'Confirm the supplies used each day. Stock and costs update by themselves.' | t"
      [breadcrumbActiveNode]="crumbLabel | t">
      <div actions class="flex flex-wrap items-center gap-2">
        
        <!-- Date Stepper Controls -->
        <div class="flex items-center bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-1">
          <button [attr.aria-label]="'Previous' | t" (click)="stepDate(-1)" class="p-1 text-gray-500 hover:text-gray-700 dark:hover:text-white rounded-lg transition-colors">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">chevron_left</mat-icon>
          </button>
          <input type="date" [ngModel]="selectedDate()" (ngModelChange)="onDateChange($event)"
                 class="bg-transparent border-0 text-xs font-bold text-gray-800 dark:text-gray-200 focus:outline-none px-2 py-0.5" />
          <button [attr.aria-label]="'Next' | t" (click)="stepDate(1)" class="p-1 text-gray-500 hover:text-gray-700 dark:hover:text-white rounded-lg transition-colors">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">chevron_right</mat-icon>
          </button>
        </div>

        <button (click)="setToday()" [disabled]="isToday()"
          class="px-3 py-2 text-xs font-semibold rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-40 transition-colors shadow-sm inline-flex items-center gap-1">
          <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">calendar_today</mat-icon> {{ 'Today' | t }}
        </button>

        <button (click)="generateEntries()" [disabled]="generating()"
          class="px-3.5 py-2 text-xs font-semibold text-teal-700 dark:text-teal-300 bg-teal-50 dark:bg-teal-950/40 hover:bg-teal-100 dark:hover:bg-teal-900/50 rounded-xl border border-teal-200 dark:border-teal-800 transition-colors shadow-sm inline-flex items-center gap-1.5 disabled:opacity-50">
          <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">sync</mat-icon> {{ 'Generate Entries' | t }}
        </button>

        <button (click)="bulkConfirmAll()" [disabled]="pendingCount() === 0 || bulkConfirming()"
          class="px-4 py-2 text-xs font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-xl transition-colors shadow-sm inline-flex items-center gap-1.5 disabled:opacity-50 disabled:cursor-not-allowed">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">done_all</mat-icon> {{ 'Confirm All ({n})' | t: { n: pendingCount() } }}
        </button>
      </div>
    </app-page-header>
    <app-inventory-subnav></app-inventory-subnav>

    <!-- Entries "Confirm All" could not confirm, and why -->
    <div *ngIf="bulkFailures().length && bulkFailuresDate() === selectedDate()" role="alert"
      class="mb-6 p-4 rounded-2xl border border-amber-200 dark:border-amber-800 bg-amber-50 dark:bg-amber-950/30 text-amber-900 dark:text-amber-100">
      <div class="flex items-start justify-between gap-3">
        <div class="flex items-center gap-2 font-bold text-sm">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-amber-500">warning</mat-icon>
          {{ '{n} entry(s) not confirmed' | t: { n: bulkFailures().length } }}
        </div>
        <button type="button" (click)="bulkFailures.set([])" [attr.aria-label]="'Dismiss' | t"
          class="p-1 -m-1 text-amber-700 hover:text-amber-900 dark:text-amber-300 rounded-lg">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">close</mat-icon>
        </button>
      </div>
      <ul class="mt-2 space-y-1 text-xs list-disc pl-6">
        <li *ngFor="let f of bulkFailures()"><b>{{ f.itemName }}</b>: {{ f.reason }}</li>
      </ul>
      <p class="mt-2 mb-0 text-xs text-amber-800 dark:text-amber-200">{{ 'These are still pending. Fix the cause (for example, record the Stock In) and confirm them again.' | t }}</p>
    </div>

    <!-- KPI Summary Cards -->
    <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 mb-6">
      
      <!-- Total Planned -->
      <div class="bg-white dark:bg-gray-900 rounded-2xl p-4 border border-gray-100 dark:border-gray-800/60 shadow-sm relative overflow-hidden">
        <div class="flex items-center justify-between">
          <div>
            <span class="text-[11px] font-bold uppercase tracking-wider text-gray-400">{{ 'Total Planned' | t }}</span>
            <div class="text-xl font-extrabold text-gray-900 dark:text-white mt-1">
              {{ summary()?.totalPlannedEntries ?? 0 }}
            </div>
          </div>
          <div class="w-10 h-10 rounded-xl bg-teal-50 dark:bg-teal-950/40 text-teal-600 dark:text-teal-400 flex items-center justify-center">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">assignment</mat-icon>
          </div>
        </div>
      </div>

      <!-- Confirmed -->
      <div class="bg-white dark:bg-gray-900 rounded-2xl p-4 border border-gray-100 dark:border-gray-800/60 shadow-sm relative overflow-hidden">
        <div class="flex items-center justify-between">
          <div>
            <span class="text-[11px] font-bold uppercase tracking-wider text-gray-400">{{ 'Confirmed' | t }}</span>
            <div class="text-xl font-extrabold text-emerald-600 dark:text-emerald-400 mt-1">
              {{ summary()?.confirmedEntries ?? 0 }}
            </div>
          </div>
          <div class="w-10 h-10 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">check_circle</mat-icon>
          </div>
        </div>
      </div>

      <!-- Pending -->
      <div class="bg-white dark:bg-gray-900 rounded-2xl p-4 border border-gray-100 dark:border-gray-800/60 shadow-sm relative overflow-hidden">
        <div class="flex items-center justify-between">
          <div>
            <span class="text-[11px] font-bold uppercase tracking-wider text-gray-400">{{ 'Pending' | t }}</span>
            <div class="text-xl font-extrabold text-amber-500 mt-1">
              {{ summary()?.pendingEntries ?? 0 }}
            </div>
          </div>
          <div class="w-10 h-10 rounded-xl bg-amber-50 dark:bg-amber-950/40 text-amber-600 dark:text-amber-400 flex items-center justify-center">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">pending</mat-icon>
          </div>
        </div>
      </div>

      <!-- Today's Cost -->
      <div class="bg-white dark:bg-gray-900 rounded-2xl p-4 border border-gray-100 dark:border-gray-800/60 shadow-sm relative overflow-hidden">
        <div class="flex items-center justify-between">
          <div>
            <span class="text-[11px] font-bold uppercase tracking-wider text-gray-400">{{ 'Recorded Cost' | t }}</span>
            <div class="text-xl font-extrabold text-gray-900 dark:text-white mt-1">
              {{ summary()?.totalCostBdt ?? 0 | money:2 }}
            </div>
          </div>
          <div class="w-10 h-10 rounded-xl bg-indigo-50 dark:bg-indigo-950/40 text-indigo-600 dark:text-indigo-400 flex items-center justify-center">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">payments</mat-icon>
          </div>
        </div>
      </div>

    </div>

    <!-- Main Content: Grouped by Plan -->
    <div class="bg-white dark:bg-gray-900 rounded-2xl shadow-sm border border-gray-200 dark:border-gray-800 overflow-hidden relative min-h-[400px]">
      <app-loading *ngIf="loading()" [overlay]="true"></app-loading>

      <div class="p-4 sm:p-6">
        @if (loadError(); as err) {
          <app-error-state [message]="err" (retry)="retryLoad()"></app-error-state>
        } @else if (!loading() && entries().length === 0) {
          <app-empty-state
            icon="cleaning_services"
            [title]="'No Consumable Entries For This Date' | t"
            [description]="emptyDescription | t"
            [actionLabel]="'Generate Entries' | t"
            (action)="generateEntries()">
          </app-empty-state>
        } @else {
          <div class="space-y-6">
            @for (group of planGroups(); track group.planId) {
              <div class="bg-gray-50/70 dark:bg-gray-900/40 rounded-2xl border border-gray-200/80 dark:border-gray-700/60 p-4 sm:p-5">
                
                <!-- Plan Group Header -->
                <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-3 mb-3 border-b border-gray-200/60 dark:border-gray-700/60">
                  <div class="flex items-center gap-2.5">
                    <div class="w-7 h-7 rounded-lg bg-teal-100 dark:bg-teal-900/50 text-teal-700 dark:text-teal-300 flex items-center justify-center">
                      <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">folder</mat-icon>
                    </div>
                    <div>
                      <h3 class="font-bold text-gray-900 dark:text-white text-sm m-0">{{ group.planName }}</h3>
                      <span class="text-[11px] text-gray-400">{{ '{n} item(s)' | t: { n: group.entries.length } }} • {{ '{done}/{total} confirmed' | t: { done: group.confirmedCount, total: group.entries.length } }}</span>
                    </div>
                  </div>
                </div>

                <!-- Entries Table / Card List -->
                <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                  @for (entry of group.entries; track entry.id) {
                    <div class="bg-white dark:bg-gray-800 rounded-xl p-3.5 border border-gray-100 dark:border-gray-700/60 shadow-xs flex flex-col justify-between relative"
                      [ngClass]="{
                        'border-l-4 border-l-emerald-500': entry.status === DailyConsumableEntryStatus.Confirmed || entry.status === DailyConsumableEntryStatus.Adjusted,
                        'border-l-4 border-l-amber-500': entry.status === DailyConsumableEntryStatus.Pending,
                        'border-l-4 border-l-gray-400': entry.status === DailyConsumableEntryStatus.Skipped
                      }">

                      <div>
                        <!-- Top Info -->
                        <div class="flex items-start justify-between gap-2 mb-2">
                          <span class="font-bold text-gray-900 dark:text-white text-xs leading-snug">{{ entry.itemName }}</span>
                          
                          <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold uppercase tracking-wider shrink-0"
                            [ngClass]="{
                              'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-400': entry.status === DailyConsumableEntryStatus.Confirmed || entry.status === DailyConsumableEntryStatus.Adjusted,
                              'bg-amber-50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-400': entry.status === DailyConsumableEntryStatus.Pending,
                              'bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-400': entry.status === DailyConsumableEntryStatus.Skipped
                            }">
                            {{ entry.statusName | t }}
                          </span>
                        </div>

                        <!-- Quantities & Cost -->
                        <div class="grid grid-cols-2 gap-2 p-2 bg-gray-50/80 dark:bg-gray-900/40 rounded-lg text-xs mb-3">
                          <div>
                            <span class="text-gray-400 text-[11px] block font-semibold">{{ 'Planned' | t }}</span>
                            <span class="font-bold text-gray-700 dark:text-gray-300">{{ entry.expectedQuantity }} {{ entry.unitOfMeasure }}</span>
                          </div>
                          <div>
                            <span class="text-gray-400 text-[11px] block font-semibold">{{ 'Actual' | t }}</span>
                            <span class="font-bold" [ngClass]="entry.actualQuantity != null ? 'text-emerald-600 dark:text-emerald-400' : 'text-gray-400'">
                              {{ entry.actualQuantity != null ? entry.actualQuantity + ' ' + entry.unitOfMeasure : '—' }}
                            </span>
                          </div>
                          @if (entry.totalCostBdt) {
                            <div class="col-span-2 pt-1 border-t border-gray-200/50 dark:border-gray-700/50 flex items-center justify-between text-[11px]">
                              <span class="text-gray-400">{{ 'Total Expense:' | t }}</span>
                              <span class="font-bold text-gray-900 dark:text-white">{{ entry.totalCostBdt | money:2 }}</span>
                            </div>
                          }
                        </div>
                      </div>

                      <!-- Actions -->
                      <div class="flex items-center justify-end gap-1.5 pt-1">
                        @if (entry.status === DailyConsumableEntryStatus.Pending) {
                          <button (click)="skipEntry(entry)"
                            class="px-2.5 py-1 text-[11px] font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors">
                            {{ 'Skip' | t }}
                          </button>
                          <button (click)="openConfirmDialog(entry)"
                            class="px-3 py-1 text-[11px] font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-all shadow-xs shadow-teal-500/20 inline-flex items-center gap-1">
                            <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">check</mat-icon> {{ 'Confirm' | t }}
                          </button>
                        } @else {
                          <button (click)="openConfirmDialog(entry)"
                            class="px-2.5 py-1 text-[11px] font-semibold text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors inline-flex items-center gap-1">
                            <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">edit</mat-icon> {{ 'Adjust' | t }}
                          </button>
                        }
                      </div>

                    </div>
                  }
                </div>

              </div>
            }
          </div>
        }
      </div>

    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DailyConsumableDashboardComponent {
  private readonly inventoryService = inject(InventoryService);
  private readonly contextService = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(I18nService);

  /** Labels with apostrophes / quotes, kept out of the template. */
  readonly crumbLabel = 'Daily Supplies';
  readonly emptyDescription = "No entries generated yet. Click 'Generate Entries' to pull items from active consumable plans.";

  readonly loading = signal(true);
  readonly generating = signal(false);
  readonly bulkConfirming = signal(false);
  /** Entries the last "Confirm All" could not confirm, with the reason (e.g. not enough stock). */
  readonly bulkFailures = signal<{ itemName: string; reason: string }[]>([]);
  /** Day those failures belong to; the list is hidden on other days. */
  readonly bulkFailuresDate = signal<string | null>(null);
  readonly selectedDate = signal<string>(toLocalIsoDate());
  private readonly refreshTrigger = signal(0);
  readonly loadError = signal<string | null>(null);

  retryLoad(): void {
    this.refreshTrigger.update(v => v + 1);
  }
  readonly DailyConsumableEntryStatus = DailyConsumableEntryStatus;

  readonly isToday = computed(() => {
    const today = toLocalIsoDate();
    return this.selectedDate() === today;
  });

  readonly currentFarmId = toSignal(
    this.contextService.currentFarm$.pipe(
      switchMap(farm => of(farm?.id || null))
    ),
    { initialValue: this.contextService.currentFarmValue?.id || null }
  );

  // Load entries and summary reactively
  readonly dataState = toSignal(
    toObservable(computed(() => ({
      farmId: this.currentFarmId(),
      date: this.selectedDate(),
      refresh: this.refreshTrigger()
    }))).pipe(
      switchMap(state => {
        if (!state.farmId) {
          this.loading.set(false);
          return of({ entries: [], summary: null });
        }
        this.loading.set(true);
        this.loadError.set(null);
        return forkJoin({
          entries: this.inventoryService.getDailyConsumables(state.farmId, state.date).pipe(
            catchError(err => {
              this.loadError.set(parseApiError(err, this.i18n.t('Could not load daily consumables.')));
              return of([]);
            })
          ),
          summary: this.inventoryService.getDailyConsumableSummary(state.farmId, state.date).pipe(
            catchError(() => of(null))
          )
        }).pipe(
          switchMap(res => {
            this.loading.set(false);
            return of(res);
          })
        );
      })
    ),
    { initialValue: { entries: [], summary: null } }
  );

  readonly entries = computed(() => this.dataState()?.entries ?? []);
  readonly summary = computed(() => this.dataState()?.summary ?? null);

  readonly pendingCount = computed(() => {
    return this.entries().filter(e => e.status === DailyConsumableEntryStatus.Pending).length;
  });

  // Group entries by plan
  readonly planGroups = computed<GroupedPlanEntries[]>(() => {
    const entriesList = this.entries();
    const map = new Map<string, GroupedPlanEntries>();

    for (const e of entriesList) {
      if (!map.has(e.consumableUsagePlanId)) {
        map.set(e.consumableUsagePlanId, {
          planId: e.consumableUsagePlanId,
          planName: e.planName,
          entries: [],
          totalExpected: 0,
          totalActual: 0,
          confirmedCount: 0
        });
      }
      const group = map.get(e.consumableUsagePlanId)!;
      group.entries.push(e);
      group.totalExpected += e.expectedQuantity;
      if (e.actualQuantity != null) {
        group.totalActual += e.actualQuantity;
      }
      if (e.status === DailyConsumableEntryStatus.Confirmed || e.status === DailyConsumableEntryStatus.Adjusted) {
        group.confirmedCount++;
      }
    }

    return Array.from(map.values());
  });

  stepDate(days: number): void {
    const current = new Date(this.selectedDate());
    current.setDate(current.getDate() + days);
    this.selectedDate.set(toLocalIsoDate(current));
  }

  setToday(): void {
    this.selectedDate.set(toLocalIsoDate());
  }

  onDateChange(date: string): void {
    if (date) {
      this.selectedDate.set(date);
    }
  }

  generateEntries(): void {
    const farmId = this.currentFarmId();
    if (!farmId) return;

    this.generating.set(true);
    this.inventoryService.generateDailyConsumables(farmId, this.selectedDate()).subscribe({
      next: res => {
        this.generating.set(false);
        this.notify.success(this.i18n.t('Generated {n} entries for {date}', { n: res.generatedCount, date: this.selectedDate() }));
        this.refreshTrigger.update(v => v + 1);
      },
      error: () => {
        this.generating.set(false);
        this.notify.error(this.i18n.t('Failed to generate entries.'));
      }
    });
  }

  openConfirmDialog(entry: DailyConsumableEntry): void {
    const ref = this.dialog.open(ConfirmConsumableEntryDialogComponent, { maxWidth: '95vw',
      data: { entry },
      width: '420px',
      panelClass: 'custom-dialog-container',
      disableClose: true
    });

    ref.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.refreshTrigger.update(v => v + 1);
      }
    });
  }

  bulkConfirmAll(): void {
    const farmId = this.currentFarmId();
    if (!farmId) return;

    this.bulkConfirming.set(true);
    this.bulkFailures.set([]);
    const entryDate = this.selectedDate();
    this.inventoryService.bulkConfirmDailyConsumables({
      farmId,
      entryDate
    }).subscribe({
      next: res => {
        this.bulkConfirming.set(false);
        const failed = res.failed ?? [];
        // Name each failed entry from the list on screen.
        const names = new Map(this.entries().map(e => [e.id, e.itemName]));
        this.bulkFailuresDate.set(entryDate);
        this.bulkFailures.set(failed.map(f => ({ itemName: names.get(f.entryId) ?? this.i18n.t('Entry'), reason: f.reason })));
        if (failed.length) {
          this.notify.warning(this.i18n.t('Confirmed {n}; {failed} could not be confirmed — see the list on the page.', { n: res.confirmedCount, failed: failed.length }));
        } else {
          this.notify.success(this.i18n.t('Confirmed {n} daily consumable entries.', { n: res.confirmedCount }));
        }
        this.refreshTrigger.update(v => v + 1);
      },
      error: () => {
        this.bulkConfirming.set(false);
        this.notify.error(this.i18n.t('Failed to bulk-confirm entries.'));
      }
    });
  }

  skipEntry(entry: DailyConsumableEntry): void {
    this.inventoryService.skipDailyConsumable({
      entryId: entry.id,
      reason: 'Skipped by farm manager'
    }).subscribe({
      next: () => {
        this.notify.success(this.i18n.t('Skipped {item}.', { item: entry.itemName }));
        this.refreshTrigger.update(v => v + 1);
      },
      error: () => {
        this.notify.error(this.i18n.t('Failed to skip entry.'));
      }
    });
  }
}
