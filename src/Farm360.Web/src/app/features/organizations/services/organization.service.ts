import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../../core/services/auth.service';

import { Organization, CreateOrganizationCommand, UpdateOrganizationCommand } from '../models/organization.model';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class OrganizationService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly baseUrl = '/api/v1/organizations';

  getOrganizations(): Observable<Organization[]> {
    return this.http.get<Organization[]>(this.baseUrl);
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
}
