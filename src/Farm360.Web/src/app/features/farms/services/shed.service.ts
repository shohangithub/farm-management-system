import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Shed, ShedList, CreateShedCommand, UpdateShedCommand } from '../models/shed.model';
@Injectable({
  providedIn: 'root'
})
export class ShedService {
  private http = inject(HttpClient);
  private apiUrl = '/api/v1/sheds';
  private farmApiUrl = '/api/v1/farms';

  getShedsByFarm(farmId: string): Observable<ShedList[]> {
    return this.http.get<ShedList[]>(`${this.farmApiUrl}/${farmId}/sheds`);
  }

  getShedById(id: string): Observable<Shed> {
    return this.http.get<Shed>(`${this.apiUrl}/${id}`);
  }

  createShed(farmId: string, command: CreateShedCommand): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.farmApiUrl}/${farmId}/sheds`, command);
  }

  updateShed(id: string, command: UpdateShedCommand): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, command);
  }

  deleteShed(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
