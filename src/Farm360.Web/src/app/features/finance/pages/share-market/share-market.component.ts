import { Component, ChangeDetectionStrategy, inject, signal, computed, DestroyRef, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, switchMap, catchError, of } from 'rxjs';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { 
  ShareMarketOverview, 
  FarmShareConfig, 
  ShareHolding, 
  ShareTransaction, 
  Investor 
} from '../../models/finance.model';
import { ConfigureSharesDialogComponent } from '../../components/configure-shares-dialog/configure-shares-dialog';
import { BuySellSharesDialogComponent, ShareActionType } from '../../components/buy-sell-shares-dialog/buy-sell-shares-dialog';

@Component({
  selector: 'app-share-market',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatDialogModule,
    BaseChartDirective,
    PageHeaderComponent,
    LoadingComponent,
    CurrencyPipe,
    DatePipe,
    DecimalPipe
  ],
  template: `
    <app-page-header 
      title="Farm Share Market" 
      description="Equity share management, shareholder capital tracking, live valuations, and ownership distribution."
      breadcrumbActiveNode="Share Market">
      <div actions class="flex items-center gap-2">
        <button *ngIf="overview()?.isConfigured" mat-stroked-button (click)="openTradeDialog('valuation')"
                class="!rounded-xl !px-4 !py-2 !border-indigo-300 dark:!border-indigo-700 !text-indigo-700 dark:!text-indigo-300 hover:!bg-indigo-50 dark:hover:!bg-indigo-950/40 flex items-center gap-1.5 transition-all">
          <mat-icon class="!text-[18px]">trending_up</mat-icon>
          <span>Update Valuation</span>
        </button>

        <button *ngIf="overview()?.isConfigured" mat-stroked-button (click)="openConfigureDialog()"
                class="!rounded-xl !px-4 !py-2 !border-teal-300 dark:!border-teal-700 !text-teal-700 dark:!text-teal-300 hover:!bg-teal-50 dark:hover:!bg-teal-950/40 flex items-center gap-1.5 transition-all">
          <mat-icon class="!text-[18px]">tune</mat-icon>
          <span>Configure Shares</span>
        </button>

        <button *ngIf="overview()?.isConfigured" mat-flat-button color="primary" (click)="openTradeDialog('purchase')"
                class="!rounded-xl !px-5 !py-2 !bg-gradient-to-r !from-teal-600 !to-emerald-600 hover:!from-teal-700 hover:!to-emerald-700 !text-white flex items-center gap-2 shadow-sm shadow-teal-500/30 transition-all">
          <mat-icon class="!text-[20px]">add_shopping_cart</mat-icon>
          <span>Issue / Buy Shares</span>
        </button>
      </div>
    </app-page-header>

    <div class="px-6 py-4 mx-auto max-w-7xl space-y-6">

      <!-- Sub-Navigation Bar -->
      <div class="flex items-center gap-2 border-b border-gray-200 dark:border-gray-700/60 pb-3 overflow-x-auto">
        <a routerLink="/finance" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">dashboard</mat-icon> Overview
        </a>
        <a routerLink="/finance/transactions" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">receipt_long</mat-icon> General Ledger
        </a>
        <a routerLink="/finance/reports/monthly-pnl" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">calendar_view_month</mat-icon> Monthly P&L
        </a>
        <a routerLink="/finance/reports/trial-balance" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">balance</mat-icon> Trial Balance
        </a>
        <a routerLink="/finance/reports/balance-sheet" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">account_balance_wallet</mat-icon> Balance Sheet
        </a>
        <a routerLink="/finance/loans" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">account_balance</mat-icon> Loans
        </a>
        <a routerLink="/finance/investors" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">group</mat-icon> Investors & Equity
        </a>
        <a routerLink="/finance/shares" 
          class="px-4 py-2 rounded-xl text-xs font-semibold bg-teal-50 dark:bg-teal-950/40 text-teal-700 dark:text-teal-300 border border-teal-200 dark:border-teal-800/60 flex items-center gap-2 whitespace-nowrap">
          <mat-icon class="text-base">pie_chart</mat-icon> Share Market
        </a>
      </div>

      <!-- Loading State -->
      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- UNCONFIGURED STATE -->
      <div *ngIf="!isLoading() && overview() && !overview()?.isConfigured"
           class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-12 text-center relative overflow-hidden">
        
        <mat-icon class="absolute -right-8 -bottom-8 text-[180px] text-teal-500/5 rotate-[-10deg] pointer-events-none">
          pie_chart
        </mat-icon>

        <div class="max-w-md mx-auto space-y-4">
          <div class="w-16 h-16 rounded-2xl bg-gradient-to-br from-teal-500 to-emerald-600 text-white flex items-center justify-center mx-auto shadow-lg shadow-teal-500/30">
            <mat-icon class="!text-[32px] !w-[32px] !h-[32px]">storefront</mat-icon>
          </div>
          <h3 class="text-xl font-bold text-gray-900 dark:text-white">Farm Share Market Not Configured</h3>
          <p class="text-sm text-gray-500 dark:text-gray-400 leading-relaxed">
            Turn your farm into an equity share investment vehicle. Divide your total farm valuation into shares, retain your majority ownership, and issue shares to outside investors or partners.
          </p>
          <button mat-flat-button color="primary" (click)="openConfigureDialog()"
                  class="!rounded-xl !px-6 !py-2.5 !bg-gradient-to-r !from-teal-600 !to-emerald-600 hover:!from-teal-700 hover:!to-emerald-700 !text-white shadow-md shadow-teal-500/20">
            <mat-icon class="mr-1.5">tune</mat-icon>
            Set Up Farm Shares Now
          </button>
        </div>
      </div>

      <!-- CONFIGURED DASHBOARD VIEW -->
      <div *ngIf="!isLoading() && overview()?.isConfigured && overview()?.config as config" class="space-y-6">

        <!-- 1. Hero KPI Grid -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          
          <!-- Farm Valuation Card -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 shadow-sm border border-gray-100 dark:border-gray-800/50 relative overflow-hidden group hover:border-teal-300 dark:hover:border-teal-700 transition-all">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Total Farm Valuation</span>
              <div class="w-9 h-9 rounded-xl bg-teal-50 dark:bg-teal-950/40 text-teal-600 dark:text-teal-400 flex items-center justify-center">
                <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">monetization_on</mat-icon>
              </div>
            </div>
            <div class="mt-3">
              <span class="text-2xl font-extrabold text-gray-900 dark:text-white">
                {{ config.totalValuationBdt | currency:'BDT':'symbol':'1.0-0' }}
              </span>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-1 mb-0">
                {{ config.totalShares | number }} total shares
              </p>
            </div>
            <mat-icon class="absolute -right-4 -bottom-4 text-[80px] text-teal-500/5 rotate-[-10deg] pointer-events-none">monetization_on</mat-icon>
          </div>

          <!-- Share Price Card -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 shadow-sm border border-gray-100 dark:border-gray-800/50 relative overflow-hidden group hover:border-emerald-300 dark:hover:border-emerald-700 transition-all">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Current Share Price</span>
              <button (click)="openTradeDialog('valuation')" title="Update Share Valuation"
                      class="px-2 py-1 rounded-lg text-[11px] font-bold bg-emerald-100 dark:bg-emerald-950/50 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-200 transition-colors">
                Revalue
              </button>
            </div>
            <div class="mt-3">
              <span class="text-2xl font-extrabold text-emerald-600 dark:text-emerald-400">
                {{ config.sharePriceBdt | currency:'BDT':'symbol':'1.0-0' }}
              </span>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-1 mb-0">
                Valued: {{ config.lastValuationDate | date:'mediumDate' }}
              </p>
            </div>
            <mat-icon class="absolute -right-4 -bottom-4 text-[80px] text-emerald-500/5 rotate-[-10deg] pointer-events-none">trending_up</mat-icon>
          </div>

          <!-- Owner Retained Equity -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 shadow-sm border border-gray-100 dark:border-gray-800/50 relative overflow-hidden group hover:border-indigo-300 dark:hover:border-indigo-700 transition-all">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Owner Retained Equity</span>
              <div class="w-9 h-9 rounded-xl bg-indigo-50 dark:bg-indigo-950/40 text-indigo-600 dark:text-indigo-400 flex items-center justify-center">
                <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">admin_panel_settings</mat-icon>
              </div>
            </div>
            <div class="mt-3">
              <span class="text-2xl font-extrabold text-indigo-600 dark:text-indigo-400">
                {{ config.ownerOwnershipPercentage | number:'1.1-1' }}%
              </span>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-1 mb-0">
                {{ config.ownerShareCount | number }} shares ({{ config.ownerEquityValueBdt | currency:'BDT':'symbol':'1.0-0' }})
              </p>
            </div>
            <mat-icon class="absolute -right-4 -bottom-4 text-[80px] text-indigo-500/5 rotate-[-10deg] pointer-events-none">shield</mat-icon>
          </div>

          <!-- Available Pool Card -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 shadow-sm border border-gray-100 dark:border-gray-800/50 relative overflow-hidden group hover:border-amber-300 dark:hover:border-amber-700 transition-all">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Available Pool</span>
              <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-bold"
                    [class]="config.isShareSaleOpen ? 'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300' : 'bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300'">
                <span class="w-1.5 h-1.5 rounded-full" [class]="config.isShareSaleOpen ? 'bg-emerald-500 animate-pulse' : 'bg-gray-400'"></span>
                {{ config.isShareSaleOpen ? 'SALE OPEN' : 'CLOSED' }}
              </span>
            </div>
            <div class="mt-3">
              <span class="text-2xl font-extrabold text-amber-600 dark:text-amber-400">
                {{ config.availableShareCount | number }}
              </span>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-1 mb-0">
                Pool Value: {{ config.availableValuationBdt | currency:'BDT':'symbol':'1.0-0' }}
              </p>
            </div>
            <mat-icon class="absolute -right-4 -bottom-4 text-[80px] text-amber-500/5 rotate-[-10deg] pointer-events-none">inventory_2</mat-icon>
          </div>

        </div>

        <!-- 2. Ownership Visualizer & Shareholder Summary Grid -->
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">

          <!-- Left: Doughnut Chart -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-6 shadow-sm border border-gray-100 dark:border-gray-800/50 flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between mb-4">
                <h3 class="text-base font-bold text-gray-900 dark:text-white m-0 flex items-center gap-2">
                  <mat-icon class="text-teal-600">pie_chart</mat-icon>
                  Ownership Breakdown
                </h3>
                <span class="text-xs text-gray-400 font-semibold">{{ overview()?.distribution?.length || 0 }} Slices</span>
              </div>

              <!-- Chart Container -->
              <div class="relative h-56 flex items-center justify-center">
                <canvas *ngIf="chartData().datasets[0].data.length > 0"
                        baseChart
                        [data]="chartData()"
                        [options]="chartOptions"
                        [type]="'doughnut'">
                </canvas>
                <div *ngIf="chartData().datasets[0].data.length === 0" class="text-xs text-gray-400">
                  No share distribution data
                </div>
              </div>
            </div>

            <!-- Mini Interactive Legend -->
            <div class="mt-4 pt-4 border-t border-gray-100 dark:border-gray-800 space-y-2 max-h-40 overflow-y-auto custom-scrollbar">
              <div *ngFor="let item of overview()?.distribution" class="flex items-center justify-between text-xs py-1">
                <div class="flex items-center gap-2 truncate">
                  <span class="w-3 h-3 rounded-full shrink-0" [style.backgroundColor]="item.colorHex"></span>
                  <span class="font-medium text-gray-800 dark:text-gray-200 truncate">{{ item.label }}</span>
                </div>
                <div class="text-right shrink-0">
                  <span class="font-bold text-gray-900 dark:text-white">{{ item.percentage | number:'1.1-1' }}%</span>
                  <span class="text-gray-400 ml-1.5">({{ item.shareCount | number }} shares)</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Right: Market Summary & Highlights -->
          <div class="lg:col-span-2 bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-6 shadow-sm border border-gray-100 dark:border-gray-800/50 flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between mb-4">
                <div>
                  <h3 class="text-base font-bold text-gray-900 dark:text-white m-0 flex items-center gap-2">
                    <mat-icon class="text-emerald-600">analytics</mat-icon>
                    Farm Capital & Shareholder Structure
                  </h3>
                  <p class="text-xs text-gray-500 dark:text-gray-400 mt-1 mb-0">
                    Distribution of active partner capital, dividend entitlement, and equity reserves
                  </p>
                </div>
              </div>

              <!-- Metrics Row -->
              <div class="grid grid-cols-2 sm:grid-cols-3 gap-4 mb-6">
                <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-800/60 border border-gray-200/60 dark:border-gray-700/60">
                  <span class="text-xs text-gray-500 dark:text-gray-400 block font-medium">Outside Capital Raised</span>
                  <span class="text-lg font-extrabold text-teal-700 dark:text-teal-300 mt-1 block">
                    {{ overview()?.totalCapitalRaisedBdt | currency:'BDT':'symbol':'1.0-0' }}
                  </span>
                  <span class="text-[11px] text-gray-400 block mt-0.5">{{ overview()?.totalAllocatedShares | number }} shares issued</span>
                </div>

                <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-800/60 border border-gray-200/60 dark:border-gray-700/60">
                  <span class="text-xs text-gray-500 dark:text-gray-400 block font-medium">Active Shareholders</span>
                  <span class="text-lg font-extrabold text-emerald-700 dark:text-emerald-300 mt-1 block">
                    {{ overview()?.totalShareholdersCount | number }} partners
                  </span>
                  <span class="text-[11px] text-gray-400 block mt-0.5">Excluding owner</span>
                </div>

                <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-800/60 border border-gray-200/60 dark:border-gray-700/60 col-span-2 sm:col-span-1">
                  <span class="text-xs text-gray-500 dark:text-gray-400 block font-medium">Issued Shares Value</span>
                  <span class="text-lg font-extrabold text-indigo-700 dark:text-indigo-300 mt-1 block">
                    {{ (overview()?.totalAllocatedShares || 0) * config.sharePriceBdt | currency:'BDT':'symbol':'1.0-0' }}
                  </span>
                  <span class="text-[11px] text-gray-400 block mt-0.5">At current valuation</span>
                </div>
              </div>

              <!-- Quick Market Actions Bar -->
              <div class="p-4 rounded-xl bg-gradient-to-r from-teal-500/10 via-emerald-500/5 to-transparent border border-teal-200/60 dark:border-teal-800/40 flex flex-wrap items-center justify-between gap-3">
                <div class="flex items-center gap-3">
                  <div class="w-10 h-10 rounded-xl bg-teal-600 text-white flex items-center justify-center shrink-0 shadow-md shadow-teal-500/20">
                    <mat-icon>shopping_bag</mat-icon>
                  </div>
                  <div>
                    <span class="text-xs font-bold text-gray-900 dark:text-white block">Ready to accept a new investment?</span>
                    <span class="text-[11px] text-gray-500 dark:text-gray-400">Issue shares to an existing or new equity investor directly into the farm ledger.</span>
                  </div>
                </div>
                <div class="flex items-center gap-2">
                  <button mat-flat-button color="primary" (click)="openTradeDialog('purchase')"
                          class="!rounded-xl !px-4 !py-1.5 !bg-teal-600 hover:!bg-teal-700 !text-white text-xs font-semibold">
                    Issue Shares
                  </button>
                  <button mat-stroked-button (click)="openTradeDialog('transfer')"
                          class="!rounded-xl !px-4 !py-1.5 !border-gray-300 dark:!border-gray-600 text-xs font-semibold text-gray-700 dark:text-gray-300">
                    Transfer
                  </button>
                </div>
              </div>

            </div>

            <div class="mt-4 pt-3 border-t border-gray-100 dark:border-gray-800 flex items-center justify-between text-xs text-gray-500 dark:text-gray-400">
              <span>Valuation Notes: {{ config.valuationNotes || 'Standard asset-backed farm valuation model.' }}</span>
              <a routerLink="/finance/investors/pnl" class="text-teal-600 hover:underline flex items-center gap-1 font-medium">
                <span>View P&L Profit Sharing</span>
                <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">arrow_forward</mat-icon>
              </a>
            </div>
          </div>

        </div>

        <!-- 3. Shareholders Portfolio Table -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden">
          <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex flex-wrap items-center justify-between gap-3">
            <div>
              <h3 class="text-base font-bold text-gray-900 dark:text-white m-0 flex items-center gap-2">
                <mat-icon class="text-teal-600">group</mat-icon>
                Farm Shareholders & Equity Positions
              </h3>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">
                Individual partner holdings, cost basis, unrealized capital gain, and ownership %
              </p>
            </div>
            <div class="flex items-center gap-2">
              <span class="text-xs font-bold text-gray-500 dark:text-gray-400">
                {{ overview()?.shareholders?.length || 0 }} Active Partners
              </span>
            </div>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-xs">
              <thead>
                <tr class="border-b border-gray-200 dark:border-gray-700/60 bg-gray-50/50 dark:bg-gray-800/30 text-gray-500 dark:text-gray-400 font-bold uppercase tracking-wider">
                  <th class="py-3 px-6">Shareholder</th>
                  <th class="py-3 px-4">Certificate #</th>
                  <th class="py-3 px-4 text-right">Shares Held</th>
                  <th class="py-3 px-4 text-right">Equity %</th>
                  <th class="py-3 px-4 text-right">Avg Buy Price</th>
                  <th class="py-3 px-4 text-right">Invested Capital</th>
                  <th class="py-3 px-4 text-right">Current Market Value</th>
                  <th class="py-3 px-4 text-right">Gain / Loss</th>
                  <th class="py-3 px-4 text-center">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                <!-- Owner Retained Row -->
                <tr class="bg-teal-50/30 dark:bg-teal-950/20 font-medium">
                  <td class="py-3.5 px-6">
                    <div class="flex items-center gap-2">
                      <span class="w-7 h-7 rounded-lg bg-teal-600 text-white flex items-center justify-center font-bold text-xs">
                        👑
                      </span>
                      <div>
                        <span class="font-bold text-gray-900 dark:text-white block">Farm Owner (Retained)</span>
                        <span class="text-[11px] text-gray-500 dark:text-gray-400">Founding Equity</span>
                      </div>
                    </div>
                  </td>
                  <td class="py-3.5 px-4 text-gray-400">ORIG-001</td>
                  <td class="py-3.5 px-4 text-right font-bold text-gray-900 dark:text-white">
                    {{ config.ownerShareCount | number }}
                  </td>
                  <td class="py-3.5 px-4 text-right">
                    <span class="px-2 py-0.5 rounded-full font-bold bg-teal-100 dark:bg-teal-950 text-teal-800 dark:text-teal-300">
                      {{ config.ownerOwnershipPercentage | number:'1.1-1' }}%
                    </span>
                  </td>
                  <td class="py-3.5 px-4 text-right text-gray-500">—</td>
                  <td class="py-3.5 px-4 text-right text-gray-500">—</td>
                  <td class="py-3.5 px-4 text-right font-extrabold text-teal-700 dark:text-teal-300">
                    {{ config.ownerEquityValueBdt | currency:'BDT':'symbol':'1.0-0' }}
                  </td>
                  <td class="py-3.5 px-4 text-right text-gray-400">—</td>
                  <td class="py-3.5 px-4 text-center">
                    <button (click)="openConfigureDialog()"
                            class="px-2 py-1 text-xs text-teal-600 hover:text-teal-700 hover:bg-teal-50 dark:hover:bg-teal-950/50 rounded-lg transition-colors">
                      Edit
                    </button>
                  </td>
                </tr>

                <!-- Investor Shareholders Rows -->
                <tr *ngFor="let s of overview()?.shareholders" class="hover:bg-gray-50/50 dark:hover:bg-gray-800/40 transition-colors">
                  <td class="py-3.5 px-6">
                    <div class="flex items-center gap-2">
                      <div class="w-7 h-7 rounded-lg bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center font-bold text-xs">
                        {{ s.investorName.charAt(0).toUpperCase() }}
                      </div>
                      <div>
                        <span class="font-bold text-gray-900 dark:text-white block">{{ s.investorName }}</span>
                        <span class="text-[11px] text-gray-400">{{ s.investorPhone || s.investorEmail || 'No contact' }}</span>
                      </div>
                    </div>
                  </td>
                  <td class="py-3.5 px-4 font-mono text-[11px] text-gray-500">
                    {{ s.certificateNumber || '—' }}
                  </td>
                  <td class="py-3.5 px-4 text-right font-bold text-gray-900 dark:text-white">
                    {{ s.shareCount | number }}
                  </td>
                  <td class="py-3.5 px-4 text-right">
                    <span class="px-2 py-0.5 rounded-full font-bold bg-emerald-100 dark:bg-emerald-950 text-emerald-800 dark:text-emerald-300">
                      {{ s.ownershipPercentage | number:'1.2-2' }}%
                    </span>
                  </td>
                  <td class="py-3.5 px-4 text-right text-gray-600 dark:text-gray-400">
                    {{ s.averagePurchasePriceBdt | currency:'BDT':'symbol':'1.0-0' }}
                  </td>
                  <td class="py-3.5 px-4 text-right font-medium text-gray-900 dark:text-white">
                    {{ s.totalInvestedBdt | currency:'BDT':'symbol':'1.0-0' }}
                  </td>
                  <td class="py-3.5 px-4 text-right font-extrabold text-teal-700 dark:text-teal-300">
                    {{ s.currentValueBdt | currency:'BDT':'symbol':'1.0-0' }}
                  </td>
                  <td class="py-3.5 px-4 text-right">
                    <span [class]="s.unrealizedGainLossBdt >= 0 ? 'text-emerald-600 dark:text-emerald-400 font-bold' : 'text-rose-600 dark:text-rose-400 font-bold'">
                      {{ s.unrealizedGainLossBdt >= 0 ? '+' : '' }}{{ s.unrealizedGainLossBdt | currency:'BDT':'symbol':'1.0-0' }}
                      <span class="text-[10px] font-normal">({{ s.returnOnInvestmentPercent >= 0 ? '+' : '' }}{{ s.returnOnInvestmentPercent | number:'1.1-1' }}%)</span>
                    </span>
                  </td>
                  <td class="py-3.5 px-4 text-center">
                    <div class="inline-flex items-center gap-1">
                      <button (click)="openTradeDialogForInvestor('purchase', s.investorId)" title="Issue more shares"
                              class="p-1.5 text-teal-600 hover:bg-teal-50 dark:hover:bg-teal-950 rounded-lg transition-colors">
                        <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">add_circle</mat-icon>
                      </button>
                      <button (click)="openTradeDialogForInvestor('sell', s.investorId)" title="Redeem / Buy back shares"
                              class="p-1.5 text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950 rounded-lg transition-colors">
                        <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">remove_circle</mat-icon>
                      </button>
                      <button (click)="openTradeDialogForInvestor('transfer', s.investorId)" title="Transfer shares"
                              class="p-1.5 text-indigo-600 hover:bg-indigo-50 dark:hover:bg-indigo-950 rounded-lg transition-colors">
                        <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">swap_horiz</mat-icon>
                      </button>
                    </div>
                  </td>
                </tr>

                <!-- Available Shares Pool Row -->
                <tr *ngIf="config.availableShareCount > 0" class="bg-gray-50/70 dark:bg-gray-800/60 font-medium">
                  <td class="py-3.5 px-6">
                    <div class="flex items-center gap-2">
                      <span class="w-7 h-7 rounded-lg bg-gray-400 text-white flex items-center justify-center font-bold text-xs">
                        📦
                      </span>
                      <div>
                        <span class="font-bold text-gray-700 dark:text-gray-300 block">Available Shares Pool</span>
                        <span class="text-[11px] text-gray-500">Unissued Equity</span>
                      </div>
                    </div>
                  </td>
                  <td class="py-3.5 px-4 text-gray-400">POOL</td>
                  <td class="py-3.5 px-4 text-right font-bold text-amber-600 dark:text-amber-400">
                    {{ config.availableShareCount | number }}
                  </td>
                  <td class="py-3.5 px-4 text-right">
                    <span class="px-2 py-0.5 rounded-full font-bold bg-amber-100 dark:bg-amber-950 text-amber-800 dark:text-amber-300">
                      {{ (config.totalShares > 0 ? (config.availableShareCount / config.totalShares * 100) : 0) | number:'1.1-1' }}%
                    </span>
                  </td>
                  <td class="py-3.5 px-4 text-right text-gray-500">—</td>
                  <td class="py-3.5 px-4 text-right text-gray-500">—</td>
                  <td class="py-3.5 px-4 text-right font-bold text-gray-700 dark:text-gray-300">
                    {{ config.availableValuationBdt | currency:'BDT':'symbol':'1.0-0' }}
                  </td>
                  <td class="py-3.5 px-4 text-right text-gray-400">—</td>
                  <td class="py-3.5 px-4 text-center">
                    <button (click)="openTradeDialog('purchase')"
                            class="px-2 py-1 text-xs text-teal-600 hover:text-teal-700 hover:bg-teal-50 dark:hover:bg-teal-950/50 rounded-lg font-semibold transition-colors">
                      Sell Shares
                    </button>
                  </td>
                </tr>

              </tbody>
            </table>
          </div>
        </div>

        <!-- 4. Recent Transactions Audit Trail -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden">
          <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex items-center justify-between">
            <div>
              <h3 class="text-base font-bold text-gray-900 dark:text-white m-0 flex items-center gap-2">
                <mat-icon class="text-indigo-600">receipt_long</mat-icon>
                Recent Share Transactions & Audit Log
              </h3>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">
                Full chronological ledger of share issuances, buybacks, transfers, and price revaluations
              </p>
            </div>
            <span class="text-xs text-gray-400 font-semibold">{{ overview()?.recentTransactions?.length || 0 }} Records</span>
          </div>

          <div *ngIf="overview()?.recentTransactions?.length === 0" class="p-8 text-center text-xs text-gray-400">
            No share transactions recorded yet. Click "Issue / Buy Shares" above to allocate shares to partners.
          </div>

          <div *ngIf="(overview()?.recentTransactions?.length || 0) > 0" class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-xs">
              <thead>
                <tr class="border-b border-gray-200 dark:border-gray-700/60 bg-gray-50/50 dark:bg-gray-800/30 text-gray-500 dark:text-gray-400 font-bold uppercase tracking-wider">
                  <th class="py-3 px-6">Date</th>
                  <th class="py-3 px-4">Type</th>
                  <th class="py-3 px-4">Investor</th>
                  <th class="py-3 px-4 text-right">Shares</th>
                  <th class="py-3 px-4 text-right">Price / Share</th>
                  <th class="py-3 px-4 text-right">Total Amount</th>
                  <th class="py-3 px-4">Reference / Notes</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                <tr *ngFor="let tx of overview()?.recentTransactions" class="hover:bg-gray-50/50 dark:hover:bg-gray-800/40 transition-colors">
                  <td class="py-3 px-6 text-gray-600 dark:text-gray-400 whitespace-nowrap">
                    {{ tx.transactionDate | date:'mediumDate' }}
                  </td>
                  <td class="py-3 px-4 whitespace-nowrap">
                    <span class="px-2.5 py-1 rounded-full text-[11px] font-bold inline-flex items-center gap-1"
                          [ngClass]="{
                            'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300': tx.type === 'Purchase',
                            'bg-rose-100 dark:bg-rose-950/60 text-rose-800 dark:text-rose-300': tx.type === 'Sale',
                            'bg-indigo-100 dark:bg-indigo-950/60 text-indigo-800 dark:text-indigo-300': tx.type === 'Transfer',
                            'bg-amber-100 dark:bg-amber-950/60 text-amber-800 dark:text-amber-300': tx.type === 'ValuationAdjustment'
                          }">
                      {{ tx.type }}
                    </span>
                  </td>
                  <td class="py-3 px-4 font-medium text-gray-900 dark:text-white">
                    {{ tx.investorName }}
                    <span *ngIf="tx.counterpartyInvestorName" class="text-gray-400 text-[11px] block">
                      Transfer to/from: {{ tx.counterpartyInvestorName }}
                    </span>
                  </td>
                  <td class="py-3 px-4 text-right font-bold text-gray-900 dark:text-white">
                    {{ tx.shareCount | number }}
                  </td>
                  <td class="py-3 px-4 text-right text-gray-600 dark:text-gray-400">
                    {{ tx.pricePerShareBdt | currency:'BDT':'symbol':'1.0-0' }}
                  </td>
                  <td class="py-3 px-4 text-right font-extrabold text-teal-700 dark:text-teal-300">
                    {{ tx.totalAmountBdt | currency:'BDT':'symbol':'1.0-0' }}
                  </td>
                  <td class="py-3 px-4 text-gray-500 max-w-xs truncate">
                    <span *ngIf="tx.referenceId" class="font-mono text-[10px] text-gray-400 mr-1.5">[{{ tx.referenceId }}]</span>
                    {{ tx.notes || '—' }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

      </div>

    </div>
  `
})
export class ShareMarketDashboardComponent implements OnInit {
  private financeService = inject(FinanceService);
  private workingContext = inject(WorkingContextService);
  private dialog = inject(MatDialog);
  private destroyRef = inject(DestroyRef);

