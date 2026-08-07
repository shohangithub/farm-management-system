export interface Farm {
  id: string;
  branchId: string;
  farmCode: string;
  farmName: string;
  type: string; // enum FarmType
  farmSize?: number;
  landArea?: number;
  latitude?: number;
  longitude?: number;
  mapPolygon?: string;
  capacity?: number;
  currentAnimalCount: number;
  ownerId?: string;
  managerId?: string;
  status: string; // enum FarmStatus
  description?: string;
  createdAtUtc?: string;
  createdBy?: string;
  modifiedAtUtc?: string;
  modifiedBy?: string;
}

export interface FarmList {
  id: string;
  farmCode: string;
  farmName: string;
  type: string;
  currentAnimalCount: number;
  capacity?: number;
  status: string;
}

export interface CreateFarmCommand {
  branchId: string;
  farmCode: string;
  farmName: string;
  type: string;
  farmSize?: number;
  landArea?: number;
  latitude?: number;
  longitude?: number;
  mapPolygon?: string;
  capacity?: number;
  ownerId?: string;
  managerId?: string;
  description?: string;
}

export interface UpdateFarmCommand extends CreateFarmCommand {
  id: string;
  status: string;
}
