import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { IntelligencePanelComponent } from '../../components/intelligence-panel/intelligence-panel.component';
import { WhatIfSimulatorComponent } from '../../components/what-if-simulator/what-if-simulator.component';

export interface AnimalIntelligenceDialogData {
  animalId: string;
  tagId: string;
}

@Component({
  selector: 'app-animal-intelligence-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, 
    MatDialogModule, 
    MatIconModule,
    IntelligencePanelComponent,
    WhatIfSimulatorComponent
  ],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-emerald-500">psychology</mat-icon>
            Smart Consultant
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Intelligence Insights for {{ data.tagId }}</p>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Scrollable Body -->
      <div class="p-6 space-y-8 overflow-y-auto custom-scrollbar flex-1 bg-gray-50 dark:bg-gray-900/50">
        <!-- Intelligence Panel -->
        <app-intelligence-panel [animalId]="data.animalId"></app-intelligence-panel>
        
        <!-- What-If Simulator -->
        <app-what-if-simulator [animalId]="data.animalId"></app-what-if-simulator>
      </div>

      <!-- Footer Actions -->
      <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-white dark:bg-gray-800 flex justify-end shrink-0">
        <button type="button" mat-dialog-close
          class="px-6 py-2.5 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-800 border border-transparent dark:border-gray-700 rounded-xl hover:bg-gray-200 dark:hover:bg-gray-700 transition-colors shadow-sm">
          Close Consultant
        </button>
      </div>
    </div>
  `,
  styles: [`
    .custom-scrollbar::-webkit-scrollbar { width: 6px; }
    .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
    .custom-scrollbar::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.5); border-radius: 20px; }
    .custom-scrollbar:hover::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.8); }
  `]
})
export class AnimalIntelligenceDialogComponent {
  readonly dialogRef = inject(MatDialogRef<AnimalIntelligenceDialogComponent>);
  readonly data = inject<AnimalIntelligenceDialogData>(MAT_DIALOG_DATA);
}
