import { CompleteReturnDialogComponent, CompleteReturnDialogResult } from '../../components/dialogs/complete-return-dialog/complete-return-dialog.component';
import { InventorySubnavComponent } from '../../components/inventory-subnav/inventory-subnav.component';
import { Component, ChangeDetectionStrategy, inject, signal, computed, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { toObservable, toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { of } from 'rxjs';

import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import {
  PurchaseReturn,
  PurchaseReturnStatus,
  PurchaseReturnStatusNames,
  PurchaseReturnReasonNames,
  PurchaseReturnParams
} from '../../models/inventory.models';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { parseApiError } from '../../../../core/utils/error-parser';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { NotificationService } from '../../../../shared/services/notification.service';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

@Component({
  selector: 'app-purchase-return-list',
  standalone: true,
  imports: [InventorySubnavComponent, MoneyPipe, 
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    PageHeaderComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    LoadingComponent,
    TranslatePipe
  ],
  template: `
    <app-page-header
      [title]="'Purchase Returns' | t"
      [description]="'Goods sent back to suppliers.' | t"
      [breadcrumbActiveNode]="'Purchase Returns' | t">
      <div actions class="flex items-center gap-3">
        <button [routerLink]="['/inventory/purchase-orders']"
          class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 hover:bg-gray-50 rounded-xl transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">receipt_long</mat-icon> {{ 'Purchase Orders' | t }}
        </button>
      </div>
    </app-page-header>
    <app-inventory-subnav></app-inventory-subnav>

    <div class="bg-white dark:bg-gray-900 rounded-2xl shadow-sm border border-gray-200 dark:border-gray-800 overflow-hidden relative min-h-[400px]">
      <app-loading *ngIf="loading()" [overlay]="true"></app-loading>
      <app-error-state *ngIf="error() as err" [message]="err" (retry)="retryLoad()"></app-error-state>

      <!-- Filters Toolbar -->
      <div class="p-4 border-b border-gray-100 dark:border-gray-800 flex flex-col sm:flex-row items-center justify-between gap-4 bg-gray-50/50 dark:bg-gray-900/30">
        <div class="relative w-full sm:w-80">
          <mat-icon class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 !text-[18px] !w-[18px] !h-[18px]">search</mat-icon>
          <input [ngModel]="searchTerm()" (ngModelChange)="onSearchChange($event)"
            [placeholder]="'Search return # or credit note...' | t"
            class="w-full pl-9 pr-4 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-amber-500/20 focus:border-amber-500 transition-all" />
        </div>

        <div class="flex items-center gap-3 w-full sm:w-auto">
          <select [ngModel]="statusFilter()" (ngModelChange)="onStatusChange($event)"
            class="w-full sm:w-48 px-3 py-2 text-sm rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-amber-500/20 focus:border-amber-500 transition-all">
            <option [ngValue]="null">{{ 'All Statuses' | t }}</option>
            <option [ngValue]="PurchaseReturnStatus.Draft">{{ 'Draft' | t }}</option>
            <option [ngValue]="PurchaseReturnStatus.Completed">{{ 'Completed' | t }}</option>
            <option [ngValue]="PurchaseReturnStatus.Cancelled">{{ 'Cancelled' | t }}</option>
          </select>
        </div>
      </div>

      <!-- Empty State -->
      <app-empty-state
        *ngIf="!loading() && !error() && (!result()?.items || result()?.items?.length === 0)"
        icon="assignment_return"
        [title]="'No Purchase Returns found' | t"
        [description]="emptyDescription | t"
        [actionLabel]="'Go to Purchase Orders' | t"
        (action)="router.navigate(['/inventory/purchase-orders'])">
      </app-empty-state>

      <!-- Returns Table -->
      <div *ngIf="!loading() && result()?.items?.length" class="overflow-x-auto">
        <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
          <thead class="bg-gray-50 dark:bg-gray-800/60 text-xs font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider">
            <tr>
              <th scope="col" class="px-5 py-3.5 text-left">{{ 'Return #' | t }}</th>
              <th scope="col" class="px-5 py-3.5 text-left">{{ 'Date' | t }}</th>
              <th scope="col" class="px-5 py-3.5 text-left">{{ 'PO Reference' | t }}</th>
              <th scope="col" class="px-5 py-3.5 text-left">{{ 'Supplier' | t }}</th>
              <th scope="col" class="px-5 py-3.5 text-left">{{ 'Reason' | t }}</th>
              <th scope="col" class="px-5 py-3.5 text-center">{{ 'Status' | t }}</th>
              <th scope="col" class="px-5 py-3.5 text-right">{{ 'Total Refund' | t }}</th>
              <th scope="col" class="px-5 py-3.5 text-right">{{ 'Actions' | t }}</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100 dark:divide-gray-800 bg-white dark:bg-gray-800">
            @for (ret of result()?.items; track ret.id) {
              <tr class="hover:bg-gray-50/70 dark:hover:bg-gray-700/50 transition-colors">
                <td class="px-5 py-4 text-sm font-bold text-gray-900 dark:text-white">
                  <div class="flex items-center gap-2">
                    <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-amber-500">assignment_return</mat-icon>
                    <span>{{ ret.returnNumber }}</span>
                  </div>
                  <span *ngIf="ret.creditNoteNumber" class="text-xs text-gray-400 block font-normal mt-0.5">
                    {{ 'Ref: {ref}' | t: { ref: ret.creditNoteNumber } }}
                  </span>
                </td>
                <td class="px-5 py-4 text-sm text-gray-600 dark:text-gray-300">
                  {{ ret.returnDate | date:'mediumDate' }}
                </td>
                <td class="px-5 py-4 text-sm font-medium">
                  <a [routerLink]="['/inventory/purchase-orders', ret.purchaseOrderId]"
                     class="text-emerald-600 hover:text-emerald-700 hover:underline">
                    {{ ret.poNumber || ('View PO' | t) }}
                  </a>
                </td>
                <td class="px-5 py-4 text-sm text-gray-700 dark:text-gray-300 font-medium">
                  {{ ret.supplierName || '—' }}
                </td>
                <td class="px-5 py-4 text-sm text-gray-600 dark:text-gray-300">
                  <span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 dark:bg-gray-700 text-gray-800 dark:text-gray-200">
                    {{ getReasonName(ret.reason) | t }}
                  </span>
                </td>
                <td class="px-5 py-4 text-center">
                  <span class="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold uppercase tracking-wider shadow-sm"
                    [ngClass]="{
                      'bg-gray-100 text-gray-700 border border-gray-200 dark:bg-gray-700 dark:text-gray-300': ret.status === PurchaseReturnStatus.Draft,
                      'bg-success-50 text-success-700 border border-success-200 dark:bg-success-900/40 dark:text-success-300': ret.status === PurchaseReturnStatus.Completed,
                      'bg-red-50 text-red-700 border border-red-200 dark:bg-red-900/40 dark:text-red-300': ret.status === PurchaseReturnStatus.Cancelled
                    }">
                    {{ getStatusName(ret.status) | t }}
                  </span>
                </td>
                <td class="px-5 py-4 text-sm font-black text-amber-600 dark:text-amber-400 text-right">
                  {{ ret.totalAmountBdt | money:2 }}
                </td>
                <td class="px-5 py-4 text-sm text-right space-x-1.5 whitespace-nowrap">
                  <!-- Complete Draft Action -->
                  <button *ngIf="ret.status === PurchaseReturnStatus.Draft" (click)="onCompleteReturn(ret)"
                    [title]="'Complete return and deduct stock' | t"
                    class="px-2.5 py-1 text-xs font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1">
                    <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">check</mat-icon> {{ 'Complete' | t }}
                  </button>

                  <!-- Cancel Draft Action -->
                  <button *ngIf="ret.status === PurchaseReturnStatus.Draft" (click)="onCancelReturn(ret)"
                    [title]="'Cancel draft return' | t"
                    class="px-2.5 py-1 text-xs font-semibold text-red-700 bg-red-50 hover:bg-red-100 border border-red-200 rounded-lg transition-colors inline-flex items-center gap-1">
                    <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">close</mat-icon> {{ 'Cancel' | t }}
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <!-- Pagination Footer -->
      <div *ngIf="result() && result()!.totalPages > 1" class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 flex items-center justify-between bg-gray-50/50 dark:bg-gray-900/30">
        <span class="text-xs text-gray-500">
          {{ 'Showing page {page} of {pages} ({n} returns)' | t: { page: result()!.pageNumber, pages: result()!.totalPages, n: result()!.totalCount } }}
        </span>
        <div class="flex items-center gap-2">
          <button (click)="onPageChange(result()!.pageNumber - 1)" [disabled]="!result()!.hasPreviousPage"
            class="px-3 py-1 text-xs font-semibold rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 disabled:opacity-50 hover:bg-gray-50">
            {{ 'Previous' | t }}
          </button>
          <button (click)="onPageChange(result()!.pageNumber + 1)" [disabled]="!result()!.hasNextPage"
            class="px-3 py-1 text-xs font-semibold rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 disabled:opacity-50 hover:bg-gray-50">
            {{ 'Next' | t }}
          </button>
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PurchaseReturnListComponent {
  private readonly notify = inject(NotificationService);
  private readonly inventoryService = inject(InventoryService);
  private readonly workingContext = inject(WorkingContextService);
  readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(I18nService);
  readonly emptyDescription = "To return items to a supplier, open a fulfilled purchase order and click 'Return Items'.";

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  retryLoad(): void {
    this.error.set(null);
    this.refreshTrigger.update(n => n + 1);
  }
  readonly searchTerm = signal('');
  readonly statusFilter = signal<PurchaseReturnStatus | null>(null);
  readonly pageNumber = signal(1);
  private readonly refreshTrigger = signal(0);

  readonly PurchaseReturnStatus = PurchaseReturnStatus;

  readonly currentFarmId = toSignal(
    this.workingContext.currentFarm$.pipe(switchMap(f => of(f?.id || null))),
    { initialValue: this.workingContext.currentFarmValue?.id || null }
  );

  private readonly queryParams = computed<PurchaseReturnParams>(() => ({
    pageNumber: this.pageNumber(),
    pageSize: 20,
    farmId: this.currentFarmId() || undefined,
    status: this.statusFilter() ?? undefined,
    search: this.searchTerm().trim() || undefined,
    sortBy: 'returndate',
    sortDesc: true
  }));

  private readonly fetchTrigger = computed(() => ({
    params: this.queryParams(),
    refresh: this.refreshTrigger()
  }));

  readonly result = toSignal(
    toObservable(this.fetchTrigger).pipe(
      debounceTime(150),
      switchMap(({ params }) => {
        this.loading.set(true);
        this.error.set(null);
        return this.inventoryService.getPurchaseReturns(params).pipe(
          catchError(err => {
            this.error.set(parseApiError(err, this.i18n.t('Could not load purchase returns.')));
            return of(null);
          })
        );
      }),
      switchMap(res => {
        this.loading.set(false);
        return of(res);
      })
    )
  );

  onSearchChange(term: string): void {
    this.searchTerm.set(term);
    this.pageNumber.set(1);
  }

  onStatusChange(status: PurchaseReturnStatus | null): void {
    this.statusFilter.set(status);
    this.pageNumber.set(1);
  }

  onPageChange(page: number): void {
    this.pageNumber.set(page);
  }

  refresh(): void {
    this.refreshTrigger.update(n => n + 1);
  }

  getStatusName(status: PurchaseReturnStatus): string {
    return PurchaseReturnStatusNames[status] || status;
  }

  getReasonName(reason: string): string {
    return (PurchaseReturnReasonNames as Record<string, string>)[reason] || reason;
  }

  onCompleteReturn(ret: PurchaseReturn): void {
    // Confirms, and asks which money account the supplier's cash refund goes into.
    const dialogRef = this.dialog.open(CompleteReturnDialogComponent, {
      width: '520px', maxWidth: '95vw',
      data: { returnNumber: ret.returnNumber, itemCount: ret.items.length, totalAmountBdt: ret.totalAmountBdt },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result?: CompleteReturnDialogResult) => {
        if (!result) return;
        this.loading.set(true);
        this.inventoryService.completePurchaseReturn(ret.id, result.moneyAccountId)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({
            next: () => this.refresh(),
            error: (err) => {
              this.loading.set(false);
              this.notify.error(err?.error?.detail || err?.message || this.i18n.t('Failed to complete return.'));
            }
          });
      });
  }

  onCancelReturn(ret: PurchaseReturn): void {
    const reason = prompt(this.i18n.t('Enter a reason for cancelling this return:'), this.i18n.t('Refused by supplier'));
    if (!reason) return;

    this.loading.set(true);
    this.inventoryService.cancelPurchaseReturn(ret.id, reason)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.refresh(),
        error: (err) => {
          this.loading.set(false);
          this.notify.error(err?.error?.detail || err?.message || this.i18n.t('Failed to cancel return.'));
        }
      });
  }
}
