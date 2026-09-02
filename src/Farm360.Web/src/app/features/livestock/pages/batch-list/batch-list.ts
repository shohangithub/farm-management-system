import { Component, OnInit, inject, signal, ChangeDetectionStrategy, ViewChild, TemplateRef, computed } from '@angular/core';
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
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { of } from 'rxjs';
import { WorkingContextService } from '../../../../core/services/working-context.service';

@Component({
  selector: 'app-batch-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageHeaderComponent, FormsModule, MatButtonModule, MatIconModule, MatDialogModule, MatSnackBarModule],
  templateUrl: './batch-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BatchList {
  private readonly svc = inject(BatchService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly contextService = inject(WorkingContextService);

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
          this.error.set(err.message || 'Error loading batches');
          return of([]);
        })
      )),
      tap(() => this.loading.set(false))
    ),
    { initialValue: [] }
  );

  onCreateBatch() {
    this.createForm.name = '';
    this.createForm.notes = '';
    this.createForm.farmId = this.currentFarmId() || '';
    if (this.createForm.farmId) {
      this.dialog.open(this.createBatchDialog, { disableClose: true, width: '560px' });
    } else {
      this.snackBar.open('Please select a farm context first', 'Close', { duration: 3000 });
    }
  }

  submitCreateBatch() {
    if (!this.createForm.name || !this.createForm.farmId) return;

    this.svc.createBatch({
      farmId: this.createForm.farmId,
      name: this.createForm.name,
      notes: this.createForm.notes
    }).subscribe({
      next: () => {
        this.dialog.closeAll();
        this.snackBar.open('Batch created successfully', 'Close', { duration: 3000 });
        this.refreshTrigger.update(v => v + 1);
      }
    });
  }
}
