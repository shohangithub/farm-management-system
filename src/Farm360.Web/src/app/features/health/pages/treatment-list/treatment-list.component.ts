import { CanPipe } from '../../../../shared/pipes/can.pipe';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { Component, inject, ChangeDetectionStrategy, signal, computed, ViewChild, ElementRef } from '@angular/core';
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
import { PdfExportService } from '../../../../shared/services/pdf-export.service';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { HealthNavComponent } from '../../components/health-nav/health-nav.component';
import { HealthFilterBarComponent, HealthFilterOption } from '../../components/health-filter-bar/health-filter-bar.component';

@Component({
  selector: 'app-treatment-list',
  standalone: true,
  imports: [HasPermissionDirective, CanPipe, ErrorStateComponent, MoneyPipe, 
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
    LoadingComponent,
    TranslatePipe,
    HealthNavComponent,
    HealthFilterBarComponent
  ],
  templateUrl: './treatment-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TreatmentListComponent {
  private readonly notify = inject(NotificationService);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private pdfExportService = inject(PdfExportService);
  private dialog = inject(MatDialog);
  private readonly i18n = inject(I18nService);

  @ViewChild('reportSheet') reportSheet?: ElementRef<HTMLElement>;
  readonly isExporting = signal(false);

  displayedColumns: string[] = ['animalId', 'diagnosis', 'medicationName', 'startDate', 'status', 'cost', 'actions'];
  treatmentStatus = TreatmentStatus;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  // State
  pageIndex = signal(0);
  pageSize = signal(12);
  readonly statusFilter = signal('');
  readonly search = signal('');
  readonly isFiltered = computed(() => !!this.statusFilter() || !!this.search());
  readonly statusOptions: HealthFilterOption[] = [
    { value: '', label: 'All' },
    { value: TreatmentStatus.Ongoing, label: 'Ongoing' },
    { value: TreatmentStatus.Completed, label: 'Completed' },
    { value: TreatmentStatus.Failed, label: 'Failed' },
  ];
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
    status: (this.statusFilter() || undefined) as TreatmentStatus | undefined,
    search: this.search() || undefined,
    refresh: this.refreshTrigger()
  }));

  private treatmentsResult = toSignal(
    toObservable(this.paginationParams).pipe(
      tap(() => { this.isLoading.set(true); this.loadError.set(null); }),
      switchMap(({ pageIndex, pageSize, farmId, status, search }) =>
        this.healthService.getTreatments({ pageNumber: pageIndex + 1, pageSize, farmId, status, search }).pipe(
          catchError(err => {
            this.loadError.set(parseApiError(err, 'Could not load treatments.'));
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

  setStatus(value: string): void {
    this.statusFilter.set(value);
    this.pageIndex.set(0);
  }

  setSearch(value: string): void {
    this.search.set(value);
    this.pageIndex.set(0);
  }

  /** Last day the animal must not go for slaughter, while the meat withdrawal is still running. */
  meatSafeDate(t: MedicalTreatmentDto): Date | null {
    if (!t.meatWithdrawalDays || !t.startDate) return null;
    const safe = new Date(t.startDate);
    safe.setDate(safe.getDate() + t.meatWithdrawalDays);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return safe >= today ? safe : null;
  }

  loadTreatments(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  openLogTreatmentDialog(): void {
    const dialogRef = this.dialog.open(LogTreatmentDialog, { maxWidth: '95vw', 
      disableClose: true,
      width: '720px'
    });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadTreatments();
    });
  }

  openEditTreatmentDialog(treatment: MedicalTreatmentDto): void {
    const dialogRef = this.dialog.open(LogTreatmentDialog, { maxWidth: '95vw',
      disableClose: true,
      width: '720px',
      data: { treatment }
    });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadTreatments();
    });
  }

  updateStatus(treatment: MedicalTreatmentDto, status: TreatmentStatus): void {
    this.healthService.updateTreatmentStatus(treatment.id, status, 'Status updated via list view').subscribe({
      next: () => this.loadTreatments(),
      error: (err) => this.notify.error(parseApiError(err, 'Could not update the treatment status.'))
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
        filename: `Farm360_Medical_Treatments_${toLocalIsoDate()}`,
        orientation: 'landscape',
        header: {
          title: this.i18n.t('Treatments'),
          subtitle: this.i18n.t('Medicines given to sick animals'),
          farmName: farm?.name || this.i18n.t('Primary Farm'),
          orgName: org?.name || 'Farm360',
          currency: 'BDT (৳)',
          metaFields: [
            { label: this.i18n.t('Total treatments'), value: `${total}` },
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
