# Health & Vaccination Module — Implementation Plan

**Module:** Health & Veterinary  
**PRD Features:** F-035 through F-047 (13 features)  
**Functional Requirements:** FR-HV-01 through FR-HV-14  
**Business Rules:** BRU-HV-01 through BRU-HV-05  
**User Stories:** US-HV-01 through US-HV-06  

---

## Rationale: Why Health Module is Next

Per the Module Dependency Map (PRD §6.1), after Livestock the next natural module is **Health & Vaccination**. It has:
- A direct dependency on the Livestock module (animals must exist before health records can be created).
- No dependency on Inventory or Finance (those modules depend on Health via domain events for auto-deduction, not the reverse).
- The highest user impact among the remaining modules — farm managers need vaccination tracking to prevent livestock losses.

---

## Current State — Gap Analysis

### What Already Exists ✅

| Layer | Status | Details |
|---|---|---|
| **Domain Layer** | ~80% complete | 4 Aggregate Roots: `VaccinationProtocol` (with `VaccinationProtocolStep` child), `VaccinationEvent`, `MedicalTreatment`, `DiseaseIncident`. Enums: `VaccinationStatus`, `TreatmentStatus`, `IncidentSeverity`, `IncidentStatus`, `VetVisitType`. Domain Events: 4 events. Value Objects: `Dosage`, `WithdrawalPeriod`. |
| **Application Layer** | ~40% complete | Commands: `ScheduleVaccinationCommand`, `RecordVaccinationAdministrationCommand`, `LogMedicalTreatmentCommand`, `ReportDiseaseIncidentCommand`. Queries: `GetUpcomingVaccinationsQuery`, `GetAnimalHealthHistoryQuery`. DTOs: `HealthDtos.cs`. Mappings: `HealthMappingExtensions.cs`. |
| **API Layer** | ~35% complete | 5 endpoints: POST `/vaccinations/schedule`, PUT `/vaccinations/{id}/administer`, GET `/vaccinations/upcoming`, POST `/treatments`, POST `/incidents`, GET `/animals/{id}/history`. |
| **Persistence** | ~30% complete | 4 DbSets registered (`VaccinationProtocols`, `VaccinationEvents`, `MedicalTreatments`, `DiseaseIncidents`). No dedicated EF Configurations. No migrations for `health` schema tables. |
| **Angular Frontend** | ~20% complete | Basic routing (`/health/vaccinations`, `/health/incidents`). Models/enums exist. `HealthService` with 4 API calls. 2 minimal pages: `VaccinationDueListComponent`, `ReportIncidentComponent` (unstyled, old design system). |

### What's Missing ❌

| Gap | PRD Requirement | Priority |
|---|---|---|
| **VetVisit Aggregate** (Domain + all layers) | FR-HV-09 | Must Have |
| **MortalityRecord Aggregate** (Domain + all layers) | FR-HV-10 | Must Have |
| **Vaccination Protocol Builder UI** | FR-HV-01, F-035 | Must Have |
| **Protocol Assignment** (to animals/batches/sheds) | FR-HV-02, F-036 | Must Have |
| **Auto Vaccination Schedule Generator** | FR-HV-03, F-037 | Must Have |
| **Vaccination Due Alerts** (in-app notifications) | FR-HV-04, F-038 | Must Have |
| **Treatment List/Detail UI** | FR-HV-06, F-040 | Must Have |
| **Disease Incident Manager UI** (list + detail + link animals) | FR-HV-08, F-041 | Must Have |
| **Deworming Calendar** | FR-HV-13, F-044 | Must Have |
| **Health Status Dashboard** | FR-HV-12, F-045 | Must Have |
| **Animal Health Report (export)** | FR-HV-11, F-046 | Must Have |
| **Milk Withdrawal Tracker** | FR-HV-14, F-047 | Should Have |
| **EF Configurations** for all Health entities | Architecture | Must Have |
| **Database Migration** (`health` schema) | Architecture | Must Have |
| **FluentValidation** for all Commands | Quality | Must Have |
| **Pagination** on list queries | Performance | Must Have |
| **Full UI Redesign** of existing Health pages | UX | Must Have |

---

## User Review Required

> [!IMPORTANT]
> This is a large module with 13 features. I recommend a **phased approach** split into 2 sprints:
> - **Phase 1 (Core):** Vaccination Protocols, Vaccination Events, Medical Treatments, Mortality Records, EF Configurations, Migration, full Angular UI redesign.
> - **Phase 2 (Extended):** Disease Incidents CRUD, Vet Visits, Deworming Calendar, Health Dashboard, Milk Withdrawal, Reports/Export.
>
> **Do you want to proceed with Phase 1 first, or implement the entire module in one pass?**

