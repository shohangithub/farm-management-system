# Livestock Module — Comprehensive Audit & Implementation Plan

Based on the audit comparing the current codebase to the `PRODUCT_REQUIREMENTS.md` (MVP PRD), there are several missing features, business rules, and UI inconsistencies in the Livestock Module.

## 1. Requirement Gap Analysis

| PRD Req | Feature | Status | Notes / Gaps |
|---|---|---|---|
| FR-LM-05 | Assign to Shed and Pen | ✅ Complete (Backend) | Location is tracked via `AnimalMovement` history (join) rather than direct fields on `Animal` to prevent confusion. |
| FR-LM-06 | Batch/Group Creation | ❌ Missing | No `Batch` entity, endpoints, or UI exists for batch management. |
| FR-LM-08 | ADG Calculation | ⚠️ Incomplete | Handled via `RecalculateAdg()` in `Animal.cs` when weights are recorded. |
| FR-LM-09 | Record Mating Events | ✅ Complete | Handled via `AddBreedingRecord()` in `Animal.cs` and CQRS endpoints. UI is complete. |
| FR-LM-10 | Pregnancy Confirmation | ✅ Complete | Domain support for confirming pregnancy added, API exposed, UI implemented. |
| FR-LM-11 | Record Births (Calves) | ✅ Complete | Domain logic and API to link newborn calves to a dam/sire added. UI implemented. |
| FR-LM-13 | Sale Transactions | ✅ Complete | Backend command and Frontend UI (Record Sale dialog) are fully implemented. |
| FR-LM-16 | Body Condition Score (BCS) | ❌ Missing | Completely missing from domain, API, and UI. |
| FR-P-08 | Org -> Branch -> Farm Hierarchy | ✅ Complete | UI dropdowns fully cascaded and assignment complete. |

---

## 2. Completed Work

- [x] **Backend Architecture Fixed**: CQRS+EF Core native tracking for business rules.
- [x] **Location Tracking**: Implemented `AnimalMovement` for tracking Shed/Pen.
- [x] **Animal Detail UI**: Tabbed view for Location, Health, and Breeding.
- [x] **Breeding & Lifecycle**: Mating, Pregnancy, and Calving backend commands and UI dialogs implemented.
- [x] **Sale Transactions**: Record Sale dialog integrated with `SellAnimalCommand`.

---

## 3. Prioritized Implementation Plan (Next Steps)

### Phase 3: Animal Batch & Group Management (Current Task)
1. **Domain**: Create `Batch` aggregate root (Name, FarmId, Type, Status, AnimalCount).
2. **Backend**: Add CQRS commands (`CreateBatchCommand`, `AddAnimalsToBatchCommand`).
3. **Frontend**: Add Batch List and Batch Detail UI.

### Phase 4: Body Condition Score (BCS)
1. **Domain**: Add `BodyConditionScore` child entity to `Animal` (Date, Score 1-5, Notes, Evaluator).
2. **Backend**: Add `RecordBcsCommand` and expose via API.
3. **Frontend**: Add a BCS chart or history table in the Medical/Health tab.
