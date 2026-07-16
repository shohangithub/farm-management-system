import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, tap, map, BehaviorSubject } from 'rxjs';
import { MasterDataEntry, CreateMasterDataCommand, UpdateMasterDataCommand, MasterDataType } from '../models/master-data.model';

@Injectable({
  providedIn: 'root'
})
export class MasterDataService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/master-data';
  
  // Cache dictionary where key is the MasterDataType (number) and value is the list of entries
  private cache = new Map<number, BehaviorSubject<MasterDataEntry[] | null>>();

  getByType(type: MasterDataType, forceRefresh = false): Observable<MasterDataEntry[]> {
    if (!this.cache.has(type)) {
      this.cache.set(type, new BehaviorSubject<MasterDataEntry[] | null>(null));
    }

    const subject = this.cache.get(type)!;

    if (!forceRefresh && subject.value !== null) {
      return subject.asObservable().pipe(map(entries => entries || []));
    }

    return this.http.get<MasterDataEntry[]>(`${this.baseUrl}/${type}`).pipe(
      tap(entries => subject.next(entries))
    );
  }

  getById(id: string): Observable<MasterDataEntry> {
    return this.http.get<MasterDataEntry>(`${this.baseUrl}/entry/${id}`);
  }

  create(command: CreateMasterDataCommand): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, command).pipe(
      tap(() => this.invalidateCache(command.type))
    );
  }

  update(id: string, command: UpdateMasterDataCommand, type: MasterDataType): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, command).pipe(
      tap(() => this.invalidateCache(type))
    );
  }

  delete(id: string, type: MasterDataType): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(
      tap(() => this.invalidateCache(type))
    );
  }

  invalidateCache(type: MasterDataType): void {
    if (this.cache.has(type)) {
      this.cache.get(type)!.next(null); // Force next fetch to refresh
    }
  }
}
