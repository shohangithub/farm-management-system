import type { CreditTerms } from '../../finance/models/finance.model';
// ── Enums ─────────────────────────────────────────────────────────────────────

export enum AnimalSpecies {
  CattleBeef  = 'CattleBeef',
  CattleDairy = 'CattleDairy',
  Goat        = 'Goat',
  Sheep       = 'Sheep',
}

export enum AnimalSex {
  Male   = 'Male',
  Female = 'Female',
}

export enum AnimalStatus {
  Active      = 'Active',
  Quarantined = 'Quarantined',
  Sold        = 'Sold',
  Slaughtered = 'Slaughtered',
  Dead        = 'Dead',
  Transferred = 'Transferred',
}

export enum AcquisitionType {
  Purchased   = 'Purchased',
  BornOnFarm  = 'BornOnFarm',
}

export enum DisposalReason {
  Sale         = 'Sale',
  Slaughter    = 'Slaughter',
  NaturalDeath = 'NaturalDeath',
  Disease      = 'Disease',
  Accident     = 'Accident',
  Unknown      = 'Unknown',
}

export enum TagType {
  Manual = 'Manual',
  EarTag = 'EarTag',
  Rfid   = 'Rfid',
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
  buyerName?: string | null;
  saleWeightKg?: number | null;
  /** Date of death (Dead animals). */
  deathDate?: string | null;
  /** Date the animal left the farm: saleDate (Sold) or deathDate (Dead). */
  exitDate?: string | null;
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
  shedName?: string;
  penId?: string;
  penName?: string;
  latestWeightKg?: number;
  latestWeightDate?: string;
  adgKgPerDay?: number;
  latestBcs?: number;
  primaryPhotoUrl?: string;
  hasHealthAlert?: boolean;
  lastVaccinationDate?: string;
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
  /** Opt out of the health programmes that start automatically for purchased animals. */
  skipHealthProgramme?: boolean;
  shedId?: string;
  penId?: string;
}

export interface UpdateAnimalRequest {
  id: string;
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

/** What undoing a sale or death changed. */
export interface ReverseAnimalExitResult {
  wasSold: boolean;
  exitDate: string;
  receivedFromBuyerBdt: number;
  mortalityRecordsRemoved: number;
}

export interface SellAnimalRequest {
  salePriceBdt: number;
  saleDate: string;
  buyerName?: string;
  saleWeightKg?: number;
  /** Set when the buyer pays later — the balance becomes a customer receivable. */
  credit?: CreditTerms | null;
  /** Sell even though a medicine's meat withdrawal period is still running (e.g. sold live). */
  allowDuringMeatWithdrawal?: boolean;
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

export type AnimalPresence = 'OnFarm' | 'LeftFarm';

/**
 * Farm rule: an animal is "on farm" only while Active or Quarantined.
 * Sold / Dead / Slaughtered / Transferred animals have left the farm — their
 * history stays visible, but they must not be offered for new actions.
 */
export const ON_FARM_STATUSES: readonly AnimalStatus[] = [AnimalStatus.Active, AnimalStatus.Quarantined];

export function isOnFarm(status: AnimalStatus | string | null | undefined): boolean {
  return status === AnimalStatus.Active || status === AnimalStatus.Quarantined;
}

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
  /** OnFarm = Active/Quarantined; LeftFarm = Sold/Dead/Slaughtered/Transferred; omitted = all. */
  presence?: AnimalPresence;
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
