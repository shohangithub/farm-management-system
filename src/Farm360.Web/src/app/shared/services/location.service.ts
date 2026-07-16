import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Country, Division, District, Upazila, Union, Village } from '../models/location.model';

@Injectable({
  providedIn: 'root'
})
export class LocationService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/locations';

  getCountries(): Observable<Country[]> {
    return this.http.get<Country[]>(`${this.baseUrl}/countries`);
  }

  getDivisions(countryId: string): Observable<Division[]> {
    return this.http.get<Division[]>(`${this.baseUrl}/divisions?countryId=${countryId}`);
  }

  getDistricts(divisionId: string): Observable<District[]> {
    return this.http.get<District[]>(`${this.baseUrl}/districts?divisionId=${divisionId}`);
  }

  getUpazilas(districtId: string): Observable<Upazila[]> {
    return this.http.get<Upazila[]>(`${this.baseUrl}/upazilas?districtId=${districtId}`);
  }

  getUnions(upazilaId: string): Observable<Union[]> {
    return this.http.get<Union[]>(`${this.baseUrl}/unions?upazilaId=${upazilaId}`);
  }

  getVillages(unionId: string): Observable<Village[]> {
    return this.http.get<Village[]>(`${this.baseUrl}/villages?unionId=${unionId}`);
  }
}
