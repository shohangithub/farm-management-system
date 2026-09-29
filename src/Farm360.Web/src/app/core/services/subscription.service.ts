import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import {
  SubscriptionCatalog,
  TenantSubscriptionRecord,
  TenantSubscriptionStatus,
  SubscribeRequest,
  StartTrialRequest
} from '../models/subscription.model';

@Injectable({
  providedIn: 'root'
})
export class SubscriptionService {
  private http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/subscriptions';

  // Global active subscription status signal
  public currentSubscription = signal<TenantSubscriptionStatus | null>(null);
  public isLoading = signal<boolean>(false);

  getCurrentSubscription(): Observable<TenantSubscriptionStatus> {
    this.isLoading.set(true);
    return this.http.get<TenantSubscriptionStatus>(`${this.baseUrl}/current`).pipe(
      tap({
        next: (status) => {
          this.currentSubscription.set(status);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false)
      })
    );
  }

  getCatalog(): Observable<SubscriptionCatalog> {
    return this.http.get<SubscriptionCatalog>(`${this.baseUrl}/plans`);
  }

  startTrial(trialDays: number): Observable<TenantSubscriptionStatus> {
    const payload: StartTrialRequest = { trialDays };
    return this.http.post<TenantSubscriptionStatus>(`${this.baseUrl}/start-trial`, payload).pipe(
      tap((status) => this.currentSubscription.set(status))
    );
  }

  subscribe(payload: SubscribeRequest): Observable<TenantSubscriptionStatus> {
    return this.http.post<TenantSubscriptionStatus>(`${this.baseUrl}/subscribe`, payload).pipe(
      tap((status) => this.currentSubscription.set(status))
    );
  }

  getHistory(): Observable<TenantSubscriptionRecord[]> {
    return this.http.get<TenantSubscriptionRecord[]>(`${this.baseUrl}/history`);
  }
}
