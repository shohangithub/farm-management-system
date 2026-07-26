# Livestock Module — Comprehensive Audit & Implementation Plan

Based on the audit comparing the current codebase to the `PRODUCT_REQUIREMENTS.md` (MVP PRD), there are several missing features, business rules, and UI inconsistencies in the Livestock Module.

## 1. Requirement Gap Analysis

| PRD Req | Feature | Status | Notes / Gaps |
|---|---|---|---|
| FR-LM-05 | Assign to Shed and Pen | ✅ Complete (Backend) | Location is tracked via `AnimalMovement` history (join) rather than direct fields on `Animal` to prevent confusion. |
| FR-LM-06 | Batch/Group Creation | ❌ Missing | No `Batch` entity, endpoints, or UI exists for batch management. |
| FR-LM-08 | ADG Calculation | ✅ Complete | Handled via `RecalculateAdg()` in `Animal.cs` when weights are recorded. |
| FR-LM-09 | Record Mating Events | ⚠️ Incomplete | `Animal.cs` has `AddBreedingRecord()`, but there are no CQRS Commands or Minimal API endpoints exposing this. UI is missing. |
| FR-LM-10 | Pregnancy Confirmation | ❌ Missing | No domain support for confirming pregnancy or calculating expected calving date. |
| FR-LM-11 | Record Births (Calves) | ❌ Missing | Missing domain logic and API to link newborn calves to a dam/sire. |
| FR-LM-13 | Sale Transactions | ✅ Complete (Backend) | `SellAnimalCommand` contains `SalePrice`, `SaleDate`, `SaleWeight`, and `BuyerName`. Frontend UI needs to use these. |
| FR-LM-16 | Body Condition Score (BCS) | ❌ Missing | Completely missing from domain, API, and UI. |
| FR-P-08 | Org -> Branch -> Farm Hierarchy | ⚠️ Incomplete | UI dropdowns are not fully cascaded in all forms. |

---

## 2. Completed Work

- [x] **Backend Architecture Fixed**: Removed hardcoded entity tracking in EF Core interceptors, opting for CQRS+EF Core native tracking for business rules.
- [x] **Location Tracking**: Implemented `AnimalMovement` for tracking Shed and Pen assignments instead of polluting the `Animal` table.
- [x] **Animal Detail UI**: Moved Location and Status into a dedicated Tab in the Animal Detail component.

---

## 3. Prioritized Implementation Plan (Next Steps)

### Phase 1: Animal Detail View Expansions (Current Task)
1. **Frontend**: Add "other entries" to the `AnimalDetailComponent`.
   - **Movement History Tab**: Display the history of Shed/Pen transfers (from `AnimalMovement`).
   - **Breeding History Tab**: Add tab for female animals to show mating and pregnancy records.
   - **Medical/Health Tab**: Add tab for vaccination and treatment history.

### Phase 2: Breeding & Lifecycle (Medium Priority)
1. **Backend**: Create CQRS Commands (`RecordMatingCommand`, `ConfirmPregnancyCommand`) and expose them in `LivestockEndpoints.cs`.
2. **Frontend**: Add dialogs/forms in the Breeding tab to submit Mating & Pregnancy Confirmation.

### Phase 3: Sales UI & Groups (Low Priority)
1. **Frontend**: Update the Sale Dialog in the frontend to capture `BuyerName` and `SaleWeightKg` to match the backend command.
2. **Backend/Frontend**: Implement Batch/Group management for bulk animal processing.

---

## Next Action
I will now begin Phase 1: Adding the remaining entries/tabs to the `AnimalDetailComponent` UI to give a comprehensive view of the animal.
