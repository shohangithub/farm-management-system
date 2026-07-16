import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Farm, FarmList, CreateFarmCommand, UpdateFarmCommand } from '../models/farm.model';
@Injectable({
  providedIn: 'root'
})
export class FarmService {
  private http = inject(HttpClient);
  private apiUrl = '/api/v1/farms';
  private branchApiUrl = '/api/v1/branches';

  getFarmsByBranch(branchId: string): Observable<FarmList[]> {
    return this.http.get<FarmList[]>(`${this.branchApiUrl}/${branchId}/farms`);
  }

  getFarmById(id: string): Observable<Farm> {
    return this.http.get<Farm>(`${this.apiUrl}/${id}`);
  }

  createFarm(branchId: string, command: CreateFarmCommand): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.branchApiUrl}/${branchId}/farms`, command);
  }

  updateFarm(id: string, command: UpdateFarmCommand): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, command);
  }

  deleteFarm(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
