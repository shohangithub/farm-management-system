export enum VaccinationStatus {
  Scheduled = 'Scheduled',
  Administered = 'Administered',
  Missed = 'Missed',
  Cancelled = 'Cancelled'
}

export enum TreatmentStatus {
  Ongoing = 'Ongoing',
  Completed = 'Completed',
  Failed = 'Failed',
  Discontinued = 'Discontinued'
}

export enum IncidentSeverity {
  Mild = 1,
  Moderate = 2,
  Severe = 3,
  Critical = 4
}

export enum IncidentStatus {
  Reported = 1,
  UnderTreatment = 2,
  Contained = 3,
  Resolved = 4
}

export enum CauseOfDeath {
  Disease = 1,
  Accident = 2,
  NaturalCauses = 3,
  Unknown = 4,
  Slaughter = 5
}

export enum VetVisitType {
  RoutineCheckup = 'RoutineCheckup',
  Emergency = 'Emergency',
  VaccinationDrive = 'VaccinationDrive',
  Surgery = 'Surgery'
}

export enum AnimalSpecies {
  Cattle = 'Cattle',
  Sheep = 'Sheep',
  Goat = 'Goat',
  Poultry = 'Poultry'
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

export interface VaccinationProtocolParams {
  pageNumber?: number;
  pageSize?: number;
  farmId?: string;
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

export interface MedicalTreatmentParams {
  pageNumber?: number;
  pageSize?: number;
  farmId?: string;
  animalId?: string;
  status?: TreatmentStatus;
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

export interface DiseaseIncidentParams {
  pageNumber?: number;
  pageSize?: number;
  farmId?: string;
  status?: IncidentStatus;
  severity?: IncidentSeverity;
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

export interface MortalityRecordParams {
  pageNumber?: number;
  pageSize?: number;
  farmId?: string;
  animalId?: string;
  reason?: string;
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

export interface VetVisitParams {
  pageNumber?: number;
  pageSize?: number;
  farmId?: string;
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

export interface VaccinationProtocolStepDto {
  id: string;
  stepName: string;
  targetAgeDays: number;
  vaccineName: string;
  dosageInstruction: string;
}

export interface VaccinationProtocolDto {
  id: string;
  title: string;
  targetSpecies: string;
  description?: string;
  isActive: boolean;
  steps: VaccinationProtocolStepDto[];
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

export interface MortalityRecordDto {
  id: string;
  animalId: string;
  deathDate: string;
  causeOfDeath: string;
  diseaseName?: string;
  postMortemNotes?: string;
  estimatedEconomicLossBdt?: number;
  diseaseIncidentId?: string;
  recordedByUserId: string;
}

export interface VetVisitDto {
  id: string;
  farmId: string;
  vetName: string;
  visitDate: string;
  visitType: string;
  visitTypeId?: number;
  purpose?: string;
  findings?: string;
  recommendations?: string;
  costBdt?: number;
  nextVisitDate?: string;
  createdAt?: string;
}

export interface HealthDashboardDto {
  vaccinationsDueThisWeek: number;
  vaccinationsOverdue: number;
  activeTreatments: number;
  activeDiseaseIncidents: number;
  recentMortalityCount: number;
  monthlyHealthCostBdt: number;
}

export interface AnimalHealthHistoryDto {
  vaccinations: VaccinationEventDto[];
  treatments: MedicalTreatmentDto[];
}

export interface DiseaseIncidentDetail extends DiseaseIncidentDto {
  affectedAnimals: {
    animalId: string;
    tagNumber: string;
    species: string;
    breedName: string;
  }[];
}

export interface DewormingCalendarDto {
  eventId: string;
  animalId: string;
  animalTag: string;
  vaccineName: string;
  scheduledDate: string;
  status: string;
}

export interface MilkWithdrawalDto {
  animalId: string;
  animalTag: string;
  treatmentId: string;
  medicationName: string;
  treatmentStartDate: string;
  milkWithdrawalDays: number;
  safeToMilkDate: string;
}

export type DiseaseIncident = DiseaseIncidentDto;
