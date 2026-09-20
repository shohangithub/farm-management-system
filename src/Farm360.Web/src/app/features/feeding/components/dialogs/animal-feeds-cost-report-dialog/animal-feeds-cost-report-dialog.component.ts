import { Component, ChangeDetectionStrategy, inject, signal, computed, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { AnimalFeedingPlan } from '../../../models/feeding.models';
import { FeedingReportPdfService } from '../../../services/feeding-report-pdf.service';
import { ExportService } from '../../../../../shared/services/export.service';
import { ReportService } from '../../../../reports/services/report.service';

export interface FeedsCostReportDialogData {
  plans: AnimalFeedingPlan[];
  farmName: string;
  orgName?: string;
}

@Component({
  selector: 'app-animal-feeds-cost-report-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule
  ],
  styles: [`
    .custom-scrollbar::-webkit-scrollbar { width: 6px; height: 6px; }
    .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
    .custom-scrollbar::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.4); border-radius: 20px; }
    .custom-scrollbar:hover::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.7); }

    @media print {
      body * { visibility: hidden !important; }
      #printable-report-sheet, #printable-report-sheet * { visibility: visible !important; }
      #printable-report-sheet {
        position: absolute !important;
        left: 0 !important;
        top: 0 !important;
        width: 100% !important;
        background: #ffffff !important;
        color: #000000 !important;
        margin: 0 !important;
        padding: 0 !important;
      }
      @page {
        size: landscape;
        margin: 10mm;
      }
    }
  `],
  template: `
    <div class="p-0 flex flex-col h-full max-h-[90vh] bg-gray-50 dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl">
      
      <!-- ── Dialog Header ────────────────────────────────────────────────── -->
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-b border-gray-100 dark:border-gray-700 flex justify-between items-center z-10 shadow-sm shrink-0">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20">
            <mat-icon class="!w-5 !h-5 !text-[20px]">picture_as_pdf</mat-icon>
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h2 class="text-lg font-bold text-gray-900 dark:text-white leading-tight m-0">
                Animal Feeds & Cost Report
              </h2>
              <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-emerald-50 text-emerald-700 border border-emerald-200 dark:bg-emerald-950/40 dark:text-emerald-300 dark:border-emerald-800">
                Active Plans ({{ activePlans().length }})
              </span>
              <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold bg-blue-50 text-blue-700 border border-blue-200 dark:bg-blue-950/40 dark:text-blue-300 dark:border-blue-800">
                Unicode & বাংলা Supported
              </span>
            </div>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">
              Farm: <strong class="text-gray-700 dark:text-gray-300">{{ data.farmName || 'Current Farm' }}</strong> • 
              Animal-wise ration breakdown with daily & monthly cost projections
            </p>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <!-- View Mode Toggle -->
          <div class="inline-flex rounded-xl bg-gray-100 dark:bg-gray-700 p-1 border border-gray-200 dark:border-gray-600 mr-2">
            <button type="button" (click)="viewMode.set('table')"
              [class.bg-white]="viewMode() === 'table'"
              [class.dark:bg-gray-800]="viewMode() === 'table'"
              [class.text-emerald-700]="viewMode() === 'table'"
              [class.dark:text-emerald-400]="viewMode() === 'table'"
              [class.shadow-xs]="viewMode() === 'table'"
              class="px-2.5 py-1 text-xs font-semibold rounded-lg text-gray-600 dark:text-gray-300 transition-all flex items-center gap-1">
              <mat-icon class="!w-3.5 !h-3.5 !text-[14px]">table_chart</mat-icon> Grid
            </button>
            <button type="button" (click)="viewMode.set('sheet')"
              [class.bg-white]="viewMode() === 'sheet'"
              [class.dark:bg-gray-800]="viewMode() === 'sheet'"
              [class.text-emerald-700]="viewMode() === 'sheet'"
              [class.dark:text-emerald-400]="viewMode() === 'sheet'"
              [class.shadow-xs]="viewMode() === 'sheet'"
              class="px-2.5 py-1 text-xs font-semibold rounded-lg text-gray-600 dark:text-gray-300 transition-all flex items-center gap-1">
              <mat-icon class="!w-3.5 !h-3.5 !text-[14px]">description</mat-icon> Document Sheet
            </button>
          </div>

          <button type="button" (click)="openSapReport()"
            matTooltip="Open in Enterprise SAP Report Viewer with multi-page table banding"
            class="px-2.5 py-1 text-xs font-semibold rounded-lg bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800 transition-all flex items-center gap-1 shadow-xs hover:bg-emerald-100">
            <mat-icon class="!w-3.5 !h-3.5 !text-[14px]">open_in_new</mat-icon> SAP Report
          </button>

          <button mat-icon-button (click)="close()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 rounded-full transition-colors">
            <mat-icon>close</mat-icon>
          </button>
        </div>
      </div>

      <!-- ── Scrollable Dialog Body ───────────────────────────────────────── -->
      <div class="flex-1 overflow-y-auto custom-scrollbar p-6 space-y-6">

        <!-- ── KPI Summary Cards ──────────────────────────────────────────── -->
        <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
          <div class="p-3.5 rounded-xl bg-white dark:bg-gray-800 border border-emerald-100 dark:border-emerald-900/40 shadow-xs">
            <div class="text-[10px] uppercase font-bold text-gray-400 dark:text-gray-500 tracking-wider">Active Animals</div>
            <div class="text-xl font-bold text-gray-900 dark:text-white mt-1">{{ totals().totalAnimals }} Head</div>
            <div class="text-[11px] text-emerald-600 dark:text-emerald-400 font-medium mt-0.5">Enrolled Cattle</div>
          </div>

          <div class="p-3.5 rounded-xl bg-white dark:bg-gray-800 border border-sky-100 dark:border-sky-900/40 shadow-xs">
            <div class="text-[10px] uppercase font-bold text-gray-400 dark:text-gray-500 tracking-wider">Daily Feed Vol.</div>
            <div class="text-xl font-bold text-sky-600 dark:text-sky-400 mt-1">{{ totals().totalDailyFeedKg | number:'1.1-1' }} kg</div>
            <div class="text-[11px] text-gray-500 dark:text-gray-400 mt-0.5 truncate" title="Conc: {{ totals().totalConcentrateKg | number:'1.1-1' }} kg | Rough: {{ totals().totalRoughageKg | number:'1.1-1' }} kg">
              Conc: {{ totals().totalConcentrateKg | number:'1.0-0' }}kg | Rough: {{ totals().totalRoughageKg | number:'1.0-0' }}kg
            </div>
          </div>

          <div class="p-3.5 rounded-xl bg-white dark:bg-gray-800 border border-amber-100 dark:border-amber-900/40 shadow-xs">
            <div class="text-[10px] uppercase font-bold text-gray-400 dark:text-gray-500 tracking-wider">Avg. Feed Cost</div>
            <div class="text-xl font-bold text-amber-600 dark:text-amber-400 mt-1">৳ {{ totals().averageCostPerKgBdt | number:'1.2-2' }}</div>
            <div class="text-[11px] text-gray-500 dark:text-gray-400 mt-0.5">Per kg weighted</div>
          </div>

          <div class="p-3.5 rounded-xl bg-white dark:bg-gray-800 border border-indigo-100 dark:border-indigo-900/40 shadow-xs">
            <div class="text-[10px] uppercase font-bold text-gray-400 dark:text-gray-500 tracking-wider">Daily Expenditure</div>
            <div class="text-xl font-bold text-indigo-600 dark:text-indigo-400 mt-1">৳ {{ totals().totalDailyCostBdt | number:'1.0-0' }}</div>
            <div class="text-[11px] text-gray-500 dark:text-gray-400 mt-0.5">Daily total feed</div>
          </div>

          <div class="p-3.5 rounded-xl bg-white dark:bg-gray-800 border border-rose-100 dark:border-rose-900/40 shadow-xs col-span-2 sm:col-span-1">
            <div class="text-[10px] uppercase font-bold text-gray-400 dark:text-gray-500 tracking-wider">30-Day Projection</div>
            <div class="text-xl font-bold text-rose-600 dark:text-rose-400 mt-1">৳ {{ totals().totalMonthlyCostBdt | number:'1.0-0' }}</div>
            <div class="text-[11px] text-rose-500 dark:text-rose-400 font-medium mt-0.5">Monthly forecast</div>
          </div>
        </div>

        <!-- ── Search & Filter Controls ───────────────────────────────────── -->
        <div class="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
          <div class="relative flex-1 max-w-sm">
            <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 !text-[16px] !w-[16px] !h-[16px] text-gray-400">search</mat-icon>
            <input
              [ngModel]="searchTerm()"
              (ngModelChange)="searchTerm.set($event)"
              placeholder="Filter by animal tag, rule set, or species..."
              class="w-full pl-9 pr-3 py-1.5 text-xs bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 text-gray-900 dark:text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all shadow-xs" />
          </div>

          <!-- Filter chips for Rule Sets -->
          <div class="flex flex-wrap items-center gap-1.5 text-xs">
            <button type="button" (click)="selectedRuleSetFilter.set('ALL')"
              class="px-2.5 py-1 rounded-lg text-xs font-semibold transition-all"
              [ngClass]="selectedRuleSetFilter() === 'ALL' ? 'bg-emerald-600 text-white shadow-xs' : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300 border border-gray-200 dark:border-gray-700 hover:bg-gray-50'">
              All Rules
            </button>
            @for (rs of totals().ruleSetSummaries; track rs.ruleSetName) {
              <button type="button" (click)="selectedRuleSetFilter.set(rs.ruleSetName)"
                class="px-2.5 py-1 rounded-lg text-xs font-semibold transition-all truncate max-w-[170px]"
                [title]="rs.ruleSetName"
                [ngClass]="selectedRuleSetFilter() === rs.ruleSetName ? 'bg-emerald-600 text-white shadow-xs' : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300 border border-gray-200 dark:border-gray-700 hover:bg-gray-50'">
                {{ rs.ruleSetName }} ({{ rs.animalCount }})
              </button>
            }
          </div>
        </div>

        <!-- ── VIEW MODE 1: Interactive Table ─────────────────────────────── -->
        <div *ngIf="viewMode() === 'table'" class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 overflow-hidden shadow-xs">
          <div class="overflow-x-auto max-h-[380px] custom-scrollbar">
            <table class="w-full text-left border-collapse text-xs">
              <thead class="sticky top-0 z-10 bg-gray-50/95 dark:bg-gray-900/95 backdrop-blur-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider font-bold border-b border-gray-200 dark:border-gray-700">
                <tr>
                  <th class="px-3 py-2.5 text-center w-10">#</th>
                  <th class="px-4 py-2.5">Animal Tag</th>
                  <th class="px-3 py-2.5 text-center">Species</th>
                  <th class="px-3 py-2.5 text-right">Weight</th>
                  <th class="px-4 py-2.5">Rule Set</th>
                  <th class="px-4 py-2.5">Ration / Formula</th>
                  <th class="px-3 py-2.5 text-right">Conc.</th>
                  <th class="px-3 py-2.5 text-right">Rough.</th>
                  <th class="px-3 py-2.5 text-right">Daily Feed</th>
                  <th class="px-3 py-2.5 text-right">Rate</th>
                  <th class="px-4 py-2.5 text-right">Daily Cost</th>
                  <th class="px-4 py-2.5 text-right">30-Day Cost</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                @for (plan of filteredPlans(); track plan.id; let i = $index) {
                  <tr class="hover:bg-gray-50/60 dark:hover:bg-gray-750/50 transition-colors">
                    <td class="px-3 py-2 text-center text-gray-400">{{ i + 1 }}</td>
                    <td class="px-4 py-2 font-bold text-gray-900 dark:text-white flex items-center gap-1.5">
                      <mat-icon class="!text-[14px] !w-[14px] !h-[14px] text-emerald-600 dark:text-emerald-400">pets</mat-icon>
                      {{ plan.animalTag }}
                    </td>
                    <td class="px-3 py-2 text-center text-gray-500 dark:text-gray-400">
                      {{ plan.animalSpecies || 'Cattle' }}
                    </td>
                    <td class="px-3 py-2 text-right text-gray-600 dark:text-gray-300">
                      {{ plan.animalWeightKg ? (plan.animalWeightKg | number:'1.1-1') + ' kg' : '—' }}
                    </td>
                    <td class="px-4 py-2 font-medium text-gray-800 dark:text-gray-200 truncate max-w-[160px]" [title]="plan.ruleSetName">
                      {{ plan.ruleSetName }}
                    </td>
                    <td class="px-4 py-2 text-gray-500 dark:text-gray-400 truncate max-w-[160px]" [title]="plan.formulaName || 'Standard Ration'">
                      {{ plan.formulaName || 'Standard Ration' }}
                    </td>
                    <td class="px-3 py-2 text-right text-gray-600 dark:text-gray-300">
                      {{ (plan.concentrateKgPerDay || 0) | number:'1.2-2' }} kg
                    </td>
                    <td class="px-3 py-2 text-right text-gray-600 dark:text-gray-300">
                      {{ (plan.roughageKgPerDay || 0) | number:'1.2-2' }} kg
                    </td>
                    <td class="px-3 py-2 text-right font-extrabold text-emerald-600 dark:text-emerald-400">
                      {{ (plan.expectedDailyFeedKg || 0) | number:'1.2-2' }} kg
                    </td>
                    <td class="px-3 py-2 text-right text-gray-600 dark:text-gray-300">
                      ৳ {{ (plan.estimatedCostPerKgBdt || 0) | number:'1.2-2' }}
                    </td>
                    <td class="px-4 py-2 text-right font-bold text-gray-900 dark:text-white">
                      ৳ {{ (plan.estimatedDailyCostBdt || 0) | number:'1.2-2' }}
                    </td>
                    <td class="px-4 py-2 text-right font-bold text-rose-600 dark:text-rose-400">
                      ৳ {{ ((plan.estimatedDailyCostBdt || 0) * 30) | number:'1.0-0' }}
                    </td>
                  </tr>
                }

                @if (filteredPlans().length === 0) {
                  <tr>
                    <td colspan="12" class="px-6 py-8 text-center text-gray-400 dark:text-gray-500">
                      No active feeding plans found matching current criteria.
                    </td>
                  </tr>
                }
              </tbody>
              <!-- Totals Sticky Foot -->
              <tfoot class="bg-gray-100/90 dark:bg-gray-900/90 font-bold border-t-2 border-gray-300 dark:border-gray-700">
                <tr>
                  <td class="px-3 py-2.5"></td>
                  <td class="px-4 py-2.5 text-gray-900 dark:text-white" colspan="5">
                    TOTALS ({{ filteredPlans().length }} Animals)
                  </td>
                  <td class="px-3 py-2.5 text-right text-gray-800 dark:text-gray-200">
                    {{ filteredTotals().totalConcentrateKg | number:'1.2-2' }} kg
                  </td>
                  <td class="px-3 py-2.5 text-right text-gray-800 dark:text-gray-200">
                    {{ filteredTotals().totalRoughageKg | number:'1.2-2' }} kg
                  </td>
                  <td class="px-3 py-2.5 text-right text-emerald-700 dark:text-emerald-300 font-black">
                    {{ filteredTotals().totalDailyFeedKg | number:'1.2-2' }} kg
                  </td>
                  <td class="px-3 py-2.5 text-right text-gray-700 dark:text-gray-300">
                    ৳ {{ filteredTotals().averageCostPerKgBdt | number:'1.2-2' }}
                  </td>
                  <td class="px-4 py-2.5 text-right text-emerald-700 dark:text-emerald-300 font-black">
                    ৳ {{ filteredTotals().totalDailyCostBdt | number:'1.2-2' }}
                  </td>
                  <td class="px-4 py-2.5 text-right text-rose-700 dark:text-rose-300 font-black">
                    ৳ {{ filteredTotals().totalMonthlyCostBdt | number:'1.0-0' }}
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>

        <!-- ── VIEW MODE 2: Document Sheet Preview (Also used for Print & PDF capture) ── -->
        <div [ngClass]="viewMode() === 'sheet' ? 'flex justify-center my-2' : 'fixed -left-[9999px] top-0 pointer-events-none'" class="w-full">
          <div #reportSheetRef id="printable-report-sheet" style="background-color: #ffffff !important; color: #0f172a !important;" class="pdf-page bg-white text-slate-900 w-[1100px] p-8 rounded-xl shadow-lg border border-gray-200 font-sans">
            
            <!-- Top Emerald Accent -->
            <div class="h-1.5 bg-emerald-600 rounded-full mb-4"></div>

            <!-- Letterhead -->
            <div class="flex justify-between items-start border-b border-slate-200 pb-4 mb-4">
              <div>
                <h1 class="text-xl font-extrabold text-slate-900 tracking-tight m-0">FARM360 AI</h1>
                <p class="text-[11px] font-semibold text-emerald-700 uppercase tracking-wider mt-0.5 m-0">Intelligent Livestock Nutrition & Expenditure Suite</p>
                <p class="text-xs text-slate-500 mt-1 m-0">Farm: <strong>{{ data.farmName || 'Primary Farm' }}</strong> | Org: <strong>{{ data.orgName || 'Farm360 Enterprise' }}</strong></p>
              </div>
              <div class="text-right">
                <h2 class="text-base font-black text-emerald-700 m-0">ANIMAL-WISE FEEDS & COST REPORT</h2>
                <p class="text-xs text-slate-500 mt-0.5 m-0">Active Feeding Plans Allocation</p>
                <p class="text-[11px] text-slate-400 mt-1 m-0">Date: {{ currentDateFormatted }} | Currency: BDT (৳)</p>
              </div>
            </div>

            <!-- KPI Ribbon in Sheet -->
            <div class="grid grid-cols-5 gap-2.5 mb-5 text-center">
              <div class="p-2.5 rounded-lg bg-emerald-50/70 border border-emerald-200">
                <div class="text-[9px] uppercase font-bold text-emerald-800">Active Animals</div>
                <div class="text-base font-black text-slate-900 mt-0.5">{{ filteredTotals().totalAnimals }} Head</div>
              </div>
              <div class="p-2.5 rounded-lg bg-sky-50/70 border border-sky-200">
                <div class="text-[9px] uppercase font-bold text-sky-800">Daily Feed Vol.</div>
                <div class="text-base font-black text-sky-700 mt-0.5">{{ filteredTotals().totalDailyFeedKg | number:'1.1-1' }} kg</div>
              </div>
              <div class="p-2.5 rounded-lg bg-amber-50/70 border border-amber-200">
                <div class="text-[9px] uppercase font-bold text-amber-800">Avg Cost / kg</div>
                <div class="text-base font-black text-amber-700 mt-0.5">৳ {{ filteredTotals().averageCostPerKgBdt | number:'1.2-2' }}</div>
              </div>
              <div class="p-2.5 rounded-lg bg-indigo-50/70 border border-indigo-200">
                <div class="text-[9px] uppercase font-bold text-indigo-800">Daily Total Cost</div>
                <div class="text-base font-black text-indigo-700 mt-0.5">৳ {{ filteredTotals().totalDailyCostBdt | number:'1.0-0' }}</div>
              </div>
              <div class="p-2.5 rounded-lg bg-rose-50/70 border border-rose-200">
                <div class="text-[9px] uppercase font-bold text-rose-800">Projected 30-Day</div>
                <div class="text-base font-black text-rose-700 mt-0.5">৳ {{ filteredTotals().totalMonthlyCostBdt | number:'1.0-0' }}</div>
              </div>
            </div>

            <!-- Detail Table with Full Bengali Unicode support -->
            <table class="w-full text-left border-collapse text-[11px] mb-5">
              <thead>
                <tr class="bg-emerald-600 text-white font-bold text-center">
                  <th class="p-2 w-8">#</th>
                  <th class="p-2 text-left">Animal Tag</th>
                  <th class="p-2">Species</th>
                  <th class="p-2 text-right">Weight</th>
                  <th class="p-2 text-left">Rule Set (ফিডিং রুল)</th>
                  <th class="p-2 text-left">Ration Formula</th>
                  <th class="p-2 text-right">Conc.</th>
                  <th class="p-2 text-right">Rough.</th>
                  <th class="p-2 text-right">Daily Feed</th>
                  <th class="p-2 text-right">Rate</th>
                  <th class="p-2 text-right">Daily Cost</th>
                  <th class="p-2 text-right">30-Day Cost</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-200">
                @for (plan of filteredPlans(); track plan.id; let idx = $index) {
                  <tr [class.bg-slate-50]="idx % 2 === 1">
                    <td class="p-2 text-center text-slate-400">{{ idx + 1 }}</td>
                    <td class="p-2 font-bold text-slate-900">{{ plan.animalTag }}</td>
                    <td class="p-2 text-center text-slate-600">{{ plan.animalSpecies || 'Cattle' }}</td>
                    <td class="p-2 text-right text-slate-700">{{ plan.animalWeightKg ? (plan.animalWeightKg | number:'1.1-1') + ' kg' : '—' }}</td>
                    <td class="p-2 font-medium text-slate-900">{{ plan.ruleSetName }}</td>
                    <td class="p-2 text-slate-600">{{ plan.formulaName || 'Standard Ration' }}</td>
                    <td class="p-2 text-right">{{ (plan.concentrateKgPerDay || 0) | number:'1.2-2' }} kg</td>
                    <td class="p-2 text-right">{{ (plan.roughageKgPerDay || 0) | number:'1.2-2' }} kg</td>
                    <td class="p-2 text-right font-bold text-emerald-700">{{ (plan.expectedDailyFeedKg || 0) | number:'1.2-2' }} kg</td>
                    <td class="p-2 text-right">৳ {{ (plan.estimatedCostPerKgBdt || 0) | number:'1.2-2' }}</td>
                    <td class="p-2 text-right font-bold text-slate-900">৳ {{ (plan.estimatedDailyCostBdt || 0) | number:'1.2-2' }}</td>
                    <td class="p-2 text-right font-bold text-rose-700">৳ {{ ((plan.estimatedDailyCostBdt || 0) * 30) | number:'1.0-0' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot class="bg-slate-100 font-bold border-t-2 border-slate-300">
                <tr>
                  <td class="p-2"></td>
                  <td class="p-2 text-slate-900" colspan="5">TOTALS ({{ filteredPlans().length }} Animals)</td>
                  <td class="p-2 text-right">{{ filteredTotals().totalConcentrateKg | number:'1.2-2' }} kg</td>
                  <td class="p-2 text-right">{{ filteredTotals().totalRoughageKg | number:'1.2-2' }} kg</td>
                  <td class="p-2 text-right text-emerald-800 font-extrabold">{{ filteredTotals().totalDailyFeedKg | number:'1.2-2' }} kg</td>
                  <td class="p-2 text-right">৳ {{ filteredTotals().averageCostPerKgBdt | number:'1.2-2' }}</td>
                  <td class="p-2 text-right text-emerald-800 font-extrabold">৳ {{ filteredTotals().totalDailyCostBdt | number:'1.2-2' }}</td>
                  <td class="p-2 text-right text-rose-800 font-extrabold">৳ {{ filteredTotals().totalMonthlyCostBdt | number:'1.0-0' }}</td>
                </tr>
              </tfoot>
            </table>

            <!-- Rule Set Distribution Table -->
            <div *ngIf="totals().ruleSetSummaries.length > 1" class="mb-6">
              <h3 class="text-xs font-bold uppercase tracking-wider text-slate-700 mb-2">Rule Set Distribution & Cost Allocation</h3>
              <table class="w-full text-left border-collapse text-[10px]">
                <thead>
                  <tr class="bg-teal-700 text-white font-bold">
                    <th class="p-1.5">Rule Set Name</th>
                    <th class="p-1.5 text-center">Animals</th>
                    <th class="p-1.5 text-right">Conc. (kg)</th>
                    <th class="p-1.5 text-right">Rough. (kg)</th>
                    <th class="p-1.5 text-right">Total Feed (kg)</th>
                    <th class="p-1.5 text-right">Daily Cost (৳)</th>
                    <th class="p-1.5 text-right">30-Day Cost (৳)</th>
                    <th class="p-1.5 text-center">% Share</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-200">
                  @for (rs of totals().ruleSetSummaries; track rs.ruleSetName) {
                    <tr>
                      <td class="p-1.5 font-semibold text-slate-900">{{ rs.ruleSetName }}</td>
                      <td class="p-1.5 text-center">{{ rs.animalCount }}</td>
                      <td class="p-1.5 text-right">{{ rs.totalDailyConcentrateKg | number:'1.2-2' }}</td>
                      <td class="p-1.5 text-right">{{ rs.totalDailyRoughageKg | number:'1.2-2' }}</td>
                      <td class="p-1.5 text-right font-bold text-teal-800">{{ rs.totalDailyFeedKg | number:'1.2-2' }}</td>
                      <td class="p-1.5 text-right">৳ {{ rs.totalDailyCostBdt | number:'1.2-2' }}</td>
                      <td class="p-1.5 text-right font-bold">৳ {{ rs.totalMonthlyCostBdt | number:'1.0-0' }}</td>
                      <td class="p-1.5 text-center font-bold text-slate-600">{{ rs.percentageOfCost | number:'1.1-1' }}%</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <!-- Signatures Section -->
            <div class="grid grid-cols-3 gap-8 pt-8 mt-4 border-t border-slate-200 text-center">
              <div>
                <div class="border-b border-slate-400 pb-1 mb-1"></div>
                <div class="text-[11px] font-bold text-slate-900">Prepared By</div>
                <div class="text-[10px] text-slate-500">Feed Management Specialist</div>
              </div>
              <div>
                <div class="border-b border-slate-400 pb-1 mb-1"></div>
                <div class="text-[11px] font-bold text-slate-900">Verified By</div>
                <div class="text-[10px] text-slate-500">Veterinarian / Farm Supervisor</div>
              </div>
              <div>
                <div class="border-b border-slate-400 pb-1 mb-1"></div>
                <div class="text-[11px] font-bold text-slate-900">Approved By</div>
                <div class="text-[10px] text-slate-500">General Farm Manager</div>
              </div>
            </div>

            <!-- Sheet Footer -->
            <div class="flex justify-between items-center text-[9px] text-slate-400 mt-6 pt-2 border-t border-slate-100">
              <span>Farm360 AI Livestock Engine • Confidential Operational Record</span>
              <span>Generated on {{ currentDateFormatted }}</span>
            </div>

          </div>
        </div>

      </div>

      <!-- ── Dialog Footer Actions ────────────────────────────────────────── -->
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-t border-gray-100 dark:border-gray-700 flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 z-10 shadow-sm shrink-0">
        
        <div class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
          <mat-icon class="!text-[16px] !w-[16px] !h-[16px] text-emerald-600">verified</mat-icon>
          <span>Full Unicode rendering with high-resolution A4 Landscape export</span>
        </div>

        <div class="flex items-center justify-end gap-2.5">
          <!-- Close Button -->
          <button type="button" (click)="close()"
            class="px-4 py-2 text-xs font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-xl hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors">
            Close
          </button>

          <!-- CSV Export Button -->
          <button type="button" (click)="exportCsv()"
            matTooltip="Download raw tabular data as spreadsheet"
            class="px-3.5 py-2 text-xs font-semibold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-xl transition-colors shadow-xs inline-flex items-center gap-1.5">
            <mat-icon class="!text-[15px] !w-[15px] !h-[15px] text-teal-600">table_chart</mat-icon> Export CSV
          </button>

          <!-- Excel Export Button -->
          <button type="button" (click)="exportExcel()"
            matTooltip="Download Excel spreadsheet"
            class="px-3.5 py-2 text-xs font-semibold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-xl transition-colors shadow-xs inline-flex items-center gap-1.5">
            <mat-icon class="!text-[15px] !w-[15px] !h-[15px] text-emerald-600">description</mat-icon> Export Excel
          </button>

          <!-- Print Button -->
          <button type="button" (click)="printReport()"
            matTooltip="Print formatted report via browser"
            class="px-3.5 py-2 text-xs font-semibold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-xl transition-colors shadow-xs inline-flex items-center gap-1.5">
            <mat-icon class="!text-[15px] !w-[15px] !h-[15px] text-gray-600">print</mat-icon> Print
          </button>

          <!-- PDF Download Button (Primary) -->
          <button type="button" (click)="downloadPdf()" [disabled]="isExportingPdf()"
            class="px-4 py-2 text-xs font-semibold text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed rounded-xl transition-all shadow-sm shadow-emerald-500/20 inline-flex items-center gap-1.5">
            <mat-icon *ngIf="isExportingPdf()" class="animate-spin !w-4 !h-4 !text-[16px]">refresh</mat-icon>
            <mat-icon *ngIf="!isExportingPdf()" class="!text-[16px] !w-[16px] !h-[16px]">file_download</mat-icon>
            {{ isExportingPdf() ? 'Generating PDF...' : 'Download PDF Report' }}
          </button>
        </div>
      </div>

    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnimalFeedsCostReportDialogComponent {
  @ViewChild('reportSheetRef') reportSheetRef?: ElementRef<HTMLElement>;

  private readonly dialogRef = inject(MatDialogRef<AnimalFeedsCostReportDialogComponent>);
  readonly data = inject<FeedsCostReportDialogData>(MAT_DIALOG_DATA);
  private readonly pdfService = inject(FeedingReportPdfService);
  private readonly exportService = inject(ExportService);
  private readonly reportService = inject(ReportService);
  private readonly router = inject(Router);

  readonly searchTerm = signal<string>('');
  readonly selectedRuleSetFilter = signal<string>('ALL');
  readonly viewMode = signal<'table' | 'sheet'>('table');
  readonly isExportingPdf = signal(false);

  readonly currentDateFormatted = new Date().toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric'
  });

  /**
   * Only active plans are considered for this report
   */
  readonly activePlans = computed(() => {
    const list = this.data?.plans || [];
    const active = list.filter(p => p.isActive);
    return active.length > 0 ? active : list;
  });

  /**
   * Grand totals for all active plans
   */
  readonly totals = computed(() => {
    return this.pdfService.calculateReportTotals(this.activePlans());
  });

  /**
   * Filtered plans based on user search and rule set chip
   */
  readonly filteredPlans = computed(() => {
    let list = this.activePlans();
    const ruleFilter = this.selectedRuleSetFilter();
    if (ruleFilter !== 'ALL') {
      list = list.filter(p => (p.ruleSetName || '') === ruleFilter);
    }

    const term = this.searchTerm().trim().toLowerCase();
    if (!term) return list;

    return list.filter(p =>
      (p.animalTag || '').toLowerCase().includes(term) ||
      (p.ruleSetName || '').toLowerCase().includes(term) ||
      (p.formulaName || '').toLowerCase().includes(term) ||
      (p.animalSpecies || '').toLowerCase().includes(term)
    );
  });

  /**
   * Dynamic totals reflecting currently filtered view
   */
  readonly filteredTotals = computed(() => {
    return this.pdfService.calculateReportTotals(this.filteredPlans());
  });

  openSapReport(): void {
    this.dialogRef.close();
    this.router.navigate(['/reports', 'feeding.plans-cost-projection']);
  }

  downloadPdf(): void {
    this.isExportingPdf.set(true);

    this.reportService.export('feeding.plans-cost-projection', 'pdf', {}).subscribe({
      next: res => {
        if (res.body) {
          const fn = this.reportService.fileNameFrom(res.headers, 'Farm360_Feeding_Plans_Cost_Report.pdf');
          this.reportService.saveBlob(res.body, fn);
        }
        this.isExportingPdf.set(false);
      },
      error: async err => {
        console.warn('Server-side PDF export fallback to HTML renderer:', err);
        if (this.reportSheetRef) {
          const dateStr = new Date().toISOString().split('T')[0];
          const safeFarmName = (this.data.farmName || 'Farm').replace(/[^a-zA-Z0-9_-]/g, '_');
          const filename = `Farm360_Feeds_Cost_Report_${safeFarmName}_${dateStr}.pdf`;
          try {
            await this.pdfService.downloadPdfFromHtml(this.reportSheetRef.nativeElement, filename);
          } catch (pdfErr) {
            console.error('HTML PDF export error:', pdfErr);
          }
        }
        this.isExportingPdf.set(false);
      }
    });
  }

  exportExcel(): void {
    this.reportService.export('feeding.plans-cost-projection', 'xlsx', {}).subscribe({
      next: res => {
        if (res.body) {
          const fn = this.reportService.fileNameFrom(res.headers, 'Farm360_Feeding_Plans_Cost_Report.xlsx');
          this.reportService.saveBlob(res.body, fn);
        }
      },
      error: err => console.error('Excel export error:', err)
    });
  }

  printReport(): void {
    window.print();
  }

  exportCsv(): void {
    const plansToExport = this.filteredPlans().length > 0 ? this.filteredPlans() : this.activePlans();
    const csvData = plansToExport.map((p, idx) => ({
      'SL': idx + 1,
      'Animal Tag': p.animalTag,
      'Species': p.animalSpecies || 'Cattle',
      'Weight (kg)': p.animalWeightKg ?? '',
      'Rule Set': p.ruleSetName,
      'Formula / Feed': p.formulaName || 'Standard Ration',
      'Concentrate (kg/day)': (p.concentrateKgPerDay || 0).toFixed(2),
      'Roughage (kg/day)': (p.roughageKgPerDay || 0).toFixed(2),
      'Total Daily Feed (kg)': (p.expectedDailyFeedKg || 0).toFixed(2),
      'Cost per kg (BDT)': (p.estimatedCostPerKgBdt || 0).toFixed(2),
      'Daily Cost (BDT)': (p.estimatedDailyCostBdt || 0).toFixed(2),
      '30-Day Cost (BDT)': ((p.estimatedDailyCostBdt || 0) * 30).toFixed(2)
    }));

    const dateStr = new Date().toISOString().split('T')[0];
    this.exportService.exportToCsv(csvData, `Farm360_Feeds_Cost_Report_${dateStr}`);
  }

  close(): void {
    this.dialogRef.close();
  }
}
