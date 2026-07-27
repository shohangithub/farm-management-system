import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { HealthService } from '../../services/health.service';
import { DiseaseIncident, IncidentSeverity, IncidentStatus } from '../../models/health.models';

@Component({
  selector: 'app-incident-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="px-4 sm:px-6 lg:px-8 py-8 w-full max-w-9xl mx-auto">
      <!-- Page header -->
      <div class="sm:flex sm:justify-between sm:items-center mb-8">
        <div class="mb-4 sm:mb-0">
          <h1 class="text-2xl md:text-3xl text-slate-800 dark:text-slate-100 font-bold">Disease Incidents ✨</h1>
        </div>
        <div class="grid grid-flow-col sm:auto-cols-max justify-start sm:justify-end gap-2">
          <a routerLink="/health/incidents/report" class="btn bg-indigo-500 hover:bg-indigo-600 text-white">
            <svg class="w-4 h-4 fill-current opacity-50 shrink-0" viewBox="0 0 16 16">
              <path d="M15 7H9V1c0-.6-.4-1-1-1S7 .4 7 1v6H1c-.6 0-1 .4-1 1s.4 1 1 1h6v6c0 .6.4 1 1 1s1-.4 1-1V9h6c.6 0 1-.4 1-1s-.4-1-1-1z" />
            </svg>
            <span class="hidden xs:block ml-2">Report Incident</span>
          </a>
        </div>
      </div>

      <!-- Table -->
      <div class="bg-white dark:bg-slate-800 shadow-lg rounded-sm border border-slate-200 dark:border-slate-700">
        <header class="px-5 py-4">
          <h2 class="font-semibold text-slate-800 dark:text-slate-100">All Incidents <span class="text-slate-400 dark:text-slate-500 font-medium">{{ totalCount }}</span></h2>
        </header>
        <div class="overflow-x-auto">
          <table class="table-auto w-full dark:text-slate-300">
            <thead class="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400 bg-slate-50 dark:bg-slate-900/20 border-t border-b border-slate-200 dark:border-slate-700">
              <tr>
                <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Date</div></th>
                <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Disease</div></th>
                <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Severity</div></th>
                <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Affected Animals</div></th>
                <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-left">Status</div></th>
                <th class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap"><div class="font-semibold text-center">Actions</div></th>
              </tr>
            </thead>
            <tbody class="text-sm divide-y divide-slate-200 dark:divide-slate-700">
              <tr *ngFor="let incident of incidents">
                <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                  <div>{{ incident.incidentDate | date:'mediumDate' }}</div>
                </td>
                <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                  <div class="font-medium text-slate-800 dark:text-slate-100">{{ incident.diseaseName }}</div>
                </td>
                <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                  <div class="inline-flex font-medium rounded-full text-center px-2.5 py-0.5"
                       [ngClass]="getSeverityClass(incident.severity)">
                    {{ getSeverityName(incident.severity) }}
                  </div>
                </td>
                <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                  <div class="text-left">{{ incident.affectedAnimalCount }}</div>
                </td>
                <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap">
                  <div class="inline-flex font-medium rounded-full text-center px-2.5 py-0.5"
                       [ngClass]="getStatusClass(incident.status)">
                    {{ getStatusName(incident.status) }}
                  </div>
                </td>
                <td class="px-2 first:pl-5 last:pr-5 py-3 whitespace-nowrap w-px">
                  <div class="text-center">
                    <a [routerLink]="['/health/incidents', incident.id]" class="text-indigo-500 hover:text-indigo-600">Details</a>
                  </div>
                </td>
              </tr>
              <tr *ngIf="incidents.length === 0 && !isLoading">
                <td colspan="6" class="px-2 first:pl-5 last:pr-5 py-8 text-center text-slate-500 dark:text-slate-400">
                  No disease incidents found.
                </td>
              </tr>
              <tr *ngIf="isLoading">
                <td colspan="6" class="px-2 first:pl-5 last:pr-5 py-8 text-center text-slate-500 dark:text-slate-400">
                  Loading incidents...
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class IncidentListComponent implements OnInit {
  private healthService = inject(HealthService);

  incidents: DiseaseIncident[] = [];
  totalCount = 0;
  isLoading = false;

  ngOnInit() {
    this.loadIncidents();
  }

  loadIncidents() {
    this.isLoading = true;
    this.healthService.getIncidents(1, 50).subscribe({
      next: (res) => {
        this.incidents = res.items;
        this.totalCount = res.totalCount;
        this.isLoading = false;
      },
      error: () => {
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
