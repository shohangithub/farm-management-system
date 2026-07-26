export enum BatchStatus {
  Active = 'Active',
  Archived = 'Archived'
}

export interface BatchDto {
  id: string;
  tenantId: string;
  farmId: string;
  name: string;
  status: BatchStatus;
  notes?: string;
  animalCount: number;
  createdAtUtc: string;
}

export interface PagedBatchListDto {
  items: BatchDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface CreateBatchRequest {
  farmId: string;
  name: string;
  notes?: string;
}
