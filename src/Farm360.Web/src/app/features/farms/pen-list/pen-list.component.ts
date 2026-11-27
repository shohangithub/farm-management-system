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
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../core/utils/error-parser';

@Component({
  selector: 'app-pen-list',
  standalone: true,
  imports: [TranslatePipe, ErrorStateComponent, CommonModule, RouterModule, FormsModule, MatIconModule, PageHeaderComponent],
  templateUrl: './pen-list.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PenListComponent {
  private readonly i18n = inject(I18nService);
  private readonly penService = inject(PenService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  isLoading = signal<boolean>(true);
  readonly loadError = signal<string | null>(null);
  Math = Math;

  searchTerm = signal<string>('');
  statusFilter = signal<string | null>(null);

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
      tap(() => { this.isLoading.set(true); this.loadError.set(null); }),
      switchMap(({ shedId }) => this.penService.getPensByShed(shedId).pipe(
        catchError(err => {
          this.loadError.set(parseApiError(err, 'Could not load pens.'));
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
    this.statusFilter.set(val ? val : null);
  }

  onAddPen(): void {
    this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens', 'new']);
  }

  getStatusName(status: string): string {
    switch (status) {
      case 'Active': return 'Active';
      case 'Inactive': return 'Inactive';
      case 'UnderMaintenance': return 'Maintenance';
      default: return status || 'Unknown';
    }
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Active': return 'bg-success-50 text-success-700 dark:bg-success-900/20 dark:text-success-400 border border-success-200 dark:border-success-800';
      case 'Inactive': return 'bg-gray-50 text-gray-600 dark:bg-gray-800 dark:text-gray-400 border border-gray-200 dark:border-gray-700';
      case 'UnderMaintenance': return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
      default: return 'bg-gray-50 text-gray-700 dark:bg-gray-900/20 dark:text-gray-400 border border-gray-200 dark:border-gray-800';
    }
  }
}
