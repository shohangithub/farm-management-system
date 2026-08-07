export interface Shed {
  id: string;
  farmId: string;
  shedNumber: string;
  shedName: string;
  capacity?: number;
  currentOccupancy: number;
  animalType?: string;
  floorType?: string;
  roofType?: string;
  hasVentilation: boolean;
  hasWaterLine: boolean;
  hasFeedLine: boolean;
  status: string; // enum ShedStatus
  createdAtUtc?: string;
  createdBy?: string;
  modifiedAtUtc?: string;
  modifiedBy?: string;
}

export interface ShedList {
  id: string;
  shedNumber: string;
  shedName: string;
  capacity?: number;
  currentOccupancy: number;
  animalType?: string;
  status: string;
}

export interface CreateShedCommand {
  farmId: string;
  shedNumber: string;
  shedName: string;
  capacity?: number;
  animalType?: string;
  floorType?: string;
  roofType?: string;
  hasVentilation: boolean;
  hasWaterLine: boolean;
  hasFeedLine: boolean;
}

export interface UpdateShedCommand extends CreateShedCommand {
  id: string;
  status: string;
}
