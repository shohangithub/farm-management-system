import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MortalityRecordDto, CauseOfDeath } from '../../../models/health.models';
import { AnimalService } from '../../../../livestock/services/animal.service';
import { AnimalDto } from '../../../../livestock/models/animal.models';
import { signal, inject } from '@angular/core';

@Component({
  selector: 'app-mortality-detail-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatIconModule],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-red-500">warning</mat-icon>
            Mortality Details
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Review details of the animal loss record</p>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Content -->
      <div class="p-6 custom-scrollbar max-h-[70vh] overflow-y-auto bg-gray-50/30 dark:bg-gray-900/10">
        
        <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-100 dark:border-gray-700 p-5 shadow-sm mb-5 relative overflow-hidden">
          <div class="absolute right-0 top-0 w-24 h-24 bg-gradient-to-br from-red-50 to-transparent dark:from-red-900/10 rounded-bl-full pointer-events-none"></div>
          
          <h3 class="text-sm font-bold text-gray-900 dark:text-white mb-4 flex items-center gap-2">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-gray-400">info</mat-icon> General Information
          </h3>
          
          <div class="grid grid-cols-2 gap-4">
            <div>
              <span class="block text-[10px] font-bold uppercase tracking-wider text-gray-400 mb-1">Animal Tag</span>
              <span class="text-sm font-semibold text-gray-900 dark:text-white bg-gray-100 dark:bg-gray-700 px-2 py-1 rounded-md inline-flex items-center gap-1.5">
                <mat-icon *ngIf="isLoadingAnimal()" class="!w-[14px] !h-[14px] !text-[14px] animate-spin text-gray-400">autorenew</mat-icon>
                {{ isLoadingAnimal() ? 'Loading...' : (animal()?.tagId || data.animalId) }}
              </span>
            </div>
            <div>
              <span class="block text-[10px] font-bold uppercase tracking-wider text-gray-400 mb-1">Date of Death</span>
              <span class="text-sm font-semibold text-gray-900 dark:text-white">{{ data.deathDate | date:'fullDate' }}</span>
            </div>
            <div>
              <span class="block text-[10px] font-bold uppercase tracking-wider text-gray-400 mb-1">Cause of Death</span>
              <span class="text-sm font-semibold text-gray-900 dark:text-white inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-red-50 text-red-700 dark:bg-red-500/10 dark:text-red-400 border border-red-100 dark:border-red-500/20">
                {{ getCauseName(data.causeOfDeath) }}
              </span>
            </div>
            <div *ngIf="data.diseaseName">
              <span class="block text-[10px] font-bold uppercase tracking-wider text-gray-400 mb-1">Disease Name</span>
              <span class="text-sm font-bold text-red-600 dark:text-red-400">{{ data.diseaseName }}</span>
            </div>
          </div>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-5 mb-5">
          <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-100 dark:border-gray-700 p-4 shadow-sm flex items-start gap-3">
            <div class="p-2 bg-amber-50 dark:bg-amber-900/20 text-amber-600 dark:text-amber-400 rounded-lg shrink-0 mt-0.5">
              <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">payments</mat-icon>
            </div>
            <div>
              <span class="block text-[10px] font-bold uppercase tracking-wider text-gray-400 mb-0.5">Economic Loss (BDT)</span>
              <div class="text-lg font-bold text-gray-900 dark:text-white">
                <span *ngIf="data.estimatedEconomicLossBdt">৳ {{ data.estimatedEconomicLossBdt | number:'1.0-0' }}</span>
                <span *ngIf="!data.estimatedEconomicLossBdt" class="text-gray-400 text-sm">Not Specified</span>
              </div>
            </div>
          </div>
          
          <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-100 dark:border-gray-700 p-4 shadow-sm flex items-start gap-3">
            <div class="p-2 bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400 rounded-lg shrink-0 mt-0.5">
              <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">person</mat-icon>
            </div>
            <div>
              <span class="block text-[10px] font-bold uppercase tracking-wider text-gray-400 mb-0.5">Recorded By</span>
              <div class="text-sm font-semibold text-gray-700 dark:text-gray-300 break-all flex items-center gap-2">
                System Administrator
              </div>
            </div>
          </div>
        </div>

        <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-100 dark:border-gray-700 p-5 shadow-sm">
          <h3 class="text-sm font-bold text-gray-900 dark:text-white mb-3 flex items-center gap-2">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-gray-400">description</mat-icon> Post Mortem Notes
          </h3>
          <div class="text-sm text-gray-700 dark:text-gray-300 leading-relaxed whitespace-pre-wrap bg-gray-50 dark:bg-gray-900/50 p-4 rounded-lg border border-gray-100 dark:border-gray-700/50" *ngIf="data.postMortemNotes">
            {{ data.postMortemNotes }}
          </div>
          <div class="text-sm text-gray-400 italic py-2" *ngIf="!data.postMortemNotes">
            No notes were provided for this record.
          </div>
        </div>
      </div>

      <!-- Footer Actions -->
      <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end shrink-0">
        <button type="button" mat-dialog-close
          class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
          Close
        </button>
      </div>
    </div>
  `,
  styles: [`
    .custom-scrollbar::-webkit-scrollbar {
      width: 6px;
    }
    .custom-scrollbar::-webkit-scrollbar-track {
      background: transparent;
    }
    .custom-scrollbar::-webkit-scrollbar-thumb {
      background-color: rgba(156, 163, 175, 0.5);
      border-radius: 20px;
    }
    .custom-scrollbar:hover::-webkit-scrollbar-thumb {
      background-color: rgba(156, 163, 175, 0.8);
    }
  `]
})
export class MortalityDetailDialog {
  private animalService = inject(AnimalService);
  animal = signal<AnimalDto | null>(null);
  isLoadingAnimal = signal(true);

  constructor(
    public dialogRef: MatDialogRef<MortalityDetailDialog>,
    @Inject(MAT_DIALOG_DATA) public data: MortalityRecordDto
  ) {
    this.animalService.getById(this.data.animalId).subscribe({
      next: (res) => {
        this.animal.set(res);
        this.isLoadingAnimal.set(false);
      },
      error: () => {
        this.isLoadingAnimal.set(false);
      }
    });
  }

  getCauseName(causeValue: CauseOfDeath | string): string {
    switch (causeValue) {
      case CauseOfDeath.Disease: return 'Disease';
      case CauseOfDeath.Accident: return 'Accident';
      case CauseOfDeath.NaturalCauses: return 'Natural Causes';
      case CauseOfDeath.Unknown: return 'Unknown';
      case CauseOfDeath.Slaughter: return 'Slaughter';
      default: return 'Unknown';
    }
  }
}
