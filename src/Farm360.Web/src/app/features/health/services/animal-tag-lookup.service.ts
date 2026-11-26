import { Injectable, inject } from '@angular/core';
import { Observable, forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { AnimalService } from '../../livestock/services/animal.service';

/**
 * Turns animal ids into ear-tag numbers for the health lists whose API rows only carry the id
 * (vaccinations, mortality). Tags are cached for the session, so each animal is fetched once.
 */
@Injectable({ providedIn: 'root' })
export class AnimalTagLookupService {
  private readonly animals = inject(AnimalService);
  private readonly cache = new Map<string, string>();

  /** Map of id -> tag for the given ids. An animal that cannot be loaded falls back to a short id. */
  tagsFor(ids: string[]): Observable<Record<string, string>> {
    const unique = Array.from(new Set(ids.filter(Boolean)));
    const missing = unique.filter(id => !this.cache.has(id));

    const load$ = missing.length === 0
      ? of([] as void[])
      : forkJoin(missing.map(id => this.animals.getById(id).pipe(
          map(a => { this.cache.set(id, a.tagId || id.slice(0, 8)); }),
          catchError(() => { this.cache.set(id, id.slice(0, 8)); return of(undefined); })
        )));

    return load$.pipe(map(() => {
      const result: Record<string, string> = {};
      for (const id of unique) result[id] = this.cache.get(id) ?? id.slice(0, 8);
      return result;
    }));
  }
}
