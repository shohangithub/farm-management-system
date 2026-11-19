import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { finalize } from 'rxjs';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { NotificationService } from '../../../shared/services/notification.service';
import { ConfirmService } from '../../../shared/services/confirm.service';
import { HasUnsavedChanges } from '../../../shared/utils/unsaved-changes.guard';
import { AuthService } from '../../../core/services/auth.service';
import { parseApiError } from '../../../core/utils/error-parser';
import { FarmService } from '../../farms/services/farm.service';
import { FarmList } from '../../farms/models/farm.model';
import {
  BusinessRulesOverrides,
  BusinessRulesService,
  BusinessRulesSettings,
  BusinessRulesValues
} from './business-rules.service';

export const SETTINGS_EDIT_PERMISSION = 'settings.edit';

interface Option<T extends string> { value: T; label: string; }

/**
 * Settings → Farm rules. The organisation's own valuation prices and allocation methods, layered over
 * the platform defaults. Only future calculations change; recorded allocations keep their method.
 */
@Component({
  selector: 'app-business-rules',
  standalone: true,
  imports: [CommonModule, DatePipe, ReactiveFormsModule, MatIconModule, MatButtonModule,
            PageHeaderComponent, ErrorStateComponent, HasPermissionDirective, MoneyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header
      title="Farm rules"
      description="How your farm values animals and shares feed, labour and overhead costs between them."
      breadcrumbActiveNode="Farm rules">
      <div actions class="flex gap-2" *hasPermission="editPermission">
        <button mat-stroked-button type="button" (click)="resetToDefaults()"
                [disabled]="saving() || loading() || !settings()?.isCustomized" class="!rounded-xl">
          <mat-icon class="mr-1">restart_alt</mat-icon> Reset to defaults
        </button>
        <button mat-flat-button type="button" (click)="save()"
                [disabled]="saving() || loading() || !!loadError() || form.pristine"
                class="!rounded-xl !bg-emerald-600 !text-white">
          <mat-icon class="mr-1">{{ saving() ? 'hourglass_top' : 'save' }}</mat-icon>
          {{ saving() ? 'Saving…' : 'Save changes' }}
        </button>
      </div>
    </app-page-header>

    <div class="px-6 pb-8 mx-auto max-w-5xl space-y-6">
      <app-error-state *ngIf="loadError() as msg" [message]="msg" (retry)="load()"></app-error-state>

      <div *ngIf="loading()" class="text-sm text-gray-500 flex items-center gap-2">
        <mat-icon class="animate-spin !text-[18px]">progress_activity</mat-icon> Loading farm rules…
      </div>

      <div *ngIf="farms().length > 1" class="card !flex-row !items-center flex-wrap !gap-3">
        <label for="scope" class="text-sm font-semibold">Rules for</label>
        <select id="scope" class="field" [value]="farmId() ?? ''" (change)="changeScope($any($event.target))"
                [disabled]="saving() || loading()">
          <option value="">Whole organisation (all farms)</option>
          <option *ngFor="let f of farms()" [value]="f.id">{{ f.farmName }} only</option>
        </select>
        <p class="help w-full !mt-0">
          {{ farmId()
            ? 'Only this farm uses these values. Anything you leave at the organisation value keeps following the organisation.'
            : 'Every farm uses these unless a farm has its own value.' }}
        </p>
      </div>

      <ng-container *ngIf="settings() as s">
        <div class="p-3 rounded-xl bg-sky-50 dark:bg-sky-950/30 border border-sky-200 dark:border-sky-800 text-sm text-sky-800 dark:text-sky-200 flex gap-2">
          <mat-icon class="!text-[18px] shrink-0">info</mat-icon>
          <span>
            Changes apply to <b>future calculations only</b>. Feed and overhead costs already recorded keep the method and
            prices they were worked out with, so reports you have already issued will not change.
            <span *ngIf="s.lastModifiedAtUtc" class="block text-xs mt-1 opacity-80">Last changed {{ s.lastModifiedAtUtc | date:'d MMM yyyy, h:mm a' }}.</span>
          </span>
        </div>

        <div *ngIf="!canEdit()" class="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 text-sm text-amber-800 dark:text-amber-200 flex gap-2">
          <mat-icon class="!text-[18px] shrink-0">lock</mat-icon>
          <span>You can view these rules but not change them. Ask your organisation owner or an administrator.</span>
        </div>

        <form [formGroup]="form" class="space-y-6" (ngSubmit)="save()">
          <!-- ── Valuation & prices ─────────────────────────────────────── -->
          <section class="card">
            <h3 class="section-title"><mat-icon>sell</mat-icon> Valuation &amp; prices</h3>

            <div class="row">
              <label for="valuation">How an animal is valued <span *ngIf="isCustom('valuation')" class="chip">Custom</span></label>
              <select id="valuation" formControlName="valuation" class="field">
                <option *ngFor="let o of valuationOptions" [value]="o.value">{{ o.label }}</option>
              </select>
              <p class="help">Sets the "current value" shown for each animal and herd on dashboards, projections and the balance sheet. Default: {{ label(valuationOptions, s.inherited.valuation) }}.</p>
            </div>

            <div class="row">
              <label for="dressing">Dressing percentage (%) <span *ngIf="isCustom('defaultDressingPercentage')" class="chip">Custom</span></label>
              <input id="dressing" type="number" step="0.5" min="30" max="70" formControlName="dressingPercent" class="field w-40" />
              <p *ngIf="invalid('dressingPercent')" class="err">Enter a percentage between 30 and 70.</p>
              <p class="help">How much of a live animal's weight becomes saleable meat, for breeds that don't state their own. Used by the meat-yield valuation. Default: {{ s.inherited.defaultDressingPercentage * 100 | number:'1.0-1' }}%.</p>
            </div>

            <div class="row">
              <label for="meatPrice">Meat price per kg (৳) <span *ngIf="isCustom('defaultMeatPricePerKgBdt')" class="chip">Custom</span></label>
              <input id="meatPrice" type="number" step="1" min="1" formControlName="defaultMeatPricePerKgBdt" class="field w-40" />
              <p *ngIf="invalid('defaultMeatPricePerKgBdt')" class="err">Enter a price greater than zero (up to ৳ 1,00,000).</p>
              <p class="help">The meat price used to value animals when no market price is recorded for the farm. Default: {{ s.inherited.defaultMeatPricePerKgBdt | money }}.</p>
            </div>

            <div class="row">
              <label for="livePrice">Live-weight price per kg (৳) <span *ngIf="isCustom('defaultLiveWeightPricePerKgBdt')" class="chip">Custom</span></label>
              <input id="livePrice" type="number" step="1" min="1" formControlName="defaultLiveWeightPricePerKgBdt" class="field w-40" />
              <p *ngIf="invalid('defaultLiveWeightPricePerKgBdt')" class="err">Enter a price greater than zero (up to ৳ 1,00,000).</p>
              <p class="help">What a buyer pays per kg of live animal. Only used when animals are valued by live weight. Default: {{ s.inherited.defaultLiveWeightPricePerKgBdt | money }}.</p>
            </div>
          </section>

          <!-- ── Feed allocation ────────────────────────────────────────── -->
          <section class="card">
            <h3 class="section-title"><mat-icon>grass</mat-icon> Feed allocation</h3>

            <div class="row">
              <label for="feedAllocation">How shared feed is split <span *ngIf="isCustom('feedAllocation')" class="chip">Custom</span></label>
              <select id="feedAllocation" formControlName="feedAllocation" class="field">
                <option *ngFor="let o of feedAllocationOptions" [value]="o.value">{{ o.label }}</option>
              </select>
              <p class="help">When a batch, shed or pen is fed together, decides how much of that feed's cost each animal carries. Default: {{ label(feedAllocationOptions, s.inherited.feedAllocation) }}.</p>
            </div>

            <div class="row" *ngIf="form.controls.feedAllocation.value === 'MetabolicWeight'">
              <label for="exponent">Metabolic weight exponent <span *ngIf="isCustom('metabolicWeightExponent')" class="chip">Custom</span></label>
              <input id="exponent" type="number" step="0.01" min="0.5" max="1" formControlName="metabolicWeightExponent" class="field w-40" />
              <p *ngIf="invalid('metabolicWeightExponent')" class="err">Enter a value between 0.50 and 1.00.</p>
              <p class="help">How strongly heavier animals get a bigger share. 0.75 is the standard in cattle nutrition; 1.00 means strictly by weight. Default: {{ s.inherited.metabolicWeightExponent }}.</p>
            </div>

            <div class="row">
              <label for="basis">Group plan quantities are <span *ngIf="isCustom('groupPlanQuantityBasis')" class="chip">Custom</span></label>
              <select id="basis" formControlName="groupPlanQuantityBasis" class="field">
                <option *ngFor="let o of basisOptions" [value]="o.value">{{ o.label }}</option>
              </select>
              <p class="help">Whether the quantity on a group feeding plan is one animal's ration (multiplied by head count) or the whole group's total. Default: {{ label(basisOptions, s.inherited.groupPlanQuantityBasis) }}.</p>
            </div>

            <div class="row">
              <label for="fallbackWeight">Weight for unweighed animals (kg) <span *ngIf="isCustom('fallbackAnimalWeightKg')" class="chip">Custom</span></label>
              <input id="fallbackWeight" type="number" step="1" min="1" max="2000" formControlName="fallbackAnimalWeightKg" class="field w-40" />
              <p *ngIf="invalid('fallbackAnimalWeightKg')" class="err">Enter a weight between 1 and 2,000 kg.</p>
              <p class="help">Assumed weight for an animal that has never been weighed, so it still carries a fair share of the feed. Default: {{ s.inherited.fallbackAnimalWeightKg }} kg.</p>
            </div>
          </section>

          <!-- ── Overhead allocation ────────────────────────────────────── -->
          <section class="card">
            <h3 class="section-title"><mat-icon>account_balance_wallet</mat-icon> Overhead allocation</h3>

            <div class="row">
              <label for="overhead">How labour and overheads are shared <span *ngIf="isCustom('overheadAllocation')" class="chip">Custom</span></label>
              <select id="overhead" formControlName="overheadAllocation" class="field">
                <option *ngFor="let o of overheadOptions" [value]="o.value">{{ o.label }}</option>
              </select>
              <p class="help">Decides how wages, utilities and other farm-wide costs are added to each animal's cost. Default: {{ label(overheadOptions, s.inherited.overheadAllocation) }}.</p>
            </div>

            <div class="row">
              <label for="period">A farm-wide expense covers <span *ngIf="isCustom('overheadCostPeriod')" class="chip">Custom</span></label>
              <select id="period" formControlName="overheadCostPeriod" class="field">
                <option *ngFor="let o of periodOptions" [value]="o.value">{{ o.label }}</option>
              </select>
              <p class="help">Which animals share an expense: those on the farm during its whole month (good for salaries and bills) or only on the day it was paid. Default: {{ label(periodOptions, s.inherited.overheadCostPeriod) }}.</p>
            </div>
          </section>

          <!-- ── Reports & language ─────────────────────────────────────── -->
          <section class="card">
            <h3 class="section-title"><mat-icon>translate</mat-icon> Reports &amp; language</h3>

            <div class="row">
              <label for="language">Default report language <span *ngIf="isCustom('defaultReportLanguage')" class="chip">Custom</span></label>
              <select id="language" formControlName="defaultReportLanguage" class="field">
                <option *ngFor="let o of languageOptions" [value]="o.value">{{ o.label }}</option>
              </select>
              <p class="help">The language printed reports start in. You can still pick another language when running a report. Default: {{ label(languageOptions, s.inherited.defaultReportLanguage) }}.</p>
            </div>

            <div class="row">
              <label class="flex items-center gap-2 cursor-pointer">
                <input type="checkbox" formControlName="bengaliNumeralsByDefault" />
                Print Bengali numerals (০-৯) <span *ngIf="isCustom('bengaliNumeralsByDefault')" class="chip">Custom</span>
              </label>
              <p class="help">Shows numbers in reports as ০১২৩ instead of 0123. Default: {{ s.inherited.bengaliNumeralsByDefault ? 'on' : 'off' }}.</p>
            </div>
          </section>
        </form>
      </ng-container>
    </div>
  `,
  styles: [`
    .card { background: white; border-radius: 1rem; border: 1px solid rgb(243 244 246); padding: 1.25rem; display: flex; flex-direction: column; gap: 1rem; }
    :host-context(.dark) .card { background: rgb(31 41 55); border-color: rgb(55 65 81); }
    .section-title { font-size: 0.875rem; font-weight: 800; display: flex; align-items: center; gap: 0.5rem; }
    .section-title mat-icon { font-size: 18px; width: 18px; height: 18px; color: rgb(5 150 105); }
    .row { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.8125rem; }
    .row > label { font-weight: 600; }
    .help { font-size: 0.75rem; color: rgb(107 114 128); }
    .err { font-size: 0.75rem; color: rgb(225 29 72); }
    .chip { margin-left: 0.375rem; font-size: 0.625rem; font-weight: 700; padding: 0.0625rem 0.375rem; border-radius: 9999px; background: rgb(209 250 229); color: rgb(4 120 87); vertical-align: middle; }
    .field { max-width: 24rem; padding: 0.375rem 0.625rem; border-radius: 0.5rem; border: 1px solid rgb(209 213 219); background: transparent; font-size: 0.8125rem; outline: none; }
    .field:focus { border-color: rgb(16 185 129); }
    .field:disabled { opacity: 0.7; cursor: not-allowed; }
    :host-context(.dark) .field { border-color: rgb(55 65 81); }
  `]
})
export class BusinessRulesComponent implements OnInit, HasUnsavedChanges {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(BusinessRulesService);
  private readonly notify = inject(NotificationService);
  private readonly confirm = inject(ConfirmService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly farmService = inject(FarmService);

  /** null = organisation-wide rules; otherwise the farm whose overrides are being edited. */
  readonly farmId = signal<string | null>(null);
  readonly farms = signal<FarmList[]>([]);

  readonly editPermission = SETTINGS_EDIT_PERMISSION;

  readonly settings = signal<BusinessRulesSettings | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly canEdit = computed(() => {
    this.auth.currentUserSignal();
    return this.auth.hasPermission(SETTINGS_EDIT_PERMISSION);
  });

  readonly valuationOptions: Option<BusinessRulesValues['valuation']>[] = [
    { value: 'MeatYieldMarket', label: 'Expected meat yield × meat price' },
    { value: 'LiveWeightMarket', label: 'Live weight × live-weight price' },
    { value: 'CostBasis', label: 'What it has cost so far (purchase + costs)' },
  ];
  readonly feedAllocationOptions: Option<BusinessRulesValues['feedAllocation']>[] = [
    { value: 'MetabolicWeight', label: 'By body size (metabolic weight)' },
    { value: 'LiveWeight', label: 'By live weight' },
    { value: 'EqualPerHead', label: 'Equally per animal' },
  ];
  readonly basisOptions: Option<BusinessRulesValues['groupPlanQuantityBasis']>[] = [
    { value: 'PerHead', label: 'One animal’s ration' },
    { value: 'WholeGroup', label: 'The whole group’s total' },
  ];
  readonly overheadOptions: Option<BusinessRulesValues['overheadAllocation']>[] = [
    { value: 'PerHeadDay', label: 'Per animal per day on the farm' },
    { value: 'PerLiveWeight', label: 'By live weight' },
  ];
  readonly periodOptions: Option<BusinessRulesValues['overheadCostPeriod']>[] = [
    { value: 'CalendarMonth', label: 'The whole calendar month' },
    { value: 'TransactionDate', label: 'Only the day it was paid' },
  ];
  readonly languageOptions: Option<BusinessRulesValues['defaultReportLanguage']>[] = [
    { value: 'English', label: 'English' },
    { value: 'Bangla', label: 'বাংলা (Bangla)' },
  ];

  readonly form = this.fb.nonNullable.group({
    valuation: this.fb.nonNullable.control<BusinessRulesValues['valuation']>('MeatYieldMarket'),
    dressingPercent: [50, [Validators.required, Validators.min(30), Validators.max(70)]],
    defaultMeatPricePerKgBdt: [680, [Validators.required, Validators.min(0.01), Validators.max(100000)]],
    defaultLiveWeightPricePerKgBdt: [400, [Validators.required, Validators.min(0.01), Validators.max(100000)]],
    feedAllocation: this.fb.nonNullable.control<BusinessRulesValues['feedAllocation']>('MetabolicWeight'),
    metabolicWeightExponent: [0.75, [Validators.required, Validators.min(0.5), Validators.max(1)]],
    groupPlanQuantityBasis: this.fb.nonNullable.control<BusinessRulesValues['groupPlanQuantityBasis']>('PerHead'),
    fallbackAnimalWeightKg: [150, [Validators.required, Validators.min(1), Validators.max(2000)]],
    overheadAllocation: this.fb.nonNullable.control<BusinessRulesValues['overheadAllocation']>('PerHeadDay'),
    overheadCostPeriod: this.fb.nonNullable.control<BusinessRulesValues['overheadCostPeriod']>('CalendarMonth'),
    defaultReportLanguage: this.fb.nonNullable.control<BusinessRulesValues['defaultReportLanguage']>('English'),
    bengaliNumeralsByDefault: [false],
  });

  ngOnInit(): void {
    this.load();
    this.farmService.getAllFarms()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: f => this.farms.set(f ?? []), error: () => this.farms.set([]) });
  }

  changeScope(select: HTMLSelectElement): void {
    const next = select.value || null;
    if (next === this.farmId()) return;
    const go = () => { this.farmId.set(next); this.load(); };
    if (!this.form.dirty) { go(); return; }
    this.confirm.confirm({
      title: 'Discard unsaved changes?',
      message: 'You have changes that are not saved yet. Switching will discard them.',
      confirmText: 'Discard and switch',
      destructive: true,
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(ok => {
      if (ok) go();
      else select.value = this.farmId() ?? ''; // put the selector back
    });
  }

  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saving();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.api.get(this.farmId())
      .pipe(finalize(() => this.loading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: s => this.apply(s),
        error: err => this.loadError.set(parseApiError(err, 'Could not load your farm rules.')),
      });
  }

  save(): void {
    if (!this.canEdit() || this.saving()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.notify.warning('Please fix the highlighted fields before saving.');
      return;
    }
    this.persist(this.toOverrides(), 'Farm rules saved. New calculations will use them.');
  }

  resetToDefaults(): void {
    if (!this.canEdit() || this.saving()) return;
    this.confirm.confirm({
      title: this.farmId() ? 'Use the organisation rules for this farm?' : 'Reset farm rules to defaults?',
      message: (this.farmId()
                 ? 'This farm’s own values will be removed and it will follow the organisation rules again. '
                 : 'All your custom prices and allocation choices will be replaced by the standard Farm360 defaults. ') +
               'Costs and reports already recorded will not change.',
      confirmText: 'Reset to defaults',
      destructive: true,
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(ok => {
      if (ok) this.persist(this.emptyOverrides(), 'Farm rules reset to the defaults.');
    });
  }

  isCustom(field: keyof BusinessRulesValues): boolean {
    const s = this.settings();
    return !!s && s.overrides[field] !== null && s.overrides[field] !== undefined;
  }

  invalid(control: string): boolean {
    const c = this.form.get(control);
    return !!c && c.invalid && (c.touched || c.dirty);
  }

  label<T extends string>(options: Option<T>[], value: T): string {
    return options.find(o => o.value === value)?.label ?? value;
  }

  private persist(overrides: BusinessRulesOverrides, message: string): void {
    this.saving.set(true);
    this.api.save(overrides, this.farmId())
      .pipe(finalize(() => this.saving.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: s => {
          this.apply(s);
          this.notify.success(message);
        },
        error: err => this.notify.error(parseApiError(err, 'Could not save your farm rules.')),
      });
  }

  private apply(s: BusinessRulesSettings): void {
    this.settings.set(s);
    const e = s.effective;
    this.form.reset({
      valuation: e.valuation,
      dressingPercent: Math.round(e.defaultDressingPercentage * 10000) / 100,
      defaultMeatPricePerKgBdt: e.defaultMeatPricePerKgBdt,
      defaultLiveWeightPricePerKgBdt: e.defaultLiveWeightPricePerKgBdt,
      feedAllocation: e.feedAllocation,
      metabolicWeightExponent: e.metabolicWeightExponent,
      groupPlanQuantityBasis: e.groupPlanQuantityBasis,
      fallbackAnimalWeightKg: e.fallbackAnimalWeightKg,
      overheadAllocation: e.overheadAllocation,
      overheadCostPeriod: e.overheadCostPeriod,
      defaultReportLanguage: e.defaultReportLanguage,
      bengaliNumeralsByDefault: e.bengaliNumeralsByDefault,
    });
    if (this.canEdit()) this.form.enable(); else this.form.disable();
  }

  /**
   * A value equal to the inherited default is sent as null, so the organisation keeps following the
   * platform default for that rule instead of freezing today's value.
   */
  private toOverrides(): BusinessRulesOverrides {
    const v = this.form.getRawValue();
    const d = this.settings()!.inherited;
    const pick = <K extends keyof BusinessRulesValues>(key: K, value: BusinessRulesValues[K]): BusinessRulesValues[K] | null =>
      value === d[key] ? null : value;

    return {
      valuation: pick('valuation', v.valuation),
      defaultDressingPercentage: pick('defaultDressingPercentage', Math.round(Number(v.dressingPercent) * 100) / 10000),
      defaultMeatPricePerKgBdt: pick('defaultMeatPricePerKgBdt', Number(v.defaultMeatPricePerKgBdt)),
      defaultLiveWeightPricePerKgBdt: pick('defaultLiveWeightPricePerKgBdt', Number(v.defaultLiveWeightPricePerKgBdt)),
      feedAllocation: pick('feedAllocation', v.feedAllocation),
      metabolicWeightExponent: pick('metabolicWeightExponent', Number(v.metabolicWeightExponent)),
      groupPlanQuantityBasis: pick('groupPlanQuantityBasis', v.groupPlanQuantityBasis),
      fallbackAnimalWeightKg: pick('fallbackAnimalWeightKg', Number(v.fallbackAnimalWeightKg)),
      overheadAllocation: pick('overheadAllocation', v.overheadAllocation),
      overheadCostPeriod: pick('overheadCostPeriod', v.overheadCostPeriod),
      defaultReportLanguage: pick('defaultReportLanguage', v.defaultReportLanguage),
      bengaliNumeralsByDefault: pick('bengaliNumeralsByDefault', !!v.bengaliNumeralsByDefault),
    };
  }

  private emptyOverrides(): BusinessRulesOverrides {
    return {
      valuation: null, defaultDressingPercentage: null, defaultMeatPricePerKgBdt: null, defaultLiveWeightPricePerKgBdt: null,
      feedAllocation: null, metabolicWeightExponent: null, groupPlanQuantityBasis: null, fallbackAnimalWeightKg: null,
      overheadAllocation: null, overheadCostPeriod: null, defaultReportLanguage: null, bengaliNumeralsByDefault: null,
    };
  }
}
