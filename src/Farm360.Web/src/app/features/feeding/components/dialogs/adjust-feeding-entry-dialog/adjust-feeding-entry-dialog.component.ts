import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FeedingService } from '../../../services/feeding.service';
import { DailyFeedingEntry } from '../../../models/feeding.models';

export interface AdjustDialogData {
  entry: DailyFeedingEntry;
  action: 'adjust' | 'skip';
}

@Component({
  selector: 'app-adjust-feeding-entry-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule
  ],
  template: `
    <div class="p-0 flex flex-col h-full bg-gray-50 dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl">
      <!-- Header -->
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-b border-gray-100 dark:border-gray-700 flex justify-between items-center shadow-sm shrink-0">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl text-white flex items-center justify-center shadow-md"
               [ngClass]="data.action === 'adjust' ? 'bg-gradient-to-br from-amber-500 to-orange-600 shadow-orange-500/20' : 'bg-gradient-to-br from-red-500 to-rose-600 shadow-red-500/20'">
            <mat-icon class="!w-5 !h-5 !text-[20px]">{{ data.action === 'adjust' ? 'tune' : 'block' }}</mat-icon>
          </div>
          <div>
            <h2 class="text-lg font-bold text-gray-900 dark:text-white leading-tight m-0">
              {{ data.action === 'adjust' ? 'Adjust' : 'Skip' }} Feed Entry
            </h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Animal: <span class="font-bold text-gray-800 dark:text-gray-200">{{ data.entry.animalTag }}</span></p>
          </div>
        </div>
        <button mat-icon-button (click)="close()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 rounded-full transition-colors">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Form Content -->
      <div class="p-6 flex-1">
        <div class="bg-blue-50 dark:bg-blue-950/40 text-blue-800 dark:text-blue-200 border border-blue-200 dark:border-blue-800 rounded-xl p-4 mb-6 flex gap-3">
          <mat-icon class="text-blue-500 mt-0.5">info</mat-icon>
          <div>
            <p class="text-sm font-semibold">Expected Amount: {{ data.entry.expectedKg }} kg</p>
            <p class="text-xs mt-1" *ngIf="data.action === 'adjust'">Please enter the actual amount provided and the reason for adjustment.</p>
            <p class="text-xs mt-1" *ngIf="data.action === 'skip'">Please provide a reason why this feeding is being skipped entirely.</p>
          </div>
        </div>

        <div class="space-y-4">
          <div *ngIf="data.action === 'adjust'">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">
              Actual Quantity (kg) <span class="text-red-500">*</span>
            </label>
            <input [(ngModel)]="actualKg" type="number" step="0.01" min="0" placeholder="e.g. 5.5"
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-sm font-medium placeholder-gray-400 dark:placeholder-gray-500" />
          </div>

          <div>
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300 mb-1.5">
              Reason / Notes <span class="text-red-500">*</span>
            </label>
            <textarea [(ngModel)]="notes" rows="3" placeholder="Provide a detailed reason..."
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-sm font-medium placeholder-gray-400 dark:placeholder-gray-500"></textarea>
          </div>
        </div>
      </div>

      <!-- Footer Actions -->
      <div class="px-6 py-4 bg-white dark:bg-gray-800 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3 shadow-sm shrink-0 mt-auto">
        <button type="button" (click)="close()" [disabled]="isSubmitting()"
          class="px-5 py-2.5 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-xl hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors">
          Cancel
        </button>
        <button type="button" (click)="submit()" [disabled]="!isValid() || isSubmitting()"
          class="px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50 disabled:cursor-not-allowed rounded-xl transition-colors shadow-sm inline-flex items-center justify-center min-w-[120px]"
          [ngClass]="data.action === 'adjust' ? 'bg-orange-600 hover:bg-orange-700' : 'bg-red-600 hover:bg-red-700'">
          <mat-icon *ngIf="isSubmitting()" class="animate-spin !w-5 !h-5 !text-[20px] mr-2">refresh</mat-icon>
          {{ data.action === 'adjust' ? 'Submit Adjustment' : 'Skip Entry' }}
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AdjustFeedingEntryDialogComponent {
  readonly data = inject<AdjustDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<AdjustFeedingEntryDialogComponent>);
  private readonly feedingService = inject(FeedingService);
  private readonly snackBar = inject(MatSnackBar);

  actualKg = this.data.entry.expectedKg;
  notes = '';
  
  readonly isSubmitting = signal(false);

  isValid(): boolean {
    if (this.data.action === 'adjust') {
      return this.actualKg >= 0 && this.notes.trim().length > 0;
    }
    return this.notes.trim().length > 0;
  }

  close(): void {
    this.dialogRef.close();
  }

  submit(): void {
    if (!this.isValid()) return;

    this.isSubmitting.set(true);

    if (this.data.action === 'adjust') {
      this.feedingService.adjustEntry(this.data.entry.id, this.actualKg, this.notes).subscribe({
        next: () => {
          this.snackBar.open('Entry adjusted successfully', 'Close', { duration: 3000 });
          this.dialogRef.close(true);
        },
        error: (err) => {
          this.snackBar.open(err.error?.detail || 'Failed to adjust entry', 'Close', { duration: 5000 });
          this.isSubmitting.set(false);
        }
      });
    } else {
      this.feedingService.skipEntry(this.data.entry.id, this.notes).subscribe({
        next: () => {
          this.snackBar.open('Entry skipped successfully', 'Close', { duration: 3000 });
          this.dialogRef.close(true);
        },
        error: (err) => {
          this.snackBar.open(err.error?.detail || 'Failed to skip entry', 'Close', { duration: 5000 });
          this.isSubmitting.set(false);
        }
      });
    }
  }
}
