import { Injectable } from '@angular/core';
import jsPDF from 'jspdf';
import html2canvas from 'html2canvas';

export interface ReportMetaHeader {
  title: string;
  subtitle?: string;
  farmName?: string;
  orgName?: string;
  dateRange?: string;
  currency?: string;
  metaFields?: { label: string; value: string }[];
}

export interface PdfExportOptions {
  filename?: string;
  orientation?: 'portrait' | 'landscape';
  format?: 'a4' | 'letter';
  scale?: number;
  quality?: number;
  header?: ReportMetaHeader;
  showSignatures?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class PdfExportService {

  /**
   * Captures a DOM element and renders it into a multi-page PDF using html2canvas & jsPDF.
   * - Guarantees 100% accurate Unicode / Bengali (বাংলা) rendering (CTL, ligatures, matras)
   * - Forces light mode in the cloned canvas for crisp white-sheet printing
   * - Automatically excludes any elements with class 'no-print' or attribute 'data-no-print'
   * - Optionally prepends a Farm360 branded letterhead
   */
  async exportElement(element: HTMLElement, options?: PdfExportOptions): Promise<void> {
    if (!element) {
      console.error('[PdfExportService] Cannot export: Target element is null or undefined.');
      return;
    }

    const orientation = options?.orientation || 'portrait';
    const format = options?.format || 'a4';
    const scale = options?.scale || 2;
    const quality = options?.quality || 0.95;

    // Standard page dimensions in mm
    const isLandscape = orientation === 'landscape';
    const pdfWidth = isLandscape ? 297 : 210;
    const pdfHeight = isLandscape ? 210 : 297;

    const canvas = await html2canvas(element, {
      scale,
      useCORS: true,
      allowTaint: true,
      backgroundColor: '#ffffff',
      logging: false,
      scrollX: 0,
      scrollY: 0,
      ignoreElements: (el) => el.classList?.contains('no-print') || el.hasAttribute('data-no-print'),
      onclone: (clonedDoc, clonedEl) => {
        // 1. Force light mode in cloned document
        clonedDoc.documentElement.classList.remove('dark');
        clonedDoc.body.classList.remove('dark');
        clonedEl.classList.remove('dark');

        // 2. Ensure container has white background and slate text
        clonedEl.style.backgroundColor = '#ffffff';
        clonedEl.style.color = '#0f172a';
        clonedEl.style.padding = clonedEl.style.padding || '24px';
        clonedEl.style.borderRadius = '0px';
        clonedEl.style.position = 'static';
        clonedEl.style.left = '0';
        clonedEl.style.top = '0';
        clonedEl.style.display = 'block';
        clonedEl.style.visibility = 'visible';

        // Clean up any residual interactive elements, tab headers or controls in clone
        clonedEl.querySelectorAll('.mat-mdc-tab-header, .no-print, [data-no-print]').forEach(el => el.remove());

        // 3. Optional: Inject Farm360 branded letterhead at top
        if (options?.header) {
          const headerEl = this.buildLetterheadDom(clonedDoc, options.header);
          clonedEl.insertBefore(headerEl, clonedEl.firstChild);
        }

        // 4. Optional: Inject standard Farm360 3-signature block at bottom
        if (options?.showSignatures) {
          const sigEl = this.buildSignaturesDom(clonedDoc);
          clonedEl.appendChild(sigEl);
        }
      }
    });

    if (!canvas || canvas.width === 0 || canvas.height === 0) {
      console.error('[PdfExportService] Canvas generation failed with 0 dimensions.');
      return;
    }

    const doc = new jsPDF({
      orientation,
      unit: 'mm',
      format
    });

    // Height of one page in canvas pixel coordinates
    const pageHeightPx = Math.floor((canvas.width * pdfHeight) / pdfWidth);
    const totalPages = Math.max(1, Math.ceil(canvas.height / pageHeightPx));

    for (let page = 0; page < totalPages; page++) {
      if (page > 0) {
        doc.addPage(format, orientation);
      }

      const srcY = page * pageHeightPx;
      const srcH = Math.min(pageHeightPx, canvas.height - srcY);

      const pageCanvas = document.createElement('canvas');
      pageCanvas.width = canvas.width;
      pageCanvas.height = pageHeightPx;

      const ctx = pageCanvas.getContext('2d');
      if (ctx) {
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, pageCanvas.width, pageCanvas.height);
        ctx.drawImage(
          canvas,
          0, srcY, canvas.width, srcH,
          0, 0, canvas.width, srcH
        );
      }

      const imgData = pageCanvas.toDataURL('image/jpeg', quality);
      doc.addImage(imgData, 'JPEG', 0, 0, pdfWidth, pdfHeight);
    }

    const defaultName = `Farm360_Report_${new Date().toISOString().split('T')[0]}.pdf`;
    let filename = options?.filename || defaultName;
    if (!filename.toLowerCase().endsWith('.pdf')) {
      filename += '.pdf';
    }

    doc.save(filename);
  }

