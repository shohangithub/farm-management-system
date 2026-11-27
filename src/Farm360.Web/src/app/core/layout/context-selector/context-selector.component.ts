import { Component, inject, OnInit, OnDestroy, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { WorkingContextService } from '../../services/working-context.service';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { Subject, takeUntil, combineLatest, take } from 'rxjs';

@Component({
  selector: 'app-context-selector',
  standalone: true,
  imports: [CommonModule, MatSelectModule, MatFormFieldModule, MatIconModule, MatTooltipModule, ReactiveFormsModule, TranslatePipe],
  template: `
    <div [class]="stacked ? 'flex flex-col gap-3 w-full' : 'flex items-center gap-2 mr-2'">
      <!-- Organization Selector (multi-org dropdown) -->
      <mat-form-field appearance="outline" subscriptSizing="dynamic" [class]="stacked ? 'w-full text-sm' : 'w-28 lg:w-36 text-sm'" *ngIf="(organizations$ | async)?.length! > 1">
        <mat-select [formControl]="orgControl" [placeholder]="'Organization' | t">
          <mat-option *ngFor="let org of organizations$ | async" [value]="org.id">
            {{ org.name }}
          </mat-option>
        </mat-select>
      </mat-form-field>

      <!-- Organization Display (single-org) -->
      <!-- Inline (header) mode hides it: with nothing to choose it only adds noise; the farm chip's tooltip names it. -->
      <div *ngIf="stacked && (organizations$ | async)?.length === 1" class="flex flex-col">
        <span class="text-[11px] text-gray-500 uppercase tracking-widest font-bold leading-tight">{{ 'Org' | t }}</span>
        <span class="text-[13px] font-semibold text-gray-900 dark:text-gray-200 truncate leading-tight max-w-[120px]">
          {{ (currentOrg$ | async)?.name }}
        </span>
      </div>

      <!-- Branch Selector (multi-branch dropdown) -->
      <mat-form-field appearance="outline" subscriptSizing="dynamic" [class]="stacked ? 'w-full text-sm' : 'w-28 lg:w-36 text-sm'" *ngIf="(branches$ | async)?.length! > 1">
        <mat-select [formControl]="branchControl" [placeholder]="'Branch' | t">
          <mat-option *ngFor="let branch of branches$ | async" [value]="branch.id">
            {{ branch.name }}
          </mat-option>
        </mat-select>
      </mat-form-field>

      <!-- Branch Display (single-branch) -->
      <div *ngIf="stacked && (branches$ | async)?.length === 1" class="flex flex-col">
        <span class="text-[11px] text-gray-500 uppercase tracking-widest font-bold leading-tight">{{ 'Branch' | t }}</span>
        <span class="text-[13px] font-semibold text-gray-900 dark:text-gray-200 truncate leading-tight w-full">
          {{ (currentBranch$ | async)?.name }}
        </span>
      </div>

      <!-- Farm Selector (multi-farm dropdown) -->
      <mat-form-field appearance="outline" subscriptSizing="dynamic" [class]="stacked ? 'w-full text-sm' : 'w-28 lg:w-36 text-sm'" *ngIf="(farms$ | async)?.length! > 1">
        <mat-select [formControl]="farmControl" [placeholder]="'Farm' | t">
          <mat-option [value]="null">{{ 'All Farms' | t }}</mat-option>
          <mat-option *ngFor="let farm of farms$ | async" [value]="farm.id">
            {{ farm.name }}
          </mat-option>
        </mat-select>
      </mat-form-field>

      <!-- Farm Display (single farm): stacked shows a labelled row; the header shows one quiet chip with the farm name. -->
      <div *ngIf="stacked && (farms$ | async)?.length === 1" class="flex flex-col">
        <span class="text-[11px] text-gray-500 uppercase tracking-widest font-bold leading-tight">{{ 'Farm' | t }}</span>
        <span class="text-[13px] font-semibold text-gray-900 dark:text-gray-200 truncate leading-tight">{{ (currentFarm$ | async)?.name }}</span>
      </div>
      <div *ngIf="!stacked && (farms$ | async)?.length === 1"
           class="hidden lg:flex items-center gap-1.5 h-9 px-3 rounded-full bg-emerald-50 dark:bg-emerald-900/20 text-emerald-800 dark:text-emerald-200 transition-all duration-300"
           [class.context-flash]="farmChanged"
           [matTooltip]="contextTooltip((currentOrg$ | async)?.name, (currentBranch$ | async)?.name)">
        <mat-icon class="!text-[16px] !w-[16px] !h-[16px] text-emerald-600 dark:text-emerald-400 shrink-0" aria-hidden="true">agriculture</mat-icon>
        <span class="text-[13px] font-semibold truncate max-w-[180px]">{{ (currentFarm$ | async)?.name }}</span>
      </div>
    </div>
  `,
  styles: [`
    /* Customize the appearance for a denser layout in header */
    ::ng-deep app-context-selector .mat-mdc-form-field-infix {
      padding-top: 4px !important;
      padding-bottom: 4px !important;
      min-height: 32px !important;
    }
    /* Brief green-pulse animation when the active farm context changes */
    @keyframes context-flash-anim {
      0%   { box-shadow: 0 0 0 0 rgba(139, 92, 246, 0.5); }
      50%  { box-shadow: 0 0 0 6px rgba(139, 92, 246, 0); }
      100% { box-shadow: 0 0 0 0 rgba(139, 92, 246, 0); }
    }
    .context-flash {
      animation: context-flash-anim 0.7s ease-out;
    }
  `]
})
export class ContextSelectorComponent implements OnInit, OnDestroy {
  /** Vertical full-width layout (used in the mobile context dialog). */
  @Input() stacked = false;
  private contextService = inject(WorkingContextService);
  private destroy$ = new Subject<void>();

  orgControl = new FormControl<string | null>(null);
  branchControl = new FormControl<string | null>(null);
  farmControl = new FormControl<string | null>(null);

  // Template flag to trigger the flash animation
  farmChanged = false;

  organizations$ = this.contextService.organizations$;
  branches$ = this.contextService.branches$;
  farms$ = this.contextService.farms$;

  currentOrg$ = this.contextService.currentOrg$;
  currentBranch$ = this.contextService.currentBranch$;
  currentFarm$ = this.contextService.currentFarm$;

  private isInitializing = true;

  /** Header chip tooltip: where this farm sits (organisation · branch). */
  contextTooltip(org?: string | null, branch?: string | null): string {
    return [org, branch].filter(Boolean).join(' · ');
  }

  ngOnInit() {
    // Sync current values to form controls
    combineLatest([
      this.contextService.currentOrg$,
      this.contextService.currentBranch$,
      this.contextService.currentFarm$
    ]).pipe(takeUntil(this.destroy$)).subscribe(([org, branch, farm]) => {
      this.isInitializing = true;
      if (this.orgControl.value !== org?.id) this.orgControl.setValue(org?.id || null);
      if (this.branchControl.value !== branch?.id) this.branchControl.setValue(branch?.id || null);
      if (this.farmControl.value !== farm?.id) {
        this.farmControl.setValue(farm?.id || null);
        // Trigger flash animation when farm context changes (but not on first load)
        if (!this.isInitializing && farm?.id) {
          this.triggerFarmFlash();
        }
      }
      this.isInitializing = false;
    });

    // Handle user changing org
    this.orgControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(val => {
      if (!this.isInitializing) {
        this.contextService.organizations$.pipe(take(1)).subscribe(orgs => {
          const org = orgs.find(o => o.id === val) || null;
          this.contextService.setOrganization(org);
        });
      }
    });

    // Handle user changing branch
    this.branchControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(val => {
      if (!this.isInitializing) {
        this.contextService.branches$.pipe(take(1)).subscribe(branches => {
          const branch = branches.find(b => b.id === val) || null;
          this.contextService.setBranch(branch);
        });
      }
    });

    // Handle user changing farm
    this.farmControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(val => {
      if (!this.isInitializing) {
        this.contextService.farms$.pipe(take(1)).subscribe(farms => {
          const farm = farms.find(f => f.id === val) || null;
          this.contextService.setFarm(farm);
          this.triggerFarmFlash();
        });
      }
    });
  }

  private triggerFarmFlash(): void {
    this.farmChanged = false;
    // Small timeout to allow Angular to reset the class before reapplying
    setTimeout(() => { this.farmChanged = true; }, 10);
    setTimeout(() => { this.farmChanged = false; }, 800);
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
