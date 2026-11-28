/**
 * Mirrors the server contracts in Farm360.Application/Reporting/Model.
 * Kept in lock-step deliberately: the viewer renders whatever the server describes, so a new
 * report needs no frontend change at all.
 */

export type ReportColumnType =
  | 'Text' | 'Date' | 'DateTime' | 'WholeNumber' | 'Number' | 'Money' | 'Percent' | 'Boolean';

export type ReportAlign = 'Left' | 'Center' | 'Right';

export type ReportParameterType =
  | 'Text' | 'WholeNumber' | 'Number' | 'Boolean' | 'Date' | 'DateRange'
  | 'Select' | 'MultiSelect' | 'Animal' | 'Batch' | 'Farm' | 'Shed' | 'Breed'
  | 'InventoryItem' | 'MoneyAccount' | 'Partner';

export type ReportCategory =
  | 'Livestock' | 'Feeding' | 'Health' | 'Finance' | 'Inventory' | 'Intelligence' | 'Operations';

export interface LocalizedText {
  en: string;
  bn: string;
}

export interface ReportSelectOption {
  value: string;
  label: LocalizedText;
}

export interface ReportParameter {
  name: string;
  label: LocalizedText;
  type: ReportParameterType;
  required: boolean;
  defaultValue?: string | null;
  options?: ReportSelectOption[] | null;
  helpText?: string | null;
}

export interface ReportColumn {
  field: string;
  header: LocalizedText;
  type: ReportColumnType;
  align: ReportAlign;
  decimals: number;
  width: number;
  isRelativeWidth: boolean;
  aggregate: string;
  mergeRepeating?: boolean;
}

/**
 * `v` is the raw value, `f` the server-formatted display string.
 * The viewer always prints `f`. Formatting in the browser would be a second implementation of
 * the same rules, and the first time the two disagreed the screen would contradict the PDF.
 */
export interface ReportCell {
  v: unknown;
  f: string;
}

export interface ReportRow {
  cells: ReportCell[];
  groupKey?: string | null;
}

export interface ReportGroupTotals {
  groupKey: string;
  groupLabel: string;
  rowCount: number;
  totals: ReportCell[];
}

export interface ReportMetaField {
  label: string;
  value: string;
}

export interface ReportMetaHeader {
  organizationName: string;
  farmName?: string | null;
  title: string;
  reportKey: string;
  runId: string;
  generatedByName: string;
  generatedAtUtc: string;
  parameters: ReportMetaField[];
  subject?: string | null;
  /** Farm-local stamp with its zone, e.g. "16-Nov-2026 14:05 BST (UTC+6)". */
  generatedAtLocal?: string | null;
  generatedAtDisplay?: string | null;
  timeZoneLabel?: string | null;
  /** Organisation address · phone · e-mail, whichever are on file. */
  contactLine?: string | null;
  /** Relative URL of the organisation logo (e.g. /uploads/organizations/…png). */
  logoUrl?: string | null;
}

export interface PageSetup {
  orientation: 'Portrait' | 'Landscape';
  marginMm: number;
  repeatHeader: boolean;
  showPageNumbers: boolean;
  showSignatureBlock: boolean;
}

export interface ReportSummaryFigure {
  label: string;
  value: string;
  /** 'good' | 'bad' | 'neutral' */
  tone?: string;
}

export interface ReportSummary {
  title: string;
  figures: ReportSummaryFigure[];
  insightsTitle?: string | null;
  insights: string[];
}

export interface ReportDataSet {
  key: string;
  title: string;
  category: ReportCategory;
  page: PageSetup;
  meta: ReportMetaHeader;
  columns: ReportColumn[];
  rows: ReportRow[];
  groups: ReportGroupTotals[];
  grandTotals: ReportCell[];
  hasGrandTotals: boolean;
  totalRowCount: number;
  language?: string;
  bengaliNumerals?: boolean;
  grandTotalLabel?: string | null;
  /** "How to read this report" lines, already in the report's language. */
  notes?: string[] | null;
  /** Headline figures and findings printed above the table (only some reports have one). */
  summary?: ReportSummary | null;
  grandTotalCaption?: string | null;
}

export interface ReportCatalogItem {
  key: string;
  title: string;
  titleBn: string;
  description: string;
  category: ReportCategory;
  parameters: ReportParameter[];
  descriptionBn?: string;
  permission?: string | null;
  /** Whether the user holds the export permission; export buttons are hidden otherwise. */
  canExport?: boolean;
}

/** One audited execution, as returned by GET /reports/runs. */
export interface ReportRunHistoryItem {
  id: string;
  reportKey: string;
  reportTitle: string;
  requestedByName: string;
  requestedAtUtc: string;
  format: string;
  outcome: string;
  rowCount: number;
  durationMs: number;
  outputHash?: string | null;
  failureReason?: string | null;
  parameters?: Record<string, string | null> | null;
  language?: string | null;
  bengaliNumerals?: boolean;
}

export type ReportLanguage = 'en' | 'bn';

export interface RunReportRequest {
  parameters: Record<string, string | null>;
  language: ReportLanguage;
  bengaliNumerals: boolean;
}

export type ReportExportFormat = 'pdf' | 'xlsx' | 'csv';

/**
 * Presets the server's ReportContext.Range understands. They are sent as keywords, not dates:
 * the server resolves them in the farm's own time zone (Asia/Dhaka) and financial year, so
 * "today" is never the browser's UTC date and "this financial year" follows the farm's settings.
 */
export const DATE_RANGE_PRESETS: ReadonlyArray<{ value: string; label: LocalizedText }> = [
  { value: 'today', label: { en: 'Today', bn: 'আজ' } },
  { value: 'current-month', label: { en: 'This month', bn: 'এই মাস' } },
  { value: 'last-month', label: { en: 'Last month', bn: 'গত মাস' } },
  { value: 'last-7-days', label: { en: 'Last 7 days', bn: 'গত ৭ দিন' } },
  { value: 'last-30-days', label: { en: 'Last 30 days', bn: 'গত ৩০ দিন' } },
  { value: 'last-90-days', label: { en: 'Last 90 days', bn: 'গত ৯০ দিন' } },
  { value: 'fiscal-year-to-date', label: { en: 'Financial year to date', bn: 'অর্থবছরের শুরু থেকে আজ' } },
  { value: 'current-fiscal-year', label: { en: 'This financial year', bn: 'চলতি অর্থবছর' } },
  { value: 'last-fiscal-year', label: { en: 'Last financial year', bn: 'গত অর্থবছর' } },
  { value: 'current-year', label: { en: 'This calendar year', bn: 'চলতি পঞ্জিকা বছর' } },
  { value: 'all-time', label: { en: 'All time', bn: 'সব সময়' } },
];

/** Picks the label for the report language, falling back to English when no translation exists. */
export function localized(text: LocalizedText | null | undefined, language: ReportLanguage): string {
  if (!text) {
    return '';
  }
  return language === 'bn' && text.bn ? text.bn : text.en;
}

export const CATEGORY_ICONS: Readonly<Record<ReportCategory, string>> = {
  Livestock: 'pets',
  Feeding: 'restaurant_menu',
  Health: 'healing',
  Finance: 'account_balance_wallet',
  Inventory: 'inventory',
  Intelligence: 'insights',
  Operations: 'today',
};
