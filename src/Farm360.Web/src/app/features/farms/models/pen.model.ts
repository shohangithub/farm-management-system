export interface Pen {
  id: string;
  shedId: string;
  penNumber: string;
  penName: string;
  capacity: number;
  currentOccupancy: number;
  animalGroup?: string;
  notes?: string;
  status: string; // enum PenStatus
  createdAtUtc?: string;
  createdBy?: string;
  modifiedAtUtc?: string;
  modifiedBy?: string;
}

export interface PenList {
  id: string;
  penNumber: string;
  penName: string;
  capacity: number;
  currentOccupancy: number;
  animalGroup?: string;
  status: string;
}

export interface CreatePenCommand {
  shedId: string;
  penNumber: string;
  penName: string;
  capacity: number;
  animalGroup?: string;
  notes?: string;
}

export interface UpdatePenCommand {
  id: string;
  penName: string;
  capacity: number;
  animalGroup?: string;
  notes?: string;
  status: string;
}
