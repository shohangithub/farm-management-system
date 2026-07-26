import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BatchDto, BatchStatus, CreateBatchRequest, PagedBatchListDto } from '../models/batch.models';

@Injectable({
  providedIn: 'root'
})
export class BatchService {
  private http = inject(HttpClient);
  private apiUrl = '/api/v1/livestock/batches';

  getBatches(farmId: string, status?: BatchStatus, pageNumber = 1, pageSize = 20): Observable<PagedBatchListDto> {
    let params = new HttpParams()
      .set('farmId', farmId)
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (status) {
      params = params.set('status', status);
    }

    return this.http.get<PagedBatchListDto>(this.apiUrl, { params });
  }

  getBatchDetails(id: string): Observable<BatchDto> {
    return this.http.get<BatchDto>(`${this.apiUrl}/${id}`);
  }

  createBatch(request: CreateBatchRequest): Observable<string> {
    return this.http.post<string>(this.apiUrl, request);
  }

  assignAnimalsToBatch(batchId: string, animalIds: string[]): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${batchId}/assign`, animalIds);
  }
}
