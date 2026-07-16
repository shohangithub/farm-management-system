import {
  Component, OnInit, OnDestroy, inject, signal, ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule }        from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil }  from 'rxjs';
import { AnimalService }       from '../../services/animal.service';
import {
  AnimalDto, AnimalStatus, AnimalSex,
  SPECIES_LABELS, STATUS_LABELS, STATUS_BADGE_CLASS, SEX_LABELS,
} from '../../models/animal.models';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { DataTableComponent, TableColumn } from '../../../../shared/components/data-table/data-table.component';

@Component({
  selector: 'app-animal-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, RouterModule, 
    PageHeaderComponent, LoadingComponent, EmptyStateComponent, DataTableComponent,
    MatCardModule, MatButtonModule, MatIconModule, MatDialogModule, MatDividerModule
  ],
  template: `
    <app-loading *ngIf="loading()"></app-loading>

    <app-empty-state 
      *ngIf="!loading() && error()" 
      icon="error_outline" 
      title="Animal not found" 
      [description]="error() || 'The requested animal could not be loaded.'"
      actionLabel="Back to List" 
      (action)="goBack()">
    </app-empty-state>

    <ng-container *ngIf="!loading() && animal() as a">
      <app-page-header 
        [title]="a.tagId" 
        [description]="speciesLabel(a.species) + ' · ' + a.breedName + ' · ' + sexLabel(a.sex)"
        primaryActionLabel="Record Weight"
        primaryActionIcon="add"
        (primaryAction)="onRecordWeight(a)">
      </app-page-header>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
        
        <!-- Identity Card -->
        <mat-card class="col-span-1 md:col-span-2 !bg-white dark:!bg-gray-800 !shadow-sm !rounded-xl border border-gray-200 dark:border-gray-700">
          <mat-card-header class="!pb-4 !pt-4 !border-b border-gray-100 dark:border-gray-800">
            <mat-card-title class="!text-lg !font-bold">Animal Profile</mat-card-title>
            <div class="flex-1"></div>
            <div class="flex gap-2">
              <button mat-stroked-button color="warn" *ngIf="a.status === AnimalStatus.Active" (click)="onQuarantine(a)">Quarantine</button>
              <button mat-flat-button color="primary" *ngIf="a.status === AnimalStatus.Quarantined" (click)="onRelease(a)">Release</button>
              <button mat-flat-button color="accent" *ngIf="a.status === AnimalStatus.Active" (click)="onSell(a)">Sell</button>
            </div>
          </mat-card-header>
          <mat-card-content class="!p-0">
            <div class="flex flex-col sm:flex-row">
              <div class="w-full sm:w-1/3 p-4 flex justify-center items-start border-b sm:border-b-0 sm:border-r border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-900/50">
                <img *ngIf="a.primaryPhotoUrl" [src]="a.primaryPhotoUrl" class="w-full max-w-[200px] rounded-lg shadow-sm object-cover aspect-square" />
                <div *ngIf="!a.primaryPhotoUrl" class="w-full max-w-[200px] aspect-square flex items-center justify-center text-6xl opacity-30 bg-gray-200 dark:bg-gray-800 rounded-lg">
                  {{ speciesEmoji(a.species) }}
                </div>
              </div>
              <div class="w-full sm:w-2/3 p-4">
                <dl class="grid grid-cols-1 sm:grid-cols-2 gap-x-4 gap-y-4">
                  <div>
                    <dt class="text-xs font-semibold text-gray-500 uppercase">Tag ID</dt>
                    <dd class="mt-1 text-sm text-gray-900 dark:text-white font-medium">{{ a.tagId }}</dd>
                  </div>
                  <div>
                    <dt class="text-xs font-semibold text-gray-500 uppercase">Status</dt>
                    <dd class="mt-1 text-sm">
                      <span class="px-2 py-1 text-xs font-medium rounded-full bg-gray-100 text-gray-800">{{ statusLabel(a.status) }}</span>
                    </dd>
                  </div>
                  <div>
                    <dt class="text-xs font-semibold text-gray-500 uppercase">Species & Breed</dt>
                    <dd class="mt-1 text-sm text-gray-900 dark:text-white">{{ speciesLabel(a.species) }} / {{ a.breedName }}</dd>
                  </div>
                  <div>
                    <dt class="text-xs font-semibold text-gray-500 uppercase">Date of Birth</dt>
                    <dd class="mt-1 text-sm text-gray-900 dark:text-white">{{ a.dateOfBirth | date:'mediumDate' }} ({{ ageLabel(a.dateOfBirth) }})</dd>
                  </div>
                  <div>
                    <dt class="text-xs font-semibold text-gray-500 uppercase">Tag Type</dt>
                    <dd class="mt-1 text-sm text-gray-900 dark:text-white">{{ tagTypeLabel(a.tagType) }}</dd>
                  </div>
                  <div *ngIf="a.acquisitionPriceBdt">
                    <dt class="text-xs font-semibold text-gray-500 uppercase">Purchase Price</dt>
                    <dd class="mt-1 text-sm text-gray-900 dark:text-white">৳ {{ a.acquisitionPriceBdt | number }}</dd>
                  </div>
                </dl>
              </div>
            </div>
          </mat-card-content>
        </mat-card>

        <!-- KPI Column -->
        <div class="col-span-1 flex flex-col gap-4">
          <mat-card class="!bg-white dark:!bg-gray-800 !shadow-sm !rounded-xl border border-gray-200 dark:border-gray-700">
            <mat-card-content class="!p-4 flex items-center justify-between">
              <div>
                <p class="text-sm font-medium text-gray-500">Latest Weight</p>
                <p class="text-2xl font-bold text-green-600">{{ a.latestWeightKg ? (a.latestWeightKg | number:'1.1-1') + ' kg' : '—' }}</p>
                <p class="text-xs text-gray-400 mt-1" *ngIf="a.latestWeightDate">as of {{ a.latestWeightDate | date:'mediumDate' }}</p>
              </div>
              <mat-icon class="text-4xl text-gray-200 dark:text-gray-700">scale</mat-icon>
            </mat-card-content>
          </mat-card>

          <mat-card class="!bg-white dark:!bg-gray-800 !shadow-sm !rounded-xl border border-gray-200 dark:border-gray-700">
            <mat-card-content class="!p-4 flex items-center justify-between">
              <div>
                <p class="text-sm font-medium text-gray-500">Avg Daily Gain</p>
                <p class="text-2xl font-bold" [class.text-green-600]="(a.adgKgPerDay ?? 0) > 0">{{ a.adgKgPerDay ? (a.adgKgPerDay | number:'1.3-3') + ' kg/d' : '—' }}</p>
              </div>
              <mat-icon class="text-4xl text-gray-200 dark:text-gray-700">trending_up</mat-icon>
            </mat-card-content>
          </mat-card>
        </div>
      </div>

      <!-- Weight History Table -->
      <mat-card class="mb-6 !bg-white dark:!bg-gray-800 !shadow-sm !rounded-xl border border-gray-200 dark:border-gray-700">
        <mat-card-header class="!pb-4 !pt-4 !border-b border-gray-100 dark:border-gray-800">
          <mat-card-title class="!text-lg !font-bold">Weight History</mat-card-title>
        </mat-card-header>
        
        <app-empty-state 
          *ngIf="a.weightRecords.length === 0" 
          icon="scale" 
          title="No weight records yet" 
          description="Record the first weight measurement."
          actionLabel="Record Weight" 
          (action)="onRecordWeight(a)">
        </app-empty-state>

        <mat-card-content class="!p-4" *ngIf="a.weightRecords.length > 0">
          <app-data-table 
            [data]="sortedWeights(a)" 
            [columns]="weightColumns" 
            [displayedColumns]="['date', 'weight', 'recordedAt', 'notes']">
          </app-data-table>
        </mat-card-content>
      </mat-card>

      <!-- Photos -->
      <mat-card class="!bg-white dark:!bg-gray-800 !shadow-sm !rounded-xl border border-gray-200 dark:border-gray-700">
        <mat-card-header class="!pb-4 !pt-4 !border-b border-gray-100 dark:border-gray-800">
          <mat-card-title class="!text-lg !font-bold">Photos</mat-card-title>
        </mat-card-header>
        
        <app-empty-state 
          *ngIf="a.photos.length === 0" 
          icon="add_a_photo" 
          title="No photos yet" 
          description="Add a photo to the animal profile.">
        </app-empty-state>

        <mat-card-content class="!p-4 flex flex-wrap gap-4" *ngIf="a.photos.length > 0">
          <div *ngFor="let p of a.photos" class="relative rounded-lg overflow-hidden border-2" [class.border-green-500]="p.isPrimary" [class.border-transparent]="!p.isPrimary">
            <img [src]="p.photoUrl" [alt]="p.caption ?? 'Photo'" class="w-32 h-32 object-cover" />
            <div *ngIf="p.isPrimary" class="absolute bottom-1 left-1 text-[10px] font-bold bg-green-600 text-white px-1.5 py-0.5 rounded">Primary</div>
          </div>
        </mat-card-content>
      </mat-card>

    </ng-container>
  `
})
export class AnimalDetailComponent implements OnInit, OnDestroy {
  private readonly svc     = inject(AnimalService);
  private readonly route   = inject(ActivatedRoute);
  private readonly destroy$ = new Subject<void>();

