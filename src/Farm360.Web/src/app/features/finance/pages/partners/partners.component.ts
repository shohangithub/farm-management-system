import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { FinanceSubnavComponent } from '../../components/finance-subnav/finance-subnav.component';
import { InvestorFormDialogComponent } from '../../components/investor-form-dialog/investor-form-dialog';
import { InvestorTransactionDialogComponent, PartnerMoneyType } from '../../components/investor-transaction-dialog/investor-transaction-dialog';
import { FinanceService } from '../../services/finance.service';
import { Investor, PartnerProfitAllocation, PartnerSummary, PartnersOverview, WhoseMoney } from '../../models/finance.model';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { NotificationService } from '../../../../shared/services/notification.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { CanPipe } from '../../../../shared/pipes/can.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

/**
 * Partners / অংশীদার — everyone who has money in the farm (the owner is a partner too). All
 * partners fund the whole farm from one pool; each partner's share is their capital ÷ total
 * capital, and profit is split by capital-days.
 */
@Component({
  selector: 'app-partners',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, RouterModule, MatIconModule, MatDialogModule, MatMenuModule, MatTooltipModule,
    PageHeaderComponent, LoadingComponent, ErrorStateComponent, FinanceSubnavComponent, MoneyPipe, CanPipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header [title]="'Partners' | t"
      [description]="'Everyone who has money in the farm — the owner too. Money first comes into a farm account; each partner\\'s share is their capital.' | t"
      [breadcrumbActiveNode]="'Partners' | t">
      <div actions class="flex flex-wrap items-center gap-2">
        <a routerLink="/finance/shares" class="px-4 py-2 rounded-xl text-sm font-semibold border border-gray-200 dark:border-gray-700 text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 hover:bg-gray-50 dark:hover:bg-gray-700 flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">pie_chart</mat-icon>{{ 'Farm Shares' | t }}
        </a>
        @if ('finance.admin' | can) {
          <button type="button" (click)="money(null, 'CapitalContribution')" class="px-4 py-2 rounded-xl text-sm font-semibold text-white bg-success-600 hover:bg-success-700 flex items-center gap-1.5">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add_card</mat-icon>{{ 'Add partner money' | t }}
          </button>
          <button type="button" (click)="addPartner()" class="px-4 py-2 rounded-xl text-sm font-semibold text-white bg-primary-600 hover:bg-primary-700 flex items-center gap-1.5">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">person_add</mat-icon>{{ 'New partner' | t }}
          </button>
        }
      </div>
    </app-page-header>

    <div class="px-4 sm:px-6 py-4 mx-auto max-w-7xl space-y-6">
      <app-finance-subnav></app-finance-subnav>

      @if (loading()) { <app-loading [overlay]="true"></app-loading> }
      @if (loadError(); as err) { <app-error-state [message]="err" (retry)="load()"></app-error-state> }

      @if (overview(); as o) {
        <!-- Headline -->
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-3">
          <div class="rounded-2xl p-4 bg-white dark:bg-gray-800/80 border border-gray-100 dark:border-gray-800">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">{{ 'Partners\\' capital' | t }}</p>
            <p class="text-xl font-black m-0 mt-1 text-gray-900 dark:text-white">{{ o.totalCapitalBdt | money }}</p>
          </div>
          <div class="rounded-2xl p-4 bg-white dark:bg-gray-800/80 border border-gray-100 dark:border-gray-800">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0" [matTooltip]="'Animals + stock + cash & bank + money owed to the farm + fixed assets − loans and other debts, at book value.' | t">{{ 'Farm net assets' | t }} ⓘ</p>
            <p class="text-xl font-black m-0 mt-1" [ngClass]="o.netAssetsBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-gray-900 dark:text-white'">{{ o.netAssetsBdt | money }}</p>
          </div>
          <div class="rounded-2xl p-4 bg-white dark:bg-gray-800/80 border border-gray-100 dark:border-gray-800">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">{{ 'Profit allocated' | t }}</p>
            <p class="text-xl font-black m-0 mt-1" [ngClass]="o.profitAllocatedBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-success-700 dark:text-success-400'">{{ o.profitAllocatedBdt | money }}</p>
          </div>
          <div class="rounded-2xl p-4 bg-white dark:bg-gray-800/80 border border-gray-100 dark:border-gray-800">
            <p class="text-[11px] font-bold uppercase tracking-wider text-gray-500 m-0">{{ 'Profit paid out' | t }}</p>
            <p class="text-xl font-black m-0 mt-1 text-gray-900 dark:text-white">{{ o.profitPaidBdt | money }}</p>
          </div>
        </div>

        <!-- Partners table -->
        <div class="rounded-2xl border border-gray-100 dark:border-gray-800 bg-white dark:bg-gray-800/80 overflow-hidden">
          <div class="px-4 sm:px-5 py-3 border-b border-gray-100 dark:border-gray-800 flex items-center justify-between gap-2">
            <h3 class="text-base font-bold text-gray-900 dark:text-white m-0">{{ 'Capital and share' | t }}</h3>
            <span class="text-[11px] text-gray-500 dark:text-gray-400">{{ 'Share = partner\\'s capital ÷ all partners\\' capital (live).' | t }}</span>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
                <tr>
                  <th class="text-left px-3 py-2">{{ 'Partner' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'Put in' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'Taken out' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'Capital' | t }}</th>
                  <th class="text-left px-3 py-2 min-w-[140px]">{{ 'Share' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'Profit allocated' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'Profit paid' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'Profit due' | t }}</th>
                  <th class="px-2 py-2"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                @for (p of o.partners; track p.id) {
                  <tr class="hover:bg-gray-50 dark:hover:bg-gray-800/60" [class.opacity-60]="!p.isActive">
                    <td class="px-3 py-2">
                      <a [routerLink]="['/finance/partners', p.id]" class="font-semibold text-primary-700 dark:text-primary-300 hover:underline">{{ p.name }}</a>
                      @if (!p.isActive) { <span class="ml-1 text-[10px] px-1.5 py-0.5 rounded bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300">{{ 'Inactive' | t }}</span> }
                      @if (p.phone) { <p class="m-0 text-[11px] text-gray-500">{{ p.phone }}</p> }
                    </td>
                    <td class="px-3 py-2 text-right font-mono">{{ p.depositedBdt | money }}</td>
                    <td class="px-3 py-2 text-right font-mono">{{ p.withdrawnBdt | money }}</td>
                    <td class="px-3 py-2 text-right font-mono font-bold text-gray-900 dark:text-white">{{ p.capitalBdt | money }}</td>
                    <td class="px-3 py-2">
                      <div class="flex items-center gap-2">
                        <div class="flex-1 h-2 rounded-full bg-gray-100 dark:bg-gray-700 overflow-hidden">
                          <div class="h-full bg-primary-500" [style.width.%]="p.sharePercent"></div>
                        </div>
                        <span class="text-xs font-bold text-gray-900 dark:text-white w-12 text-right">{{ p.sharePercent | number:'1.0-2' }}%</span>
                      </div>
                    </td>
                    <td class="px-3 py-2 text-right font-mono" [ngClass]="p.profitAllocatedBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-success-700 dark:text-success-400'">{{ p.profitAllocatedBdt | money }}</td>
                    <td class="px-3 py-2 text-right font-mono">{{ p.profitPaidBdt | money }}</td>
                    <td class="px-3 py-2 text-right font-mono font-semibold" [ngClass]="p.profitDueBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-gray-900 dark:text-white'">{{ p.profitDueBdt | money }}</td>
                    <td class="px-2 py-2 text-right whitespace-nowrap">
                      <a [routerLink]="['/finance/partners', p.id]" [matTooltip]="'Statement' | t" class="p-1 rounded text-gray-500 hover:text-primary-600 inline-flex"><mat-icon class="!text-[20px]">description</mat-icon></a>
                      @if ('finance.admin' | can) {
                        <button type="button" [matMenuTriggerFor]="menu" [attr.aria-label]="'More' | t" class="p-1 rounded text-gray-500 hover:text-gray-800 dark:hover:text-gray-200"><mat-icon class="!text-[20px]">more_vert</mat-icon></button>
                        <mat-menu #menu="matMenu">
                          <button mat-menu-item (click)="money(p, 'CapitalContribution')"><mat-icon>add_circle</mat-icon><span>{{ 'Put money in' | t }}</span></button>
                          <button mat-menu-item (click)="money(p, 'CapitalWithdrawal')"><mat-icon>remove_circle</mat-icon><span>{{ 'Take capital out' | t }}</span></button>
                          <button mat-menu-item (click)="money(p, 'ProfitDistribution')"><mat-icon>payments</mat-icon><span>{{ 'Pay profit' | t }}</span></button>
                          <button mat-menu-item (click)="edit(p)"><mat-icon>edit</mat-icon><span>{{ 'Edit' | t }}</span></button>
                          <button mat-menu-item (click)="toggle(p)"><mat-icon>{{ p.isActive ? 'person_off' : 'person' }}</mat-icon><span>{{ (p.isActive ? 'Deactivate' : 'Activate') | t }}</span></button>
                        </mat-menu>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="9" class="px-3 py-10 text-center text-gray-500 dark:text-gray-400">
                    {{ 'No partners yet. Add each partner — the owner too — then record the money each one puts in.' | t }}
                  </td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <!-- Profit allocation -->
        <div class="rounded-2xl border border-gray-100 dark:border-gray-800 bg-white dark:bg-gray-800/80 overflow-hidden">
          <div class="px-4 sm:px-5 py-3 border-b border-gray-100 dark:border-gray-800 flex flex-wrap items-center justify-between gap-2">
            <h3 class="text-base font-bold text-gray-900 dark:text-white m-0 flex items-center gap-1">
              {{ 'Profit split for a period' | t }}
              <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-gray-400 cursor-help" [matTooltip]="capitalDaysHelp | t">help</mat-icon>
            </h3>
            <div class="flex flex-wrap items-center gap-2 text-xs">
              <input type="date" [ngModel]="allocFrom()" (ngModelChange)="allocFrom.set($event); loadAllocation()" class="px-2 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
              <span class="text-gray-500">{{ 'to' | t }}</span>
              <input type="date" [ngModel]="allocTo()" (ngModelChange)="allocTo.set($event); loadAllocation()" class="px-2 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
            </div>
          </div>
          <p class="px-4 sm:px-5 pt-3 m-0 text-xs text-gray-600 dark:text-gray-300 leading-relaxed">{{ capitalDaysHelp | t }}</p>
          @if (allocation(); as a) {
            <div class="px-4 sm:px-5 py-3 grid grid-cols-2 sm:grid-cols-4 gap-3 text-sm">
              <div><p class="text-[11px] uppercase font-bold text-gray-500 m-0">{{ 'Net profit' | t }}</p><p class="m-0 font-bold" [ngClass]="a.netProfitBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-gray-900 dark:text-white'">{{ a.netProfitBdt | money }}</p></div>
              <div><p class="text-[11px] uppercase font-bold text-gray-500 m-0">{{ 'Charity ({percent}%)' | t: { percent: a.charityPercent } }}</p><p class="m-0 font-bold text-gray-900 dark:text-white">{{ a.charityBdt | money }}</p></div>
              <div><p class="text-[11px] uppercase font-bold text-gray-500 m-0">{{ 'To share' | t }}</p><p class="m-0 font-bold" [ngClass]="a.distributableProfitBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-success-700 dark:text-success-400'">{{ a.distributableProfitBdt | money }}</p></div>
              <div><p class="text-[11px] uppercase font-bold text-gray-500 m-0">{{ 'Days' | t }}</p><p class="m-0 font-bold text-gray-900 dark:text-white">{{ a.days }}</p></div>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500">
                  <tr>
                    <th class="text-left px-3 py-2">{{ 'Partner' | t }}</th>
                    <th class="text-right px-3 py-2" [matTooltip]="'Capital held on an average day of the period.' | t">{{ 'Average capital' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'Capital-days' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'Share of profit' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'Profit allocated' | t }}</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                  @for (l of a.lines; track l.partnerId) {
                    <tr>
                      <td class="px-3 py-2 font-semibold text-gray-900 dark:text-white">{{ l.name }}</td>
                      <td class="px-3 py-2 text-right font-mono">{{ l.averageCapitalBdt | money }}</td>
                      <td class="px-3 py-2 text-right font-mono text-gray-600 dark:text-gray-300">{{ l.capitalDays | money:0:false }}</td>
                      <td class="px-3 py-2 text-right font-bold text-gray-900 dark:text-white">{{ l.sharePercent | number:'1.0-2' }}%</td>
                      <td class="px-3 py-2 text-right font-mono font-bold" [ngClass]="l.allocatedBdt < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-success-700 dark:text-success-400'">{{ l.allocatedBdt | money }}</td>
                    </tr>
                  } @empty {
                    <tr><td colspan="5" class="px-3 py-6 text-center text-gray-500 dark:text-gray-400">{{ 'No partner had money in the farm during this period.' | t }}</td></tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>

        <!-- Whose money where -->
        <div class="rounded-2xl border border-gray-100 dark:border-gray-800 bg-white dark:bg-gray-800/80 overflow-hidden">
          <div class="px-4 sm:px-5 py-3 border-b border-gray-100 dark:border-gray-800 flex flex-wrap items-center justify-between gap-2">
            <div>
              <h3 class="text-base font-bold text-gray-900 dark:text-white m-0">{{ 'Whose money where' | t }}</h3>
              <p class="text-xs text-gray-500 dark:text-gray-400 m-0">{{ 'Each spend split between the partners by their capital on the day it was spent (pooled fund).' | t }}</p>
            </div>
            <div class="flex flex-wrap items-center gap-2 text-xs">
              <input type="date" [ngModel]="whoFrom()" (ngModelChange)="whoFrom.set($event); loadWhose()" class="px-2 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
              <span class="text-gray-500">{{ 'to' | t }}</span>
              <input type="date" [ngModel]="whoTo()" (ngModelChange)="whoTo.set($event); loadWhose()" class="px-2 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
            </div>
          </div>
          @if (whose(); as w) {
            <div class="overflow-x-auto max-h-[480px]">
              <table class="w-full text-sm">
                <thead class="bg-gray-50 dark:bg-gray-900/40 text-[11px] uppercase tracking-wider text-gray-500 sticky top-0">
                  <tr>
                    <th class="text-left px-3 py-2">{{ 'Date' | t }}</th>
                    <th class="text-left px-3 py-2">{{ 'Spend' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'Amount' | t }}</th>
                    @for (p of w.partners; track p.id) { <th class="text-right px-3 py-2">{{ p.name }}</th> }
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                  @for (r of w.rows; track r.transactionId) {
                    <tr>
                      <td class="px-3 py-2 whitespace-nowrap text-gray-600 dark:text-gray-300">{{ r.date | date:'d MMM yy' }}</td>
                      <td class="px-3 py-2">
                        <p class="m-0 text-gray-900 dark:text-white">{{ r.description }}</p>
                        <p class="m-0 text-[11px] text-gray-500">{{ isBn ? r.categoryNameBn : r.categoryName }}</p>
                      </td>
                      <td class="px-3 py-2 text-right font-mono font-semibold text-gray-900 dark:text-white">{{ r.amountBdt | money }}</td>
                      @for (p of w.partners; track p.id) {
                        <td class="px-3 py-2 text-right font-mono text-gray-700 dark:text-gray-200">{{ partPart(r.parts, p.id) }}</td>
                      }
                    </tr>
                  } @empty {
                    <tr><td [attr.colspan]="3 + w.partners.length" class="px-3 py-6 text-center text-gray-500 dark:text-gray-400">{{ 'No spending in this period.' | t }}</td></tr>
                  }
                </tbody>
                @if (w.rows.length > 0) {
                  <tfoot class="bg-gray-50 dark:bg-gray-900/40 font-bold">
                    <tr>
                      <td class="px-3 py-2" colspan="2">{{ 'Total' | t }}</td>
                      <td class="px-3 py-2 text-right font-mono text-gray-900 dark:text-white">{{ w.totalBdt | money }}</td>
                      @for (p of w.partners; track p.id) {
                        <td class="px-3 py-2 text-right font-mono text-gray-900 dark:text-white">{{ partPart(w.totals, p.id) }}</td>
                      }
                    </tr>
                  </tfoot>
                }
              </table>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class PartnersComponent implements OnInit {
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(I18nService);
  private readonly money$ = new MoneyPipe();

  readonly isBn = this.i18n.isBangla;
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly overview = signal<PartnersOverview | null>(null);
  readonly allocation = signal<PartnerProfitAllocation | null>(null);
  readonly whose = signal<WhoseMoney | null>(null);
  private readonly investors = signal<Investor[]>([]);

  readonly allocFrom = signal(toLocalIsoDate(new Date(new Date().getFullYear(), 0, 1)));
  readonly allocTo = signal(toLocalIsoDate());
  readonly whoFrom = signal(toLocalIsoDate(new Date(new Date().getFullYear(), new Date().getMonth() - 2, 1)));
  readonly whoTo = signal(toLocalIsoDate());

  readonly capitalDaysHelp =
    'How profit is split: by capital-days — each partner\'s capital multiplied by the number of days it was in the farm during the period. ' +
    'Example: A puts in ৳1,00,000 for the whole year, B puts in ৳1,00,000 half-way through. A held it twice as long, so A gets about 66.7% and B about 33.3% of that year\'s profit.';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) { this.loading.set(false); return; }
    this.loading.set(true);
    this.loadError.set(null);
    this.finance.getPartnersOverview(farmId, true).subscribe({
      next: o => { this.overview.set(o); this.loading.set(false); },
      error: err => { this.loading.set(false); this.loadError.set(parseApiError(err)); },
    });
    this.finance.getInvestors(farmId, true).subscribe({ next: list => this.investors.set(list), error: () => this.investors.set([]) });
    this.loadAllocation();
    this.loadWhose();
  }

  loadAllocation(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) return;
    this.finance.getPartnerAllocation(farmId, this.allocFrom(), this.allocTo()).subscribe({
      next: a => this.allocation.set(a),
      error: err => this.notify.error(parseApiError(err)),
    });
  }

  loadWhose(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) return;
    this.finance.getWhoseMoney(farmId, { fromDate: this.whoFrom(), toDate: this.whoTo() }).subscribe({
      next: w => this.whose.set(w),
      error: err => this.notify.error(parseApiError(err)),
    });
  }

  partPart(parts: { partnerId: string; amountBdt: number }[], partnerId: string): string {
    const part = parts.find(p => p.partnerId === partnerId);
    return part ? this.money$.transform(part.amountBdt) : '—';
  }

  private investor(p: PartnerSummary | null): Investor | null {
    return p ? this.investors().find(i => i.id === p.id) ?? null : null;
  }

  money(p: PartnerSummary | null, type: PartnerMoneyType): void {
    this.dialog.open(InvestorTransactionDialogComponent, {
      width: '560px', maxWidth: '95vw',
      data: { investor: this.investor(p), defaultType: type },
    }).afterClosed().subscribe(done => { if (done) this.load(); });
  }

  addPartner(): void {
    this.dialog.open(InvestorFormDialogComponent, { width: '640px', maxWidth: '95vw', data: null })
      .afterClosed().subscribe(done => { if (done) this.load(); });
  }

  edit(p: PartnerSummary): void {
    const investor = this.investor(p);
    if (!investor) return;
    this.dialog.open(InvestorFormDialogComponent, { width: '640px', maxWidth: '95vw', data: investor })
      .afterClosed().subscribe(done => { if (done) this.load(); });
  }

  toggle(p: PartnerSummary): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) return;
    this.finance.toggleInvestorStatus(farmId, p.id, !p.isActive).subscribe({
      next: () => this.load(),
      error: err => this.notify.error(parseApiError(err)),
    });
  }
}
