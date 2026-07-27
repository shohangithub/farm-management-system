import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { WorkingContextService } from '../../services/working-context.service';
import { Subject, takeUntil, combineLatest } from 'rxjs';

@Component({
  selector: 'app-context-selector',
  standalone: true,
  imports: [CommonModule, MatSelectModule, MatFormFieldModule, ReactiveFormsModule],
  template: `
    <div class="flex items-center gap-2 mr-4">
      <!-- Organization Selector -->
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="w-36 text-sm" *ngIf="(organizations$ | async)?.length! > 1">
        <mat-select [formControl]="orgControl" placeholder="Organization">
          <mat-option *ngFor="let org of organizations$ | async" [value]="org.id">
            {{ org.name }}
          </mat-option>
        </mat-select>
      </mat-form-field>
      
      <div *ngIf="(organizations$ | async)?.length === 1" class="hidden lg:flex flex-col mr-2">
        <span class="text-[10px] text-gray-500 uppercase tracking-widest font-bold leading-tight">Org</span>
        <span class="text-[13px] font-semibold text-gray-900 dark:text-gray-200 truncate leading-tight max-w-[120px]">
          {{ (currentOrg$ | async)?.name }}
        </span>
      </div>

      <!-- Branch Selector -->
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="w-36 text-sm" *ngIf="(branches$ | async)?.length! > 1">
        <mat-select [formControl]="branchControl" placeholder="Branch">
          <mat-option *ngFor="let branch of branches$ | async" [value]="branch.id">
            {{ branch.name }}
          </mat-option>
        </mat-select>
      </mat-form-field>
      
      <div *ngIf="(branches$ | async)?.length === 1" class="hidden lg:flex flex-col mr-2">
        <span class="text-[10px] text-gray-500 uppercase tracking-widest font-bold leading-tight">Branch</span>
        <span class="text-[13px] font-semibold text-gray-900 dark:text-gray-200 truncate leading-tight w-full">
          {{ (currentBranch$ | async)?.name }}
        </span>
      </div>

      <!-- Farm Selector -->
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="w-36 text-sm" *ngIf="(farms$ | async)?.length! > 1">
        <mat-select [formControl]="farmControl" placeholder="Farm">
          <!-- Optional All Farms -->
          <mat-option [value]="null">All Farms</mat-option>
          <mat-option *ngFor="let farm of farms$ | async" [value]="farm.id">
            {{ farm.farmName }}
          </mat-option>
        </mat-select>
      </mat-form-field>

      <div *ngIf="(farms$ | async)?.length === 1" class="hidden lg:flex flex-col">
        <span class="text-[10px] text-gray-500 uppercase tracking-widest font-bold leading-tight">Farm</span>
        <span class="text-[13px] font-semibold text-gray-900 dark:text-gray-200 truncate leading-tight max-w-[120px]">
          {{ (currentFarm$ | async)?.farmName }}
        </span>
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
  `]
})
export class ContextSelectorComponent implements OnInit, OnDestroy {
  private contextService = inject(WorkingContextService);
  private destroy$ = new Subject<void>();

  orgControl = new FormControl<string | null>(null);
  branchControl = new FormControl<string | null>(null);
  farmControl = new FormControl<string | null>(null);

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
      if (this.farmControl.value !== farm?.id) this.farmControl.setValue(farm?.id || null);
      this.isInitializing = false;
    });

    // Handle user changing values
    this.orgControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(val => {
      if (!this.isInitializing) {
        this.contextService.organizations$.pipe(takeUntil(this.destroy$)).subscribe(orgs => {
          const org = orgs.find(o => o.id === val) || null;
          this.contextService.setOrganization(org);
        });
      }
    });

    this.branchControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(val => {
      if (!this.isInitializing) {
        this.contextService.branches$.pipe(takeUntil(this.destroy$)).subscribe(branches => {
          const branch = branches.find(b => b.id === val) || null;
          this.contextService.setBranch(branch);
        });
      }
    });

    this.farmControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(val => {
      if (!this.isInitializing) {
        this.contextService.farms$.pipe(takeUntil(this.destroy$)).subscribe(farms => {
          const farm = farms.find(f => f.id === val) || null;
          this.contextService.setFarm(farm);
        });
      }
    });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
