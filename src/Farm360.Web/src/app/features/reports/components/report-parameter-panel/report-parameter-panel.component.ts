import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { DATE_RANGE_PRESETS, LocalizedText, ReportParameter, localized } from '../../models/report.models';
import { ReportEntityPickerComponent } from '../report-entity-picker/report-entity-picker.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { I18nService } from '../../../../core/i18n/i18n.service';

/**
 * Renders any report's inputs from its parameter schema alone.
 *
 * There is deliberately no per-report form: a new report on the server appears here complete,
 * which is the difference between a report platform and thirty hand-built screens.
 */
@Component({
  selector: 'app-report-parameter-panel',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatCheckboxModule, MatButtonModule, MatIconModule,
    MatDatepickerModule, MatNativeDateModule,
    ReportEntityPickerComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="panel" (ngSubmit)="submit()">
      <h3 class="heading">{{ t('Parameters', 'প্যারামিটার') }}</h3>

      @for (p of parameters(); track p.name) {
        <div class="field">
          @switch (controlKind(p)) {
            @case ('dateRange') {
              <div class="space-y-2">
                <mat-form-field appearance="outline" subscriptSizing="dynamic">
                  <mat-label>{{ L(p.label) }}{{ p.required ? ' *' : '' }}</mat-label>
                  <mat-select [ngModel]="dateRangeSelection(p.name)" [name]="p.name"
                              (ngModelChange)="onDateRangePresetChange(p.name, $event)">
                    @for (preset of presets; track preset.value) {
                      <mat-option [value]="preset.value">{{ L(preset.label) }}</mat-option>
                    }
                    <mat-option value="custom">{{ t('Custom date range…', 'নির্দিষ্ট তারিখ…') }}</mat-option>
                  </mat-select>
                </mat-form-field>

                <div class="grid grid-cols-2 gap-2" *ngIf="isCustomDateRange(p.name)">
                  <mat-form-field appearance="outline" subscriptSizing="dynamic">
                    <mat-label>{{ t('From', 'শুরু') }}</mat-label>
                    <input matInput [matDatepicker]="fromPicker" [ngModel]="getFromDateObj(p.name)"
                           [name]="p.name + '_from'" [ngModelOptions]="{standalone: true}"
                           (dateChange)="onFromDateChange(p.name, $event.value)" placeholder="YYYY-MM-DD" />
                    <mat-datepicker-toggle matIconSuffix [for]="fromPicker"></mat-datepicker-toggle>
                    <mat-datepicker #fromPicker></mat-datepicker>
                  </mat-form-field>
                  <mat-form-field appearance="outline" subscriptSizing="dynamic">
                    <mat-label>{{ t('To', 'শেষ') }}</mat-label>
                    <input matInput [matDatepicker]="toPicker" [ngModel]="getToDateObj(p.name)"
                           [name]="p.name + '_to'" [ngModelOptions]="{standalone: true}"
                           (dateChange)="onToDateChange(p.name, $event.value)" placeholder="YYYY-MM-DD" />
                    <mat-datepicker-toggle matIconSuffix [for]="toPicker"></mat-datepicker-toggle>
                    <mat-datepicker #toPicker></mat-datepicker>
                  </mat-form-field>
                </div>
              </div>
            }
            @case ('select') {
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ L(p.label) }}{{ p.required ? ' *' : '' }}</mat-label>
                <mat-select [ngModel]="value(p.name)" [name]="p.name"
                            (ngModelChange)="set(p.name, $event)">
                  @for (opt of p.options ?? []; track opt.value) {
                    <mat-option [value]="opt.value">{{ L(opt.label) }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            }
            @case ('boolean') {
              <mat-checkbox [ngModel]="value(p.name) === 'true'" [name]="p.name"
                            (ngModelChange)="set(p.name, $event ? 'true' : 'false')">
                {{ L(p.label) }}
              </mat-checkbox>
            }
            @case ('date') {
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ L(p.label) }}{{ p.required ? ' *' : '' }}</mat-label>
                <input matInput [matDatepicker]="singleDatePicker" [ngModel]="getSingleDateObj(p.name)"
                       [name]="p.name" [ngModelOptions]="{standalone: true}"
                       (dateChange)="onSingleDateChange(p.name, $event.value)" placeholder="YYYY-MM-DD" />
                <mat-datepicker-toggle matIconSuffix [for]="singleDatePicker"></mat-datepicker-toggle>
                <mat-datepicker #singleDatePicker></mat-datepicker>
              </mat-form-field>
            }
            @case ('entity') {
              <app-report-entity-picker
                [entityType]="p.type"
                [value]="nullableValue(p.name)"
                [farmId]="nullableValue('farmId')"
                [label]="L(p.label)"
                [required]="p.required"
                (valueChange)="set(p.name, $event)" />
            }
            @case ('number') {
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ L(p.label) }}{{ p.required ? ' *' : '' }}</mat-label>
                <input matInput type="number" inputmode="decimal" [ngModel]="value(p.name)" [name]="p.name"
                       (ngModelChange)="setNumber(p.name, $event)"
                       [placeholder]="placeholder(p)" />
                @if (p.helpText) {
                  <mat-hint>{{ p.helpText }}</mat-hint>
                }
              </mat-form-field>
            }
            @default {
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ L(p.label) }}{{ p.required ? ' *' : '' }}</mat-label>
                <input matInput [ngModel]="value(p.name)" [name]="p.name"
                       (ngModelChange)="set(p.name, $event)"
                       [placeholder]="placeholder(p)" />
                @if (p.helpText) {
                  <mat-hint>{{ p.helpText }}</mat-hint>
                }
              </mat-form-field>
            }
          }
          @if (showErrors() && fieldError(p); as err) {
            <p class="field-error" role="alert">{{ err }}</p>
          }
        </div>
      }

      <h3 class="heading">{{ t('Presentation', 'উপস্থাপনা') }}</h3>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ t('Language', 'ভাষা') }}</mat-label>
        <mat-select [ngModel]="language()" name="language" (ngModelChange)="language.set($event)">
          <mat-option value="en">English</mat-option>
          <mat-option value="bn">বাংলা</mat-option>
        </mat-select>
      </mat-form-field>

      <mat-checkbox [ngModel]="bengaliNumerals()" name="bengaliNumerals"
                    (ngModelChange)="bengaliNumerals.set($event)">
        {{ t('Bengali numerals (০-৯)', 'বাংলা সংখ্যা (০-৯)') }}
      </mat-checkbox>

      <div class="actions">
        <button mat-flat-button color="primary" type="submit" [disabled]="busy()">
          <mat-icon>play_arrow</mat-icon> {{ busy() ? t('Running…', 'চলছে…') : t('Run report', 'প্রতিবেদন চালান') }}
        </button>
      </div>

      @if (showErrors() && !isValid()) {
        <p class="warn">{{ t('Fix the highlighted fields before running the report.', 'প্রতিবেদন চালানোর আগে চিহ্নিত ঘরগুলো ঠিক করুন।') }}</p>
      }
    </form>
  `,
  styles: [`
    .panel { display: flex; flex-direction: column; gap: 12px; padding: 16px; }
    .heading { margin: 4px 0 0; font-size: 12px; text-transform: uppercase;
               letter-spacing: .06em; font-weight: 700; color: #374151; }
    :host-context(.dark) .heading { color: #d1d5db; }
    .field, mat-form-field { width: 100%; }
    .actions { margin-top: 8px; }
    .actions button { width: 100%; }
    .warn { margin: 0; font-size: 12px; color: var(--mat-sys-error, #b3261e); }
    .field-error { margin: 4px 0 0 4px; font-size: 11px; color: var(--mat-sys-error, #b3261e); }
  `],
})
export class ReportParameterPanelComponent {
  private readonly workingContext = inject(WorkingContextService);
  private readonly i18n = inject(I18nService);

  readonly parameters = input.required<ReportParameter[]>();
  readonly initial = input<Record<string, string | null>>({});
  readonly busy = input(false);
  /** Language and numeral choice to start with (the last run's, or a re-run's). */
  readonly initialLanguage = input<'en' | 'bn'>(this.i18n.isBangla ? 'bn' : 'en');
  readonly initialBengaliNumerals = input(false);

  readonly run = output<{
    parameters: Record<string, string | null>;
    language: 'en' | 'bn';
    bengaliNumerals: boolean;
  }>();

  protected readonly presets = DATE_RANGE_PRESETS;
  protected readonly language = signal<'en' | 'bn'>(this.i18n.isBangla ? 'bn' : 'en');
  protected readonly bengaliNumerals = signal(false);

  private readonly values = signal<Record<string, string | null>>({});
  private readonly customMode = signal<Record<string, boolean>>({});

  /** Errors appear once the user tries to run, not while they are still filling the form in. */
  protected readonly showErrors = signal(false);

  constructor() {
    effect(() => this.language.set(this.initialLanguage()));
    effect(() => this.bengaliNumerals.set(this.initialBengaliNumerals()));
  }

  /** A schema label in the chosen report language (always Bangla when the interface is Bangla). */
  protected L(text: LocalizedText | null | undefined): string {
    return localized(text, this.i18n.isBangla ? 'bn' : this.language());
  }

  /** Fixed panel wording: Bangla when the interface or the chosen report language is Bangla. */
  protected t(en: string, bn: string): string {
    return this.i18n.isBangla || this.language() === 'bn' ? bn : en;
  }

  protected setNumber(name: string, value: unknown): void {
    this.set(name, value === null || value === undefined || value === '' ? '' : String(value));
  }

  /** The inline validation message for one field, or null when it is valid. */
  protected fieldError(p: ReportParameter): string | null {
    const v = this.value(p.name).trim();

    if (p.required && !v) {
      return this.t('This field is required.', 'এই ঘরটি পূরণ করা আবশ্যক।');
    }

    if (!v) {
      return null;
    }

    if ((p.type === 'Number' || p.type === 'WholeNumber') && !/^-?\d+(\.\d+)?$/.test(v)) {
      return this.t('Enter a number.', 'একটি সংখ্যা লিখুন।');
    }

    if (p.type === 'Number' && p.name === 'pricePerKg' && Number(v) <= 0) {
      return this.t('Enter a rate above zero, or leave blank for the farm rate.', 'শূন্যের বেশি দর লিখুন, অথবা খামারের দরের জন্য খালি রাখুন।');
    }

    if (p.type === 'DateRange' && v.includes('..')) {
      const [from, to] = v.split('..');
      if (!from || !to) {
        return this.t('Choose both a start and an end date.', 'শুরু ও শেষ দুটো তারিখই দিন।');
      }
      if (from > to) {
        return this.t('The start date is after the end date.', 'শুরুর তারিখ শেষের তারিখের পরে।');
      }
    }

    return null;
  }

  protected isCustomDateRange(name: string): boolean {
    if (this.customMode()[name] === true) return true;
    const v = this.value(name);
    return !!v && v.includes('..') && !this.presets.some(p => p.value === v);
  }

  protected dateRangeSelection(name: string): string {
    if (this.isCustomDateRange(name)) {
      return 'custom';
    }
    const val = this.value(name);
    if (this.presets.some(p => p.value === val)) {
      return val;
    }
    return 'current-month';
  }

  protected onDateRangePresetChange(name: string, selectedValue: string): void {
    if (selectedValue === 'custom') {
      this.customMode.update(m => ({ ...m, [name]: true }));
      this.set(name, `${this.getFromDate(name)}..${this.getToDate(name)}`);
    } else {
      this.customMode.update(m => ({ ...m, [name]: false }));
      this.set(name, selectedValue);
    }
  }

  private parseDateString(str: string | null | undefined): Date | null {
    if (!str) return null;
    const parts = str.trim().split('-');
    if (parts.length === 3) {
      const year = parseInt(parts[0], 10);
      const month = parseInt(parts[1], 10) - 1;
      const day = parseInt(parts[2], 10);
      if (!isNaN(year) && !isNaN(month) && !isNaN(day)) {
        return new Date(year, month, day);
      }
    }
    const parsed = new Date(str);
    return isNaN(parsed.getTime()) ? null : parsed;
  }

  /**
   * [ngModel] on the Material datepickers calls these getters on every change-detection pass.
   * Allocating a fresh Date each call makes NgModel see a "changed" reference every cycle, which
   * writes it back into the picker and schedules another cycle -- an infinite CD loop that freezes
   * the tab. Caching by the underlying date string keeps the same object across calls that resolve
   * to the same logical date, breaking the loop.
   */
  private readonly dateObjCache = new Map<string, { forKey: string; date: Date | null }>();

  private memoizedDate(cacheKey: string, forKey: string, compute: () => Date | null): Date | null {
    const cached = this.dateObjCache.get(cacheKey);
    if (cached && cached.forKey === forKey) {
      return cached.date;
    }
    const date = compute();
    this.dateObjCache.set(cacheKey, { forKey, date });
    return date;
  }

  private formatDateToString(date: Date | null | undefined): string {
    if (!date || isNaN(date.getTime())) return '';
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  protected getFromDate(name: string): string {
    const v = this.value(name);
    if (v && v.includes('..')) {
      const parts = v.split('..');
      if (parts[0]) return parts[0];
    }
    const today = new Date();
    const firstDay = new Date(today.getFullYear(), today.getMonth(), 1);
    return this.formatDateToString(firstDay);
  }

  protected getToDate(name: string): string {
    const v = this.value(name);
    if (v && v.includes('..')) {
      const parts = v.split('..');
      if (parts[1]) return parts[1];
    }
    return this.formatDateToString(new Date());
  }

  protected getFromDateObj(name: string): Date | null {
    const str = this.getFromDate(name);
    return this.memoizedDate(`from:${name}`, str, () => this.parseDateString(str));
  }

  protected getToDateObj(name: string): Date | null {
    const str = this.getToDate(name);
    return this.memoizedDate(`to:${name}`, str, () => this.parseDateString(str));
  }

  protected onFromDateChange(name: string, date: Date | null): void {
    if (!date) return;
    const fromStr = this.formatDateToString(date);
    const toStr = this.getToDate(name);
    this.set(name, `${fromStr}..${toStr}`);
  }

  protected onToDateChange(name: string, date: Date | null): void {
    if (!date) return;
    const fromStr = this.getFromDate(name);
    const toStr = this.formatDateToString(date);
    this.set(name, `${fromStr}..${toStr}`);
  }

  protected getSingleDateObj(name: string): Date | null {
    const val = this.value(name);
    const forKey = !val || val === 'today' ? 'today' : val;
    return this.memoizedDate(`single:${name}`, forKey, () =>
      forKey === 'today' ? new Date() : this.parseDateString(val));
  }

  protected onSingleDateChange(name: string, date: Date | null): void {
    if (!date) return;
    this.set(name, this.formatDateToString(date));
  }

  protected readonly isValid = computed(() =>
    this.parameters().every(p => this.fieldError(p) === null),
  );

  protected value(name: string): string {
    const explicit = this.values()[name];
    if (explicit !== undefined && explicit !== null) {
      return explicit;
    }

    const fromCaller = this.initial()[name];
    if (fromCaller) {
      return fromCaller;
    }

    const defaultVal = this.parameters().find(p => p.name === name)?.defaultValue;
    if (defaultVal) {
      return defaultVal;
    }

    if (name === 'farmId') {
      return this.workingContext.currentFarmValue?.id ?? '';
    }

    return '';
  }

  protected set(name: string, value: string | null): void {
    this.values.update(current => ({ ...current, [name]: value }));
  }

  /** Same as value(), but empty means "unset" rather than the empty string -- what the entity picker's typed inputs expect. */
  protected nullableValue(name: string): string | null {
    const v = this.value(name);
    return v ? v : null;
  }

  protected controlKind(p: ReportParameter): string {
    switch (p.type) {
      case 'DateRange': return 'dateRange';
      case 'Select':
      case 'MultiSelect': return 'select';
      case 'Boolean': return 'boolean';
      case 'Date': return 'date';
      case 'Number':
      case 'WholeNumber': return 'number';
      case 'Animal':
      case 'Batch':
      case 'Farm':
      case 'Shed':
      case 'Breed':
      case 'InventoryItem': return 'entity';
      default: return 'text';
    }
  }

  protected placeholder(p: ReportParameter): string {
    return p.helpText ?? '';
  }

  protected submit(): void {
    this.showErrors.set(true);
    if (!this.isValid()) {
      return;
    }

    const parameters: Record<string, string | null> = {};
    for (const p of this.parameters()) {
      parameters[p.name] = this.value(p.name) || p.defaultValue || null;
    }

    this.run.emit({
      parameters,
      language: this.language(),
      bengaliNumerals: this.bengaliNumerals(),
    });
  }
}
