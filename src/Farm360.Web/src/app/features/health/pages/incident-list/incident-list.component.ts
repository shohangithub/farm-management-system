import { Component, inject, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { HealthService } from '../../services/health.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { DiseaseIncident, IncidentSeverity, IncidentStatus } from '../../models/health.models';
import { ReportIncidentDialog } from '../../components/dialogs/report-incident-dialog/report-incident-dialog.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-incident-list',
  standalone: true,
  imports: [ErrorStateComponent, CommonModule, RouterModule, MatIconModule, MatDialogModule, PageHeaderComponent, EmptyStateComponent, LoadingComponent, TranslatePipe],
  template: `
<app-page-header 
  [title]="'Disease Incidents' | t"
  [description]="'Track and manage disease outbreaks and incidents.' | t"
  icon="coronavirus" 
  iconColor="text-rose-600">
  <div actions class="flex gap-2">
    <button (click)="reportIncident()" class="px-4 py-2 text-sm font-semibold text-white bg-rose-600 hover:bg-rose-700 rounded-lg transition-colors shadow-sm flex items-center gap-1.5">
      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> {{ 'Report Incident' | t }}
    </button>
  </div>
</app-page-header>

<div class="bg-white/80 dark:bg-surface-dark/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
  <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>
  <app-error-state *ngIf="!isLoading() && loadError()" class="block p-6" [message]="loadError()!" (retry)="retryLoad()"></app-error-state>

  <div class="relative overflow-x-auto">
    <table class="w-full text-sm text-left" *ngIf="incidents().length > 0">
      <thead class="text-xs text-gray-700 uppercase bg-gray-50 border-b border-gray-100 dark:bg-gray-800 dark:text-gray-400 dark:border-gray-700 font-bold tracking-wider">
        <tr>
          <th scope="col" class="px-6 py-4">{{ 'Date' | t }}</th>
          <th scope="col" class="px-6 py-4">{{ 'Disease' | t }}</th>
          <th scope="col" class="px-6 py-4">{{ 'Severity' | t }}</th>
          <th scope="col" class="px-6 py-4">{{ 'Affected Animals' | t }}</th>
          <th scope="col" class="px-6 py-4">{{ 'Status' | t }}</th>
          <th scope="col" class="px-6 py-4 text-right">{{ 'Actions' | t }}</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let incident of incidents()" class="border-b border-gray-50 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
          <td class="px-6 py-4 text-gray-600 dark:text-gray-400">
            {{ incident.incidentDate | date:'mediumDate' }}
          </td>
          <td class="px-6 py-4 font-medium text-gray-900 dark:text-white">
            {{ incident.diseaseName }}
          </td>
          <td class="px-6 py-4">
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium" [ngClass]="getSeverityClass(incident.severity)">
              {{ getSeverityName(incident.severity) | t }}
            </span>
          </td>
          <td class="px-6 py-4 text-gray-700 dark:text-gray-300">
            {{ incident.affectedAnimalCount }}
          </td>
          <td class="px-6 py-4">
            <label [for]="'status-' + incident.id" class="sr-only">{{ 'Status' | t }}</label>
            <select [id]="'status-' + incident.id"
              class="text-xs font-medium rounded-full border-0 px-2.5 py-1 cursor-pointer disabled:opacity-50"
              [ngClass]="getStatusClass(incident.status)"
              [disabled]="savingId() === incident.id"
              (change)="changeStatus(incident, $any($event.target).value, $event)">
              <option *ngFor="let s of statusOptions" [value]="s" [selected]="s === incident.status">{{ getStatusName(s) | t }}</option>
            </select>
            <p *ngIf="statusErrorId() === incident.id && statusError()" class="mt-1 text-xs text-rose-600 dark:text-rose-400">{{ statusError() }}</p>
          </td>
          <td class="px-6 py-4 text-right space-x-2">
            <a [routerLink]="['/health/incidents', incident.id]" class="px-3 py-1.5 text-xs font-semibold text-indigo-700 bg-indigo-50 hover:bg-indigo-100 border border-indigo-200 rounded-md transition-colors shadow-sm inline-block">
              {{ 'Details' | t }}
            </a>
          </td>
        </tr>
      </tbody>
    </table>
    
    <app-empty-state 
      *ngIf="!isLoading() && incidents().length === 0 && !loadError()"
      icon="health_and_safety"
      [title]="'No Incidents' | t"
      [description]="'No disease incidents have been reported.' | t"
      [actionLabel]="'Report Incident' | t"
      (action)="reportIncident()">
    </app-empty-state>
  </div>
</div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IncidentListComponent {
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private dialog = inject(MatDialog);

  isLoading = signal(true);
  readonly loadError = signal<string | null>(null);

  retryLoad(): void {
    this.refreshTrigger.update(v => v + 1);
  }

  private refreshTrigger = signal(0);
  private currentFarm = toSignal(this.contextService.currentFarm$);
  
  private fetchParams = computed(() => ({
    farmId: this.currentFarm()?.id,
    refresh: this.refreshTrigger()
  }));

  private incidentsResult = toSignal(
    toObservable(this.fetchParams).pipe(
      tap(() => { this.isLoading.set(true); this.loadError.set(null); }),
      switchMap(({ farmId }) => this.healthService.getIncidents({ pageNumber: 1, pageSize: 50, farmId }).pipe(
        catchError(err => {
            this.loadError.set(parseApiError(err, 'Could not load incidents.'));
            return of({ items: [], totalCount: 0 });
          })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { items: [], totalCount: 0 } }
  );

  incidents = computed(() => this.incidentsResult().items);
  totalCount = computed(() => this.incidentsResult().totalCount);

  reportIncident() {
    const dialogRef = this.dialog.open(ReportIncidentDialog, { maxWidth: '95vw', disableClose: true, width: '720px' });
    dialogRef.afterClosed().subscribe(result => {
      if (result) this.loadIncidents();
    });
  }

  loadIncidents() {
    this.refreshTrigger.update(v => v + 1);
  }

  readonly statusOptions: IncidentStatus[] = [
    IncidentStatus.Reported,
    IncidentStatus.UnderTreatment,
    IncidentStatus.Contained,
    IncidentStatus.Resolved
  ];
  readonly savingId = signal<string | null>(null);
  readonly statusErrorId = signal<string | null>(null);
  readonly statusError = signal<string | null>(null);

  /** Quick status change from the row. The affected count is sent unchanged (the endpoint sets both). */
  changeStatus(incident: DiseaseIncident, status: IncidentStatus, event: Event): void {
    if (status === incident.status) return;
    const select = event.target as HTMLSelectElement;

    this.savingId.set(incident.id);
    this.statusErrorId.set(null);
    this.statusError.set(null);
    this.healthService.updateIncidentStatus(incident.id, status, incident.affectedAnimalCount).subscribe({
      next: () => {
        this.savingId.set(null);
        this.loadIncidents();
      },
      error: (err) => {
        this.savingId.set(null);
        // Put the dropdown back to the saved status.
        select.value = incident.status;
        this.statusErrorId.set(incident.id);
        this.statusError.set(parseApiError(err, 'Could not update the status.'));
      }
    });
  }

  getSeverityName(severity: IncidentSeverity): string {
    switch (severity) {
      case IncidentSeverity.Mild: return 'Mild';
      case IncidentSeverity.Moderate: return 'Moderate';
      case IncidentSeverity.Severe: return 'Severe';
      case IncidentSeverity.Critical: return 'Critical';
      default: return 'Unknown';
    }
  }

  getSeverityClass(severity: IncidentSeverity): string {
    switch (severity) {
      case IncidentSeverity.Mild: return 'bg-emerald-100 dark:bg-emerald-400/30 text-emerald-600 dark:text-emerald-400';
      case IncidentSeverity.Moderate: return 'bg-amber-100 dark:bg-amber-400/30 text-amber-600 dark:text-amber-400';
      case IncidentSeverity.Severe: return 'bg-rose-100 dark:bg-rose-500/30 text-rose-500 dark:text-rose-400';
      case IncidentSeverity.Critical: return 'bg-rose-500 text-white';
      default: return 'bg-slate-100 text-slate-500';
    }
  }

  getStatusName(status: IncidentStatus): string {
    switch (status) {
      case IncidentStatus.Reported: return 'Reported';
      case IncidentStatus.UnderTreatment: return 'Under Treatment';
      case IncidentStatus.Contained: return 'Contained';
      case IncidentStatus.Resolved: return 'Resolved';
      default: return 'Unknown';
    }
  }

  getStatusClass(status: IncidentStatus): string {
    switch (status) {
      case IncidentStatus.Reported: return 'bg-amber-100 dark:bg-amber-400/30 text-amber-600 dark:text-amber-400';
      case IncidentStatus.UnderTreatment: return 'bg-indigo-100 dark:bg-indigo-500/30 text-indigo-600 dark:text-indigo-400';
      case IncidentStatus.Contained: return 'bg-blue-100 dark:bg-blue-500/30 text-blue-600 dark:text-blue-400';
      case IncidentStatus.Resolved: return 'bg-emerald-100 dark:bg-emerald-400/30 text-emerald-600 dark:text-emerald-400';
      default: return 'bg-slate-100 text-slate-500';
    }
  }
}
