# Health Module — Animal Picker UX Overhaul

## Problem Statement

Every health module dialog form (Schedule Vaccination, Log Treatment, Record Mortality, Assign Protocol) currently asks the user to type a **raw GUID** into a plain text field labelled "Animal ID". This is completely unusable for a farm manager — no one knows or should need to know the internal database GUID of their animal.

Additionally, the `farmId` is **hardcoded** to `11111111-1111-1111-1111-111111111111` in every dialog, meaning the forms are permanently locked to a single farm regardless of context.

### Current State (All 4 Dialogs)

| Dialog | Animal Field | Farm Context |
|---|---|---|
| Schedule Vaccination | `<input placeholder="e.g. GUID">` | Hardcoded GUID |
| Log Treatment | `<input placeholder="e.g. GUID">` | Hardcoded GUID |
| Record Mortality | `<input placeholder="e.g. GUID">` | Hardcoded GUID |
| Assign Protocol | `<textarea placeholder="e.g. GUID1, GUID2">` | Hardcoded GUID |

---

## Proposed UX: Cascading Location → Animal Picker

### Design Philosophy

A farm manager thinks in this hierarchy:
> "I need to vaccinate **the cow with ear tag BD-2024-0047** in **Shed 2** at **Green Valley Farm**."

The UI should mirror this mental model with a **cascading dropdown flow**:

```
┌─────────────┐     ┌─────────────┐     ┌──────────────────────────┐
│  Select Farm │ ──► │ Select Shed │ ──► │ Search / Select Animal   │
│  (dropdown)  │     │ (optional)  │     │ (autocomplete by tag/name)│
└─────────────┘     └─────────────┘     └──────────────────────────┘
```

### Step-by-Step UX Flow

#### Step 1: Farm Selection (Required)
- A **mat-select dropdown** populated from the existing `FarmService.getAllFarms()` API.
- If the tenant has only 1 farm, it is **auto-selected** and the dropdown is read-only (reduces friction for small farmers).
- Selecting a farm triggers a load of sheds for that farm.

#### Step 2: Shed Selection (Optional Filter)
- A second **mat-select dropdown** populated from the existing sheds-by-farm API.
- Shows "All Sheds" as the default option.
- Selecting a specific shed narrows the animal list to that shed only.
- This step is **optional** — the user can skip it and search all animals in the farm.

#### Step 3: Animal Selection (Required)
- A **mat-autocomplete** (Material autocomplete) field that replaces the raw GUID input.
- The user types a **tag number**, **breed name**, or **any search keyword**.
- The dropdown shows matching animals with rich context per option:
  ```
  ┌──────────────────────────────────────────────┐
  │ 🐄  BD-2024-0047  •  Holstein Friesian       │
  │     Female  •  Shed 2 / Pen A  •  Active     │
  ├──────────────────────────────────────────────┤
  │ 🐄  BD-2024-0051  •  Sahiwal                 │
  │     Male  •  Shed 3 / —  •  Active           │
  └──────────────────────────────────────────────┘
  ```
- Each option displays: **Tag ID**, **Breed**, **Sex**, **Current Shed/Pen**, **Status**.
- On selection, the internal GUID is captured in the form's `animalId` field (hidden from user).
- After selection, a **compact info chip** is shown below the field confirming what was picked:
  ```
  Selected: BD-2024-0047 — Holstein Friesian, Female, Shed 2
  ```

#### Special Case: Assign Protocol Dialog (Multi-Select)
- Instead of a single autocomplete, use a **mat-chip-list with autocomplete**.
- User types to search, selects an animal → it appears as a removable chip.
- Multiple animals can be added one by one.
- Each chip shows the tag ID for quick visual confirmation.

---

## Architectural Approach

### Shared Reusable Component

> [!IMPORTANT]
> Rather than duplicating this picker logic in 4+ dialog components, build it as a **single reusable Angular component** that can be dropped into any form.

#### Component 1: `AnimalPickerComponent` (Single Select)
- **Inputs**: `farmId` (optional pre-selection), `shedId` (optional pre-filter), `required` (boolean)
- **Outputs**: `animalSelected` event emitting the full `AnimalListItemDto`
- **ControlValueAccessor**: Implements `NG_VALUE_ACCESSOR` so it works directly with Reactive Forms (`formControlName="animalId"`)
- Contains the Farm dropdown → Shed dropdown → Autocomplete cascade internally

