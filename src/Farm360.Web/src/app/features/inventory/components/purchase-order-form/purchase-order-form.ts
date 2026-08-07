import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators, FormArray } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { toObservable, toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, of } from 'rxjs';
import { catchError, tap, switchMap, filter } from 'rxjs/operators';

import { InventoryService } from '../../services/inventory.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { CreatePurchaseOrderRequest, InventoryItem, Supplier } from '../../models/inventory.models';

import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';

@Component({
  selector: 'app-purchase-order-form',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    PageHeaderComponent,
    LoadingComponent
  ],
  template: `
    <app-page-header
      title="Create Purchase Order"
      description="Create a new purchase order to request items from a supplier."
      breadcrumbActiveNode="Create PO">
      <div actions class="flex items-center gap-3">
        <button [routerLink]="['/inventory/purchase-orders']"
          class="px-4 py-2 text-sm font-semibold text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">close</mat-icon> Cancel
        </button>
        <button (click)="onSubmit()" [disabled]="form.invalid || loading() || items.length === 0"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5 disabled:opacity-50 disabled:cursor-not-allowed">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">save</mat-icon> Submit PO
        </button>
      </div>
    </app-page-header>

    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
      <app-loading *ngIf="loading() || initLoading()" [overlay]="true"></app-loading>

      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="p-6">
        
        <!-- Error Banner -->
        <div *ngIf="error()" class="mb-6 p-4 rounded-xl bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-800/50 flex items-start gap-3 text-red-800 dark:text-red-200">
          <mat-icon class="mt-0.5">error_outline</mat-icon>
          <div class="text-sm font-medium">{{ error() }}</div>
        </div>

        <!-- Master Data Section -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
          <div>
            <label class="block text-sm font-bold text-gray-700 dark:text-gray-300 mb-1.5">Supplier <span class="text-red-500">*</span></label>
            <select formControlName="supplierId"
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
              <option value="" disabled selected>Select a supplier</option>
              <option *ngFor="let s of suppliers()" [value]="s.id">{{ s.name }}</option>
            </select>
          </div>
          
          <div>
            <label class="block text-sm font-bold text-gray-700 dark:text-gray-300 mb-1.5">Order Date <span class="text-red-500">*</span></label>
            <input type="date" formControlName="orderDate"
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all" />
          </div>

          <div>
            <label class="block text-sm font-bold text-gray-700 dark:text-gray-300 mb-1.5">Expected Delivery Date</label>
            <input type="date" formControlName="expectedDeliveryDate"
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all" />
          </div>

          <div class="md:col-span-2">
            <label class="block text-sm font-bold text-gray-700 dark:text-gray-300 mb-1.5">Notes / Terms</label>
            <textarea formControlName="notes" rows="3" placeholder="Additional details..."
              class="w-full px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all resize-none"></textarea>
          </div>
        </div>

        <!-- Line Items Section -->
        <div>
          <div class="flex items-center justify-between mb-4">
            <h3 class="text-lg font-bold text-gray-900 dark:text-white">Order Items <span class="text-red-500">*</span></h3>
            <button type="button" (click)="addItem()"
              class="px-3 py-1.5 text-xs font-semibold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 hover:bg-emerald-100 dark:hover:bg-emerald-900/50 rounded-lg border border-emerald-200 dark:border-emerald-800 transition-colors inline-flex items-center gap-1">
              <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">add</mat-icon> Add Row
            </button>
          </div>

          <div class="overflow-x-auto rounded-xl border border-gray-200 dark:border-gray-700">
            <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
              <thead class="bg-gray-50 dark:bg-gray-800/50">
                <tr>
                  <th scope="col" class="px-4 py-3 text-left text-xs font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider w-[40%]">Item</th>
                  <th scope="col" class="px-4 py-3 text-right text-xs font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Quantity</th>
                  <th scope="col" class="px-4 py-3 text-right text-xs font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Unit Cost (৳)</th>
                  <th scope="col" class="px-4 py-3 text-right text-xs font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Total (৳)</th>
                  <th scope="col" class="px-4 py-3 text-center text-xs font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider w-[60px]">Action</th>
                </tr>
              </thead>
              <tbody class="bg-white dark:bg-gray-900 divide-y divide-gray-200 dark:divide-gray-700" formArrayName="items">
                <tr *ngFor="let itemCtrl of items.controls; let i = index" [formGroupName]="i">
                  <td class="px-4 py-3">
                    <select formControlName="inventoryItemId"
                      class="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all">
                      <option value="" disabled selected>Select an item...</option>
                      <option *ngFor="let inv of inventoryItems()" [value]="inv.id">{{ inv.name }} ({{ inv.unitOfMeasure }})</option>
                    </select>
                  </td>
                  <td class="px-4 py-3">
                    <input type="number" formControlName="quantity" min="0" step="0.01"
                      class="w-full text-right px-3 py-2 text-sm rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all" />
                  </td>
                  <td class="px-4 py-3">
                    <input type="number" formControlName="unitCostBdt" min="0" step="0.01"
                      class="w-full text-right px-3 py-2 text-sm rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all" />
                  </td>
                  <td class="px-4 py-3 text-right text-sm font-bold text-gray-900 dark:text-white align-middle">
                    {{ calculateRowTotal(i) | number:'1.2-2' }}
                  </td>
                  <td class="px-4 py-3 text-center align-middle">
                    <button type="button" (click)="removeItem(i)" class="text-gray-400 hover:text-red-500 transition-colors p-1 rounded hover:bg-red-50 dark:hover:bg-red-900/30">
                      <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">delete</mat-icon>
                    </button>
                  </td>
                </tr>
                <tr *ngIf="items.length === 0">
                  <td colspan="5" class="px-4 py-8 text-center text-sm text-gray-500 dark:text-gray-400">
                    No items added yet. Click "Add Row" to request items.
                  </td>
                </tr>
              </tbody>
              <tfoot *ngIf="items.length > 0" class="bg-gray-50 dark:bg-gray-800/50">
                <tr>
                  <td colspan="3" class="px-4 py-3 text-right text-sm font-bold text-gray-700 dark:text-gray-300 uppercase">Grand Total:</td>
                  <td class="px-4 py-3 text-right text-base font-black text-emerald-600 dark:text-emerald-400">৳ {{ grandTotal() | number:'1.2-2' }}</td>
                  <td></td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
      </form>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PurchaseOrderForm implements OnInit {
  private readonly inventoryService = inject(InventoryService);
  private readonly contextService = inject(WorkingContextService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly initLoading = signal(true);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly inventoryItems = signal<InventoryItem[]>([]);
  readonly suppliers = signal<Supplier[]>([]);

  readonly form = this.fb.group({
    supplierId: ['', Validators.required],
    orderDate: [new Date().toISOString().split('T')[0], Validators.required],
    expectedDeliveryDate: [''],
    notes: [''],
    items: this.fb.array([])
  });

  get items(): FormArray {
    return this.form.get('items') as FormArray;
  }

  // Use a signal created from form valueChanges to automatically trigger re-evaluations
  readonly formValueSignal = toSignal(this.form.valueChanges, { initialValue: this.form.value });

  // Compute totals dynamically without needing manual change detection hooks
  readonly grandTotal = computed(() => {
    const vals = this.formValueSignal();
    if (!vals || !vals.items) return 0;
    return vals.items.reduce((sum: number, item: any) => {
      const q = parseFloat(item.quantity || '0');
      const c = parseFloat(item.unitCostBdt || '0');
      return sum + (q * c);
    }, 0);
  });

  ngOnInit(): void {
    // 1. We must fetch suppliers and inventory items for dropdowns upon init.
    // Use the current working farm context to filter items.
    this.contextService.currentFarm$.pipe(
      takeUntilDestroyed(),
      filter(f => !!f),
      switchMap(farm => {
        this.initLoading.set(true);
        // Fork join parallel fetch (using rule 5)
        return forkJoin({
          items: this.inventoryService.getItems({ farmId: farm!.id, pageSize: 1000 }), // large page size for dropdown
          suppliers: this.inventoryService.getSuppliers({ pageSize: 1000 })
        }).pipe(
          catchError(err => {
            this.error.set('Failed to load master data for the form.');
            return of(null);
          })
        );
      })
    ).subscribe(data => {
      this.initLoading.set(false);
      if (data) {
        this.inventoryItems.set(data.items.items);
        this.suppliers.set(data.suppliers.items);
        if (this.items.length === 0) {
          this.addItem(); // add an initial row empty
        }
      }
    });
  }

  addItem(): void {
    const itemGroup = this.fb.group({
      inventoryItemId: ['', Validators.required],
      quantity: [1, [Validators.required, Validators.min(0.01)]],
      unitCostBdt: [0, [Validators.required, Validators.min(0)]]
    });

    // When an item is selected, optionally set its default cost (based on weighted avg)
    itemGroup.get('inventoryItemId')?.valueChanges.pipe(takeUntilDestroyed()).subscribe(id => {
      const item = this.inventoryItems().find(i => i.id === id);
      if (item && item.weightedAverageCostBdt) {
        // Only set cost if the cost is currently 0 to avoid overwriting user entry
        if (itemGroup.get('unitCostBdt')?.value === 0) {
          itemGroup.get('unitCostBdt')?.setValue(item.weightedAverageCostBdt, { emitEvent: false });
        }
      }
    });

    this.items.push(itemGroup);
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  calculateRowTotal(index: number): number {
    const vals = this.formValueSignal();
    const row = vals?.items?.[index] as any;
    if (!row) return 0;
    const q = parseFloat(row.quantity || '0');
    const c = parseFloat(row.unitCostBdt || '0');
    return q * c;
  }

  onSubmit(): void {
    if (this.form.invalid || this.items.length === 0) {
      return;
    }

    const farmId = this.contextService.currentFarmValue?.id;
    if (!farmId) {
      this.error.set('No active farm context selected.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    const val = this.form.value;
    
    // Sanitize empty strings to null for backend correctly handling BadHttpRequestException
    const payload: CreatePurchaseOrderRequest = {
      farmId: farmId,
      supplierId: val.supplierId!,
      orderDate: val.orderDate!,
      expectedDeliveryDate: val.expectedDeliveryDate === "" ? undefined : (val.expectedDeliveryDate || undefined),
      notes: val.notes === "" ? undefined : (val.notes || undefined),
      items: (val.items || []).map((i: any) => ({
        inventoryItemId: i.inventoryItemId,
        quantity: i.quantity,
        unitCostBdt: i.unitCostBdt
      }))
    };

    this.inventoryService.createPurchaseOrder(payload).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.router.navigate(['/inventory/purchase-orders', res.id]);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.detail || 'Failed to create purchase order.');
      }
    });
  }
}
