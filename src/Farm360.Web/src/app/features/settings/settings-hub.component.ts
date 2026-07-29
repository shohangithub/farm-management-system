import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-settings-hub',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6 animate-fade-in-up p-6">
      <div class="mb-8">
        <h2 class="text-2xl font-bold text-gray-900 dark:text-white tracking-tight">System Settings & Setup</h2>
        <p class="text-gray-500 dark:text-gray-400 mt-1">Manage organizations, master data, and reference tables.</p>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        
        <!-- Organizations -->
        <a routerLink="/organizations" class="block bg-white dark:bg-gray-800 rounded-2xl p-6 shadow-sm border border-gray-100 dark:border-gray-700 hover:shadow-md hover:border-emerald-500/30 transition-all duration-300 group cursor-pointer relative overflow-hidden">
          <div class="absolute -right-4 -bottom-4 opacity-5 group-hover:opacity-10 transition-opacity duration-300">
            <mat-icon class="!w-24 !h-24 !text-[96px]">business</mat-icon>
          </div>
          <div class="flex items-center gap-4 mb-4">
            <div class="w-12 h-12 rounded-xl bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400 flex items-center justify-center">
              <mat-icon>business</mat-icon>
            </div>
            <h3 class="text-lg font-bold text-gray-900 dark:text-white">Organizations</h3>
          </div>
          <p class="text-sm text-gray-500 dark:text-gray-400 relative z-10">Manage multi-tenant organizations, branches, and farm setups.</p>
        </a>

        <!-- Breeds -->
        <a routerLink="/livestock/breeds" class="block bg-white dark:bg-gray-800 rounded-2xl p-6 shadow-sm border border-gray-100 dark:border-gray-700 hover:shadow-md hover:border-emerald-500/30 transition-all duration-300 group cursor-pointer relative overflow-hidden">
          <div class="absolute -right-4 -bottom-4 opacity-5 group-hover:opacity-10 transition-opacity duration-300">
            <mat-icon class="!w-24 !h-24 !text-[96px]">pets</mat-icon>
          </div>
          <div class="flex items-center gap-4 mb-4">
            <div class="w-12 h-12 rounded-xl bg-emerald-50 dark:bg-emerald-900/20 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
              <mat-icon>pets</mat-icon>
            </div>
            <h3 class="text-lg font-bold text-gray-900 dark:text-white">Breeds Setup</h3>
          </div>
          <p class="text-sm text-gray-500 dark:text-gray-400 relative z-10">Configure animal breeds, genetic traits, and predictive growth metrics.</p>
        </a>

        <!-- Batches -->
        <a routerLink="/livestock/batches" class="block bg-white dark:bg-gray-800 rounded-2xl p-6 shadow-sm border border-gray-100 dark:border-gray-700 hover:shadow-md hover:border-emerald-500/30 transition-all duration-300 group cursor-pointer relative overflow-hidden">
          <div class="absolute -right-4 -bottom-4 opacity-5 group-hover:opacity-10 transition-opacity duration-300">
            <mat-icon class="!w-24 !h-24 !text-[96px]">group_work</mat-icon>
          </div>
          <div class="flex items-center gap-4 mb-4">
            <div class="w-12 h-12 rounded-xl bg-purple-50 dark:bg-purple-900/20 text-purple-600 dark:text-purple-400 flex items-center justify-center">
              <mat-icon>group_work</mat-icon>
            </div>
            <h3 class="text-lg font-bold text-gray-900 dark:text-white">Batches & Groups</h3>
          </div>
          <p class="text-sm text-gray-500 dark:text-gray-400 relative z-10">Group livestock into manageable batches for bulk feeding and health operations.</p>
        </a>

        <!-- Master Data -->
        <a routerLink="/settings/master-data" class="block bg-white dark:bg-gray-800 rounded-2xl p-6 shadow-sm border border-gray-100 dark:border-gray-700 hover:shadow-md hover:border-emerald-500/30 transition-all duration-300 group cursor-pointer relative overflow-hidden">
          <div class="absolute -right-4 -bottom-4 opacity-5 group-hover:opacity-10 transition-opacity duration-300">
            <mat-icon class="!w-24 !h-24 !text-[96px]">settings_applications</mat-icon>
          </div>
          <div class="flex items-center gap-4 mb-4">
            <div class="w-12 h-12 rounded-xl bg-orange-50 dark:bg-orange-900/20 text-orange-600 dark:text-orange-400 flex items-center justify-center">
              <mat-icon>settings_applications</mat-icon>
            </div>
            <h3 class="text-lg font-bold text-gray-900 dark:text-white">General Master Data</h3>
          </div>
          <p class="text-sm text-gray-500 dark:text-gray-400 relative z-10">Configure geographic locations, user permissions and global attributes.</p>
        </a>

      </div>
    </div>
  `
})
export class SettingsHubComponent { }
