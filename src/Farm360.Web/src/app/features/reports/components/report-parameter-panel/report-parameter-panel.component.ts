import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { DATE_RANGE_PRESETS, ReportParameter } from '../../models/report.models';
import { ReportEntityPickerComponent } from '../report-entity-picker/report-entity-picker.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';

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
    ReportEntityPickerComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="panel" (ngSubmit)="submit()">
      <h3 class="heading">Assumptions</h3>

      @for (p of parameters(); track p.name) {
        <div class="field">
          @switch (controlKind(p)) {
            @case ('dateRange') {
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ p.label.en }}</mat-label>
                <mat-select [ngModel]="value(p.name)" [name]="p.name"
                            (ngModelChange)="set(p.name, $event)">
                  @for (preset of presets; track preset.value) {
                    <mat-option [value]="preset.value">{{ preset.label }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            }
            @case ('select') {
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ p.label.en }}</mat-label>
                <mat-select [ngModel]="value(p.name)" [name]="p.name"
                            (ngModelChange)="set(p.name, $event)">
                  @for (opt of p.options ?? []; track opt.value) {
                    <mat-option [value]="opt.value">{{ opt.label.en }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            }
            @case ('boolean') {
              <mat-checkbox [ngModel]="value(p.name) === 'true'" [name]="p.name"
                            (ngModelChange)="set(p.name, $event ? 'true' : 'false')">
                {{ p.label.en }}
              </mat-checkbox>
            }
            @case ('date') {
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ p.label.en }}</mat-label>
                <input matInput type="date" [ngModel]="value(p.name)" [name]="p.name"
                       (ngModelChange)="set(p.name, $event)" />
              </mat-form-field>
            }
            @case ('entity') {
              <app-report-entity-picker
                [entityType]="p.type"
                [value]="nullableValue(p.name)"
                [farmId]="nullableValue('farmId')"
                [label]="p.label.en"
                [required]="p.required"
                (valueChange)="set(p.name, $event)" />
            }
            @default {
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ p.label.en }}{{ p.required ? ' *' : '' }}</mat-label>
                <input matInput [ngModel]="value(p.name)" [name]="p.name"
                       (ngModelChange)="set(p.name, $event)"
                       [placeholder]="placeholder(p)" />
                @if (p.helpText) {
                  <mat-hint>{{ p.helpText }}</mat-hint>
                }
              </mat-form-field>
            }
          }
        </div>
      }

      <h3 class="heading">Presentation</h3>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Language</mat-label>
        <mat-select [ngModel]="language()" name="language" (ngModelChange)="language.set($event)">
          <mat-option value="en">English</mat-option>
          <mat-option value="bn">বাংলা</mat-option>
        </mat-select>
      </mat-form-field>

      <mat-checkbox [ngModel]="bengaliNumerals()" name="bengaliNumerals"
                    (ngModelChange)="bengaliNumerals.set($event)">
        Bengali numerals (০-৯)
      </mat-checkbox>

      <div class="actions">
        <button mat-flat-button color="primary" type="submit" [disabled]="!isValid() || busy()">
          <mat-icon>play_arrow</mat-icon> Run report
        </button>
      </div>

      @if (!isValid()) {
        <p class="warn">Fill the required fields marked *.</p>
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
  `],
})
export class ReportParameterPanelComponent {
  private readonly workingContext = inject(WorkingContextService);

  readonly parameters = input.required<ReportParameter[]>();
  readonly initial = input<Record<string, string | null>>({});
  readonly busy = input(false);

  readonly run = output<{
    parameters: Record<string, string | null>;
    language: 'en' | 'bn';
    bengaliNumerals: boolean;
  }>();

  protected readonly presets = DATE_RANGE_PRESETS;
  protected readonly language = signal<'en' | 'bn'>('en');
  protected readonly bengaliNumerals = signal(false);

  private readonly values = signal<Record<string, string | null>>({});

  protected readonly isValid = computed(() =>
    this.parameters()
      .filter(p => p.required)
      .every(p => this.value(p.name).trim().length > 0),
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
      case 'Animal':
      case 'Batch':
      case 'Farm':
      case 'Shed':
      case 'Breed': return 'entity';
      default: return 'text';
    }
  }

  protected placeholder(p: ReportParameter): string {
    return p.helpText ?? '';
  }

  protected submit(): void {
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
