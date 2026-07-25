import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Branch, BranchList, CreateBranchCommand, UpdateBranchCommand } from '../models/branch.model';
import { PagedResult } from '../../../shared/models/paged-result.model';

@Injectable({
  providedIn: 'root'
})
export class BranchService {
  private readonly http = inject(HttpClient);

  getBranchesByOrganization(orgId: string, search?: string, status?: number, page: number = 1, size: number = 10): Observable<PagedResult<BranchList>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('size', size.toString());

    if (search) {
      params = params.set('search', search);
    }
    
    if (status !== undefined && status !== null) {
      params = params.set('status', status.toString());
    }

    return this.http.get<PagedResult<BranchList>>(`/api/v1/organizations/${orgId}/branches`, { params });
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

  activateBranch(id: string): Observable<void> {
    return this.http.post<void>(`/api/v1/branches/${id}/activate`, {});
  }
}
