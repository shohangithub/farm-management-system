import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { FarmService } from '../services/farm.service';
import { FarmList } from '../models/farm.model';
import { FarmCardComponent } from '../components/farm-card/farm-card.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { of } from 'rxjs';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-farm-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, MatIconModule, FarmCardComponent, PageHeaderComponent, LoadingComponent, EmptyStateComponent],
  templateUrl: './farm-list.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FarmListComponent {
  private readonly farmService = inject(FarmService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  isLoading = signal<boolean>(true);

  // Using a custom observable chain to pick up changes from the route automatically
  readonly branchId = toSignal(
    this.route.paramMap.pipe(map(params => params.get('branchId') || this.route.parent?.snapshot.paramMap.get('branchId') || '')),
    { initialValue: '' }
  );

  searchTerm = signal<string>('');
  statusFilter = signal<string | null>(null);

  private refreshTrigger = signal(0);
  private fetchParams = computed(() => ({
    branchId: this.branchId(),
    refresh: this.refreshTrigger()
  }));

  readonly farmsResult = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(params => !!params.branchId),
      tap(() => this.isLoading.set(true)),
      switchMap(({ branchId }) => this.farmService.getFarmsByBranch(branchId).pipe(
        catchError(err => {
          console.error('Failed to load farms', err);
          return of([] as FarmList[]);
        })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: [] as FarmList[] }
  );

  filteredFarms = computed(() => {
    let result = this.farmsResult();
    
    const search = this.searchTerm().toLowerCase();
    if (search) {
      result = result.filter(f => 
        f.farmName.toLowerCase().includes(search) || 
        f.farmCode.toLowerCase().includes(search)
      );
    }
    
    const status = this.statusFilter();
    if (status) {
      result = result.filter(f => f.status === status);
    }
    
    return result;
  });

  loadFarms(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  onFilterStatus(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.statusFilter.set(val ? val : null);
  }

  onAddFarm(): void {
    const branchId = this.branchId();
    if (branchId) {
      this.router.navigate(['/organizations/branches', branchId, 'farms', 'new']);
    }
  }
}
