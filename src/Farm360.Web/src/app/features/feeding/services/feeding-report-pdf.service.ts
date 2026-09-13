import { Injectable } from '@angular/core';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import html2canvas from 'html2canvas';
import { AnimalFeedingPlan } from '../models/feeding.models';

export interface FeedsCostReportMetadata {
  farmName: string;
  orgName?: string;
  generatedBy?: string;
  generatedAt?: Date;
  currency?: string;
}

export interface RuleSetCostSummary {
  ruleSetName: string;
  animalCount: number;
  totalDailyConcentrateKg: number;
  totalDailyRoughageKg: number;
  totalDailyFeedKg: number;
  totalDailyCostBdt: number;
  totalMonthlyCostBdt: number;
  percentageOfCost: number;
}

export interface FeedsCostReportTotals {
  totalAnimals: number;
  totalConcentrateKg: number;
  totalRoughageKg: number;
  totalDailyFeedKg: number;
  totalDailyCostBdt: number;
  totalMonthlyCostBdt: number;
  averageCostPerKgBdt: number;
  ruleSetSummaries: RuleSetCostSummary[];
}

@Injectable({
  providedIn: 'root'
})
export class FeedingReportPdfService {

  /**
   * Calculates grand totals and rule set groupings for active plans
   */
  calculateReportTotals(plans: AnimalFeedingPlan[]): FeedsCostReportTotals {
    const activePlans = plans.filter(p => p.isActive);
    let totalConcentrateKg = 0;
    let totalRoughageKg = 0;
    let totalDailyFeedKg = 0;
    let totalDailyCostBdt = 0;

    const ruleSetMap = new Map<string, {
      name: string;
      count: number;
      concKg: number;
      roughKg: number;
      feedKg: number;
      costBdt: number;
    }>();

    for (const plan of activePlans) {
      const conc = plan.concentrateKgPerDay ?? 0;
      const rough = plan.roughageKgPerDay ?? 0;
      const feed = plan.expectedDailyFeedKg || (conc + rough);
      const unitCost = plan.estimatedCostPerKgBdt ?? 0;
      const dailyCost = plan.estimatedDailyCostBdt || (feed * unitCost);

      totalConcentrateKg += conc;
      totalRoughageKg += rough;
      totalDailyFeedKg += feed;
      totalDailyCostBdt += dailyCost;

      const rsId = plan.ruleSetId || 'other';
      const rsName = plan.ruleSetName || 'Standard Plan';
      if (!ruleSetMap.has(rsId)) {
        ruleSetMap.set(rsId, {
          name: rsName,
          count: 0,
          concKg: 0,
          roughKg: 0,
          feedKg: 0,
          costBdt: 0
        });
      }
      const entry = ruleSetMap.get(rsId)!;
      entry.count++;
      entry.concKg += conc;
      entry.roughKg += rough;
      entry.feedKg += feed;
      entry.costBdt += dailyCost;
    }

    const totalMonthlyCostBdt = totalDailyCostBdt * 30;
    const averageCostPerKgBdt = totalDailyFeedKg > 0 ? (totalDailyCostBdt / totalDailyFeedKg) : 0;

    const ruleSetSummaries: RuleSetCostSummary[] = Array.from(ruleSetMap.values()).map(rs => ({
      ruleSetName: rs.name,
      animalCount: rs.count,
      totalDailyConcentrateKg: rs.concKg,
      totalDailyRoughageKg: rs.roughKg,
      totalDailyFeedKg: rs.feedKg,
      totalDailyCostBdt: rs.costBdt,
      totalMonthlyCostBdt: rs.costBdt * 30,
      percentageOfCost: totalDailyCostBdt > 0 ? (rs.costBdt / totalDailyCostBdt) * 100 : 0
    })).sort((a, b) => b.totalDailyCostBdt - a.totalDailyCostBdt);

    return {
      totalAnimals: activePlans.length,
      totalConcentrateKg,
      totalRoughageKg,
      totalDailyFeedKg,
      totalDailyCostBdt,
      totalMonthlyCostBdt,
      averageCostPerKgBdt,
      ruleSetSummaries
    };
  }

