import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { PenService } from '../services/pen.service';
import { PenList } from '../models/pen.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-pen-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, MatIconModule, PageHeaderComponent],
  templateUrl: './pen-list.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PenListComponent {
  private readonly penService = inject(PenService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  isLoading = signal<boolean>(true);
  Math = Math;

  searchTerm = signal<string>('');
  statusFilter = signal<number | null>(null);

  private routeParams = toSignal(
    this.route.paramMap.pipe(
      map(() => {
        let currentRoute: ActivatedRoute | null = this.route;
        let branchId = '';
        let farmId = '';
        let shedId = this.route.snapshot.paramMap.get('shedId') || this.route.parent?.snapshot.paramMap.get('shedId') || '';
        
        while (currentRoute) {
          if (!branchId) branchId = currentRoute.snapshot.paramMap.get('branchId') || '';
          if (!farmId) farmId = currentRoute.snapshot.paramMap.get('farmId') || '';
          if (!shedId) shedId = currentRoute.snapshot.paramMap.get('shedId') || '';
          currentRoute = currentRoute.parent;
        }
        return { branchId, farmId, shedId };
      })
    ),
    { initialValue: { branchId: '', farmId: '', shedId: '' } }
  );

  readonly branchId = computed(() => this.routeParams().branchId);
  readonly farmId = computed(() => this.routeParams().farmId);
  readonly shedId = computed(() => this.routeParams().shedId);

  private refreshTrigger = signal(0);
  private fetchParams = computed(() => ({
    shedId: this.shedId(),
    refresh: this.refreshTrigger()
  }));

  readonly pensResult = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(params => !!params.shedId),
      tap(() => this.isLoading.set(true)),
      switchMap(({ shedId }) => this.penService.getPensByShed(shedId).pipe(
        catchError(err => {
          console.error('Failed to load pens', err);
          return of([] as PenList[]);
        })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: [] as PenList[] }
  );

  filteredPens = computed(() => {
    let result = this.pensResult();
    
    const search = this.searchTerm().toLowerCase();
    if (search) {
      result = result.filter(p => 
        p.penName.toLowerCase().includes(search) || 
        p.penNumber.toLowerCase().includes(search)
      );
    }
    
    const status = this.statusFilter();
    if (status) {
      result = result.filter(p => p.status === status);
    }
    
    return result;
  });

  loadPens(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  onFilterStatus(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.statusFilter.set(val ? parseInt(val, 10) : null);
  }

  onAddPen(): void {
    this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens', 'new']);
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
