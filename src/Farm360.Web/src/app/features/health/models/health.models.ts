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
  Mild = 'Mild',
  Moderate = 'Moderate',
  Severe = 'Severe',
  Critical = 'Critical'
}

export enum IncidentStatus {
  Reported = 'Reported',
  UnderTreatment = 'UnderTreatment',
  Resolved = 'Resolved',
  Fatal = 'Fatal'
}

export enum CauseOfDeath {
  Disease = 'Disease',
  Injury = 'Injury',
  Natural = 'Natural',
  Predator = 'Predator',
  Unknown = 'Unknown'
}

export enum VetVisitType {
  Routine = 'Routine',
  Emergency = 'Emergency',
  FollowUp = 'FollowUp'
}

export enum AnimalSpecies {
  Cattle = 'Cattle',
  Sheep = 'Sheep',
  Goat = 'Goat',
  Poultry = 'Poultry'
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
  purpose?: string;
  findings?: string;
  recommendations?: string;
  costBdt?: number;
  nextVisitDate?: string;
}

export interface HealthDashboardDto {
  vaccinationsDueThisWeek: number;
  vaccinationsOverdue: number;
  activeTreatments: number;
  activeIncidents: number;
  recentMortalityCount: number;
  monthlyHealthCost: number;
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
