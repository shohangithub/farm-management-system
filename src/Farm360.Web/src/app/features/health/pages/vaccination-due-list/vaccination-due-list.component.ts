import { CanPipe } from '../../../../shared/pipes/can.pipe';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { Component, inject, ChangeDetectionStrategy, signal, computed, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { HealthService } from '../../services/health.service';
import { VaccinationEventDto, VaccinationStatus } from '../../models/health.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ScheduleVaccinationDialog } from '../../components/dialogs/schedule-vaccination-dialog/schedule-vaccination-dialog.component';
import { PdfExportService } from '../../../../shared/services/pdf-export.service';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { NotificationService } from '../../../../shared/services/notification.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { HealthNavComponent } from '../../components/health-nav/health-nav.component';
import { HealthFilterBarComponent, HealthFilterOption } from '../../components/health-filter-bar/health-filter-bar.component';

@Component({
  selector: 'app-vaccination-due-list',
  standalone: true,
  imports: [HasPermissionDirective, CanPipe, ErrorStateComponent, CommonModule, RouterModule, FormsModule, MatIconModule, MatButtonModule, MatDialogModule, PageHeaderComponent, EmptyStateComponent, LoadingComponent, TranslatePipe, HealthNavComponent, HealthFilterBarComponent],
  templateUrl: './vaccination-due-list.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VaccinationDueListComponent {
  private readonly notify = inject(NotificationService);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private pdfExportService = inject(PdfExportService);
  private dialog = inject(MatDialog);
  private readonly i18n = inject(I18nService);

  @ViewChild('reportSheet') reportSheet?: ElementRef<HTMLElement>;
  readonly isExporting = signal(false);

  readonly VaccinationStatus = VaccinationStatus;

  // State
  isLoading = signal(true);
  error = signal('');

  retryLoad(): void {
    this.refreshTrigger.update(v => v + 1);
  }
  private refreshTrigger = signal(0);

  // Derive target date (30 days ahead)
  private dateStr = computed(() => {
    const d = new Date();
    d.setDate(d.getDate() + 30);
    return toLocalIsoDate(d);
  });

  // Current farm ID from context
  private currentFarmId = toSignal(this.contextService.currentFarm$, { initialValue: null });

  // Combined params for the fetch request
  private fetchParams = computed(() => ({
    farmId: this.currentFarmId()?.id || '',
    dateStr: this.dateStr(),
    refresh: this.refreshTrigger()
  }));

  // Reactive data stream
  upcomingVaccinations = toSignal(
    toObservable(this.fetchParams).pipe(
      tap(() => { this.isLoading.set(true); this.error.set(''); }),
      switchMap(({ farmId, dateStr }) => !farmId
        ? of([] as VaccinationEventDto[]).pipe(tap(() => this.isLoading.set(false)))
        : this.healthService.getUpcomingVaccinations(farmId, dateStr).pipe(
          catchError((err) => {
            this.error.set(parseApiError(err, 'Failed to load upcoming vaccinations.'));
            return of([]);
          }),
          tap(() => this.isLoading.set(false))
        )
      )
    ),
    { initialValue: [] as VaccinationEventDto[] }
  );

  // Filters (applied in the browser: the whole 30-day list is already loaded)
  readonly search = signal('');
  readonly whenFilter = signal('');
  readonly whenOptions: HealthFilterOption[] = [
    { value: '', label: 'All' },
    { value: 'overdue', label: 'Overdue' },
    { value: 'week', label: 'Due this week' },
  ];

  /** The list after filters, overdue first. */
  readonly visible = computed(() => {
    const term = this.search().toLowerCase();
    const when = this.whenFilter();
    return [...(this.upcomingVaccinations() || [])]
      .filter(v => {
        const urgency = this.getUrgencyClass(v.scheduledDate);
        if (when === 'overdue' && urgency !== 'danger-row') return false;
        if (when === 'week' && urgency === '') return false;
        if (!term) return true;
        return v.vaccineName.toLowerCase().includes(term) || (v.animalTag || '').toLowerCase().includes(term);
      })
      .sort((a, b) => a.scheduledDate.localeCompare(b.scheduledDate));
  });

  urgencyLabel(dateStr: string): string {
    switch (this.getUrgencyClass(dateStr)) {
      case 'danger-row': return 'Overdue';
      case 'warning-row': return 'Due today';
      case 'info-row': return 'This week';
      default: return 'Upcoming';
    }
  }

  getUrgencyClass(dateStr: string): string {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const dueDate = new Date(dateStr);
    dueDate.setHours(0, 0, 0, 0);

    const diffTime = dueDate.getTime() - today.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

    if (diffDays < 0) return 'danger-row'; // Overdue
    if (diffDays === 0) return 'warning-row'; // Today
    if (diffDays <= 7) return 'info-row'; // This week
    return '';
  }

  /** "Dose: 2 tablet for 290 kg" for weight-based doses, "Dose: 5 ml" for fixed ones. */
  doseText(ev: VaccinationEventDto): string | null {
    if (ev.kind === 'CareTask' || ev.dosageQuantity == null) return null;
    const unit = ev.doseUnit ? this.i18n.t(ev.doseUnit) : '';
    return ev.weightBased && ev.doseWeightKg
      ? this.i18n.t('Dose: {dose} {unit} for {kg} kg', { dose: ev.dosageQuantity, unit, kg: ev.doseWeightKg })
      : this.i18n.t('Dose: {dose} {unit}', { dose: ev.dosageQuantity, unit });
  }

  administer(id: string): void {
    const todayStr = toLocalIsoDate();
    this.healthService.administerVaccination(id, todayStr, 'Administered routinely')
      .subscribe({
        next: () => {
          this.refreshTrigger.update(v => v + 1);
        },
        error: (err) => {
          this.notify.error(parseApiError(err, 'Could not record the vaccination.'));
        }
      });
  }

  openRecordVaccinationDialog(): void {
    const dialogRef = this.dialog.open(ScheduleVaccinationDialog, { maxWidth: '95vw',
      width: '720px',
      panelClass: 'custom-dialog-container',
      disableClose: true
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.refreshTrigger.update(v => v + 1);
      }
    });
  }

  async exportPdf(): Promise<void> {
    const el = this.reportSheet?.nativeElement;
    if (!el) return;

    const list = this.visible();
    const farm = this.contextService.currentFarmValue;
    const org = this.contextService.currentOrgValue;

    this.isExporting.set(true);
    try {
      await this.pdfExportService.exportElement(el, {
        filename: `Farm360_Vaccination_Schedule_${toLocalIsoDate()}`,
        orientation: 'landscape',
        header: {
          title: this.i18n.t('Vaccinations Due'),
          subtitle: this.i18n.t('Vaccines due in the next 30 days'),
          farmName: farm?.name || this.i18n.t('Primary Farm'),
          orgName: org?.name || 'Farm360',
          currency: 'BDT (৳)',
          metaFields: [
            { label: this.i18n.t('Vaccines due'), value: `${list.length}` },
            { label: this.i18n.t('Period'), value: this.i18n.t('Next 30 Days') }
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
