import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Branch, BranchList, CreateBranchCommand, UpdateBranchCommand } from '../models/branch.model';

@Injectable({
  providedIn: 'root'
})
export class BranchService {
  private readonly http = inject(HttpClient);

  getBranchesByOrganization(orgId: string): Observable<BranchList[]> {
    return this.http.get<BranchList[]>(`/api/v1/organizations/${orgId}/branches`);
  }

  getBranchById(id: string): Observable<Branch> {
    return this.http.get<Branch>(`/api/v1/branches/${id}`);
  }

  createBranch(orgId: string, command: CreateBranchCommand): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`/api/v1/organizations/${orgId}/branches`, command);
  }

  updateBranch(id: string, command: UpdateBranchCommand): Observable<void> {
    return this.http.put<void>(`/api/v1/branches/${id}`, command);
  }

  deleteBranch(id: string): Observable<void> {
    return this.http.delete<void>(`/api/v1/branches/${id}`);
  }
}
