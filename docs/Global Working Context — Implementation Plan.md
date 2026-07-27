# Global Working Context — Implementation Plan

## Objective
Implement a robust, persistent **Global Working Context** (Organization → Branch → Farm) that serves as the foundation for the entire Farm360 AI application. This context will drive all module data loading, navigation, and user context.

## Architectural Approach

We will introduce a centralized **`WorkingContextService`** in the `core/services` layer. This service will act as the single source of truth for the active hierarchy. The application's top navigation bar (Header) will house intelligent selectors that allow users (with appropriate permissions) to change their context, which will reactively update all subscribed modules.

### 1. `WorkingContextService` (State Management)
**Path:** `src/app/core/services/working-context.service.ts`

**Responsibilities:**
- Store the currently active `Organization`, `Branch`, and `Farm` as `BehaviorSubject` signals.
- Persist the selected IDs in `localStorage` (`farm360_active_org`, `farm360_active_branch`, `farm360_active_farm`).
- Reactively respond to login/logout events from `AuthService`.
- **Auto-Selection Logic:** 
  - Fetch available Organizations/Branches/Farms on startup based on the user's role and Tenant ID.
  - If a user has access to only *one* Organization, auto-select it.
  - If a selected Organization has only *one* Branch, auto-select it.
  - If a selected Branch has only *one* Farm, auto-select it.
- **Validation:** Upon app startup, ensure the IDs stored in `localStorage` are still valid and accessible by the current user. If invalid, fallback to the auto-selection logic.
- Expose observables (`currentOrg$`, `currentBranch$`, `currentFarm$`) for components to subscribe to.

### 2. Context Selector UI (Global Navigation)
**Path:** `src/app/core/layout/header/header.component.ts` & `.html`

**Changes:**
- Add a new **Context Selector** section to the `HeaderComponent` (top navbar).
- Implement responsive `mat-select` dropdowns for Organization, Branch, and Farm.
- **Visibility Rules:**
  - **Platform Admin:** Shows Tenant (mocked for now), Organization, Branch, Farm.
  - **Org Admin/Owner:** Organization is read-only text (if 1) or hidden. Shows Branch and Farm selectors.
  - **Branch Manager:** Branch is read-only text. Shows Farm selector.
  - **Farm Manager/Employee:** Branch and Farm are read-only text. No dropdowns.
- Changing a parent selector (e.g., Organization) will clear the child selectors (Branch, Farm) and trigger the auto-selection logic.

### 3. Module Refactoring (Integration)

All existing modules will be refactored to consume the `WorkingContextService` instead of relying on URL parameters or hardcoded MVP values.

#### A. Health Module
- **Affected:** `HealthDashboardComponent`, `ScheduleVaccinationDialog`, `LogTreatmentDialog`, `RecordMortalityDialog`, `AssignProtocolDialog`.
- **Change:** Remove the hardcoded `private farmId = '11111111-1111-1111-1111-111111111111';`. Inject `WorkingContextService` and use `currentFarm$.value.id`.
- **Change:** `AnimalPickerComponent` and `AnimalMultiPickerComponent` will automatically pull the active `farmId` from the Context Service instead of maintaining their own `Farm` dropdown. This simplifies the picker to just **Shed → Animal**.

#### B. Livestock Module
- **Affected:** `LivestockDashboardComponent`, `AnimalListComponent`.
- **Change:** Remove any local Farm selectors. Subscribe to `currentFarm$` from the Context Service. When the farm changes, automatically reload the animal list and dashboard metrics.

#### C. Farms/Organizations Module
- **Affected:** `ShedListComponent`, `PenListComponent`, `BranchDetailComponent`.
- **Change:** While these pages still rely on route parameters (e.g., `/organizations/branches/:branchId/farms/:farmId/sheds`), we will ensure they synchronize with the Global Context.

## Verification Plan

### Automated Checks
- Ensure the Angular build (`npm run build`) completes with zero errors after refactoring.
- Ensure strict TypeScript typing is maintained for context models.

### Manual Testing Scenarios
1. **Fresh Login:** Log in as an Org Admin. Verify that if only 1 branch/farm exists, they are auto-selected and persisted to `localStorage`.
2. **Context Switching:** Change the active Farm from the Header. Verify that the current page (e.g., Health Dashboard) instantly reloads its data for the new farm.
3. **Role Restrictions:** Log in as a Farm Employee. Verify that the dropdowns in the Header are disabled/hidden, but the active context is displayed as read-only text.
4. **Picker Integration:** Open the "Log Treatment" dialog. Verify the Farm dropdown is gone, and it correctly uses the Global Farm Context to load Sheds and Animals.
5. **Persistence:** Refresh the browser on the Livestock Dashboard. Verify the previously selected Farm remains active.

## User Review Required

> [!IMPORTANT]
> **API Capabilities:** Does the existing backend `OrganizationService`, `BranchService`, and `FarmService` support fetching lists of entities for the currently authenticated user? Currently, the endpoints are e.g., `/api/v1/organizations` and `/api/v1/organizations/{orgId}/branches`. I will assume these endpoints internally filter by the user's role and tenant, so I can use them to populate the context selectors. Is this correct?

> [!WARNING]
> **"All Farms" Option:** You requested support for an "All Branches" or "All Farms" option for dashboards. The current API endpoints (like `GET /api/v1/livestock/animals`) accept `farmId` as an optional parameter, which theoretically supports cross-farm queries. Should the "All Farms" option be a selectable value in the Header dropdown (e.g., `value=null`), or should it be a separate "Executive Dashboard" page? My plan defaults to adding an "All Farms" option to the dropdown if the user's role permits it.

Please review and approve this plan to begin implementation.
