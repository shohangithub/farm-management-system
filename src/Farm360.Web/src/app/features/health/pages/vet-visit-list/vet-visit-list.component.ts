import { CanPipe } from '../../../../shared/pipes/can.pipe';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
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
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { HealthNavComponent } from '../../components/health-nav/health-nav.component';
import { HealthFilterBarComponent } from '../../components/health-filter-bar/health-filter-bar.component';

@Component({
  selector: 'app-vet-visit-list',
  standalone: true,
  imports: [HasPermissionDirective, CanPipe, ErrorStateComponent, MoneyPipe, 
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    MatTooltipModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent,
    TranslatePipe,
    HealthNavComponent,
    HealthFilterBarComponent
  ],
  templateUrl: './vet-visit-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VetVisitListComponent {
  private readonly notify = inject(NotificationService);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private pdfExportService = inject(PdfExportService);
  private dialog = inject(MatDialog);
  private readonly i18n = inject(I18nService);

  /** Empty-state text; kept here because the apostrophe cannot sit inside a template string literal. */
  readonly emptyDescription = "Log professional vet visits to track your farm's health history.";

  @ViewChild('reportSheet') reportSheet?: ElementRef<HTMLElement>;
  readonly isExporting = signal(false);

  displayedColumns: string[] = ['visitDate', 'vetName', 'visitType', 'purpose', 'cost', 'nextVisit', 'actions'];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  // State
  pageIndex = signal(0);
  pageSize = signal(12);
  readonly search = signal('');
  refreshTrigger = signal(0);
  isLoading = signal(true);
  readonly loadError = signal<string | null>(null);

  retryLoad(): void {
    this.refreshTrigger.update(v => v + 1);
  }


  private currentFarm = toSignal(this.contextService.currentFarm$);

  private paginationParams = computed(() => ({
    pageIndex: this.pageIndex(),
    pageSize: this.pageSize(),
    farmId: this.currentFarm()?.id,
    search: this.search() || undefined,
    refresh: this.refreshTrigger()
  }));

  private vetVisitsResult = toSignal(
    toObservable(this.paginationParams).pipe(
      tap(() => { this.isLoading.set(true); this.loadError.set(null); }),
      switchMap(({ pageIndex, pageSize, farmId, search }) =>
        this.healthService.getVetVisits({ pageNumber: pageIndex + 1, pageSize, farmId, search }).pipe(
          catchError(err => {
            this.loadError.set(parseApiError(err, 'Could not load vet visits.'));
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

  setSearch(value: string): void {
    this.search.set(value);
    this.pageIndex.set(0);
  }

  loadVetVisits(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  openScheduleVisitDialog(): void {
    const dialogRef = this.dialog.open(LogVetVisitDialog, { maxWidth: '95vw',
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
    const dialogRef = this.dialog.open(VetVisitDetailDialogComponent, { maxWidth: '95vw',
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
        filename: `Farm360_Vet_Visits_${toLocalIsoDate()}`,
        orientation: 'landscape',
        header: {
          title: this.i18n.t('Vet Visits'),
          subtitle: this.i18n.t('Vet visits, findings and costs'),
          farmName: farm?.name || this.i18n.t('Primary Farm'),
          orgName: org?.name || 'Farm360',
          currency: 'BDT (৳)',
          metaFields: [
            { label: this.i18n.t('Total visits'), value: `${total}` },
            { label: this.i18n.t('In this PDF'), value: `${items.length}` }
          ]
        },
        showSignatures: true
      });
    } catch (err) {
      this.notify.error(parseApiError(err, 'Could not export the PDF. Please try again.'));
    } finally {
      this.isExporting.set(false);
    }
  }
}
