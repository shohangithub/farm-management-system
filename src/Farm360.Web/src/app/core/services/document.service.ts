import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { NotificationService } from '../../shared/services/notification.service';
import { parseApiError } from '../utils/error-parser';
import { translate } from '../i18n/i18n.service';
import { SKIP_GLOBAL_ERROR_TOAST } from '../interceptors/auth.interceptor';

/** Language a printed document is rendered in. Omit to use the farm's "Default report language" rule. */
export type DocumentLanguage = 'en' | 'bn';

export interface DocumentOptions {
  language?: DocumentLanguage;
  /** Print ০-৯ instead of 0-9. Omit to use the farm's "Bengali numerals" rule. */
  bengaliNumerals?: boolean;
}

/**
 * Customer documents rendered by the server (QuestPDF, with the farm's letterhead and logo):
 * sale invoices, payment receipts and purchase orders.
 *
 * The API needs the JWT, so a plain link cannot open them. Each `open…` method fetches the PDF
 * through HttpClient (the auth interceptor adds the bearer token), then shows it in a new tab
 * via a blob URL. The tab is opened synchronously on the click so popup blockers allow it, and
 * filled once the PDF arrives; if the browser blocks it anyway, the PDF is downloaded instead.
 *
 * Every method resolves to `true` when the document opened and `false` when it failed — the
 * failure has already been shown as a toast, so callers need no error handling of their own.
 */
@Injectable({ providedIn: 'root' })
export class DocumentService {
  private readonly http = inject(HttpClient);
  private readonly notifications = inject(NotificationService);
  private readonly baseUrl = '/api/v1/documents';

  /** Sale invoice / receipt of a sold animal. */
  openSaleInvoice(animalId: string, options?: DocumentOptions): Promise<boolean> {
    return this.openPdf(`sale-invoice/${encodeURIComponent(animalId)}`, options, 'sale-invoice.pdf');
  }

  /** Money receipt (customer collection) or payment voucher (supplier payment) for one recorded payment. */
  openPaymentReceipt(paymentId: string, options?: DocumentOptions): Promise<boolean> {
    return this.openPdf(`payment-receipt/${encodeURIComponent(paymentId)}`, options, 'payment-receipt.pdf');
  }

  /** Printable purchase order for the supplier. */
  openPurchaseOrder(purchaseOrderId: string, options?: DocumentOptions): Promise<boolean> {
    return this.openPdf(`purchase-order/${encodeURIComponent(purchaseOrderId)}`, options, 'purchase-order.pdf');
  }

  /** Fetches the sale invoice as a Blob without opening it (e.g. to attach or save it). */
  fetchSaleInvoice(animalId: string, options?: DocumentOptions): Promise<Blob> {
    return this.fetchPdf(`sale-invoice/${encodeURIComponent(animalId)}`, options).then(r => r.blob);
  }

  /**
   * Fetches any document endpoint under /api/v1/documents with the user's token and opens it
   * in a new tab. `path` is relative, e.g. `purchase-order/{id}`.
   */
  async openPdf(path: string, options?: DocumentOptions, fallbackFileName = 'document.pdf'): Promise<boolean> {
    // Open the tab now, inside the click, so popup blockers treat it as user-initiated.
    const tab = window.open('', '_blank');
    if (tab) {
      const preparing = translate('Preparing document…');
      tab.document.title = preparing;
      tab.document.body.innerHTML =
        `<p style="font-family: system-ui, sans-serif; color: #555; padding: 24px">${preparing}</p>`;
    }

    try {
      const { blob, fileName } = await this.fetchPdf(path, options, fallbackFileName);
      const url = URL.createObjectURL(blob);

      if (tab && !tab.closed) {
        tab.location.href = url;
      } else {
        this.download(url, fileName);
      }

      // The tab has loaded the PDF by then; release the memory.
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
      return true;
    } catch (err) {
      tab?.close();
      this.notifications.error(await this.describeError(err));
      return false;
    }
  }

  private async fetchPdf(
    path: string,
    options?: DocumentOptions,
    fallbackFileName = 'document.pdf',
  ): Promise<{ blob: Blob; fileName: string }> {
    let params = new HttpParams();
    if (options?.language) {
      params = params.set('language', options.language);
    }
    if (options?.bengaliNumerals !== undefined) {
      params = params.set('bengaliNumerals', String(options.bengaliNumerals));
    }

    const response = await firstValueFrom(
      this.http.get(`${this.baseUrl}/${path}`, {
        params,
        observe: 'response',
        responseType: 'blob',
        // This service shows its own, more specific message.
        context: new HttpContext().set(SKIP_GLOBAL_ERROR_TOAST, true),
      }),
    );

    const body = response.body ?? new Blob([], { type: 'application/pdf' });
    const blob = body.type === 'application/pdf' ? body : new Blob([body], { type: 'application/pdf' });
    return { blob, fileName: this.fileNameFrom(response.headers, fallbackFileName) };
  }

  private download(url: string, fileName: string): void {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
  }

  private fileNameFrom(headers: HttpHeaders, fallback: string): string {
    const disposition = headers.get('content-disposition');
    if (!disposition) {
      return fallback;
    }

    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
    return match?.[1] ? decodeURIComponent(match[1]) : fallback;
  }

  /** A blob error body is JSON ProblemDetails underneath; read it so the toast says why. */
  private async describeError(err: unknown): Promise<string> {
    const fallback = translate('Could not open the document. Please try again.');
    if (err instanceof HttpErrorResponse) {
      if (err.status === 403) {
        return translate('You do not have permission to open this document.');
      }
      if (err.error instanceof Blob) {
        try {
          return parseApiError({ error: await err.error.text() }, fallback);
        } catch {
          return fallback;
        }
      }
      return parseApiError(err, fallback);
    }
    return fallback;
  }
}
