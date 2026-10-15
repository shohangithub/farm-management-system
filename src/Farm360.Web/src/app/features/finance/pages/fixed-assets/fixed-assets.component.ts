import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, catchError, combineLatest, forkJoin, of, switchMap, tap } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { FinanceService } from '../../services/finance.service';
import { FinanceSubnavComponent } from '../../components/finance-subnav/finance-subnav.component';
import { AccountingSettings, FixedAsset, FixedAssetSchedule, SaveFixedAssetRequest } from '../../models/finance.model';

interface AssetForm extends SaveFixedAssetRequest {
  supplierName: string;
  supplierPhone: string;
  dueDate: string;
  paidNowBdt: number;
}

/**
 * Fixed-asset register: sheds, machinery, vehicles and land. Buying one posts it to the ledger
 * (cash, on credit or brought in by the owner); assets owned before the books start are entered
 * at their written-down value. Depreciation is calculated, not posted — the statements read it.
 */
@Component({
  selector: 'app-fixed-assets',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatIconModule, MatButtonModule, PageHeaderComponent, FinanceSubnavComponent, CurrencyPipe, DatePipe],
  template: `
    <app-page-header
      title="Fixed Assets"
      description="Sheds, machinery, vehicles and land — at cost less depreciation, with the auditor's fixed-asset schedule."
      breadcrumbActiveNode="Fixed Assets">
      <div actions>
        <button mat-flat-button (click)="newAsset()" class="!rounded-xl !bg-emerald-600 !text-white">
          <mat-icon>add</mat-icon> New Asset
        </button>
      </div>
    </app-page-header>

    <div class="px-6 py-4 mx-auto max-w-7xl space-y-6">
      <app-finance-subnav></app-finance-subnav>

      <div *ngIf="error()" class="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-800 text-sm text-rose-700 dark:text-rose-300 flex justify-between">
        <span>{{ error() }}</span><button (click)="error.set(null)"><mat-icon class="!text-[16px]">close</mat-icon></button>
      </div>
      <div *ngIf="notice()" class="p-3 rounded-xl bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800 text-sm text-emerald-700 dark:text-emerald-300">{{ notice() }}</div>
      <div *ngIf="settings() && !settings()!.booksStartDate" class="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 text-xs text-amber-800 dark:text-amber-200">
        To register assets the farm owned before using Farm360, first set the books start date in
        <a routerLink="/finance/accounts-setup" class="underline font-semibold">Accounts setup</a>.
      </div>

      <!-- Summary -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white/80 dark:bg-gray-800/80 rounded-2xl border border-gray-100 dark:border-gray-800/50 p-4">
          <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500">Assets in use</p>
          <h3 class="text-xl font-black mt-1">{{ totals().count }}</h3>
        </div>
        <div class="bg-white/80 dark:bg-gray-800/80 rounded-2xl border border-gray-100 dark:border-gray-800/50 p-4">
          <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500">Cost</p>
          <h3 class="text-xl font-black mt-1">{{ totals().cost | currency:'BDT ':'symbol':'1.0-0' }}</h3>
        </div>
        <div class="bg-white/80 dark:bg-gray-800/80 rounded-2xl border border-gray-100 dark:border-gray-800/50 p-4">
          <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500">Accumulated depreciation</p>
          <h3 class="text-xl font-black mt-1 text-amber-600">{{ totals().depreciation | currency:'BDT ':'symbol':'1.0-0' }}</h3>
        </div>
        <div class="bg-white/80 dark:bg-gray-800/80 rounded-2xl border border-gray-100 dark:border-gray-800/50 p-4">
          <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500">Written-down value</p>
          <h3 class="text-xl font-black mt-1 text-emerald-600">{{ totals().wdv | currency:'BDT ':'symbol':'1.0-0' }}</h3>
        </div>
      </div>

      <!-- Form -->
      <div *ngIf="form() as f" class="bg-white dark:bg-gray-800 rounded-2xl border border-emerald-200 dark:border-emerald-800/60 p-5 space-y-4">
        <h3 class="text-sm font-black">{{ editingId() ? 'Edit asset' : 'Register an asset' }}</h3>
        <div class="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
          <label class="flex flex-col gap-1">Name
            <input [(ngModel)]="f.name" class="field" placeholder="e.g. Cattle shed 2" />
          </label>
          <label class="flex flex-col gap-1">Category
            <select [(ngModel)]="f.category" class="field">
              <option *ngFor="let c of categories" [value]="c.value">{{ c.label }}</option>
            </select>
          </label>
          <label class="flex flex-col gap-1">How acquired
            <select [(ngModel)]="f.acquisition" class="field" [disabled]="!!editingId()">
              <option value="Cash">Bought — paid in cash</option>
              <option value="Credit">Bought on credit (supplier bill)</option>
              <option value="OwnerContributed">Brought in by the owner</option>
              <option value="Opening">Owned before the books start (opening)</option>
            </select>
          </label>
          <label class="flex flex-col gap-1">
            {{ f.acquisition === 'Opening' ? 'Written-down value at books start (BDT)' : 'Cost (BDT)' }}
            <input type="number" min="0" [(ngModel)]="f.costBdt" class="field" />
          </label>
          <label *ngIf="f.acquisition !== 'Opening'" class="flex flex-col gap-1">Purchase date
            <input type="date" [(ngModel)]="f.acquisitionDate" class="field" />
          </label>
          <div *ngIf="f.acquisition === 'Opening'" class="flex flex-col gap-1 justify-end text-gray-500">
            Dated {{ settings()?.booksStartDate | date:'d MMM yyyy' }} (books start)
          </div>
          <label class="flex flex-col gap-1">Location
            <input [(ngModel)]="f.location" class="field" />
          </label>
          <ng-container *ngIf="f.category !== 'Land'">
            <label class="flex flex-col gap-1">Depreciation method
              <select [(ngModel)]="f.method" class="field">
                <option value="ReducingBalance">Reducing balance (% per year)</option>
                <option value="StraightLine">Straight line (over useful life)</option>
              </select>
            </label>
            <label *ngIf="f.method === 'ReducingBalance'" class="flex flex-col gap-1">Rate % per year
              <input type="number" min="0" max="100" [(ngModel)]="f.ratePercent" class="field" />
            </label>
            <ng-container *ngIf="f.method === 'StraightLine'">
              <label class="flex flex-col gap-1">Useful life (years)
                <input type="number" min="0" [(ngModel)]="f.usefulLifeYears" class="field" />
              </label>
              <label class="flex flex-col gap-1">Residual value (BDT)
                <input type="number" min="0" [(ngModel)]="f.residualValueBdt" class="field" />
              </label>
            </ng-container>
          </ng-container>
          <div *ngIf="f.category === 'Land'" class="flex items-end text-gray-500">Land is not depreciated.</div>
          <ng-container *ngIf="f.acquisition === 'Credit' && !editingId()">
            <label class="flex flex-col gap-1">Supplier
              <input [(ngModel)]="f.supplierName" class="field" />
            </label>
            <label class="flex flex-col gap-1">Supplier phone
              <input [(ngModel)]="f.supplierPhone" class="field" />
            </label>
            <label class="flex flex-col gap-1">Due date
              <input type="date" [(ngModel)]="f.dueDate" class="field" />
            </label>
            <label class="flex flex-col gap-1">Paid now (BDT)
              <input type="number" min="0" [(ngModel)]="f.paidNowBdt" class="field" />
            </label>
          </ng-container>
          <label class="flex flex-col gap-1 md:col-span-3">Notes
            <input [(ngModel)]="f.notes" class="field" />
          </label>
        </div>
        <div class="flex gap-2 justify-end">
          <button mat-stroked-button (click)="form.set(null)" class="!rounded-xl">Cancel</button>
          <button mat-flat-button (click)="save(f)" [disabled]="busy()" class="!rounded-xl !bg-emerald-600 !text-white">Save</button>
        </div>
      </div>

      <!-- Dispose panel -->
      <div *ngIf="disposing() as d" class="bg-white dark:bg-gray-800 rounded-2xl border border-amber-200 dark:border-amber-800/60 p-5 space-y-3 text-xs">
        <h3 class="text-sm font-black">Sell / scrap {{ d.asset.name }}</h3>
        <p class="text-gray-500">The written-down value on the day is removed from the books; the difference to the sale price is a gain or loss on disposal. Enter 0 if scrapped.</p>
        <div class="flex gap-3 flex-wrap">
          <label class="flex flex-col gap-1">Date <input type="date" [(ngModel)]="d.date" class="field" /></label>
          <label class="flex flex-col gap-1">Sale proceeds (BDT) <input type="number" min="0" [(ngModel)]="d.proceeds" class="field" /></label>
        </div>
        <div class="flex gap-2 justify-end">
          <button mat-stroked-button (click)="disposing.set(null)" class="!rounded-xl">Cancel</button>
          <button mat-flat-button (click)="dispose()" [disabled]="busy()" class="!rounded-xl !bg-amber-600 !text-white">Record disposal</button>
        </div>
      </div>

      <!-- Tabs -->
      <div class="flex gap-2 text-xs font-semibold">
        <button (click)="tab.set('register')" class="px-3 py-1.5 rounded-lg" [ngClass]="tab() === 'register' ? 'bg-emerald-600 text-white' : 'bg-gray-100 dark:bg-gray-800'">Register</button>
        <button (click)="tab.set('schedule')" class="px-3 py-1.5 rounded-lg" [ngClass]="tab() === 'schedule' ? 'bg-emerald-600 text-white' : 'bg-gray-100 dark:bg-gray-800'">Fixed asset schedule</button>
      </div>

      <!-- Register -->
      <div *ngIf="tab() === 'register'" class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-800/50 overflow-x-auto">
        <table class="w-full text-sm">
          <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
            <tr>
              <th class="text-left px-4 py-2">Code</th><th class="text-left px-3 py-2">Asset</th><th class="text-left px-3 py-2">Acquired</th>
              <th class="text-left px-3 py-2">Depreciation</th><th class="text-right px-3 py-2">Cost</th>
              <th class="text-right px-3 py-2">Acc. dep.</th><th class="text-right px-3 py-2">WDV</th><th class="px-3 py-2"></th>
            </tr>
          </thead>
          <tbody>
            <tr *ngIf="!assets().length"><td colspan="8" class="px-4 py-8 text-center text-gray-400">No fixed assets registered yet.</td></tr>
            <tr *ngFor="let a of assets()" class="border-t border-gray-100 dark:border-gray-700/60" [class.opacity-60]="a.disposalDate">
              <td class="px-4 py-2 font-mono text-xs text-gray-500">{{ a.code }}</td>
              <td class="px-3 py-2">
                <span class="font-semibold">{{ a.name }}</span>
                <span class="block text-[11px] text-gray-500">{{ categoryLabel(a.category) }}<span *ngIf="a.location"> · {{ a.location }}</span></span>
              </td>
              <td class="px-3 py-2 text-xs">
                {{ a.acquisitionDate | date:'d MMM yyyy' }}
                <span class="block text-[11px] text-gray-500">{{ acquisitionLabel(a.acquisition) }}</span>
                <span *ngIf="a.disposalDate" class="block text-[11px] text-amber-600">Disposed {{ a.disposalDate | date:'d MMM yyyy' }} for {{ a.disposalProceedsBdt | currency:'BDT ':'symbol':'1.0-0' }}</span>
              </td>
              <td class="px-3 py-2 text-xs">{{ a.category === 'Land' ? '—' : (a.method === 'StraightLine' ? a.usefulLifeYears + ' yrs straight line' : a.ratePercent + '% reducing') }}</td>
              <td class="px-3 py-2 text-right font-mono">{{ a.costBdt | currency:'BDT ':'symbol':'1.0-0' }}</td>
              <td class="px-3 py-2 text-right font-mono text-amber-600">{{ a.accumulatedDepreciationBdt | currency:'BDT ':'symbol':'1.0-0' }}</td>
              <td class="px-3 py-2 text-right font-mono font-bold">{{ a.writtenDownValueBdt | currency:'BDT ':'symbol':'1.0-0' }}</td>
              <td class="px-3 py-2 text-right whitespace-nowrap">
                <button *ngIf="!a.disposalDate" (click)="edit(a)" title="Edit" class="p-1 text-gray-500 hover:text-emerald-600"><mat-icon class="!text-[18px]">edit</mat-icon></button>
                <button *ngIf="!a.disposalDate" (click)="startDispose(a)" title="Sell / scrap" class="p-1 text-gray-500 hover:text-amber-600"><mat-icon class="!text-[18px]">sell</mat-icon></button>
                <button *ngIf="a.disposalDate" (click)="cancelDisposal(a)" title="Undo disposal" class="p-1 text-gray-500 hover:text-amber-600"><mat-icon class="!text-[18px]">undo</mat-icon></button>
                <button (click)="remove(a)" title="Delete" class="p-1 text-gray-500 hover:text-rose-600"><mat-icon class="!text-[18px]">delete</mat-icon></button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Schedule -->
      <div *ngIf="tab() === 'schedule'" class="space-y-3">
        <div class="flex items-center gap-2 text-xs">
          <span class="text-gray-500">Period</span>
          <input type="date" [(ngModel)]="scheduleFrom" (change)="loadSchedule()" class="field" />
          <span>to</span>
          <input type="date" [(ngModel)]="scheduleTo" (change)="loadSchedule()" class="field" />
          <span class="text-gray-400">(blank start = financial year start)</span>
          <a routerLink="/reports/finance.fixed-asset-schedule" class="ml-auto underline text-emerald-700">Open as report (PDF / Excel)</a>
        </div>
        <div *ngIf="schedule() as sc" class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-800/50 overflow-x-auto">
          <table class="w-full text-xs">
            <thead class="bg-gray-50 dark:bg-gray-900/40 uppercase tracking-wider text-gray-500">
              <tr>
                <th class="text-left px-3 py-2" rowspan="2">Asset</th>
                <th class="text-center px-3 py-1 border-b border-gray-200 dark:border-gray-700" colspan="4">Cost</th>
                <th class="text-center px-3 py-1 border-b border-gray-200 dark:border-gray-700" colspan="4">Depreciation</th>
                <th class="text-right px-3 py-2" rowspan="2">WDV {{ sc.toDate | date:'d MMM yy' }}</th>
              </tr>
              <tr>
                <th class="text-right px-2 py-1">b/f</th><th class="text-right px-2 py-1">Additions</th><th class="text-right px-2 py-1">Disposals</th><th class="text-right px-2 py-1">c/f</th>
                <th class="text-right px-2 py-1">b/f</th><th class="text-right px-2 py-1">Charge</th><th class="text-right px-2 py-1">On disposal</th><th class="text-right px-2 py-1">c/f</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let l of sc.lines" class="border-t border-gray-100 dark:border-gray-700/60">
                <td class="px-3 py-1.5"><span class="font-mono text-gray-400">{{ l.code }}</span> {{ l.name }}</td>
                <td class="px-2 py-1.5 text-right font-mono">{{ l.costOpeningBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-1.5 text-right font-mono">{{ l.additionsBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-1.5 text-right font-mono">{{ l.disposalsBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-1.5 text-right font-mono font-semibold">{{ l.costClosingBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-1.5 text-right font-mono">{{ l.depreciationOpeningBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-1.5 text-right font-mono">{{ l.depreciationChargeBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-1.5 text-right font-mono">{{ l.depreciationOnDisposalBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-1.5 text-right font-mono font-semibold">{{ l.depreciationClosingBdt | number:'1.0-0' }}</td>
                <td class="px-3 py-1.5 text-right font-mono font-bold">{{ l.writtenDownValueBdt | number:'1.0-0' }}</td>
              </tr>
              <tr class="border-t-2 border-gray-800 dark:border-gray-200 font-black">
                <td class="px-3 py-2">Total ({{ sc.fromDate | date:'d MMM yy' }} – {{ sc.toDate | date:'d MMM yy' }})</td>
                <td class="px-2 py-2 text-right font-mono">{{ sc.totals.costOpeningBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-2 text-right font-mono">{{ sc.totals.additionsBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-2 text-right font-mono">{{ sc.totals.disposalsBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-2 text-right font-mono">{{ sc.totals.costClosingBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-2 text-right font-mono">{{ sc.totals.depreciationOpeningBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-2 text-right font-mono">{{ sc.totals.depreciationChargeBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-2 text-right font-mono">{{ sc.totals.depreciationOnDisposalBdt | number:'1.0-0' }}</td>
                <td class="px-2 py-2 text-right font-mono">{{ sc.totals.depreciationClosingBdt | number:'1.0-0' }}</td>
                <td class="px-3 py-2 text-right font-mono">{{ sc.totals.writtenDownValueBdt | number:'1.0-0' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .field { padding: 0.375rem 0.625rem; border-radius: 0.5rem; border: 1px solid rgb(209 213 219); background: transparent; font-size: 0.75rem; outline: none; }
    .field:focus { border-color: rgb(16 185 129); }
    :host-context(.dark) .field { border-color: rgb(55 65 81); }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FixedAssetsComponent {
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly destroyRef = inject(DestroyRef);

  readonly categories = [
    { value: 'BuildingAndShed', label: 'Building & shed' },
    { value: 'MachineryAndEquipment', label: 'Machinery & equipment' },
    { value: 'Vehicle', label: 'Vehicle' },
    { value: 'FurnitureAndOffice', label: 'Furniture & office' },
    { value: 'Land', label: 'Land' },
    { value: 'Other', label: 'Other' },
  ];

  readonly assets = signal<FixedAsset[]>([]);
  readonly settings = signal<AccountingSettings | null>(null);
  readonly schedule = signal<FixedAssetSchedule | null>(null);
  readonly form = signal<AssetForm | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly disposing = signal<{ asset: FixedAsset; date: string; proceeds: number } | null>(null);
  readonly tab = signal<'register' | 'schedule'>('register');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  scheduleFrom = '';
  scheduleTo = this.today();

  readonly totals = computed(() => {
    const live = this.assets().filter(a => !a.disposalDate);
    return {
      count: live.length,
      cost: live.reduce((s, a) => s + a.costBdt, 0),
      depreciation: live.reduce((s, a) => s + a.accumulatedDepreciationBdt, 0),
      wdv: live.reduce((s, a) => s + a.writtenDownValueBdt, 0),
    };
  });

  private readonly refresh$ = new BehaviorSubject<void>(undefined);

  constructor() {
    combineLatest([this.refresh$, this.context.currentFarm$]).pipe(
      switchMap(([, farm]) => !farm ? of(null) : forkJoin({
        assets: this.finance.getFixedAssets(farm.id),
        settings: this.finance.getAccountingSettings(farm.id),
      }).pipe(catchError(err => { this.error.set(parseApiError(err, 'Failed to load fixed assets.')); return of(null); }))),
      tap(() => this.loadSchedule()),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(r => {
      if (!r) return;
      this.assets.set(r.assets);
      this.settings.set(r.settings);
    });
  }

  private get farmId(): string | null {
    return this.context.currentFarmValue?.id ?? null;
  }

  private today(): string {
    return new Date().toISOString().split('T')[0];
  }

  categoryLabel(value: string): string {
    return this.categories.find(c => c.value === value)?.label ?? value;
  }

  acquisitionLabel(value: string): string {
    return ({ Cash: 'Paid in cash', Credit: 'On credit', OwnerContributed: 'From owner', Opening: 'Opening (before books start)' } as Record<string, string>)[value] ?? value;
  }

  newAsset(): void {
    this.editingId.set(null);
    this.form.set({
      name: '', category: 'BuildingAndShed', acquisition: 'Cash', acquisitionDate: this.today(), costBdt: 0,
      method: 'ReducingBalance', ratePercent: 10, usefulLifeYears: 0, residualValueBdt: 0, location: '', notes: '',
      supplierName: '', supplierPhone: '', dueDate: '', paidNowBdt: 0,
    });
  }

  edit(a: FixedAsset): void {
    this.editingId.set(a.id);
    this.form.set({
      name: a.name, category: a.category, acquisition: a.acquisition, acquisitionDate: a.acquisitionDate.substring(0, 10),
      costBdt: a.costBdt, method: a.method, ratePercent: a.ratePercent, usefulLifeYears: a.usefulLifeYears,
      residualValueBdt: a.residualValueBdt, location: a.location ?? '', notes: a.notes ?? '',
      supplierName: '', supplierPhone: '', dueDate: '', paidNowBdt: 0,
    });
  }

  save(f: AssetForm): void {
    const farmId = this.farmId;
    if (!farmId) return;
    const body: SaveFixedAssetRequest = {
      name: f.name, category: f.category, acquisition: f.acquisition,
      acquisitionDate: f.acquisition === 'Opening' ? (this.settings()?.booksStartDate ?? this.today()) : f.acquisitionDate,
      costBdt: +f.costBdt, method: f.method, ratePercent: +f.ratePercent, usefulLifeYears: +f.usefulLifeYears,
      residualValueBdt: +f.residualValueBdt, location: f.location || null, notes: f.notes || null,
      credit: f.acquisition === 'Credit' && !this.editingId()
        ? { partyName: f.supplierName, partyPhone: f.supplierPhone || null, dueDate: f.dueDate || null, paidNowBdt: +f.paidNowBdt || 0 }
        : null,
    };
    const id = this.editingId();
    this.busy.set(true);
    (id ? this.finance.updateFixedAsset(farmId, id, body) : this.finance.createFixedAsset(farmId, body)).subscribe({
      next: a => this.done(`${a.code} ${a.name} saved.`, () => this.form.set(null)),
      error: err => this.fail(err, 'Could not save the asset.'),
    });
  }

  startDispose(a: FixedAsset): void {
    this.disposing.set({ asset: a, date: this.today(), proceeds: 0 });
  }

  dispose(): void {
    const d = this.disposing();
    const farmId = this.farmId;
    if (!d || !farmId) return;
    this.busy.set(true);
    this.finance.disposeFixedAsset(farmId, d.asset.id, { disposalDate: d.date, proceedsBdt: +d.proceeds || 0 }).subscribe({
      next: () => this.done(`${d.asset.name} disposed.`, () => this.disposing.set(null)),
      error: err => this.fail(err, 'Could not record the disposal.'),
    });
  }

  cancelDisposal(a: FixedAsset): void {
    const farmId = this.farmId;
    if (!farmId || !confirm(`Undo the disposal of ${a.name}? The sale posting is removed.`)) return;
    this.busy.set(true);
    this.finance.cancelFixedAssetDisposal(farmId, a.id).subscribe({
      next: () => this.done('Disposal undone.'),
      error: err => this.fail(err, 'Could not undo the disposal.'),
    });
  }

  remove(a: FixedAsset): void {
    const farmId = this.farmId;
    if (!farmId || !confirm(`Delete ${a.code} ${a.name}? Its purchase and sale postings are removed from the ledger.`)) return;
    this.busy.set(true);
    this.finance.deleteFixedAsset(farmId, a.id).subscribe({
      next: () => this.done('Asset deleted.'),
      error: err => this.fail(err, 'Could not delete the asset.'),
    });
  }

  loadSchedule(): void {
    const farmId = this.farmId;
    if (!farmId) return;
    this.finance.getFixedAssetSchedule(farmId, this.scheduleFrom || null, this.scheduleTo || null).subscribe({
      next: s => this.schedule.set(s),
      error: err => this.error.set(parseApiError(err, 'Failed to load the fixed-asset schedule.')),
    });
  }

  private done(message: string, after?: () => void): void {
    this.busy.set(false);
    this.error.set(null);
    this.notice.set(message);
    after?.();
    this.refresh$.next();
  }

  private fail(err: unknown, fallback: string): void {
    this.busy.set(false);
    this.error.set(parseApiError(err, fallback));
  }
}
