# Master Data Module Implementation Plan

The Master Data module includes 20+ reference entities that are critical for the rest of the system (e.g., dropdowns, validation). Implementing these using the standard boilerplate (Controller, 4x CQRS Commands/Queries, Repository, UI Components) for *each* of the 20 entities would result in hundreds of files and massive code duplication.

To fulfill the requirement to "Generate reusable master data modules", I propose a highly reusable architecture.

## User Review Required

> [!IMPORTANT]
> **Architecture Decision: Generic vs. Explicit Entities**
> 
> **Option A: Generic Master Data Architecture (Recommended)**
> - We create a unified backend approach for flat reference data.
> - **Domain**: `MasterDataEntry` with a `Type` discriminator (Enum) for things like `FeedType`, `Currency`, `AnimalType`.
> - **API**: A single controller: `/api/v1/master-data/{type}`.
> - **Angular**: A generic `MasterDataComponent` and `<app-master-data-dropdown [type]="'AnimalType'">` that automatically fetches and caches data.
> - **Locations**: Handled separately via explicit hierarchical entities (`Country` -> `Division` -> `District` -> `Upazila`).
> 
> **Option B: Explicit Domain Entities**
> - We create 20 separate classes (`FeedType.cs`, `Currency.cs`, etc.), 20 repositories, and 80+ CQRS commands.
> - **Pros**: Strict type safety.
> - **Cons**: Massive boilerplate, harder to maintain.
> 
> *I will proceed with **Option A** (with Locations handled explicitly) unless you specify Option B.*

## Proposed Architecture (Option A)

### 1. Domain Layer (`Farm360.Domain.MasterData`)

**Flat Master Data:**
- `MasterDataEntry` (Aggregate Root): `Id, TenantId, Type (Enum), Name, Code, Description, IsActive, DisplayOrder`.
- `MasterDataType` (Enum): `Breed, AnimalType, FeedType, MedicineType, VaccinationType, Disease, SupplierCategory, ExpenseCategory, PaymentMethod, MeasurementUnit, Currency, Language, Timezone, BusinessType`.

**Hierarchical Locations:**
- `Country` (Id, Name, Code)
- `Division` (Id, CountryId, Name)
- `District` (Id, DivisionId, Name)
- `Upazila` (Id, DistrictId, Name)
- `Union` (Id, UpazilaId, Name)
- `Village` (Id, UnionId, Name)

### 2. Persistence Layer (`Farm360.Persistence`)
- `MasterDataConfiguration`: EF Core config mapping discriminator types.
- `LocationConfigurations`: Configs for the cascading location tables.
- `IMasterDataRepository` and `ILocationRepository`.
- Built-in In-Memory **Caching** using `IMemoryCache` for ultra-fast reads.

### 3. Application Layer (CQRS)
- **Generic Handlers**: 
  - `CreateMasterDataCommand`, `UpdateMasterDataCommand`, `DeleteMasterDataCommand`.
  - `GetMasterDataByTypeQuery`.
- **Location Handlers**: Specific queries like `GetDistrictsByDivisionQuery`.

### 4. API Layer
- `MasterDataEndpoints`: `/api/v1/master-data/{type}`
- `LocationEndpoints`: `/api/v1/locations/countries`, `/api/v1/locations/districts?divisionId={id}`

### 5. Angular UI
- **Services**: `master-data.service.ts` with local `BehaviorSubject` caching.
- **Reusable UI Components**:
  - `master-data-dropdown.component.ts`: `<app-dropdown type="Breed" formControlName="breedId"></app-dropdown>`
  - `location-selector.component.ts`: Cascading dropdowns (Country -> Division -> District).
- **Management UI**: A single dynamic Management page (`/settings/master-data`) where admins can select a category from a sidebar and manage its entries.

## Verification Plan
1. Ensure generic endpoints correctly filter by `TenantId` and `MasterDataType`.
2. Verify Angular caching prevents duplicate HTTP requests for the same dropdown type.
3. Run `dotnet test` and `npm run build`.