  readonly loading = signal(true);
  readonly error   = signal<string | null>(null);
  readonly animal  = signal<AnimalDto | null>(null);

  readonly AnimalStatus = AnimalStatus;

  private dialog = inject(MatDialog);
  private router = inject(Router);

  weightColumns: TableColumn[] = [
    { def: 'date', header: 'Date', cell: (e: any) => new Date(e.recordedDate).toLocaleDateString() },
    { def: 'weight', header: 'Weight (kg)', cell: (e: any) => `<span class="font-bold text-green-600">${e.weightKg.toFixed(1)}</span>` },
    { def: 'recordedAt', header: 'Recorded At', cell: (e: any) => new Date(e.recordedAtUtc).toLocaleString() },
    { def: 'notes', header: 'Notes', cell: (e: any) => e.notes ?? '—' }
  ];

  ngOnInit(): void { this.load(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  load(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loading.set(true);
    this.error.set(null);

    this.svc.getById(id).pipe(takeUntil(this.destroy$)).subscribe({
      next:  a  => { this.animal.set(a); this.loading.set(false); },
      error: e  => { this.error.set(e?.error?.detail ?? 'Not found'); this.loading.set(false); }
    });
  }

  sortedWeights(a: AnimalDto) {
    return [...a.weightRecords].sort((x, y) => y.recordedDate.localeCompare(x.recordedDate));
  }

  speciesLabel(s: number): string { return (SPECIES_LABELS as any)[s] ?? '—'; }
  statusLabel(s: number):  string { return (STATUS_LABELS as any)[s]  ?? '—'; }
  sexLabel(s: number):     string { return (SEX_LABELS as any)[s]     ?? '—'; }
  statusBadge(s: number):  string { return (STATUS_BADGE_CLASS as any)[s] ?? 'badge-default'; }
  tagTypeLabel(t: number): string { return t === 1 ? 'Manual' : t === 2 ? 'Ear Tag' : 'RFID'; }
  speciesEmoji(s: number): string { return s === 3 ? '🐐' : s === 4 ? '🐑' : '🐄'; }

  ageLabel(dob: string): string {
    const days = Math.floor((Date.now() - new Date(dob).getTime()) / 86_400_000);
    if (days < 365) return `${Math.floor(days / 30)}mo old`;
    return `${Math.floor(days / 365)}y old`;
  }

  onQuarantine(a: AnimalDto): void {
    const reason = prompt('Quarantine reason:');
    if (!reason) return;
    this.svc.quarantine(a.id, { reason }).pipe(takeUntil(this.destroy$)).subscribe({ next: () => this.load() });
  }

  onRelease(a: AnimalDto): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      data: {
        title: 'Release from Quarantine',
        message: `Are you sure you want to release ${a.tagId} from quarantine?`,
        confirmButtonText: 'Release',
        isDestructive: false
      }
    });

    dialogRef.afterClosed().pipe(takeUntil(this.destroy$)).subscribe(result => {
      if (result) {
        this.svc.releaseFromQuarantine(a.id).pipe(takeUntil(this.destroy$)).subscribe({ next: () => this.load() });
      }
    });
  }

  onRecordWeight(a: AnimalDto): void {
    this.router.navigate(['/livestock', a.id, 'weights', 'new']);
  }

  goBack(): void {
    this.router.navigate(['/livestock']);
  }

  onSell(a: AnimalDto): void {
    const price = prompt('Sale price (BDT):');
    if (!price || isNaN(+price)) return;
    const today = new Date().toISOString().split('T')[0];
    this.svc.sell(a.id, { salePriceBdt: +price, saleDate: today })
      .pipe(takeUntil(this.destroy$)).subscribe({ next: () => this.load() });
  }
}