  private buildLetterheadDom(doc: Document, header: ReportMetaHeader): HTMLElement {
    const wrapper = doc.createElement('div');
    wrapper.className = 'farm360-injected-letterhead mb-6 pb-4 border-b border-slate-200';
    wrapper.style.fontFamily = 'Inter, ui-sans-serif, system-ui, sans-serif';

    const nowFormatted = new Intl.DateTimeFormat('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    }).format(new Date());

    const metaItemsHtml = (header.metaFields || [])
      .map(m => `<span><strong>${m.label}:</strong> ${m.value}</span>`)
      .join(' &nbsp;|&nbsp; ');

    wrapper.innerHTML = `
      <div style="height: 5px; background: linear-gradient(to right, #059669, #0d9488); border-radius: 9999px; margin-bottom: 16px;"></div>
      <div style="display: flex; justify-content: space-between; align-items: flex-start;">
        <div>
          <h1 style="font-size: 22px; font-weight: 900; color: #0f172a; margin: 0; letter-spacing: -0.5px;">FARM360 AI</h1>
          <p style="font-size: 11px; font-weight: 700; color: #047857; text-transform: uppercase; letter-spacing: 0.8px; margin: 2px 0 0 0;">Agricultural Operations & Financial Intelligence Suite</p>
          <p style="font-size: 12px; color: #475569; margin: 4px 0 0 0;">Farm: <strong>${header.farmName || 'Primary Farm'}</strong> ${header.orgName ? `| Org: <strong>${header.orgName}</strong>` : ''}</p>
        </div>
        <div style="text-align: right;">
          <h2 style="font-size: 17px; font-weight: 800; color: #047857; margin: 0; text-transform: uppercase;">${header.title}</h2>
          ${header.subtitle ? `<p style="font-size: 12px; color: #475569; margin: 2px 0 0 0;">${header.subtitle}</p>` : ''}
          <p style="font-size: 11px; color: #64748b; margin: 4px 0 0 0;">
            ${header.dateRange ? `${header.dateRange} | ` : ''}Date: ${nowFormatted} | Currency: ${header.currency || 'BDT (৳)'}
          </p>
          ${metaItemsHtml ? `<p style="font-size: 11px; color: #64748b; margin: 2px 0 0 0;">${metaItemsHtml}</p>` : ''}
        </div>
      </div>
    `;

    return wrapper;
  }

  private buildSignaturesDom(doc: Document): HTMLElement {
    const wrapper = doc.createElement('div');
    wrapper.className = 'farm360-injected-signatures mt-8 pt-6 border-t border-slate-200';
    wrapper.style.fontFamily = 'Inter, ui-sans-serif, system-ui, sans-serif';
    wrapper.innerHTML = `
      <div style="display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 32px; text-align: center;">
        <div>
          <div style="border-bottom: 1px solid #94a3b8; height: 32px; margin-bottom: 6px;"></div>
          <div style="font-size: 11px; font-weight: 700; color: #0f172a;">Prepared By</div>
          <div style="font-size: 10px; color: #64748b;">Accountant / Operator</div>
        </div>
        <div>
          <div style="border-bottom: 1px solid #94a3b8; height: 32px; margin-bottom: 6px;"></div>
          <div style="font-size: 11px; font-weight: 700; color: #0f172a;">Verified By</div>
          <div style="font-size: 10px; color: #64748b;">Farm Supervisor / Manager</div>
        </div>
        <div>
          <div style="border-bottom: 1px solid #94a3b8; height: 32px; margin-bottom: 6px;"></div>
          <div style="font-size: 11px; font-weight: 700; color: #0f172a;">Approved By</div>
          <div style="font-size: 10px; color: #64748b;">Managing Director / Owner</div>
        </div>
      </div>
    `;
    return wrapper;
  }
}
