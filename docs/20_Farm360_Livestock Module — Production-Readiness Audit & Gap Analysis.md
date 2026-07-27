# Livestock Module — Production-Readiness Audit & Gap Analysis

## Audit Scope
Complete production-readiness review of the **Livestock Module** across Domain, Application, API, Persistence, and Angular UI layers. Cross-referenced against PRD (§3.2, §7.2, §8.2, §9.2), PROJECT_CONSTITUTION, SOFTWARE_ARCHITECTURE, and DEVELOPMENT_STATUS.

---

## 1. Gap Analysis — Findings by Priority

### 🔴 Critical (Must Fix Before Production)

| # | Finding | PRD Ref | Layer | Details |
|---|---------|---------|-------|---------|
| C-1 | **Missing FluentValidation validators for 5 commands** | Architecture §3.2 | Application | `RecordBcsCommand`, `RecordMatingCommand`, `ConfirmPregnancyCommand`, `RecordCalvingCommand`, `CreateBatchCommand`, and `UploadAnimalPhotoCommand` all lack `AbstractValidator<T>` implementations. The `ValidationBehavior` MediatR pipeline only catches errors if a validator is registered. Without validators, invalid data (future dates, negative scores, empty required fields) bypasses validation and reaches the domain or database. |
| C-2 | **Photo upload limit not enforced (max 5 per animal)** | FR-LM-17 | Domain | PRD explicitly states "max 5 photos per animal". Neither `Animal.AddPhoto()` domain method nor `UploadAnimalPhotoCommand` handler checks `_photos.Count >= 5`. Users can upload unlimited photos. |
| C-3 | **BCS `RecordedDate` can be in the future** | BRU-LM-02 analog | Application | `RecordBcsCommand` has no FluentValidation validator. The domain method `RecordBodyConditionScore()` doesn't validate date bounds. A future date is accepted. |
| C-4 | **Mating date can be in the future** | BRU-LM-09 | Application/Domain | `RecordMatingCommand` has no validator. `AddBreedingRecord()` doesn't check if `matingDate > today`. |
| C-5 | **Pregnancy confirmation date not validated against mating date** | BRU-LM-09 | Domain | PRD states "Pregnancy confirmation date must be ≥ mating date". The `BreedingRecord.ConfirmPregnancy()` method does not validate this constraint. |

---

### 🟠 High (Should Fix Before Production)

