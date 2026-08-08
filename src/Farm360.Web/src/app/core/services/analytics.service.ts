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
}
