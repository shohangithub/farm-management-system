import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export type FeedAllocationMethod = 'EqualPerHead' | 'MetabolicWeight' | 'LiveWeight';
export type GroupPlanQuantityBasis = 'PerHead' | 'WholeGroup';
export type OverheadAllocationMethod = 'PerHeadDay' | 'PerLiveWeight';
export type OverheadCostPeriod = 'CalendarMonth' | 'TransactionDate';
export type AnimalValuationPolicy = 'CostBasis' | 'LiveWeightMarket' | 'MeatYieldMarket';
export type ReportLanguagePreference = 'English' | 'Bangla';

/** A complete set of the organisation-editable business rules. */
export interface BusinessRulesValues {
  feedAllocation: FeedAllocationMethod;
  metabolicWeightExponent: number;
  groupPlanQuantityBasis: GroupPlanQuantityBasis;
  fallbackAnimalWeightKg: number;
  overheadAllocation: OverheadAllocationMethod;
  overheadCostPeriod: OverheadCostPeriod;
  valuation: AnimalValuationPolicy;
  /** Fraction of live weight, 0.30–0.70. */
  defaultDressingPercentage: number;
  defaultMeatPricePerKgBdt: number;
  defaultLiveWeightPricePerKgBdt: number;
  defaultReportLanguage: ReportLanguagePreference;
  bengaliNumeralsByDefault: boolean;
}

/** Stored overrides: a null field inherits the default. */
export type BusinessRulesOverrides = { [K in keyof BusinessRulesValues]: BusinessRulesValues[K] | null };

export interface BusinessRulesSettings {
  farmId: string | null;
  /** What calculations use now. */
  effective: BusinessRulesValues;
  /** What applies with no override ("Reset to defaults" returns to these). */
  inherited: BusinessRulesValues;
  overrides: BusinessRulesOverrides;
  isCustomized: boolean;
  lastModifiedAtUtc: string | null;
}

/** Settings → Farm rules: the organisation's own business assumptions. */
@Injectable({ providedIn: 'root' })
export class BusinessRulesService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/settings/business-rules';

  /** farmId omitted = the organisation-wide rules; a farm id = that farm's overrides on top of them. */
  get(farmId?: string | null): Observable<BusinessRulesSettings> {
    return this.http.get<BusinessRulesSettings>(this.url, { params: this.params(farmId) });
  }

  save(overrides: BusinessRulesOverrides, farmId?: string | null): Observable<BusinessRulesSettings> {
    return this.http.put<BusinessRulesSettings>(this.url, overrides, { params: this.params(farmId) });
  }

  private params(farmId?: string | null): HttpParams {
    return farmId ? new HttpParams().set('farmId', farmId) : new HttpParams();
  }
}