  public isLoading = signal(true);
  public overview = signal<ShareMarketOverview | null>(null);
  public investors = signal<Investor[]>([]);

  // Chart Configuration
  public chartData = signal<ChartData<'doughnut'>>({
    labels: [],
    datasets: [{
      data: [],
      backgroundColor: [],
      borderWidth: 2,
      borderColor: '#ffffff'
    }]
  });

  public chartOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '72%',
    plugins: {
      legend: {
        display: false
      },
      tooltip: {
        callbacks: {
          label: (context) => {
            const label = context.label || '';
            const value = context.parsed || 0;
            return ` ${label}: ${value.toLocaleString()} shares`;
          }
        }
      }
    }
  };

  ngOnInit(): void {
    this.workingContext.currentFarm$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((farm) => {
        if (farm?.id) {
          this.loadData(farm.id);
        }
      });
  }

  loadData(farmId: string): void {
    this.isLoading.set(true);

    forkJoin({
      overview: this.financeService.getShareOverview(farmId).pipe(
        catchError(() => of(null))
      ),
      investors: this.financeService.getInvestors(farmId).pipe(
        catchError(() => of([] as Investor[]))
      )
    }).subscribe({
      next: ({ overview, investors }) => {
        this.overview.set(overview);
        this.investors.set(investors);
        this.updateChart(overview);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  private updateChart(overview: ShareMarketOverview | null): void {
    if (!overview || !overview.distribution || overview.distribution.length === 0) {
      this.chartData.set({
        labels: [],
        datasets: [{ data: [], backgroundColor: [] }]
      });
      return;
    }

    const labels = overview.distribution.map(d => d.label);
    const data = overview.distribution.map(d => d.shareCount);
    const bgColors = overview.distribution.map(d => d.colorHex);

    this.chartData.set({
      labels,
      datasets: [{
        data,
        backgroundColor: bgColors,
        borderWidth: 2,
        borderColor: '#ffffff'
      }]
    });
  }

  openConfigureDialog(): void {
    const currentFarm = this.workingContext.currentFarmValue;
    if (!currentFarm) return;

    const dialogRef = this.dialog.open(ConfigureSharesDialogComponent, {
      width: '580px',
      panelClass: 'rounded-2xl',
      data: {
        farmId: currentFarm.id,
        config: this.overview()?.config
      }
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadData(currentFarm.id);
      }
    });
  }

  openTradeDialog(type: ShareActionType): void {
    const currentFarm = this.workingContext.currentFarmValue;
    const config = this.overview()?.config;
    if (!currentFarm || !config) return;

    const dialogRef = this.dialog.open(BuySellSharesDialogComponent, {
      width: '600px',
      panelClass: 'rounded-2xl',
      data: {
        farmId: currentFarm.id,
        config,
        actionType: type,
        investors: this.investors(),
        shareholders: this.overview()?.shareholders
      }
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadData(currentFarm.id);
      }
    });
  }

  openTradeDialogForInvestor(type: ShareActionType, investorId: string): void {
    const currentFarm = this.workingContext.currentFarmValue;
    const config = this.overview()?.config;
    if (!currentFarm || !config) return;

    const dialogRef = this.dialog.open(BuySellSharesDialogComponent, {
      width: '600px',
      panelClass: 'rounded-2xl',
      data: {
        farmId: currentFarm.id,
        config,
        actionType: type,
        investors: this.investors(),
        selectedInvestorId: investorId,
        shareholders: this.overview()?.shareholders
      }
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadData(currentFarm.id);
      }
    });
  }
}
