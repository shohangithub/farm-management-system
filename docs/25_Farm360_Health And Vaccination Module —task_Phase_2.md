# Health & Vaccination Module — Phase 2 Tasks

## 1. Domain Layer Updates
- `[x]` Update `VaccinationProtocol` to include `IsDeworming` flag (or `ProtocolType`).
- `[x]` Add `AffectedAnimalIds` to `DiseaseIncident`.

## 2. Persistence Layer Updates
- `[x]` Generate EF Migration for Domain updates (if required).
- `[x]` Implement missing repository methods.

## 3. Application Layer (Queries & Commands)
- `[x]` Implement `GetDiseaseIncidentListQuery.cs`.
- `[x]` Implement `GetDiseaseIncidentDetailQuery.cs`.
- `[x]` Implement `GetDewormingCalendarQuery.cs`.
- `[x]` Implement `GetMilkWithdrawalAnimalsQuery.cs`.
- `[x]` Implement `GetAnimalHealthReportQuery.cs`.
- `[x]` Verify/Update `ReportDiseaseIncidentCommand.cs` and `UpdateDiseaseIncidentCommand.cs`.

## 4. API Layer
- `[x]` Update `HealthEndpoints.cs` to expose new Phase 2 queries and commands.

## 5. Frontend Layer
- `[x]` Create/Modify `IncidentListComponent`.
- `[x]` Create `IncidentDetailComponent`.
- `[x]` Create `DewormingCalendarComponent`.
- `[x]` Create `MilkWithdrawalComponent`.
- `[x]` Update routing and navigation logic.
- `[x]` Verify API integration end-to-end.
- `[x]` Implement Dialog Forms (`AssignProtocol`, `RecordMortality`, `LogTreatment`, `ScheduleVaccination`).

## 6. Verification
- `[x]` Backend builds successfully.
- `[x]` Frontend builds successfully.
