import { Component, OnInit, inject, signal, ChangeDetectionStrategy, ViewChild, TemplateRef, computed } from '@angular/core';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { BatchService } from '../../services/batch.service';
import { BatchDto, BatchStatus } from '../../models/batch.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { FarmService } from '../../../farms/services/farm.service';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { of } from 'rxjs';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { NotificationService } from '../../../../shared/services/notification.service';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-batch-list',
  standalone: true,
  imports: [ErrorStateComponent, CommonModule, RouterModule, PageHeaderComponent, FormsModule, MatButtonModule, MatIconModule, MatDialogModule, MatSnackBarModule, TranslatePipe],
  templateUrl: './batch-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BatchList {
  private readonly svc = inject(BatchService);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotificationService);
  private readonly contextService = inject(WorkingContextService);
  private readonly i18n = inject(I18nService);
  readonly couldNotLoadTitle = this.i18n.t("Couldn't load batches");

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  @ViewChild('createBatchDialog') createBatchDialog!: TemplateRef<any>;
  
  createForm = {
    name: '',
    notes: '',
    farmId: ''
  };

  private currentFarmId = toSignal(this.contextService.currentFarm$.pipe(map(f => f?.id)), { initialValue: null });
  private refreshTrigger = signal(0);
  
  private fetchParams = computed(() => ({
    farmId: this.currentFarmId(),
    refresh: this.refreshTrigger()
  }));

  readonly batches = toSignal(
    toObservable(this.fetchParams).pipe(
      filter(({ farmId }) => !!farmId),
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(({ farmId }) => this.svc.getBatches(farmId!).pipe(
        map(res => res.items),
        catchError(err => {
          this.error.set(parseApiError(err, this.i18n.t('Could not load batches.')));
          return of([]);
        })
      )),
      tap(() => this.loading.set(false))
    ),
    { initialValue: [] }
  );

  refresh(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  onCreateBatch() {
    this.createForm.name = '';
    this.createForm.notes = '';
    this.createForm.farmId = this.currentFarmId() || '';
    if (this.createForm.farmId) {
      this.dialog.open(this.createBatchDialog, { maxWidth: '95vw', disableClose: true, width: '560px' });
    } else {
      this.notify.warning(this.i18n.t('Please select a farm context first'));
    }
  }

  readonly creating = signal(false);

  submitCreateBatch() {
    if (!this.createForm.name.trim() || !this.createForm.farmId || this.creating()) return;

    this.creating.set(true);
    this.svc.createBatch({
      farmId: this.createForm.farmId,
      name: this.createForm.name,
      notes: this.createForm.notes
    }).subscribe({
      next: () => {
        this.creating.set(false);
        this.dialog.closeAll();
        this.notify.success(this.i18n.t('Batch created successfully'));
        this.refreshTrigger.update(v => v + 1);
      },
      error: (err) => {
        this.creating.set(false);
        this.notify.error(parseApiError(err, this.i18n.t('Could not create the batch.')));
      }
    });
  }
}
