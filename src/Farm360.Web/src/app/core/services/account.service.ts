import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { AuthService, LoginResponse } from './auth.service';
import { SKIP_AUTH } from '../interceptors/auth.interceptor';

export interface InvitationPreview {
  tenantName: string;
  roleName: string;
  phoneMasked: string | null;
  emailMasked: string | null;
  expiresAtUtc: string;
  hasAccount: boolean;
  needsPhone: boolean;
}

export interface AcceptInvitationRequest {
  token: string;
  fullName?: string | null;
  password?: string | null;
  phone?: string | null;
}

export interface AcceptInvitationResult {
  tenantName: string;
  accountCreated: boolean;
  alreadyMember: boolean;
}

export interface PasswordResetToken {
  resetToken: string;
  expiresInSeconds: number;
}

/**
 * Account self-service: change password (signed in), forgot/reset password via SMS code and
 * invitation accept (public). Public calls never send the bearer token (SKIP_AUTH) so an invitation
 * can be accepted from a browser that is signed in to another organisation.
 */
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly publicContext = () => new HttpContext().set(SKIP_AUTH, true);

  /** Changes the password; every other session is signed out and this one gets fresh tokens. */
  changePassword(currentPassword: string, newPassword: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>('/api/v1/auth/change-password', { currentPassword, newPassword })
      .pipe(tap(response => this.auth.setSession(response)));
  }

  requestPasswordReset(phone: string): Observable<unknown> {
    return this.http.post('/api/v1/auth/password/forgot', { phone }, { context: this.publicContext() });
  }

  verifyResetCode(phone: string, code: string): Observable<PasswordResetToken> {
    return this.http.post<PasswordResetToken>('/api/v1/auth/password/verify', { phone, code }, { context: this.publicContext() });
  }

  resetPassword(resetToken: string, newPassword: string): Observable<void> {
    return this.http.post<void>('/api/v1/auth/password/reset', { resetToken, newPassword }, { context: this.publicContext() });
  }

  previewInvitation(token: string): Observable<InvitationPreview> {
    return this.http.post<InvitationPreview>('/api/v1/auth/invitations/preview', { token }, { context: this.publicContext() });
  }

  acceptInvitation(request: AcceptInvitationRequest): Observable<AcceptInvitationResult> {
    return this.http.post<AcceptInvitationResult>('/api/v1/auth/invitations/accept', request, { context: this.publicContext() });
  }
}

/** Mirrors the backend password policy (IdentityServiceExtensions / PasswordPolicy). */
export const PASSWORD_PATTERN = /^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,128}$/;
export const PASSWORD_HINT = 'At least 8 characters with an uppercase letter, a lowercase letter, a digit and a symbol.';
