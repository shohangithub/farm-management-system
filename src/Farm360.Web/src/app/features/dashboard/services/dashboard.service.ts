import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ExecutiveDashboardData } from '../models/dashboard.model';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private http = inject(HttpClient);
  // Uses relative path to hit the proxy which proxies to the API.
  private baseUrl = '/api/v1/farms';

  getExecutiveDashboard(farmId: string): Observable<ExecutiveDashboardData> {
    return this.http.get<ExecutiveDashboardData>(`${this.baseUrl}/${farmId}/dashboard`);
  }
}
