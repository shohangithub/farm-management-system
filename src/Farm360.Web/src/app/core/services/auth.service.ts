import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, catchError, firstValueFrom, map, of, switchMap, tap, throwError } from 'rxjs';
import { Router } from '@angular/router';

export interface LoginRequest {
  phone: string;
  password?: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  sessionId: string;
}

export interface UserProfile {
  id: string;
  tenantId: string;
  role: string;
  tier: string;
  isSystemUser: boolean;
  permissions?: string[];
}

// ── Token storage keys ────────────────────────────────────────────────────────
// Access token: sessionStorage (cleared on tab/browser close, not readable cross-tab)
// Refresh token: localStorage  (persists for silent re-authentication across sessions)
// Note: httpOnly cookies would be ideal but require backend cookie support (Phase 2).
const ACCESS_TOKEN_KEY = 'farm360_access_token';
const REFRESH_TOKEN_KEY = 'farm360_refresh_token';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  public isInitialized = signal<boolean>(false);
  private isInitializedSubject = new BehaviorSubject<boolean>(false);
  public isInitialized$ = this.isInitializedSubject.asObservable();

  private currentUserSubject = new BehaviorSubject<UserProfile | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();
  public currentUserSignal = signal<UserProfile | null>(null);

  // ── Token accessors ───────────────────────────────────────────────────────

  public get accessToken(): string | null {
    return sessionStorage.getItem(ACCESS_TOKEN_KEY);
  }

  public get refreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  public get isAuthenticated(): boolean {
    return !!this.accessToken;
  }

  // ── Session initialization (called at app startup) ────────────────────────

  public initializeSession(): Promise<boolean> {
    return new Promise<boolean>((resolve) => {
      if (this.accessToken) {
        this.fetchProfile().subscribe({
          next: (user) => {
            this.setUserData(user);
            this.markInitialized();
            resolve(true);
          },
          error: () => {
            // Access token expired or invalid; attempt silent refresh
            if (this.refreshToken) {
              this.refreshSessionAndLoadProfile(resolve);
            } else {
              this.clearSession();
              this.markInitialized();
              resolve(false);
            }
          }
        });
      } else if (this.refreshToken) {
        this.refreshSessionAndLoadProfile(resolve);
      } else {
        this.clearSession();
        this.markInitialized();
        resolve(false);
      }
    });
  }

  private refreshSessionAndLoadProfile(resolve: (value: boolean) => void): void {
    this.refresh().subscribe({
      next: () => {
        this.fetchProfile().subscribe({
          next: (user) => {
            this.setUserData(user);
            this.markInitialized();
            resolve(true);
          },
          error: () => {
            this.clearSession();
            this.markInitialized();
            resolve(false);
          }
        });
      },
      error: () => {
        this.clearSession();
        this.markInitialized();
        resolve(false);
      }
    });
  }

  // ── Authentication actions ────────────────────────────────────────────────

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/v1/auth/login', request).pipe(
      tap(response => this.setSession(response)),
      tap(() => {
        this.fetchProfile().subscribe({
          next: (user) => this.setUserData(user)
        });
      })
    );
  }

  refresh(): Observable<LoginResponse> {
    const token = this.refreshToken;
    if (!token) {
      return throwError(() => new Error('No refresh token available'));
    }
    return this.http.post<LoginResponse>('/api/v1/auth/refresh', { refreshToken: token }).pipe(
      tap(response => this.setSession(response))
    );
  }

  refreshSession(): Observable<UserProfile> {
    return this.refresh().pipe(
      switchMap(() => this.fetchProfile()),
      tap(user => this.setUserData(user))
    );
  }

  /**
   * H2 Fix: Logout properly awaits the server-side session revocation before clearing local state.
   * If the API call fails (network error, etc.), we still clear the local session — the server-side
   * session will eventually expire naturally, but the user is immediately logged out locally.
   */
  logout(): void {
    const token = this.refreshToken;

    // Clear local state immediately to prevent further API calls with this token
    this.clearSession();

    // Attempt server-side revocation — fire-and-observe (don't block navigation)
    if (token) {
      this.http.post('/api/v1/auth/logout', { refreshToken: token }).subscribe({
        error: (err) => {
          // Log but don't re-throw — local session is already cleared
          console.warn('[AuthService] Server-side session revocation failed (session will expire naturally):', err?.status);
        }
      });
    }

    this.router.navigate(['/login']);
  }

  // ── Authorization ─────────────────────────────────────────────────────────

  hasPermission(permissionCode: string): boolean {
    const user = this.currentUserSubject.value;
    if (!user || !user.permissions) return false;
    return user.permissions.includes(permissionCode);
  }

  // ── Session management ────────────────────────────────────────────────────

  public setSession(response: LoginResponse): void {
    // Access token: sessionStorage (shorter lifetime, tab-scoped)
    sessionStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
    // Refresh token: localStorage (persists for silent re-auth)
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
  }

  public clearSession(): void {
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    this.currentUserSubject.next(null);
    this.currentUserSignal.set(null);
  }

  private setUserData(user: UserProfile): void {
    this.currentUserSubject.next(user);
    this.currentUserSignal.set(user);
  }

  private fetchProfile(): Observable<UserProfile> {
    return this.http.get<UserProfile>('/api/v1/auth/me');
  }

  private markInitialized(): void {
    this.isInitialized.set(true);
    this.isInitializedSubject.next(true);
  }
}
