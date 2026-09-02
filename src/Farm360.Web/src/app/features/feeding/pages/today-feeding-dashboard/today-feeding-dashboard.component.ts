import { Component, ChangeDetectionStrategy, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FeedingService } from '../../services/feeding.service';
import { DailyFeedingEntry, DailyFeedingEntryStatus } from '../../models/feeding.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { AdjustFeedingEntryDialogComponent, AdjustDialogData } from '../../components/dialogs/adjust-feeding-entry-dialog/adjust-feeding-entry-dialog.component';
import { forkJoin } from 'rxjs';

interface PenGroup {
  penName: string;
  entries: DailyFeedingEntry[];
  totalExpectedKg: number;
  totalActualKg: number;
}

interface ShedGroup {
  shedName: string;
  pens: PenGroup[];
  totalExpectedKg: number;
  totalActualKg: number;
  progressPct: number;
}

@Component({
  selector: 'app-today-feeding-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Today's Feeding Workflow"
      description="Manage and confirm automated feeding instructions across all sheds."
      breadcrumbActiveNode="Today's Feeding">
      <div actions>
        <button (click)="loadEntries()"
          class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">refresh</mat-icon> Refresh Data
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative min-h-[500px]">
      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Dashboard Header Stats -->
      <div class="p-6 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-900/30 grid grid-cols-2 md:grid-cols-4 gap-6">
        <div>
          <div class="text-xs uppercase font-bold text-gray-500 tracking-wider">Total Required Feed</div>
          <div class="text-3xl font-extrabold text-emerald-600 dark:text-emerald-400 mt-1">{{ totalExpectedKg() | number:'1.2-2' }} <span class="text-sm">kg</span></div>
        </div>
        <div>
          <div class="text-xs uppercase font-bold text-gray-500 tracking-wider">Feed Consumed</div>
          <div class="text-3xl font-extrabold text-blue-600 dark:text-blue-400 mt-1">{{ totalActualKg() | number:'1.2-2' }} <span class="text-sm">kg</span></div>
        </div>
        <div>
          <div class="text-xs uppercase font-bold text-gray-500 tracking-wider">Total Animals</div>
          <div class="text-3xl font-extrabold text-gray-900 dark:text-white mt-1">{{ allEntries().length }}</div>
        </div>
        <div>
          <div class="text-xs uppercase font-bold text-gray-500 tracking-wider">Workflow Progress</div>
          <div class="mt-2 w-full bg-gray-200 rounded-full h-2.5 dark:bg-gray-700">
            <div class="bg-emerald-600 h-2.5 rounded-full" [style.width]="progressPct() + '%'"></div>
          </div>
          <div class="text-sm font-semibold text-gray-700 mt-1">{{ progressPct() | number:'1.0-0' }}% Completed</div>
        </div>
      </div>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!isLoading() && allEntries().length === 0"
        icon="check_circle"
        title="No Feeding Required"
        description="There are no pending feeding entries for today. Check your feeding plans and automated schedules."
        actionLabel="Refresh List"
        (action)="loadEntries()">
      </app-empty-state>

      <!-- Grouped List -->
      <div *ngIf="!isLoading() && allEntries().length > 0" class="p-6 space-y-8">
        
        <div *ngFor="let shed of groupedEntries()" class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 overflow-hidden shadow-sm">
          
          <!-- Shed Header -->
          <div class="px-5 py-4 bg-gray-50 dark:bg-gray-900 border-b border-gray-200 dark:border-gray-700 flex justify-between items-center">
            <div class="flex items-center gap-3">
              <mat-icon class="text-emerald-600 dark:text-emerald-400">home_work</mat-icon>
              <h2 class="text-lg font-bold text-gray-900 dark:text-white">{{ shed.shedName }}</h2>
            </div>
            <div class="text-sm font-semibold text-gray-600 dark:text-gray-400">
              <span class="text-emerald-600 dark:text-emerald-400">{{ shed.totalActualKg | number:'1.2-2' }}kg</span> / {{ shed.totalExpectedKg | number:'1.2-2' }}kg Confirmed
            </div>
          </div>

          <!-- Pens -->
          <div class="divide-y divide-gray-100 dark:divide-gray-800">
            <div *ngFor="let pen of shed.pens" class="p-5">
              
              <div class="flex justify-between items-center mb-4">
                <div class="flex items-center gap-2">
                  <mat-icon class="text-gray-400 !w-5 !h-5 !text-[20px]">fence</mat-icon>
                  <h3 class="text-md font-bold text-gray-800 dark:text-gray-200">{{ pen.penName }}</h3>
                </div>
                <button (click)="confirmPen(pen)" 
                  class="px-3 py-1.5 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1">
                  <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">done_all</mat-icon> Confirm All
                </button>
              </div>

              <!-- Entries Grid -->
              <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                <div *ngFor="let entry of pen.entries" class="border border-gray-200 dark:border-gray-700 rounded-xl p-4 bg-gray-50/50 dark:bg-gray-900/50 relative overflow-hidden group">
                  
                  <!-- Status Indicator -->
                  <div class="absolute top-0 right-0 w-16 h-16 pointer-events-none">
                    <div class="absolute transform rotate-45 text-[10px] font-bold text-white text-center w-24 py-1 right-[-24px] top-[16px]"
                         [ngClass]="{
                           'bg-gray-400': entry.status === 'Pending',
                           'bg-emerald-500 shadow-emerald-500/50 shadow-sm': entry.status === 'Confirmed',
                           'bg-orange-500 shadow-orange-500/50 shadow-sm': entry.status === 'Adjusted',
                           'bg-red-500 shadow-red-500/50 shadow-sm': entry.status === 'Skipped'
                         }">
                      {{ entry.status }}
                    </div>
                  </div>

                  <div class="flex items-center gap-2 mb-2">
                    <mat-icon class="text-gray-500 !w-4 !h-4 !text-[16px]">pets</mat-icon>
                    <span class="font-bold text-gray-900 dark:text-white text-sm">{{ entry.animalTag }}</span>
                  </div>

                  <div class="text-2xl font-extrabold text-gray-900 dark:text-white mb-3">
                    {{ (entry.actualKg !== null && entry.actualKg !== undefined ? entry.actualKg : entry.expectedKg) | number:'1.2-2' }} <span class="text-sm font-normal text-gray-500">kg</span>
                  </div>

                  <!-- Actions -->
                  <div class="flex justify-between items-center mt-3 pt-3 border-t border-gray-200 dark:border-gray-700/50" *ngIf="entry.status === 'Pending'">
                    <button (click)="confirmEntry(entry)"
                      class="text-emerald-600 hover:bg-emerald-50 px-2 py-1 rounded font-semibold text-xs flex items-center transition-colors">
                      <mat-icon class="!w-4 !h-4 !text-[16px] mr-1">check</mat-icon> Confirm
                    </button>
                    
                    <button mat-icon-button [matMenuTriggerFor]="menu" class="!w-6 !h-6 flex items-center justify-center text-gray-400 hover:text-gray-600">
                      <mat-icon class="!w-5 !h-5 !text-[20px]">more_vert</mat-icon>
                    </button>
                    <mat-menu #menu="matMenu" class="!rounded-xl shadow-xl">
                      <button mat-menu-item (click)="openAdjustDialog(entry, 'adjust')">
                        <mat-icon class="text-orange-500">tune</mat-icon>
                        <span>Adjust Amount</span>
                      </button>
                      <button mat-menu-item (click)="openAdjustDialog(entry, 'skip')">
                        <mat-icon class="text-red-500">block</mat-icon>
                        <span>Skip Feeding</span>
                      </button>
                    </mat-menu>
                  </div>
                  
                  <div class="text-xs text-gray-500 italic mt-3 pt-3 border-t border-gray-200 dark:border-gray-700/50" *ngIf="entry.status !== 'Pending'">
                    Processed. {{ entry.notes ? '"' + entry.notes + '"' : '' }}
                  </div>

                </div>
              </div>

            </div>
          </div>
        </div>

      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TodayFeedingDashboardComponent implements OnInit {
  private readonly feedingService = inject(FeedingService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly isLoading = signal(true);
  readonly allEntries = signal<DailyFeedingEntry[]>([]);

  // Computed signals
  readonly totalExpectedKg = computed(() => {
    return this.allEntries().reduce((sum, e) => sum + e.expectedKg, 0);
  });

  readonly totalActualKg = computed(() => {
    return this.allEntries().reduce((sum, e) => sum + (e.actualKg ?? 0), 0);
  });

  readonly progressPct = computed(() => {
    const entries = this.allEntries();
    if (entries.length === 0) return 0;
    const completed = entries.filter(e => e.status !== 'Pending').length;
    return (completed / entries.length) * 100;
  });

  readonly groupedEntries = computed<ShedGroup[]>(() => {
    const entries = this.allEntries();
    const shedMap = new Map<string, ShedGroup>();

    entries.forEach(entry => {
      const shedName = entry.shedName || 'Unassigned Shed';
      const penName = entry.penName || 'Unassigned Pen';

      if (!shedMap.has(shedName)) {
        shedMap.set(shedName, {
          shedName,
          pens: [],
          totalExpectedKg: 0,
          totalActualKg: 0,
          progressPct: 0
        });
      }

      const shedGroup = shedMap.get(shedName)!;
      shedGroup.totalExpectedKg += entry.expectedKg;
      shedGroup.totalActualKg += entry.actualKg ?? 0;

      let penGroup = shedGroup.pens.find(p => p.penName === penName);
      if (!penGroup) {
        penGroup = { penName, entries: [], totalExpectedKg: 0, totalActualKg: 0 };
        shedGroup.pens.push(penGroup);
      }

      penGroup.totalExpectedKg += entry.expectedKg;
      penGroup.totalActualKg += entry.actualKg ?? 0;
      penGroup.entries.push(entry);
    });

    return Array.from(shedMap.values());
  });

  // Placeholder tenant/farm setup
  private readonly farmId = '00000000-0000-0000-0000-000000000000';

  ngOnInit(): void {
    this.loadEntries();
  }

  loadEntries(): void {
    this.isLoading.set(true);
    // Use today's date for standard local flow
    const today = new Date().toISOString().split('T')[0];
    this.feedingService.getTodayEntries(this.farmId, today).subscribe({
      next: (res) => {
        this.allEntries.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  confirmEntry(entry: DailyFeedingEntry): void {
    this.feedingService.confirmEntry(entry.id, entry.expectedKg).subscribe({
      next: () => {
        this.snackBar.open(`Confirmed feed for ${entry.animalTag}`, 'Close', { duration: 3000 });
        this.loadEntries(); // Refresh to ensure data sync
      },
      error: (err) => {
        this.snackBar.open(err.error?.detail || 'Failed to confirm entry', 'Close', { duration: 5000 });
      }
    });
  }

  openAdjustDialog(entry: DailyFeedingEntry, action: 'adjust' | 'skip'): void {
    const dialogRef = this.dialog.open(AdjustFeedingEntryDialogComponent, { disableClose: true,
      width: '500px',
      data: { entry, action }
    });

    dialogRef.afterClosed().subscribe(res => {
      if (res) this.loadEntries();
    });
  }

  confirmPen(pen: PenGroup): void {
    // Only confirm pending ones
    const pendingEntries = pen.entries.filter(e => e.status === 'Pending');
    
    if (pendingEntries.length === 0) {
      this.snackBar.open(`All entries in ${pen.penName} are already processed.`, 'Close', { duration: 3000 });
      return;
    }

    // In a real production app, we would have a batch API endpoint `confirmBatch(ids[])`.
    // Since we don't have a batch API in Phase 4, we use forkJoin to parallelize HTTP calls.
    const observables = pendingEntries.map(e => this.feedingService.confirmEntry(e.id, e.expectedKg));
    
    this.isLoading.set(true);
    forkJoin(observables).subscribe({
      next: () => {
        this.snackBar.open(`Confirmed ${pendingEntries.length} entries for ${pen.penName}`, 'Close', { duration: 3000 });
        this.loadEntries();
      },
      error: (err) => {
        this.snackBar.open('Some confirmations failed', 'Close', { duration: 5000 });
        this.loadEntries();
      }
    });
  }
}

