import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export type MemberStatus = 'Active' | 'Deactivated' | 'Pending';
export type InvitationStatus = 'Pending' | 'Expired' | 'Accepted' | 'Revoked';

export interface TeamMember {
  id: string;
  userId: string;
  fullName: string | null;
  phone: string | null;
  email: string | null;
  roleId: string;
  roleName: string;
  isOwner: boolean;
  branchId: string | null;
  branchName: string | null;
  status: MemberStatus;
  joinedAt: string | null;
  lastLoginAt: string | null;
  isCurrentUser: boolean;
}

export interface TeamInvitation {
  id: string;
  phone: string | null;
  email: string | null;
  roleId: string;
  roleName: string;
  branchId: string | null;
  branchName: string | null;
  status: InvitationStatus;
  expiresAtUtc: string;
  lastSentAtUtc: string;
  sendCount: number;
  invitedByName: string | null;
}

export interface RoleOption {
  id: string;
  name: string;
  description: string;
  isSystemRole: boolean;
  /** False when the role has permissions the current user does not hold. */
  canGrant: boolean;
}

export interface BranchOption {
  id: string;
  name: string;
}

export interface CreateInvitationRequest {
  phone: string | null;
  email: string | null;
  roleId: string;
  branchId: string | null;
}

/** Team (tenant members + invitations) API — /api/v1/users. */
@Injectable({ providedIn: 'root' })
export class TeamService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/users';

  getMembers(): Observable<TeamMember[]> {
    return this.http.get<TeamMember[]>(`${this.base}/members`);
  }

  getInvitations(): Observable<TeamInvitation[]> {
    return this.http.get<TeamInvitation[]>(`${this.base}/invitations`);
  }

  getRoles(): Observable<RoleOption[]> {
    return this.http.get<RoleOption[]>(`${this.base}/roles`);
  }

  getBranches(): Observable<BranchOption[]> {
    return this.http.get<BranchOption[]>(`${this.base}/branches`);
  }

  changeRole(memberId: string, roleId: string): Observable<void> {
    return this.http.put<void>(`${this.base}/members/${memberId}/role`, { roleId });
  }

  changeBranch(memberId: string, branchId: string | null): Observable<void> {
    return this.http.put<void>(`${this.base}/members/${memberId}/branch`, { branchId });
  }

  deactivate(memberId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/members/${memberId}/deactivate`, {});
  }

  reactivate(memberId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/members/${memberId}/reactivate`, {});
  }

  remove(memberId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/members/${memberId}`);
  }

  invite(request: CreateInvitationRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/invitations`, request);
  }

  resendInvitation(invitationId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/invitations/${invitationId}/resend`, {});
  }

  revokeInvitation(invitationId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/invitations/${invitationId}/revoke`, {});
  }
}
