import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-intelligence-panel',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-indigo-100 dark:border-indigo-900 overflow-hidden">
      <div class="bg-indigo-50 dark:bg-indigo-900/30 p-4 border-b border-indigo-100 dark:border-indigo-800 flex items-center justify-between">
        <h3 class="font-semibold text-indigo-900 dark:text-indigo-100 m-0">AI Insights</h3>
        <span class="px-2 py-1 bg-indigo-100 text-indigo-700 text-xs font-bold rounded-full">BETA</span>
      </div>
      <div class="p-4 space-y-3">
        <div class="flex gap-3 text-sm">
          <div class="text-indigo-500">✨</div>
          <p class="text-gray-700 dark:text-gray-300 m-0">Growth trajectory indicates animal is <strong>5% above</strong> breed standard ADG.</p>
        </div>
        <div class="flex gap-3 text-sm">
          <div class="text-indigo-500">✨</div>
          <p class="text-gray-700 dark:text-gray-300 m-0">Optimal market sale date projected: <strong>Oct 15, 2026</strong> based on current feed conversion ratio.</p>
        </div>
      </div>
    </div>
  `
})
export class IntelligencePanelComponent {
  @Input() animalId: string | null = null;
}
