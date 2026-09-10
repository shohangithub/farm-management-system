import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  InventoryItem,
  CreateInventoryItemRequest,
  Supplier,
  CreateSupplierRequest,
  StockTransaction,
  RecordStockInRequest,
  RecordStockOutRequest,
  InventoryValuationReport,
  CurrentStockSummary,
  PagedResult,
  InventoryItemParams,
  StockTransactionParams,
  SupplierParams,
  PurchaseOrder,
  CreatePurchaseOrderRequest,
  PurchaseOrderParams,
  ConsumableUsagePlan,
  ConsumableUsagePlanStatus,
  DailyConsumableEntry,
  DailyConsumableSummary,
  CreateConsumableUsagePlanRequest,
  UpdateConsumableUsagePlanRequest,
  ConfirmDailyConsumableEntryRequest,
  BulkConfirmDailyConsumablesRequest,
  SkipDailyConsumableEntryRequest
} from '../models/inventory.models';

@Injectable({
  providedIn: 'root'
})
export class InventoryService {
  private readonly http: HttpClient = inject(HttpClient);
  private readonly baseUrl = '/api/v1/inventory';

  // ── Inventory Items ────────────────────────────────────────────────────────
  getItems(params: InventoryItemParams = {}): Observable<PagedResult<InventoryItem>> {
    let httpParams = new HttpParams();
    if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize)   httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.farmId)     httpParams = httpParams.set('farmId', params.farmId);
    if (params.category != null) httpParams = httpParams.set('category', params.category);
    if (params.status != null)   httpParams = httpParams.set('status', params.status);
    if (params.search)     httpParams = httpParams.set('search', params.search);
    if (params.sortBy)     httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDesc != null) httpParams = httpParams.set('sortDesc', params.sortDesc);

    return this.http.get<PagedResult<InventoryItem>>(`${this.baseUrl}/items`, { params: httpParams });
  }

  getItemById(id: string): Observable<InventoryItem> {
    return this.http.get<InventoryItem>(`${this.baseUrl}/items/${id}`);
  }

  createItem(request: CreateInventoryItemRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/items`, request);
  }

  updateItem(id: string, request: CreateInventoryItemRequest & { isActive?: boolean }): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/items/${id}`, { id, ...request });
  }

  deleteItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/items/${id}`);
  }

  // ── Stock Transactions ──────────────────────────────────────────────────────
  recordStockIn(request: RecordStockInRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/transactions/stock-in`, request);
  }

  recordStockOut(request: RecordStockOutRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/transactions/stock-out`, request);
  }

  recordStockWriteOff(request: any): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/transactions/write-off`, request);
  }

  getTransactions(params: StockTransactionParams = {}): Observable<PagedResult<StockTransaction>> {
    let httpParams = new HttpParams();
    if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize)   httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.farmId)     httpParams = httpParams.set('farmId', params.farmId);
    if (params.inventoryItemId) httpParams = httpParams.set('inventoryItemId', params.inventoryItemId);
    if (params.transactionType != null) httpParams = httpParams.set('transactionType', params.transactionType);
    if (params.fromDate)   httpParams = httpParams.set('fromDate', params.fromDate);
    if (params.toDate)     httpParams = httpParams.set('toDate', params.toDate);
    if (params.search)     httpParams = httpParams.set('search', params.search);
    if (params.sortBy)     httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDesc != null) httpParams = httpParams.set('sortDesc', params.sortDesc);

    return this.http.get<PagedResult<StockTransaction>>(`${this.baseUrl}/transactions`, { params: httpParams });
  }

  // ── Suppliers ───────────────────────────────────────────────────────────────
  getSuppliers(params: SupplierParams = {}): Observable<PagedResult<Supplier>> {
    let httpParams = new HttpParams();
    if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize)   httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.search)     httpParams = httpParams.set('search', params.search);
    if (params.sortBy)     httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDesc != null) httpParams = httpParams.set('sortDesc', params.sortDesc);

    return this.http.get<PagedResult<Supplier>>(`${this.baseUrl}/suppliers`, { params: httpParams });
  }

  createSupplier(request: CreateSupplierRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/suppliers`, request);
  }

  updateSupplier(id: string, request: CreateSupplierRequest & { isActive?: boolean }): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/suppliers/${id}`, { id, ...request });
  }

  deleteSupplier(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/suppliers/${id}`);
  }

  // ── Reports ────────────────────────────────────────────────────────────────
  getValuationReport(farmId: string): Observable<InventoryValuationReport> {
    return this.http.get<InventoryValuationReport>(`${this.baseUrl}/reports/valuation`, {
      params: new HttpParams().set('farmId', farmId)
    });
  }

  getCurrentStockSummary(farmId: string): Observable<CurrentStockSummary> {
    return this.http.get<CurrentStockSummary>(`${this.baseUrl}/reports/current-stock/summary`, {
      params: new HttpParams().set('farmId', farmId)
    });
  }

  getMovementReport(farmId: string, startDate: string, endDate: string): Observable<any> {
    let params = new HttpParams()
      .set('farmId', farmId)
      .set('startDate', startDate)
      .set('endDate', endDate);
    return this.http.get<any>(`${this.baseUrl}/reports/movement`, { params });
  }

  getExpiringItems(farmId: string, daysThreshold: number = 30): Observable<any[]> {
    let params = new HttpParams()
      .set('farmId', farmId)
      .set('daysThreshold', daysThreshold);
    return this.http.get<any[]>(`${this.baseUrl}/reports/expiring`, { params });
  }

  // ── Purchase Orders ─────────────────────────────────────────────────────────
  getPurchaseOrders(params: PurchaseOrderParams = {}): Observable<PagedResult<PurchaseOrder>> {
    let httpParams = new HttpParams();
    if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params.pageSize)   httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.farmId)     httpParams = httpParams.set('farmId', params.farmId);
    if (params.supplierId) httpParams = httpParams.set('supplierId', params.supplierId);
    if (params.status != null) httpParams = httpParams.set('status', params.status);
    if (params.search)     httpParams = httpParams.set('search', params.search);
    if (params.sortBy)     httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDesc != null) httpParams = httpParams.set('sortDesc', params.sortDesc);

    return this.http.get<PagedResult<PurchaseOrder>>(`${this.baseUrl}/purchase-orders`, { params: httpParams });
  }

  getPurchaseOrderById(id: string): Observable<PurchaseOrder> {
    return this.http.get<PurchaseOrder>(`${this.baseUrl}/purchase-orders/${id}`);
  }

  createPurchaseOrder(request: CreatePurchaseOrderRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/purchase-orders`, request);
  }

  approvePurchaseOrder(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/purchase-orders/${id}/approve`, {});
  }

  fulfillPurchaseOrder(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/purchase-orders/${id}/fulfill`, {});
  }

  // ── Consumable Usage Plans ──────────────────────────────────────────────────
  getConsumablePlans(
    farmId: string,
    pageNumber: number = 1,
    pageSize: number = 20,
    status?: ConsumableUsagePlanStatus,
    search?: string
  ): Observable<PagedResult<ConsumableUsagePlan>> {
    let params = new HttpParams()
      .set('farmId', farmId)
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (status) params = params.set('status', status);
    if (search) params = params.set('search', search);

    return this.http.get<PagedResult<ConsumableUsagePlan>>(`${this.baseUrl}/consumable-plans`, { params });
  }

  getConsumablePlanById(id: string): Observable<ConsumableUsagePlan> {
    return this.http.get<ConsumableUsagePlan>(`${this.baseUrl}/consumable-plans/${id}`);
  }

  createConsumablePlan(request: CreateConsumableUsagePlanRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/consumable-plans`, request);
  }

  updateConsumablePlan(id: string, request: UpdateConsumableUsagePlanRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/consumable-plans/${id}`, request);
  }

  deleteConsumablePlan(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/consumable-plans/${id}`);
  }

  // ── Daily Consumables Workflow ──────────────────────────────────────────────
  getDailyConsumables(farmId: string, date?: string): Observable<DailyConsumableEntry[]> {
    let params = new HttpParams().set('farmId', farmId);
    if (date) params = params.set('date', date);

    return this.http.get<DailyConsumableEntry[]>(`${this.baseUrl}/daily-consumables`, { params });
  }

  getDailyConsumableSummary(farmId: string, date?: string): Observable<DailyConsumableSummary> {
    let params = new HttpParams().set('farmId', farmId);
    if (date) params = params.set('date', date);

    return this.http.get<DailyConsumableSummary>(`${this.baseUrl}/daily-consumables/summary`, { params });
  }

  generateDailyConsumables(farmId: string, targetDate?: string): Observable<{ generatedCount: number }> {
    return this.http.post<{ generatedCount: number }>(`${this.baseUrl}/daily-consumables/generate`, {
      farmId,
      targetDate
    });
  }

  confirmDailyConsumable(request: ConfirmDailyConsumableEntryRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/daily-consumables/${request.entryId}/confirm`, request);
  }

  bulkConfirmDailyConsumables(request: BulkConfirmDailyConsumablesRequest): Observable<{ confirmedCount: number }> {
    return this.http.post<{ confirmedCount: number }>(`${this.baseUrl}/daily-consumables/bulk-confirm`, request);
  }

  skipDailyConsumable(request: SkipDailyConsumableEntryRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/daily-consumables/${request.entryId}/skip`, request);
  }
}
