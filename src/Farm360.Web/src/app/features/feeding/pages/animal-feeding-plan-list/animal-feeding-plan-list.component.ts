import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { forkJoin, of, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { FeedingService } from '../../services/feeding.service';
import { AnimalFeedingPlan, FeedingRuleSet, FeedingPlanType, TargetAnimalType, FeedingPurpose } from '../../models/feeding.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { AssignFeedingPlanDialogComponent } from '../../components/dialogs/assign-feeding-plan-dialog/assign-feeding-plan-dialog.component';
import { FeedingRuleSetDialogComponent } from '../../components/dialogs/feeding-rule-set-dialog/feeding-rule-set-dialog.component';
import { AnimalFeedsCostReportDialogComponent } from '../../components/dialogs/animal-feeds-cost-report-dialog/animal-feeds-cost-report-dialog.component';
import { FeedingReportPdfService } from '../../services/feeding-report-pdf.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';

export interface RuleSetGroup {
  ruleSet: FeedingRuleSet;
  plans: AnimalFeedingPlan[];
  activePlansCount: number;
  totalDailyFeedKg: number;
  isUnknown?: boolean;
}

@Component({
  selector: 'app-animal-feeding-plan-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatMenuModule,
    MatTooltipModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  styles: [`
    .custom-scrollbar::-webkit-scrollbar { width: 6px; }
    .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
    .custom-scrollbar::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.4); border-radius: 20px; }
    .custom-scrollbar:hover::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.7); }
  `],
  template: `
    <app-page-header
      title="Animal Feeding Plans"
      description="View and manage animal feeding allocations grouped by rule sets, edit formulas, and enroll cattle."
      breadcrumbActiveNode="Feeding Plans">
      <div actions class="flex items-center gap-2 flex-wrap xl:flex-nowrap shrink-0">
        <!-- View Mode Switcher -->
        <div class="inline-flex items-center h-9 rounded-xl bg-gray-100 dark:bg-gray-800 p-1 border border-gray-200/80 dark:border-gray-700/80 shrink-0">
          <button type="button" (click)="viewMode.set('grouped')"
            [class.bg-white]="viewMode() === 'grouped'"
            [class.dark:bg-gray-700]="viewMode() === 'grouped'"
            [class.text-emerald-700]="viewMode() === 'grouped'"
            [class.dark:text-emerald-400]="viewMode() === 'grouped'"
            [class.shadow-xs]="viewMode() === 'grouped'"
            class="h-7 px-2.5 text-xs font-semibold rounded-lg text-gray-600 dark:text-gray-300 transition-all inline-flex items-center gap-1.5 whitespace-nowrap shrink-0">
            <mat-icon class="!w-3.5 !h-3.5 !text-[15px]">view_agenda</mat-icon>
            <span>Grouped</span>
          </button>
          <button type="button" (click)="viewMode.set('table')"
            [class.bg-white]="viewMode() === 'table'"
            [class.dark:bg-gray-700]="viewMode() === 'table'"
            [class.text-emerald-700]="viewMode() === 'table'"
            [class.dark:text-emerald-400]="viewMode() === 'table'"
            [class.shadow-xs]="viewMode() === 'table'"
            class="h-7 px-2.5 text-xs font-semibold rounded-lg text-gray-600 dark:text-gray-300 transition-all inline-flex items-center gap-1.5 whitespace-nowrap shrink-0">
            <mat-icon class="!w-3.5 !h-3.5 !text-[15px]">table_rows</mat-icon>
            <span>Table</span>
          </button>
        </div>

        <!-- Reports Dropdown Menu (SAP & Modal) -->
        <button [matMenuTriggerFor]="reportsMenu"
          class="h-9 px-3 text-xs font-semibold text-emerald-800 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 hover:bg-emerald-100 dark:hover:bg-emerald-900/60 border border-emerald-200/80 dark:border-emerald-800/80 rounded-xl transition-all shadow-xs inline-flex items-center gap-1.5 whitespace-nowrap shrink-0">
          <mat-icon class="!text-[16px] !w-[16px] !h-[16px] text-emerald-600 dark:text-emerald-400">assessment</mat-icon>
          <span>Reports</span>
          <mat-icon class="!text-[14px] !w-[14px] !h-[14px] text-emerald-600/70 -mr-0.5">expand_more</mat-icon>
        </button>

        <mat-menu #reportsMenu="matMenu" class="!rounded-2xl !p-1.5 shadow-xl border border-gray-100 dark:border-gray-800">
          <a mat-menu-item routerLink="/reports/feeding.daily-workflow" class="!rounded-xl">
            <mat-icon class="text-teal-600">fact_check</mat-icon>
            <div class="flex flex-col text-left py-0.5">
              <span class="font-semibold text-xs text-gray-800 dark:text-gray-200">Daily Feeding & Workflow (SAP)</span>
              <span class="text-[11px] text-gray-400">Animal-wise daily history, DM, Protein, Cost & Status</span>
            </div>
          </a>
          <a mat-menu-item routerLink="/reports/feeding.plans-cost-projection" class="!rounded-xl">
            <mat-icon class="text-emerald-600">assessment</mat-icon>
            <div class="flex flex-col text-left py-0.5">
              <span class="font-semibold text-xs text-gray-800 dark:text-gray-200">Plans & Cost Projections (SAP)</span>
              <span class="text-[11px] text-gray-400">Ration allocations and 30-day cost forecasts</span>
            </div>
          </a>
          <button mat-menu-item (click)="openFeedsCostReportDialog()" class="!rounded-xl">
            <mat-icon class="text-blue-600">picture_as_pdf</mat-icon>
            <div class="flex flex-col text-left py-0.5">
              <span class="font-semibold text-xs text-gray-800 dark:text-gray-200">Quick Feeds Modal</span>
              <span class="text-[11px] text-gray-400">Printable modal overview dialog</span>
            </div>
          </button>
        </mat-menu>

        <!-- New Rule Set Button -->
        <button (click)="openCreateRuleSetDialog()"
          class="h-9 px-3 text-xs font-semibold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 border border-gray-200 dark:border-gray-700 rounded-xl transition-colors shadow-xs inline-flex items-center gap-1.5 whitespace-nowrap shrink-0">
          <mat-icon class="!text-[16px] !w-[16px] !h-[16px] text-gray-500">tune</mat-icon>
          <span>New Rule Set</span>
        </button>

        <!-- Sync Weights Button -->
        <button (click)="syncPlanWeights()"
          [disabled]="isSyncing()"
          matTooltip="Synchronize all active plans with animals' latest recorded weights"
          class="h-9 px-3 text-xs font-semibold text-teal-800 dark:text-teal-300 bg-teal-50 dark:bg-teal-950/40 hover:bg-teal-100 dark:hover:bg-teal-900/60 border border-teal-200 dark:border-teal-800 rounded-xl transition-all shadow-xs inline-flex items-center gap-1.5 whitespace-nowrap shrink-0 disabled:opacity-50">
          <mat-icon [class.animate-spin]="isSyncing()" class="!text-[16px] !w-[16px] !h-[16px] text-teal-600 dark:text-teal-400">sync</mat-icon>
          <span>{{ isSyncing() ? 'Syncing...' : 'Sync Weights' }}</span>
        </button>

        <!-- Assign Plan Primary Button -->
        <button (click)="openAssignDialog()"
          class="h-9 px-3.5 text-xs font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-xl transition-all shadow-sm shadow-emerald-500/20 inline-flex items-center gap-1.5 whitespace-nowrap shrink-0">
          <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">add</mat-icon>
          <span>Assign Plan</span>
        </button>
      </div>
    </app-page-header>

    <!-- Global Toolbar / Metrics -->
    <div class="mb-6 flex flex-col md:flex-row items-stretch md:items-center justify-between gap-4">
      <!-- Search Input -->
      <div class="relative flex-1 max-w-md">
        <mat-icon class="absolute left-3.5 top-1/2 -translate-y-1/2 !text-[18px] !w-[18px] !h-[18px] text-gray-400">search</mat-icon>
        <input
          [ngModel]="searchTerm()"
          (ngModelChange)="searchTerm.set($event)"
          placeholder="Filter by animal tag, rule set name, or species..."
          class="w-full pl-10 pr-4 py-2 text-sm bg-white/80 dark:bg-gray-800/80 backdrop-blur-md rounded-xl border border-gray-200 dark:border-gray-700 text-gray-900 dark:text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all shadow-sm" />
      </div>

      <!-- Quick Stats -->
      <div class="flex items-center gap-3 text-xs">
        <div class="px-3 py-1.5 rounded-xl bg-white/80 dark:bg-gray-800/80 border border-gray-100 dark:border-gray-800 text-gray-600 dark:text-gray-300 font-medium shadow-sm flex items-center gap-1.5">
          <span class="w-2 h-2 rounded-full bg-emerald-500"></span>
          <span><strong>{{ totalActivePlans() }}</strong> Active Enrolled</span>
        </div>
        <div class="px-3 py-1.5 rounded-xl bg-white/80 dark:bg-gray-800/80 border border-gray-100 dark:border-gray-800 text-gray-600 dark:text-gray-300 font-medium shadow-sm flex items-center gap-1.5">
          <span class="w-2 h-2 rounded-full bg-teal-500"></span>
          <span><strong>{{ ruleSetGroups().length }}</strong> Configured Rules</span>
        </div>
        <button *ngIf="viewMode() === 'grouped'" (click)="toggleAllGroups()"
          class="px-3 py-1.5 text-xs text-gray-600 dark:text-gray-400 hover:text-emerald-600 dark:hover:text-emerald-400 font-medium transition-colors">
          {{ areAllCollapsed() ? 'Expand All' : 'Collapse All' }}
        </button>
      </div>
    </div>

    <!-- Main Container -->
    <div class="relative min-h-[300px]">
      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!isLoading() && plans().length === 0 && ruleSets().length === 0"
        icon="assignment"
        title="No Feeding Plans or Rules Found"
        description="Get started by configuring a feeding rule set or assigning animals."
        actionLabel="Assign Plan"
        (action)="openAssignDialog()">
      </app-empty-state>

      <!-- =================================================================== -->
      <!-- VIEW MODE 1: GROUPED BY RULE SET                                    -->
      <!-- =================================================================== -->
      <div *ngIf="!isLoading() && viewMode() === 'grouped'" class="space-y-6">
        @for (group of ruleSetGroups(); track group.ruleSet.id) {
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/60 overflow-hidden transition-all duration-200">
            
            <!-- Group Header -->
            <div class="px-6 py-4 bg-gradient-to-r from-gray-50/90 via-white to-gray-50/50 dark:from-gray-800/90 dark:via-gray-800 dark:to-gray-900/60 border-b border-gray-100 dark:border-gray-700/80 flex flex-col md:flex-row md:items-center justify-between gap-4">
              
              <!-- Left: Icon, Rule Set Name & Meta Badges -->
              <div class="flex items-start md:items-center gap-3.5">
                <div class="w-11 h-11 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20 shrink-0">
                  <mat-icon class="!text-[22px] !w-[22px] !h-[22px]">tune</mat-icon>
                </div>
                <div>
                  <div class="flex flex-wrap items-center gap-2">
                    <h3 class="text-base font-bold text-gray-900 dark:text-white m-0 leading-tight">
                      {{ group.ruleSet.name }}
                    </h3>
                    <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider shadow-sm"
                      [ngClass]="group.ruleSet.isActive ? 'bg-emerald-50 text-emerald-700 border border-emerald-200 dark:bg-emerald-950/40 dark:text-emerald-300 dark:border-emerald-800' : 'bg-gray-100 text-gray-600 border border-gray-200 dark:bg-gray-800 dark:text-gray-400'">
                      {{ group.ruleSet.isActive ? 'Active' : 'Inactive' }}
                    </span>
                    <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium bg-indigo-50 dark:bg-indigo-950/40 text-indigo-700 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800/60">
                      {{ formatPlanType(group.ruleSet.planType) }}
                    </span>
                    <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium bg-amber-50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-300 border border-amber-200 dark:border-amber-800/60">
                      {{ formatAnimalType(group.ruleSet.targetAnimalType) }}
                    </span>
                    <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium bg-sky-50 dark:bg-sky-950/40 text-sky-700 dark:text-sky-300 border border-sky-200 dark:border-sky-800/60">
                      {{ formatPurpose(group.ruleSet.feedingPurpose) }}
                    </span>
                  </div>

                  <p class="text-xs text-gray-500 dark:text-gray-400 mt-1 mb-0 flex items-center gap-3">
                    <span><strong>{{ group.plans.length }}</strong> animals assigned</span>
                    <span class="text-gray-300 dark:text-gray-600">•</span>
                    <span>Expected: <strong class="text-emerald-600 dark:text-emerald-400 font-bold">{{ group.totalDailyFeedKg | number:'1.2-2' }} kg/day</strong></span>
                    <span *ngIf="group.ruleSet.baseNotes" class="text-gray-300 dark:text-gray-600">•</span>
                    <span *ngIf="group.ruleSet.baseNotes" class="italic">{{ group.ruleSet.baseNotes }}</span>
                  </p>
                </div>
              </div>

              <!-- Right: Group Action Buttons -->
              <div class="flex items-center gap-2 self-end md:self-center shrink-0">
                <!-- Edit Rule Set Button -->
                <button (click)="openEditRuleDialog(group.ruleSet, $event)"
                  matTooltip="Edit rule set formulas, weight stages, and feed rations"
                  class="px-3 py-1.5 text-xs font-semibold text-gray-700 dark:text-gray-300 hover:text-emerald-700 dark:hover:text-emerald-300 bg-white dark:bg-gray-800 hover:bg-emerald-50 dark:hover:bg-emerald-950/30 rounded-xl border border-gray-200 dark:border-gray-700 transition-all shadow-sm inline-flex items-center gap-1.5">
                  <mat-icon class="!text-[15px] !w-[15px] !h-[15px] text-emerald-600 dark:text-emerald-400">edit_note</mat-icon> Edit Rule
                </button>

                <!-- Assign Animals Button -->
                <button (click)="openAssignDialog(group.ruleSet.id, $event)"
                  matTooltip="Assign more cattle to this specific rule set"
                  class="px-3 py-1.5 text-xs font-semibold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 hover:bg-emerald-100 dark:hover:bg-emerald-900/60 rounded-xl border border-emerald-200 dark:border-emerald-800/60 transition-all shadow-sm inline-flex items-center gap-1.5">
                  <mat-icon class="!text-[15px] !w-[15px] !h-[15px]">person_add</mat-icon> Assign Animals
                </button>

                <!-- Expand/Collapse Chevron -->
                <button (click)="toggleGroup(group.ruleSet.id)"
                  class="p-1.5 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
                  <mat-icon class="!text-[20px] !w-[20px] !h-[20px] transition-transform duration-200"
                    [class.rotate-180]="!isCollapsed(group.ruleSet.id)">expand_more</mat-icon>
                </button>
              </div>
            </div>

            <!-- Configured Rule Tiers Quick Preview (Chips) -->
            <div *ngIf="group.ruleSet.rules && group.ruleSet.rules.length > 0"
              class="px-6 py-2.5 bg-gray-50/40 dark:bg-gray-900/30 border-b border-gray-100 dark:border-gray-800 flex flex-wrap items-center gap-2 text-xs">
              <span class="text-gray-400 dark:text-gray-500 font-semibold uppercase tracking-wider text-[10px]">Rule Tiers:</span>
              @for (rule of group.ruleSet.rules; track rule.id) {
                <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-300 border border-gray-200 dark:border-gray-700 shadow-xs text-[11px]">
                  <span class="text-gray-500 dark:text-gray-400">{{ formatRuleCondition(group.ruleSet.planType, rule) }}</span>
                  <span class="text-gray-300 dark:text-gray-600">→</span>
                  <strong class="text-emerald-600 dark:text-emerald-400">{{ rule.quantityValue }}{{ group.ruleSet.planType === 'WeightPercentage' ? '%' : ' kg' }}</strong>
                  <span class="text-gray-500 dark:text-gray-400 font-medium">({{ rule.feedType }})</span>
                </span>
              }
            </div>

            <!-- Group Animals Table (Collapsible) -->
            <div *ngIf="!isCollapsed(group.ruleSet.id)" class="overflow-x-auto">
              
              <!-- If group has animals -->
              <table *ngIf="group.plans.length > 0" class="w-full text-left border-collapse">
                <thead>
                  <tr class="bg-gray-50/50 dark:bg-gray-900/40 text-gray-500 dark:text-gray-400 text-[11px] uppercase tracking-wider font-bold border-b border-gray-100 dark:border-gray-800">
                    <th class="px-6 py-3">Animal ID / Tag</th>
                    <th class="px-6 py-3">Status</th>
                    <th class="px-6 py-3 text-right">Expected Daily Feed</th>
                    <th class="px-6 py-3 text-center">Assigned On</th>
                    <th class="px-6 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100 dark:divide-gray-800 text-sm">
                  @for (plan of group.plans; track plan.id) {
                    <tr class="hover:bg-gray-50/40 dark:hover:bg-gray-800/40 transition-colors group">
                      <td class="px-6 py-3.5 font-bold text-gray-900 dark:text-white">
                        <div class="flex items-center gap-2.5">
                          <div class="w-8 h-8 rounded-lg bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                            <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">pets</mat-icon>
                          </div>
                          <div>
                            <span class="font-bold">{{ plan.animalTag }}</span>
                            <span *ngIf="!plan.isActive" class="text-xs text-gray-400 block font-normal">Plan canceled</span>
                          </div>
                        </div>
                      </td>
                      <td class="px-6 py-3.5">
                        <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider shadow-xs"
                          [ngClass]="plan.isActive ? 'bg-emerald-50 text-emerald-700 border border-emerald-200 dark:bg-emerald-950/40 dark:text-emerald-300 dark:border-emerald-800' : 'bg-red-50 text-red-700 border border-red-200 dark:bg-red-950/40 dark:text-red-300 dark:border-red-800'">
                          {{ plan.isActive ? 'Active' : 'Canceled' }}
                        </span>
                      </td>
                      <td class="px-6 py-3.5 text-right font-extrabold text-emerald-600 dark:text-emerald-400">
                        {{ plan.expectedDailyFeedKg | number:'1.2-2' }} kg
                      </td>
                      <td class="px-6 py-3.5 text-center text-gray-500 dark:text-gray-400 text-xs">
                        {{ plan.assignedOn | date:'mediumDate' }}
                      </td>
                      <td class="px-6 py-3.5 text-right">
                        <button *ngIf="plan.isActive" (click)="cancelPlan(plan)"
                          class="px-2.5 py-1 text-xs font-semibold text-red-700 hover:text-white bg-red-50 hover:bg-red-600 rounded-lg border border-red-200 transition-all shadow-xs inline-flex items-center gap-1">
                          <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">cancel</mat-icon> Cancel
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>

              <!-- If group has NO animals assigned yet -->
              <div *ngIf="group.plans.length === 0" class="p-8 text-center bg-gray-50/20 dark:bg-gray-900/20">
                <mat-icon class="!text-[32px] !w-[32px] !h-[32px] text-gray-300 dark:text-gray-600 mb-2">assignment_late</mat-icon>
                <p class="text-sm font-medium text-gray-600 dark:text-gray-400 mb-2">
                  No animals are currently enrolled in this rule set.
                </p>
                <button (click)="openAssignDialog(group.ruleSet.id)"
                  class="px-3.5 py-1.5 text-xs font-semibold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 hover:bg-emerald-100 rounded-xl border border-emerald-200 dark:border-emerald-800 transition-all inline-flex items-center gap-1">
                  <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">person_add</mat-icon> Assign Animals Now
                </button>
              </div>

            </div>
          </div>
        }
      </div>

      <!-- =================================================================== -->
      <!-- VIEW MODE 2: FLAT TABLE                                             -->
      <!-- =================================================================== -->
      <div *ngIf="!isLoading() && viewMode() === 'table' && filteredPlans().length > 0"
        class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden">
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="bg-gray-50/80 dark:bg-gray-900/50 text-gray-500 dark:text-gray-400 text-[11px] uppercase tracking-wider font-bold border-b border-gray-200 dark:border-gray-700">
                <th class="px-6 py-4">Animal Tag</th>
                <th class="px-6 py-4">Rule Set</th>
                <th class="px-6 py-4">Status</th>
                <th class="px-6 py-4 text-right">Expected Daily Feed</th>
                <th class="px-6 py-4 text-center">Assigned On</th>
                <th class="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100 dark:divide-gray-800 text-sm">
              @for (plan of filteredPlans(); track plan.id) {
                <tr class="hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors group">
                  <td class="px-6 py-4 font-bold text-gray-900 dark:text-white">
                    <div class="flex items-center gap-2">
                      <div class="w-8 h-8 rounded bg-emerald-50 dark:bg-emerald-900/30 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                        <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">pets</mat-icon>
                      </div>
                      {{ plan.animalTag }}
                    </div>
                  </td>
                  <td class="px-6 py-4">
                    <div class="font-medium text-gray-900 dark:text-gray-100 flex items-center gap-2">
                      <span>{{ plan.ruleSetName }}</span>
                      <button *ngIf="getRuleSetForPlan(plan)" (click)="openEditRuleDialog(getRuleSetForPlan(plan)!, $event)"
                        matTooltip="Edit this rule set"
                        class="text-gray-400 hover:text-emerald-600 transition-colors">
                        <mat-icon class="!text-[15px] !w-[15px] !h-[15px]">edit_note</mat-icon>
                      </button>
                    </div>
                  </td>
                  <td class="px-6 py-4">
                    <span class="inline-flex items-center px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider shadow-sm"
                      [ngClass]="plan.isActive ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' : 'bg-red-50 text-red-700 border border-red-200'">
                      {{ plan.isActive ? 'Active' : 'Canceled' }}
                    </span>
                  </td>
                  <td class="px-6 py-4 text-right font-extrabold text-emerald-600 dark:text-emerald-400">
                    {{ plan.expectedDailyFeedKg | number:'1.2-2' }} kg
                  </td>
                  <td class="px-6 py-4 text-center text-gray-500 dark:text-gray-400">
                    {{ plan.assignedOn | date:'mediumDate' }}
                  </td>
                  <td class="px-6 py-4 text-right">
                    <button *ngIf="plan.isActive" (click)="cancelPlan(plan)"
                      class="px-3 py-1.5 text-xs font-semibold text-red-700 hover:text-white bg-red-50 hover:bg-red-600 rounded-lg border border-red-200 transition-all shadow-sm inline-flex items-center gap-1">
                      <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">cancel</mat-icon> Cancel
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnimalFeedingPlanListComponent implements OnInit {
  private readonly feedingService = inject(FeedingService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly contextService = inject(WorkingContextService);
  private readonly destroyRef = inject(DestroyRef);

  readonly isLoading = signal(true);
  readonly isSyncing = signal(false);
  readonly plans = signal<AnimalFeedingPlan[]>([]);
  readonly ruleSets = signal<FeedingRuleSet[]>([]);
  readonly searchTerm = signal<string>('');
  readonly viewMode = signal<'grouped' | 'table'>('grouped');
  readonly collapsedGroupIds = signal<Set<string>>(new Set());

  readonly totalActivePlans = computed(() => {
    return this.plans().filter(p => p.isActive).length;
  });

  readonly filteredPlans = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const list = this.plans();
    if (!term) return list;
    return list.filter(p =>
      p.animalTag.toLowerCase().includes(term) ||
      p.ruleSetName.toLowerCase().includes(term)
    );
  });

  readonly ruleSetGroups = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const allRuleSets = this.ruleSets();
    const allPlans = this.plans();

    // Map rule sets to groups
    const groups: RuleSetGroup[] = allRuleSets.map(ruleSet => {
      let matchingPlans = allPlans.filter(p => p.ruleSetId === ruleSet.id);
      if (term) {
        matchingPlans = matchingPlans.filter(p =>
          p.animalTag.toLowerCase().includes(term) ||
          ruleSet.name.toLowerCase().includes(term) ||
          ruleSet.targetAnimalType.toLowerCase().includes(term) ||
          ruleSet.feedingPurpose.toLowerCase().includes(term)
        );
      }

      const activeCount = matchingPlans.filter(p => p.isActive).length;
      const totalFeed = matchingPlans
        .filter(p => p.isActive)
        .reduce((sum, p) => sum + (p.expectedDailyFeedKg || 0), 0);

      return {
        ruleSet,
        plans: matchingPlans,
        activePlansCount: activeCount,
        totalDailyFeedKg: totalFeed
      };
    });

    // Check for orphaned plans (plans with ruleSetId not in allRuleSets)
    const ruleSetIdMap = new Set(allRuleSets.map(r => r.id));
    const orphanPlans = allPlans.filter(p => !ruleSetIdMap.has(p.ruleSetId));
    if (orphanPlans.length > 0) {
      let filteredOrphans = orphanPlans;
      if (term) {
        filteredOrphans = orphanPlans.filter(p =>
          p.animalTag.toLowerCase().includes(term) ||
          p.ruleSetName.toLowerCase().includes(term)
        );
      }
      if (filteredOrphans.length > 0 || !term) {
        groups.push({
          ruleSet: {
            id: 'unassigned-ruleset',
            name: orphanPlans[0]?.ruleSetName || 'Other Feeding Plans',
            planType: FeedingPlanType.FixedQuantity,
            targetAnimalType: TargetAnimalType.Cattle,
            feedingPurpose: FeedingPurpose.Maintenance,
            isActive: true,
            rules: []
          },
          plans: filteredOrphans,
          activePlansCount: filteredOrphans.filter(p => p.isActive).length,
          totalDailyFeedKg: filteredOrphans
            .filter(p => p.isActive)
            .reduce((sum, p) => sum + (p.expectedDailyFeedKg || 0), 0),
          isUnknown: true
        });
      }
    }

    // If searching, filter out groups with no matching plans and non-matching names
    if (term) {
      return groups.filter(g =>
        g.plans.length > 0 ||
        g.ruleSet.name.toLowerCase().includes(term)
      );
    }

    // Sort: groups with assigned plans first, then alphabetical
    return groups.sort((a, b) => {
      if (b.plans.length !== a.plans.length) {
        return b.plans.length - a.plans.length;
      }
      return a.ruleSet.name.localeCompare(b.ruleSet.name);
    });
  });

  ngOnInit(): void {
    this.contextService.currentFarm$
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        switchMap(farm => {
          if (!farm) {
            this.plans.set([]);
            this.ruleSets.set([]);
            this.isLoading.set(false);
            return of({ plans: [], ruleSets: [] });
          }
          this.isLoading.set(true);
          return forkJoin({
            plans: this.feedingService.getFeedingPlans(farm.id),
            ruleSets: this.feedingService.getRuleSets()
          });
        })
      )
      .subscribe({
        next: (res) => {
          this.plans.set(res.plans);
          this.ruleSets.set(res.ruleSets);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false)
      });
  }

  syncPlanWeights(): void {
    const farmId = this.contextService.currentFarmValue?.id;
    this.isSyncing.set(true);
    this.feedingService.syncPlanWeights(farmId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.isSyncing.set(false);
          this.snackBar.open(
            `Synchronized ${res.updatedPlans} of ${res.totalActivePlans} feeding plans (${res.updatedEntriesCount} pending daily entries updated)`,
            'Close',
            { duration: 4000 }
          );
          this.loadData();
        },
        error: (err) => {
          this.isSyncing.set(false);
          this.snackBar.open(
            err.error?.message || 'Failed to sync feeding plan weights',
            'Close',
            { duration: 4000 }
          );
        }
      });
  }

  loadData(farmId?: string): void {
    farmId = farmId || this.contextService.currentFarmValue?.id;
    if (!farmId) {
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    forkJoin({
      plans: this.feedingService.getFeedingPlans(farmId),
      ruleSets: this.feedingService.getRuleSets()
    }).subscribe({
      next: (res) => {
        this.plans.set(res.plans);
        this.ruleSets.set(res.ruleSets);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  getRuleSetForPlan(plan: AnimalFeedingPlan): FeedingRuleSet | undefined {
    return this.ruleSets().find(r => r.id === plan.ruleSetId);
  }

  isCollapsed(ruleSetId: string): boolean {
    return this.collapsedGroupIds().has(ruleSetId);
  }

  toggleGroup(ruleSetId: string): void {
    const updated = new Set(this.collapsedGroupIds());
    if (updated.has(ruleSetId)) {
      updated.delete(ruleSetId);
    } else {
      updated.add(ruleSetId);
    }
    this.collapsedGroupIds.set(updated);
  }

  areAllCollapsed(): boolean {
    const groups = this.ruleSetGroups();
    return groups.length > 0 && groups.every(g => this.collapsedGroupIds().has(g.ruleSet.id));
  }

  toggleAllGroups(): void {
    if (this.areAllCollapsed()) {
      this.collapsedGroupIds.set(new Set());
    } else {
      const allIds = new Set(this.ruleSetGroups().map(g => g.ruleSet.id));
      this.collapsedGroupIds.set(allIds);
    }
  }

  openAssignDialog(preselectedRuleSetId?: string, event?: Event): void {
    if (event) event.stopPropagation();

    const dialogRef = this.dialog.open(AssignFeedingPlanDialogComponent, {
      disableClose: true,
      width: '95vw',
      maxWidth: '600px',
      data: preselectedRuleSetId ? { ruleSetId: preselectedRuleSetId } : null
    });

    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadData();
    });
  }

  openEditRuleDialog(ruleSet: FeedingRuleSet, event?: Event): void {
    if (event) event.stopPropagation();

    const dialogRef = this.dialog.open(FeedingRuleSetDialogComponent, {
      disableClose: true,
      width: '95vw',
      maxWidth: '850px',
      data: ruleSet
    });

    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadData();
    });
  }

  openCreateRuleSetDialog(): void {
    const dialogRef = this.dialog.open(FeedingRuleSetDialogComponent, {
      disableClose: true,
      width: '95vw',
      maxWidth: '850px'
    });

    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadData();
    });
  }

  openFeedsCostReportDialog(): void {
    const activeFarm = this.contextService.currentFarmValue;
    const currentOrg = this.contextService.currentOrgValue;

    this.dialog.open(AnimalFeedsCostReportDialogComponent, {
      disableClose: false,
      width: '95vw',
      maxWidth: '1200px',
      data: {
        plans: this.plans(),
        farmName: activeFarm?.name || 'Current Farm',
        orgName: currentOrg?.name || 'Farm360 Enterprise'
      }
    });
  }

  cancelPlan(plan: AnimalFeedingPlan): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      disableClose: true,
      width: '450px',
      panelClass: ['!rounded-2xl', '!bg-white', 'dark:!bg-gray-900'],
      data: {
        title: 'Cancel Feeding Plan',
        message: `Are you sure you want to cancel the feeding plan for ${plan.animalTag}? Future daily entries will no longer be generated.`,
        confirmButtonText: 'Cancel Plan',
        cancelButtonText: 'Keep Plan',
        isDestructive: true
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.feedingService.cancelPlan(plan.id).subscribe({
          next: () => {
            this.snackBar.open('Feeding plan canceled', 'Close', { duration: 3000 });
            this.loadData();
          },
          error: (err) => {
            this.snackBar.open(err.error?.detail || 'Failed to cancel plan', 'Close', { duration: 5000 });
          }
        });
      }
    });
  }

  formatPlanType(type: string): string {
    if (!type) return '';
    return type.replace(/([A-Z])/g, ' $1').trim();
  }

  formatAnimalType(type: string): string {
    return type || '';
  }

  formatPurpose(purpose: string): string {
    return purpose || '';
  }

  formatRuleCondition(planType: string, rule: any): string {
    if (planType === 'WeightPercentage' || planType === 'WeightQuantity') {
      if (rule.minWeightKg && rule.maxWeightKg) return `${rule.minWeightKg} - ${rule.maxWeightKg} kg`;
      if (rule.minWeightKg) return `> ${rule.minWeightKg} kg`;
      if (rule.maxWeightKg) return `< ${rule.maxWeightKg} kg`;
      return 'All Weights';
    } else if (planType === 'AgeBased') {
      if (rule.minAgeDays && rule.maxAgeDays) return `${rule.minAgeDays} - ${rule.maxAgeDays} days`;
      if (rule.minAgeDays) return `> ${rule.minAgeDays} days`;
      if (rule.maxAgeDays) return `< ${rule.maxAgeDays} days`;
      return 'All Ages';
    }
    return 'Fixed Ration';
  }
}


