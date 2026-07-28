import { Component, inject, ChangeDetectionStrategy, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatTooltipModule } from '@angular/material/tooltip';
import { HealthService } from '../../services/health.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-vet-visit-list',
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
  templateUrl: './vet-visit-list.html',
  styleUrls: ['./vet-visit-list.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VetVisitListComponent {
  private healthService = inject(HealthService);
  private dialog = inject(MatDialog);

  displayedColumns: string[] = ['visitDate', 'vetName', 'visitType', 'purpose', 'cost', 'nextVisit', 'actions'];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  // State
  pageIndex = signal(0);
  pageSize = signal(10);
  refreshTrigger = signal(0);
  isLoading = signal(true);

  private paginationParams = computed(() => ({
    pageIndex: this.pageIndex(),
    pageSize: this.pageSize(),
    refresh: this.refreshTrigger()
  }));

  private vetVisitsResult = toSignal(
    toObservable(this.paginationParams).pipe(
      tap(() => this.isLoading.set(true)),
      switchMap(({ pageIndex, pageSize }) => 
        this.healthService.getVetVisits(pageIndex + 1, pageSize).pipe(
          catchError((err) => {
            console.error('Error loading vet visits', err);
            return of({ items: [], totalCount: 0 });
          })
        )
      ),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { items: [], totalCount: 0 } }
  );

  dataSource = computed(() => this.vetVisitsResult().items);
  totalItems = computed(() => this.vetVisitsResult().totalCount);

  onPageChange(event: any): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  loadVetVisits(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  openScheduleVisitDialog(): void {
    // We could create a ScheduleVetVisitDialog if required, or skip if out of scope
  }
}
