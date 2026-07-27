import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LookupDto } from '../../shared/models/lookup.model';

@Injectable({
  providedIn: 'root'
})
export class LookupService {
  private http = inject(HttpClient);
  private apiUrl = '/api/v1';

  getOrganizations(): Observable<LookupDto[]> {
    return this.http.get<LookupDto[]>(`${this.apiUrl}/organizations/lookups`);
  }

  getBranches(organizationId?: string): Observable<LookupDto[]> {
    let url = `${this.apiUrl}/organizations/${organizationId}/branches/lookups`;
    if (!organizationId) {
       return new Observable(observer => {
           observer.next([]);
           observer.complete();
       });
    }
    return this.http.get<LookupDto[]>(url);
  }

  getFarms(branchId?: string): Observable<LookupDto[]> {
    let url = `${this.apiUrl}/branches/${branchId}/farms/lookups`;
    if (!branchId) {
       return new Observable(observer => {
           observer.next([]);
           observer.complete();
       });
    }
    return this.http.get<LookupDto[]>(url);
  }
}
