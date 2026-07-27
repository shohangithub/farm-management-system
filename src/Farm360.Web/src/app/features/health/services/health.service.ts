import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PagedResult } from '../../../shared/models/paged-result.model';
import { 
  AnimalHealthHistoryDto, 
  VaccinationEventDto, 
  IncidentSeverity,
  VaccinationProtocolDto,
  MedicalTreatmentDto,
  DiseaseIncidentDto,
  MortalityRecordDto,
  VetVisitDto,
  HealthDashboardDto,
  TreatmentStatus,
  IncidentStatus
} from '../models/health.models';

@Injectable({
  providedIn: 'root'
})
export class HealthService {
  private http = inject(HttpClient);
  private apiUrl = '/api/v1/health';

  // --- Dashboard ---
  getHealthDashboard(): Observable<HealthDashboardDto> {
    return this.http.get<HealthDashboardDto>(`${this.apiUrl}/dashboard`);
  }

  // --- Vaccination Protocols ---
  getVaccinationProtocols(pageNumber = 1, pageSize = 10, searchTerm?: string): Observable<PagedResult<VaccinationProtocolDto>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    if (searchTerm) params = params.set('searchTerm', searchTerm);
    
    return this.http.get<PagedResult<VaccinationProtocolDto>>(`${this.apiUrl}/vaccination-protocols`, { params });
  }

  getVaccinationProtocol(id: string): Observable<VaccinationProtocolDto> {
    return this.http.get<VaccinationProtocolDto>(`${this.apiUrl}/vaccination-protocols/${id}`);
  }

  createVaccinationProtocol(data: any): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.apiUrl}/vaccination-protocols`, data);
  }

  assignProtocolToAnimals(data: any): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/vaccination-protocols/assign`, data);
  }

  // --- Vaccinations ---
  getUpcomingVaccinations(farmId: string, beforeDate: string): Observable<VaccinationEventDto[]> {
    const params = new HttpParams()
      .set('farmId', farmId)
      .set('beforeDate', beforeDate);
    
    return this.http.get<VaccinationEventDto[]>(`${this.apiUrl}/vaccinations/upcoming`, { params });
  }

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

  // --- Treatments ---
  getTreatments(pageNumber = 1, pageSize = 10, animalId?: string): Observable<PagedResult<MedicalTreatmentDto>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    if (animalId) params = params.set('animalId', animalId);

    return this.http.get<PagedResult<MedicalTreatmentDto>>(`${this.apiUrl}/treatments`, { params });
  }

  logTreatment(data: any): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.apiUrl}/treatments`, data);
  }

  updateTreatmentStatus(id: string, status: TreatmentStatus, notes?: string): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/treatments/${id}/status`, { status, notes });
  }

  getIncidents(pageNumber = 1, pageSize = 10): Observable<PagedResult<DiseaseIncidentDto>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<PagedResult<DiseaseIncidentDto>>(`${this.apiUrl}/incidents`, { params });
  }

  getIncidentDetails(id: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/incidents/${id}`);
  }

  reportIncident(data: any): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.apiUrl}/incidents`, data);
  }

  updateIncidentStatus(id: string, status: IncidentStatus, affectedAnimalCount: number, notes?: string): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/incidents/${id}/status`, { status, affectedAnimalCount, notes });
  }

  // --- Mortality Records ---
  getMortalityRecords(pageNumber = 1, pageSize = 10): Observable<PagedResult<MortalityRecordDto>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);

    return this.http.get<PagedResult<MortalityRecordDto>>(`${this.apiUrl}/mortality-records`, { params });
  }

  recordMortality(data: any): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.apiUrl}/mortality-records`, data);
  }

  // --- Vet Visits ---
  getVetVisits(pageNumber = 1, pageSize = 10, farmId?: string): Observable<PagedResult<VetVisitDto>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    if (farmId) params = params.set('farmId', farmId);

    return this.http.get<PagedResult<VetVisitDto>>(`${this.apiUrl}/vet-visits`, { params });
  }

  createVetVisit(data: any): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.apiUrl}/vet-visits`, data);
  }

  // --- Miscellaneous ---
  getAnimalHealthHistory(animalId: string): Observable<AnimalHealthHistoryDto> {
    return this.http.get<AnimalHealthHistoryDto>(`${this.apiUrl}/animals/${animalId}/history`);
  }

  getAnimalHealthReport(animalId: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/reports/animals/${animalId}`);
  }

  getDewormingCalendar(farmId: string, pageNumber = 1, pageSize = 10): Observable<any> {
    const params = new HttpParams()
      .set('farmId', farmId)
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<any>(`${this.apiUrl}/deworming/calendar`, { params });
  }

  getMilkWithdrawals(farmId: string): Observable<any[]> {
    const params = new HttpParams().set('farmId', farmId);
    return this.http.get<any[]>(`${this.apiUrl}/reports/withdrawals`, { params });
  }
}
