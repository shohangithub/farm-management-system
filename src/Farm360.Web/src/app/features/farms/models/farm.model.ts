export interface Farm {
  id: string;
  branchId: string;
  farmCode: string;
  farmName: string;
  type: number; // enum FarmType
  farmSize?: number;
  landArea?: number;
  latitude?: number;
  longitude?: number;
  mapPolygon?: string;
  capacity?: number;
  currentAnimalCount: number;
  ownerId?: string;
  managerId?: string;
  status: number; // enum FarmStatus
  description?: string;
}

export interface FarmList {
  id: string;
  farmCode: string;
  farmName: string;
  type: number;
  currentAnimalCount: number;
  capacity?: number;
  status: number;
}

export interface CreateFarmCommand {
  branchId: string;
  farmCode: string;
  farmName: string;
  type: number;
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
  status: number;
}
