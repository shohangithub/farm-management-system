import { Component, inject, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { HealthService } from '../../services/health.service';
import { DiseaseIncident, IncidentSeverity, IncidentStatus } from '../../models/health.models';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-incident-list',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, PageHeaderComponent, EmptyStateComponent, LoadingComponent],
  template: `
<app-page-header 
  title="Disease Incidents" 
  description="Track and manage disease outbreaks and incidents."
  icon="coronavirus" 
  iconColor="text-rose-600">
  <div actions class="flex gap-2">
    <button routerLink="/health/incidents/report" class="px-4 py-2 text-sm font-semibold text-white bg-rose-600 hover:bg-rose-700 rounded-lg transition-colors shadow-sm flex items-center gap-1.5">
      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> Report Incident
    </button>
  </div>
</app-page-header>

<div class="bg-white/80 dark:bg-surface-dark/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
  <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

  <div class="relative overflow-x-auto">
    <table class="w-full text-sm text-left" *ngIf="incidents().length > 0">
      <thead class="text-xs text-gray-700 uppercase bg-gray-50 border-b border-gray-100 dark:bg-gray-800 dark:text-gray-400 dark:border-gray-700 font-bold tracking-wider">
        <tr>
          <th scope="col" class="px-6 py-4">Date</th>
          <th scope="col" class="px-6 py-4">Disease</th>
          <th scope="col" class="px-6 py-4">Severity</th>
          <th scope="col" class="px-6 py-4">Affected Animals</th>
          <th scope="col" class="px-6 py-4">Status</th>
          <th scope="col" class="px-6 py-4 text-right">Actions</th>
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
              {{ getSeverityName(incident.severity) }}
            </span>
          </td>
          <td class="px-6 py-4 text-gray-700 dark:text-gray-300">
            {{ incident.affectedAnimalCount }}
          </td>
          <td class="px-6 py-4">
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium" [ngClass]="getStatusClass(incident.status)">
              {{ getStatusName(incident.status) }}
            </span>
          </td>
          <td class="px-6 py-4 text-right space-x-2">
            <a [routerLink]="['/health/incidents', incident.id]" class="px-3 py-1.5 text-xs font-semibold text-indigo-700 bg-indigo-50 hover:bg-indigo-100 border border-indigo-200 rounded-md transition-colors shadow-sm inline-block">
              Details
            </a>
          </td>
        </tr>
      </tbody>
    </table>
    
    <app-empty-state 
      *ngIf="!isLoading() && incidents().length === 0"
      icon="health_and_safety"
      title="No Incidents"
      description="No disease incidents have been reported."
      actionLabel="Report Incident"
      (action)="reportIncident()">
    </app-empty-state>
  </div>
</div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IncidentListComponent {
  private healthService = inject(HealthService);
  private router = inject(Router);

  isLoading = signal(true);
  private refreshTrigger = signal(0);
  
  private fetchParams = computed(() => ({
    refresh: this.refreshTrigger()
  }));

  private incidentsResult = toSignal(
    toObservable(this.fetchParams).pipe(
      tap(() => this.isLoading.set(true)),
      switchMap(() => this.healthService.getIncidents(1, 50).pipe(
        catchError(() => of({ items: [], totalCount: 0 }))
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { items: [], totalCount: 0 } }
  );

  incidents = computed(() => this.incidentsResult().items);
  totalCount = computed(() => this.incidentsResult().totalCount);

  reportIncident() {
    this.router.navigate(['/health/incidents/report']);
  }

  loadIncidents() {
    this.refreshTrigger.update(v => v + 1);
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
      case IncidentStatus.Resolved: return 'Resolved';
      case IncidentStatus.Fatal: return 'Fatal';
      default: return 'Unknown';
    }
  }

  getStatusClass(status: IncidentStatus): string {
    switch (status) {
      case IncidentStatus.Reported: return 'bg-amber-100 dark:bg-amber-400/30 text-amber-600 dark:text-amber-400';
      case IncidentStatus.UnderTreatment: return 'bg-indigo-100 dark:bg-indigo-500/30 text-indigo-600 dark:text-indigo-400';
      case IncidentStatus.Resolved: return 'bg-emerald-100 dark:bg-emerald-400/30 text-emerald-600 dark:text-emerald-400';
      case IncidentStatus.Fatal: return 'bg-rose-100 dark:bg-rose-500/30 text-rose-500 dark:text-rose-400';
      default: return 'bg-slate-100 text-slate-500';
    }
  }
}