> [!WARNING]
> The existing 2 Angular pages (`VaccinationDueListComponent`, `ReportIncidentComponent`) are unstyled with the old design system. They will be **completely rewritten** to match the Livestock module's premium design language (Tailwind + Material).

---

## Proposed Changes — Phase 1 (Core)

---

### Domain Layer

#### [MODIFY] [HealthEnums.cs](file:///d:/Personel/Farm Management System/src/Farm360.Domain/Health/Enums/HealthEnums.cs)
- Add `CauseOfDeath` enum: `Disease`, `Accident`, `NaturalCauses`, `Unknown`, `Slaughter` (per DB §14.9).
- Add `DewormingFrequency` enum: `Monthly`, `Quarterly`, `BiAnnual`, `Annual`, `Custom`.

#### [NEW] `Farm360.Domain/Health/MortalityRecord.cs`
- Aggregate Root per DB Design §14.9.
- Properties: `AnimalId`, `DeathDate`, `CauseOfDeath`, `DiseaseName`, `PostMortemNotes`, `EstimatedEconomicLossBdt`, `DiseaseIncidentId`, `RecordedByUserId`.
- Business Rule BRU-HV-04: Validates the animal is not already deceased.
- Domain Event: `AnimalDeathRecordedEvent` → triggers animal status change to `Dead`.

#### [NEW] `Farm360.Domain/Health/VetVisit.cs`
- Aggregate Root per DB Design §14.8.
- Properties: `FarmId`, `VetName`, `VisitDate`, `VisitType`, `Purpose`, `Findings`, `Recommendations`, `CostBdt`, `NextVisitDate`.

#### [NEW] `Farm360.Domain/Health/Events/MortalityRecordedEvent.cs`
- New domain event for cross-module integration (Livestock status change, Finance auto-posting).

---

### Persistence Layer

#### [NEW] `Farm360.Persistence/Configurations/Health/VaccinationProtocolConfiguration.cs`
- Schema: `health`, Table: `VaccinationProtocols`.
- Owns `VaccinationProtocolStep` as child entity table `health.ProtocolScheduleItems`.
- Indexes per DB Design §14.1, §14.2.

#### [NEW] `Farm360.Persistence/Configurations/Health/VaccinationEventConfiguration.cs`
- Schema: `health`, Table: `VaccinationRecords`.
- FK to `Animals`, nullable FK to `ProtocolScheduleItems`.
- Indexes per DB Design §14.4.

#### [NEW] `Farm360.Persistence/Configurations/Health/MedicalTreatmentConfiguration.cs`
- Schema: `health`, Table: `TreatmentRecords`.
- FK to `Animals`. Indexes per DB Design §14.5.

#### [NEW] `Farm360.Persistence/Configurations/Health/DiseaseIncidentConfiguration.cs`
- Schema: `health`, Table: `DiseaseIncidents` + join table `DiseaseIncidentAnimals`.
- Indexes per DB Design §14.6, §14.7.

#### [NEW] `Farm360.Persistence/Configurations/Health/MortalityRecordConfiguration.cs`
- Schema: `health`, Table: `MortalityRecords`.
- Unique constraint on `AnimalId` (one death per animal).
- Indexes per DB Design §14.9.

#### [NEW] `Farm360.Persistence/Configurations/Health/VetVisitConfiguration.cs`
- Schema: `health`, Table: `VetVisits`.
- Indexes per DB Design §14.8.

#### [NEW] EF Migration
- `dotnet ef migrations add AddHealthSchema` to create all 7+ health tables.

---

### Application Layer

#### [NEW] Commands (with FluentValidation)
| Command | Description |
|---|---|
| `CreateVaccinationProtocolCommand` | Creates a protocol template with steps |
| `AssignProtocolToAnimalsCommand` | Assigns a protocol to a list of animal IDs, generating schedule records |
| `RecordMortalityCommand` | Records animal death, triggers status change |
| `CreateVetVisitCommand` | Logs a vet visit to a farm |
| `UpdateTreatmentStatusCommand` | Complete/fail a treatment |
| `UpdateDiseaseIncidentCommand` | Update incident status/resolution |

#### [NEW] Queries
| Query | Description |
|---|---|
| `GetVaccinationProtocolsQuery` | Paginated list of protocols for the tenant |
| `GetVaccinationProtocolDetailQuery` | Single protocol with steps |
| `GetTreatmentListQuery` | Paginated treatment records with filtering |
| `GetDiseaseIncidentListQuery` | Paginated disease incidents |
| `GetDiseaseIncidentDetailQuery` | Single incident with linked animals |
| `GetMortalityRecordsQuery` | Paginated mortality records |
| `GetVetVisitListQuery` | Paginated vet visits per farm |
| `GetHealthDashboardQuery` | Aggregated stats: due vaccinations, active treatments, quarantined animals, recent mortality |

