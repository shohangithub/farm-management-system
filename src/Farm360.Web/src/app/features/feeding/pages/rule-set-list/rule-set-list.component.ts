import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FeedingService } from '../../services/feeding.service';
import { FeedingRuleSet, FeedingPlanType, TargetAnimalType, FeedingPurpose } from '../../models/feeding.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
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
                  @for (rule of ruleSet.rules; track rule.id) {
                    <div class="px-3 py-2 text-xs flex justify-between items-center hover:bg-gray-50 dark:hover:bg-gray-800/50">
                      <span class="text-gray-600 dark:text-gray-400">{{ formatRuleCondition(ruleSet.planType, rule) }}</span>
                      <span class="font-bold text-emerald-600 dark:text-emerald-400">{{ rule.quantityValue }}{{ ruleSet.planType === 'WeightPercentage' ? '%' : ' kg' }} {{ rule.feedType }}</span>
                    </div>
                  }
                  @if (ruleSet.rules.length === 0) {
                    <div class="px-3 py-3 text-xs text-gray-400 text-center italic">No rules defined</div>
                  }
                </div>
              </div>
            </div>

            <!-- Footer Action -->
            <div class="p-3 bg-gray-50/80 dark:bg-gray-800/80 border-t border-gray-100 dark:border-gray-700/50 flex justify-end relative z-10">
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

  readonly isLoading = signal(true);
  readonly ruleSets = signal<FeedingRuleSet[]>([]);

  ngOnInit(): void {
    this.loadRuleSets();
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
      maxWidth: '800px' 
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadRuleSets();
    });
  }

  openEditDialog(ruleSet: FeedingRuleSet): void {
    const dialogRef = this.dialog.open(FeedingRuleSetDialogComponent, { disableClose: true,
      width: '95vw',
      maxWidth: '800px',
      data: ruleSet
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) this.loadRuleSets();
    });
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

  formatRuleCondition(planType: string, rule: any): string {
    if (planType === 'WeightPercentage') {
      if (rule.minWeightKg && rule.maxWeightKg) return `${rule.minWeightKg} - ${rule.maxWeightKg} kg`;
      if (rule.minWeightKg) return `> ${rule.minWeightKg} kg`;
      if (rule.maxWeightKg) return `< ${rule.maxWeightKg} kg`;
      return 'All Weights';
    } else if (planType === 'AgeBased') {
      if (rule.minAgeDays && rule.maxAgeDays) return `${rule.minAgeDays} - ${rule.maxAgeDays} days`;
      if (rule.minAgeDays) return `> ${rule.minAgeDays} days`;
      if (rule.maxAgeDays) return `< ${rule.maxAgeDays} days`;
      return 'All Ages';
    }
    return 'Fixed Amount';
  }
}
