import { Component, ChangeDetectionStrategy, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { BreedService } from '../../../services/breed.service';
import { BreedDto, CreateBreedRequest } from '../../../models/breed.models';
import { finalize } from 'rxjs';
import { parseApiError } from '../../../../../core/utils/error-parser';
import { BreedReferenceDialogComponent } from '../breed-reference-dialog/breed-reference-dialog.component';

@Component({
  selector: 'app-breed-setup-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-gray-500">pets</mat-icon>
            {{ isEdit() ? 'Edit Breed' : 'Add New Breed' }}
          </h2>
          <div class="flex items-center gap-3 mt-1">
            <p class="text-xs text-gray-500 dark:text-gray-400 m-0">Define intelligence targets</p>
            <button type="button" (click)="openReference()" class="text-xs text-blue-600 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-300 font-semibold flex items-center gap-1 transition-colors bg-blue-50 hover:bg-blue-100 dark:bg-blue-900/30 dark:hover:bg-blue-900/50 px-2 py-0.5 rounded-md">
              <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">menu_book</mat-icon> View Reference Guide
            </button>
          </div>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Content -->
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden">
        
        <!-- Error State -->
        <div *ngIf="error()" class="mx-6 mt-4 p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-lg text-sm whitespace-pre-wrap flex items-start gap-2">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
          <span>{{ error() }}</span>
        </div>

        <div class="p-6 space-y-4 overflow-y-auto custom-scrollbar flex-1">
          
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Breed Name <span class="text-red-500">*</span></label>
              <input type="text" formControlName="name" placeholder="e.g. Shahiwal"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>
            
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Category <span class="text-red-500">*</span></label>
              <select formControlName="category"
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                <option value="Indigenous">Indigenous</option>
                <option value="Exotic">Exotic</option>
                <option value="Crossbred">Crossbred</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Main Purpose <span class="text-red-500">*</span></label>
              <select formControlName="mainPurpose"
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                <option value="Beef">Beef</option>
                <option value="Dairy">Dairy</option>
                <option value="Dual-purpose">Dual-purpose</option>
              </select>
            </div>
            
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Origin</label>
              <input type="text" formControlName="origin" placeholder="e.g. Punjab, Pakistan"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Description</label>
            <textarea formControlName="description" rows="2"
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow resize-none"></textarea>
          </div>

          <div class="pt-4 border-t border-gray-100 dark:border-gray-800">
            <h3 class="text-xs font-bold text-gray-900 dark:text-white uppercase tracking-wider mb-4">Growth & Intelligence Targets</h3>
            
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
              <div class="space-y-1.5">
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Standard ADG (Min - Max kg)</label>
                <div class="flex items-center gap-2">
                  <input type="number" formControlName="standardAdgMin"
                         class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                  <span class="text-gray-400 font-bold">-</span>
                  <input type="number" formControlName="standardAdgMax"
                         class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                </div>
              </div>
              
              <div class="space-y-1.5">
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Feed Conv. Ratio (FCR) (Min - Max)</label>
                <div class="flex items-center gap-2">
                  <input type="number" formControlName="fcrMin"
                         class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                  <span class="text-gray-400 font-bold">-</span>
                  <input type="number" formControlName="fcrMax"
                         class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                </div>
              </div>
            </div>
            
            <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
               <div class="space-y-1.5">
                <label class="block text-[10px] font-bold uppercase tracking-wider text-gray-500">ADG (Poor)</label>
                <input type="number" formControlName="adgPoorManagement"
                       class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
              </div>
              <div class="space-y-1.5">
                <label class="block text-[10px] font-bold uppercase tracking-wider text-gray-500">ADG (Average)</label>
                <input type="number" formControlName="adgAverageFarm"
                       class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
              </div>
              <div class="space-y-1.5">
                <label class="block text-[10px] font-bold uppercase tracking-wider text-gray-500">ADG (Good)</label>
                <input type="number" formControlName="adgGoodCommercialFarm"
                       class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
              </div>
              <div class="space-y-1.5">
                <label class="block text-[10px] font-bold uppercase tracking-wider text-gray-500">ADG (Intensive)</label>
                <input type="number" formControlName="adgIntensiveFattening"
                       class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
              </div>
            </div>
          </div>
        </div>

        <!-- Footer Actions -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
          <button type="button" mat-dialog-close [disabled]="isLoading()"
            class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || isLoading()"
                  class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
            <mat-icon *ngIf="isLoading()" class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon>
            <span>{{ isLoading() ? (isEdit() ? 'Saving...' : 'Creating...') : (isEdit() ? 'Save Changes' : 'Create Breed') }}</span>
          </button>
        </div>
      </form>
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
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BreedSetupDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly breedService = inject(BreedService);
  private readonly dialogRef = inject(MatDialogRef<BreedSetupDialogComponent>);
  public readonly data = inject<{ breed?: BreedDto }>(MAT_DIALOG_DATA);

  isLoading = signal<boolean>(false);
  isEdit = signal<boolean>(!!this.data?.breed);
  error = signal<string>('');

  form: FormGroup = this.fb.group({
    name: [this.data?.breed?.name || '', Validators.required],
    description: [this.data?.breed?.description || ''],
    category: [this.data?.breed?.category || 'Indigenous', Validators.required],
    origin: [this.data?.breed?.origin || ''],
    mainPurpose: [this.data?.breed?.mainPurpose || 'Beef', Validators.required],
    bestFor: [this.data?.breed?.bestFor || ''],
    adgPoorManagement: [this.data?.breed?.adgPoorManagement ?? 0.2],
    adgAverageFarm: [this.data?.breed?.adgAverageFarm ?? 0.4],
    adgGoodCommercialFarm: [this.data?.breed?.adgGoodCommercialFarm ?? 0.6],
    adgIntensiveFattening: [this.data?.breed?.adgIntensiveFattening ?? 0.8],
    standardAdgMin: [this.data?.breed?.standardAdgMin ?? 0.4],
    standardAdgMax: [this.data?.breed?.standardAdgMax ?? 0.8],
    fcrMin: [this.data?.breed?.fcrMin ?? 5],
    fcrMax: [this.data?.breed?.fcrMax ?? 8],
    milkYieldMinLiters: [this.data?.breed?.milkYieldMinLiters ?? 0],
    milkYieldMaxLiters: [this.data?.breed?.milkYieldMaxLiters ?? 0],
    fatPercentageMin: [this.data?.breed?.fatPercentageMin ?? 0],
    fatPercentageMax: [this.data?.breed?.fatPercentageMax ?? 0]
  });

  private readonly dialog = inject(MatDialog);

  ngOnInit(): void {
    // Data initialized directly in form builder
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isLoading.set(true);
    this.error.set('');
    const request = this.form.value as CreateBreedRequest;

    // Payload sanitization rule
    for (const key in request) {
      if (typeof (request as any)[key] === 'string' && (request as any)[key] === '') {
        (request as any)[key] = null;
      }
    }

    const obs$ = this.isEdit()
      ? this.breedService.updateBreed(this.data.breed!.id, request)
      : this.breedService.createBreed(request);

    obs$.pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: () => this.dialogRef.close(true),
        error: (err) => {
          console.error('Failed to save breed', err);
          const parsedMsg = parseApiError(err, 'Failed to save breed.');
          this.error.set(parsedMsg);
        }
      });
  }

  openReference(): void {
    if (this.dialog.openDialogs.some(d => d.componentInstance instanceof BreedReferenceDialogComponent)) return;
    
    this.dialog.open(BreedReferenceDialogComponent, { disableClose: true,
      width: '700px',
      maxWidth: '95vw',
      panelClass: ['!rounded-2xl', '!bg-transparent']
    });
  }
}
