import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import { Organization, CreateOrganizationCommand, UpdateOrganizationCommand } from '../models/organization.model';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class OrganizationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/organizations';

  getOrganizations(): Observable<Organization[]> {
    return this.http.get<Organization[]>(this.baseUrl);
  }

  getOrganizationById(id: string): Observable<Organization> {
    return this.http.get<Organization>(`${this.baseUrl}/${id}`);
  }

  createOrganization(command: CreateOrganizationCommand): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, command);
  }

  updateOrganization(id: string, command: UpdateOrganizationCommand): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, command);
  }

  deactivateOrganization(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
