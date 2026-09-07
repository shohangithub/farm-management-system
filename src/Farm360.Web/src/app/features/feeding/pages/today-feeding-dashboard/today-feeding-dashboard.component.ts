import { Component, ChangeDetectionStrategy, inject, signal, OnInit, computed, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { FeedingService } from '../../services/feeding.service';
import { DailyFeedingEntry, DailyFeedingEntryStatus } from '../../models/feeding.models';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { AdjustFeedingEntryDialogComponent } from '../../components/dialogs/adjust-feeding-entry-dialog/adjust-feeding-entry-dialog.component';

interface PenGroup {
  penName: string;
  entries: DailyFeedingEntry[];
  totalExpectedKg: number;
  totalActualKg: number;
}

interface ShedGroup {
  shedName: string;
  pens: PenGroup[];
  totalExpectedKg: number;
  totalActualKg: number;
  progressPct: number;
}

@Component({
  selector: 'app-today-feeding-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatDialogModule,
    MatTooltipModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Daily Feeding Entries & Workflow"
      description="Manage and confirm daily feeding instructions based on active feeding plans across all sheds."
      breadcrumbActiveNode="Today's Feeding">
      <div actions class="flex flex-wrap items-center gap-2.5">
        <!-- Date Stepper / Picker Controls -->
        <div class="flex items-center bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-1 shadow-sm">
          <button type="button" (click)="previousDay()" matTooltip="Previous Day"
            class="p-1 text-gray-500 hover:text-gray-900 dark:hover:text-white rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">chevron_left</mat-icon>
          </button>

          <button type="button" (click)="setToday()"
            [class.bg-emerald-50]="isToday()"
            [class.text-emerald-700]="isToday()"
            [class.dark:bg-emerald-950/60]="isToday()"
            [class.dark:text-emerald-400]="isToday()"
            class="px-2.5 py-1 text-xs font-bold text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
            Today
          </button>

          <button type="button" (click)="nextDay()" matTooltip="Next Day"
            class="p-1 text-gray-500 hover:text-gray-900 dark:hover:text-white rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">chevron_right</mat-icon>
          </button>

          <div class="h-4 w-px bg-gray-200 dark:bg-gray-700 mx-1"></div>

          <input type="date" [ngModel]="selectedDate()" (ngModelChange)="onDateSelect($event)"
            class="text-xs font-semibold bg-transparent border-none text-gray-700 dark:text-gray-300 focus:outline-none cursor-pointer pr-1" />
        </div>

        <!-- Generate Entries On-Demand -->
        <button (click)="generateEntries()" [disabled]="isGenerating()"
          class="px-3.5 py-2 text-xs font-semibold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 hover:bg-emerald-100 dark:hover:bg-emerald-900/50 border border-emerald-200 dark:border-emerald-800 rounded-xl transition-colors shadow-sm inline-flex items-center gap-1.5 disabled:opacity-50">
          <mat-icon class="!text-[16px] !w-[16px] !h-[16px]" [class.animate-spin]="isGenerating()">auto_awesome</mat-icon>
          {{ isGenerating() ? 'Generating...' : 'Generate Entries' }}
        </button>

        <!-- Refresh Data -->
        <button (click)="loadEntries()"
          class="px-3.5 py-2 text-xs font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700 rounded-xl transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">refresh</mat-icon> Refresh
        </button>
      </div>
    </app-page-header>

    <!-- Global Toolbar / Filter Bar -->
    <div class="mb-6 flex flex-col md:flex-row items-stretch md:items-center justify-between gap-4">
      <!-- Search Box -->
      <div class="relative flex-1 max-w-md">
        <mat-icon class="absolute left-3.5 top-1/2 -translate-y-1/2 !text-[18px] !w-[18px] !h-[18px] text-gray-400">search</mat-icon>
        <input
          [ngModel]="searchTerm()"
          (ngModelChange)="searchTerm.set($event)"
          placeholder="Search by animal tag, formula, shed, or pen..."
          class="w-full pl-10 pr-4 py-2 text-xs rounded-xl border border-gray-200 dark:border-gray-700 bg-white/80 dark:bg-gray-800/80 backdrop-blur-sm text-gray-800 dark:text-gray-200 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all shadow-sm" />
      </div>

      <!-- View Switcher & Status Filter Chips -->
      <div class="flex flex-wrap items-center gap-2">
        <!-- Status Filter Pills -->
        <div class="flex items-center bg-gray-100/80 dark:bg-gray-800/80 p-1 rounded-xl border border-gray-200/50 dark:border-gray-700/50 text-xs">
          <button type="button" (click)="statusFilter.set('all')"
            [class.bg-white]="statusFilter() === 'all'"
            [class.dark:bg-gray-700]="statusFilter() === 'all'"
            [class.text-emerald-700]="statusFilter() === 'all'"
            [class.dark:text-emerald-400]="statusFilter() === 'all'"
            [class.shadow-sm]="statusFilter() === 'all'"
            class="px-2.5 py-1 rounded-lg font-semibold text-gray-600 dark:text-gray-300 transition-all">
            All ({{ allEntries().length }})
          </button>
          <button type="button" (click)="statusFilter.set('Pending')"
            [class.bg-white]="statusFilter() === 'Pending'"
            [class.dark:bg-gray-700]="statusFilter() === 'Pending'"
            [class.text-amber-600]="statusFilter() === 'Pending'"
            [class.shadow-sm]="statusFilter() === 'Pending'"
            class="px-2.5 py-1 rounded-lg font-semibold text-gray-600 dark:text-gray-300 transition-all">
            Pending ({{ countPending() }})
          </button>
          <button type="button" (click)="statusFilter.set('Confirmed')"
            [class.bg-white]="statusFilter() === 'Confirmed'"
            [class.dark:bg-gray-700]="statusFilter() === 'Confirmed'"
            [class.text-emerald-600]="statusFilter() === 'Confirmed'"
            [class.shadow-sm]="statusFilter() === 'Confirmed'"
            class="px-2.5 py-1 rounded-lg font-semibold text-gray-600 dark:text-gray-300 transition-all">
            Confirmed
          </button>
          <button type="button" (click)="statusFilter.set('Adjusted')"
            [class.bg-white]="statusFilter() === 'Adjusted'"
            [class.dark:bg-gray-700]="statusFilter() === 'Adjusted'"
            [class.text-blue-600]="statusFilter() === 'Adjusted'"
            [class.shadow-sm]="statusFilter() === 'Adjusted'"
            class="px-2.5 py-1 rounded-lg font-semibold text-gray-600 dark:text-gray-300 transition-all">
            Adjusted
          </button>
          <button type="button" (click)="statusFilter.set('Skipped')"
            [class.bg-white]="statusFilter() === 'Skipped'"
            [class.dark:bg-gray-700]="statusFilter() === 'Skipped'"
            [class.text-red-600]="statusFilter() === 'Skipped'"
            [class.shadow-sm]="statusFilter() === 'Skipped'"
            class="px-2.5 py-1 rounded-lg font-semibold text-gray-600 dark:text-gray-300 transition-all">
            Skipped
          </button>
        </div>

        <!-- View Mode Switcher -->
        <div class="flex items-center bg-gray-100/80 dark:bg-gray-800/80 p-1 rounded-xl border border-gray-200/50 dark:border-gray-700/50">
          <button type="button" (click)="viewMode.set('grouped')"
            [class.bg-white]="viewMode() === 'grouped'"
            [class.dark:bg-gray-700]="viewMode() === 'grouped'"
            [class.text-emerald-700]="viewMode() === 'grouped'"
            [class.dark:text-emerald-400]="viewMode() === 'grouped'"
            [class.shadow-sm]="viewMode() === 'grouped'"
            matTooltip="Grouped by Shed and Pen"
            class="px-3 py-1 text-xs font-semibold rounded-lg text-gray-600 dark:text-gray-300 transition-all flex items-center gap-1.5">
            <mat-icon class="!w-4 !h-4 !text-[16px]">view_agenda</mat-icon> Grouped
          </button>
          <button type="button" (click)="viewMode.set('list')"
            [class.bg-white]="viewMode() === 'list'"
            [class.dark:bg-gray-700]="viewMode() === 'list'"
            [class.text-emerald-700]="viewMode() === 'list'"
            [class.dark:text-emerald-400]="viewMode() === 'list'"
            [class.shadow-sm]="viewMode() === 'list'"
            matTooltip="Table List of All Entries"
            class="px-3 py-1 text-xs font-semibold rounded-lg text-gray-600 dark:text-gray-300 transition-all flex items-center gap-1.5">
            <mat-icon class="!w-4 !h-4 !text-[16px]">table_rows</mat-icon> List View
          </button>
        </div>
      </div>
    </div>

    <!-- Main Content Container -->
    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative min-h-[500px]">
      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Dashboard Header Stats -->
      <div class="p-6 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-900/30 grid grid-cols-2 md:grid-cols-4 gap-6">
        <div>
          <div class="text-xs uppercase font-bold text-gray-400 tracking-wider">Total Planned Feed</div>
          <div class="text-2xl md:text-3xl font-extrabold text-emerald-600 dark:text-emerald-400 mt-1">
            {{ totalExpectedKg() | number:'1.2-2' }} <span class="text-xs font-normal text-gray-500">kg</span>
          </div>
        </div>
        <div>
          <div class="text-xs uppercase font-bold text-gray-400 tracking-wider">Feed Consumed</div>
          <div class="text-2xl md:text-3xl font-extrabold text-blue-600 dark:text-blue-400 mt-1">
            {{ totalActualKg() | number:'1.2-2' }} <span class="text-xs font-normal text-gray-500">kg</span>
          </div>
        </div>
        <div>
          <div class="text-xs uppercase font-bold text-gray-400 tracking-wider">Total Feeding Entries</div>
          <div class="text-2xl md:text-3xl font-extrabold text-gray-900 dark:text-white mt-1">
            {{ filteredEntries().length }}
            <span class="text-xs font-normal text-gray-500" *ngIf="filteredEntries().length !== allEntries().length">
              (of {{ allEntries().length }})
            </span>
          </div>
        </div>
        <div>
          <div class="text-xs uppercase font-bold text-gray-400 tracking-wider flex items-center justify-between">
            <span>Workflow Progress</span>
            <span class="text-emerald-600 font-extrabold">{{ progressPct() | number:'1.0-0' }}%</span>
          </div>
          <div class="mt-2 w-full bg-gray-200 rounded-full h-2.5 dark:bg-gray-700 overflow-hidden">
            <div class="bg-gradient-to-r from-emerald-500 to-teal-500 h-2.5 rounded-full transition-all duration-500" [style.width]="progressPct() + '%'"></div>
          </div>
          <div class="text-xs font-medium text-gray-500 mt-1.5 flex items-center justify-between">
            <span>{{ countPending() }} pending</span>
            <span>Target Date: {{ selectedDate() }}</span>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!isLoading() && filteredEntries().length === 0"
        icon="restaurant_menu"
        [title]="allEntries().length === 0 ? 'No Feeding Entries for Selected Date' : 'No Matching Feeding Entries'"
        [description]="allEntries().length === 0 ? 'No daily feeding entries were found for ' + selectedDate() + '. Click \\'Generate Entries\\' to automatically create today\\'s feeding logs from active plans.' : 'No entries matched your current search or status filters.'"
        [actionLabel]="allEntries().length === 0 ? 'Generate Entries Now' : 'Reset Filters'"
        (action)="allEntries().length === 0 ? generateEntries() : resetFilters()">
      </app-empty-state>

      <!-- ══════════════════════════════════════════════════════════════════════════ -->
      <!-- VIEW MODE 1: GROUPED BY SHED & PEN                                      -->
      <!-- ══════════════════════════════════════════════════════════════════════════ -->
      <div *ngIf="!isLoading() && filteredEntries().length > 0 && viewMode() === 'grouped'" class="p-6 space-y-8">
        
        <div *ngFor="let shed of groupedEntries()" class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 overflow-hidden shadow-sm">
          
          <!-- Shed Header -->
          <div class="px-5 py-4 bg-gray-50/80 dark:bg-gray-900/60 border-b border-gray-200 dark:border-gray-700 flex justify-between items-center">
            <div class="flex items-center gap-3">
              <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20">
                <mat-icon class="!w-5 !h-5 !text-[20px]">home_work</mat-icon>
              </div>
              <div>
                <h2 class="text-base font-bold text-gray-900 dark:text-white leading-tight">{{ shed.shedName }}</h2>
                <div class="text-xs text-gray-500">{{ shed.pens.length }} pens • {{ getShedEntriesCount(shed) }} total entries</div>
              </div>
            </div>
            <div class="text-sm font-semibold text-gray-600 dark:text-gray-400">
              <span class="text-emerald-600 dark:text-emerald-400 font-bold">{{ shed.totalActualKg | number:'1.2-2' }}kg</span> / {{ shed.totalExpectedKg | number:'1.2-2' }}kg Confirmed
            </div>
          </div>

          <!-- Pens -->
          <div class="divide-y divide-gray-100 dark:divide-gray-800">
            <div *ngFor="let pen of shed.pens" class="p-5">
              
              <div class="flex justify-between items-center mb-4">
                <div class="flex items-center gap-2">
                  <mat-icon class="text-gray-400 !w-5 !h-5 !text-[20px]">fence</mat-icon>
                  <h3 class="text-sm font-bold text-gray-800 dark:text-gray-200">{{ pen.penName }}</h3>
                  <span class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300">
                    {{ pen.entries.length }} entries
                  </span>
                </div>
                
                <button (click)="confirmPen(pen)" *ngIf="hasPending(pen)"
                  class="px-3 py-1.5 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1">
                  <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">done_all</mat-icon> Confirm All ({{ getPendingPenCount(pen) }})
                </button>
              </div>

              <!-- Entries Grid -->
              <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                <div *ngFor="let entry of pen.entries" class="border border-gray-200 dark:border-gray-700 rounded-xl p-4 bg-gray-50/50 dark:bg-gray-900/50 relative overflow-hidden group hover:border-emerald-500/50 transition-colors">
                  
                  <!-- Status Ribbon Indicator -->
                  <div class="absolute top-0 right-0 w-16 h-16 pointer-events-none">
                    <div class="absolute transform rotate-45 text-[9px] font-bold text-white text-center w-24 py-0.5 right-[-26px] top-[14px]"
                         [ngClass]="{
                           'bg-amber-500 shadow-amber-500/50 shadow-sm': entry.status === 'Pending',
                           'bg-emerald-500 shadow-emerald-500/50 shadow-sm': entry.status === 'Confirmed',
                           'bg-blue-500 shadow-blue-500/50 shadow-sm': entry.status === 'Adjusted',
                           'bg-red-500 shadow-red-500/50 shadow-sm': entry.status === 'Skipped'
                         }">
                      {{ entry.status }}
                    </div>
                  </div>

                  <!-- Animal Tag Header -->
                  <div class="flex items-center gap-2 mb-1">
                    <div class="w-6 h-6 rounded-lg bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 flex items-center justify-center">
                      <mat-icon class="!w-3.5 !h-3.5 !text-[14px]">pets</mat-icon>
                    </div>
                    <span class="font-bold text-gray-900 dark:text-white text-sm">{{ entry.animalTag }}</span>
                  </div>
                  
                  <!-- Ration Formula Badge -->
                  <div class="text-xs text-gray-600 dark:text-gray-300 mt-1 mb-2 font-medium bg-gray-100 dark:bg-gray-800 rounded px-2 py-0.5 inline-block">
                    {{ entry.formulaName || 'Base Ration' }}
                  </div>

                  <!-- Quantity Metrics -->
                  <div class="text-2xl font-extrabold text-gray-900 dark:text-white mb-2">
                    {{ (entry.actualKg !== null && entry.actualKg !== undefined ? entry.actualKg : entry.expectedKg) | number:'1.2-2' }} <span class="text-xs font-normal text-gray-400">kg</span>
                    <span *ngIf="entry.status === 'Adjusted' && entry.actualKg !== entry.expectedKg" class="text-xs font-medium text-gray-400 line-through ml-1.5">
                      {{ entry.expectedKg | number:'1.2-2' }}kg
                    </span>
                  </div>

                  <!-- Actions for Pending -->
                  <div class="flex justify-between items-center mt-3 pt-3 border-t border-gray-200 dark:border-gray-700/50" *ngIf="entry.status === 'Pending'">
                    <button (click)="confirmEntry(entry)"
                      class="text-emerald-600 hover:bg-emerald-50 dark:hover:bg-emerald-950/40 px-2.5 py-1 rounded-lg font-semibold text-xs flex items-center transition-colors">
                      <mat-icon class="!w-4 !h-4 !text-[16px] mr-1">check</mat-icon> Confirm
                    </button>
                    
                    <button mat-icon-button [matMenuTriggerFor]="menu" class="!w-6 !h-6 flex items-center justify-center text-gray-400 hover:text-gray-600 dark:hover:text-gray-300">
                      <mat-icon class="!w-4 !h-4 !text-[18px]">more_vert</mat-icon>
                    </button>
                    <mat-menu #menu="matMenu" class="!rounded-xl shadow-xl">
                      <button mat-menu-item (click)="openAdjustDialog(entry, 'adjust')">
                        <mat-icon class="text-orange-500">tune</mat-icon>
                        <span>Adjust Amount</span>
                      </button>
                      <button mat-menu-item (click)="openAdjustDialog(entry, 'skip')">
                        <mat-icon class="text-red-500">block</mat-icon>
                        <span>Skip Feeding</span>
                      </button>
                    </mat-menu>
                  </div>
                  
                  <!-- Note / State for Processed -->
                  <div class="text-xs text-gray-500 italic mt-3 pt-3 border-t border-gray-200 dark:border-gray-700/50" *ngIf="entry.status !== 'Pending'">
                    <span *ngIf="entry.status === 'Confirmed'">✓ Confirmed</span>
                    <span *ngIf="entry.status === 'Adjusted'">⚖ Adjusted: {{ entry.notes || 'Amount modified' }}</span>
                    <span *ngIf="entry.status === 'Skipped'">✕ Skipped: {{ entry.notes || 'Feed omitted' }}</span>
                  </div>

                </div>
              </div>

            </div>
          </div>
        </div>

      </div>

      <!-- ══════════════════════════════════════════════════════════════════════════ -->
      <!-- VIEW MODE 2: FLAT LIST / TABLE VIEW                                     -->
      <!-- ══════════════════════════════════════════════════════════════════════════ -->
      <div *ngIf="!isLoading() && filteredEntries().length > 0 && viewMode() === 'list'" class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr class="bg-gray-50/80 dark:bg-gray-900/50 border-b border-gray-100 dark:border-gray-800 text-[11px] uppercase tracking-wider font-bold text-gray-400">
              <th class="py-3.5 px-4">Animal Tag</th>
              <th class="py-3.5 px-4">Location (Shed / Pen)</th>
              <th class="py-3.5 px-4">Ration Formula</th>
              <th class="py-3.5 px-4">Planned (kg)</th>
              <th class="py-3.5 px-4">Consumed (kg)</th>
              <th class="py-3.5 px-4">Status</th>
              <th class="py-3.5 px-4">Notes / Reason</th>
              <th class="py-3.5 px-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100 dark:divide-gray-800 text-sm">
            <tr *ngFor="let entry of filteredEntries()" class="hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors">
              <!-- Animal Tag -->
              <td class="py-3.5 px-4">
                <div class="flex items-center gap-2">
                  <div class="w-7 h-7 rounded-lg bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 flex items-center justify-center">
                    <mat-icon class="!w-4 !h-4 !text-[16px]">pets</mat-icon>
                  </div>
                  <span class="font-bold text-gray-900 dark:text-white">{{ entry.animalTag }}</span>
                </div>
              </td>

              <!-- Location -->
              <td class="py-3.5 px-4 text-gray-600 dark:text-gray-300 font-medium">
                {{ entry.shedName || 'Unassigned Shed' }} / {{ entry.penName || 'Unassigned Pen' }}
              </td>

              <!-- Ration Formula -->
              <td class="py-3.5 px-4 font-semibold text-emerald-700 dark:text-emerald-400">
                {{ entry.formulaName || 'Base Ration' }}
              </td>

              <!-- Planned (kg) -->
              <td class="py-3.5 px-4 font-semibold text-gray-800 dark:text-gray-200">
                {{ entry.expectedKg | number:'1.2-2' }} kg
              </td>

              <!-- Actual (kg) -->
              <td class="py-3.5 px-4 font-bold"
                [class.text-gray-400]="entry.actualKg === null || entry.actualKg === undefined"
                [class.text-emerald-600]="entry.actualKg !== null && entry.actualKg !== undefined">
                {{ (entry.actualKg !== null && entry.actualKg !== undefined ? entry.actualKg : '-') }} {{ entry.actualKg !== null && entry.actualKg !== undefined ? 'kg' : '' }}
              </td>

              <!-- Status Badge -->
              <td class="py-3.5 px-4">
                <span class="px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider inline-flex items-center gap-1"
                  [ngClass]="{
                    'bg-amber-50 text-amber-700 dark:bg-amber-950/60 dark:text-amber-400 border border-amber-200 dark:border-amber-800': entry.status === 'Pending',
                    'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800': entry.status === 'Confirmed',
                    'bg-blue-50 text-blue-700 dark:bg-blue-950/60 dark:text-blue-400 border border-blue-200 dark:border-blue-800': entry.status === 'Adjusted',
                    'bg-rose-50 text-rose-700 dark:bg-rose-950/60 dark:text-rose-400 border border-rose-200 dark:border-rose-800': entry.status === 'Skipped'
                  }">
                  {{ entry.status }}
                </span>
              </td>

              <!-- Notes -->
              <td class="py-3.5 px-4 text-xs text-gray-500 max-w-xs truncate">
                {{ entry.notes || '-' }}
              </td>

              <!-- Actions -->
              <td class="py-3.5 px-4 text-right">
                <div class="flex items-center justify-end gap-1.5" *ngIf="entry.status === 'Pending'">
                  <button (click)="confirmEntry(entry)" matTooltip="Confirm Planned Amount"
                    class="p-1.5 text-xs font-semibold text-emerald-700 hover:bg-emerald-50 dark:hover:bg-emerald-950/40 rounded-lg transition-colors">
                    <mat-icon class="!w-4 !h-4 !text-[16px]">check</mat-icon>
                  </button>
                  <button (click)="openAdjustDialog(entry, 'adjust')" matTooltip="Adjust Amount"
                    class="p-1.5 text-xs font-semibold text-orange-600 hover:bg-orange-50 dark:hover:bg-orange-950/40 rounded-lg transition-colors">
                    <mat-icon class="!w-4 !h-4 !text-[16px]">tune</mat-icon>
                  </button>
                  <button (click)="openAdjustDialog(entry, 'skip')" matTooltip="Skip Entry"
                    class="p-1.5 text-xs font-semibold text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40 rounded-lg transition-colors">
                    <mat-icon class="!w-4 !h-4 !text-[16px]">block</mat-icon>
                  </button>
                </div>
                <div *ngIf="entry.status !== 'Pending'" class="text-xs text-gray-400 italic">
                  Processed
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TodayFeedingDashboardComponent implements OnInit {
  private readonly feedingService = inject(FeedingService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly contextService = inject(WorkingContextService);
  private readonly destroyRef = inject(DestroyRef);

  // Signals
  readonly isLoading = signal(true);
  readonly isGenerating = signal(false);
  readonly allEntries = signal<DailyFeedingEntry[]>([]);
  readonly selectedDate = signal<string>(new Date().toISOString().split('T')[0]);
  readonly searchTerm = signal<string>('');
  readonly statusFilter = signal<string>('all');
  readonly viewMode = signal<'grouped' | 'list'>('grouped');

  private currentFarmId: string | null = null;

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

  // KPI Computations
  readonly totalExpectedKg = computed(() => {
    return this.filteredEntries().reduce((sum, e) => sum + e.expectedKg, 0);
  });

  readonly totalActualKg = computed(() => {
    return this.filteredEntries().reduce((sum, e) => sum + (e.actualKg ?? 0), 0);
  });

  readonly progressPct = computed(() => {
    const entries = this.filteredEntries();
    if (entries.length === 0) return 0;
    const completed = entries.filter(e => e.status !== 'Pending').length;
    return (completed / entries.length) * 100;
  });

  readonly countPending = computed(() => {
    return this.allEntries().filter(e => e.status === 'Pending').length;
  });

  readonly isToday = computed(() => {
    const today = new Date().toISOString().split('T')[0];
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
    const farmId = this.currentFarmId || undefined;
    const date = this.selectedDate();

    this.feedingService.getTodayEntries(farmId, date).subscribe({
      next: (res) => {
        this.allEntries.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  generateEntries(): void {
    this.isGenerating.set(true);
    this.feedingService.generateEntries().subscribe({
      next: (res) => {
        this.snackBar.open(res.message || 'Daily entries generated successfully.', 'Close', { duration: 3000 });
        this.isGenerating.set(false);
        this.loadEntries();
      },
      error: (err) => {
        this.snackBar.open(err.error?.detail || 'Failed to generate entries.', 'Close', { duration: 5000 });
        this.isGenerating.set(false);
      }
    });
  }

  confirmEntry(entry: DailyFeedingEntry): void {
    this.feedingService.confirmEntry(entry.id, entry.expectedKg).subscribe({
      next: () => {
        this.snackBar.open(`Confirmed feed for ${entry.animalTag}`, 'Close', { duration: 3000 });
        this.loadEntries();
      },
      error: (err) => {
        this.snackBar.open(err.error?.detail || 'Failed to confirm entry', 'Close', { duration: 5000 });
      }
    });
  }

  openAdjustDialog(entry: DailyFeedingEntry, action: 'adjust' | 'skip'): void {
    const dialogRef = this.dialog.open(AdjustFeedingEntryDialogComponent, {
      disableClose: true,
      width: '500px',
      data: { entry, action }
    });

    dialogRef.afterClosed().subscribe(res => {
      if (res) this.loadEntries();
    });
  }

  confirmPen(pen: PenGroup): void {
    const pendingEntries = pen.entries.filter(e => e.status === 'Pending');

    if (pendingEntries.length === 0) {
      this.snackBar.open(`All entries in ${pen.penName} are already processed.`, 'Close', { duration: 3000 });
      return;
    }

    const observables = pendingEntries.map(e => this.feedingService.confirmEntry(e.id, e.expectedKg));

    this.isLoading.set(true);
    forkJoin(observables).subscribe({
      next: () => {
        this.snackBar.open(`Confirmed ${pendingEntries.length} entries for ${pen.penName}`, 'Close', { duration: 3000 });
        this.loadEntries();
      },
      error: () => {
        this.snackBar.open('Some confirmations failed', 'Close', { duration: 5000 });
        this.loadEntries();
      }
    });
  }

  // Date stepper methods
  previousDay(): void {
    const current = new Date(this.selectedDate());
    current.setDate(current.getDate() - 1);
    this.selectedDate.set(current.toISOString().split('T')[0]);
    this.loadEntries();
  }

  nextDay(): void {
    const current = new Date(this.selectedDate());
    current.setDate(current.getDate() + 1);
    this.selectedDate.set(current.toISOString().split('T')[0]);
    this.loadEntries();
  }

  setToday(): void {
    const today = new Date().toISOString().split('T')[0];
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
}
