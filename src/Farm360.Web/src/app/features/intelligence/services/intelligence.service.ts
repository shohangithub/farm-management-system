import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ProjectionDefaults } from '../models/projection.model';

/**
 * The animal page's resolved price and cost to date. Profit projections come from the Sell-or-keep
 * engine (AnimalService.getSellOrKeep / getFatteningPlan), not from here.
 */
@Injectable({
  providedIn: 'root'
})
export class IntelligenceService {
  private http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/intelligence/projections';

  getProjectionDefaults(animalId: string): Observable<ProjectionDefaults> {
    return this.http.get<ProjectionDefaults>(`${this.baseUrl}/defaults/${animalId}`);
  }
}
