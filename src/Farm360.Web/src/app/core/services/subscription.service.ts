import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import {
  SubscriptionCatalog,
  TenantSubscriptionRecord,
  TenantSubscriptionStatus,
  SubscribeRequest,
  StartTrialRequest,
  InitiateCheckoutRequest,
  CheckoutSession
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

  /**
   * Submits a self-reported payment reference. Returns the created record in `Pending` status --
   * it does not activate the subscription, so `currentSubscription` is deliberately left alone
   * until an admin verifies the payment (or a real gateway's callback does, for a paid-online flow).
   */
  subscribe(payload: SubscribeRequest): Observable<TenantSubscriptionRecord> {
    return this.http.post<TenantSubscriptionRecord>(`${this.baseUrl}/subscribe`, payload);
  }

  getHistory(): Observable<TenantSubscriptionRecord[]> {
    return this.http.get<TenantSubscriptionRecord[]>(`${this.baseUrl}/history`);
  }

  /**
   * Starts a verified SSLCommerz checkout. On success, redirect the browser to
   * `gatewayPageUrl` -- the subscription activates automatically once the gateway confirms
   * payment (no manual admin review needed, unlike `subscribe()`).
   */
  initiateCheckout(payload: InitiateCheckoutRequest): Observable<CheckoutSession> {
    return this.http.post<CheckoutSession>(`${this.baseUrl}/checkout/initiate`, payload);
  }
}
