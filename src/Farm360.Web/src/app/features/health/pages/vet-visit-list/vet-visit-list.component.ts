import { Component, inject, ChangeDetectionStrategy, signal, computed, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatTooltipModule } from '@angular/material/tooltip';
import { HealthService } from '../../services/health.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { LogVetVisitDialog } from '../../components/dialogs/log-vet-visit-dialog/log-vet-visit-dialog.component';
import { VetVisitDetailDialogComponent } from '../../components/dialogs/vet-visit-detail-dialog/vet-visit-detail-dialog.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';

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
  private contextService = inject(WorkingContextService);
  private pdfExportService = inject(PdfExportService);
  private dialog = inject(MatDialog);

  @ViewChild('reportSheet') reportSheet?: ElementRef<HTMLElement>;
  readonly isExporting = signal(false);

  displayedColumns: string[] = ['visitDate', 'vetName', 'visitType', 'purpose', 'cost', 'nextVisit', 'actions'];

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

  private vetVisitsResult = toSignal(
    toObservable(this.paginationParams).pipe(
      tap(() => this.isLoading.set(true)),
      switchMap(({ pageIndex, pageSize, farmId }) =>
        this.healthService.getVetVisits({ pageNumber: pageIndex + 1, pageSize, farmId }).pipe(
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
    const dialogRef = this.dialog.open(LogVetVisitDialog, {
      width: '720px',
      panelClass: 'custom-dialog-container',
      disableClose: true
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadVetVisits();
      }
    });
  }

  viewVisitDetails(visit: any): void {
    const dialogRef = this.dialog.open(VetVisitDetailDialogComponent, {
      width: '700px',
      panelClass: 'custom-dialog-container',
      data: { visitId: visit.id }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadVetVisits();
      }
    });
  }

  async exportPdf(): Promise<void> {
    const el = this.reportSheet?.nativeElement;
    if (!el) return;

    const items = this.dataSource() || [];
    const total = this.totalItems();
    const farm = this.contextService.currentFarmValue;
    const org = this.contextService.currentOrgValue;

    this.isExporting.set(true);
    try {
      await this.pdfExportService.exportElement(el, {
        filename: `Farm360_Vet_Visits_${new Date().toISOString().split('T')[0]}`,
        orientation: 'landscape',
        header: {
          title: 'Veterinary Visits & Health Log',
          subtitle: 'Professional Clinical Evaluations & Medical Services',
          farmName: farm?.name || 'Primary Farm',
          orgName: org?.name || 'Farm360 Enterprise',
          currency: 'BDT (৳)',
          metaFields: [
            { label: 'Total Recorded Visits', value: `${total}` },
            { label: 'Current Page Items', value: `${items.length}` }
          ]
        },
        showSignatures: true
      });
    } catch (err) {
      console.error('Failed to export Vet Visits PDF:', err);
    } finally {
      this.isExporting.set(false);
    }
  }
}
