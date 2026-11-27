export interface BreedDto {
  id: string;
  name: string;
  description: string;
  category: string;
  origin: string;
  mainPurpose: string;
  bestFor: string;
  adgPoorManagement: number;
  adgAverageFarm: number;
  adgGoodCommercialFarm: number;
  adgIntensiveFattening: number;
  standardAdgMin: number;
  standardAdgMax: number;
  fcrMin: number;
  fcrMax: number;
  milkYieldMinLiters: number;
  milkYieldMaxLiters: number;
  fatPercentageMin: number;
  fatPercentageMax: number;
  /** Optional price per kg live weight this breed is expected to fetch (BDT); used by the sell-or-keep advice. */
  expectedPricePerKgBdt?: number | null;
}

export interface CreateBreedRequest {
  name: string;
  description: string;
  category: string;
  origin: string;
  mainPurpose: string;
  bestFor: string;
  adgPoorManagement: number;
  adgAverageFarm: number;
  adgGoodCommercialFarm: number;
  adgIntensiveFattening: number;
  standardAdgMin: number;
  standardAdgMax: number;
  fcrMin: number;
  fcrMax: number;
  milkYieldMinLiters: number;
  milkYieldMaxLiters: number;
  fatPercentageMin: number;
  fatPercentageMax: number;
  /** Optional price per kg live weight this breed is expected to fetch (BDT); used by the sell-or-keep advice. */
  expectedPricePerKgBdt?: number | null;
}
