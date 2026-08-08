import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { 
  AnimalHealthHistoryDto, 
  VaccinationEventDto, 
  VaccinationProtocolDto,
  MedicalTreatmentDto,
  DiseaseIncidentDto,
  MortalityRecordDto,
  VetVisitDto,
  HealthDashboardDto,
  TreatmentStatus,
  IncidentStatus,
  PagedResult,
  VaccinationProtocolParams,
  MedicalTreatmentParams,
  DiseaseIncidentParams,
  MortalityRecordParams,
  VetVisitParams
} from '../models/health.models';

@Injectable({
  providedIn: 'root'
})
export class HealthService {
  private readonly http: HttpClient = inject(HttpClient);
  private readonly apiUrl = '/api/v1/health';

  // --- Dashboard ---
  getHealthDashboard(farmId?: string): Observable<HealthDashboardDto> {
    let params = new HttpParams();
    if (farmId) params = params.set('farmId', farmId);
    return this.http.get<HealthDashboardDto>(`${this.apiUrl}/dashboard`, { params });
  }

  // --- Vaccination Protocols ---
  getVaccinationProtocols(params: VaccinationProtocolParams = {}): Observable<PagedResult<VaccinationProtocolDto>> {
    let httpParams = new HttpParams();
    if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize)   httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.farmId)     httpParams = httpParams.set('farmId', params.farmId);
    if (params.search)     httpParams = httpParams.set('search', params.search);
    if (params.sortBy)     httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDesc != null) httpParams = httpParams.set('sortDesc', params.sortDesc);
    
    return this.http.get<PagedResult<VaccinationProtocolDto>>(`${this.apiUrl}/protocols`, { params: httpParams });
  }

  getVaccinationProtocol(id: string): Observable<VaccinationProtocolDto> {
    return this.http.get<VaccinationProtocolDto>(`${this.apiUrl}/protocols/${id}`);
  }

  createVaccinationProtocol(data: any): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.apiUrl}/protocols`, data);
  }

  updateVaccinationProtocol(id: string, data: any): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/protocols/${id}`, data);
  }

  assignProtocolToAnimals(data: any): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/protocols/assign`, data);
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

  batchAdministerVaccination(animalIds: string[], vaccineName: string, batchNumber: string, administeredDate: string, notes?: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/vaccinations/batch`, {
      animalIds,
      vaccineName,
      batchNumber,
      administeredDate,
      notes
    });
  }

  // --- Treatments ---
  getTreatments(params: MedicalTreatmentParams = {}): Observable<PagedResult<MedicalTreatmentDto>> {
    let httpParams = new HttpParams();
    if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize)   httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.farmId)     httpParams = httpParams.set('farmId', params.farmId);
    if (params.animalId)   httpParams = httpParams.set('animalId', params.animalId);
    if (params.status)     httpParams = httpParams.set('status', params.status);
    if (params.search)     httpParams = httpParams.set('search', params.search);
    if (params.sortBy)     httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDesc != null) httpParams = httpParams.set('sortDesc', params.sortDesc);

    return this.http.get<PagedResult<MedicalTreatmentDto>>(`${this.apiUrl}/treatments`, { params: httpParams });
  }

  logTreatment(data: any): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.apiUrl}/treatments`, data);
  }

  updateTreatmentStatus(id: string, status: TreatmentStatus, notes?: string): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/treatments/${id}/status`, { status, notes });
  }

  getIncidents(params: DiseaseIncidentParams = {}): Observable<PagedResult<DiseaseIncidentDto>> {
    let httpParams = new HttpParams();
    if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize)   httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.farmId)     httpParams = httpParams.set('farmId', params.farmId);
    if (params.status != null)   httpParams = httpParams.set('status', params.status);
    if (params.severity != null) httpParams = httpParams.set('severity', params.severity);
    if (params.search)     httpParams = httpParams.set('search', params.search);
    if (params.sortBy)     httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDesc != null) httpParams = httpParams.set('sortDesc', params.sortDesc);

    return this.http.get<PagedResult<DiseaseIncidentDto>>(`${this.apiUrl}/incidents`, { params: httpParams });
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
  getMortalityRecords(params: MortalityRecordParams = {}): Observable<PagedResult<MortalityRecordDto>> {
    let httpParams = new HttpParams();
    if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize)   httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.farmId)     httpParams = httpParams.set('farmId', params.farmId);
    if (params.animalId)   httpParams = httpParams.set('animalId', params.animalId);
    if (params.reason)     httpParams = httpParams.set('reason', params.reason);
    if (params.search)     httpParams = httpParams.set('search', params.search);
    if (params.sortBy)     httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDesc != null) httpParams = httpParams.set('sortDesc', params.sortDesc);

    return this.http.get<PagedResult<MortalityRecordDto>>(`${this.apiUrl}/mortality`, { params: httpParams });
  }

  recordMortality(data: any): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.apiUrl}/mortality`, data);
  }

  // --- Vet Visits ---
  getVetVisits(params: VetVisitParams = {}): Observable<PagedResult<VetVisitDto>> {
    let httpParams = new HttpParams();
    if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize)   httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.farmId)     httpParams = httpParams.set('farmId', params.farmId);
    if (params.search)     httpParams = httpParams.set('search', params.search);
    if (params.sortBy)     httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDesc != null) httpParams = httpParams.set('sortDesc', params.sortDesc);

    return this.http.get<PagedResult<VetVisitDto>>(`${this.apiUrl}/vet-visits`, { params: httpParams });
  }

  createVetVisit(data: any): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.apiUrl}/vet-visits`, data);
  }

  getVetVisitDetail(id: string): Observable<VetVisitDto> {
    return this.http.get<VetVisitDto>(`${this.apiUrl}/vet-visits/${id}`);
  }

  updateVetVisit(id: string, data: any): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/vet-visits/${id}`, { id, ...data });
  }

  // --- Miscellaneous ---
  getAnimalHealthHistory(animalId: string): Observable<AnimalHealthHistoryDto> {
    return this.http.get<AnimalHealthHistoryDto>(`${this.apiUrl}/animals/${animalId}/summary`);
  }

  getDewormingCalendar(farmId: string, pageNumber = 1, pageSize = 10): Observable<any> {
    const params = new HttpParams()
      .set('farmId', farmId)
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<any>(`${this.apiUrl}/reports/deworming`, { params });
  }

  getMilkWithdrawals(farmId: string): Observable<any[]> {
    const params = new HttpParams().set('farmId', farmId);
    return this.http.get<any[]>(`${this.apiUrl}/reports/withdrawal`, { params });
  }
}
