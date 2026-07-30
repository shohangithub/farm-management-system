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
  PagedResult,
  InventoryItemParams,
  StockTransactionParams,
  SupplierParams
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
}
