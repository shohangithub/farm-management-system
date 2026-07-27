import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { HealthService } from '../../services/health.service';
import { DiseaseIncidentDetail, IncidentSeverity, IncidentStatus } from '../../models/health.models';

@Component({
  selector: 'app-incident-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="px-4 sm:px-6 lg:px-8 py-8 w-full max-w-9xl mx-auto" *ngIf="incident">
      
      <!-- Page header -->
      <div class="mb-8">
        <div class="flex items-center space-x-2">
          <a routerLink="/health/incidents" class="text-slate-500 hover:text-indigo-500">
            <svg class="w-6 h-6 fill-current" viewBox="0 0 24 24">
              <path d="M10.7 18.7l-7.4-7.4 7.4-7.4 1.4 1.4-5 5H20v2H7.1l5 5z" />
            </svg>
          </a>
          <h1 class="text-2xl md:text-3xl text-slate-800 dark:text-slate-100 font-bold">Incident Details ✨</h1>
        </div>
      </div>

      <!-- Main content -->
      <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
        
        <!-- Left column -->
        <div class="md:col-span-2 space-y-6">
          <div class="bg-white dark:bg-slate-800 shadow-lg rounded-sm border border-slate-200 dark:border-slate-700 p-5">
            <h2 class="font-semibold text-slate-800 dark:text-slate-100 mb-4">Overview</h2>
            
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <div class="text-sm text-slate-500 dark:text-slate-400">Disease Name</div>
                <div class="font-medium text-slate-800 dark:text-slate-100">{{ incident.diseaseName }}</div>
              </div>
              <div>
                <div class="text-sm text-slate-500 dark:text-slate-400">Date Reported</div>
                <div class="font-medium text-slate-800 dark:text-slate-100">{{ incident.incidentDate | date:'longDate' }}</div>
              </div>
              <div>
                <div class="text-sm text-slate-500 dark:text-slate-400">Severity</div>
                <div class="mt-1 inline-flex font-medium rounded-full text-center px-2.5 py-0.5" [ngClass]="getSeverityClass(incident.severity)">
                  {{ getSeverityName(incident.severity) }}
                </div>
              </div>
              <div>
                <div class="text-sm text-slate-500 dark:text-slate-400">Status</div>
                <div class="mt-1 inline-flex font-medium rounded-full text-center px-2.5 py-0.5" [ngClass]="getStatusClass(incident.status)">
                  {{ getStatusName(incident.status) }}
                </div>
              </div>
            </div>

            <div class="mt-6">
              <div class="text-sm text-slate-500 dark:text-slate-400">Symptoms</div>
              <p class="mt-1 text-slate-800 dark:text-slate-100">{{ incident.symptoms }}</p>
            </div>
            
            <div class="mt-4" *ngIf="incident.notes">
              <div class="text-sm text-slate-500 dark:text-slate-400">Notes</div>
              <p class="mt-1 text-slate-800 dark:text-slate-100">{{ incident.notes }}</p>
            </div>
          </div>
        </div>

        <!-- Right column -->
        <div class="space-y-6">
          <div class="bg-white dark:bg-slate-800 shadow-lg rounded-sm border border-slate-200 dark:border-slate-700 p-5">
            <h2 class="font-semibold text-slate-800 dark:text-slate-100 mb-4">Affected Animals ({{ incident.affectedAnimals.length }})</h2>
            
            <ul class="space-y-3">
              <li *ngFor="let animal of incident.affectedAnimals" class="flex justify-between items-center">
                <div class="flex items-center">
                  <div class="w-8 h-8 rounded-full bg-slate-100 dark:bg-slate-700 flex items-center justify-center mr-3">
                    <svg class="w-4 h-4 fill-current text-slate-400 dark:text-slate-500" viewBox="0 0 16 16">
                      <path d="M8 0C3.6 0 0 3.6 0 8s3.6 8 8 8 8-3.6 8-8-3.6-8-8-8zm0 14c-3.3 0-6-2.7-6-6s2.7-6 6-6 6 2.7 6 6-2.7 6-6 6z"/>
                    </svg>
                  </div>
                  <div>
                    <div class="text-sm font-medium text-slate-800 dark:text-slate-100">Tag: {{ animal.tagNumber }}</div>
                    <div class="text-xs text-slate-500">{{ animal.species }} - {{ animal.breedName }}</div>
                  </div>
                </div>
              </li>
              <li *ngIf="incident.affectedAnimals.length === 0" class="text-sm text-slate-500">
                No individual animals explicitly tracked for this incident.
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
    
    <div *ngIf="isLoading" class="flex justify-center items-center h-screen">
      <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-indigo-500"></div>
    </div>
    
    <div *ngIf="error" class="px-4 py-8 max-w-3xl mx-auto">
      <div class="bg-rose-50 dark:bg-rose-900/30 text-rose-600 dark:text-rose-400 p-4 rounded-sm border border-rose-200 dark:border-rose-800">
        {{ error }}
      </div>
    </div>
  `
})
export class IncidentDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private healthService = inject(HealthService);

  incident: DiseaseIncidentDetail | null = null;
  isLoading = false;
  error = '';

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadIncident(id);
    }
  }

  loadIncident(id: string) {
    this.isLoading = true;
    this.healthService.getIncidentDetails(id).subscribe({
      next: (data) => {
        this.incident = data;
        this.isLoading = false;
      },
      error: (err) => {
        this.error = 'Failed to load incident details.';
        this.isLoading = false;
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
