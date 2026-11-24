import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ExecutiveDashboardData, HerdComposition, AdgTrend, FeedCostTrend, VaccinationCompliance, FarmSummaryCard, ActivityFeedItem, FarmSetupStatus } from '../models/dashboard.model';

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

  getHerdComposition(farmId: string): Observable<HerdComposition> {
    return this.http.get<HerdComposition>(`${this.baseUrl}/${farmId}/dashboard/herd-composition`);
  }

  getAdgTrends(farmId: string): Observable<AdgTrend[]> {
    return this.http.get<AdgTrend[]>(`${this.baseUrl}/${farmId}/dashboard/adg-trends`);
  }

  getFeedCostTrends(farmId: string): Observable<FeedCostTrend[]> {
    return this.http.get<FeedCostTrend[]>(`${this.baseUrl}/${farmId}/dashboard/feed-cost-trends`);
  }

  getVaccinationCompliance(farmId: string): Observable<VaccinationCompliance> {
    return this.http.get<VaccinationCompliance>(`${this.baseUrl}/${farmId}/dashboard/vaccination-compliance`);
  }

  getFarmSummaryCards(farmId: string): Observable<FarmSummaryCard[]> {
    return this.http.get<FarmSummaryCard[]>(`${this.baseUrl}/${farmId}/dashboard/farm-summary-cards`);
  }

  getRecentActivityFeed(farmId: string): Observable<ActivityFeedItem[]> {
    return this.http.get<ActivityFeedItem[]>(`${this.baseUrl}/${farmId}/dashboard/recent-activity`);
  }

  /** The onboarding checklist: which setup steps this farm has done. */
  getSetupStatus(farmId: string): Observable<FarmSetupStatus> {
    return this.http.get<FarmSetupStatus>(`${this.baseUrl}/${farmId}/dashboard/setup-status`);
  }
}
