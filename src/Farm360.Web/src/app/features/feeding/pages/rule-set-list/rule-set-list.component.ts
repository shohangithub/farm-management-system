import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FeedingService } from '../../services/feeding.service';
import { FeedingRuleSet, FeedingRuleLine, FeedingPlanType, TargetAnimalType, FeedingPurpose } from '../../models/feeding.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { FeedingRuleSetDialogComponent } from '../../components/dialogs/feeding-rule-set-dialog/feeding-rule-set-dialog.component';

@Component({
  selector: 'app-rule-set-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Feeding Rule Sets"
      description="Configure automated smart feeding rules based on animal weight, age, and purpose."
      breadcrumbActiveNode="Feeding Rules">
      <div actions>
        <button (click)="openCreateDialog()"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add</mat-icon> New Rule Set
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!isLoading() && ruleSets().length === 0"
        icon="rule"
        title="No Feeding Rules Configured"
        description="Create your first feeding rule set to automate feed calculations."
        actionLabel="Create Rule Set"
        (action)="openCreateDialog()">
      </app-empty-state>

      <!-- Rule Sets Grid -->
      <div *ngIf="!isLoading() && ruleSets().length > 0" class="p-6 grid grid-cols-1 lg:grid-cols-2 xl:grid-cols-3 gap-6">
        @for (ruleSet of ruleSets(); track ruleSet.id) {
          <div class="group relative bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 shadow-sm hover:shadow-xl transition-all duration-300 overflow-hidden transform hover:-translate-y-1 flex flex-col">
            <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-emerald-500/5 rotate-[-10deg] pointer-events-none transition-transform duration-500 group-hover:scale-110">settings_input_component</mat-icon>

            <!-- Card Header -->
            <div class="p-5 flex items-start justify-between border-b border-gray-50 dark:border-gray-700/50 relative z-10">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20 group-hover:scale-110 transition-transform duration-300">
                  <mat-icon class="!w-5 !h-5 !text-[20px]">rule</mat-icon>
                </div>
                <div>
                  <h3 class="font-bold text-gray-900 dark:text-white text-base leading-tight group-hover:text-emerald-600 transition-colors">{{ ruleSet.name }}</h3>
                  <span class="inline-flex items-center mt-1 text-xs font-medium text-gray-500 dark:text-gray-400">
                    {{ formatPlanType(ruleSet.planType) }} • {{ formatAnimalType(ruleSet.targetAnimalType) }}
                  </span>
                </div>
              </div>

              <span class="inline-flex items-center px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider shadow-sm"
                [ngClass]="ruleSet.isActive ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' : 'bg-gray-50 text-gray-700 border border-gray-200'">
                {{ ruleSet.isActive ? 'Active' : 'Inactive' }}
              </span>
            </div>

            <!-- Card Body -->
            <div class="p-5 flex-1 relative z-10">
              <div class="flex items-center gap-2 mb-4">
                <mat-icon class="text-gray-400 !text-[16px] !w-[16px] !h-[16px]">track_changes</mat-icon>
                <span class="text-sm font-medium text-gray-700 dark:text-gray-300">Purpose: {{ formatPurpose(ruleSet.feedingPurpose) }}</span>
              </div>
              
              <div class="text-xs text-gray-500 dark:text-gray-400 mb-3" *ngIf="ruleSet.baseNotes">{{ ruleSet.baseNotes }}</div>

              <div class="bg-gray-50 dark:bg-gray-900/50 rounded-xl border border-gray-100 dark:border-gray-800 overflow-hidden">
                <div class="px-3 py-2 bg-gray-100/50 dark:bg-gray-800/50 text-xs font-bold text-gray-500 uppercase tracking-wider border-b border-gray-100 dark:border-gray-800">
                  Configured Rules ({{ ruleSet.rules.length }})
                </div>
                <div class="divide-y divide-gray-100 dark:divide-gray-800 max-h-32 overflow-y-auto">
                  @for (rule of sortedRules(ruleSet); track rule.id) {
                    <div class="px-3 py-2 text-xs flex justify-between items-center gap-2 hover:bg-gray-50 dark:hover:bg-gray-800/50">
                      <span class="text-gray-600 dark:text-gray-400">{{ formatRuleCondition(ruleSet.planType, rule) }}</span>
                      <span class="font-bold text-emerald-600 dark:text-emerald-400 text-right">{{ rule.quantityValue }}{{ ruleSet.planType === 'WeightPercentage' ? '%' : ' kg' }} {{ feedName(rule) }}</span>
                    </div>
                  }
                  @if (ruleSet.rules.length === 0) {
                    <div class="px-3 py-3 text-xs text-gray-400 text-center italic">No rules defined</div>
                  }
                </div>
              </div>
            </div>

            <!-- Footer Action -->
            <div class="p-3 bg-gray-50/80 dark:bg-gray-800/80 border-t border-gray-100 dark:border-gray-700/50 flex justify-end gap-2 relative z-10">
              <button (click)="printRuleSet(ruleSet)" title="Print this rule set" class="px-3 py-1.5 text-xs font-semibold text-gray-700 dark:text-gray-300 hover:bg-white dark:hover:bg-gray-700 rounded-lg border border-gray-200 dark:border-gray-700 transition-colors shadow-sm inline-flex items-center gap-1">
                <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">print</mat-icon> Print
              </button>
              <button (click)="openEditDialog(ruleSet)" class="px-3 py-1.5 text-xs font-semibold text-gray-700 dark:text-gray-300 hover:bg-white dark:hover:bg-gray-700 rounded-lg border border-gray-200 dark:border-gray-700 transition-colors shadow-sm inline-flex items-center gap-1">
                <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">edit</mat-icon> Edit Configuration
              </button>
            </div>
          </div>
        }
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RuleSetListComponent implements OnInit {
  private readonly feedingService = inject(FeedingService);
  private readonly dialog = inject(MatDialog);
  private readonly context = inject(WorkingContextService);

  readonly isLoading = signal(true);
  readonly ruleSets = signal<FeedingRuleSet[]>([]);
  readonly formulaNames = signal(new Map<string, string>());

  ngOnInit(): void {
    this.loadRuleSets();
    this.feedingService.getFormulas(1, 500).subscribe({
      next: res => this.formulaNames.set(new Map(res.items.map(f => [f.id, f.title] as [string, string])))
    });
  }

  loadRuleSets(): void {
    this.isLoading.set(true);
    this.feedingService.getRuleSets().subscribe({
      next: (res) => {
        this.ruleSets.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  openCreateDialog(): void {
    const dialogRef = this.dialog.open(FeedingRuleSetDialogComponent, { disableClose: true, 
      width: '95vw', 
      maxWidth: '960px' 
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadRuleSets();
    });
  }

  openEditDialog(ruleSet: FeedingRuleSet): void {
    const dialogRef = this.dialog.open(FeedingRuleSetDialogComponent, { disableClose: true,
      width: '95vw',
      maxWidth: '960px',
      data: ruleSet
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadRuleSets();
    });
  }

  /**
   * Prints one rule set on its own A4 page — every rule, not just the ones visible in the
   * card's scroll area — through a hidden iframe, so the app page itself is untouched.
   */
  printRuleSet(ruleSet: FeedingRuleSet): void {
    const esc = (v: unknown) => String(v ?? '').replace(/[&<>"']/g, c =>
      ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' } as Record<string, string>)[c]);
    const unit = ruleSet.planType === 'WeightPercentage' ? '% of body weight' : 'kg / head / day';
    const conditionHeader = this.isWeightBased(ruleSet.planType) ? 'Body weight range'
      : ruleSet.planType === 'AgeBased' ? 'Age range' : 'Applies to';
    const farm = this.context.currentFarmValue?.name ?? '';
    const org = this.context.currentOrgValue?.name ?? 'Farm360';
    const printed = new Date().toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' });

    const rules = this.sortedRules(ruleSet);
    const rows = rules.length
      ? rules.map((r, i) => `
          <tr>
            <td class="num">${i + 1}</td>
            <td>${esc(this.formatRuleCondition(ruleSet.planType, r))}</td>
            <td>${esc(this.feedName(r))}</td>
            <td class="num"><strong>${esc(r.quantityValue)}</strong> ${ruleSet.planType === 'WeightPercentage' ? '%' : 'kg'}</td>
          </tr>`).join('')
      : `<tr><td colspan="4" class="empty">No rules defined</td></tr>`;

    const html = `<!doctype html><html><head><meta charset="utf-8"><title>${esc(ruleSet.name)} — Feeding Rule Set</title>
      <style>
        @page { size: A4; margin: 16mm; }
        * { box-sizing: border-box; }
        body { font-family: 'Segoe UI', Arial, sans-serif; color: #111827; font-size: 12px; margin: 0; }
        header { border-bottom: 2px solid #059669; padding-bottom: 8px; margin-bottom: 14px; display: flex; justify-content: space-between; align-items: flex-end; }
        header .org { font-size: 16px; font-weight: 700; }
        header .farm, header .printed { color: #6b7280; font-size: 11px; }
        h1 { font-size: 18px; margin: 0 0 4px; }
        .status { display: inline-block; padding: 2px 8px; border-radius: 999px; font-size: 10px; font-weight: 700; text-transform: uppercase;
                  border: 1px solid ${ruleSet.isActive ? '#a7f3d0' : '#e5e7eb'}; color: ${ruleSet.isActive ? '#047857' : '#374151'}; }
        dl { display: grid; grid-template-columns: 140px 1fr; gap: 4px 12px; margin: 12px 0 16px; }
        dt { color: #6b7280; } dd { margin: 0; font-weight: 600; }
        table { width: 100%; border-collapse: collapse; }
        th, td { border: 1px solid #d1d5db; padding: 6px 8px; text-align: left; }
        th { background: #f3f4f6; font-size: 10px; text-transform: uppercase; letter-spacing: .05em; }
        td.num, th.num { text-align: right; width: 1%; white-space: nowrap; }
        td.empty { text-align: center; color: #9ca3af; font-style: italic; }
        .notes { margin-top: 14px; padding: 8px 10px; background: #f9fafb; border-left: 3px solid #059669; }
        .signatures { display: flex; justify-content: space-between; margin-top: 60px; }
        .signatures div { width: 30%; border-top: 1px solid #6b7280; padding-top: 4px; text-align: center; color: #6b7280; font-size: 11px; }
      </style></head><body>
      <header>
        <div><div class="org">${esc(org)}</div><div class="farm">${esc(farm)}</div></div>
        <div class="printed">Printed ${esc(printed)}</div>
      </header>
      <h1>${esc(ruleSet.name)} <span class="status">${ruleSet.isActive ? 'Active' : 'Inactive'}</span></h1>
      <dl>
        <dt>Plan type</dt><dd>${esc(this.formatPlanType(ruleSet.planType))}</dd>
        <dt>Target animals</dt><dd>${esc(this.formatAnimalType(ruleSet.targetAnimalType))}</dd>
        <dt>Purpose</dt><dd>${esc(this.formatPurpose(ruleSet.feedingPurpose))}</dd>
        <dt>Number of rules</dt><dd>${ruleSet.rules.length}</dd>
      </dl>
      <table>
        <thead><tr><th class="num">#</th><th>${conditionHeader}</th><th>Formula / ration</th><th class="num">Quantity (${unit})</th></tr></thead>
        <tbody>${rows}</tbody>
      </table>
      ${ruleSet.baseNotes ? `<div class="notes"><strong>Notes:</strong> ${esc(ruleSet.baseNotes)}</div>` : ''}
      <div class="signatures"><div>Prepared by</div><div>Checked by</div><div>Approved by</div></div>
      </body></html>`;

    const frame = document.createElement('iframe');
    frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;';
    document.body.appendChild(frame);
    const doc = frame.contentWindow?.document;
    if (!doc) { frame.remove(); return; }
    doc.open();
    doc.write(html);
    doc.close();
    setTimeout(() => {
      frame.contentWindow?.focus();
      frame.contentWindow?.print();
      setTimeout(() => frame.remove(), 1000);
    }, 250);
  }

  formatPlanType(type: string): string {
    return type.replace(/([A-Z])/g, ' $1').trim();
  }

  formatAnimalType(type: string): string {
    return type;
  }

  formatPurpose(purpose: string): string {
    return purpose;
  }

  /** Weight-percentage and weight-quantity plans both use body-weight bands. */
  isWeightBased(planType: string): boolean {
    return planType === 'WeightPercentage' || planType === 'WeightQuantity';
  }

  /** Rules in band order (lightest / youngest first), as they apply to a growing animal. */
  sortedRules(ruleSet: FeedingRuleSet): FeedingRuleLine[] {
    const key = (r: FeedingRuleLine) => this.isWeightBased(ruleSet.planType)
      ? (r.minWeightKg ?? r.maxWeightKg ?? 0)
      : ruleSet.planType === 'AgeBased' ? (r.minAgeDays ?? r.maxAgeDays ?? 0) : 0;
    return [...ruleSet.rules].sort((a, b) => key(a) - key(b));
  }

  /** The ration's name when the rule uses a formula, otherwise the feed category. */
  feedName(rule: FeedingRuleLine): string {
    return (rule.formulaId && this.formulaNames().get(rule.formulaId)) || rule.feedType;
  }

  formatRuleCondition(planType: string, rule: FeedingRuleLine): string {
    const has = (v: number | null | undefined): v is number => v !== null && v !== undefined;
    if (this.isWeightBased(planType)) {
      if (has(rule.minWeightKg) && has(rule.maxWeightKg)) return `${rule.minWeightKg} – ${rule.maxWeightKg} kg`;
      if (has(rule.minWeightKg)) return `${rule.minWeightKg} kg and above`;
      if (has(rule.maxWeightKg)) return `Up to ${rule.maxWeightKg} kg`;
      return 'All weights';
    } else if (planType === 'AgeBased') {
      if (has(rule.minAgeDays) && has(rule.maxAgeDays)) return `${rule.minAgeDays} – ${rule.maxAgeDays} days`;
      if (has(rule.minAgeDays)) return `${rule.minAgeDays} days and above`;
      if (has(rule.maxAgeDays)) return `Up to ${rule.maxAgeDays} days`;
      return 'All ages';
    }
    return 'Every animal (fixed amount)';
  }
}
