import { Component, inject, ChangeDetectionStrategy, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HealthService } from '../../services/health.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { MedicalTreatmentDto, TreatmentStatus } from '../../models/health.models';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { LogTreatmentDialog } from '../../components/dialogs/log-treatment-dialog/log-treatment-dialog.component';
import { MatMenuModule } from '@angular/material/menu';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-treatment-list',
  standalone: true,
  imports: [
    CommonModule, 
    RouterModule, 
    MatCardModule, 
    MatButtonModule, 
    MatIconModule, 
    MatTableModule, 
    MatPaginatorModule, 
    MatChipsModule, 
    MatProgressSpinnerModule,
    MatDialogModule,
    MatMenuModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  templateUrl: './treatment-list.html',
  styleUrls: ['./treatment-list.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TreatmentListComponent {
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private dialog = inject(MatDialog);

  displayedColumns: string[] = ['animalId', 'diagnosis', 'medicationName', 'startDate', 'status', 'cost', 'actions'];
  treatmentStatus = TreatmentStatus;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  // State
  pageIndex = signal(0);
  pageSize = signal(10);
  refreshTrigger = signal(0);
  isLoading = signal(true);

  private currentFarm = toSignal(this.contextService.currentFarm$);

  private paginationParams = computed(() => ({
    pageIndex: this.pageIndex(),
    pageSize: this.pageSize(),
    farmId: this.currentFarm()?.id,
    refresh: this.refreshTrigger()
  }));

  private treatmentsResult = toSignal(
    toObservable(this.paginationParams).pipe(
      tap(() => this.isLoading.set(true)),
      switchMap(({ pageIndex, pageSize, farmId }) => 
        this.healthService.getTreatments({ pageNumber: pageIndex + 1, pageSize, farmId }).pipe(
          catchError((err) => {
            console.error('Error loading treatments', err);
            return of({ items: [], totalCount: 0 });
          })
        )
      ),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { items: [], totalCount: 0 } }
  );

  dataSource = computed(() => this.treatmentsResult().items);
  totalItems = computed(() => this.treatmentsResult().totalCount);

  onPageChange(event: any): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  loadTreatments(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  openLogTreatmentDialog(): void {
    const dialogRef = this.dialog.open(LogTreatmentDialog, {
      width: '600px'
    });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadTreatments();
    });
  }

  updateStatus(treatment: MedicalTreatmentDto, status: TreatmentStatus): void {
    this.healthService.updateTreatmentStatus(treatment.id, status, 'Status updated via list view').subscribe({
      next: () => this.loadTreatments(),
      error: (err) => console.error('Error updating status', err)
    });
  }
}
