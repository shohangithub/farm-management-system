import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Pen, PenList, CreatePenCommand, UpdatePenCommand } from '../models/pen.model';

@Injectable({
  providedIn: 'root'
})
export class PenService {
  private http = inject(HttpClient);
  private apiUrl = '/api/v1/pens';
  private shedApiUrl = '/api/v1/sheds';

  getPensByShed(shedId: string): Observable<PenList[]> {
    return this.http.get<PenList[]>(`${this.shedApiUrl}/${shedId}/pens`);
  }

  getPenById(id: string): Observable<Pen> {
    return this.http.get<Pen>(`${this.apiUrl}/${id}`);
  }

  createPen(shedId: string, command: CreatePenCommand): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.shedApiUrl}/${shedId}/pens`, command);
  }

  updatePen(id: string, command: UpdatePenCommand): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, command);
  }

  deletePen(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
