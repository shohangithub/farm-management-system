// ── Enums ─────────────────────────────────────────────────────────────────────

export enum AnimalSpecies {
  CattleBeef  = 1,
  CattleDairy = 2,
  Goat        = 3,
  Sheep       = 4,
}

export enum AnimalSex {
  Male   = 1,
  Female = 2,
}

export enum AnimalStatus {
  Active      = 1,
  Quarantined = 2,
  Sold        = 3,
  Slaughtered = 4,
  Dead        = 5,
  Transferred = 6,
}

export enum AcquisitionType {
  Purchased   = 1,
  BornOnFarm  = 2,
}

export enum DisposalReason {
  Sale         = 1,
  Slaughter    = 2,
  NaturalDeath = 3,
  Disease      = 4,
  Accident     = 5,
  Unknown      = 6,
}

export enum TagType {
  Manual = 1,
  EarTag = 2,
  Rfid   = 3,
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

export interface WeightRecordDto {
  id: string;
  animalId: string;
  weightKg: number;
  recordedDate: string;
  notes?: string;
  recordedAtUtc: string;
}

export interface BreedingRecordDto {
  id: string;
  animalId: string;
  matingDate: string;
  sireAnimalId?: string;
  sireExternalId?: string;
  isArtificialInsemination: boolean;
  pregnancyConfirmDate?: string;
  isPregnancyConfirmed: boolean;
  expectedCalvingDate?: string;
  actualCalvingDate?: string;
  calvingOutcome?: string;
  calvesCount?: number;
  createdAtUtc: string;
}

export interface AnimalPhotoDto {
  id: string;
  animalId: string;
  photoUrl: string;
  caption?: string;
  isPrimary: boolean;
  uploadedAtUtc: string;
}

export interface AnimalMovementDto {
  id: string;
  animalId: string;
  shedId?: string;
  penId?: string;
  placedAtUtc: string;
  placedBy: string;
  removedAtUtc?: string;
  removedBy?: string;
  transferReason?: string;
}

export interface BcsRecordDto {
  id: string;
  animalId: string;
  score: number;
  recordedDate: string;
  evaluatorId: string;
  notes?: string;
}

export interface AnimalDto {
  id: string;
  tenantId: string;
  farmId: string;
  batchId?: string;
  shedId?: string;
  penId?: string;
  tagId: string;
  tagType: TagType;
  species: AnimalSpecies;
  breedId: string;
  breedName: string;
  sex: AnimalSex;
  dateOfBirth: string;
  acquisitionType: AcquisitionType;
  acquisitionDate: string;
  acquisitionPriceBdt?: number;
  salePriceBdt?: number;
  saleDate?: string;
  status: AnimalStatus;
  quarantineReason?: string;
  disposalReason?: DisposalReason;
  notes?: string;
  latestWeightKg?: number;
  latestWeightDate?: string;
  adgKgPerDay?: number;
  latestBcs?: number;
  primaryPhotoUrl?: string;
  weightRecords: WeightRecordDto[];
  breedingRecords: BreedingRecordDto[];
  photos: AnimalPhotoDto[];
  movements: AnimalMovementDto[];
  bcsRecords: BcsRecordDto[];
  createdAtUtc: string;
  createdBy: string;
  modifiedAtUtc?: string;
}

export interface AnimalListItemDto {
  id: string;
  tagId: string;
  tagType: TagType;
  species: AnimalSpecies;
  breedId: string;
  breedName: string;
  sex: AnimalSex;
  dateOfBirth: string;
  status: AnimalStatus;
  farmId: string;
  batchId?: string;
  shedId?: string;
  penId?: string;
  latestWeightKg?: number;
  latestWeightDate?: string;
  adgKgPerDay?: number;
  latestBcs?: number;
  primaryPhotoUrl?: string;
  createdAtUtc: string;
}

export interface PagedAnimalListDto {
  items: AnimalListItemDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

// ── Request payloads ──────────────────────────────────────────────────────────

export interface RegisterAnimalRequest {
  farmId: string;
  tagId: string;
  tagType: TagType;
  species: AnimalSpecies;
  breedId: string;
  sex: AnimalSex;
  dateOfBirth: string;
  acquisitionType: AcquisitionType;
  acquisitionDate: string;
  acquisitionPriceBdt?: number;
  notes?: string;
}

export interface RecordWeightRequest {
  weightKg: number;
  recordedDate: string;
  notes?: string;
}

export interface SellAnimalRequest {
  salePriceBdt: number;
  saleDate: string;
  buyerName?: string;
  saleWeightKg?: number;
}

export interface QuarantineAnimalRequest {
  reason: string;
}

export interface RecordDeathRequest {
  cause: DisposalReason;
  deathDate: string;
  notes?: string;
}

export interface TransferAnimalRequest {
  toShedId?: string;
  toPenId?: string;
  transferDate: string;
  reason?: string;
}

export interface RecordMatingRequest {
  matingDate: string;
  sireAnimalId?: string;
  sireExternalId?: string;
  isArtificialInsemination: boolean;
}

export interface AddPhotoRequest {
  photoUrl: string;
  caption?: string;
}

export interface ConfirmPregnancyRequest {
  confirmDate: string;
  expectedCalvingDate: string;
}

export interface RecordCalvingRequest {
  calvingDate: string;
  outcome: string;
  calvesCount: number;
}

// ── Filter params ─────────────────────────────────────────────────────────────

export interface AnimalListParams {
  pageNumber?: number;
  pageSize?: number;
  farmId?: string;
  batchId?: string;
  shedId?: string;
  penId?: string;
  species?: AnimalSpecies;
  sex?: AnimalSex;
  status?: AnimalStatus;
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

// ── Display helpers ───────────────────────────────────────────────────────────

export const SPECIES_LABELS: Record<AnimalSpecies, string> = {
  [AnimalSpecies.CattleBeef]:  'Cattle (Beef)',
  [AnimalSpecies.CattleDairy]: 'Cattle (Dairy)',
  [AnimalSpecies.Goat]:        'Goat',
  [AnimalSpecies.Sheep]:       'Sheep',
};

export const STATUS_LABELS: Record<AnimalStatus, string> = {
  [AnimalStatus.Active]:      'Active',
  [AnimalStatus.Quarantined]: 'Quarantined',
  [AnimalStatus.Sold]:        'Sold',
  [AnimalStatus.Slaughtered]: 'Slaughtered',
  [AnimalStatus.Dead]:        'Dead',
  [AnimalStatus.Transferred]: 'Transferred',
};

export const STATUS_BADGE_CLASS: Record<AnimalStatus, string> = {
  [AnimalStatus.Active]:      'badge-active',
  [AnimalStatus.Quarantined]: 'badge-quarantine',
  [AnimalStatus.Sold]:        'badge-sold',
  [AnimalStatus.Slaughtered]: 'badge-default',
  [AnimalStatus.Dead]:        'badge-dead',
  [AnimalStatus.Transferred]: 'badge-default',
};

export const SEX_LABELS: Record<AnimalSex, string> = {
  [AnimalSex.Male]:   'Male ♂',
  [AnimalSex.Female]: 'Female ♀',
};
