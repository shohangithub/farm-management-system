export interface Pen {
  id: string;
  shedId: string;
  penNumber: string;
  penName: string;
  capacity: number;
  currentOccupancy: number;
  animalGroup?: string;
  notes?: string;
  status: number; // enum PenStatus
}

export interface PenList {
  id: string;
  penNumber: string;
  penName: string;
  capacity: number;
  currentOccupancy: number;
  animalGroup?: string;
  status: number;
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
  status: number;
}
