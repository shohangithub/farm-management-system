import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-breed-reference-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-blue-500">menu_book</mat-icon>
            Breed Intelligence Reference
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Standard ADG targets and FCR ratios by breed type</p>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Content -->
      <div class="p-6 overflow-y-auto custom-scrollbar flex-1 space-y-8">
        
        <!-- Indigenous -->
        <section>
          <h3 class="text-sm font-bold text-gray-900 dark:text-white mb-3 flex items-center gap-2">
            <span class="w-2 h-2 rounded-full bg-emerald-500"></span> Indigenous (Native)
          </h3>
          <div class="overflow-x-auto rounded-xl border border-gray-200 dark:border-gray-700">
            <table class="w-full text-sm text-left">
              <thead class="text-xs text-gray-500 bg-gray-50 dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
                <tr>
                  <th class="px-4 py-2 font-bold uppercase tracking-wider">Breed</th>
                  <th class="px-4 py-2 font-bold uppercase tracking-wider">Daily Weight Gain</th>
                  <th class="px-4 py-2 font-bold uppercase tracking-wider">Target FCR</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                <tr>
                  <td class="px-4 py-2 text-gray-500 font-medium">Deshi (Local)</td>
                  <td class="px-4 py-2 text-emerald-600">0.2 - 0.4 kg</td>
                  <td class="px-4 py-2 text-indigo-600">8.0 - 10.0</td>
                </tr>
                <tr>
                  <td class="px-4 py-2 text-gray-500 font-medium">Red Chittagong (RCC)</td>
                  <td class="px-4 py-2 text-emerald-600">0.3 - 0.5 kg</td>
                  <td class="px-4 py-2 text-indigo-600">7.0 - 9.0</td>
                </tr>
                <tr>
                  <td class="px-4 py-2 text-gray-500 font-medium">Pabna</td>
                  <td class="px-4 py-2 text-emerald-600">0.3 - 0.5 kg</td>
                  <td class="px-4 py-2 text-indigo-600">8.0 - 10.0</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Exotic -->
        <section>
          <h3 class="text-sm font-bold text-gray-900 dark:text-white mb-3 flex items-center gap-2">
            <span class="w-2 h-2 rounded-full bg-blue-500"></span> Exotic (Imported)
          </h3>
          <div class="overflow-x-auto rounded-xl border border-gray-200 dark:border-gray-700">
            <table class="w-full text-sm text-left">
              <thead class="text-xs text-gray-500 bg-gray-50 dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
                <tr>
                  <th class="px-4 py-2 font-bold uppercase tracking-wider">Breed</th>
                  <th class="px-4 py-2 font-bold uppercase tracking-wider">Daily Weight Gain</th>
                  <th class="px-4 py-2 font-bold uppercase tracking-wider">Target FCR</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                <tr>
                  <td class="px-4 py-2 text-gray-500 font-medium">Holstein Friesian</td>
                  <td class="px-4 py-2 text-emerald-600">0.6 - 1.0 kg</td>
                  <td class="px-4 py-2 text-indigo-600">6.0 - 8.0</td>
                </tr>
                <tr>
                  <td class="px-4 py-2 text-gray-500 font-medium">Jersey</td>
                  <td class="px-4 py-2 text-emerald-600">0.5 - 0.8 kg</td>
                  <td class="px-4 py-2 text-indigo-600">6.0 - 8.0</td>
                </tr>
                <tr>
                  <td class="px-4 py-2 text-gray-500 font-medium">Brahman</td>
                  <td class="px-4 py-2 text-emerald-600">0.8 - 1.2 kg</td>
                  <td class="px-4 py-2 text-indigo-600">5.0 - 7.0</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Crossbred -->
        <section>
          <h3 class="text-sm font-bold text-gray-900 dark:text-white mb-3 flex items-center gap-2">
            <span class="w-2 h-2 rounded-full bg-purple-500"></span> Crossbred
          </h3>
          <div class="overflow-x-auto rounded-xl border border-gray-200 dark:border-gray-700">
            <table class="w-full text-sm text-left">
              <thead class="text-xs text-gray-500 bg-gray-50 dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
                <tr>
                  <th class="px-4 py-2 font-bold uppercase tracking-wider">Breed</th>
                  <th class="px-4 py-2 font-bold uppercase tracking-wider">Daily Weight Gain</th>
                  <th class="px-4 py-2 font-bold uppercase tracking-wider">Target FCR</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                <tr>
                  <td class="px-4 py-2 text-gray-500 font-medium">Holstein Cross</td>
                  <td class="px-4 py-2 text-emerald-600">0.7 - 1.0 kg</td>
                  <td class="px-4 py-2 text-indigo-600">6.0 - 8.0</td>
                </tr>
                <tr>
                  <td class="px-4 py-2 text-gray-500 font-medium">Brahman Cross</td>
                  <td class="px-4 py-2 text-emerald-600">0.9 - 1.3 kg</td>
                  <td class="px-4 py-2 text-indigo-600">5.0 - 7.0</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <div class="bg-blue-50 dark:bg-blue-900/20 p-4 rounded-xl text-sm text-blue-800 dark:text-blue-300 border border-blue-200 dark:border-blue-800/30">
          <strong class="font-bold">Formula Note:</strong> Projected Weight = Current Weight + (Target ADG × Days). Daily Feed DM (kg) = Target ADG × Breed FCR.
        </div>
      </div>
      
      <!-- Footer -->
      <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-900/50 shrink-0 flex items-center justify-end rounded-b-2xl">
        <button mat-flat-button mat-dialog-close class="!rounded-xl !bg-blue-600 hover:!bg-blue-700 !text-white">
          Got it
        </button>
      </div>
    </div>
  `,
  styles: [`
    .custom-scrollbar::-webkit-scrollbar { width: 6px; }
    .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
    .custom-scrollbar::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.5); border-radius: 20px; }
    .custom-scrollbar:hover::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.8); }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BreedReferenceDialogComponent { }
