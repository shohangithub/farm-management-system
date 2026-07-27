# Health & Vaccination Module — Phase 2 (Extended) Implementation Plan

Phase 1 (Core) is fully implemented. We are now proceeding with **Phase 2**, which focuses on the extended health management features: Disease Incidents, Milk Withdrawal, Deworming, and Health Reporting.

## User Review Required

> [!IMPORTANT]
> **Database Design for Deworming Calendar**
> The PRD mentions a "Deworming Calendar" (FR-HV-13) but the Database Design document does not specify a distinct table for deworming. 
> **Proposed Solution:** I will model Deworming as a specific type of `VaccinationProtocol` (by adding `IsDeworming = true` or using a `ProtocolType` enum) rather than creating a whole new aggregate, to keep the schema clean and reuse the existing scheduling logic. **Does this approach work for you?**

> [!NOTE]
> **Milk Withdrawal Tracker**
> The DB already supports this via `MedicalTreatment.WithdrawalPeriodDays`. I will implement a dedicated Dashboard widget / Query to list all active dairy animals currently under a milk withdrawal period (where `StartDate + WithdrawalPeriodDays > Today`).

## Proposed Changes

---

### Application Layer (Commands & Queries)

#### [NEW] `Farm360.Application/Health/Queries/DiseaseIncidents/GetDiseaseIncidentListQuery.cs`
- Paginated query to list all disease incidents.

#### [NEW] `Farm360.Application/Health/Queries/DiseaseIncidents/GetDiseaseIncidentDetailQuery.cs`
- Fetches a single disease incident along with the IDs/Details of all affected animals.

#### [NEW] `Farm360.Application/Health/Queries/Deworming/GetDewormingCalendarQuery.cs`
- Fetches all upcoming deworming schedules (Vaccination Events where `Protocol.IsDeworming == true`).

#### [NEW] `Farm360.Application/Health/Queries/Reports/GetMilkWithdrawalAnimalsQuery.cs`
- Returns a list of animals currently under a milk or meat withdrawal period due to recent medical treatments.

#### [NEW] `Farm360.Application/Health/Queries/Reports/GetAnimalHealthReportQuery.cs`
- Aggregates an animal's entire health history (Treatments, Vaccinations, Incidents, Vet Visits) for export.

---

### API Layer

#### [MODIFY] [HealthEndpoints.cs](file:///d:/Personel/Farm Management System/src/Farm360.Api/Endpoints/Health/HealthEndpoints.cs)
- `GET /api/health/incidents` -> `GetDiseaseIncidentListQuery`
- `GET /api/health/incidents/{id}` -> `GetDiseaseIncidentDetailQuery`
- `GET /api/health/deworming/calendar` -> `GetDewormingCalendarQuery`
- `GET /api/health/reports/withdrawals` -> `GetMilkWithdrawalAnimalsQuery`
- `GET /api/health/reports/animal/{id}` -> `GetAnimalHealthReportQuery`

---

### Frontend Layer (Angular UI)

#### [NEW/MODIFY] Disease Incidents CRUD
- **[MODIFY]** `incident-list.component.ts|html`: Completely redesign the existing minimal component to match the Phase 1 Premium UI using Material tables and Tailwind.
- **[NEW]** `incident-detail.component.ts|html`: New page to view an incident and manage the list of affected animals.

#### [NEW] Deworming Calendar Page
- **[NEW]** `deworming-calendar.component.ts|html`: A specialized view (potentially a calendar or list layout) displaying upcoming deworming schedules for all batches/animals.

#### [NEW] Reports & Withdrawal Tracking
- **[NEW]** `milk-withdrawal.component.ts|html`: A page displaying animals whose milk cannot be consumed/sold yet, showing a countdown of remaining withdrawal days.
- **[MODIFY]** `health-dashboard.component.ts|html`: Add a "Milk Withdrawal Alert" widget.

#### [MODIFY] Dialog Form Implementations
- Finish the form logic (Reactive Forms) for the dialogs created in Phase 1:
  - `AssignProtocolDialog`
  - `RecordMortalityDialog`
  - `LogTreatmentDialog`
  - `ScheduleVaccinationDialog`

## Verification Plan

### Automated Tests
- Build both the backend API and Angular workspace to ensure zero compilation errors.

### Manual Verification
- Navigate to the **Disease Incidents** page and create an incident.
- Navigate to the **Milk Withdrawal** page and verify the logic (create a treatment with a withdrawal period and see if it appears).
- Verify the **Deworming Calendar** accurately filters deworming protocols.
- Ensure all forms in the dialogs (e.g., Log Treatment) correctly capture data and hit the backend API.
