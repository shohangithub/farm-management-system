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

import { HealthNavComponent } from '../../components/health-nav/health-nav.component';
import { HealthFilterBarComponent, HealthFilterOption } from '../../components/health-filter-bar/health-filter-bar.component';

@Component({
  selector: 'app-incident-list',
  standalone: true,
  imports: [ErrorStateComponent, CommonModule, RouterModule, MatIconModule, MatDialogModule, PageHeaderComponent, EmptyStateComponent, LoadingComponent, TranslatePipe, HealthNavComponent, HealthFilterBarComponent],
  template: `
<app-page-header
  [title]="'Disease Incidents' | t"
  [description]="'Outbreaks on the farm and how far they have spread.' | t">
  <div actions class="flex flex-wrap items-center gap-2">
    <button type="button" (click)="reportIncident()"
      class="inline-flex items-center gap-1.5 h-10 px-4 text-sm font-semibold rounded-xl bg-primary-600 hover:bg-primary-700 text-white shadow-sm transition-colors">
      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> {{ 'Report Incident' | t }}
    </button>
  </div>
</app-page-header>

<app-health-nav></app-health-nav>

<app-health-filter-bar
  [placeholder]="'Search disease'"
  [options]="openOptions"
  [selected]="openFilter()"
  (selectedChange)="openFilter.set($event)"
  (searchChange)="search.set($event)">
</app-health-filter-bar>

<div class="relative min-h-[200px]">
  <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>
  <app-error-state *ngIf="!isLoading() && loadError()" class="block" [message]="loadError()!" (retry)="retryLoad()"></app-error-state>

  <div *ngIf="!isLoading() && !loadError() && visible().length === 0">
    <app-empty-state
      icon="health_and_safety"
      [title]="(!incidents().length ? 'No disease incidents' : (openFilter() === 'open' && !search() ? 'No active outbreaks' : 'Nothing matches this filter')) | t"
      [description]="(!incidents().length ? 'Report one when several animals fall sick with the same disease.' : (openFilter() === 'open' && !search() ? 'Every reported incident is resolved.' : 'Try another word or filter.')) | t"
      [actionLabel]="incidents().length ? undefined : ('Report Incident' | t)"
      (action)="reportIncident()">
    </app-empty-state>
  </div>

  <div *ngIf="!isLoading() && visible().length > 0" class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
    <div *ngFor="let incident of visible()"
         class="bg-white dark:bg-gray-800/80 rounded-2xl border border-gray-200/70 dark:border-gray-700/60 shadow-sm flex flex-col min-w-0">
      <div class="p-4 flex items-start gap-3">
        <div class="w-10 h-10 shrink-0 rounded-xl flex items-center justify-center bg-orange-50 text-orange-600 dark:bg-orange-500/10 dark:text-orange-400">
          <mat-icon class="!w-5 !h-5 !text-[20px]">coronavirus</mat-icon>
        </div>
        <div class="min-w-0 flex-1">
          <h3 class="font-semibold text-[15px] leading-snug text-gray-900 dark:text-white break-words">{{ incident.diseaseName }}</h3>
          <p class="mt-0.5 text-xs text-gray-500 dark:text-gray-400">{{ incident.incidentDate | date:'mediumDate' }}</p>
        </div>
        <span class="shrink-0 inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold" [ngClass]="getSeverityClass(incident.severity)">
          {{ getSeverityName(incident.severity) | t }}
        </span>
      </div>

      <dl class="px-4 pb-4 grid grid-cols-2 gap-x-3 gap-y-3 text-sm">
        <div>
          <dt class="text-[11px] font-medium text-gray-500 dark:text-gray-400">{{ 'Affected Animals' | t }}</dt>
          <dd class="font-semibold text-gray-900 dark:text-white">{{ incident.affectedAnimalCount }}</dd>
        </div>
        <div>
          <dt class="text-[11px] font-medium text-gray-500 dark:text-gray-400"><label [for]="'status-' + incident.id">{{ 'Status' | t }}</label></dt>
          <dd>
            <select [id]="'status-' + incident.id"
              class="text-xs font-semibold rounded-full border-0 pl-2.5 pr-7 py-1 cursor-pointer disabled:opacity-50"
              [ngClass]="getStatusClass(incident.status)"
              [disabled]="savingId() === incident.id"
              (change)="changeStatus(incident, $any($event.target).value, $event)">
              <option *ngFor="let s of statusOptions" [value]="s" [selected]="s === incident.status">{{ getStatusName(s) | t }}</option>
            </select>
            <p *ngIf="statusErrorId() === incident.id && statusError()" class="mt-1 text-xs text-rose-600 dark:text-rose-400">{{ statusError() }}</p>
          </dd>
        </div>
      </dl>

      <div class="mt-auto px-4 py-3 border-t border-gray-100 dark:border-gray-700/60 flex gap-2">
        <a [routerLink]="['/health/incidents', incident.id]"
          class="flex-1 inline-flex items-center justify-center gap-1 h-10 px-3 text-[13px] font-semibold rounded-lg border border-gray-200 dark:border-gray-700 text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">visibility</mat-icon> {{ 'View Details' | t }}
        </a>
      </div>
    </div>
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

  // Filters (applied in the browser to the loaded list)
  readonly search = signal('');
  readonly openFilter = signal('open');
  readonly openOptions: HealthFilterOption[] = [
    { value: 'open', label: 'Still active' },
    { value: 'resolved', label: 'Resolved' },
    { value: '', label: 'All' },
  ];
  readonly visible = computed(() => {
    const term = this.search().toLowerCase();
    const which = this.openFilter();
    return this.incidents().filter(i => {
      const resolved = i.status === IncidentStatus.Resolved;
      if (which === 'open' && resolved) return false;
      if (which === 'resolved' && !resolved) return false;
      return !term || i.diseaseName.toLowerCase().includes(term);
    });
  });

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
      case IncidentSeverity.Mild: return 'bg-emerald-50 dark:bg-emerald-500/10 text-emerald-700 dark:text-emerald-300';
      case IncidentSeverity.Moderate: return 'bg-amber-50 dark:bg-amber-500/10 text-amber-700 dark:text-amber-300';
      case IncidentSeverity.Severe: return 'bg-rose-50 dark:bg-rose-500/10 text-rose-700 dark:text-rose-300';
      case IncidentSeverity.Critical: return 'bg-rose-500 text-white';
      default: return 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300';
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
      case IncidentStatus.Reported: return 'bg-amber-50 dark:bg-amber-500/10 text-amber-700 dark:text-amber-300';
      case IncidentStatus.UnderTreatment: return 'bg-blue-50 dark:bg-blue-500/10 text-blue-700 dark:text-blue-300';
      case IncidentStatus.Contained: return 'bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-200';
      case IncidentStatus.Resolved: return 'bg-emerald-50 dark:bg-emerald-500/10 text-emerald-700 dark:text-emerald-300';
      default: return 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300';
    }
  }
}
