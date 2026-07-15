import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { 
  AnimalHealthHistoryDto, 
  VaccinationEventDto, 
  IncidentSeverity 
} from '../models/health.models';

@Injectable({
  providedIn: 'root'
})
export class HealthService {
  private http = inject(HttpClient);
  private apiUrl = '/api/v1/health';

  scheduleVaccination(animalId: string, vaccineName: string, batchNumber: string, scheduledDate: string, notes?: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.apiUrl}/vaccinations/schedule`, {
      animalId,
      vaccineName,
      batchNumber,
      scheduledDate,
      notes
    });
  }

  administerVaccination(id: string, administeredDate: string, notes?: string): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/vaccinations/${id}/administer`, {
      administeredDate,
      notes
    });
  }

  getUpcomingVaccinations(farmId: string, beforeDate: string): Observable<VaccinationEventDto[]> {
    const params = new HttpParams()
      .set('farmId', farmId)
      .set('beforeDate', beforeDate);
    
    return this.http.get<VaccinationEventDto[]>(`${this.apiUrl}/vaccinations/upcoming`, { params });
  }

  logTreatment(data: {
    animalId: string;
    diagnosis: string;
    medicationName: string;
    dosageAmount: number;
    dosageUnit: string;
    milkWithdrawalDays: number;
    meatWithdrawalDays: number;
    startDate: string;
    costBdt: number;
    veterinarianName?: string;
    notes?: string;
  }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.apiUrl}/treatments`, data);
  }

  reportIncident(data: {
    farmId: string;
    shedId?: string;
    diseaseName: string;
    severity: IncidentSeverity;
    incidentDate: string;
    symptoms: string;
    affectedAnimalCount: number;
    notes?: string;
  }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.apiUrl}/incidents`, data);
  }

  getAnimalHealthHistory(animalId: string): Observable<AnimalHealthHistoryDto> {
    return this.http.get<AnimalHealthHistoryDto>(`${this.apiUrl}/animals/${animalId}/history`);
  }
}
