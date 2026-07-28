import { Component, inject, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { HealthService } from '../../services/health.service';
import { DiseaseIncidentDetail, IncidentSeverity, IncidentStatus } from '../../models/health.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, tap, map } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-incident-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, MatButtonModule, MatTooltipModule, PageHeaderComponent, LoadingComponent],
  template: `
<app-page-header 
  title="Incident Details" 
  description="View comprehensive information about the disease incident."
  icon="coronavirus" 
  iconColor="text-rose-600">
  <div actions class="flex gap-2">
    <button routerLink="/health/incidents" class="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 rounded-lg transition-colors shadow-sm flex items-center gap-1.5">
      <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">arrow_back</mat-icon> Back to List
    </button>
  </div>
</app-page-header>

<div class="animate-fade-in relative">
  <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

  <div *ngIf="error()" class="bg-red-50 border-l-4 border-red-500 p-4 mb-6 rounded-md shadow-sm">
    <div class="flex">
      <mat-icon class="text-red-500 mr-2">error</mat-icon>
      <p class="text-sm text-red-700 font-medium">{{ error() }}</p>
    </div>
  </div>

  <div *ngIf="incident() && !isLoading()" class="grid grid-cols-1 md:grid-cols-3 gap-6">
    <!-- Left column -->
    <div class="md:col-span-2 space-y-6">
      <div class="bg-white/80 dark:bg-surface-dark/80 backdrop-blur-xl shadow-sm rounded-2xl border border-gray-100 dark:border-gray-800/50 p-6">
        <h2 class="text-lg font-bold text-gray-900 dark:text-white mb-4 flex items-center">
          <mat-icon class="mr-2 text-gray-400">info</mat-icon> Overview
        </h2>
        
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-6">
          <div class="bg-gray-50 dark:bg-gray-800/50 p-4 rounded-xl border border-gray-100 dark:border-gray-700/50">
            <div class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Disease Name</div>
            <div class="font-bold text-gray-900 dark:text-white text-lg">{{ incident()?.diseaseName }}</div>
          </div>
          <div class="bg-gray-50 dark:bg-gray-800/50 p-4 rounded-xl border border-gray-100 dark:border-gray-700/50">
            <div class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Date Reported</div>
            <div class="font-bold text-gray-900 dark:text-white text-lg">{{ incident()?.incidentDate | date:'longDate' }}</div>
          </div>
          <div class="bg-gray-50 dark:bg-gray-800/50 p-4 rounded-xl border border-gray-100 dark:border-gray-700/50">
            <div class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">Severity</div>
            <span class="inline-flex items-center px-2.5 py-1 rounded-md text-sm font-medium" *ngIf="incident()?.severity !== undefined" [ngClass]="getSeverityClass(incident()!.severity)">
              {{ getSeverityName(incident()!.severity) }}
            </span>
          </div>
          <div class="bg-gray-50 dark:bg-gray-800/50 p-4 rounded-xl border border-gray-100 dark:border-gray-700/50">
            <div class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">Status</div>
            <span class="inline-flex items-center px-2.5 py-1 rounded-md text-sm font-medium" *ngIf="incident()?.status !== undefined" [ngClass]="getStatusClass(incident()!.status)">
              {{ getStatusName(incident()!.status) }}
            </span>
          </div>
        </div>

        <div class="mt-6">
          <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Symptoms</h3>
          <div class="bg-gray-50 dark:bg-gray-800/50 p-4 rounded-xl border border-gray-100 dark:border-gray-700/50 text-gray-700 dark:text-gray-300">
            {{ incident()?.symptoms }}
          </div>
        </div>
        
        <div class="mt-6" *ngIf="incident()?.notes">
          <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Notes</h3>
          <div class="bg-yellow-50 dark:bg-yellow-900/10 p-4 rounded-xl border border-yellow-100 dark:border-yellow-700/30 text-gray-700 dark:text-gray-300 whitespace-pre-wrap">
            {{ incident()?.notes }}
          </div>
        </div>
      </div>
    </div>

    <!-- Right column -->
    <div class="space-y-6">
      <div class="bg-white/80 dark:bg-surface-dark/80 backdrop-blur-xl shadow-sm rounded-2xl border border-gray-100 dark:border-gray-800/50 p-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center">
            <mat-icon class="mr-2 text-gray-400">pets</mat-icon> Affected Animals
          </h2>
          <span class="bg-indigo-100 text-indigo-800 text-xs font-bold px-2.5 py-1 rounded-full">{{ incident()?.affectedAnimals?.length || 0 }}</span>
        </div>
        
        <ul class="space-y-3" *ngIf="incident()?.affectedAnimals?.length">
          <li *ngFor="let animal of incident()?.affectedAnimals" class="flex justify-between items-center bg-gray-50 dark:bg-gray-800/50 p-3 rounded-xl border border-gray-100 dark:border-gray-700/50 hover:bg-gray-100 transition-colors">
            <div class="flex items-center">
              <div class="w-10 h-10 rounded-full bg-indigo-100 dark:bg-indigo-900/30 flex items-center justify-center mr-3 border border-indigo-200 dark:border-indigo-800">
                <mat-icon class="text-indigo-600 dark:text-indigo-400">pets</mat-icon>
              </div>
              <div>
                <div class="text-sm font-bold text-gray-900 dark:text-white">{{ animal.tagNumber }}</div>
                <div class="text-xs text-gray-500 dark:text-gray-400">{{ animal.species }} - {{ animal.breedName }}</div>
              </div>
            </div>
            <button mat-icon-button color="primary" [routerLink]="['/livestock/animal', animal.animalId]" matTooltip="View Animal">
              <mat-icon>chevron_right</mat-icon>
            </button>
          </li>
        </ul>
        
        <div *ngIf="!incident()?.affectedAnimals?.length" class="text-center py-6 bg-gray-50 dark:bg-gray-800/50 rounded-xl border border-dashed border-gray-300 dark:border-gray-700">
          <mat-icon class="text-gray-400 mb-2">info_outline</mat-icon>
          <p class="text-sm text-gray-500">No individual animals explicitly tracked for this incident.</p>
        </div>
      </div>
    </div>
  </div>
</div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IncidentDetailComponent {
  private route = inject(ActivatedRoute);
  private healthService = inject(HealthService);

  isLoading = signal(true);
  error = signal('');
  private refreshTrigger = signal(0);

  private routeId = toSignal(
    this.route.paramMap.pipe(map(params => params.get('id'))),
    { initialValue: null }
  );

  private fetchParams = computed(() => ({
    id: this.routeId(),
    refresh: this.refreshTrigger()
  }));

  incident = toSignal(
    toObservable(this.fetchParams).pipe(
      tap(() => { this.isLoading.set(true); this.error.set(''); }),
      switchMap(({ id }) => {
        if (!id) {
          this.isLoading.set(false);
          return of(null);
        }
        return this.healthService.getIncidentDetails(id).pipe(
          catchError(() => {
            this.error.set('Failed to load incident details.');
            return of(null);
          }),
          tap(() => this.isLoading.set(false))
        );
      })
    ),
    { initialValue: null }
  );

  loadIncident() {
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
