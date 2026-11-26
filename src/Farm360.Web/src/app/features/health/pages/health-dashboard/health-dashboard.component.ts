import { Component, inject, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HealthService } from '../../services/health.service';
import { DiseaseIncidentDto, IncidentStatus, MedicalTreatmentDto, TreatmentStatus, VaccinationEventDto } from '../../models/health.models';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ScheduleVaccinationDialog } from '../../components/dialogs/schedule-vaccination-dialog/schedule-vaccination-dialog.component';
import { LogTreatmentDialog } from '../../components/dialogs/log-treatment-dialog/log-treatment-dialog.component';
import { RecordMortalityDialog } from '../../components/dialogs/record-mortality-dialog/record-mortality-dialog.component';
import { ReportIncidentDialog } from '../../components/dialogs/report-incident-dialog/report-incident-dialog.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { forkJoin, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { parseApiError } from '../../../../core/utils/error-parser';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { HealthNavComponent } from '../../components/health-nav/health-nav.component';
import { AnimalTagLookupService } from '../../services/animal-tag-lookup.service';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';

@Component({
  selector: 'app-health-dashboard',
  standalone: true,
  imports: [MoneyPipe, CommonModule, RouterModule, MatCardModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatDialogModule, PageHeaderComponent, LoadingComponent, TranslatePipe, ErrorStateComponent, HealthNavComponent],
  templateUrl: './health-dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HealthDashboardComponent {
  private healthService = inject(HealthService);
  private dialog = inject(MatDialog);
  private contextService = inject(WorkingContextService);
  private tagLookup = inject(AnimalTagLookupService);

  // --- Reactive State (Signals) ---
  isLoading = signal(true);
  error = signal('');
  private refreshTrigger = signal(0);

  // Derive farm context as a signal
  private currentFarm = toSignal(this.contextService.currentFarm$);

  // Combined trigger for loading data
  private fetchTrigger = computed(() => ({
    farm: this.currentFarm(),
    refresh: this.refreshTrigger()
  }));

  /** Counts plus the short "to-do" lists shown under them, loaded together. */
  private readonly data = toSignal(
    toObservable(this.fetchTrigger).pipe(
      tap(() => {
        this.isLoading.set(true);
        this.error.set('');
      }),
      switchMap(({ farm }) => {
        if (!farm) {
          this.isLoading.set(false);
          return of(null);
        }
        const weekAhead = new Date();
        weekAhead.setDate(weekAhead.getDate() + 7);
        return forkJoin({
          counts: this.healthService.getHealthDashboard(farm.id),
          // The lists are a nice-to-have: if one fails the counts still show.
          vaccines: this.healthService.getUpcomingVaccinations(farm.id, toLocalIsoDate(weekAhead)).pipe(catchError(() => of([] as VaccinationEventDto[]))),
          treatments: this.healthService.getTreatments({ farmId: farm.id, status: TreatmentStatus.Ongoing, pageNumber: 1, pageSize: 5 })
            .pipe(map(r => r.items), catchError(() => of([] as MedicalTreatmentDto[]))),
          incidents: this.healthService.getIncidents({ farmId: farm.id, pageNumber: 1, pageSize: 20 })
            .pipe(map(r => r.items.filter(i => i.status !== IncidentStatus.Resolved).slice(0, 3)), catchError(() => of([] as DiseaseIncidentDto[])))
        }).pipe(
          catchError((err) => {
            this.error.set(parseApiError(err, 'Failed to load dashboard data. Please try again.'));
            return of(null);
          }),
          tap(() => this.isLoading.set(false))
        );
      })
    ),
    { initialValue: null }
  );

  readonly dashboardData = computed(() => this.data()?.counts ?? null);
  readonly vaccinesDue = computed(() =>
    [...(this.data()?.vaccines ?? [])].sort((a, b) => a.scheduledDate.localeCompare(b.scheduledDate)).slice(0, 5));
  readonly ongoingTreatments = computed(() => this.data()?.treatments ?? []);
  readonly openIncidents = computed(() => this.data()?.incidents ?? []);

  /** Ear tags for the vaccine list (the API row only carries the animal id). */
  readonly tags = toSignal(
    toObservable(this.vaccinesDue).pipe(switchMap(list => this.tagLookup.tagsFor(list.map(v => v.animalId)))),
    { initialValue: {} as Record<string, string> }
  );

  isOverdue(date: string): boolean {
    return date < toLocalIsoDate();
  }

  loadDashboard(): void {
    // Triggers reactivity naturally without manual CDR
    this.refreshTrigger.update(v => v + 1);
  }

  // --- Dialogs ---
  openScheduleVaccinationDialog(): void {
    const dialogRef = this.dialog.open(ScheduleVaccinationDialog, { maxWidth: '95vw', disableClose: true, width: '560px' });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadDashboard();
    });
  }

  openLogTreatmentDialog(): void {
    const dialogRef = this.dialog.open(LogTreatmentDialog, { maxWidth: '95vw', disableClose: true, width: '720px' });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadDashboard();
    });
  }

  openRecordMortalityDialog(): void {
    const dialogRef = this.dialog.open(RecordMortalityDialog, { maxWidth: '95vw', disableClose: true, width: '560px' });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadDashboard();
    });
  }

  openReportIncidentDialog(): void {
    const dialogRef = this.dialog.open(ReportIncidentDialog, { maxWidth: '95vw', disableClose: true, width: '720px' });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadDashboard();
    });
  }
}
