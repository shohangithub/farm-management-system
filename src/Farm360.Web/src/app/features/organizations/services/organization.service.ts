import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { AuthService } from '../../../core/services/auth.service';
import { PagedResult } from '../../../shared/models/paged-result.model';

import { Organization, CreateOrganizationCommand, UpdateOrganizationCommand } from '../models/organization.model';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class OrganizationService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly baseUrl = '/api/v1/organizations';

  getOrganizations(search?: string, status?: number, page: number = 1, size: number = 10): Observable<PagedResult<Organization>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('size', size.toString());

    if (search) {
      params = params.set('search', search);
    }
    
    if (status !== undefined && status !== null) {
      params = params.set('status', status.toString());
    }

    return this.http.get<PagedResult<Organization>>(this.baseUrl, { params });
  }

  getOrganizationById(id: string): Observable<Organization> {
    return this.http.get<Organization>(`${this.baseUrl}/${id}`);
  }

  createOrganization(command: CreateOrganizationCommand): Observable<{ id: string }> {
    const user = this.authService.currentUserSignal();
    const isNewTenant = user?.tenantId === '00000000-0000-0000-0000-000000000000';
    
    if (isNewTenant) {
      return this.http.post<{ id: string }>('/api/v1/tenants/onboard', command);
    }
    
    return this.http.post<{ id: string }>(this.baseUrl, command);
  }

  updateOrganization(id: string, command: UpdateOrganizationCommand): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, command);
  }

  deactivateOrganization(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  activateOrganization(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/activate`, {});
  }

  uploadLogo(id: string, file: File): Observable<{ logoUrl: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ logoUrl: string }>(`${this.baseUrl}/${id}/logo`, formData);
  }
}
