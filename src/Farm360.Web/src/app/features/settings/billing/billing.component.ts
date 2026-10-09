import { Component, OnInit, ChangeDetectionStrategy, inject, signal, computed, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import {
  SubscriptionCatalog,
  SubscriptionPlan,
  TenantSubscriptionRecord,
  TenantSubscriptionStatus,
  TrialOption
} from '../../../core/models/subscription.model';
import { SubscriptionService } from '../../../core/services/subscription.service';
import { SubscribeDialogComponent, SubscribeDialogData } from './subscribe-dialog.component';
import { parseApiError } from '../../../core/utils/error-parser';

@Component({
  selector: 'app-billing',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatSnackBarModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header
      title="Subscription & Billing Hub"
      description="Manage your subscription tier, billing frequencies, trial activations, and payment history."
      breadcrumbActiveNode="Subscription & Billing">
      <div actions class="flex items-center gap-2">
        <button
          (click)="loadData()"
          [disabled]="isLoading()"
          class="px-3.5 py-2 text-xs font-semibold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/60 rounded-xl transition-all shadow-sm inline-flex items-center gap-1.5 cursor-pointer">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]" [class.animate-spin]="isLoading()">refresh</mat-icon>
          Refresh
        </button>
        <button
          *ngIf="plans().length > 0"
          (click)="openSubscribeDialog(plans()[1] || plans()[0])"
          class="px-4 py-2 text-xs font-semibold text-white bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-700 hover:to-teal-700 rounded-xl transition-all shadow-md shadow-emerald-500/20 inline-flex items-center gap-1.5 cursor-pointer">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">workspace_premium</mat-icon>
          Upgrade Plan
        </button>
      </div>
    </app-page-header>

    <div class="space-y-8 relative">
      <!-- Loading Overlay -->
      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Error Notification -->
      <div *ngIf="errorMessage()" class="p-4 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-2xl text-xs flex items-center justify-between">
        <div class="flex items-center gap-2.5">
          <mat-icon class="text-red-500">error</mat-icon>
          <span>{{ errorMessage() }}</span>
        </div>
        <button (click)="errorMessage.set(null)" class="text-red-400 hover:text-red-600 dark:hover:text-red-200">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">close</mat-icon>
        </button>
      </div>

      <!-- Hero Card: Current Subscription Status -->
      <div *ngIf="status() as sub" class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 md:p-8 relative overflow-hidden">
        <!-- Background watermark -->
        <mat-icon class="absolute -right-6 -bottom-6 text-[140px] text-emerald-500/5 rotate-[-10deg] pointer-events-none">verified</mat-icon>

        <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-6 relative z-10">
          <!-- Plan Info -->
          <div class="space-y-2">
            <div class="flex flex-wrap items-center gap-2.5">
              <span class="text-xs font-bold uppercase tracking-wider text-gray-400">Current Subscription</span>
              
              <!-- Status Badge -->
              <span
                [class.bg-emerald-50]="sub.status === 'Active'"
                [class.text-emerald-700]="sub.status === 'Active'"
                [class.border-emerald-200]="sub.status === 'Active'"
                [class.bg-blue-50]="sub.isTrial"
                [class.text-blue-700]="sub.isTrial"
                [class.border-blue-200]="sub.isTrial"
                [class.bg-amber-50]="sub.status === 'GracePeriod'"
                [class.text-amber-700]="sub.status === 'GracePeriod'"
                [class.border-amber-200]="sub.status === 'GracePeriod'"
                [class.bg-red-50]="sub.status === 'Suspended' || sub.status === 'Expired'"
                [class.text-red-700]="sub.status === 'Suspended' || sub.status === 'Expired'"
                [class.border-red-200]="sub.status === 'Suspended' || sub.status === 'Expired'"
                class="px-2.5 py-0.5 rounded-full text-xs font-bold border inline-flex items-center gap-1">
                <span class="w-1.5 h-1.5 rounded-full"
                  [class.bg-emerald-500]="sub.status === 'Active' && !sub.isTrial"
                  [class.bg-blue-500]="sub.isTrial"
                  [class.bg-amber-500]="sub.status === 'GracePeriod'"
                  [class.bg-red-500]="sub.status === 'Suspended' || sub.status === 'Expired'"></span>
                {{ sub.isTrial ? (sub.trialDays + '-Day Free Trial') : sub.status }}
              </span>

              <!-- Billing Cycle Badge -->
              <span class="px-2 py-0.5 rounded-full text-[11px] font-medium bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300">
                {{ sub.billingCycle }} Cycle
              </span>
            </div>

            <div class="flex items-baseline gap-3">
              <h2 class="text-2xl md:text-3xl font-extrabold text-gray-900 dark:text-white tracking-tight">
                {{ sub.tier }} Plan
              </h2>
              <span class="text-sm text-gray-500 dark:text-gray-400">
                for <strong class="text-gray-800 dark:text-gray-200">{{ sub.tenantName }}</strong>
              </span>
            </div>

            <!-- Expiration / Trial Notice -->
            <p class="text-xs text-gray-500 dark:text-gray-400 max-w-xl">
              <ng-container *ngIf="sub.isTrial">
                You are currently exploring Farm360 on a <strong>{{ sub.trialDays }}-day Free Trial</strong>.
                <span class="text-blue-600 dark:text-blue-400 font-semibold" *ngIf="sub.trialDaysRemaining > 0">
                  {{ sub.trialDaysRemaining }} day{{ sub.trialDaysRemaining === 1 ? '' : 's' }} remaining (ends {{ sub.trialEndsAtUtc | date:'mediumDate' }}).
                </span>
                <span class="text-red-500 font-bold" *ngIf="sub.trialDaysRemaining <= 0">
                  Your trial has expired. Subscribe to maintain uninterrupted access.
                </span>
              </ng-container>

              <ng-container *ngIf="!sub.isTrial">
                <span *ngIf="sub.billingCycle === 'OneTime'" class="text-emerald-600 font-semibold">
                  Lifetime access unlocked. No recurring renewal necessary.
                </span>
                <span *ngIf="sub.billingCycle !== 'OneTime' && sub.subscriptionExpiresAtUtc">
                  Next renewal date: <strong>{{ sub.subscriptionExpiresAtUtc | date:'mediumDate' }}</strong>
                  <span *ngIf="sub.daysRemaining !== null"> ({{ sub.daysRemaining }} day{{ sub.daysRemaining === 1 ? '' : 's' }} left)</span>.
                </span>
              </ng-container>
            </p>
          </div>

          <!-- Quick Action -->
          <div class="flex items-center gap-3 shrink-0">
            <button
              (click)="scrollToPlans()"
              class="px-5 py-2.5 text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-700 dark:bg-emerald-500 dark:hover:bg-emerald-600 rounded-xl transition-all shadow-md shadow-emerald-600/20 inline-flex items-center gap-2 cursor-pointer">
              <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">shopping_cart_checkout</mat-icon>
              {{ sub.isTrial ? 'Choose Subscription' : 'Change Plan' }}
            </button>
          </div>
        </div>

        <!-- Quota Gauges -->
        <div class="mt-8 pt-6 border-t border-gray-100 dark:border-gray-700/60 grid grid-cols-1 md:grid-cols-3 gap-6">
          <!-- Users Quota -->
          <div class="space-y-2">
            <div class="flex justify-between text-xs">
              <span class="font-semibold text-gray-700 dark:text-gray-300 flex items-center gap-1.5">
                <mat-icon class="!text-[16px] !w-[16px] !h-[16px] text-gray-400">group</mat-icon> User Seats
              </span>
              <span class="text-gray-500 dark:text-gray-400">
                <strong>{{ sub.currentUsers }}</strong> / {{ sub.maxUsers }}
              </span>
            </div>
            <div class="w-full bg-gray-100 dark:bg-gray-700 h-2 rounded-full overflow-hidden">
              <div
                class="h-full rounded-full transition-all duration-500"
                [class.bg-emerald-500]="quotaUsersPercent() < 80"
                [class.bg-amber-500]="quotaUsersPercent() >= 80 && quotaUsersPercent() < 100"
                [class.bg-red-500]="quotaUsersPercent() >= 100"
                [style.width.%]="quotaUsersPercent()"></div>
            </div>
          </div>

          <!-- Farms Quota -->
          <div class="space-y-2">
            <div class="flex justify-between text-xs">
              <span class="font-semibold text-gray-700 dark:text-gray-300 flex items-center gap-1.5">
                <mat-icon class="!text-[16px] !w-[16px] !h-[16px] text-gray-400">location_on</mat-icon> Farm Locations
              </span>
              <span class="text-gray-500 dark:text-gray-400">
                <strong>{{ sub.currentFarms }}</strong> / {{ sub.maxFarms }}
              </span>
            </div>
            <div class="w-full bg-gray-100 dark:bg-gray-700 h-2 rounded-full overflow-hidden">
              <div
                class="h-full rounded-full transition-all duration-500"
                [class.bg-emerald-500]="quotaFarmsPercent() < 80"
                [class.bg-amber-500]="quotaFarmsPercent() >= 80 && quotaFarmsPercent() < 100"
                [class.bg-red-500]="quotaFarmsPercent() >= 100"
                [style.width.%]="quotaFarmsPercent()"></div>
            </div>
          </div>

          <!-- Animals Quota -->
          <div class="space-y-2">
            <div class="flex justify-between text-xs">
              <span class="font-semibold text-gray-700 dark:text-gray-300 flex items-center gap-1.5">
                <mat-icon class="!text-[16px] !w-[16px] !h-[16px] text-gray-400">pets</mat-icon> Livestock Capacity
              </span>
              <span class="text-gray-500 dark:text-gray-400">
                <strong>{{ sub.currentAnimals }}</strong> / {{ sub.maxAnimals }}
              </span>
            </div>
            <div class="w-full bg-gray-100 dark:bg-gray-700 h-2 rounded-full overflow-hidden">
              <div
                class="h-full rounded-full transition-all duration-500"
                [class.bg-emerald-500]="quotaAnimalsPercent() < 80"
                [class.bg-amber-500]="quotaAnimalsPercent() >= 80 && quotaAnimalsPercent() < 100"
                [class.bg-red-500]="quotaAnimalsPercent() >= 100"
                [style.width.%]="quotaAnimalsPercent()"></div>
            </div>
          </div>
        </div>
      </div>

      <!-- Free Trial Activation Section (Visible only if tenant has not used trial yet) -->
      <div *ngIf="status() && !status()?.hasUsedTrial && !status()?.isTrial" class="bg-gradient-to-br from-blue-50/80 via-white to-emerald-50/50 dark:from-blue-950/20 dark:via-gray-800 dark:to-emerald-950/20 rounded-2xl p-6 md:p-8 border border-blue-100 dark:border-blue-900/40 shadow-sm relative overflow-hidden">
        <div class="max-w-2xl mb-6">
          <div class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-blue-100 dark:bg-blue-900/50 text-blue-700 dark:text-blue-300 text-xs font-bold uppercase tracking-wider mb-2">
            <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">bolt</mat-icon> Risk-Free Evaluation
          </div>
          <h3 class="text-xl font-bold text-gray-900 dark:text-white tracking-tight">
            Start Your Free Trial — No Credit Card Required
          </h3>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-1">
            Choose a trial duration that best fits your evaluation schedule. Full features unlocked immediately.
          </p>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-5">
          <div *ngFor="let trial of trialOptions()"
            class="bg-white dark:bg-gray-800 rounded-xl p-5 border border-gray-100 dark:border-gray-700 shadow-sm hover:shadow-md hover:border-blue-300 dark:hover:border-blue-600 transition-all flex flex-col justify-between relative overflow-hidden">
            <div *ngIf="trial.days === 7" class="absolute top-2 right-2 text-[9px] font-bold px-2 py-0.5 rounded-full bg-blue-600 text-white">
              Recommended
            </div>
            <div>
              <div class="w-10 h-10 rounded-xl bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400 flex items-center justify-center mb-3">
                <mat-icon>hourglass_top</mat-icon>
              </div>
              <h4 class="text-sm font-bold text-gray-900 dark:text-white">{{ trial.title }}</h4>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-1 leading-relaxed">{{ trial.description }}</p>
            </div>

            <button
              (click)="onActivateTrial(trial.days)"
              [disabled]="isActivatingTrial()"
              class="mt-5 w-full py-2 px-3 text-xs font-bold rounded-lg border border-blue-600 text-blue-600 hover:bg-blue-600 hover:text-white dark:border-blue-400 dark:text-blue-400 dark:hover:bg-blue-500 dark:hover:text-white transition-all cursor-pointer flex items-center justify-center gap-1.5 disabled:opacity-50">
              <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">play_arrow</mat-icon>
              Activate {{ trial.days }}-Day Trial
            </button>
          </div>
        </div>
      </div>

      <!-- Pricing Plans Section -->
      <div id="plans-section" class="space-y-6">
        <div class="text-center max-w-xl mx-auto space-y-2">
          <h3 class="text-2xl font-bold text-gray-900 dark:text-white tracking-tight">
            Flexible Plans Built for Growth
          </h3>
          <p class="text-xs text-gray-500 dark:text-gray-400">
            From single livestock farms to commercial agro-enterprises, select the tier and billing frequency that fits your scale.
          </p>

          <!-- Frequency Toggle Switch -->
          <div class="inline-flex items-center p-1 bg-gray-100 dark:bg-gray-700/80 rounded-2xl mt-4">
            <button
              type="button"
              (click)="selectedCycle.set('Monthly')"
              [class.bg-white]="selectedCycle() === 'Monthly'"
              [class.dark:bg-gray-800]="selectedCycle() === 'Monthly'"
              [class.text-gray-900]="selectedCycle() === 'Monthly'"
              [class.dark:text-white]="selectedCycle() === 'Monthly'"
              [class.shadow-sm]="selectedCycle() === 'Monthly'"
              class="px-4 py-2 text-xs font-bold rounded-xl transition-all text-gray-500 dark:text-gray-400 cursor-pointer">
              Monthly
            </button>
            <button
              type="button"
              (click)="selectedCycle.set('Yearly')"
              [class.bg-white]="selectedCycle() === 'Yearly'"
              [class.dark:bg-gray-800]="selectedCycle() === 'Yearly'"
              [class.text-gray-900]="selectedCycle() === 'Yearly'"
              [class.dark:text-white]="selectedCycle() === 'Yearly'"
              [class.shadow-sm]="selectedCycle() === 'Yearly'"
              class="px-4 py-2 text-xs font-bold rounded-xl transition-all text-gray-500 dark:text-gray-400 cursor-pointer flex items-center gap-1.5">
              <span>Yearly</span>
              <span class="text-[9px] font-bold px-1.5 py-0.5 bg-emerald-500 text-white rounded-full">Save ~17%</span>
            </button>
            <button
              type="button"
              (click)="selectedCycle.set('OneTime')"
              [class.bg-white]="selectedCycle() === 'OneTime'"
              [class.dark:bg-gray-800]="selectedCycle() === 'OneTime'"
              [class.text-gray-900]="selectedCycle() === 'OneTime'"
              [class.dark:text-white]="selectedCycle() === 'OneTime'"
              [class.shadow-sm]="selectedCycle() === 'OneTime'"
              class="px-4 py-2 text-xs font-bold rounded-xl transition-all text-gray-500 dark:text-gray-400 cursor-pointer flex items-center gap-1.5">
              <span>One-Time</span>
              <span class="text-[9px] font-bold px-1.5 py-0.5 bg-amber-500 text-white rounded-full">Lifetime</span>
            </button>
          </div>
        </div>

        <!-- Plans Grid -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 items-stretch">
          <div *ngFor="let plan of plans()"
            [class.border-emerald-500]="plan.isPopular"
            [class.ring-2]="plan.isPopular"
            [class.ring-emerald-500/20]="plan.isPopular"
            [class.scale-[1.02]]="plan.isPopular"
            class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700/80 shadow-sm p-6 flex flex-col justify-between relative transition-all duration-300 hover:shadow-xl hover:border-emerald-500/30">
            
            <!-- Popular Badge -->
            <div *ngIf="plan.isPopular" class="absolute -top-3 left-1/2 -translate-x-1/2 px-3 py-1 bg-gradient-to-r from-emerald-600 to-teal-600 text-white text-[10px] font-extrabold uppercase tracking-wider rounded-full shadow-md">
              Most Popular
            </div>

            <div>
              <div class="flex items-center justify-between">
                <span class="text-xs font-bold uppercase tracking-wider text-gray-400">{{ plan.name }}</span>
                <span *ngIf="isCurrentPlan(plan.tier)" class="text-[10px] font-bold px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
                  Current
                </span>
              </div>

              <!-- Price Display -->
              <div class="mt-4 mb-3">
                <div class="flex items-baseline gap-1">
                  <span class="text-3xl font-extrabold text-gray-900 dark:text-white">
                    ৳{{ getPlanPrice(plan) | number }}
                  </span>
                  <span class="text-xs text-gray-500 dark:text-gray-400 font-medium">
                    <ng-container *ngIf="selectedCycle() === 'Monthly'">/ mo</ng-container>
                    <ng-container *ngIf="selectedCycle() === 'Yearly'">/ yr</ng-container>
                    <ng-container *ngIf="selectedCycle() === 'OneTime'">one-time</ng-container>
                  </span>
                </div>
                <p class="text-xs text-gray-500 dark:text-gray-400 mt-1 min-h-[36px]">{{ plan.description }}</p>
              </div>

              <!-- Limits Overview -->
              <div class="py-3 border-y border-gray-100 dark:border-gray-700/60 space-y-1.5 text-xs text-gray-600 dark:text-gray-300">
                <div class="flex items-center gap-2">
                  <mat-icon class="!text-[16px] !w-[16px] !h-[16px] text-emerald-500">check_circle</mat-icon>
                  <span><strong>{{ plan.maxUsers }}</strong> User Seats</span>
                </div>
                <div class="flex items-center gap-2">
                  <mat-icon class="!text-[16px] !w-[16px] !h-[16px] text-emerald-500">check_circle</mat-icon>
                  <span><strong>{{ plan.maxFarms }}</strong> Farm Location{{ plan.maxFarms > 1 ? 's' : '' }}</span>
                </div>
                <div class="flex items-center gap-2">
                  <mat-icon class="!text-[16px] !w-[16px] !h-[16px] text-emerald-500">check_circle</mat-icon>
                  <span><strong>{{ plan.maxAnimals | number }}</strong> Livestock Head</span>
                </div>
              </div>

              <!-- Detailed Feature List -->
              <ul class="mt-4 space-y-2 text-xs text-gray-500 dark:text-gray-400">
                <li *ngFor="let feat of plan.features" class="flex items-start gap-2">
                  <mat-icon class="!text-[15px] !w-[15px] !h-[15px] text-emerald-500 shrink-0 mt-0.5">done</mat-icon>
                  <span>{{ feat }}</span>
                </li>
              </ul>
            </div>

            <!-- Action Button -->
            <button
              (click)="openSubscribeDialog(plan)"
              [class.bg-emerald-600]="plan.isPopular"
              [class.hover:bg-emerald-700]="plan.isPopular"
              [class.text-white]="plan.isPopular"
              [class.bg-gray-100]="!plan.isPopular"
              [class.dark:bg-gray-700]="!plan.isPopular"
              [class.hover:bg-gray-200]="!plan.isPopular"
              [class.dark:hover:bg-gray-600]="!plan.isPopular"
              [class.text-gray-800]="!plan.isPopular"
              [class.dark:text-white]="!plan.isPopular"
              class="mt-6 w-full py-2.5 px-4 rounded-xl text-xs font-bold transition-all shadow-sm flex items-center justify-center gap-1.5 cursor-pointer">
              <span>{{ isCurrentPlan(plan.tier) ? 'Renew / Extend' : 'Choose ' + plan.name }}</span>
              <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">arrow_forward</mat-icon>
            </button>
          </div>
        </div>
      </div>

      <!-- Payment & Invoices History Table -->
      <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
        <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-700/80 flex items-center justify-between">
          <div>
            <h3 class="text-base font-bold text-gray-900 dark:text-white">Billing & Payment History</h3>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">Audit log of all subscription payments and invoices for this organization.</p>
          </div>
        </div>

        <!-- History Table -->
        <div class="overflow-x-auto" *ngIf="history().length > 0">
          <table class="w-full text-left border-collapse text-xs">
            <thead>
              <tr class="bg-gray-50/50 dark:bg-gray-700/30 border-b border-gray-100 dark:border-gray-700 text-gray-400 font-bold uppercase tracking-wider text-[10px]">
                <th class="py-3.5 px-6">Invoice #</th>
                <th class="py-3.5 px-6">Plan Tier</th>
                <th class="py-3.5 px-6">Billing Cycle</th>
                <th class="py-3.5 px-6">Amount</th>
                <th class="py-3.5 px-6">Method & TrxID</th>
                <th class="py-3.5 px-6">Date</th>
                <th class="py-3.5 px-6 text-right">Status</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
              <tr *ngFor="let item of history()" class="hover:bg-gray-50/50 dark:hover:bg-gray-700/20 transition-colors">
                <td class="py-4 px-6 font-mono font-semibold text-gray-800 dark:text-gray-200">
                  {{ item.invoiceNumber }}
                </td>
                <td class="py-4 px-6 font-medium text-gray-900 dark:text-white">
                  {{ item.tier }}
                </td>
                <td class="py-4 px-6 text-gray-500 dark:text-gray-400">
                  {{ item.billingCycle }}
                </td>
                <td class="py-4 px-6 font-bold text-gray-900 dark:text-white">
                  ৳{{ item.amount | number }} <span class="text-[10px] text-gray-400 font-normal">{{ item.currency }}</span>
                </td>
                <td class="py-4 px-6 text-gray-600 dark:text-gray-300">
                  <div class="font-medium">{{ item.paymentMethod }}</div>
                  <div class="text-[11px] font-mono text-gray-400" *ngIf="item.paymentReference">Ref: {{ item.paymentReference }}</div>
                </td>
                <td class="py-4 px-6 text-gray-500 dark:text-gray-400">
                  {{ item.createdAtUtc | date:'mediumDate' }}
                </td>
                <td class="py-4 px-6 text-right">
                  <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold border"
                    [class.bg-emerald-50]="item.status === 'Completed'" [class.text-emerald-700]="item.status === 'Completed'" [class.dark:bg-emerald-950/40]="item.status === 'Completed'" [class.dark:text-emerald-300]="item.status === 'Completed'" [class.border-emerald-200]="item.status === 'Completed'" [class.dark:border-emerald-800]="item.status === 'Completed'"
                    [class.bg-amber-50]="item.status === 'Pending'" [class.text-amber-700]="item.status === 'Pending'" [class.dark:bg-amber-950/40]="item.status === 'Pending'" [class.dark:text-amber-300]="item.status === 'Pending'" [class.border-amber-200]="item.status === 'Pending'" [class.dark:border-amber-800]="item.status === 'Pending'"
                    [class.bg-red-50]="item.status === 'Rejected'" [class.text-red-700]="item.status === 'Rejected'" [class.dark:bg-red-950/40]="item.status === 'Rejected'" [class.dark:text-red-300]="item.status === 'Rejected'" [class.border-red-200]="item.status === 'Rejected'" [class.dark:border-red-800]="item.status === 'Rejected'">
                    <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">{{ item.status === 'Completed' ? 'check' : item.status === 'Pending' ? 'hourglass_top' : 'close' }}</mat-icon> {{ item.status }}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Empty State if no invoices -->
        <app-empty-state
          *ngIf="!isLoading() && history().length === 0"
          icon="receipt_long"
          title="No Billing Records Found"
          description="Your payment receipts and subscription invoices will appear here once recorded."
          actionText="Subscribe Now"
          (actionClicked)="scrollToPlans()">
        </app-empty-state>
      </div>
    </div>
  `
})
export class BillingComponent implements OnInit {
  private subscriptionService = inject(SubscriptionService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private destroyRef = inject(DestroyRef);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  public status = signal<TenantSubscriptionStatus | null>(null);
  public catalog = signal<SubscriptionCatalog | null>(null);
  public history = signal<TenantSubscriptionRecord[]>([]);
  public isLoading = signal<boolean>(true);
  public isActivatingTrial = signal<boolean>(false);
  public errorMessage = signal<string | null>(null);
  public selectedCycle = signal<'Monthly' | 'Yearly' | 'OneTime'>('Monthly');

  // Computed state
  public plans = computed(() => this.catalog()?.plans ?? []);
  public trialOptions = computed(() => this.catalog()?.trialOptions ?? []);

  public quotaUsersPercent = computed(() => {
    const s = this.status();
    if (!s || s.maxUsers <= 0) return 0;
    return Math.min(100, Math.round((s.currentUsers / s.maxUsers) * 100));
  });

  public quotaFarmsPercent = computed(() => {
    const s = this.status();
    if (!s || s.maxFarms <= 0) return 0;
    return Math.min(100, Math.round((s.currentFarms / s.maxFarms) * 100));
  });

  public quotaAnimalsPercent = computed(() => {
    const s = this.status();
    if (!s || s.maxAnimals <= 0) return 0;
    return Math.min(100, Math.round((s.currentAnimals / s.maxAnimals) * 100));
  });

  ngOnInit(): void {
    this.loadData();
    this.handleCheckoutRedirect();
  }

  /** Shows the result of an SSLCommerz checkout the user just returned from, then clears the query param. */
  private handleCheckoutRedirect(): void {
    const checkout = this.route.snapshot.queryParamMap.get('checkout');
    if (!checkout) {
      return;
    }

    const messages: Record<string, string> = {
      success: '✅ Payment confirmed! Your subscription is now active.',
      fail: '❌ The payment did not complete. No charge was made -- please try again.',
      cancel: 'Checkout was cancelled. No charge was made.'
    };

    this.snackBar.open(messages[checkout] ?? 'Checkout finished.', 'Dismiss', {
      duration: 7000,
      horizontalPosition: 'right',
      verticalPosition: 'top'
    });

    // A successful online checkout activates via the gateway's IPN, which can land slightly after
    // this redirect -- reload shortly after so the status card reflects it.
    if (checkout === 'success') {
      setTimeout(() => this.loadData(), 2000);
    }

    this.router.navigate([], { queryParams: {}, replaceUrl: true });
  }

  loadData(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    forkJoin({
      status: this.subscriptionService.getCurrentSubscription(),
      catalog: this.subscriptionService.getCatalog(),
      history: this.subscriptionService.getHistory()
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.status.set(res.status);
          this.catalog.set(res.catalog);
          this.history.set(res.history);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.errorMessage.set(parseApiError(err, 'Failed to load subscription details.'));
          this.isLoading.set(false);
        }
      });
  }

  getPlanPrice(plan: SubscriptionPlan): number {
    switch (this.selectedCycle()) {
      case 'Yearly':
        return plan.yearlyPrice;
      case 'OneTime':
        return plan.oneTimePrice;
      case 'Monthly':
      default:
        return plan.monthlyPrice;
    }
  }

  isCurrentPlan(planTier: string): boolean {
    const s = this.status();
    if (!s) return false;
    return s.tier?.toLowerCase() === planTier?.toLowerCase();
  }

  onActivateTrial(days: number): void {
    if (this.isActivatingTrial()) return;
    this.isActivatingTrial.set(true);

    this.subscriptionService.startTrial(days)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updatedStatus) => {
          this.status.set(updatedStatus);
          this.isActivatingTrial.set(false);
          this.snackBar.open(`🎉 ${days}-Day Free Trial activated successfully!`, 'Dismiss', {
            duration: 5000,
            horizontalPosition: 'right',
            verticalPosition: 'top'
          });
        },
        error: (err) => {
          this.isActivatingTrial.set(false);
          const msg = parseApiError(err, 'Failed to activate free trial.');
          this.snackBar.open(`Error: ${msg}`, 'Dismiss', {
            duration: 6000,
            horizontalPosition: 'right',
            verticalPosition: 'top'
          });
        }
      });
  }

  openSubscribeDialog(plan: SubscriptionPlan): void {
    const dialogRef = this.dialog.open<SubscribeDialogComponent, SubscribeDialogData, TenantSubscriptionRecord>(
      SubscribeDialogComponent,
      {
        data: {
          plan,
          initialCycle: this.selectedCycle()
        },
        panelClass: ['rounded-2xl', 'p-0', 'overflow-hidden'],
        autoFocus: false,
        disableClose: false
      }
    );

    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((result) => {
      if (result) {
        // The subscription is NOT active yet -- it's recorded as Pending until an admin verifies
        // the payment reference, so `status` (the tenant's actual tier/access) is left untouched.
        this.snackBar.open(
          `Payment reference submitted (${result.invoiceNumber}). We'll verify it and activate your ${result.tier} plan shortly.`,
          'Dismiss',
          { duration: 7000, horizontalPosition: 'right', verticalPosition: 'top' }
        );
        // Reload history to show the new pending invoice
        this.subscriptionService.getHistory().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
          next: (hist) => this.history.set(hist)
        });
      }
    });
  }

  scrollToPlans(): void {
    const elem = document.getElementById('plans-section');
    if (elem) {
      elem.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }
}