#### Component 2: `AnimalMultiPickerComponent` (Multi Select)
- Same cascade logic, but outputs `string[]` of animal IDs
- Uses `mat-chip-list` for selected animals
- Used only in Assign Protocol dialog

### Backend: Lightweight Animal Lookup Endpoint

> [!IMPORTANT]
> The existing `AnimalService.getList()` endpoint already supports filtering by `farmId`, `shedId`, `search`, `species`, `status` and returns paginated `AnimalListItemDto` results. **No new backend endpoint is needed.**

The existing endpoint at `GET /api/v1/livestock/animals` with its query parameters perfectly serves this use case:
- `?farmId=xxx` — filter by farm
- `?shedId=xxx` — filter by shed (optional)
- `?search=BD-2024` — search by tag, breed, etc.
- `?pageSize=20` — limit results for autocomplete performance
- `?status=1` — filter to only Active animals (we don't want to vaccinate dead ones)

### Frontend Data Flow

```
Dialog opens
  │
  ├─► FarmService.getAllFarms()  →  populate Farm dropdown
  │
  User selects Farm
  │
  ├─► FarmService.getFarmById(farmId).sheds  →  populate Shed dropdown
  │
  User types in Animal search field (debounced 300ms)
  │
  ├─► AnimalService.getList({ farmId, shedId?, search, status: Active, pageSize: 20 })
  │     →  populate autocomplete options
  │
  User selects animal
  │
  └─► animalId GUID is set in the form (hidden), display chip shown
```

---

## Affected Dialogs (4 total)

| Dialog | Change Summary |
|---|---|
| [schedule-vaccination-dialog](file:///D:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/health/components/dialogs/schedule-vaccination-dialog/schedule-vaccination-dialog.component.ts) | Replace `animalId` text input → `AnimalPickerComponent`. Remove hardcoded farmId. |
| [log-treatment-dialog](file:///D:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/health/components/dialogs/log-treatment-dialog/log-treatment-dialog.component.ts) | Replace `animalId` text input → `AnimalPickerComponent`. Remove hardcoded farmId. |
| [record-mortality-dialog](file:///D:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/health/components/dialogs/record-mortality-dialog/record-mortality-dialog.component.ts) | Replace `animalId` text input → `AnimalPickerComponent`. Filter to Active animals only. Remove hardcoded farmId. |
| [assign-protocol-dialog](file:///D:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/health/components/dialogs/assign-protocol-dialog/assign-protocol-dialog.component.ts) | Replace `animalIds` textarea → `AnimalMultiPickerComponent`. Remove hardcoded farmId. |

---

## File Changes Summary

### New Files

| File | Purpose |
|---|---|
| `shared/components/animal-picker/animal-picker.component.ts` | Single-select animal picker with Farm → Shed → Animal cascade |
| `shared/components/animal-multi-picker/animal-multi-picker.component.ts` | Multi-select variant with chip list |

### Modified Files

| File | Change |
|---|---|
| 4 dialog components (listed above) | Swap raw inputs for picker components |

### No Backend Changes

The existing `GET /api/v1/livestock/animals` endpoint already provides all the filtering, search, and pagination needed.

---

## Open Questions

> [!IMPORTANT]
> **Q1: Should the Shed dropdown also include Pens?**
> The animal model has both `shedId` and `penId`. We could add a third level (Farm → Shed → Pen → Animal), but for most Bangladeshi farms the Shed level is sufficient granularity. Adding Pen filtering would make the cascade 4 levels deep which may be excessive. **Recommendation**: Start with Farm → Shed → Animal. Add Pen later if users request it.

> [!IMPORTANT]
> **Q2: Should we pre-select the farm from the user's current branch context?**
> The sidebar already shows the active Branch. If the user's branch has only one farm, we could auto-select it. This reduces the cascade to just Shed → Animal for single-farm branches. **Recommendation**: Yes, auto-detect from branch context when possible.

---

## Verification Plan

### Manual Verification
- Open each of the 4 dialogs and confirm:
  - Farm dropdown loads with all tenant farms
  - Shed dropdown loads on farm selection
  - Animal autocomplete searches by tag number and breed name
  - Selected animal shows tag ID (not GUID) in the confirmation chip
  - Form submits the correct animal GUID to the backend
  - Assign Protocol dialog allows selecting multiple animals as chips
- Verify the picker works correctly when the tenant has 1 farm (auto-selection)
- Verify the picker filters out non-Active animals for mortality recording

### Automated Verification
```bash
cd src/Farm360.Web && npm run build
```
