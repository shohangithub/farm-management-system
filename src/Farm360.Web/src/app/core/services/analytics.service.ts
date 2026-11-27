import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface BreedingAnalyticsDto {
  totalConfirmedPregnancies: number;
  expectedCalvingsNext30Days: number;
  conceptionRatePercentage: number;
}

export interface MonthlyRevenueExpenseDto {
  month: number;
  year: number;
  totalRevenueBdt: number;
  totalExpenseBdt: number;
}

export interface FinanceAnalyticsDto {
  monthlyData: MonthlyRevenueExpenseDto[];
}

export interface HealthAnalyticsDto {
  totalDeathsLast12Months: number;
  vaccinationCompliancePercentage: number;
}

/** One month of the health trend window (months with no records come back as zeros). */
export interface HealthTrendMonthDto {
  year: number;
  /** 1-12 */
  month: number;
  deaths: number;
  treatments: number;
  treatmentCostBdt: number;
}

/** 12-month health aggregates, counted on the server over every record (oldest month first). */
export interface HealthTrendsDto {
  from: string;
  to: string;
  months: HealthTrendMonthDto[];
  topIllnesses: { name: string; count: number }[];
  totalDeaths: number;
  totalTreatments: number;
  totalTreatmentCostBdt: number;
  onFarmAnimals: number;
  vaccinatedAnimals: number;
  vaccinationCoveragePercent: number;
}

@Injectable({
  providedIn: 'root'
})
export class AnalyticsService {
  private http = inject(HttpClient);
  private apiUrl = '/api/v1/farms';

  getBreedingAnalytics(farmId: string): Observable<BreedingAnalyticsDto> {
    return this.http.get<BreedingAnalyticsDto>(`${this.apiUrl}/${farmId}/analytics/breeding`);
  }

  getFinanceAnalytics(farmId: string, year?: number): Observable<FinanceAnalyticsDto> {
    let params = new HttpParams();
    if (year) {
      params = params.set('year', year);
    }
    return this.http.get<FinanceAnalyticsDto>(`${this.apiUrl}/${farmId}/analytics/finance`, { params });
  }

  getHealthAnalytics(farmId: string): Observable<HealthAnalyticsDto> {
    return this.http.get<HealthAnalyticsDto>(`${this.apiUrl}/${farmId}/analytics/health`);
  }

  getHealthTrends(farmId: string): Observable<HealthTrendsDto> {
    return this.http.get<HealthTrendsDto>(`${this.apiUrl}/${farmId}/analytics/health/trends`);
  }
}
