import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {
  AnimalDto,
  AnimalPhotoDto,
  AnimalListParams,
  PagedAnimalListDto,
  RegisterAnimalRequest,
  RecordWeightRequest,
  SellAnimalRequest,
  QuarantineAnimalRequest,
  RecordDeathRequest,
  TransferAnimalRequest,
  AddPhotoRequest,
  WeightRecordDto,
  ConfirmPregnancyRequest,
  RecordCalvingRequest,
  BcsRecordDto
} from '../models/animal.models';

/**
 * Livestock API service.
 * All endpoints call Farm360.Api /api/v1/livestock/*
 * HttpClient is configured with base URL and JWT interceptor via app.config.ts
 */
@Injectable({ providedIn: 'root' })
export class AnimalService {
  // Explicit type annotation required by TypeScript 6 strict mode with inject()
  private readonly http: HttpClient = inject(HttpClient);
  private readonly base = '/api/v1/livestock/animals';

  // ── Queries ──────────────────────────────────────────────────────────────

  getList(params: AnimalListParams = {}): Observable<PagedAnimalListDto> {
    let httpParams = new HttpParams();

    if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize)   httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.farmId)     httpParams = httpParams.set('farmId', params.farmId);
    if (params.shedId)     httpParams = httpParams.set('shedId', params.shedId);
    if (params.penId)      httpParams = httpParams.set('penId', params.penId);
    if (params.species != null) httpParams = httpParams.set('species', params.species);
    if (params.sex != null)     httpParams = httpParams.set('sex', params.sex);
    if (params.status != null)  httpParams = httpParams.set('status', params.status);
    if (params.search)     httpParams = httpParams.set('search', params.search);
    if (params.sortBy)     httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDesc)   httpParams = httpParams.set('sortDesc', params.sortDesc);

    return this.http.get<PagedAnimalListDto>(this.base, { params: httpParams });
  }

  getById(id: string): Observable<AnimalDto> {
    return this.http.get<AnimalDto>(`${this.base}/${id}`);
  }

  getWeightHistory(id: string): Observable<WeightRecordDto[]> {
    return this.http.get<WeightRecordDto[]>(`${this.base}/${id}/weights`);
  }

  // ── Commands ─────────────────────────────────────────────────────────────

  register(request: RegisterAnimalRequest): Observable<AnimalDto> {
    return this.http.post<AnimalDto>(this.base, request);
  }

  recordWeight(id: string, request: RecordWeightRequest): Observable<WeightRecordDto> {
    return this.http.post<WeightRecordDto>(`${this.base}/${id}/weights`, request);
  }

  // 204 No-Content endpoints — map to void explicitly to satisfy TS6 strict generics
  sell(id: string, request: SellAnimalRequest): Observable<void> {
    return this.http.post(`${this.base}/${id}/sell`, request).pipe(map(() => undefined));
  }

  quarantine(id: string, request: QuarantineAnimalRequest): Observable<void> {
    return this.http.post(`${this.base}/${id}/quarantine`, request).pipe(map(() => undefined));
  }

  releaseFromQuarantine(id: string): Observable<void> {
    return this.http.post(`${this.base}/${id}/release-quarantine`, {}).pipe(map(() => undefined));
  }

  recordDeath(id: string, request: RecordDeathRequest): Observable<void> {
    return this.http.post(`${this.base}/${id}/death`, request).pipe(map(() => undefined));
  }

  transfer(id: string, request: TransferAnimalRequest): Observable<void> {
    return this.http.post(`${this.base}/${id}/transfer`, request).pipe(map(() => undefined));
  }

  addPhoto(id: string, request: AddPhotoRequest): Observable<AnimalPhotoDto> {
    return this.http.post<AnimalPhotoDto>(`${this.base}/${id}/photos`, request);
  }

  recordMating(id: string, request: any): Observable<void> {
    return this.http.post(`${this.base}/${id}/breeding`, request).pipe(map(() => undefined));
  }

  confirmPregnancy(id: string, recordId: string, request: ConfirmPregnancyRequest): Observable<void> {
    return this.http.put(`${this.base}/${id}/breeding/${recordId}/pregnancy`, request).pipe(map(() => undefined));
  }

  recordCalving(id: string, recordId: string, request: RecordCalvingRequest): Observable<void> {
    return this.http.put(`${this.base}/${id}/breeding/${recordId}/calving`, request).pipe(map(() => undefined));
  }

  recordBcs(id: string, score: number, recordedDate: string, notes?: string): Observable<BcsRecordDto> {
    return this.http.post<BcsRecordDto>(`${this.base}/${id}/bcs`, { score, recordedDate, notes });
  }

  delete(id: string): Observable<void> {
    return this.http.delete(`${this.base}/${id}`).pipe(map(() => undefined));
  }
}
