import { Component, inject, ViewChild, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { of } from 'rxjs';

import { HealthService } from '../../services/health.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { VaccinationProtocolDto } from '../../models/health.models';
import { AssignProtocolDialog } from '../../components/dialogs/assign-protocol-dialog/assign-protocol-dialog.component';
import { CreateProtocolDialogComponent } from '../../components/dialogs/create-protocol-dialog/create-protocol-dialog.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-vaccination-protocol-list',
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
  templateUrl: './vaccination-protocol-list.html',
  styleUrls: ['./vaccination-protocol-list.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VaccinationProtocolListComponent {
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private dialog = inject(MatDialog);

  displayedColumns: string[] = ['title', 'targetSpecies', 'steps', 'status', 'actions'];
  
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  // --- Reactive State (Signals) ---
  pageIndex = signal(0);
  pageSize = signal(10);
  refreshTrigger = signal(0);
  isLoading = signal(true);

  private currentFarm = toSignal(this.contextService.currentFarm$);

  // Derived state to drive the fetch pipeline
  private paginationParams = computed(() => ({
    pageIndex: this.pageIndex(),
    pageSize: this.pageSize(),
    farmId: this.currentFarm()?.id,
    refresh: this.refreshTrigger()
  }));

  // Reactive Data Stream handling the HTTP request
  private protocolsResult = toSignal(
    toObservable(this.paginationParams).pipe(
      tap(() => this.isLoading.set(true)),
      switchMap(({ pageIndex, pageSize, farmId }) => 
        this.healthService.getVaccinationProtocols({ pageNumber: pageIndex + 1, pageSize, farmId }).pipe(
          catchError((err) => {
            console.error('Error loading protocols', err);
            return of({ items: [], totalCount: 0 });
          })
        )
      ),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { items: [], totalCount: 0 } }
  );

  // Exposed Signals for the Template
  dataSource = computed(() => this.protocolsResult().items);
  totalItems = computed(() => this.protocolsResult().totalCount);

  // --- Actions ---
  onPageChange(event: any): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  loadProtocols(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  openCreateDialog(): void {
    const dialogRef = this.dialog.open(CreateProtocolDialogComponent, {
      width: '700px',
      maxWidth: '95vw'
    });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadProtocols();
    });
  }

  openEditDialog(protocol: VaccinationProtocolDto): void {
    const dialogRef = this.dialog.open(CreateProtocolDialogComponent, {
      width: '700px',
      maxWidth: '95vw',
      data: protocol
    });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadProtocols();
    });
  }

  openAssignDialog(protocol: VaccinationProtocolDto): void {
    this.dialog.open(AssignProtocolDialog, {
      width: '720px',
      data: { protocol }
    });
  }
}
