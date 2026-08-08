import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDividerModule } from '@angular/material/divider';
import { FarmService } from '../services/farm.service';
import { Farm } from '../models/farm.model';
import { ShedService } from '../services/shed.service';
import { ShedList } from '../models/shed.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { forkJoin, of } from 'rxjs';

@Component({
  selector: 'app-farm-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule,
    MatButtonModule,
    MatTabsModule,
    MatDividerModule,
    PageHeaderComponent,
    LoadingComponent,
    DatePipe,
    DecimalPipe
  ],
  templateUrl: './farm-detail.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FarmDetailComponent {
  private readonly farmService = inject(FarmService);
  private readonly shedService = inject(ShedService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly branchId = toSignal(this.route.paramMap.pipe(map(params => params.get('branchId') || this.route.parent?.snapshot.paramMap.get('branchId'))), { initialValue: null });
  readonly farmId = toSignal(this.route.paramMap.pipe(map(params => params.get('farmId'))), { initialValue: null });

  readonly isLoading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  private refreshTrigger = signal(0);

  private fetchParams = computed(() => ({
    farmId: this.farmId(),
    refresh: this.refreshTrigger()
  }));

  private dataResult = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(params => !!params.farmId),
      tap(() => { this.isLoading.set(true); this.error.set(null); }),
      switchMap(({ farmId }) => forkJoin({
        farm: this.farmService.getFarmById(farmId!).pipe(catchError(() => of(null))),
        sheds: this.shedService.getShedsByFarm(farmId!).pipe(catchError(() => of([])))
      }).pipe(
        tap(res => {
          if (!res.farm) this.error.set('Failed to load farm details.');
        }),
        catchError(err => {
          this.error.set('Failed to load farm details.');
          return of({ farm: null, sheds: [] });
        })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { farm: null, sheds: [] } }
  );

  readonly farm = computed(() => this.dataResult().farm);
  readonly sheds = computed(() => this.dataResult().sheds);

  statusLabel = computed(() => {
    const s = this.farm()?.status;
    if (s === 'Active') return 'Active';
    if (s === 'Inactive') return 'Inactive';
    if (s === 'UnderMaintenance') return 'Under Maintenance';
    return s || 'Closed';
  });

  statusClass = computed(() => {
    const s = this.farm()?.status;
    if (s === 'Active') return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
    if (s === 'Inactive') return 'bg-gray-50 text-gray-600 dark:bg-gray-800 dark:text-gray-400 border border-gray-200 dark:border-gray-700';
    if (s === 'UnderMaintenance') return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
    return 'bg-red-50 text-red-700 dark:bg-red-900/20 dark:text-red-400 border border-red-200 dark:border-red-800';
  });

  farmTypeLabel = computed(() => {
    const t = this.farm()?.type;
    const types: Record<string, string> = {
      'Dairy': 'Dairy',
      'Poultry': 'Poultry',
      'Mixed': 'Mixed',
      'Crop': 'Crop',
      'Aquaculture': 'Aquaculture'
    };
    return t ? (types[t] || t) : 'Unknown';
  });

  loadFarm(id?: string): void {
    this.refreshTrigger.update(v => v + 1);
  }

  onEdit(): void {
    if (this.farm()) {
      this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'edit']);
    }
  }

  onBack(): void {
    this.router.navigate(['/organizations/branches', this.branchId(), 'farms']);
  }

  onManageSheds(): void {
    if (this.farm()) {
      this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds']);
    }
  }

  deleteFarm(): void {
    const fId = this.farmId();
    if (!fId) return;
    
    if (confirm('Are you sure you want to delete this farm? This action cannot be undone.')) {
      this.farmService.deleteFarm(fId).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms']);
        },
        error: (err) => {
          this.error.set(err?.error?.detail || 'Failed to delete farm.');
          console.error('Failed to delete farm', err);
        }
      });
    }
  }
}
