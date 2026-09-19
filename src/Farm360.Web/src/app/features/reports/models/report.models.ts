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
  | 'Select' | 'MultiSelect' | 'Animal' | 'Batch' | 'Farm' | 'Shed' | 'Breed';

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
}

export interface PageSetup {
  orientation: 'Portrait' | 'Landscape';
  marginMm: number;
  repeatHeader: boolean;
  showPageNumbers: boolean;
  showSignatureBlock: boolean;
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
}

export interface ReportCatalogItem {
  key: string;
  title: string;
  titleBn: string;
  description: string;
  category: ReportCategory;
  parameters: ReportParameter[];
}

export type ReportLanguage = 'en' | 'bn';

export interface RunReportRequest {
  parameters: Record<string, string | null>;
  language: ReportLanguage;
  bengaliNumerals: boolean;
}

export type ReportExportFormat = 'pdf' | 'xlsx' | 'csv';

/** Presets the server's ReportContext.Range understands. */
export const DATE_RANGE_PRESETS: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'current-month', label: 'This month' },
  { value: 'last-month', label: 'Last month' },
  { value: 'last-7-days', label: 'Last 7 days' },
  { value: 'last-30-days', label: 'Last 30 days' },
  { value: 'last-90-days', label: 'Last 90 days' },
  { value: 'current-year', label: 'This year' },
  { value: 'all-time', label: 'All time' },
];

export const CATEGORY_ICONS: Readonly<Record<ReportCategory, string>> = {
  Livestock: 'pets',
  Feeding: 'restaurant_menu',
  Health: 'healing',
  Finance: 'account_balance_wallet',
  Inventory: 'inventory',
  Intelligence: 'insights',
  Operations: 'today',
};
