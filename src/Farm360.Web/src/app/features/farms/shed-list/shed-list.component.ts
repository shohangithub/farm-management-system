import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { ShedService } from '../services/shed.service';
import { ShedList } from '../models/shed.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { of } from 'rxjs';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-shed-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, MatIconModule, PageHeaderComponent, LoadingComponent, EmptyStateComponent],
  templateUrl: './shed-list.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ShedListComponent {
  private readonly shedService = inject(ShedService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  isLoading = signal<boolean>(true);
  Math = Math;

  searchTerm = signal<string>('');
  statusFilter = signal<number | null>(null);

  private routeParams = toSignal(
    this.route.paramMap.pipe(
      map(() => ({
        farmId: this.route.snapshot.paramMap.get('farmId') || this.route.parent?.snapshot.paramMap.get('farmId') || '',
        branchId: this.route.snapshot.paramMap.get('branchId') || this.route.parent?.snapshot.paramMap.get('branchId') || ''
      }))
    ),
    { initialValue: { farmId: '', branchId: '' } }
  );

  readonly farmId = computed(() => this.routeParams().farmId);
  readonly branchId = computed(() => this.routeParams().branchId);

  private refreshTrigger = signal(0);
  private fetchParams = computed(() => ({
    farmId: this.farmId(),
    refresh: this.refreshTrigger()
  }));

  readonly shedsResult = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(params => !!params.farmId),
      tap(() => this.isLoading.set(true)),
      switchMap(({ farmId }) => this.shedService.getShedsByFarm(farmId).pipe(
        catchError(err => {
          console.error('Failed to load sheds', err);
          return of([] as ShedList[]);
        })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: [] as ShedList[] }
  );

  filteredSheds = computed(() => {
    let result = this.shedsResult();
    
    const search = this.searchTerm().toLowerCase();
    if (search) {
      result = result.filter(s => 
        s.shedName.toLowerCase().includes(search) || 
        s.shedNumber.toLowerCase().includes(search)
      );
    }
    
    const status = this.statusFilter();
    if (status) {
      result = result.filter(s => s.status === status);
    }
    
    return result;
  });

  loadSheds(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  onFilterStatus(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.statusFilter.set(val ? parseInt(val, 10) : null);
  }

  onAddShed(): void {
    this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', 'new']);
  }

  getStatusName(status: number): string {
    switch (status) {
      case 1: return 'Active';
      case 2: return 'Inactive';
      case 3: return 'Maintenance';
      default: return 'Unknown';
    }
  }

  getStatusClass(status: number): string {
    switch (status) {
      case 1: return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
      case 2: return 'bg-gray-50 text-gray-600 dark:bg-gray-800 dark:text-gray-400 border border-gray-200 dark:border-gray-700';
      case 3: return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
      default: return 'bg-gray-50 text-gray-700 dark:bg-gray-900/20 dark:text-gray-400 border border-gray-200 dark:border-gray-800';
    }
  }
}
