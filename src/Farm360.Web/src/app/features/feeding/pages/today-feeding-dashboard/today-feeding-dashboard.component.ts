import { CanPipe } from '../../../../shared/pipes/can.pipe';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { FeedingSubnavComponent } from '../../components/feeding-subnav/feeding-subnav.component';
import { RouterLink } from '@angular/router';
import { Component, ChangeDetectionStrategy, inject, signal, OnInit, computed, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { FeedingService } from '../../services/feeding.service';
import { DailyFeedingEntry, DailyFeedingEntryStatus } from '../../models/feeding.models';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { AdjustFeedingEntryDialogComponent } from '../../components/dialogs/adjust-feeding-entry-dialog/adjust-feeding-entry-dialog.component';
import { ManualFeedingEntryDialogComponent, ManualFeedingDialogData } from '../../components/dialogs/manual-feeding-entry-dialog/manual-feeding-entry-dialog.component';
import { ManualFeedingResult } from '../../models/feeding.models';
import { FeedingCostBreakdownDialogComponent } from '../../components/dialogs/feeding-cost-breakdown-dialog/feeding-cost-breakdown-dialog.component';
import { parseApiError } from '../../../../core/utils/error-parser';

import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { NotificationService } from '../../../../shared/services/notification.service';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
interface PenGroup {
  penName: string;
  entries: DailyFeedingEntry[];
  totalExpectedKg: number;
  totalActualKg: number;
}

interface CowGroup {
  animalId: string;
  animalTag: string;
  shedName?: string;
  penName?: string;
  entries: DailyFeedingEntry[];
  totalExpectedKg: number;
  totalActualKg: number;
  totalCostBdt: number;
  hasPending: boolean;
  allConfirmed: boolean;
}

interface ShedGroup {
  shedName: string;
  pens: PenGroup[];
  totalExpectedKg: number;
  totalActualKg: number;
  progressPct: number;
}

type ViewMode = 'cows' | 'grouped' | 'list';

@Component({
  selector: 'app-today-feeding-dashboard',
  standalone: true,
  imports: [HasPermissionDirective, CanPipe, RouterLink, ErrorStateComponent, MoneyPipe,
    FeedingSubnavComponent,
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatTooltipModule,
    PageHeaderComponent,
    EmptyStateComponent,
    TranslatePipe
  ],
  template: `
    <app-page-header
      [title]="'Confirm Planned Feed' | t"
      [description]="'Feed from your feeding plans: confirm, adjust or skip each one.' | t"
      [breadcrumbActiveNode]="'Confirm Planned Feed' | t">
      <div actions class="grid grid-cols-2 gap-2 sm:flex sm:flex-wrap sm:items-center">
        <!-- Generate Entries On-Demand -->
        <button type="button" *hasPermission="'feeding.edit'" (click)="generateEntries()" [disabled]="isGenerating()"
          class="min-h-11 sm:min-h-10 px-4 text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-700 rounded-xl transition-colors shadow-sm shadow-emerald-600/20 inline-flex items-center justify-center gap-2 text-center leading-tight disabled:opacity-60 disabled:cursor-not-allowed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 dark:focus-visible:ring-offset-gray-900">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] shrink-0" [class.animate-spin]="isGenerating()">auto_awesome</mat-icon>
          <span>{{ (isGenerating() ? 'Generating...' : (isPast() ? 'Generate Missed Day' : 'Generate Entries')) | t }}</span>
        </button>

        <!-- Feed given outside the plan -->
        <button type="button" *hasPermission="'feeding.edit'" (click)="openManualEntry()" [disabled]="!currentFarmId"
          [matTooltip]="'Record feed actually given outside the plan — a missed or extra feed, or a cow with no ration' | t"
          class="min-h-11 sm:min-h-10 px-4 text-xs font-bold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 border border-gray-200 dark:border-gray-700 rounded-xl transition-colors inline-flex items-center justify-center gap-2 text-center leading-tight disabled:opacity-60 disabled:cursor-not-allowed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 dark:focus-visible:ring-offset-gray-900">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] shrink-0">edit_note</mat-icon>
          <span>{{ 'Manual Feed Entry' | t }}</span>
        </button>

        <!-- Printable daily feeding report for the chosen date -->
        <a routerLink="/reports/feeding.daily-workflow"
          [queryParams]="{ period: selectedDate() + '..' + selectedDate(), autoRun: 'true' }"
          [matTooltip]="'Open the daily feeding report for this date' | t"
          class="min-h-11 sm:min-h-10 px-4 text-xs font-bold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 border border-gray-200 dark:border-gray-700 rounded-xl transition-colors inline-flex items-center justify-center gap-2 text-center leading-tight focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 dark:focus-visible:ring-offset-gray-900">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] shrink-0 text-emerald-600 dark:text-emerald-400">fact_check</mat-icon>
          <span>{{ 'Daily feeding report' | t }}</span>
        </a>

        <!-- Refresh Data -->
        <button type="button" (click)="loadEntries()"
          class="min-h-11 sm:min-h-10 px-4 text-xs font-bold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 border border-gray-200 dark:border-gray-700 rounded-xl transition-colors inline-flex items-center justify-center gap-2 text-center leading-tight disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 dark:focus-visible:ring-offset-gray-900">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] shrink-0" [class.animate-spin]="isLoading()">refresh</mat-icon>
          <span>{{ 'Refresh' | t }}</span>
        </button>
      </div>
    </app-page-header>

    <div class="px-0"><app-feeding-subnav></app-feeding-subnav></div>

    <div>

    <!-- ═══════════════════ Day overview: date, progress and KPI tiles ═══════════════════ -->
    <section [attr.aria-label]="'Day overview' | t"
      class="mb-4 rounded-2xl border border-gray-200/80 dark:border-gray-700/60 bg-white dark:bg-gray-800 shadow-sm p-4 space-y-4">

      <div class="flex flex-col md:flex-row md:items-center gap-4 md:gap-8">
        <!-- Date navigation: prev / date / next / Today -->
        <div role="group" [attr.aria-label]="'Choose feeding date' | t"
          class="flex items-center gap-1 rounded-xl border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-900/40 p-1 w-full md:w-auto">
          <button type="button" (click)="previousDay()" [matTooltip]="'Previous Day' | t" [attr.aria-label]="'Previous Day' | t"
            class="h-11 w-11 sm:h-10 sm:w-10 shrink-0 inline-flex items-center justify-center rounded-lg text-gray-600 dark:text-gray-300 hover:bg-white dark:hover:bg-gray-700 hover:text-gray-900 dark:hover:text-white transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500">
            <mat-icon>chevron_left</mat-icon>
          </button>

          <label class="flex-1 md:flex-none min-w-0 flex flex-col items-center justify-center px-2 rounded-lg bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 h-11 sm:h-10 focus-within:ring-2 focus-within:ring-emerald-500">
            <span class="text-[10px] font-bold uppercase tracking-wider leading-none text-gray-500 dark:text-gray-400">{{ selectedDate() | date:'EEEE' }}</span>
            <input type="date" [ngModel]="selectedDate()" (ngModelChange)="onDateSelect($event)" [attr.aria-label]="'Select date' | t"
              class="w-full min-w-0 text-center text-sm font-bold leading-tight bg-transparent border-none p-0 text-gray-900 dark:text-white focus:outline-none cursor-pointer" />
          </label>

          <button type="button" (click)="nextDay()" [matTooltip]="'Next Day' | t" [attr.aria-label]="'Next Day' | t"
            class="h-11 w-11 sm:h-10 sm:w-10 shrink-0 inline-flex items-center justify-center rounded-lg text-gray-600 dark:text-gray-300 hover:bg-white dark:hover:bg-gray-700 hover:text-gray-900 dark:hover:text-white transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500">
            <mat-icon>chevron_right</mat-icon>
          </button>

          <button type="button" (click)="setToday()" [attr.aria-pressed]="isToday()"
            [ngClass]="isToday()
              ? 'bg-emerald-600 text-white hover:bg-emerald-700'
              : 'text-emerald-700 dark:text-emerald-300 hover:bg-white dark:hover:bg-gray-700'"
            class="h-11 sm:h-10 px-4 shrink-0 rounded-lg text-xs font-bold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-1">
            {{ 'Today' | t }}
          </button>
        </div>

        <!-- Workflow progress -->
        <div class="flex-1 min-w-0">
          <div class="flex items-baseline justify-between gap-2">
            <span class="text-[11px] uppercase font-bold tracking-wider text-gray-500 dark:text-gray-400">{{ 'Workflow Progress' | t }}</span>
            <span class="text-sm font-extrabold text-emerald-700 dark:text-emerald-400 tabular-nums">{{ progressRounded() }}%</span>
          </div>
          <div class="mt-1 text-base font-bold text-gray-900 dark:text-white">
            {{ '{done} of {total} done' | t: { done: completedCount(), total: filteredEntries().length } }}
          </div>
          <div class="mt-2 h-3 w-full rounded-full bg-gray-200 dark:bg-gray-700 overflow-hidden"
            role="progressbar" [attr.aria-label]="'Feeding entries done' | t" aria-valuemin="0" aria-valuemax="100"
            [attr.aria-valuenow]="progressRounded()"
            [attr.aria-valuetext]="'{done} of {total} done' | t: { done: completedCount(), total: filteredEntries().length }">
            <div class="h-full rounded-full bg-gradient-to-r from-emerald-500 to-teal-500 transition-[width] duration-500"
              [style.width.%]="progressPct()"></div>
          </div>
          <div class="mt-2 text-xs font-medium text-gray-500 dark:text-gray-400 flex items-center justify-between gap-2">
            <span class="inline-flex items-center gap-1">
              <span class="h-2 w-2 rounded-full bg-amber-500"></span>{{ '{n} pending' | t: { n: countPending() } }}
            </span>
            <span class="tabular-nums">{{ selectedDate() | date:'d MMM y' }}</span>
          </div>
        </div>
      </div>

      <!-- KPI tiles -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-2 sm:gap-4">
        <div class="rounded-xl border border-gray-100 dark:border-gray-700/60 bg-gray-50/70 dark:bg-gray-900/40 p-4 min-w-0">
          <div class="text-[11px] uppercase font-bold text-gray-500 dark:text-gray-400 tracking-wider leading-tight">{{ 'Total Planned Feed' | t }}</div>
          <div class="text-lg sm:text-2xl font-extrabold text-emerald-700 dark:text-emerald-400 mt-1 tabular-nums break-words">
            {{ totalExpectedKg() | number:'1.2-2' }} <span class="text-xs font-medium text-gray-500 dark:text-gray-400">{{ 'kg' | t }}</span>
          </div>
        </div>
        <div class="rounded-xl border border-gray-100 dark:border-gray-700/60 bg-gray-50/70 dark:bg-gray-900/40 p-4 min-w-0">
          <div class="text-[11px] uppercase font-bold text-gray-500 dark:text-gray-400 tracking-wider leading-tight">{{ 'Feed Consumed' | t }}</div>
          <div class="text-lg sm:text-2xl font-extrabold text-blue-700 dark:text-blue-400 mt-1 tabular-nums break-words">
            {{ totalActualKg() | number:'1.2-2' }} <span class="text-xs font-medium text-gray-500 dark:text-gray-400">{{ 'kg' | t }}</span>
          </div>
        </div>
        <div class="rounded-xl border border-gray-100 dark:border-gray-700/60 bg-gray-50/70 dark:bg-gray-900/40 p-4 min-w-0">
          <div class="text-[11px] uppercase font-bold text-gray-500 dark:text-gray-400 tracking-wider leading-tight">{{ 'Total Feed Cost' | t }}</div>
          <div class="text-lg sm:text-2xl font-extrabold text-teal-700 dark:text-teal-400 mt-1 tabular-nums break-words">
            {{ totalFeedCost() | money:2 }}
          </div>
          <div class="text-[11px] text-gray-500 dark:text-gray-400 mt-1 tabular-nums">
            {{ 'Avg {cost}/kg' | t: { cost: (averageBlendedCost() | money:2) } }}
          </div>
        </div>
        <div class="rounded-xl border border-gray-100 dark:border-gray-700/60 bg-gray-50/70 dark:bg-gray-900/40 p-4 min-w-0">
          <div class="text-[11px] uppercase font-bold text-gray-500 dark:text-gray-400 tracking-wider leading-tight">{{ 'Total Feeding Entries' | t }}</div>
          <div class="text-lg sm:text-2xl font-extrabold text-gray-900 dark:text-white mt-1 tabular-nums">
            {{ filteredEntries().length }}
            @if (filteredEntries().length !== allEntries().length) {
              <span class="text-xs font-medium text-gray-500 dark:text-gray-400">{{ '(of {n})' | t: { n: allEntries().length } }}</span>
            }
          </div>
        </div>
      </div>
    </section>

    <!-- ═══════════════════ Toolbar: search, view switch, status filter ═══════════════════ -->
    <section [attr.aria-label]="'Search and filters' | t" class="mb-4 space-y-2">
      <div class="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-2">
        <!-- Search Box -->
        <div class="relative w-full lg:max-w-md">
          <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 !text-[20px] !w-[20px] !h-[20px] text-gray-400 pointer-events-none">search</mat-icon>
          <input type="search"
            [ngModel]="searchTerm()"
            (ngModelChange)="searchTerm.set($event)"
            [attr.aria-label]="'Search entries' | t"
            [placeholder]="'Search by animal tag, formula, shed, or pen...' | t"
            class="w-full h-11 pl-10 pr-4 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 placeholder-gray-400 dark:placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-emerald-500/30 focus:border-emerald-500 transition-shadow shadow-sm" />
        </div>

        <!-- View Mode Switcher -->
        <div role="group" [attr.aria-label]="'Choose view' | t"
          class="grid grid-cols-3 gap-1 p-1 rounded-xl bg-gray-100 dark:bg-gray-800 border border-gray-200/70 dark:border-gray-700/70 w-full lg:w-auto">
          @for (v of viewOptions; track v.value) {
            <button type="button" (click)="viewMode.set(v.value)"
              [matTooltip]="v.tooltip | t"
              [attr.aria-pressed]="viewMode() === v.value"
              [ngClass]="segmentClass(viewMode() === v.value)"
              class="h-10 sm:h-9 px-3 text-xs font-bold rounded-lg transition-colors inline-flex items-center justify-center gap-1.5 whitespace-nowrap focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500">
              <mat-icon class="!w-4 !h-4 !text-[16px] shrink-0">{{ v.icon }}</mat-icon> {{ v.label | t }}
            </button>
          }
        </div>
      </div>

      <!-- Status Filter Chips (scroll sideways on a phone) -->
      <div class="overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        <div role="group" [attr.aria-label]="'Filter by status' | t"
          class="inline-flex items-center gap-1 p-1 rounded-xl bg-gray-100 dark:bg-gray-800 border border-gray-200/70 dark:border-gray-700/70">
          @for (s of statusOptions; track s) {
            <button type="button" (click)="statusFilter.set(s)"
              [attr.aria-pressed]="statusFilter() === s"
              [ngClass]="segmentClass(statusFilter() === s)"
              class="h-10 sm:h-9 px-3 text-xs font-bold rounded-lg transition-colors inline-flex items-center gap-2 whitespace-nowrap focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500">
              @if (s !== 'all') {
                <span class="h-2 w-2 rounded-full shrink-0" [ngClass]="statusDotClass(s)"></span>
              }
              {{ filterLabel(s) }}
            </button>
          }
        </div>
      </div>
    </section>

    <!-- ═══════════════════ Main content ═══════════════════ -->
    <div class="relative min-h-[320px]">

      <!-- Loading skeleton -->
      @if (isLoading()) {
        <div class="space-y-4" aria-busy="true" aria-live="polite">
          <span class="sr-only">{{ 'Loading feeding entries…' | t }}</span>
          @for (s of skeletonRows; track s) {
            <div class="rounded-2xl border border-gray-200/80 dark:border-gray-700/60 bg-white dark:bg-gray-800 p-4 animate-pulse">
              <div class="flex items-center gap-4">
                <div class="h-10 w-10 rounded-xl bg-gray-200 dark:bg-gray-700"></div>
                <div class="flex-1 space-y-2">
                  <div class="h-4 w-32 rounded bg-gray-200 dark:bg-gray-700"></div>
                  <div class="h-3 w-48 max-w-full rounded bg-gray-100 dark:bg-gray-700/60"></div>
                </div>
                <div class="hidden sm:block h-10 w-28 rounded-xl bg-gray-200 dark:bg-gray-700"></div>
              </div>
              <div class="mt-4 grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-2">
                <div class="h-32 rounded-xl bg-gray-100 dark:bg-gray-700/50"></div>
                <div class="hidden sm:block h-32 rounded-xl bg-gray-100 dark:bg-gray-700/50"></div>
                <div class="hidden xl:block h-32 rounded-xl bg-gray-100 dark:bg-gray-700/50"></div>
              </div>
            </div>
          }
        </div>
      }

      @if (!isLoading() && loadError()) {
        <app-error-state [message]="loadError()!" (retry)="loadEntries()"></app-error-state>
      }

      <!-- Empty State -->
      @if (!isLoading() && !loadError() && filteredEntries().length === 0) {
        <app-empty-state
          icon="restaurant_menu"
          [title]="(allEntries().length === 0 ? 'No Feeding Entries for Selected Date' : 'No Matching Feeding Entries') | t"
          [description]="allEntries().length === 0 && !('feeding.edit' | can)
            ? ('No feeding entries were made for {date}.' | t: { date: (selectedDate() | date:'EEEE, d MMM y') })
            : allEntries().length === 0
            ? ('Nothing to confirm for {date} yet. Tap “{action}” to build the feeding list from your active feeding plans.' | t: { date: (selectedDate() | date:'EEEE, d MMM y'), action: ((isPast() ? 'Generate Missed Day' : 'Generate Entries') | t) })
            : ('No entries match your search or status filter. Clear them to see every entry for this day.' | t)"
          [actionLabel]="allEntries().length === 0 ? (('feeding.edit' | can) ? ('Generate Entries Now' | t) : undefined) : ('Reset Filters' | t)"
          (action)="allEntries().length === 0 ? generateEntries() : resetFilters()">
        </app-empty-state>
      }

      <!-- ══════════════════════════════════════════════════════════════════════════ -->
      <!-- VIEW MODE: GROUPED BY COW (ANIMAL-WISE)                                   -->
      <!-- ══════════════════════════════════════════════════════════════════════════ -->
      @if (!isLoading() && filteredEntries().length > 0 && viewMode() === 'cows') {
        <div class="space-y-4">
          @for (cow of cowGroups(); track cow.animalTag) {
            <article
              class="rounded-2xl border bg-white dark:bg-gray-800 shadow-sm overflow-hidden transition-shadow"
              [ngClass]="cow.hasPending ? 'border-gray-200 dark:border-gray-700' : 'border-gray-200/70 dark:border-gray-700/50'">

              <!-- Cow header -->
              <header class="px-4 py-4 border-b border-gray-100 dark:border-gray-700/60 flex flex-wrap items-center justify-between gap-x-4 gap-y-4"
                [ngClass]="cow.hasPending ? 'bg-gradient-to-r from-emerald-50/60 to-transparent dark:from-emerald-950/20 dark:to-transparent' : 'bg-gray-50/60 dark:bg-gray-900/30'">
                <div class="flex items-center gap-4 min-w-0 flex-1 basis-56">
                  <div class="w-11 h-11 rounded-xl flex items-center justify-center shrink-0"
                    [ngClass]="cow.hasPending ? 'bg-gradient-to-br from-emerald-500 to-teal-600 text-white shadow-md shadow-emerald-500/20' : 'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400'">
                    <mat-icon class="!text-[22px] !w-[22px] !h-[22px]">{{ cow.hasPending ? 'pets' : 'check' }}</mat-icon>
                  </div>
                  <div class="min-w-0">
                    <div class="flex items-center gap-2 min-w-0">
                      <h3 class="m-0 text-lg font-extrabold text-gray-900 dark:text-white leading-tight truncate" [title]="cow.animalTag">{{ cow.animalTag }}</h3>
                      <span class="px-2 py-0.5 rounded-full text-[11px] font-bold bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-200 whitespace-nowrap shrink-0">
                        {{ (cow.entries.length === 1 ? '{n} ration' : '{n} rations') | t: { n: cow.entries.length } }}
                      </span>
                    </div>
                    <div class="text-xs text-gray-500 dark:text-gray-400 mt-1 truncate">
                      {{ cow.shedName || ('Unassigned Shed' | t) }} / {{ cow.penName || ('Unassigned Pen' | t) }}
                    </div>
                  </div>
                </div>

                <!-- Fed vs planned, cost, and actions -->
                <div class="flex flex-wrap items-center justify-between sm:justify-end gap-x-4 gap-y-2 w-full sm:w-auto">
                  <div class="flex items-center gap-4">
                    <div class="whitespace-nowrap">
                      <div class="text-[11px] font-bold uppercase tracking-wide text-gray-500 dark:text-gray-400">{{ 'Fed / Planned' | t }}</div>
                      <div class="text-sm font-extrabold tabular-nums">
                        <span class="text-emerald-700 dark:text-emerald-400">{{ cow.totalActualKg | number:'1.2-2' }}</span>
                        <span class="text-gray-500 dark:text-gray-400 font-semibold"> / {{ cow.totalExpectedKg | number:'1.2-2' }} {{ 'kg' | t }}</span>
                      </div>
                    </div>
                    <div class="whitespace-nowrap border-l border-gray-200 dark:border-gray-700 pl-4">
                      <div class="text-[11px] font-bold uppercase tracking-wide text-gray-500 dark:text-gray-400">{{ 'Est. Cost' | t }}</div>
                      <div class="text-sm font-extrabold text-teal-700 dark:text-teal-400 tabular-nums">
                        {{ cow.totalCostBdt | money:2 }}
                      </div>
                    </div>
                  </div>

                  <div class="flex items-center gap-2 w-full sm:w-auto">
                    <button type="button" *hasPermission="'feeding.edit'" (click)="openManualEntry([cow.animalId])" [matTooltip]="'Record extra feed given to this cow' | t"
                      class="h-11 sm:h-10 px-4 flex-1 sm:flex-none text-xs font-bold text-amber-800 dark:text-amber-200 bg-amber-50 dark:bg-amber-950/40 hover:bg-amber-100 dark:hover:bg-amber-900/50 border border-amber-200 dark:border-amber-800 rounded-xl inline-flex items-center justify-center gap-1 whitespace-nowrap transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500">
                      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> {{ 'Add Feed' | t }}
                    </button>

                    @if (cow.hasPending && ('feeding.edit' | can)) {
                      <button type="button" (click)="confirmCow(cow)"
                        [matTooltip]="'Confirm all {n} pending rations for this cow' | t: { n: getPendingCountForCow(cow) }"
                        [attr.aria-label]="'Confirm ({n}) pending rations for {tag}' | t: { n: getPendingCountForCow(cow), tag: cow.animalTag }"
                        class="h-11 sm:h-10 px-4 flex-1 sm:flex-none text-sm font-bold text-white bg-emerald-600 hover:bg-emerald-700 rounded-xl transition-colors shadow-sm shadow-emerald-600/20 inline-flex items-center justify-center gap-2 whitespace-nowrap focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 dark:focus-visible:ring-offset-gray-800">
                        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">done_all</mat-icon>
                        {{ 'Confirm ({n})' | t: { n: getPendingCountForCow(cow) } }}
                      </button>
                    }

                    @if (cow.allConfirmed) {
                      <span class="h-11 sm:h-10 px-4 flex-1 sm:flex-none rounded-xl text-xs font-bold bg-emerald-50 dark:bg-emerald-950/50 text-emerald-800 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800 inline-flex items-center justify-center gap-1 whitespace-nowrap">
                        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">check_circle</mat-icon> {{ 'All Confirmed' | t }}
                      </span>
                    }
                  </div>
                </div>
              </header>

              <!-- Rations for this cow -->
              <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4 gap-2 sm:gap-4 p-2 sm:p-4">
                @for (entry of cow.entries; track entry.id) {
                  <ng-container *ngTemplateOutlet="entryCard; context: { $implicit: entry, showAnimal: false }"></ng-container>
                }
              </div>
            </article>
          }
        </div>
      }

      <!-- ══════════════════════════════════════════════════════════════════════════ -->
      <!-- VIEW MODE 2: GROUPED BY SHED & PEN                                      -->
      <!-- ══════════════════════════════════════════════════════════════════════════ -->
      @if (!isLoading() && filteredEntries().length > 0 && viewMode() === 'grouped') {
        <div class="space-y-4">
          @for (shed of groupedEntries(); track shed.shedName) {
            <article class="rounded-2xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 shadow-sm overflow-hidden">

              <!-- Shed Header -->
              <header class="px-4 py-4 bg-gray-50/80 dark:bg-gray-900/40 border-b border-gray-200 dark:border-gray-700 flex flex-wrap justify-between items-center gap-x-4 gap-y-2">
                <div class="flex items-center gap-4 min-w-0">
                  <div class="w-11 h-11 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20 shrink-0">
                    <mat-icon class="!w-[22px] !h-[22px] !text-[22px]">home_work</mat-icon>
                  </div>
                  <div class="min-w-0">
                    <h2 class="m-0 text-lg font-extrabold text-gray-900 dark:text-white leading-tight truncate">{{ shed.shedName | t }}</h2>
                    <div class="text-xs text-gray-500 dark:text-gray-400 mt-1">{{ '{pens} pens • {n} total entries' | t: { pens: shed.pens.length, n: getShedEntriesCount(shed) } }}</div>
                  </div>
                </div>
                <div class="text-sm font-semibold text-gray-600 dark:text-gray-300 tabular-nums">
                  <span class="text-success-700 dark:text-success-400 font-extrabold">{{ shed.totalActualKg | number:'1.2-2' }}{{ 'kg' | t }}</span> / {{ shed.totalExpectedKg | number:'1.2-2' }}{{ 'kg' | t }} {{ 'Confirmed' | t }}
                </div>
              </header>

              <!-- Pens -->
              <div class="divide-y divide-gray-100 dark:divide-gray-700/60">
                @for (pen of shed.pens; track pen.penName) {
                  <div class="p-2 sm:p-4">
                    <div class="flex flex-wrap justify-between items-center gap-2 mb-2 sm:mb-4 px-2 pt-2 sm:p-0">
                      <div class="flex items-center gap-2 min-w-0">
                        <mat-icon class="text-gray-400 !w-5 !h-5 !text-[20px] shrink-0">fence</mat-icon>
                        <h3 class="m-0 text-base font-bold text-gray-900 dark:text-gray-100 truncate">{{ pen.penName | t }}</h3>
                        <span class="px-2 py-0.5 rounded-full text-[11px] font-bold bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-200 whitespace-nowrap">
                          {{ '{n} entries' | t: { n: pen.entries.length } }}
                        </span>
                      </div>

                      @if (hasPending(pen) && ('feeding.edit' | can)) {
                        <button type="button" (click)="confirmPen(pen)"
                          [attr.aria-label]="'Confirm All ({n}) pending entries in {pen}' | t: { n: getPendingPenCount(pen), pen: (pen.penName | t) }"
                          class="h-11 sm:h-10 px-4 w-full sm:w-auto text-sm font-bold text-white bg-emerald-600 hover:bg-emerald-700 rounded-xl transition-colors shadow-sm shadow-emerald-600/20 inline-flex items-center justify-center gap-2 whitespace-nowrap focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 dark:focus-visible:ring-offset-gray-800">
                          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">done_all</mat-icon> {{ 'Confirm All ({n})' | t: { n: getPendingPenCount(pen) } }}
                        </button>
                      }
                    </div>

                    <!-- Entries Grid -->
                    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4 gap-2 sm:gap-4">
                      @for (entry of pen.entries; track entry.id) {
                        <ng-container *ngTemplateOutlet="entryCard; context: { $implicit: entry, showAnimal: true }"></ng-container>
                      }
                    </div>
                  </div>
                }
              </div>
            </article>
          }
        </div>
      }

      <!-- ══════════════════════════════════════════════════════════════════════════ -->
      <!-- VIEW MODE 3: FLAT LIST / TABLE VIEW                                     -->
      <!-- ══════════════════════════════════════════════════════════════════════════ -->
      @if (!isLoading() && filteredEntries().length > 0 && viewMode() === 'list') {
        <div class="rounded-2xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 shadow-sm overflow-hidden">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[960px] text-left border-collapse">
              <thead>
                <tr class="bg-gray-50 dark:bg-gray-900/50 border-b border-gray-200 dark:border-gray-700 text-[11px] uppercase tracking-wider font-bold text-gray-500 dark:text-gray-400">
                  <th scope="col" class="py-3 px-4">{{ 'Animal Tag' | t }}</th>
                  <th scope="col" class="py-3 px-4">{{ 'Location (Shed / Pen)' | t }}</th>
                  <th scope="col" class="py-3 px-4">{{ 'Ration Formula' | t }}</th>
                  <th scope="col" class="py-3 px-4">{{ 'Planned (kg)' | t }}</th>
                  <th scope="col" class="py-3 px-4">{{ 'Consumed (kg)' | t }}</th>
                  <th scope="col" class="py-3 px-4">{{ 'Unit Cost' | t }}</th>
                  <th scope="col" class="py-3 px-4">{{ 'Total Cost' | t }}</th>
                  <th scope="col" class="py-3 px-4">{{ 'Status' | t }}</th>
                  <th scope="col" class="py-3 px-4">{{ 'Notes / Reason' | t }}</th>
                  <th scope="col" class="py-3 px-4 text-right">{{ 'Actions' | t }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-700/60 text-sm">
                @for (entry of filteredEntries(); track entry.id) {
                  <tr class="hover:bg-gray-50 dark:hover:bg-gray-700/30 transition-colors"
                    [class.opacity-70]="entry.status !== 'Pending'">
                    <!-- Animal Tag -->
                    <td class="py-2 px-4">
                      <div class="flex items-center gap-2">
                        <div class="w-8 h-8 rounded-lg bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 flex items-center justify-center shrink-0">
                          <mat-icon class="!w-4 !h-4 !text-[16px]">pets</mat-icon>
                        </div>
                        <span class="font-bold text-gray-900 dark:text-white">{{ entry.animalTag }}</span>
                      </div>
                    </td>

                    <!-- Location -->
                    <td class="py-2 px-4 text-gray-600 dark:text-gray-300 font-medium">
                      {{ entry.shedName || ('Unassigned Shed' | t) }} / {{ entry.penName || ('Unassigned Pen' | t) }}
                    </td>

                    <!-- Ration Formula -->
                    <td class="py-2 px-4 font-semibold text-emerald-700 dark:text-emerald-400">
                      {{ entry.formulaName || ('Base Ration' | t) }}
                    </td>

                    <!-- Planned (kg) -->
                    <td class="py-2 px-4 font-semibold text-gray-800 dark:text-gray-200 tabular-nums whitespace-nowrap">
                      {{ entry.expectedKg | number:'1.2-2' }} {{ 'kg' | t }}
                    </td>

                    <!-- Actual (kg) -->
                    <td class="py-2 px-4 font-bold tabular-nums whitespace-nowrap"
                      [ngClass]="entry.actualKg !== null && entry.actualKg !== undefined ? 'text-emerald-700 dark:text-emerald-400' : 'text-gray-400'">
                      {{ (entry.actualKg !== null && entry.actualKg !== undefined ? entry.actualKg : '-') }} {{ entry.actualKg !== null && entry.actualKg !== undefined ? ('kg' | t) : '' }}
                    </td>

                    <!-- Unit Cost -->
                    <td class="py-2 px-4 text-xs tabular-nums whitespace-nowrap">
                      @if (entry.unitCostBdt != null) {
                        <span class="font-semibold text-emerald-700 dark:text-emerald-400">{{ entry.unitCostBdt | money:2 }}/{{ 'kg' | t }}</span>
                      } @else {
                        <span class="text-gray-400">-</span>
                      }
                    </td>

                    <!-- Total Cost -->
                    <td class="py-2 px-4 text-xs font-bold tabular-nums whitespace-nowrap">
                      @if (entry.totalCostBdt != null || entry.unitCostBdt != null) {
                        <span class="text-teal-700 dark:text-teal-300">
                          {{ (entry.totalCostBdt ?? ((entry.actualKg ?? entry.expectedKg) * (entry.unitCostBdt ?? 0))) | money:2 }}
                        </span>
                      } @else {
                        <span class="text-gray-400">-</span>
                      }
                    </td>

                    <!-- Status Badge -->
                    <td class="py-2 px-4">
                      <span class="h-6 px-2 rounded-full border text-[11px] font-bold inline-flex items-center gap-1 whitespace-nowrap"
                        [ngClass]="statusPillClass(entry.status)">
                        <mat-icon class="!w-3.5 !h-3.5 !text-[14px]">{{ statusIcon(entry.status) }}</mat-icon>
                        {{ entry.status | t }}
                      </span>
                    </td>

                    <!-- Notes -->
                    <td class="py-2 px-4 text-xs text-gray-500 dark:text-gray-400 max-w-xs truncate">
                      {{ entry.notes || '-' }}
                    </td>

                    <!-- Actions -->
                    <td class="py-2 px-4 text-right">
                      <div class="flex items-center justify-end gap-1">
                        <button type="button" (click)="openCostBreakdown(entry)" [matTooltip]="'Cost Breakdown' | t" [attr.aria-label]="'Cost Breakdown' | t"
                          class="h-10 w-10 inline-flex items-center justify-center text-teal-700 dark:text-teal-300 hover:bg-teal-50 dark:hover:bg-teal-950/40 rounded-lg transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-500">
                          <mat-icon class="!w-5 !h-5 !text-[20px]">payments</mat-icon>
                        </button>

                        @if (entry.status === 'Pending' && ('feeding.edit' | can)) {
                          <button type="button" (click)="confirmEntry(entry)" [matTooltip]="'Confirm Planned Amount' | t"
                            [attr.aria-label]="'Confirm planned amount for {tag}' | t: { tag: entry.animalTag }"
                            class="h-10 w-10 inline-flex items-center justify-center text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-1">
                            <mat-icon class="!w-5 !h-5 !text-[20px]">check</mat-icon>
                          </button>
                          <button type="button" (click)="openAdjustDialog(entry, 'adjust')" [matTooltip]="'Adjust Amount' | t"
                            [attr.aria-label]="'Adjust Amount for {tag}' | t: { tag: entry.animalTag }"
                            class="h-10 w-10 inline-flex items-center justify-center text-blue-700 dark:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-950/40 rounded-lg transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500">
                            <mat-icon class="!w-5 !h-5 !text-[20px]">tune</mat-icon>
                          </button>
                          <button type="button" (click)="openAdjustDialog(entry, 'skip')" [matTooltip]="'Skip Entry' | t"
                            [attr.aria-label]="'Skip Feeding for {tag}' | t: { tag: entry.animalTag }"
                            class="h-10 w-10 inline-flex items-center justify-center text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-gray-400">
                            <mat-icon class="!w-5 !h-5 !text-[20px]">block</mat-icon>
                          </button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </div>

    <!-- ═══════════════════ Phone: sticky progress + pending shortcut ═══════════════════ -->
    @if (!isLoading() && allEntries().length > 0) {
      <div class="md:hidden sticky bottom-2 z-20 mt-4">
        <div class="rounded-2xl border border-gray-200 dark:border-gray-700 bg-white/95 dark:bg-gray-800/95 backdrop-blur shadow-lg shadow-gray-900/10 p-2 flex items-center gap-2">
          <div class="flex-1 min-w-0 px-2">
            <div class="text-sm font-bold text-gray-900 dark:text-white truncate">
              {{ '{done} of {total} done' | t: { done: completedCount(), total: filteredEntries().length } }}
            </div>
            <div class="mt-1 h-2 w-full rounded-full bg-gray-200 dark:bg-gray-700 overflow-hidden" aria-hidden="true">
              <div class="h-full rounded-full bg-gradient-to-r from-emerald-500 to-teal-500 transition-[width] duration-500"
                [style.width.%]="progressPct()"></div>
            </div>
          </div>
          @if (statusFilter() !== 'Pending' && countPending() > 0) {
            <button type="button" (click)="statusFilter.set('Pending')"
              class="h-11 px-4 shrink-0 rounded-xl text-sm font-bold text-white bg-emerald-600 hover:bg-emerald-700 inline-flex items-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 dark:focus-visible:ring-offset-gray-800">
              <span class="h-2 w-2 rounded-full bg-amber-300"></span>
              {{ filterLabel('Pending') }}
            </button>
          } @else if (statusFilter() !== 'all') {
            <button type="button" (click)="statusFilter.set('all')"
              class="h-11 px-4 shrink-0 rounded-xl text-sm font-bold text-gray-700 dark:text-gray-200 bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 inline-flex items-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500">
              {{ filterLabel('all') }}
            </button>
          }
        </div>
      </div>
    }

    </div>

    <!-- ═══════════════════ Shared entry card (By Cow and By Pen) ═══════════════════ -->
    <ng-template #entryCard let-entry let-showAnimal="showAnimal">
      <div class="flex flex-col h-full rounded-xl border p-4 transition-colors"
        [ngClass]="entry.status === 'Pending'
          ? 'bg-white dark:bg-gray-800 border-amber-200 dark:border-amber-800/60 shadow-sm'
          : 'bg-gray-50 dark:bg-gray-900/40 border-gray-200 dark:border-gray-700/60'">

        <!-- Tag / ration + status pill -->
        <div class="flex items-start justify-between gap-2">
          <div class="min-w-0">
            @if (showAnimal) {
              <div class="flex items-center gap-2 min-w-0">
                <span class="w-7 h-7 rounded-lg bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 flex items-center justify-center shrink-0">
                  <mat-icon class="!w-4 !h-4 !text-[16px]">pets</mat-icon>
                </span>
                <span class="text-base font-extrabold text-gray-900 dark:text-white truncate" [title]="entry.animalTag">{{ entry.animalTag }}</span>
              </div>
            }
            <div class="text-sm font-semibold text-gray-700 dark:text-gray-200 truncate" [class.mt-1]="showAnimal"
              [matTooltip]="entry.formulaName || ('Base Ration' | t)">
              {{ entry.formulaName || ('Base Ration' | t) }}
            </div>
          </div>
          <span class="h-6 px-2 shrink-0 rounded-full border text-[11px] font-bold inline-flex items-center gap-1 whitespace-nowrap"
            [ngClass]="statusPillClass(entry.status)">
            <mat-icon class="!w-3.5 !h-3.5 !text-[14px]">{{ statusIcon(entry.status) }}</mat-icon>
            {{ entry.status | t }}
          </span>
        </div>

        <!-- Quantity & unit cost -->
        <div class="mt-2 flex items-end justify-between gap-2">
          <div class="text-2xl font-extrabold tabular-nums leading-none"
            [ngClass]="entry.status === 'Pending' ? 'text-gray-900 dark:text-white' : 'text-gray-600 dark:text-gray-300'">
            {{ (entry.actualKg !== null && entry.actualKg !== undefined ? entry.actualKg : entry.expectedKg) | number:'1.2-2' }}
            <span class="text-sm font-semibold text-gray-500 dark:text-gray-400">{{ 'kg' | t }}</span>
            @if (entry.status === 'Adjusted' && entry.actualKg !== entry.expectedKg) {
              <span class="text-xs font-medium text-gray-400 line-through ml-1">{{ entry.expectedKg | number:'1.2-2' }}{{ 'kg' | t }}</span>
            }
          </div>

          <button type="button" (click)="openCostBreakdown(entry)"
            [matTooltip]="entry.unitCostBdt != null ? ('Snapshotted cost: ৳{cost}/kg. Click for breakdown.' | t: { cost: (entry.unitCostBdt | number:'1.2-2') }) : ('Click to inspect planned cost breakdown.' | t)"
            [attr.aria-label]="'Cost breakdown for {tag}' | t: { tag: entry.animalTag }"
            class="h-8 px-2 shrink-0 inline-flex items-center gap-1 rounded-lg text-xs font-semibold border transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            [ngClass]="entry.unitCostBdt != null
              ? 'bg-emerald-50 dark:bg-emerald-950/50 text-emerald-800 dark:text-emerald-300 hover:bg-emerald-100 dark:hover:bg-emerald-900/60 border-emerald-200/80 dark:border-emerald-800/80'
              : 'bg-gray-100 dark:bg-gray-700/60 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700 border-gray-200 dark:border-gray-600'">
            <mat-icon class="!w-4 !h-4 !text-[16px]">payments</mat-icon>
            <span>{{ entry.unitCostBdt != null ? '৳' + (entry.unitCostBdt | number:'1.2-2') + '/' + ('kg' | t) : ('Cost' | t) }}</span>
          </button>
        </div>

        <!-- Entry cost -->
        @if (entry.totalCostBdt != null || entry.unitCostBdt != null) {
          <div class="mt-2 text-xs text-gray-500 dark:text-gray-400 flex justify-between items-center gap-2">
            <span>{{ 'Ration Cost' | t }}</span>
            <span class="font-bold text-teal-700 dark:text-teal-300 tabular-nums">
              {{ (entry.totalCostBdt ?? ((entry.actualKg ?? entry.expectedKg) * (entry.unitCostBdt ?? 0))) | money:2 }}
            </span>
          </div>
        }

        <!-- Actions for Pending: Confirm is the big primary; Adjust / Skip are secondary -->
        @if (entry.status === 'Pending' && ('feeding.edit' | can)) {
          <div class="mt-auto pt-4 space-y-2">
            <button type="button" (click)="confirmEntry(entry)"
              [attr.aria-label]="'Confirm {kg} kg for {tag}' | t: { kg: (entry.expectedKg | number:'1.2-2'), tag: entry.animalTag }"
              class="w-full h-12 rounded-xl text-base font-bold text-white bg-emerald-600 hover:bg-emerald-700 active:bg-emerald-800 shadow-sm shadow-emerald-600/20 inline-flex items-center justify-center gap-2 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 dark:focus-visible:ring-offset-gray-800">
              <mat-icon class="!w-5 !h-5 !text-[20px]">check</mat-icon> {{ 'Confirm' | t }}
            </button>
            <div class="grid grid-cols-2 gap-2">
              <button type="button" (click)="openAdjustDialog(entry, 'adjust')"
                [attr.aria-label]="'Adjust Amount for {tag}' | t: { tag: entry.animalTag }"
                class="h-11 px-2 rounded-xl text-xs font-bold text-blue-800 dark:text-blue-200 bg-blue-50 dark:bg-blue-950/40 hover:bg-blue-100 dark:hover:bg-blue-900/50 border border-blue-200 dark:border-blue-800/70 inline-flex items-center justify-center gap-1 whitespace-nowrap transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500">
                <mat-icon class="!w-4 !h-4 !text-[16px] shrink-0">tune</mat-icon> {{ 'Adjust Amount' | t }}
              </button>
              <button type="button" (click)="openAdjustDialog(entry, 'skip')"
                [attr.aria-label]="'Skip Feeding for {tag}' | t: { tag: entry.animalTag }"
                class="h-11 px-2 rounded-xl text-xs font-bold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700 border border-gray-200 dark:border-gray-600 inline-flex items-center justify-center gap-1 whitespace-nowrap transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-gray-400">
                <mat-icon class="!w-4 !h-4 !text-[16px] shrink-0">block</mat-icon> {{ 'Skip Feeding' | t }}
              </button>
            </div>
          </div>
        } @else if (entry.notes) {
          <p class="mt-auto pt-2 mb-0 text-xs italic text-gray-500 dark:text-gray-400 break-words">{{ entry.notes }}</p>
        }
      </div>
    </ng-template>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TodayFeedingDashboardComponent implements OnInit {
  private readonly feedingService = inject(FeedingService);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotificationService);
  private readonly contextService = inject(WorkingContextService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly i18n = inject(I18nService);

  // Signals
  readonly isLoading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly isGenerating = signal(false);
  readonly allEntries = signal<DailyFeedingEntry[]>([]);
  readonly selectedDate = signal<string>(toLocalIsoDate());
  readonly searchTerm = signal<string>('');
  readonly statusFilter = signal<string>('all');
  readonly viewMode = signal<ViewMode>('cows');

  protected currentFarmId: string | null = null;

  // Presentation-only option lists (labels unchanged from the user guide)
  readonly viewOptions: ReadonlyArray<{ value: ViewMode; label: string; icon: string; tooltip: string }> = [
    { value: 'cows', label: 'By Cow', icon: 'pets', tooltip: 'Grouped by Cow / Animal' },
    { value: 'grouped', label: 'By Pen', icon: 'view_agenda', tooltip: 'Grouped by Shed and Pen' },
    { value: 'list', label: 'List View', icon: 'table_rows', tooltip: 'Table List of All Entries' }
  ];
  readonly statusOptions: ReadonlyArray<string> = ['all', 'Pending', 'Confirmed', 'Adjusted', 'Skipped'];
  readonly skeletonRows: ReadonlyArray<number> = [1, 2, 3];

  // Filtered entries computed signal
  readonly filteredEntries = computed<DailyFeedingEntry[]>(() => {
    const list = this.allEntries();
    const term = this.searchTerm().trim().toLowerCase();
    const status = this.statusFilter();

    return list.filter(e => {
      // Status filter
      if (status !== 'all' && e.status !== status) {
        return false;
      }

      // Search term filter
      if (term) {
        const matchesTag = e.animalTag.toLowerCase().includes(term);
        const matchesFormula = (e.formulaName || '').toLowerCase().includes(term);
        const matchesShed = (e.shedName || '').toLowerCase().includes(term);
        const matchesPen = (e.penName || '').toLowerCase().includes(term);
        const matchesNotes = (e.notes || '').toLowerCase().includes(term);

        return matchesTag || matchesFormula || matchesShed || matchesPen || matchesNotes;
      }

      return true;
    });
  });

  // KPI Computations — across every head an entry covers (group plans), kg-only (ml extras are not kg),
  // and cost/consumption from confirmed entries only (a pending entry's cost is just a plan).
  private static heads(e: DailyFeedingEntry): number {
    return e.headCount && e.headCount > 0 ? e.headCount : 1;
  }

  private static isKg(e: DailyFeedingEntry): boolean {
    return !e.unit || e.unit.toLowerCase() === 'kg';
  }

  private static isFed(e: DailyFeedingEntry): boolean {
    return e.status === 'Confirmed' || e.status === 'Adjusted';
  }

  private static fedKg(e: DailyFeedingEntry): number {
    return e.totalKg ?? (e.actualKg ?? 0) * TodayFeedingDashboardComponent.heads(e);
  }

  private static fedCost(e: DailyFeedingEntry): number {
    if (e.totalCostBdt != null) return e.totalCostBdt;
    if (e.unitCostBdt != null) return TodayFeedingDashboardComponent.fedKg(e) * e.unitCostBdt;
    return 0;
  }

  readonly totalExpectedKg = computed(() => {
    return this.filteredEntries()
      .filter(e => TodayFeedingDashboardComponent.isKg(e) && e.status !== 'Skipped')
      .reduce((sum, e) => sum + e.expectedKg * TodayFeedingDashboardComponent.heads(e), 0);
  });

  readonly totalActualKg = computed(() => {
    return this.filteredEntries()
      .filter(e => TodayFeedingDashboardComponent.isFed(e) && TodayFeedingDashboardComponent.isKg(e))
      .reduce((sum, e) => sum + TodayFeedingDashboardComponent.fedKg(e), 0);
  });

  readonly totalFeedCost = computed(() => {
    return this.filteredEntries()
      .filter(e => TodayFeedingDashboardComponent.isFed(e))
      .reduce((sum, e) => sum + TodayFeedingDashboardComponent.fedCost(e), 0);
  });

  /** Cost per kg of the kg feed actually given (ml extras excluded from both sides). */
  readonly averageBlendedCost = computed(() => {
    const fed = this.filteredEntries().filter(e => TodayFeedingDashboardComponent.isFed(e) && TodayFeedingDashboardComponent.isKg(e));
    const kg = fed.reduce((sum, e) => sum + TodayFeedingDashboardComponent.fedKg(e), 0);
    const cost = fed.reduce((sum, e) => sum + TodayFeedingDashboardComponent.fedCost(e), 0);
    return kg > 0 ? cost / kg : 0;
  });

  readonly progressPct = computed(() => {
    const entries = this.filteredEntries();
    if (entries.length === 0) return 0;
    const completed = entries.filter(e => e.status !== 'Pending').length;
    return (completed / entries.length) * 100;
  });

  /** Entries no longer pending (confirmed, adjusted or skipped) — the numerator of progressPct. */
  readonly completedCount = computed(() => this.filteredEntries().filter(e => e.status !== 'Pending').length);

  readonly progressRounded = computed(() => Math.round(this.progressPct()));

  readonly countPending = computed(() => {
    return this.allEntries().filter(e => e.status === 'Pending').length;
  });

  /** A past day: "Generate" then catches up the entries the job missed. */
  readonly isPast = computed(() => this.selectedDate() < toLocalIsoDate());

  readonly isToday = computed(() => {
    const today = toLocalIsoDate();
    return this.selectedDate() === today;
  });

  // Grouped entries computed signal
  readonly groupedEntries = computed<ShedGroup[]>(() => {
    const entries = this.filteredEntries();
    const shedMap = new Map<string, ShedGroup>();

    entries.forEach(entry => {
      const shedName = entry.shedName || 'Unassigned Shed';
      const penName = entry.penName || 'Unassigned Pen';

      if (!shedMap.has(shedName)) {
        shedMap.set(shedName, {
          shedName,
          pens: [],
          totalExpectedKg: 0,
          totalActualKg: 0,
          progressPct: 0
        });
      }

      const shedGroup = shedMap.get(shedName)!;
      shedGroup.totalExpectedKg += entry.expectedKg;
      shedGroup.totalActualKg += entry.actualKg ?? 0;

      let penGroup = shedGroup.pens.find(p => p.penName === penName);
      if (!penGroup) {
        penGroup = { penName, entries: [], totalExpectedKg: 0, totalActualKg: 0 };
        shedGroup.pens.push(penGroup);
      }

      penGroup.totalExpectedKg += entry.expectedKg;
      penGroup.totalActualKg += entry.actualKg ?? 0;
      penGroup.entries.push(entry);
    });

    return Array.from(shedMap.values());
  });

  // Cow-wise grouped entries computed signal
  readonly cowGroups = computed<CowGroup[]>(() => {
    const entries = this.filteredEntries();
    const map = new Map<string, CowGroup>();

    entries.forEach(entry => {
      const tag = entry.animalTag || 'General Herd';
      if (!map.has(tag)) {
        map.set(tag, {
          animalId: entry.animalId,
          animalTag: tag,
          shedName: entry.shedName,
          penName: entry.penName,
          entries: [],
          totalExpectedKg: 0,
          totalActualKg: 0,
          totalCostBdt: 0,
          hasPending: false,
          allConfirmed: true
        });
      }

      const cow = map.get(tag)!;
      cow.entries.push(entry);
      cow.totalExpectedKg += entry.expectedKg;
      cow.totalActualKg += (entry.actualKg ?? 0);
      const cost = entry.totalCostBdt ?? ((entry.actualKg ?? entry.expectedKg) * (entry.unitCostBdt ?? 0));
      cow.totalCostBdt += cost;
      if (entry.status === 'Pending') {
        cow.hasPending = true;
        cow.allConfirmed = false;
      }
    });

    return Array.from(map.values()).sort((a, b) => a.animalTag.localeCompare(b.animalTag));
  });

  ngOnInit(): void {
    // React to active farm changes
    this.contextService.currentFarm$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(farm => {
        this.currentFarmId = farm?.id || null;
        this.loadEntries();
      });
  }

  loadEntries(): void {
    this.isLoading.set(true);
    this.loadError.set(null);
    const farmId = this.currentFarmId || undefined;
    const date = this.selectedDate();

    this.feedingService.getTodayEntries(farmId, date).subscribe({
      next: (res) => {
        this.allEntries.set(res);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.allEntries.set([]);
        this.loadError.set(parseApiError(err, "Could not load today's feeding entries."));
        this.isLoading.set(false);
      }
    });
  }

  generateEntries(): void {
    this.isGenerating.set(true);
    this.feedingService.generateEntries(this.selectedDate(), this.currentFarmId || undefined).subscribe({
      next: (res) => {
        this.notify.success(res.message || this.i18n.t('Daily entries generated successfully.'));
        this.isGenerating.set(false);
        this.loadEntries();
      },
      error: (err) => {
        this.notify.error(err.error?.detail || this.i18n.t('Failed to generate entries.'));
        this.isGenerating.set(false);
      }
    });
  }

  /** Feed given outside the plan for the selected day; saved as confirmed (stock, cost and reports follow). */
  openManualEntry(animalIds?: string[]): void {
    if (!this.currentFarmId) return;
    const data: ManualFeedingDialogData = { farmId: this.currentFarmId, date: this.selectedDate(), animalIds };
    this.dialog.open<ManualFeedingEntryDialogComponent, ManualFeedingDialogData, ManualFeedingResult>(
      ManualFeedingEntryDialogComponent, { data, width: '640px', maxWidth: '96vw', maxHeight: '92vh', panelClass: ['!rounded-2xl', '!bg-white', 'dark:!bg-gray-900'] })
      .afterClosed().subscribe(res => {
        if (!res) return;
        const msg = this.i18n.t(res.recorded === 1 ? 'Recorded feed for {n} animal.' : 'Recorded feed for {n} animals.', { n: res.recorded }) + (res.notes.length ? ' ' + res.notes.join(' ') : '');
        this.notify.success(msg);
        this.loadEntries();
      });
  }

  confirmEntry(entry: DailyFeedingEntry): void {
    this.feedingService.confirmEntry(entry.id, entry.expectedKg).subscribe({
      next: () => {
        this.notify.success(this.i18n.t('Confirmed feed for {tag}', { tag: entry.animalTag }));
        this.loadEntries();
      },
      error: (err) => {
        this.notify.error(parseApiError(err, this.i18n.t('Failed to confirm entry')));
      }
    });
  }

  openAdjustDialog(entry: DailyFeedingEntry, action: 'adjust' | 'skip'): void {
    const dialogRef = this.dialog.open(AdjustFeedingEntryDialogComponent, { maxWidth: '95vw',
      disableClose: true,
      width: '500px',
      data: { entry, action }
    });

    dialogRef.afterClosed().subscribe(res => {
      if (res) this.loadEntries();
    });
  }

  openCostBreakdown(entry: DailyFeedingEntry): void {
    this.dialog.open(FeedingCostBreakdownDialogComponent, {
      width: '720px',
      maxWidth: '95vw',
      panelClass: 'cost-breakdown-dialog-container',
      data: { entry }
    });
  }

  confirmPen(pen: PenGroup): void {
    const pendingEntries = pen.entries.filter(e => e.status === 'Pending');

    if (pendingEntries.length === 0) {
      this.notify.warning(this.i18n.t('All entries in {pen} are already processed.', { pen: this.i18n.t(pen.penName) }));
      return;
    }

    const observables = pendingEntries.map(e => this.feedingService.confirmEntry(e.id, e.expectedKg));

    this.isLoading.set(true);
    forkJoin(observables).subscribe({
      next: () => {
        this.notify.success(this.i18n.t('Confirmed {n} entries for {pen}', { n: pendingEntries.length, pen: this.i18n.t(pen.penName) }));
        this.loadEntries();
      },
      error: (err) => {
        this.notify.error(parseApiError(err, this.i18n.t('Some confirmations failed')));
        this.loadEntries();
      }
    });
  }

  // Date stepper methods
  previousDay(): void {
    const current = new Date(this.selectedDate());
    current.setDate(current.getDate() - 1);
    this.selectedDate.set(toLocalIsoDate(current));
    this.loadEntries();
  }

  nextDay(): void {
    const current = new Date(this.selectedDate());
    current.setDate(current.getDate() + 1);
    this.selectedDate.set(toLocalIsoDate(current));
    this.loadEntries();
  }

  setToday(): void {
    const today = toLocalIsoDate();
    if (this.selectedDate() !== today) {
      this.selectedDate.set(today);
      this.loadEntries();
    }
  }

  onDateSelect(date: string): void {
    if (date && date !== this.selectedDate()) {
      this.selectedDate.set(date);
      this.loadEntries();
    }
  }

  resetFilters(): void {
    this.searchTerm.set('');
    this.statusFilter.set('all');
  }

  // Presentation helpers
  filterLabel(status: string): string {
    if (status === 'all') return this.i18n.t('All ({n})', { n: this.allEntries().length });
    if (status === 'Pending') return this.i18n.t('Pending ({n})', { n: this.countPending() });
    return this.i18n.t(status);
  }

  segmentClass(active: boolean): string {
    return active
      ? 'bg-white dark:bg-gray-700 text-emerald-700 dark:text-emerald-300 shadow-sm ring-1 ring-black/5 dark:ring-white/10'
      : 'text-gray-600 dark:text-gray-300 hover:text-gray-900 dark:hover:text-white hover:bg-white/60 dark:hover:bg-gray-700/50';
  }

  statusPillClass(status: string): string {
    switch (status) {
      case 'Pending':
        return 'bg-amber-50 text-amber-800 border-amber-200 dark:bg-amber-950/50 dark:text-amber-300 dark:border-amber-800/70';
      case 'Confirmed':
        return 'bg-emerald-50 text-emerald-800 border-emerald-200 dark:bg-emerald-950/50 dark:text-emerald-300 dark:border-emerald-800/70';
      case 'Adjusted':
        return 'bg-blue-50 text-blue-800 border-blue-200 dark:bg-blue-950/50 dark:text-blue-300 dark:border-blue-800/70';
      default:
        return 'bg-gray-100 text-gray-700 border-gray-200 dark:bg-gray-700/60 dark:text-gray-300 dark:border-gray-600';
    }
  }

  statusDotClass(status: string): string {
    switch (status) {
      case 'Pending': return 'bg-amber-500';
      case 'Confirmed': return 'bg-success-500';
      case 'Adjusted': return 'bg-blue-500';
      default: return 'bg-gray-400';
    }
  }

  statusIcon(status: string): string {
    switch (status) {
      case 'Pending': return 'schedule';
      case 'Confirmed': return 'check_circle';
      case 'Adjusted': return 'tune';
      default: return 'block';
    }
  }

  // Helpers
  getShedEntriesCount(shed: ShedGroup): number {
    return shed.pens.reduce((sum, p) => sum + p.entries.length, 0);
  }

  hasPending(pen: PenGroup): boolean {
    return pen.entries.some(e => e.status === 'Pending');
  }

  getPendingPenCount(pen: PenGroup): number {
    return pen.entries.filter(e => e.status === 'Pending').length;
  }

  getPendingCountForCow(cow: CowGroup): number {
    return cow.entries.filter(e => e.status === 'Pending').length;
  }

  confirmCow(cow: CowGroup): void {
    const pendingEntries = cow.entries.filter(e => e.status === 'Pending');

    if (pendingEntries.length === 0) {
      this.notify.warning(this.i18n.t('All rations for {tag} are already processed.', { tag: cow.animalTag }));
      return;
    }

    const observables = pendingEntries.map(e => this.feedingService.confirmEntry(e.id, e.expectedKg));

    this.isLoading.set(true);
    forkJoin(observables).subscribe({
      next: () => {
        this.notify.success(this.i18n.t('Confirmed all rations for {tag}', { tag: cow.animalTag }));
        this.loadEntries();
      },
      error: (err) => {
        this.notify.error(parseApiError(err, this.i18n.t('Some confirmations failed')));
        this.loadEntries();
      }
    });
  }
}
