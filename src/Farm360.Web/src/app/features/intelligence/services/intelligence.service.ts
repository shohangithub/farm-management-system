import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ProjectionDefaults,
  FatteningProjectionInputs,
  ProfitProjectionResponse
} from '../models/projection.model';

@Injectable({
  providedIn: 'root'
})
export class IntelligenceService {
  private http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/intelligence/projections';

  getProjectionDefaults(animalId: string): Observable<ProjectionDefaults> {
    return this.http.get<ProjectionDefaults>(`${this.baseUrl}/defaults/${animalId}`);
  }

  calculateProfitProjection(inputs: FatteningProjectionInputs): Observable<ProfitProjectionResponse> {
    return this.http.post<ProfitProjectionResponse>(`${this.baseUrl}/calculate`, { inputs, includeDailyRows: true });
  }

  solveBreakEven(inputs: FatteningProjectionInputs, targetRoiPercentage: number): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/solve-break-even`, { inputs, targetRoiPercentage });
  }

  saveProjectionScenario(animalId: string | null, name: string, description: string, inputs: FatteningProjectionInputs): Observable<string> {
    return this.http.post<string>(`${this.baseUrl}/scenarios`, { animalId, name, description, inputs });
  }
}