  /**
   * Generates a vector-sharp, professional A4 landscape PDF document
   */
  generatePdf(plans: AnimalFeedingPlan[], metadata: FeedsCostReportMetadata): jsPDF {
    const activePlans = plans.filter(p => p.isActive);
    const totals = this.calculateReportTotals(activePlans);
    const genDate = metadata.generatedAt || new Date();
    const formattedDate = genDate.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
    const formattedTime = genDate.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: true
    });

    // Create A4 Landscape document (297 x 210 mm)
    const doc = new jsPDF({
      orientation: 'landscape',
      unit: 'mm',
      format: 'a4'
    });

    const pageWidth = 297;
    const pageHeight = 210;
    const margin = 12;
    const contentWidth = pageWidth - (margin * 2);

    // ── Primary Header Accent Bar ──────────────────────────────────────────────
    doc.setFillColor(5, 150, 105); // Emerald 600
    doc.rect(margin, 10, contentWidth, 3.5, 'F');

    // ── Brand & Organization Details ───────────────────────────────────────────
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(16);
    doc.setTextColor(15, 23, 42); // Slate 900
    doc.text('FARM360 AI', margin, 20);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8.5);
    doc.setTextColor(100, 116, 139); // Slate 500
    doc.text('INTELLIGENT LIVESTOCK FEEDING & NUTRITION ENGINE', margin, 24.5);

    // Right-aligned report header details
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(13);
    doc.setTextColor(5, 150, 105); // Emerald 600
    doc.text('ANIMAL-WISE FEEDS & COST REPORT', pageWidth - margin, 20, { align: 'right' });

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8.5);
    doc.setTextColor(71, 85, 105);
    doc.text(`Farm: ${metadata.farmName || 'Primary Farm'}  |  Org: ${metadata.orgName || 'Farm360 Enterprise'}`, pageWidth - margin, 24.5, { align: 'right' });
    doc.text(`Generated: ${formattedDate} at ${formattedTime}  |  Status: ACTIVE PLANS ONLY`, pageWidth - margin, 28.5, { align: 'right' });

    // Subtle horizontal divider
    doc.setDrawColor(226, 232, 240); // Slate 200
    doc.setLineWidth(0.4);
    doc.line(margin, 31, pageWidth - margin, 31);

    // ── Executive KPI Metric Cards (5 Cards) ───────────────────────────────────
    const cardY = 34;
    const cardHeight = 16.5;
    const cardGap = 3.5;
    const cardWidth = (contentWidth - (cardGap * 4)) / 5;

    const kpis = [
      {
        label: 'ACTIVE ANIMALS',
        value: `${totals.totalAnimals} Head`,
        sub: 'Enrolled Cattle',
        bg: [240, 253, 244], // Emerald 50
        border: [167, 243, 208], // Emerald 200
        text: [4, 120, 87]
      },
      {
        label: 'DAILY FEED VOL.',
        value: `${totals.totalDailyFeedKg.toFixed(1)} kg`,
        sub: `Conc: ${totals.totalConcentrateKg.toFixed(1)} | Rough: ${totals.totalRoughageKg.toFixed(1)}`,
        bg: [240, 249, 255], // Sky 50
        border: [186, 230, 253], // Sky 200
        text: [3, 105, 161]
      },
      {
        label: 'AVG. FEED COST',
        value: `BDT ${totals.averageCostPerKgBdt.toFixed(2)}/kg`,
        sub: 'Weighted Average',
        bg: [254, 252, 232], // Yellow 50
        border: [254, 240, 138], // Yellow 200
        text: [161, 98, 7]
      },
      {
        label: 'DAILY EXPENDITURE',
        value: `BDT ${Math.round(totals.totalDailyCostBdt).toLocaleString()}`,
        sub: 'Per Day Feed Budget',
        bg: [245, 243, 255], // Indigo 50
        border: [221, 214, 254], // Indigo 200
        text: [109, 40, 217]
      },
      {
        label: 'PROJECTED 30-DAY',
        value: `BDT ${Math.round(totals.totalMonthlyCostBdt).toLocaleString()}`,
        sub: 'Monthly Forecast',
        bg: [255, 241, 242], // Rose 50
        border: [254, 205, 211], // Rose 200
        text: [190, 18, 60]
      }
    ];

    kpis.forEach((kpi, idx) => {
      const x = margin + (idx * (cardWidth + cardGap));
      doc.setFillColor(kpi.bg[0], kpi.bg[1], kpi.bg[2]);
      doc.setDrawColor(kpi.border[0], kpi.border[1], kpi.border[2]);
      doc.roundedRect(x, cardY, cardWidth, cardHeight, 2, 2, 'FD');

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(6.8);
      doc.setTextColor(100, 116, 139);
      doc.text(kpi.label, x + 3.5, cardY + 4.2);

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(10.5);
      doc.setTextColor(kpi.text[0], kpi.text[1], kpi.text[2]);
      doc.text(kpi.value, x + 3.5, cardY + 9.5);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(6.5);
      doc.setTextColor(100, 116, 139);
      doc.text(kpi.sub, x + 3.5, cardY + 13.8);
    });

    // ── Main Animal-Wise Table ─────────────────────────────────────────────────
    const tableStartY = cardY + cardHeight + 4.5;

    const tableHeaders = [
      '#',
      'Animal Tag',
      'Species',
      'Weight (kg)',
      'Rule Set',
      'Formula / Feed',
      'Conc. (kg)',
      'Rough. (kg)',
      'Total (kg/day)',
      'Rate (BDT/kg)',
      'Daily Cost (BDT)',
      '30-Day Cost (BDT)'
    ];

    const tableRows = activePlans.map((plan, i) => {
      const conc = plan.concentrateKgPerDay ?? 0;
      const rough = plan.roughageKgPerDay ?? 0;
      const feed = plan.expectedDailyFeedKg || (conc + rough);
      const unitCost = plan.estimatedCostPerKgBdt ?? 0;
      const dailyCost = plan.estimatedDailyCostBdt || (feed * unitCost);
      const monthlyCost = dailyCost * 30;

      return [
        (i + 1).toString(),
        plan.animalTag || 'Unknown',
        plan.animalSpecies || 'Cattle',
        plan.animalWeightKg ? plan.animalWeightKg.toFixed(1) : '—',
        plan.ruleSetName || 'Standard',
        plan.formulaName || 'Standard Ration',
        conc > 0 ? conc.toFixed(2) : '0.00',
        rough > 0 ? rough.toFixed(2) : '0.00',
        feed.toFixed(2),
        unitCost > 0 ? unitCost.toFixed(2) : '0.00',
        dailyCost.toFixed(2),
        monthlyCost.toFixed(2)
      ];
    });

    // Grand Totals Footer Row
    const tableFoot = [[
      '',
      `TOTALS (${totals.totalAnimals} Head)`,
      '',
      '',
      '',
      '',
      totals.totalConcentrateKg.toFixed(2),
      totals.totalRoughageKg.toFixed(2),
      totals.totalDailyFeedKg.toFixed(2),
      totals.averageCostPerKgBdt.toFixed(2),
      totals.totalDailyCostBdt.toFixed(2),
      totals.totalMonthlyCostBdt.toFixed(2)
    ]];

    autoTable(doc, {
      startY: tableStartY,
      head: [tableHeaders],
      body: tableRows,
      foot: tableFoot,
      theme: 'grid',
      styles: {
        fontSize: 7.2,
        cellPadding: 1.8,
        textColor: [30, 41, 59],
        lineColor: [226, 232, 240],
        lineWidth: 0.2
      },
      headStyles: {
        fillColor: [5, 150, 105], // Emerald 600
        textColor: [255, 255, 255],
        fontStyle: 'bold',
        fontSize: 7.5,
        halign: 'center'
      },
      footStyles: {
        fillColor: [241, 245, 249], // Slate 100
        textColor: [15, 23, 42],
        fontStyle: 'bold',
        fontSize: 7.5,
        lineColor: [203, 213, 225],
        lineWidth: 0.3
      },
      alternateRowStyles: {
        fillColor: [248, 250, 252] // Slate 50
      },
      columnStyles: {
        0: { halign: 'center', cellWidth: 7 }, // #
        1: { halign: 'left', fontStyle: 'bold', cellWidth: 24 }, // Tag
        2: { halign: 'center', cellWidth: 16 }, // Species
        3: { halign: 'right', cellWidth: 18 }, // Weight
        4: { halign: 'left', cellWidth: 38 }, // Rule Set
        5: { halign: 'left', cellWidth: 38 }, // Formula
        6: { halign: 'right', cellWidth: 18 }, // Conc
        7: { halign: 'right', cellWidth: 18 }, // Rough
        8: { halign: 'right', fontStyle: 'bold', textColor: [5, 150, 105], cellWidth: 22 }, // Total Feed
        9: { halign: 'right', cellWidth: 22 }, // Rate
        10: { halign: 'right', fontStyle: 'bold', cellWidth: 24 }, // Daily Cost
        11: { halign: 'right', fontStyle: 'bold', cellWidth: 28 } // Monthly Cost
      },
      margin: { left: margin, right: margin, bottom: 22 }
    });

    // ── Rule Set Summary Table ─────────────────────────────────────────────────
    const currentY = (doc as any).lastAutoTable?.finalY || tableStartY + 50;

    // Check if space remains for Rule Set Breakdown & Signatures, or add new page
    let ruleSetStartY = currentY + 6;
    if (ruleSetStartY + 45 > pageHeight) {
      doc.addPage();
      ruleSetStartY = 16;
    }

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(9);
    doc.setTextColor(15, 23, 42);
    doc.text('RULE SET DISTRIBUTION & COST ALLOCATION', margin, ruleSetStartY);

    const ruleSetHeaders = [
      'Feeding Rule Set',
      'Animals',
      'Daily Conc. (kg)',
      'Daily Rough. (kg)',
      'Total Feed (kg/day)',
      'Daily Cost (BDT)',
      '30-Day Cost (BDT)',
      '% Share of Cost'
    ];

    const ruleSetRows = totals.ruleSetSummaries.map(rs => [
      rs.ruleSetName,
      rs.animalCount.toString(),
      rs.totalDailyConcentrateKg.toFixed(2),
      rs.totalDailyRoughageKg.toFixed(2),
      rs.totalDailyFeedKg.toFixed(2),
      rs.totalDailyCostBdt.toFixed(2),
      rs.totalMonthlyCostBdt.toFixed(2),
      `${rs.percentageOfCost.toFixed(1)}%`
    ]);

    autoTable(doc, {
      startY: ruleSetStartY + 2.5,
      head: [ruleSetHeaders],
      body: ruleSetRows,
      theme: 'grid',
      styles: {
        fontSize: 7.2,
        cellPadding: 1.6,
        textColor: [30, 41, 59],
        lineColor: [226, 232, 240],
        lineWidth: 0.2
      },
      headStyles: {
        fillColor: [15, 118, 110], // Teal 700
        textColor: [255, 255, 255],
        fontStyle: 'bold',
        fontSize: 7.5,
        halign: 'center'
      },
      alternateRowStyles: {
        fillColor: [248, 250, 252]
      },
      columnStyles: {
        0: { halign: 'left', fontStyle: 'bold', cellWidth: 70 },
        1: { halign: 'center', cellWidth: 20 },
        2: { halign: 'right', cellWidth: 28 },
        3: { halign: 'right', cellWidth: 28 },
        4: { halign: 'right', fontStyle: 'bold', textColor: [15, 118, 110], cellWidth: 32 },
        5: { halign: 'right', cellWidth: 30 },
        6: { halign: 'right', fontStyle: 'bold', cellWidth: 35 },
        7: { halign: 'center', cellWidth: 25 }
      },
      margin: { left: margin, right: margin, bottom: 22 }
    });

    // ── Sign-off & Verification Block ──────────────────────────────────────────
    const afterRuleTableY = (doc as any).lastAutoTable?.finalY || ruleSetStartY + 30;
    let sigY = afterRuleTableY + 12;

    if (sigY + 20 > pageHeight - 12) {
      doc.addPage();
      sigY = 25;
    }

    const colWidth = (contentWidth - 40) / 3;
    const sigBoxes = [
      { role: 'Prepared By', title: 'Feed Nutrition Specialist', x: margin },
      { role: 'Verified By', title: 'Veterinarian / Farm Supervisor', x: margin + colWidth + 20 },
      { role: 'Approved By', title: 'General Farm Manager', x: margin + (colWidth * 2) + 40 }
    ];

    sigBoxes.forEach(box => {
      doc.setDrawColor(148, 163, 184); // Slate 400
      doc.setLineWidth(0.3);
      doc.line(box.x, sigY + 8, box.x + colWidth, sigY + 8);

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(7.5);
      doc.setTextColor(30, 41, 59);
      doc.text(box.role, box.x + (colWidth / 2), sigY + 12, { align: 'center' });

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(6.5);
      doc.setTextColor(100, 116, 139);
      doc.text(box.title, box.x + (colWidth / 2), sigY + 15.5, { align: 'center' });
    });

    // ── Running Page Footers (didDrawPage style) ───────────────────────────────
    const totalPages = doc.getNumberOfPages();
    for (let i = 1; i <= totalPages; i++) {
      doc.setPage(i);

      // Top running header on subsequent pages
      if (i > 1) {
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(7.5);
        doc.setTextColor(100, 116, 139);
        doc.text(`Farm360 AI  •  Active Feeds & Cost Report  •  ${metadata.farmName || ''}`, margin, 8);
        doc.setDrawColor(226, 232, 240);
        doc.setLineWidth(0.2);
        doc.line(margin, 9.5, pageWidth - margin, 9.5);
      }

      // Bottom running footer
      doc.setDrawColor(226, 232, 240);
      doc.setLineWidth(0.2);
      doc.line(margin, pageHeight - 8, pageWidth - margin, pageHeight - 8);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(6.8);
      doc.setTextColor(148, 163, 184); // Slate 400
      doc.text('Farm360 AI Livestock Suite  •  Confidential Farm Operational Record  •  Amounts in BDT', margin, pageHeight - 4.5);
      doc.text(`Page ${i} of ${totalPages}`, pageWidth - margin, pageHeight - 4.5, { align: 'right' });
    }

    return doc;
  }

  /**
   * Triggers browser download of the PDF file
   */
  downloadPdf(plans: AnimalFeedingPlan[], metadata: FeedsCostReportMetadata, filename?: string): void {
    const doc = this.generatePdf(plans, metadata);
    const dateStr = (metadata.generatedAt || new Date()).toISOString().split('T')[0];
    const safeFarmName = (metadata.farmName || 'Farm').replace(/[^a-zA-Z0-9_-]/g, '_');
    const finalName = filename || `Farm360_Feeds_Cost_Report_${safeFarmName}_${dateStr}.pdf`;
    doc.save(finalName);
  }

  /**
   * Creates a Blob URL for inline iframe preview or modal viewing
   */
  getPdfBlobUrl(plans: AnimalFeedingPlan[], metadata: FeedsCostReportMetadata): string {
    const doc = this.generatePdf(plans, metadata);
    const blob = doc.output('blob');
    return URL.createObjectURL(blob);
  }

  /**
   * Generates and downloads a multi-page PDF from DOM elements using html2canvas.
   * This guarantees 100% accurate Unicode / Bengali (বাংলা) rendering,
   * preserving complex text layout (CTL), ligatures, matras, and exact typography.
   */
  async downloadPdfFromHtml(containerElement: HTMLElement, filename?: string): Promise<void> {
    const canvas = await html2canvas(containerElement, {
      scale: 2,
      useCORS: true,
      allowTaint: true,
      backgroundColor: '#ffffff',
      logging: false,
      scrollX: 0,
      scrollY: 0
    });

    if (!canvas || canvas.width === 0 || canvas.height === 0) {
      console.error('Failed to capture report sheet: zero canvas dimensions');
      return;
    }

    const doc = new jsPDF({
      orientation: 'landscape',
      unit: 'mm',
      format: 'a4'
    });

    const pdfWidth = 297;
    const pdfHeight = 210;

    // Height of one A4 landscape page in canvas pixel units
    const pageHeightPx = Math.floor((canvas.width * pdfHeight) / pdfWidth);
    const totalPages = Math.max(1, Math.ceil(canvas.height / pageHeightPx));

    for (let page = 0; page < totalPages; page++) {
      if (page > 0) {
        doc.addPage('a4', 'landscape');
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

      const imgData = pageCanvas.toDataURL('image/jpeg', 0.95);
      doc.addImage(imgData, 'JPEG', 0, 0, pdfWidth, pdfHeight);
    }

    const safeFilename = filename || `Farm360_Feeds_Cost_Report_${new Date().toISOString().split('T')[0]}.pdf`;
    doc.save(safeFilename);
  }
}
