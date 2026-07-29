import { Component, ChangeDetectionStrategy, input, signal, inject } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { MatIconModule } from '@angular/material/icon';
import { FormsModule } from '@angular/forms';

export interface SaleSimulationResult {
  animalId: string;
  targetDate: string;
  daysFromNow: number;
  projectedWeightKg: number;
  estimatedSalePriceBdt: number;
  projectedAdditionalCostBdt: number;
  projectedTotalCostBdt: number;
  projectedProfitMarginBdt: number;
}

@Component({
  selector: 'app-what-if-simulator',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DecimalPipe, MatIconModule, FormsModule],
  template: `
    <div class="bg-gradient-to-r from-purple-900 to-indigo-900 rounded-2xl shadow-lg border border-purple-700/50 p-6 text-white relative overflow-hidden animate-fade-in-up mt-6">
      <mat-icon class="absolute -right-4 -bottom-4 text-[120px] text-purple-500/10 rotate-[-15deg] pointer-events-none">science</mat-icon>
      
      <div class="relative z-10 flex flex-col gap-6">
        <div class="flex items-center justify-between">
          <div>
            <div class="flex items-center gap-2 mb-2">
              <span class="px-2 py-0.5 text-[10px] uppercase tracking-wider font-bold rounded-md bg-purple-500/20 text-purple-300 border border-purple-400/30">
                Decision Support
              </span>
            </div>
            <h3 class="text-xl font-bold text-white tracking-tight">"What-If" Sale Simulator</h3>
            <p class="text-purple-100/80 text-sm mt-1 max-w-xl">
              Simulate future sale dates to see how weight gain and feed costs affect your profit margin. Find the sweet spot to sell.
            </p>
          </div>
        </div>

        <div class="flex flex-col md:flex-row gap-6 items-end">
          <div class="w-full md:w-1/3">
            <label class="block text-xs text-purple-200 uppercase tracking-wide mb-2 font-bold">Target Sale Date</label>
            <input type="date"
                   class="w-full bg-black/30 border border-purple-500/30 rounded-xl px-4 py-3 text-white focus:outline-none focus:border-purple-400 focus:ring-1 focus:ring-purple-400 transition-all"
                   [ngModel]="targetDate()"
                   (ngModelChange)="onDateChange($event)" />
          </div>
          <button (click)="simulate()" 
                  [disabled]="isSimulating()"
                  class="bg-purple-600 hover:bg-purple-500 text-white font-bold py-3 px-6 rounded-xl transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
            <mat-icon *ngIf="!isSimulating()">play_arrow</mat-icon>
            <mat-icon *ngIf="isSimulating()" class="animate-spin">sync</mat-icon>
            {{ isSimulating() ? 'Simulating...' : 'Run Simulation' }}
          </button>
        </div>

        <div *ngIf="simulationResult() as result" class="grid grid-cols-1 md:grid-cols-4 gap-4 mt-4 animate-fade-in-up">
          <div class="bg-black/20 rounded-xl p-4 border border-white/5">
             <p class="text-[10px] text-purple-200 uppercase tracking-wider font-semibold mb-1">Time Horizon</p>
             <p class="text-xl font-bold">+{{ result.daysFromNow }} Days</p>
          </div>
          <div class="bg-black/20 rounded-xl p-4 border border-white/5">
             <p class="text-[10px] text-purple-200 uppercase tracking-wider font-semibold mb-1">Proj. Weight</p>
             <p class="text-xl font-bold">{{ result.projectedWeightKg | number:'1.1-1' }} kg</p>
          </div>
          <div class="bg-black/20 rounded-xl p-4 border border-white/5">
             <p class="text-[10px] text-purple-200 uppercase tracking-wider font-semibold mb-1">Est. Feed Cost</p>
             <p class="text-xl font-bold text-amber-300">৳ {{ result.projectedAdditionalCostBdt | number:'1.0-0' }}</p>
          </div>
          <div class="bg-purple-500/20 rounded-xl p-4 border border-purple-400/50">
             <p class="text-[10px] text-purple-200 uppercase tracking-wider font-semibold mb-1">Proj. Net Profit</p>
             <p class="text-xl font-bold text-emerald-300 flex items-center gap-1">
               <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">account_balance_wallet</mat-icon>
               ৳ {{ result.projectedProfitMarginBdt | number:'1.0-0' }}
             </p>
          </div>
        </div>

        <div *ngIf="error()" class="bg-red-500/20 border border-red-500/30 text-red-200 p-4 rounded-xl text-sm flex items-start gap-2">
          <mat-icon class="text-red-400">error_outline</mat-icon>
          <p class="mt-0.5">{{ error() }}</p>
        </div>

      </div>
    </div>
  `
})
export class WhatIfSimulatorComponent {
  animalId = input.required<string>();
  
  private readonly http = inject(HttpClient);
  
  readonly targetDate = signal<string>(this.getTodayPlus30Days());
  readonly isSimulating = signal<boolean>(false);
  readonly simulationResult = signal<SaleSimulationResult | null>(null);
  readonly error = signal<string | null>(null);

  onDateChange(newDate: string) {
    this.targetDate.set(newDate);
  }

  simulate() {
    if (!this.animalId() || !this.targetDate()) return;
    
    this.isSimulating.set(true);
    this.error.set(null);
    this.simulationResult.set(null);

    const url = `/api/v1/intelligence/animals/${this.animalId()}/simulate-sale?targetDate=${this.targetDate()}`;
    
    this.http.get<SaleSimulationResult>(url)
      .subscribe({
        next: (data) => {
          this.simulationResult.set(data);
          this.isSimulating.set(false);
        },
        error: (err) => {
          this.error.set(err.error || 'Failed to run simulation. Ensure the animal has enough weight data.');
          this.isSimulating.set(false);
        }
      });
  }

  private getTodayPlus30Days(): string {
    const d = new Date();
    d.setDate(d.getDate() + 30);
    return d.toISOString().split('T')[0];
  }
}
