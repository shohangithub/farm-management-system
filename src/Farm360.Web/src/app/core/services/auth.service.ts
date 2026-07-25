import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, firstValueFrom, of, tap, catchError, map } from 'rxjs';
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

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  private accessTokenKey = 'farm360_access_token';
  private refreshTokenKey = 'farm360_refresh_token';

  public isInitialized = signal<boolean>(false);
  private isInitializedSubject = new BehaviorSubject<boolean>(false);
  public isInitialized$ = this.isInitializedSubject.asObservable();

  private currentUserSubject = new BehaviorSubject<UserProfile | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();
  public currentUserSignal = signal<UserProfile | null>(null);

  public get accessToken(): string | null {
    return localStorage.getItem(this.accessTokenKey);
  }

  public get refreshToken(): string | null {
    return localStorage.getItem(this.refreshTokenKey);
  }

  public get isAuthenticated(): boolean {
    return !!this.accessToken;
  }

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
            // Access token failed/expired; try silent refresh if refresh token present
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
      throw new Error('No refresh token available');
    }
    return this.http.post<LoginResponse>('/api/v1/auth/refresh', { refreshToken: token }).pipe(
      tap(response => this.setSession(response))
    );
  }

  logout(): void {
    const token = this.refreshToken;
    if (token) {
      this.http.post('/api/v1/auth/logout', { refreshToken: token }).subscribe();
    }
    this.clearSession();
    this.router.navigate(['/login']);
  }

  hasPermission(permissionCode: string): boolean {
    const user = this.currentUserSubject.value;
    if (!user || !user.permissions) return false;
    return user.permissions.includes(permissionCode);
  }

  public setSession(response: LoginResponse): void {
    localStorage.setItem(this.accessTokenKey, response.accessToken);
    localStorage.setItem(this.refreshTokenKey, response.refreshToken);
  }

  public clearSession(): void {
    localStorage.removeItem(this.accessTokenKey);
    localStorage.removeItem(this.refreshTokenKey);
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
