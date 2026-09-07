import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FeedingService } from '../../../services/feeding.service';
import { DailyFeedingEntry, FeedingEntryCostBreakdown } from '../../../models/feeding.models';

export interface CostBreakdownDialogData {
  entry: DailyFeedingEntry;
}

@Component({
  selector: 'app-feeding-cost-breakdown-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule
  ],
  template: `
    <div class="p-0 flex flex-col h-full max-h-[85vh] bg-gray-50 dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl">
      <!-- Header -->
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-b border-gray-100 dark:border-gray-700 flex justify-between items-center shadow-sm shrink-0">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20">
            <mat-icon class="!w-5 !h-5 !text-[20px]">payments</mat-icon>
          </div>
          <div>
            <h2 class="text-lg font-bold text-gray-900 dark:text-white leading-tight m-0">
              Feeding Cost Breakdown
            </h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">
              Animal: <span class="font-bold text-gray-800 dark:text-gray-200">{{ data.entry.animalTag }}</span>
              <span class="mx-1.5 text-gray-300 dark:text-gray-600">•</span>
              Date: <span class="font-medium text-gray-700 dark:text-gray-300">{{ data.entry.targetDate }}</span>
            </p>
          </div>
        </div>
        <button mat-icon-button (click)="close()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 rounded-full transition-colors">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Content Area -->
      <div class="p-6 flex-1 overflow-y-auto space-y-6">
        <!-- Loading State -->
        <div *ngIf="isLoading()" class="flex flex-col items-center justify-center py-12 text-gray-500">
          <mat-icon class="animate-spin !w-8 !h-8 !text-[32px] text-emerald-500 mb-2">refresh</mat-icon>
          <span class="text-sm font-medium">Loading cost breakdown...</span>
        </div>

        <!-- Error State -->
        <div *ngIf="!isLoading() && errorMessage()" class="bg-red-50 dark:bg-red-950/40 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-xl p-4 flex gap-3">
          <mat-icon class="text-red-500 shrink-0">error_outline</mat-icon>
          <div class="text-sm">{{ errorMessage() }}</div>
        </div>

        <!-- Breakdown Details -->
        <ng-container *ngIf="!isLoading() && breakdown() as b">
          <!-- Summary Cards Grid -->
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <div class="bg-white dark:bg-gray-800 p-3.5 rounded-xl border border-gray-100 dark:border-gray-700/80 shadow-xs">
              <span class="text-[11px] font-semibold text-gray-400 uppercase tracking-wider block">Quantity</span>
              <div class="text-base sm:text-lg font-bold text-gray-900 dark:text-white mt-1">
                {{ b.actualKg ?? b.expectedKg }} <span class="text-xs font-normal text-gray-500">kg</span>
              </div>
              <span class="text-[10px] text-gray-400 block mt-0.5">
                {{ b.actualKg != null ? 'Actual consumed' : 'Expected planned' }}
              </span>
            </div>

            <div class="bg-white dark:bg-gray-800 p-3.5 rounded-xl border border-gray-100 dark:border-gray-700/80 shadow-xs">
              <span class="text-[11px] font-semibold text-emerald-600 dark:text-emerald-400 uppercase tracking-wider block">Blended Cost</span>
              <div class="text-base sm:text-lg font-bold text-emerald-600 dark:text-emerald-400 mt-1">
                ৳{{ (b.blendedUnitCostBdt ?? 0) | number:'1.2-2' }} <span class="text-xs font-normal text-gray-500 dark:text-gray-400">/kg</span>
              </div>
              <span class="text-[10px] text-gray-400 block mt-0.5">Weighted avg per kg</span>
            </div>

            <div class="bg-white dark:bg-gray-800 p-3.5 rounded-xl border border-gray-100 dark:border-gray-700/80 shadow-xs">
              <span class="text-[11px] font-semibold text-teal-600 dark:text-teal-400 uppercase tracking-wider block">Total Cost</span>
              <div class="text-base sm:text-lg font-bold text-teal-700 dark:text-teal-300 mt-1">
                ৳{{ (b.totalCostBdt ?? ((b.actualKg ?? b.expectedKg) * (b.blendedUnitCostBdt ?? 0))) | number:'1.2-2' }}
              </div>
              <span class="text-[10px] text-gray-400 block mt-0.5">Total feeding value</span>
            </div>

            <div class="bg-white dark:bg-gray-800 p-3.5 rounded-xl border border-gray-100 dark:border-gray-700/80 shadow-xs">
              <span class="text-[11px] font-semibold text-gray-400 uppercase tracking-wider block">Formula</span>
              <div class="text-xs sm:text-sm font-bold text-gray-900 dark:text-white mt-1 truncate" [matTooltip]="b.formulaName">
                {{ b.formulaName }}
              </div>
              <span class="inline-flex items-center gap-1 text-[10px] font-medium text-emerald-600 dark:text-emerald-400 mt-0.5">
                <span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span> {{ b.status }}
              </span>
            </div>
          </div>

          <!-- Educational Callout -->
          <div class="bg-emerald-50/70 dark:bg-emerald-950/30 text-emerald-900 dark:text-emerald-200 border border-emerald-200/80 dark:border-emerald-800/60 rounded-xl p-3.5 flex gap-3 text-xs leading-relaxed">
            <mat-icon class="text-emerald-600 dark:text-emerald-400 shrink-0 !w-5 !h-5 !text-[20px] mt-0.5">verified_user</mat-icon>
            <div>
              <span class="font-bold">Dynamic Pricing Snapshot:</span>
              Unit costs are captured at the point of consumption using the actual inventory weighted-average cost (WAC). Subsequent price adjustments or new inventory purchases will not alter these historical figures.
            </div>
          </div>

          <!-- Per-Ingredient Breakdown Table -->
          <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 overflow-hidden shadow-xs">
            <div class="px-4 py-3 bg-gray-50/80 dark:bg-gray-800/90 border-b border-gray-200 dark:border-gray-700 flex justify-between items-center">
              <h3 class="text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider m-0">
                Ingredient Cost Breakdown
              </h3>
              <span class="text-xs text-gray-500 font-medium">
                {{ b.ingredients.length }} Ingredients
              </span>
            </div>

            <div class="overflow-x-auto">
              <table class="w-full text-left border-collapse text-xs">
                <thead>
                  <tr class="border-b border-gray-100 dark:border-gray-700/60 text-gray-400 dark:text-gray-500 font-semibold uppercase tracking-wider text-[11px]">
                    <th class="py-2.5 px-4">Ingredient</th>
                    <th class="py-2.5 px-3 text-right">Ratio</th>
                    <th class="py-2.5 px-3 text-right">Quantity</th>
                    <th class="py-2.5 px-3 text-right">Unit Cost</th>
                    <th class="py-2.5 px-3 text-right">Cost (BDT)</th>
                    <th class="py-2.5 px-4 text-center">Cost Source</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100 dark:divide-gray-700/40 text-gray-700 dark:text-gray-300">
                  <tr *ngFor="let item of b.ingredients" class="hover:bg-gray-50/50 dark:hover:bg-gray-700/20 transition-colors">
                    <td class="py-2.5 px-4 font-medium text-gray-900 dark:text-white">
                      {{ item.ingredientName }}
                      <span *ngIf="item.inventoryItemName" class="block text-[10px] text-gray-400 font-normal">
                        Linked: {{ item.inventoryItemName }}
                      </span>
                    </td>
                    <td class="py-2.5 px-3 text-right font-medium">
                      {{ item.percentage }}%
                    </td>
                    <td class="py-2.5 px-3 text-right font-medium">
                      {{ item.allocatedKg | number:'1.2-2' }} kg
                    </td>
                    <td class="py-2.5 px-3 text-right font-mono font-medium">
                      ৳{{ item.unitCostBdt | number:'1.2-2' }}
                    </td>
                    <td class="py-2.5 px-3 text-right font-mono font-bold text-gray-900 dark:text-white">
                      ৳{{ item.totalCostBdt | number:'1.2-2' }}
                    </td>
                    <td class="py-2.5 px-4 text-center">
                      <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold"
                            [ngClass]="item.costSource.includes('Inventory') 
                              ? 'bg-teal-50 dark:bg-teal-950/40 text-teal-700 dark:text-teal-300 border border-teal-200 dark:border-teal-800'
                              : 'bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300 border border-gray-200 dark:border-gray-600'">
                        {{ item.costSource }}
                      </span>
                    </td>
                  </tr>

                  <tr *ngIf="b.ingredients.length === 0">
                    <td colspan="6" class="py-6 text-center text-gray-400">
                      No ingredient details recorded for this formula.
                    </td>
                  </tr>
                </tbody>
                <tfoot class="bg-gray-50/60 dark:bg-gray-800/80 font-bold border-t border-gray-200 dark:border-gray-700">
                  <tr>
                    <td class="py-2.5 px-4 text-gray-900 dark:text-white uppercase text-[11px]">Total</td>
                    <td class="py-2.5 px-3 text-right">
                      {{ totalPercentage(b) }}%
                    </td>
                    <td class="py-2.5 px-3 text-right">
                      {{ (b.actualKg ?? b.expectedKg) | number:'1.2-2' }} kg
                    </td>
                    <td class="py-2.5 px-3 text-right font-mono text-emerald-600 dark:text-emerald-400">
                      ৳{{ (b.blendedUnitCostBdt ?? 0) | number:'1.2-2' }}
                    </td>
                    <td class="py-2.5 px-3 text-right font-mono text-teal-700 dark:text-teal-300">
                      ৳{{ (b.totalCostBdt ?? ((b.actualKg ?? b.expectedKg) * (b.blendedUnitCostBdt ?? 0))) | number:'1.2-2' }}
                    </td>
                    <td></td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>
        </ng-container>
      </div>

      <!-- Footer Actions -->
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3 shadow-sm shrink-0 mt-auto">
        <button type="button" (click)="close()"
          class="px-5 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-xl hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors">
          Close
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeedingCostBreakdownDialogComponent implements OnInit {
  readonly data = inject<CostBreakdownDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<FeedingCostBreakdownDialogComponent>);
  private readonly feedingService = inject(FeedingService);

  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly breakdown = signal<FeedingEntryCostBreakdown | null>(null);

  ngOnInit(): void {
    this.fetchBreakdown();
  }

  fetchBreakdown(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.feedingService.getEntryCostBreakdown(this.data.entry.id).subscribe({
      next: (res) => {
        this.breakdown.set(res);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.detail || 'Failed to load feeding cost breakdown.');
        this.isLoading.set(false);
      }
    });
  }

  totalPercentage(b: FeedingEntryCostBreakdown): number {
    return b.ingredients.reduce((acc, item) => acc + item.percentage, 0);
  }

  close(): void {
    this.dialogRef.close();
  }
}
