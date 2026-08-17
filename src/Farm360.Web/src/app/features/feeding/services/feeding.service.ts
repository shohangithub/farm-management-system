import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  FeedIngredient,
  CreateFeedIngredientRequest,
  FeedFormula,
  CreateFeedFormulaRequest,
  FeedingSchedule,
  CreateFeedingScheduleRequest,
  FeedConsumptionLog,
  LogFeedConsumptionRequest,
  FcrAnalytics,
  FeedingRuleSet,
  CreateFeedingRuleSetRequest,
  UpdateFeedingRuleSetRequest,
  AnimalFeedingPlan,
  AssignAnimalFeedingPlanRequest,
  DailyFeedingEntry,
  FeedingCycleReconciliation
} from '../models/feeding.models';

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class FeedingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/feeding';

  // ── Ingredients ─────────────────────────────────────────────────────────────
  getIngredients(includePreloaded = true): Observable<FeedIngredient[]> {
    return this.http.get<FeedIngredient[]>(`${this.baseUrl}/ingredients`, {
      params: new HttpParams().set('includePreloaded', includePreloaded)
    });
  }

  createIngredient(request: CreateFeedIngredientRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/ingredients`, request);
  }

  updateIngredient(id: string, request: CreateFeedIngredientRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/ingredients/${id}`, { id, ...request });
  }

  // ── Formulas ────────────────────────────────────────────────────────────────
  getFormulas(pageNumber = 1, pageSize = 10, searchTerm?: string): Observable<PagedResult<FeedFormula>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (searchTerm) {
      params = params.set('searchTerm', searchTerm);
    }

    return this.http.get<PagedResult<FeedFormula>>(`${this.baseUrl}/formulas`, { params });
  }

  getFormulaById(id: string): Observable<FeedFormula> {
    return this.http.get<FeedFormula>(`${this.baseUrl}/formulas/${id}`);
  }

  createFormula(request: CreateFeedFormulaRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/formulas`, request);
  }

  updateFormula(id: string, request: CreateFeedFormulaRequest & { status?: number }): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/formulas/${id}`, { id, ...request });
  }

  // ── Schedules ───────────────────────────────────────────────────────────────
  getSchedules(farmId: string): Observable<FeedingSchedule[]> {
    return this.http.get<FeedingSchedule[]>(`${this.baseUrl}/schedules`, {
      params: new HttpParams().set('farmId', farmId)
    });
  }

  createSchedule(request: CreateFeedingScheduleRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/schedules`, request);
  }

  updateSchedule(id: string, request: CreateFeedingScheduleRequest & { isActive?: boolean }): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/schedules/${id}`, { id, ...request });
  }

  // ── Consumption Logs ────────────────────────────────────────────────────────
  getConsumptionLogs(farmId: string, fromDate?: string, toDate?: string): Observable<FeedConsumptionLog[]> {
    let params = new HttpParams().set('farmId', farmId);
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);

    return this.http.get<FeedConsumptionLog[]>(`${this.baseUrl}/consumption`, { params });
  }

  logConsumption(request: LogFeedConsumptionRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/consumption`, request);
  }

  // ── FCR Analytics ───────────────────────────────────────────────────────────
  getFcrAnalytics(farmId: string, shedId?: string): Observable<FcrAnalytics> {
    let params = new HttpParams().set('farmId', farmId);
    if (shedId) params = params.set('shedId', shedId);

    return this.http.get<FcrAnalytics>(`${this.baseUrl}/analytics/fcr`, { params });
  }

  // ─────────────────────────────────────────────────────────────────────────────
  // SMART FEEDING MODULE (PHASE 4 & 5)
  // ─────────────────────────────────────────────────────────────────────────────

  // ── Rule Sets ──
  getRuleSets(): Observable<FeedingRuleSet[]> {
    return this.http.get<FeedingRuleSet[]>(`${this.baseUrl}/rule-sets`);
  }

  createRuleSet(request: CreateFeedingRuleSetRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/rule-sets`, request);
  }

  updateRuleSet(id: string, request: UpdateFeedingRuleSetRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/rule-sets/${id}`, { id, ...request });
  }

  // ── Animal Feeding Plans ──
  getFeedingPlans(farmId: string, animalId?: string): Observable<AnimalFeedingPlan[]> {
    let params = new HttpParams().set('farmId', farmId);
    if (animalId) params = params.set('animalId', animalId);

    return this.http.get<AnimalFeedingPlan[]>(`${this.baseUrl}/plans`, { params });
  }

  assignPlan(request: AssignAnimalFeedingPlanRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/plans/assign`, request);
  }

  cancelPlan(id: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/plans/${id}/cancel`, { id });
  }

  excludeAnimalFromPlan(id: string, exclusionDate: string, reason: string, resumesOn?: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/plans/${id}/exclude`, {
      planId: id,
      exclusionDate,
      reason,
      resumesOn
    });
  }

  // ── Daily Feeding Entries ──
  getTodayEntries(farmId: string, targetDate: string): Observable<DailyFeedingEntry[]> {
    return this.http.get<DailyFeedingEntry[]>(`${this.baseUrl}/entries/today`, {
      params: new HttpParams()
        .set('farmId', farmId)
        .set('targetDate', targetDate)
    });
  }

  confirmEntry(id: string, actualKg: number, notes?: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/entries/${id}/confirm`, {
      entryId: id,
      actualKg,
      notes
    });
  }

  adjustEntry(id: string, actualKg: number, notes: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/entries/${id}/adjust`, {
      entryId: id,
      actualKg,
      notes
    });
  }

  skipEntry(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/entries/${id}/skip`, {
      entryId: id,
      reason
    });
  }

  // ── Reconciliations ──
  getReconciliations(farmId: string): Observable<FeedingCycleReconciliation[]> {
    return this.http.get<FeedingCycleReconciliation[]>(`${this.baseUrl}/reconciliations`, {
      params: new HttpParams().set('farmId', farmId)
    });
  }

  approveReconciliation(id: string, notes?: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/reconciliations/${id}/approve`, {
      id,
      notes
    });
  }

  rejectReconciliation(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/reconciliations/${id}/reject`, {
      id,
      reason
    });
  }
}
