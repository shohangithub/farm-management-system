# Health & Vaccination Module — Phase 1 Tasks

## 1. Domain Layer Updates
- `[ ]` Update `HealthEnums.cs` (add `CauseOfDeath`, `DewormingFrequency`).
- `[ ]` Create `Events/MortalityRecordedEvent.cs`.
- `[ ]` Create `MortalityRecord.cs` aggregate root.
- `[ ]` Create `VetVisit.cs` aggregate root.

## 2. Persistence Layer Updates
- `[ ]` Create `VaccinationProtocolConfiguration.cs`.
- `[ ]` Create `VaccinationEventConfiguration.cs`.
- `[ ]` Create `MedicalTreatmentConfiguration.cs`.
- `[ ]` Create `DiseaseIncidentConfiguration.cs`.
- `[ ]` Create `MortalityRecordConfiguration.cs`.
- `[ ]` Create `VetVisitConfiguration.cs`.
- `[ ]` Update `ApplicationDbContext.cs` (add DbSets for `MortalityRecord` and `VetVisit`).
- `[ ]` Generate EF Migration (`AddHealthSchema`).

## 3. Application Layer (DTOs & Mappings)
- `[ ]` Update `HealthDtos.cs` with missing DTOs.
- `[ ]` Update `HealthMappingExtensions.cs`.

## 4. Application Layer (Commands & Queries)
- `[ ]` Implement `CreateVaccinationProtocolCommand` & Handler.
- `[ ]` Implement `AssignProtocolToAnimalsCommand` & Handler.
- `[ ]` Implement `RecordMortalityCommand` & Handler.
- `[ ]` Implement `CreateVetVisitCommand` & Handler.
- `[ ]` Implement `UpdateTreatmentStatusCommand` & Handler.
- `[ ]` Implement `GetVaccinationProtocolsQuery` & Handler.
- `[ ]` Implement `GetTreatmentListQuery` & Handler.
- `[ ]` Implement `GetMortalityRecordsQuery` & Handler.
- `[ ]` Implement `GetVetVisitListQuery` & Handler.
- `[ ]` Implement `GetHealthDashboardQuery` & Handler.

## 5. API Layer
- `[ ]` Update `HealthEndpoints.cs` to expose all new commands and queries.

## 6. Frontend Layer (UI Redesign & New Pages)
- `[ ]` Restructure `/health` routes.
- `[ ]` Build `HealthDashboardComponent`.
- `[ ]` Build `VaccinationProtocolListComponent` & `VaccinationProtocolDetailComponent`.
- `[ ]` Redesign `VaccinationDueListComponent`.
- `[ ]` Build `TreatmentListComponent`.
- `[ ]` Build `MortalityListComponent`.
- `[ ]` Build Dialogs (`ScheduleVaccination`, `LogTreatment`, `RecordMortality`, `AssignProtocol`).
- `[ ]` Update `health.service.ts` and models.

## 7. Verification
- `[ ]` Backend builds successfully.
- `[ ]` Tests pass.
- `[ ]` Frontend builds successfully.
