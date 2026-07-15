export enum VaccinationStatus {
  Scheduled = 'Scheduled',
  Administered = 'Administered',
  Missed = 'Missed',
  Cancelled = 'Cancelled'
}

export enum TreatmentStatus {
  Ongoing = 'Ongoing',
  Completed = 'Completed',
  Discontinued = 'Discontinued'
}

export enum IncidentSeverity {
  Mild = 'Mild',
  Moderate = 'Moderate',
  Severe = 'Severe',
  Critical = 'Critical'
}

export enum IncidentStatus {
  Reported = 'Reported',
  UnderTreatment = 'UnderTreatment',
  Resolved = 'Resolved'
}

export interface VaccinationEventDto {
  id: string;
  animalId: string;
  vaccineName: string;
  batchNumber: string;
  scheduledDate: string;
  administeredDate?: string;
  status: VaccinationStatus;
  notes?: string;
}

export interface MedicalTreatmentDto {
  id: string;
  animalId: string;
  diagnosis: string;
  medicationName: string;
  dosageAmount: number;
  dosageUnit: string;
  milkWithdrawalDays: number;
  meatWithdrawalDays: number;
  startDate: string;
  endDate?: string;
  costBdt: number;
  veterinarianName?: string;
  status: TreatmentStatus;
  notes?: string;
}

export interface DiseaseIncidentDto {
  id: string;
  farmId: string;
  shedId?: string;
  diseaseName: string;
  severity: IncidentSeverity;
  incidentDate: string;
  symptoms: string;
  affectedAnimalCount: number;
  status: IncidentStatus;
  notes?: string;
}

export interface AnimalHealthHistoryDto {
  vaccinations: VaccinationEventDto[];
  treatments: MedicalTreatmentDto[];
}
