import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { FinanceSubnavComponent } from '../../components/finance-subnav/finance-subnav.component';
import { FinanceService } from '../../services/finance.service';
import { PartnerStatement, moneyAccountName } from '../../models/finance.model';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

/** One partner's statement: capital movements, profit allocated, and their part of the farm's net assets. */
@Component({
  selector: 'app-partner-statement',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, RouterModule, MatIconModule, MatTooltipModule,
    PageHeaderComponent, LoadingComponent, ErrorStateComponent, FinanceSubnavComponent, MoneyPipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header [title]="(s()?.partner?.name || ('Partner' | t))"
      [description]="'Partner statement: money put in and taken out, profit allocated and paid, and part of the farm.' | t"
      [breadcrumbActiveNode]="'Partner statement' | t">
      <div actions class="flex flex-wrap items-center gap-2">
        <a routerLink="/finance/partners" class="px-4 py-2 rounded-xl text-sm font-semibold border border-gray-200 dark:border-gray-700 text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">arrow_back</mat-icon>{{ 'Partners' | t }}
        </a>
        <a [routerLink]="['/reports', 'finance.partner-statement']" [queryParams]="{ partnerId: partnerId, period: from() + '..' + to() }"
          class="px-4 py-2 rounded-xl text-sm font-semibold text-white bg-primary-600 hover:bg-primary-700 flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">picture_as_pdf</mat-icon>{{ 'PDF / Excel' | t }}
        </a>
      </div>
    </app-page-header>

    <div class="px-4 sm:px-6 py-4 mx-auto max-w-7xl space-y-6">
      <app-finance-subnav></app-finance-subnav>

      <div class="flex flex-wrap items-center gap-2 text-xs">
        <span class="font-semibold text-gray-600 dark:text-gray-300">{{ 'Period' | t }}</span>
        <input type="date" [ngModel]="from()" (ngModelChange)="from.set($event); load()" class="px-2 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
        <span class="text-gray-500">{{ 'to' | t }}</span>
        <input type="date" [ngModel]="to()" (ngModelChange)="to.set($event); load()" class="px-2 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
      </div>

      @if (loading()) { <app-loading [overlay]="true"></app-loading> }
      @if (error(); as err) { <app-error-state [message]="err" (retry)="load()"></app-error-state> }

      @if (s(); as st) {
        <div class="grid grid-cols-2 lg:grid-cols-5 gap-3">
          <div class="rounded-2xl p-4 bg-white dark:bg-gray-800/80 border border-gray-100 dark:border-gray-800">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">{{ 'Capital now' | t }}</p>
            <p class="text-xl font-black m-0 mt-1 text-gray-900 dark:text-white">{{ st.partner.capitalBdt | money }}</p>
          </div>
          <div class="rounded-2xl p-4 bg-primary-50/70 dark:bg-primary-950/30 border border-primary-100 dark:border-primary-900/50">
            <p class="text-[11px] font-bold uppercase tracking-wider text-primary-700 dark:text-primary-300 m-0">{{ 'Share of the farm' | t }}</p>
            <p class="text-xl font-black m-0 mt-1 text-primary-700 dark:text-primary-200">{{ st.partner.sharePercent | number:'1.0-2' }}%</p>
          </div>
          <div class="rounded-2xl p-4 bg-white dark:bg-gray-800/80 border border-gray-100 dark:border-gray-800">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">{{ 'Profit allocated (all time)' | t }}</p>
            <p class="text-xl font-black m-0 mt-1" [ngClass]="st.partner.profitAllocatedBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-success-700 dark:text-success-400'">{{ st.partner.profitAllocatedBdt | money }}</p>
          </div>
          <div class="rounded-2xl p-4 bg-white dark:bg-gray-800/80 border border-gray-100 dark:border-gray-800">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">{{ 'Profit due' | t }}</p>
            <p class="text-xl font-black m-0 mt-1 text-gray-900 dark:text-white">{{ st.partner.profitDueBdt | money }}</p>
          </div>
          <div class="rounded-2xl p-4 bg-white dark:bg-gray-800/80 border border-gray-100 dark:border-gray-800">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">{{ 'Part of net assets' | t }}</p>
            <p class="text-xl font-black m-0 mt-1 text-gray-900 dark:text-white">{{ st.partnerNetAssetsBdt | money }}</p>
          </div>
        </div>

        <!-- Capital ledger -->
        <div class="rounded-2xl border border-gray-100 dark:border-gray-800 bg-white dark:bg-gray-800/80 overflow-hidden">
          <div class="px-4 sm:px-5 py-3 border-b border-gray-100 dark:border-gray-800">
            <h3 class="text-base font-bold text-gray-900 dark:text-white m-0">{{ 'Capital ledger' | t }}</h3>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
                <tr>
                  <th class="text-left px-3 py-2">{{ 'Date' | t }}</th>
                  <th class="text-left px-3 py-2">{{ 'Entry' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'In / allocated' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'Out / paid' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'Capital after' | t }}</th>
                  <th class="text-left px-3 py-2">{{ 'Money account' | t }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                <tr class="bg-gray-50/60 dark:bg-gray-900/20">
                  <td class="px-3 py-2 text-gray-600 dark:text-gray-300">{{ st.fromDate | date:'d MMM yy' }}</td>
                  <td class="px-3 py-2 font-semibold text-gray-700 dark:text-gray-200" colspan="3">{{ 'Capital at start' | t }}</td>
                  <td class="px-3 py-2 text-right font-mono font-semibold text-gray-900 dark:text-white">{{ st.openingCapitalBdt | money }}</td>
                  <td></td>
                </tr>
                @for (l of st.lines; track $index) {
                  <tr>
                    <td class="px-3 py-2 whitespace-nowrap text-gray-600 dark:text-gray-300">{{ l.date | date:'d MMM yy' }}</td>
                    <td class="px-3 py-2">
                      <p class="m-0 text-gray-900 dark:text-white">{{ typeLabel(l.type) | t }}</p>
                      @if (l.notes) { <p class="m-0 text-[11px] text-gray-500">{{ noteText(l.notes) }}</p> }
                    </td>
                    <td class="px-3 py-2 text-right font-mono text-success-700 dark:text-success-400">{{ isIn(l.type) ? (l.amountBdt | money) : '' }}</td>
                    <td class="px-3 py-2 text-right font-mono text-rose-600 dark:text-rose-400">{{ isIn(l.type) ? '' : (l.amountBdt | money) }}</td>
                    <td class="px-3 py-2 text-right font-mono text-gray-900 dark:text-white">{{ l.capitalAfterBdt | money }}</td>
                    <td class="px-3 py-2 text-gray-600 dark:text-gray-300">{{ accountName(l.moneyAccountName) }}</td>
                  </tr>
                } @empty {
                  <tr><td colspan="6" class="px-3 py-6 text-center text-gray-500 dark:text-gray-400">{{ 'No money moved for this partner in the period.' | t }}</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <!-- Net assets -->
        <div class="rounded-2xl border border-gray-100 dark:border-gray-800 bg-white dark:bg-gray-800/80 overflow-hidden">
          <div class="px-4 sm:px-5 py-3 border-b border-gray-100 dark:border-gray-800">
            <h3 class="text-base font-bold text-gray-900 dark:text-white m-0">{{ 'Part of the farm\\'s net assets' | t }}</h3>
            <p class="text-xs text-gray-500 dark:text-gray-400 m-0">{{ 'At book value (what was paid), by the partner\\'s current share of {percent}%.' | t: { percent: (st.partner.sharePercent | number:'1.0-2') } }}</p>
          </div>
          <table class="w-full text-sm">
            <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
              <tr>
                <th class="text-left px-3 py-2">{{ 'Item' | t }}</th>
                <th class="text-right px-3 py-2">{{ 'Farm' | t }}</th>
                <th class="text-right px-3 py-2">{{ 'This partner' | t }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
              @for (n of st.netAssets; track n.key) {
                <tr>
                  <td class="px-3 py-2 text-gray-900 dark:text-white">{{ isBn ? n.nameBn : n.name }}</td>
                  <td class="px-3 py-2 text-right font-mono" [ngClass]="n.farmBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-gray-700 dark:text-gray-200'">{{ n.farmBdt | money }}</td>
                  <td class="px-3 py-2 text-right font-mono" [ngClass]="n.partnerBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-gray-900 dark:text-white'">{{ n.partnerBdt | money }}</td>
                </tr>
              }
            </tbody>
            <tfoot class="bg-gray-50 dark:bg-gray-900/40 font-bold">
              <tr>
                <td class="px-3 py-2 text-gray-900 dark:text-white">{{ 'Net assets' | t }}</td>
                <td class="px-3 py-2 text-right font-mono text-gray-900 dark:text-white">{{ st.netAssetsBdt | money }}</td>
                <td class="px-3 py-2 text-right font-mono text-primary-700 dark:text-primary-200">{{ st.partnerNetAssetsBdt | money }}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      }
    </div>
  `,
})
export class PartnerStatementComponent implements OnInit {
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly i18n = inject(I18nService);

  readonly partnerId = this.route.snapshot.paramMap.get('id') ?? '';
  readonly isBn = this.i18n.isBangla;

  /** The server writes the profit-allocation line's note in English; the rest are the user's own words. */
  noteText(note: string): string {
    const m = /^Share of profit (.+) – (.+) by capital-days$/.exec(note);
    return m ? this.i18n.t('Share of profit {from} – {to} by capital-days', { from: m[1], to: m[2] }) : note;
  }
  readonly s = signal<PartnerStatement | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly from = signal('');
  readonly to = signal('');
  readonly accountName = moneyAccountName;

  ngOnInit(): void {
    this.load();
  }

  isIn(type: string): boolean {
    return type === 'CapitalContribution' || type === 'ProfitAllocation';
  }

  typeLabel(type: string): string {
    switch (type) {
      case 'CapitalContribution': return 'Capital put in';
      case 'CapitalWithdrawal': return 'Capital taken out';
      case 'ProfitDistribution': return 'Profit paid out';
      case 'ProfitAllocation': return 'Profit allocated (capital-days)';
      default: return type;
    }
  }

  load(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId || !this.partnerId) { this.loading.set(false); return; }
    this.loading.set(true);
    this.error.set(null);
    this.finance.getPartnerStatement(farmId, this.partnerId, this.from() || undefined, this.to() || undefined).subscribe({
      next: st => {
        this.s.set(st);
        this.from.set(st.fromDate);
        this.to.set(st.toDate);
        this.loading.set(false);
      },
      error: err => { this.loading.set(false); this.error.set(parseApiError(err)); },
    });
  }
}
