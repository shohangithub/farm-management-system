import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDividerModule } from '@angular/material/divider';
import { PenService } from '../services/pen.service';
import { AnimalService } from '../../livestock/services/animal.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { forkJoin, of } from 'rxjs';

@Component({
  selector: 'app-pen-detail',
  standalone: true,
  imports: [
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
  templateUrl: './pen-detail.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PenDetailComponent {
  private readonly penService = inject(PenService);
  private readonly animalService = inject(AnimalService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private routeParams = toSignal(
    this.route.paramMap.pipe(
      map(() => {
        let currentRoute: ActivatedRoute | null = this.route;
        let branchId = '';
        let farmId = '';
        let shedId = '';
        const penId = this.route.snapshot.paramMap.get('penId') || '';

        while (currentRoute) {
          if (!branchId) branchId = currentRoute.snapshot.paramMap.get('branchId') || '';
          if (!farmId) farmId = currentRoute.snapshot.paramMap.get('farmId') || '';
          if (!shedId) shedId = currentRoute.snapshot.paramMap.get('shedId') || '';
          currentRoute = currentRoute.parent;
        }

        return { branchId, farmId, shedId, penId };
      })
    ),
    { initialValue: { branchId: '', farmId: '', shedId: '', penId: '' } }
  );

  readonly branchId = computed(() => this.routeParams().branchId);
  readonly farmId = computed(() => this.routeParams().farmId);
  readonly shedId = computed(() => this.routeParams().shedId);
  readonly penId = computed(() => this.routeParams().penId);

  readonly isLoading = signal<boolean>(true);
  readonly error = signal<string | null>(null);

  private refreshTrigger = signal(0);
  private fetchParams = computed(() => ({
    penId: this.penId(),
    refresh: this.refreshTrigger()
  }));

  private dataResult = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(params => !!params.penId),
      tap(() => { this.isLoading.set(true); this.error.set(null); }),
      switchMap(({ penId }) => forkJoin({
        pen: this.penService.getPenById(penId).pipe(catchError(() => of(null))),
        animals: this.animalService.getList({ penId, pageSize: 50 }).pipe(
          map(res => res.items),
          catchError(() => of([]))
        )
      }).pipe(
        tap(res => {
          if (!res.pen) this.error.set('Failed to load pen details.');
        }),
        catchError(err => {
          this.error.set('Failed to load pen details.');
          return of({ pen: null, animals: [] });
        })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { pen: null, animals: [] } }
  );

  readonly pen = computed(() => this.dataResult().pen);
  readonly animals = computed(() => this.dataResult().animals);

  statusLabel = computed(() => {
    const s = this.pen()?.status;
    if (s === 1) return 'Active';
    if (s === 2) return 'Inactive';
    if (s === 3) return 'Maintenance';
    return 'Unknown';
  });

  statusClass = computed(() => {
    const s = this.pen()?.status;
    if (s === 1) return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800';
    if (s === 2) return 'bg-gray-50 text-gray-600 dark:bg-gray-800 dark:text-gray-400 border border-gray-200 dark:border-gray-700';
    if (s === 3) return 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400 border border-amber-200 dark:border-amber-800';
    return 'bg-gray-50 text-gray-700 dark:bg-gray-900/20 dark:text-gray-400 border border-gray-200 dark:border-gray-800';
  });

  loadPen(id?: string): void {
    this.refreshTrigger.update(v => v + 1);
  }

  onEdit(): void {
    if (this.pen()) {
      this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens', this.penId(), 'edit']);
    }
  }

  onBack(): void {
    this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens']);
  }

  deletePen(): void {
    const pId = this.penId();
    if (!pId) return;

    if (confirm('Are you sure you want to delete this pen? This action cannot be undone.')) {
      this.penService.deletePen(pId).subscribe({
        next: () => {
          this.router.navigate(['/organizations/branches', this.branchId(), 'farms', this.farmId(), 'sheds', this.shedId(), 'pens']);
        },
        error: (err) => {
          this.error.set(err?.error?.detail || 'Failed to delete pen.');
          console.error('Failed to delete pen', err);
        }
      });
    }
  }
}
