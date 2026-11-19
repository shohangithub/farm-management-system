import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import {
  ReportCatalogItem,
  ReportDataSet,
  ReportExportFormat,
  ReportLanguage,
  ReportRunHistoryItem,
  RunReportRequest,
} from '../models/report.models';

/** A past run handed to the viewer so it can re-open with exactly the same parameters. */
export interface ReportRerun {
  parameters: Record<string, string | null>;
  language: ReportLanguage;
  bengaliNumerals: boolean;
}

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/reports';

  /** Remembers the last parameters used per report, per browser. */
  private readonly storageKey = 'farm360.report.params';

  private readonly favouritesKey = 'farm360.report.favourites';
  private readonly recentKey = 'farm360.report.recent';

  getCatalog(): Observable<ReportCatalogItem[]> {
    return this.http.get<ReportCatalogItem[]>(`${this.baseUrl}/catalog`);
  }

  /** Audited runs, newest first (only reports the user may still run). */
  getRuns(take = 20, reportKey?: string): Observable<ReportRunHistoryItem[]> {
    const params: Record<string, string> = { take: String(take) };
    if (reportKey) {
      params['reportKey'] = reportKey;
    }
    return this.http.get<ReportRunHistoryItem[]>(`${this.baseUrl}/runs`, { params });
  }

  // ── Favourites & recently opened (per browser; a convenience, not a record) ──

  favourites(): string[] {
    return this.readList(this.favouritesKey);
  }

  toggleFavourite(key: string): string[] {
    const current = this.favourites();
    const next = current.includes(key) ? current.filter(k => k !== key) : [...current, key];
    this.writeList(this.favouritesKey, next);
    return next;
  }

  recentlyOpened(): string[] {
    return this.readList(this.recentKey);
  }

  markOpened(key: string): void {
    const next = [key, ...this.recentlyOpened().filter(k => k !== key)].slice(0, 8);
    this.writeList(this.recentKey, next);
  }

  private readList(storageKey: string): string[] {
    try {
      const raw = localStorage.getItem(storageKey);
      const parsed = raw ? JSON.parse(raw) : [];
      return Array.isArray(parsed) ? parsed.filter((k): k is string => typeof k === 'string') : [];
    } catch {
      return [];
    }
  }

  private writeList(storageKey: string, keys: string[]): void {
    try {
      localStorage.setItem(storageKey, JSON.stringify(keys));
    } catch {
      // Private browsing or a full quota: favourites simply won't persist.
    }
  }

  run(key: string, request?: Partial<RunReportRequest>): Observable<ReportDataSet> {
    const fullRequest: RunReportRequest = {
      parameters: request?.parameters ?? {},
      language: request?.language ?? 'en',
      bengaliNumerals: request?.bengaliNumerals ?? false,
    };
    return this.http
      .post<ReportDataSet>(`${this.baseUrl}/${encodeURIComponent(key)}/run`, fullRequest)
      .pipe(tap(() => this.rememberParameters(key, fullRequest)));
  }

  /**
   * Exports are a file download, so the response is a blob rather than JSON.
   * The filename comes from Content-Disposition — the server already names the file with the
   * report key and run timestamp, and duplicating that naming here would let the two drift.
   */
  export(
    key: string,
    format: ReportExportFormat,
    request?: Partial<RunReportRequest>
  ): Observable<HttpResponse<Blob>> {
    const fullRequest: RunReportRequest = {
      parameters: request?.parameters ?? {},
      language: request?.language ?? 'en',
      bengaliNumerals: request?.bengaliNumerals ?? false,
    };
    return this.http.post(`${this.baseUrl}/${encodeURIComponent(key)}/export/${format}`, fullRequest, {
      observe: 'response',
      responseType: 'blob',
    });
  }

  /** Triggers the browser download for an exported report. */
  saveBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    // Revoking immediately can cancel the download in some browsers; a tick is enough.
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  fileNameFrom(headers: HttpHeaders, fallback: string): string {
    const disposition = headers.get('content-disposition');
    if (!disposition) {
      return fallback;
    }

    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
    return match?.[1] ? decodeURIComponent(match[1]) : fallback;
  }

  // ── Per-user parameter memory ─────────────────────────────────────────────
  // A farm manager runs the same report on the same animal most days; making them re-pick it
  // every time is the sort of friction that stops a report pack getting used.

  rememberParameters(key: string, request: RunReportRequest): void {
    try {
      const all = this.readStore();
      all[key] = request;
      localStorage.setItem(this.storageKey, JSON.stringify(all));
    } catch {
      // Private browsing or a full quota. Remembering is a convenience, never a requirement.
    }
  }

  recallParameters(key: string): RunReportRequest | null {
    try {
      return this.readStore()[key] ?? null;
    } catch {
      return null;
    }
  }

  private readStore(): Record<string, RunReportRequest> {
    const raw = localStorage.getItem(this.storageKey);
    return raw ? (JSON.parse(raw) as Record<string, RunReportRequest>) : {};
  }
}
