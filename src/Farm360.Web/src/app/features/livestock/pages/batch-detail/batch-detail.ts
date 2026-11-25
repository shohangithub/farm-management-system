import { Component, inject, signal, ChangeDetectionStrategy, computed } from '@angular/core';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
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
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-batch-detail',
  standalone: true,
  imports: [ErrorStateComponent, CommonModule, RouterModule, PageHeaderComponent, MatButtonModule, MatIconModule, DatePipe, TranslatePipe],
  templateUrl: './batch-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BatchDetail {
  private readonly svc = inject(BatchService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);
  readonly couldNotLoadTitle = this.i18n.t("Couldn't load batch");

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
          this.error.set(parseApiError(err, this.i18n.t('Could not load this batch.')));
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