| # | Finding | PRD Ref | Layer | Details |
|---|---------|---------|-------|---------|
| H-1 | **Animal list missing `batchId` filter parameter** | FR-LM-15 | API/Query/Repository | PRD requires filtering by "batch". `GetAnimalListQuery`, `GetPagedAsync()`, and the API endpoint all lack a `batchId` filter. Users cannot filter the animal list by batch. |
| H-2 | **No animal timeline / chronological event log** | FR-LM-14 | All | PRD (Must Have) requires "a complete chronological animal timeline for every animal". No timeline aggregation exists — weight records, breeding records, health events, movements, and status changes are shown in separate tabs but never as a unified timeline view. |
| H-3 | **`AnimalService.recordMating()` uses `any` type** | — | Frontend | [animal.service.ts:105](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/livestock/services/animal.service.ts#L105) uses `request: any` instead of a typed interface. Bypasses TypeScript safety. |
| H-4 | **Expected calving date not auto-calculated** | BRU-LM-10 | Domain/UI | PRD states "Expected calving date is auto-calculated as mating date + gestation period by species (Cattle: 283 days, Goat: 150 days)". Current implementation requires the user to manually enter the expected calving date with no auto-suggestion. |
| H-5 | **`RecordBcsCommand` manually checks `TenantId` — bypasses global filter** | Architecture | Application | [RecordBcsCommand.cs:38](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Application/Livestock/Commands/RecordBcsCommand.cs#L38) manually checks `animal.TenantId != tenantId`. All other handlers rely on the EF Core global query filter. This is inconsistent and redundant — or worse, it indicates the handler author didn't trust the filter, suggesting a deeper issue. |
| H-6 | **Weight > 10% decrease warning not implemented** | US-LM-04 AC | Domain/UI | Acceptance criteria state: "if the new weight represents a decrease of > 10% from the previous entry, a warning flag is shown to the Farm Manager". Not implemented. |

---

### 🟡 Medium (Should Fix, Non-Blocking)

| # | Finding | PRD Ref | Layer | Details |
|---|---------|---------|-------|---------|
| M-1 | **No `TransferAnimalCommandValidator`** | — | Application | The `TransferAnimalCommand` has no validator. Transfer date could be in the future or before DOB. |
| M-2 | **`RecordMatingCommand` uses `ArgumentException` for not-found** | Architecture | Application | Should use `NotFoundException` (like all other handlers) to return proper HTTP 404 via the global exception handler. Same issue in `ConfirmPregnancyCommand`. |
| M-3 | **Batch list page filter missing `farmId`** | — | API/Query | `GetBatchesQuery` should filter by `farmId` so users see only batches belonging to their selected farm. |
| M-4 | **Inter-farm animal transfer not implemented** | FR-LM-18 | All | PRD (Must Have) states "The system shall allow inter-farm animal transfer within the same organization". Current `TransferAnimalCommand` only transfers between sheds/pens, not between farms. The `FarmId` on the `Animal` entity is never updated. |
| M-5 | **No `BuyerName` field in sale DTO output** | — | DTO | `AnimalDto` doesn't include `buyerName` or `saleWeightKg`. These are tracked in the domain but not surfaced to the UI. |
| M-6 | **Sidebar navigation: "Batches" menu item not visible** | — | Frontend | User previously reported not finding the Batch menu. Need to verify the sidebar includes a link to `/livestock/batches`. |

---

### 🟢 Low (Technical Debt / Polish)

| # | Finding | PRD Ref | Layer | Details |
|---|---------|---------|-------|---------|
| L-1 | **`recordMating` service method is untyped** | — | Frontend | Should use a proper `RecordMatingRequest` interface instead of `any`. |
| L-2 | **Inconsistent handler constructor styles** | — | Application | Some handlers use primary constructors (`RecordWeightCommandHandler(repo, uow, user)`), others use traditional DI constructors. Should standardize. |
| L-3 | **No pagination in photo gallery** | — | Frontend | If an animal had many photos (near the limit), all are loaded at once. Low priority given the 5-photo limit. |
| L-4 | **`AnimalBatch` entity lacks comprehensive domain methods** | — | Domain | The `AnimalBatch` entity is a simple data class without business rules (e.g., cannot add animals from different farms, cannot close batch with active animals). |

---

## 2. Prioritized Implementation Plan

### Phase 1: Critical Validators & Business Rules (Must Fix)

#### 1.1 Add Missing FluentValidation Validators
- `RecordBcsCommandValidator` — Score 1.0–5.0, date not future
- `RecordMatingCommandValidator` — Date not future, AnimalId required
- `ConfirmPregnancyCommandValidator` — ConfirmDate ≥ mating date, ExpectedCalvingDate > ConfirmDate
- `RecordCalvingCommandValidator` — CalvingDate not future, outcome required
- `CreateBatchCommandValidator` — Name required, FarmId required
- `UploadAnimalPhotoCommandValidator` — FileName not empty, ContentType valid
- `TransferAnimalCommandValidator` — TransferDate not future

#### 1.2 Enforce Photo Limit (max 5)
- Add guard in `Animal.AddPhoto()`: `if (_photos.Count >= 5) throw new BusinessRuleException("Maximum 5 photos per animal.")`

#### 1.3 Fix Pregnancy Date Validation
- In `BreedingRecord.ConfirmPregnancy()`, add: `if (confirmDate < MatingDate) throw`

### Phase 2: High Priority Fixes

#### 2.1 Add `batchId` Filter to Animal List
- Add `Guid? batchId` parameter to `GetAnimalListQuery`, `IAnimalRepository.GetPagedAsync()`, `AnimalRepository`, and the API endpoint.

#### 2.2 Fix `any` Type in `AnimalService`
- Type the `recordMating` method parameter properly.

#### 2.3 Add `buyerName` and `saleWeightKg` to `AnimalDto`
- Already on the domain entity; just add to the DTO mapping.

#### 2.4 Remove Redundant Manual Tenant Check in `RecordBcsCommandHandler`
- Align with other handlers that rely on the global query filter.

### Phase 3: Medium Priority

#### 3.1 Fix Exception Types
- Replace `ArgumentException` with `NotFoundException` in `RecordMatingCommandHandler` and `ConfirmPregnancyCommandHandler`.

### Deferred (Post-MVP)
- **FR-LM-14 Animal Timeline**: Unified event timeline view (requires cross-module data aggregation from Livestock + Health)
- **FR-LM-18 Inter-Farm Transfer**: Requires FarmId update + cross-farm authorization logic
- **BRU-LM-10 Auto-Calc Expected Calving Date**: UI auto-fill based on species gestation period
- **US-LM-04 Weight Decrease Warning**: Frontend toast/warning when weight drops > 10%

---

## 3. Verification Plan

### Automated Tests
- Run: `dotnet test` across all test projects
- Verify all new validators are covered by unit tests

### Manual Verification  
- Register animal → Record weight → Record BCS → Upload photos (test 5 limit) → Record mating → Confirm pregnancy → Record calving
- Verify batch filter works on animal list
- Verify validators return proper 422 errors for invalid input

---

## 4. Documentation Updates Required

After implementation:
- Update `CHANGELOG.md` with all fixes
- Update `DEVELOPMENT_STATUS.md` to mark Livestock as "Production Ready (MVP)"  
- Update `TODO.md` to move deferred items under a "Post-MVP" section
- Update [19_Farm360_LiveStockModule_Implementation_Plan.md](file:///d:/Personel/Farm%20Management%20System/docs/19_Farm360_LiveStockModule_Implementation_Plan.md) with audit results

---

> [!IMPORTANT]
> The Livestock Module is **NOT production-ready** due to 5 Critical and 6 High-priority findings. The most severe issues are missing validators that allow invalid data through the CQRS pipeline, and the missing photo upload limit. These must be resolved before marking the module as complete.
