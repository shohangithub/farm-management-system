import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDividerModule } from '@angular/material/divider';
import { ShedService } from '../services/shed.service';
import { PenService } from '../services/pen.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { forkJoin, of } from 'rxjs';
import { ConfirmService } from '../../../shared/services/confirm.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../core/utils/error-parser';

@Component({
  selector: 'app-shed-detail',
  standalone: true,
  imports: [TranslatePipe, ErrorStateComponent, 
    CommonModule,
    RouterModule,
    MatIconModule,
    MatButtonModule,
    MatTabsModule,
    MatDividerModule,
    PageHeaderComponent,
    DatePipe,
    DecimalPipe
  ],
  templateUrl: './shed-detail.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ShedDetailComponent {
  private readonly i18n = inject(I18nService);
  private readonly confirmService = inject(ConfirmService);
  private readonly shedService = inject(ShedService);
  private readonly penService = inject(PenService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private routeParams = toSignal(
    this.route.paramMap.pipe(
      map(params => {
        const branchId = params.get('branchId') || this.route.parent?.snapshot.paramMap.get('branchId') || '';
        const farmId = params.get('farmId') || this.route.parent?.snapshot.paramMap.get('farmId') || '';
        const shedId = params.get('shedId') || '';
        return { branchId, farmId, shedId };
      })
    ),
    { initialValue: { branchId: '', farmId: '', shedId: '' } }
  );

  readonly branchId = computed(() => this.routeParams().branchId);
  readonly farmId = computed(() => this.routeParams().farmId);
  readonly shedId = computed(() => this.routeParams().shedId);

  readonly isLoading = signal<boolean>(true);
  readonly error = signal<string | null>(null);

  private refreshTrigger = signal(0);
  private fetchParams = computed(() => ({
    shedId: this.shedId(),
    refresh: this.refreshTrigger()
  }));

  private dataResult = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(params => !!params.shedId),
      tap(() => { this.isLoading.set(true); this.error.set(null); }),
      switchMap(({ shedId }) => forkJoin({
        shed: this.shedService.getShedById(shedId).pipe(catchError(err => { this.error.set(parseApiError(err, 'Failed to load shed details.')); return of(null); })),
        pens: this.penService.getPensByShed(shedId).pipe(catchError(() => of([])))
      }).pipe(
        tap(res => {
          if (!res.shed && !this.error()) this.error.set(this.i18n.t('Failed to load shed details.'));
        }),
        catchError(err => {
          this.error.set(parseApiError(err, 'Failed to load shed details.'));
          return of({ shed: null, pens: [] });
        })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { shed: null, pens: [] } }
  );

  readonly shed = computed(() => this.dataResult().shed);
  readonly pens = computed(() => this.dataResult().pens);

  statusLabel = computed(() => {
    const s = this.shed()?.status;
    if (s === 'Active') return 'Active';
    if (s === 'Inactive') return 'Inactive';
    if (s === 'UnderMaintenance') return 'Maintenance';
    return s || 'Unknown';
  });

  statusClass = computed(() => {
    const s = this.shed()?.status;
    if (s === 'Active') return 'bg-success-50 text-success-700 dark:bg-success-900/20 dark:text-success-400 border border-success-200 dark:border-success-800';
    if (s === 'Inactive') return 'bg-gray-50 text-gray-600 dark:bg-gray-800 dark:text-gray-400 border border-gray-200 dark:border-gray-700';
    if (s === 'UnderMaintenance') return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
    return 'bg-gray-50 text-gray-700 dark:bg-gray-900/20 dark:text-gray-400 border border-gray-200 dark:border-gray-800';
  });

  loadShed(id?: string): void {
    this.refreshTrigger.update(v => v + 1);
  }

  onEdit(): void {
    if (this.shed()) {
      this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'edit']);
    }
  }

  onBack(): void {
    this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds']);
  }

  onManagePens(): void {
    if (this.shed()) {
      this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens']);
    }
  }

  async deleteShed(): Promise<void> {
    const sId = this.shedId();
    if (!sId) return;

    if ((await this.confirmService.confirmAsync({ message: 'Are you sure you want to delete this shed? This action cannot be undone.', confirmText: 'Delete', destructive: true }))) {
      this.shedService.deleteShed(sId).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds']);
        },
        error: (err) => {
          this.error.set(err?.error?.detail || this.i18n.t('Failed to delete shed.'));
          console.error('Failed to delete shed', err);
        }
      });
    }
  }
}
