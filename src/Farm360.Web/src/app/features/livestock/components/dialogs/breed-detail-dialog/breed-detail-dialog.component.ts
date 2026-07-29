import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { BreedService } from '../../../services/breed.service';
import { BreedDto } from '../../../models/breed.models';
import { LoadingComponent } from '../../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-breed-detail-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatIconModule, MatButtonModule, LoadingComponent],
  template: `
    <div class="flex flex-col h-full max-h-[85vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex items-center justify-between shrink-0">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20">
            <mat-icon class="text-xl">pets</mat-icon>
          </div>
          <div>
            <h2 class="text-xl font-bold text-gray-900 dark:text-white m-0 leading-tight">
              {{ breed()?.name ?? 'Loading...' }}
            </h2>
            <p class="text-sm text-gray-500 dark:text-gray-400 m-0">Breed Profile & Intelligence Config</p>
          </div>
        </div>
        <button mat-icon-button mat-dialog-close class="text-gray-400 hover:text-gray-600">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Content -->
      <div class="p-6 overflow-y-auto flex-1 relative min-h-[400px]">
        <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

        <div *ngIf="breed() as b" class="space-y-6 animate-fade-in-up">
          
          <!-- Top Overview Cards -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            
            <!-- Main Info -->
            <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 relative overflow-hidden">
              <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-emerald-500/5 rotate-[-10deg] pointer-events-none">info</mat-icon>
              <div class="flex items-center gap-3 mb-6 relative z-10">
                <div class="w-10 h-10 rounded-xl bg-blue-50 dark:bg-blue-900/20 text-blue-600 flex items-center justify-center">
                  <mat-icon>category</mat-icon>
                </div>
                <div>
                  <div class="text-xs text-gray-500 font-bold uppercase tracking-wider">Classification</div>
                  <div class="font-bold text-gray-900 dark:text-white">{{ b.category }}</div>
                </div>
              </div>
              <div class="space-y-4 relative z-10">
                <div class="flex justify-between items-center pb-3 border-b border-gray-100 dark:border-gray-700/50">
                  <span class="text-sm text-gray-500">Origin</span>
                  <span class="font-semibold text-gray-900 dark:text-white">{{ b.origin || 'Unknown' }}</span>
                </div>
                <div class="flex justify-between items-center pb-3 border-b border-gray-100 dark:border-gray-700/50">
                  <span class="text-sm text-gray-500">Main Purpose</span>
                  <span class="font-semibold text-gray-900 dark:text-white">{{ b.mainPurpose }}</span>
                </div>
                <div class="flex justify-between items-center">
                  <span class="text-sm text-gray-500">Best For</span>
                  <span class="font-semibold text-gray-900 dark:text-white text-right max-w-[150px] truncate" [title]="b.bestFor">{{ b.bestFor || 'General' }}</span>
                </div>
              </div>
            </div>

            <!-- Standard Metrics -->
            <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 relative overflow-hidden">
              <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-emerald-500/5 rotate-[-10deg] pointer-events-none">trending_up</mat-icon>
              <div class="flex items-center gap-3 mb-6 relative z-10">
                <div class="w-10 h-10 rounded-xl bg-emerald-50 dark:bg-emerald-900/20 text-emerald-600 flex items-center justify-center">
                  <mat-icon>trending_up</mat-icon>
                </div>
                <div>
                  <div class="text-xs text-gray-500 font-bold uppercase tracking-wider">Standard Growth</div>
                  <div class="font-bold text-gray-900 dark:text-white">Baselines</div>
                </div>
              </div>
              <div class="space-y-4 relative z-10">
                <div class="flex justify-between items-center pb-3 border-b border-gray-100 dark:border-gray-700/50">
                  <span class="text-sm text-gray-500">Standard ADG</span>
                  <span class="font-bold text-emerald-600">{{ b.standardAdgMin }} - {{ b.standardAdgMax }} kg/day</span>
                </div>
                <div class="flex justify-between items-center pb-3 border-b border-gray-100 dark:border-gray-700/50">
                  <span class="text-sm text-gray-500">Target FCR</span>
                  <span class="font-bold text-emerald-600">{{ b.fcrMin }} - {{ b.fcrMax }}</span>
                </div>
              </div>
            </div>

            <!-- Yield Metrics -->
            <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 relative overflow-hidden md:col-span-2">
              <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-emerald-500/5 rotate-[-10deg] pointer-events-none">water_drop</mat-icon>
              <div class="flex items-center gap-3 mb-6 relative z-10">
                <div class="w-10 h-10 rounded-xl bg-purple-50 dark:bg-purple-900/20 text-purple-600 flex items-center justify-center">
                  <mat-icon>water_drop</mat-icon>
                </div>
                <div>
                  <div class="text-xs text-gray-500 font-bold uppercase tracking-wider">Yield Targets</div>
                  <div class="font-bold text-gray-900 dark:text-white">Dairy Specific</div>
                </div>
              </div>
              <div class="grid grid-cols-2 gap-4 relative z-10">
                <div class="flex flex-col pb-3 border-b md:border-b-0 md:border-r border-gray-100 dark:border-gray-700/50">
                  <span class="text-sm text-gray-500">Milk Yield</span>
                  <span class="font-semibold text-gray-900 dark:text-white text-lg">{{ b.milkYieldMinLiters }} - {{ b.milkYieldMaxLiters }} L/day</span>
                </div>
                <div class="flex flex-col pb-3 border-b md:border-b-0 border-gray-100 dark:border-gray-700/50 pl-0 md:pl-4">
                  <span class="text-sm text-gray-500">Fat Percentage</span>
                  <span class="font-semibold text-gray-900 dark:text-white text-lg">{{ b.fatPercentageMin }}% - {{ b.fatPercentageMax }}%</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Farm Condition Performance Section -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden">
            <div class="p-6 border-b border-gray-100 dark:border-gray-800/50 flex items-center gap-3">
              <div class="w-10 h-10 rounded-xl bg-orange-50 dark:bg-orange-900/20 text-orange-600 flex items-center justify-center">
                <mat-icon>speed</mat-icon>
              </div>
              <div>
                <h3 class="font-bold text-gray-900 dark:text-white text-lg">Growth by Farming Condition</h3>
                <p class="text-xs text-gray-500">Expected Average Daily Gain (ADG) mapped to farm management levels.</p>
              </div>
            </div>
            
            <div class="grid grid-cols-2 md:grid-cols-4 divide-y md:divide-y-0 md:divide-x divide-gray-100 dark:divide-gray-800/50">
              <div class="p-4 text-center hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors">
                <div class="text-sm font-bold text-gray-900 dark:text-white mb-1">Poor</div>
                <div class="text-xl font-extrabold text-red-600">{{ b.adgPoorManagement }} <span class="text-xs font-medium">kg/d</span></div>
              </div>

              <div class="p-4 text-center hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors">
                <div class="text-sm font-bold text-gray-900 dark:text-white mb-1">Average</div>
                <div class="text-xl font-extrabold text-amber-600">{{ b.adgAverageFarm }} <span class="text-xs font-medium">kg/d</span></div>
              </div>

              <div class="p-4 text-center hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors">
                <div class="text-sm font-bold text-gray-900 dark:text-white mb-1">Good</div>
                <div class="text-xl font-extrabold text-blue-600">{{ b.adgGoodCommercialFarm }} <span class="text-xs font-medium">kg/d</span></div>
              </div>

              <div class="p-4 text-center hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors">
                <div class="text-sm font-bold text-gray-900 dark:text-white mb-1">Intensive</div>
                <div class="text-xl font-extrabold text-emerald-600">{{ b.adgIntensiveFattening }} <span class="text-xs font-medium">kg/d</span></div>
              </div>
            </div>
          </div>

        </div>
      </div>
      
      <!-- Footer -->
      <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-900/50 shrink-0 flex items-center justify-end gap-3 rounded-b-2xl">
        <button mat-flat-button mat-dialog-close class="!rounded-xl !bg-emerald-600 hover:!bg-emerald-700 !text-white">
          Close
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BreedDetailDialogComponent implements OnInit {
  private readonly breedService = inject(BreedService);
  public readonly data = inject<{ id: string }>(MAT_DIALOG_DATA);

  breed = signal<BreedDto | null>(null);
  isLoading = signal<boolean>(true);

  ngOnInit(): void {
    if (this.data?.id) {
      this.loadBreed(this.data.id);
    }
  }

  private loadBreed(id: string): void {
    this.isLoading.set(true);
    this.breedService.getBreedById(id).subscribe({
      next: (b) => {
        this.breed.set(b);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load breed', err);
        this.isLoading.set(false);
      }
    });
  }
}
