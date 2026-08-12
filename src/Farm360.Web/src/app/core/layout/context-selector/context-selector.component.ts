import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { WorkingContextService } from '../../services/working-context.service';
import { Subject, takeUntil, combineLatest } from 'rxjs';

@Component({
  selector: 'app-context-selector',
  standalone: true,
  imports: [CommonModule, MatSelectModule, MatFormFieldModule, MatIconModule, ReactiveFormsModule],
  template: `
    <div class="flex items-center gap-2 mr-4">
      <!-- Organization Selector (multi-org dropdown) -->
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="w-36 text-sm" *ngIf="(organizations$ | async)?.length! > 1">
        <mat-select [formControl]="orgControl" placeholder="Organization">
          <mat-option *ngFor="let org of organizations$ | async" [value]="org.id">
            {{ org.name }}
          </mat-option>
        </mat-select>
      </mat-form-field>

      <!-- Organization Display (single-org) -->
      <div *ngIf="(organizations$ | async)?.length === 1" class="hidden lg:flex flex-col mr-2">
        <span class="text-[10px] text-gray-500 uppercase tracking-widest font-bold leading-tight">Org</span>
        <span class="text-[13px] font-semibold text-gray-900 dark:text-gray-200 truncate leading-tight max-w-[120px]">
          {{ (currentOrg$ | async)?.name }}
        </span>
      </div>

      <!-- Branch Selector (multi-branch dropdown) -->
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="w-36 text-sm" *ngIf="(branches$ | async)?.length! > 1">
        <mat-select [formControl]="branchControl" placeholder="Branch">
          <mat-option *ngFor="let branch of branches$ | async" [value]="branch.id">
            {{ branch.name }}
          </mat-option>
        </mat-select>
      </mat-form-field>

      <!-- Branch Display (single-branch) -->
      <div *ngIf="(branches$ | async)?.length === 1" class="hidden lg:flex flex-col mr-2">
        <span class="text-[10px] text-gray-500 uppercase tracking-widest font-bold leading-tight">Branch</span>
        <span class="text-[13px] font-semibold text-gray-900 dark:text-gray-200 truncate leading-tight w-full">
          {{ (currentBranch$ | async)?.name }}
        </span>
      </div>

      <!-- Farm Selector (multi-farm dropdown) -->
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="w-36 text-sm" *ngIf="(farms$ | async)?.length! > 1">
        <mat-select [formControl]="farmControl" placeholder="Farm">
          <mat-option [value]="null">All Farms</mat-option>
          <mat-option *ngFor="let farm of farms$ | async" [value]="farm.id">
            {{ farm.name }}
          </mat-option>
        </mat-select>
      </mat-form-field>

      <!-- Farm Display (single-farm — prominent "Active Farm" badge) -->
      <div *ngIf="(farms$ | async)?.length === 1"
           class="hidden lg:flex items-center gap-2 px-3 py-1.5 rounded-xl border border-emerald-200 dark:border-emerald-800/60 bg-emerald-50 dark:bg-emerald-900/20 transition-all duration-300"
           [class.context-flash]="farmChanged">
        <!-- Farm Icon Badge -->
        <div class="w-6 h-6 rounded-md bg-gradient-to-br from-emerald-500 to-teal-600 flex items-center justify-center flex-shrink-0 shadow-sm shadow-emerald-500/20">
          <mat-icon class="!text-[13px] !w-[13px] !h-[13px] text-white">agriculture</mat-icon>
        </div>
        <div class="flex flex-col min-w-0">
          <span class="text-[9px] text-emerald-600 dark:text-emerald-400 uppercase tracking-widest font-bold leading-tight">Active Farm</span>
          <span class="text-[13px] font-bold text-emerald-800 dark:text-emerald-200 truncate leading-tight max-w-[110px]">
            {{ (currentFarm$ | async)?.name }}
          </span>
        </div>
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
      0%   { box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.5); }
      50%  { box-shadow: 0 0 0 6px rgba(16, 185, 129, 0); }
      100% { box-shadow: 0 0 0 0 rgba(16, 185, 129, 0); }
    }
    .context-flash {
      animation: context-flash-anim 0.7s ease-out;
    }
  `]
})
export class ContextSelectorComponent implements OnInit, OnDestroy {
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
        this.contextService.organizations$.pipe(takeUntil(this.destroy$)).subscribe(orgs => {
          const org = orgs.find(o => o.id === val) || null;
          this.contextService.setOrganization(org);
        });
      }
    });

    // Handle user changing branch
    this.branchControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(val => {
      if (!this.isInitializing) {
        this.contextService.branches$.pipe(takeUntil(this.destroy$)).subscribe(branches => {
          const branch = branches.find(b => b.id === val) || null;
          this.contextService.setBranch(branch);
        });
      }
    });

    // Handle user changing farm
    this.farmControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(val => {
      if (!this.isInitializing) {
        this.contextService.farms$.pipe(takeUntil(this.destroy$)).subscribe(farms => {
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
