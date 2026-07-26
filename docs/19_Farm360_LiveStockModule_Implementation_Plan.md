# Livestock Module — Comprehensive Audit & Implementation Plan

Based on the audit comparing the current codebase to the `PRODUCT_REQUIREMENTS.md` (MVP PRD), there are several missing features, business rules, and UI inconsistencies in the Livestock Module.

## 1. Requirement Gap Analysis

| PRD Req | Feature | Status | Notes / Gaps |
|---|---|---|---|
| FR-LM-05 | Assign to Shed and Pen | ⚠️ Incomplete | `Animal.cs` has `ShedId`, but **completely lacks `PenId`**. The API `RegisterAnimalCommand` is missing `PenId`. The UI lacks cascaded dropdowns for shedding/pen assignment. |
| FR-LM-06 | Batch/Group Creation | ❌ Missing | No `Batch` entity, endpoints, or UI exists for batch management. |
| FR-LM-08 | ADG Calculation | ✅ Complete | Handled via `RecalculateAdg()` in `Animal.cs` when weights are recorded. |
| FR-LM-09 | Record Mating Events | ⚠️ Incomplete | `Animal.cs` has `AddBreedingRecord()`, but there are no CQRS Commands or Minimal API endpoints exposing this. UI is missing. |
| FR-LM-10 | Pregnancy Confirmation | ❌ Missing | No domain support for confirming pregnancy or calculating expected calving date. |
| FR-LM-11 | Record Births (Calves) | ❌ Missing | Missing domain logic and API to link newborn calves to a dam/sire. |
| FR-LM-13 | Sale Transactions | ⚠️ Incomplete | `SellAnimalCommand` has `SalePrice` and `SaleDate`, but PRD explicitly requires `SaleWeight` and `BuyerName`. |
| FR-LM-16 | Body Condition Score (BCS) | ❌ Missing | Completely missing from domain, API, and UI. |
| FR-P-08 | Org -> Branch -> Farm Hierarchy | ⚠️ Incomplete | UI dropdowns are not cascaded. `animal-register` allows picking a Farm directly, bypassing Organization and Branch scoping. |

---

## 2. Missing Features Checklist

- [ ] **Domain Updates**: Add `PenId` to `Animal.cs`. Add `BuyerName` and `SaleWeight` to `Sell()`. Add Pregnancy tracking to `BreedingRecord.cs`.
- [ ] **CQRS Commands**: Create `RecordMatingCommand`, `ConfirmPregnancyCommand`, `RecordBirthCommand`.
- [ ] **API Endpoints**: Add endpoints for Breeding/Mating under `/api/v1/livestock/animals/{id}/breeding`.
- [ ] **Frontend Hierarchy**: Implement dependent dropdowns (Organization → Branch → Farm → Shed → Pen) in `animal-register.component.ts`.
- [ ] **Sale Logic Update**: Update frontend `animal-detail` to capture buyer info when selling.
- [ ] **BCS Scoring (Low Priority for MVP?)**: Needs a new child entity `BcsRecord` on `Animal`.

---

## 3. UI/UX & Workflow Inconsistencies

1. **Disconnected Registration Flow**: Currently, `animal-register` uses a flat `FarmId` dropdown. To enforce RBAC and multitenancy strictly as per PRD (FR-P-08), it must be a cascading workflow: `Select Organization` ➝ `Select Branch` ➝ `Select Farm` ➝ `Select Shed` ➝ `Select Pen`.
2. **Missing Pen Capacity Checks**: The frontend should fetch the Pen's current headcount and max capacity before allowing registration (Requires backend support).
3. **Missing Breeding Tab**: The `animal-detail` view has tabs for *Weight History*, *Photo Gallery*, and *Audit Info*, but lacks a *Breeding History* tab for female animals.

---

## 4. Prioritized Implementation Plan

> [!IMPORTANT]
> **User Review Required**: Please review the implementation order below. Do we want to implement **Batch/Group Management (FR-LM-06)** right now, or focus strictly on the Individual Animal hierarchy and breeding first?

### Phase 1: Core Hierarchy & Structure Repair (High Priority)
1. **Backend**: Update `Animal.cs` to include `Guid? PenId`. Add migration. Update `RegisterAnimalCommand`.
2. **Backend**: Update `SellAnimalCommand` to include `BuyerName` and `SaleWeightKg`.
3. **Frontend**: Refactor `animal-register` to use cascaded dropdowns for `Farm -> Shed -> Pen`. (Skipping Org/Branch if the current user is locked to a Branch, or implementing full cascade if PlatformAdmin).

### Phase 2: Breeding & Lifecycle (Medium Priority)
1. **Backend**: Create `RecordMatingCommand`, `ConfirmPregnancyCommand`, and expose them in `LivestockEndpoints.cs`.
2. **Frontend**: Add a "Breeding" tab to `animal-detail` for Female animals. Add dialogs for Mating & Pregnancy Confirmation.

### Phase 3: Groups & Batching (Low Priority)
1. **Backend**: Implement the `AnimalBatch` aggregate root.
2. **Frontend**: Create the Batch views.

---

## Open Questions

1. **Hierarchy context**: When a user registers an animal, are they guaranteed to be scoped to a specific Branch via their Identity? Or must we force them to select Organization ➝ Branch first in the UI?
2. **Batch Management**: Can Batch management be deferred to a later sprint, or is it a strict blocker for the MVP?
3. **Pen Capacity**: Should we implement hard capacity validation at the backend (e.g., throwing an error if Pen is full), or just a warning in the UI?

## Proposed Implementation (Next Steps)
If approved, I will begin **Phase 1** by updating the `Animal` domain entity, adjusting the CQRS commands, and building the cascaded dropdown UI for `animal-register`.
