import { Component, inject, ChangeDetectionStrategy, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatTooltipModule } from '@angular/material/tooltip';
import { HealthService } from '../../services/health.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { CauseOfDeath } from '../../models/health.models';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { RecordMortalityDialog } from '../../components/dialogs/record-mortality-dialog/record-mortality-dialog.component';
import { MortalityDetailDialog } from '../../components/dialogs/mortality-detail-dialog/mortality-detail-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-mortality-list',
  standalone: true,
  imports: [
    CommonModule, 
    RouterModule, 
    MatButtonModule, 
    MatIconModule, 
    MatPaginatorModule, 
    MatTooltipModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  templateUrl: './mortality-list.html',
  styleUrls: ['./mortality-list.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MortalityListComponent {
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private dialog = inject(MatDialog);

  displayedColumns: string[] = ['animalId', 'deathDate', 'causeOfDeath', 'diseaseName', 'loss', 'actions'];

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

  private mortalitiesResult = toSignal(
    toObservable(this.paginationParams).pipe(
      tap(() => this.isLoading.set(true)),
      switchMap(({ pageIndex, pageSize, farmId }) => 
        this.healthService.getMortalityRecords({ pageNumber: pageIndex + 1, pageSize, farmId }).pipe(
          catchError((err) => {
            console.error('Error loading mortality records', err);
            return of({ items: [], totalCount: 0 });
          })
        )
      ),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { items: [], totalCount: 0 } }
  );

  dataSource = computed(() => this.mortalitiesResult().items);
  totalItems = computed(() => this.mortalitiesResult().totalCount);

  onPageChange(event: any): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  loadMortalities(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  openRecordMortalityDialog(): void {
    const dialogRef = this.dialog.open(RecordMortalityDialog, {
      width: '560px'
    });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadMortalities();
    });
  }

  viewDetails(element: any): void {
    this.dialog.open(MortalityDetailDialog, {
      width: '720px',
      data: element
    });
  }

  getCauseName(causeValue: number | string): string {
    const value = Number(causeValue);
    switch (value) {
      case CauseOfDeath.Disease: return 'Disease';
      case CauseOfDeath.Accident: return 'Accident';
      case CauseOfDeath.NaturalCauses: return 'Natural Causes';
      case CauseOfDeath.Unknown: return 'Unknown';
      case CauseOfDeath.Slaughter: return 'Slaughter';
      default: return 'Unknown';
    }
  }
}
