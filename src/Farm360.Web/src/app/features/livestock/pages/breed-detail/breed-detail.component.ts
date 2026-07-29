import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { BreedService } from '../../services/breed.service';
import { BreedDto } from '../../models/breed.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-breed-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, PageHeaderComponent, LoadingComponent],
  template: `
    <app-page-header
      [title]="breed()?.name ?? 'Loading...'"
      [description]="breed()?.description ?? 'Breed Profile & Intelligence Config'"
      breadcrumbActiveNode="Breed Profile">
      <div actions>
        <a routerLink="/livestock/breeds" class="px-4 py-2 text-sm font-semibold text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">arrow_back</mat-icon> Back to Breeds
        </a>
      </div>
    </app-page-header>

    <div class="p-6 max-w-7xl mx-auto">
      <div class="relative min-h-[400px]">
        <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

        <div *ngIf="breed() as b" class="space-y-6 animate-fade-in-up">
          
          <!-- Top Overview Cards -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
            
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
            <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 relative overflow-hidden">
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
              <div class="space-y-4 relative z-10">
                <div class="flex justify-between items-center pb-3 border-b border-gray-100 dark:border-gray-700/50">
                  <span class="text-sm text-gray-500">Milk Yield</span>
                  <span class="font-semibold text-gray-900 dark:text-white">{{ b.milkYieldMinLiters }} - {{ b.milkYieldMaxLiters }} L/day</span>
                </div>
                <div class="flex justify-between items-center pb-3 border-b border-gray-100 dark:border-gray-700/50">
                  <span class="text-sm text-gray-500">Fat Percentage</span>
                  <span class="font-semibold text-gray-900 dark:text-white">{{ b.fatPercentageMin }}% - {{ b.fatPercentageMax }}%</span>
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
            
            <div class="grid grid-cols-1 md:grid-cols-4 divide-y md:divide-y-0 md:divide-x divide-gray-100 dark:divide-gray-800/50">
              <div class="p-6 text-center hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors">
                <div class="w-12 h-12 mx-auto rounded-full bg-red-50 text-red-600 flex items-center justify-center mb-3">
                  <mat-icon>trending_down</mat-icon>
                </div>
                <div class="text-sm font-bold text-gray-900 dark:text-white mb-1">Poor Management</div>
                <div class="text-xs text-gray-500 mb-2">Subsistence Farming</div>
                <div class="text-2xl font-extrabold text-red-600">{{ b.adgPoorManagement }} <span class="text-sm font-medium">kg/d</span></div>
              </div>

              <div class="p-6 text-center hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors">
                <div class="w-12 h-12 mx-auto rounded-full bg-amber-50 text-amber-600 flex items-center justify-center mb-3">
                  <mat-icon>trending_flat</mat-icon>
                </div>
                <div class="text-sm font-bold text-gray-900 dark:text-white mb-1">Average Farm</div>
                <div class="text-xs text-gray-500 mb-2">Standard Smallholder</div>
                <div class="text-2xl font-extrabold text-amber-600">{{ b.adgAverageFarm }} <span class="text-sm font-medium">kg/d</span></div>
              </div>

              <div class="p-6 text-center hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors">
                <div class="w-12 h-12 mx-auto rounded-full bg-blue-50 text-blue-600 flex items-center justify-center mb-3">
                  <mat-icon>trending_up</mat-icon>
                </div>
                <div class="text-sm font-bold text-gray-900 dark:text-white mb-1">Good Commercial</div>
                <div class="text-xs text-gray-500 mb-2">Professional Care</div>
                <div class="text-2xl font-extrabold text-blue-600">{{ b.adgGoodCommercialFarm }} <span class="text-sm font-medium">kg/d</span></div>
              </div>

              <div class="p-6 text-center hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors">
                <div class="w-12 h-12 mx-auto rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center mb-3">
                  <mat-icon>rocket_launch</mat-icon>
                </div>
                <div class="text-sm font-bold text-gray-900 dark:text-white mb-1">Intensive Fattening</div>
                <div class="text-xs text-gray-500 mb-2">High Energy Diet</div>
                <div class="text-2xl font-extrabold text-emerald-600">{{ b.adgIntensiveFattening }} <span class="text-sm font-medium">kg/d</span></div>
              </div>
            </div>
          </div>

        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BreedDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly breedService = inject(BreedService);

  breed = signal<BreedDto | null>(null);
  isLoading = signal<boolean>(true);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadBreed(id);
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
