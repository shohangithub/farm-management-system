export enum InventoryCategory {
  Feed = 'Feed',
  Medicine = 'Medicine',
  Vaccine = 'Vaccine',
  Chemical = 'Chemical',
  Equipment = 'Equipment',
  Other = 'Other'
}

export const InventoryCategoryNames: Record<InventoryCategory, string> = {
  [InventoryCategory.Feed]: 'Feed',
  [InventoryCategory.Medicine]: 'Medicine',
  [InventoryCategory.Vaccine]: 'Vaccine',
  [InventoryCategory.Chemical]: 'Chemical',
  [InventoryCategory.Equipment]: 'Equipment',
  [InventoryCategory.Other]: 'Other'
};

export enum StockTransactionType {
  StockIn = 'StockIn',
  ManualStockOut = 'ManualStockOut',
  AutoFeedConsumption = 'AutoFeedConsumption',
  AutoMedicineConsumption = 'AutoMedicineConsumption',
  Adjustment = 'Adjustment',
  WriteOff = 'WriteOff'
}

export const StockTransactionTypeNames: Record<StockTransactionType, string> = {
  [StockTransactionType.StockIn]: 'Stock In',
  [StockTransactionType.ManualStockOut]: 'Manual Stock Out',
  [StockTransactionType.AutoFeedConsumption]: 'Auto Feed Deduction',
  [StockTransactionType.AutoMedicineConsumption]: 'Auto Medicine Deduction',
  [StockTransactionType.Adjustment]: 'Adjustment',
  [StockTransactionType.WriteOff]: 'Write-Off'
};

export enum InventoryStatus {
  Sufficient = 'Sufficient',
  LowStock = 'LowStock',
  OutOfStock = 'OutOfStock',
  Excess = 'Excess'
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface InventoryItemParams {
  pageNumber?: number;
  pageSize?: number;
  farmId?: string;
  category?: InventoryCategory;
  status?: InventoryStatus;
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

export interface StockTransactionParams {
  pageNumber?: number;
  pageSize?: number;
  farmId?: string;
  inventoryItemId?: string;
  transactionType?: StockTransactionType;
  fromDate?: string;
  toDate?: string;
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

export interface SupplierParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

export interface InventoryItem {
  id: string;
  farmId: string;
  name: string;
  sku: string;
  category: InventoryCategory;
  categoryName: string;
  unitOfMeasure: string;
  reorderThreshold: number;
  currentStock: number;
  weightedAverageCostBdt: number;
  totalValueBdt: number;
  status: InventoryStatus;
  statusName: string;
  storageLocation?: string;
  isActive: boolean;
}

export interface Supplier {
  id: string;
  name: string;
  contactPerson?: string;
  phone?: string;
  email?: string;
  address?: string;
  notes?: string;
  isActive: boolean;
}

export interface StockTransaction {
  id: string;
  farmId: string;
  inventoryItemId: string;
  itemName: string;
  transactionType: StockTransactionType;
  transactionTypeName: string;
  quantity: number;
  unitCostBdt: number;
  totalCostBdt: number;
  balanceAfter: number;
  transactionDate: string;
  supplierId?: string;
  supplierName?: string;
  invoiceNumber?: string;
  batchNumber?: string;
  expiryDate?: string;
  reason?: string;
  recordedBy?: string;
}

export interface InventoryValuationReport {
  farmId: string;
  totalValuationBdt: number;
  totalSkusCount: number;
  lowStockCount: number;
  outOfStockCount: number;
  items: InventoryItem[];
}

export interface CurrentStockSummary {
  totalItems: number;
  totalValueBdt: number;
  lowStockCount: number;
  outOfStockCount: number;
}

export interface CreateInventoryItemRequest {
  farmId: string;
  name: string;
  category: InventoryCategory;
  unitOfMeasure: string;
  reorderThreshold: number;
  sku?: string;
  initialStock?: number;
  initialCostBdt?: number;
  storageLocation?: string;
}

export interface RecordStockInRequest {
  farmId: string;
  inventoryItemId: string;
  quantity: number;
  unitCostBdt: number;
  transactionDate: string;
  supplierId?: string;
  invoiceNumber?: string;
  batchNumber?: string;
  expiryDate?: string;
  notes?: string;
}

export interface RecordStockOutRequest {
  farmId: string;
  inventoryItemId: string;
  quantity: number;
  transactionType: StockTransactionType;
  transactionDate: string;
  reason?: string;
  referenceId?: string;
}

export interface CreateSupplierRequest {
  name: string;
  contactPerson?: string;
  phone?: string;
  email?: string;
  address?: string;
  notes?: string;
}

export enum PurchaseOrderStatus {
  Draft = 'Draft',
  PendingApproval = 'PendingApproval',
  Approved = 'Approved',
  Fulfilled = 'Fulfilled',
  Cancelled = 'Cancelled'
}

export interface PurchaseOrderItem {
  id: string;
  inventoryItemId: string;
  quantity: number;
  unitCostBdt: number;
  totalCostBdt: number;
}

export interface PurchaseOrder {
  id: string;
  farmId: string;
  poNumber: string;
  supplierId: string;
  status: PurchaseOrderStatus;
  orderDate: string;
  expectedDeliveryDate?: string;
  notes?: string;
  totalAmountBdt: number;
  items: PurchaseOrderItem[];
}

export interface PurchaseOrderItemDto {
  inventoryItemId: string;
  quantity: number;
  unitCostBdt: number;
}

export interface CreatePurchaseOrderRequest {
  farmId: string;
  supplierId: string;
  orderDate: string;
  expectedDeliveryDate?: string;
  notes?: string;
  items: PurchaseOrderItemDto[];
}

export interface PurchaseOrderParams {
  pageNumber?: number;
  pageSize?: number;
  farmId?: string;
  supplierId?: string;
  status?: PurchaseOrderStatus;
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