#### [MODIFY] [HealthDtos.cs](file:///d:/Personel/Farm Management System/src/Farm360.Application/Health/DTOs/HealthDtos.cs)
- Add `VaccinationProtocolDto`, `VaccinationProtocolStepDto`, `MortalityRecordDto`, `VetVisitDto`, `HealthDashboardDto`.

---

### API Layer

#### [MODIFY] [HealthEndpoints.cs](file:///d:/Personel/Farm Management System/src/Farm360.Api/Endpoints/Health/HealthEndpoints.cs)
- Add the following endpoints:

| Method | Path | Description |
|---|---|---|
| `POST` | `/protocols` | Create vaccination protocol |
| `GET` | `/protocols` | List protocols (paginated) |
| `GET` | `/protocols/{id}` | Get protocol detail |
| `POST` | `/protocols/{id}/assign` | Assign protocol to animals |
| `GET` | `/treatments` | List treatments (paginated, filterable) |
| `PUT` | `/treatments/{id}/status` | Update treatment status |
| `GET` | `/incidents` | List incidents (paginated) |
| `GET` | `/incidents/{id}` | Get incident detail |
| `PUT` | `/incidents/{id}` | Update incident |
| `POST` | `/mortality` | Record animal death |
| `GET` | `/mortality` | List mortality records |
| `POST` | `/vet-visits` | Create vet visit |
| `GET` | `/vet-visits` | List vet visits |
| `GET` | `/dashboard` | Health dashboard stats |

---

### Angular Frontend

#### Routing Structure
```
/health                        → Health Dashboard (overview stats)
/health/vaccinations           → Vaccination Due List (redesigned)
/health/vaccinations/protocols → Protocol List
/health/vaccinations/protocols/:id → Protocol Detail
/health/treatments             → Treatment List
/health/incidents              → Incident List
/health/incidents/:id          → Incident Detail
/health/mortality              → Mortality Records
/health/vet-visits             → Vet Visit List
```

#### [NEW] Angular Pages
| Component | Description |
|---|---|
| `HealthDashboardComponent` | Stats cards: due vaccinations, active treatments, quarantined animals, recent deaths |
| `VaccinationProtocolListComponent` | CRUD for protocol templates |
| `VaccinationProtocolDetailComponent` | View/edit protocol with steps |
| `TreatmentListComponent` | Paginated treatment records table |
| `MortalityListComponent` | Paginated mortality records |
| `VetVisitListComponent` | Paginated vet visit records |
| `IncidentListComponent` | Paginated incident list (redesigned) |
| `IncidentDetailComponent` | Incident detail with linked animals |

#### [NEW] Angular Dialogs
| Dialog | Description |
|---|---|
| `ScheduleVaccinationDialogComponent` | Schedule a vaccination for an animal |
| `LogTreatmentDialogComponent` | Log a medical treatment |
| `RecordMortalityDialogComponent` | Record an animal death |
| `CreateVetVisitDialogComponent` | Log a vet visit |
| `AssignProtocolDialogComponent` | Assign protocol to animals/batches |

#### [MODIFY] Existing Pages (Complete Redesign)
- `VaccinationDueListComponent` → Full redesign with Tailwind + Material design system matching Livestock module.
- `ReportIncidentComponent` → Full redesign, extract to standalone dialog.

#### [MODIFY] [health.models.ts](file:///d:/Personel/Farm Management System/src/Farm360.Web/src/app/features/health/models/health.models.ts)
- Add `VaccinationProtocolDto`, `VaccinationProtocolStepDto`, `MortalityRecordDto`, `VetVisitDto`, `HealthDashboardDto`.

#### [MODIFY] [health.service.ts](file:///d:/Personel/Farm Management System/src/Farm360.Web/src/app/features/health/services/health.service.ts)
- Add API calls for all new endpoints.

#### [MODIFY] [sidebar.component.ts](file:///d:/Personel/Farm Management System/src/Farm360.Web/src/app/core/layout/sidebar/sidebar.component.ts)
- Ensure `Health` nav item is active (it currently is, route `/health`).

---

## Verification Plan

### Automated Tests
```bash
# Backend build
dotnet build Farm360.sln

# Run all tests
dotnet test Farm360.sln --verbosity normal

# Frontend build
cd src/Farm360.Web && npm run build
```

### Manual Verification
- Navigate to `/health` and verify the Health Dashboard renders with summary cards.
- Create a Vaccination Protocol with 3 steps and verify it appears in the protocol list.
- Assign the protocol to an animal and verify vaccination schedule records are generated.
- Record a vaccination administration and verify the schedule status updates.
- Log a medical treatment and verify it appears in the treatment list.
- Record an animal death and verify the animal's status changes to `Dead` in the Livestock module.
- Create a vet visit and verify it appears in the vet visit list.
- Verify all pages are responsive across desktop, tablet, and mobile.
