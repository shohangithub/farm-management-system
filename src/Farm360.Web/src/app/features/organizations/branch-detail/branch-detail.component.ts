import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDividerModule } from '@angular/material/divider';
import { BranchService } from '../services/branch.service';
import { Branch } from '../models/branch.model';
import { FarmService } from '../../farms/services/farm.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { forkJoin, of } from 'rxjs';

@Component({
  selector: 'app-branch-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule,
    MatButtonModule,
    MatTabsModule,
    MatDividerModule,
    PageHeaderComponent,
    DatePipe
  ],
  templateUrl: './branch-detail.html',
  styleUrls: ['./branch-detail.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BranchDetailComponent {
  private readonly branchService = inject(BranchService);
  private readonly farmService = inject(FarmService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private routeParams = toSignal(
    this.route.paramMap.pipe(
      map(params => ({
        orgId: params.get('orgId') || '',
        branchId: params.get('branchId') || ''
      }))
    ),
    { initialValue: { orgId: '', branchId: '' } }
  );

  readonly orgId = computed(() => this.routeParams().orgId);
  readonly branchId = computed(() => this.routeParams().branchId);

  readonly isLoading = signal<boolean>(true);
  readonly error = signal<string | null>(null);

  private refreshTrigger = signal(0);
  private fetchParams = computed(() => ({
    branchId: this.branchId(),
    refresh: this.refreshTrigger()
  }));

  private dataResult = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(params => !!params.branchId),
      tap(() => { this.isLoading.set(true); this.error.set(null); }),
      switchMap(({ branchId }) => forkJoin({
        branch: this.branchService.getBranchById(branchId).pipe(catchError(() => of(null))),
        farms: this.farmService.getFarmsByBranch(branchId).pipe(catchError(() => of([])))
      }).pipe(
        tap(res => {
          if (!res.branch) this.error.set('Failed to load branch details.');
        }),
        catchError(err => {
          this.error.set('Failed to load branch details.');
          return of({ branch: null, farms: [] });
        })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { branch: null, farms: [] } }
  );

  readonly branch = computed(() => this.dataResult().branch);
  readonly farms = computed(() => this.dataResult().farms);

  statusLabel = computed(() => {
    const s = this.branch()?.status;
    if (s === 'Active') return 'Active';
    if (s === 'Inactive') return 'Inactive';
    return 'Closed';
  });

  statusClass = computed(() => {
    const s = this.branch()?.status;
    if (s === 'Active') return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
    if (s === 'Inactive') return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
    return 'bg-gray-50 text-gray-600 dark:bg-gray-800 dark:text-gray-400 border border-gray-200 dark:border-gray-700';
  });

  loadBranch(id?: string): void {
    this.refreshTrigger.update(v => v + 1);
  }

  onEdit(): void {
    const b = this.branch();
    if (b) {
      this.router.navigate(['/organizations', this.orgId(), 'branches', 'edit', b.id]);
    }
  }

  onBack(): void {
    this.router.navigate(['/organizations', this.orgId(), 'branches']);
  }

  onManageFarms(): void {
    const b = this.branch();
    if (b) {
      this.router.navigate(['/organizations', 'branches', b.id, 'farms']);
    }
  }

  formatAddress(branch: Branch | null): string {
    if (!branch?.address) return '';
    const parts = [
      branch.address.street,
      branch.address.city,
      branch.address.state,
      branch.address.zipCode,
      branch.address.country
    ].filter(Boolean);
    return parts.join(', ');
  }

  hasAddress(branch: Branch | null): boolean {
    return !!(branch?.address?.street || branch?.address?.city || branch?.address?.country);
  }
}
