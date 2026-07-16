export enum MasterDataType {
  Breed = 1,
  AnimalType = 2,
  FeedType = 3,
  MedicineType = 4,
  VaccinationType = 5,
  Disease = 6,
  SupplierCategory = 7,
  ExpenseCategory = 8,
  PaymentMethod = 9,
  MeasurementUnit = 10,
  Currency = 11,
  Language = 12,
  Timezone = 13,
  BusinessType = 14
}

export interface MasterDataEntry {
  id: string;
  type: number;
  name: string;
  code: string;
  description?: string;
  displayOrder: number;
  isActive: boolean;
}

export interface CreateMasterDataCommand {
  type: number;
  name: string;
  code: string;
  description?: string;
  displayOrder: number;
}

export interface UpdateMasterDataCommand {
  id: string;
  name: string;
  description?: string;
  displayOrder: number;
  isActive: boolean;
}
