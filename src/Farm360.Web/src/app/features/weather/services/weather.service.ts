import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of, shareReplay } from 'rxjs';
import { FarmWeather } from '../models/weather.model';

/**
 * The farm's weather. The server caches the forecast for 30 minutes; here the last answer per farm is
 * shared for 5 minutes so the dashboard card, the Weather page and the Today's-tasks banner make one call.
 * Errors become `null` ("Weather unavailable"): weather must never break the page that shows it.
 */
@Injectable({ providedIn: 'root' })
export class WeatherService {
  private readonly http = inject(HttpClient);
  private readonly cache = new Map<string, { at: number; value$: Observable<FarmWeather | null> }>();
  private static readonly TTL_MS = 5 * 60 * 1000;

  getFarmWeather(farmId: string, refresh = false): Observable<FarmWeather | null> {
    const hit = this.cache.get(farmId);
    if (!refresh && hit && Date.now() - hit.at < WeatherService.TTL_MS) return hit.value$;

    const value$ = this.http.get<FarmWeather>(`/api/v1/farms/${farmId}/weather`).pipe(
      catchError(() => {
        this.cache.delete(farmId);
        return of(null);
      }),
      shareReplay({ bufferSize: 1, refCount: false })
    );
    this.cache.set(farmId, { at: Date.now(), value$ });
    return value$;
  }
}
