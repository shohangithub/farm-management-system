import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import {
  ReportCatalogItem,
  ReportDataSet,
  ReportExportFormat,
  RunReportRequest,
} from '../models/report.models';

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/reports';

  /** Remembers the last parameters used per report, per browser. */
  private readonly storageKey = 'farm360.report.params';

  getCatalog(): Observable<ReportCatalogItem[]> {
    return this.http.get<ReportCatalogItem[]>(`${this.baseUrl}/catalog`);
  }

  run(key: string, request: RunReportRequest): Observable<ReportDataSet> {
    return this.http
      .post<ReportDataSet>(`${this.baseUrl}/${encodeURIComponent(key)}/run`, request)
      .pipe(tap(() => this.rememberParameters(key, request)));
  }

  /**
   * Exports are a file download, so the response is a blob rather than JSON.
   * The filename comes from Content-Disposition — the server already names the file with the
   * report key and run timestamp, and duplicating that naming here would let the two drift.
   */
  export(key: string, format: ReportExportFormat, request: RunReportRequest): Observable<HttpResponse<Blob>> {
    return this.http.post(`${this.baseUrl}/${encodeURIComponent(key)}/export/${format}`, request, {
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
