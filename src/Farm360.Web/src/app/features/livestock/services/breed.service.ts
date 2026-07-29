import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BreedDto, CreateBreedRequest } from '../models/breed.models';

export interface BreedParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  category?: string;
  mainPurpose?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class BreedService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/livestock/breeds';

  getBreeds(params?: BreedParams): Observable<PagedResult<BreedDto>> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
      if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
      if (params.search) httpParams = httpParams.set('search', params.search);
      if (params.category) httpParams = httpParams.set('category', params.category);
      if (params.mainPurpose) httpParams = httpParams.set('mainPurpose', params.mainPurpose);
      if (params.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
      if (params.sortDesc) httpParams = httpParams.set('sortDesc', params.sortDesc);
    }
    return this.http.get<PagedResult<BreedDto>>(this.baseUrl, { params: httpParams });
  }

  getBreedById(id: string): Observable<BreedDto> {
    return this.http.get<BreedDto>(`${this.baseUrl}/${id}`);
  }

  createBreed(request: CreateBreedRequest): Observable<BreedDto> {
    return this.http.post<BreedDto>(this.baseUrl, request);
  }

  updateBreed(id: string, request: CreateBreedRequest): Observable<BreedDto> {
    return this.http.put<BreedDto>(`${this.baseUrl}/${id}`, request);
  }

  deleteBreed(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
