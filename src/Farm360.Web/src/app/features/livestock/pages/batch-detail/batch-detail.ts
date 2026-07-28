import { Component, inject, signal, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { BatchService } from '../../services/batch.service';
import { BatchDto } from '../../models/batch.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, filter, map, switchMap, tap } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-batch-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageHeaderComponent, MatButtonModule, MatIconModule, DatePipe],
  templateUrl: './batch-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BatchDetail {
  private readonly svc = inject(BatchService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  private routeId = toSignal(this.route.paramMap.pipe(map(params => params.get('id'))), { initialValue: null });
  private refreshTrigger = signal(0);

  private fetchParams = computed(() => ({
    id: this.routeId(),
    refresh: this.refreshTrigger()
  }));

  readonly batch = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(params => !!params.id),
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(({ id }) => this.svc.getBatchDetails(id!).pipe(
        catchError(err => {
          this.error.set(err.message || 'Error loading batch');
          return of(null);
        })
      )),
      tap(() => this.loading.set(false))
    ),
    { initialValue: null }
  );

  load(id?: string) {
    this.refreshTrigger.update(v => v + 1);
  }

  goBack() {
    this.router.navigate(['/livestock/batches']);
  }
}
