import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-what-if-simulator',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-emerald-100 dark:border-emerald-900 p-4">
      <h3 class="font-semibold text-emerald-900 dark:text-emerald-100 mb-3 text-sm">What-If Scenario Simulator</h3>
      
      <div class="space-y-3 mb-4">
        <div>
          <label class="text-xs text-gray-500">Feed Cost Adjustment (%)</label>
          <input type="range" class="w-full accent-emerald-500" min="-20" max="20" [value]="feedAdjustment()" (input)="onFeedChange($event)">
          <div class="text-xs text-right font-medium">{{feedAdjustment() > 0 ? '+' : ''}}{{feedAdjustment()}}%</div>
        </div>
      </div>

      <div class="bg-emerald-50 dark:bg-emerald-900/30 p-3 rounded-lg flex justify-between items-center">
        <span class="text-sm font-medium text-emerald-900 dark:text-emerald-100">Projected Margin</span>
        <span class="text-sm font-bold text-emerald-600 dark:text-emerald-400">৳{{projectedMargin() | number}}</span>
      </div>
    </div>
  `
})
export class WhatIfSimulatorComponent {
  @Input() currentMargin = 15000;
  
  feedAdjustment = signal(0);
  
  projectedMargin = signal(this.currentMargin);

  onFeedChange(event: Event) {
    const target = event.target as HTMLInputElement;
    const val = parseInt(target.value, 10);
    this.feedAdjustment.set(val);
    
    // Simple mock calculation: Feed cost is 60% of total costs. 
    // If feed cost increases by 10%, margin drops.
    const newMargin = this.currentMargin - (this.currentMargin * 0.6 * (val / 100));
    this.projectedMargin.set(newMargin);
  }
}
