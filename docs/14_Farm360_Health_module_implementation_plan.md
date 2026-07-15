# Farm360 — Health & Veterinary Module Implementation Plan

**Governed by:** F360-CONST-2026-001 · F360-MTA-2026-001 · F360-AUTH-2026-001  
**Schema:** `app` (`app.VaccinationProtocols`, `app.VaccinationEvents`, `app.MedicalTreatments`, `app.DiseaseIncidents`, `app.VetVisits`)  
**Pattern:** Clean Architecture · CQRS · DDD · MediatR  
**Status:** IN PROGRESS  

---

## 🏛️ Architecture Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Schema | `app` | Standard business module schema per Migration Guide §10 |
| Core Aggregates | `VaccinationProtocol`, `VaccinationEvent`, `MedicalTreatment`, `DiseaseIncident`, `VetVisit` | Separates protocol templates, active schedules, medical logs, outbreak incidents, and vet visits cleanly per DDD §2.4 |
| Repository Interfaces | `IVaccinationRepository`, `IMedicalTreatmentRepository`, `IDiseaseIncidentRepository` | Encapsulates queries per aggregate root boundary |
| Primary Key | `Guid.NewGuid()` | Guid PKs per Constitution §4.2 |
| Automatic Audits | Inherit `AuditableEntity` | Provides `TenantId`, `CreatedAtUtc`, `CreatedBy`, `IsDeleted`, `RowVersion` soft delete & tenant filter |
| Inventory & Finance Triggers | Domain Events dispatched after commit | Triggers automatic medicine stock deduction (Inventory) & expense auto-posting (Finance) per PRD §6.2 |

---

## 📑 Layer Sequence Overview

| Layer # | Scope | Target Deliverables | Status |
|---|---|---|---|
| **Layer 1** | **Domain Layer** | Enums, Value Objects, Aggregates, Domain Events, Exceptions, Repository Interfaces | 🔄 **Active** |
| **Layer 2** | **Persistence Layer** | EF Configurations, ApplicationDbContext registration, Repository Implementations, Migration | ⬜ Pending |
| **Layer 3** | **Application Layer** | DTOs, CQRS Commands/Queries, Validators, Mappings | ⬜ Pending |
| **Layer 4** | **API Layer** | Minimal API Endpoints (`/api/v1/health/...`), Permission policy enforcement | ⬜ Pending |
| **Layer 5** | **Angular UI** | `HealthService`, TypeScript Models, Pages (Health Dashboard, Treatments, Vaccinations), Routing | ⬜ Pending |
| **Layer 6** | **Tests** | Domain rules unit tests, Command/Query handler tests, Validator tests | ⬜ Pending |

---

## 📁 Layer 1: Domain Layer — File Manifest

```
Farm360.Domain/
└── Health/
    ├── Enums/
    │   ├── VaccinationStatus.cs
    │   ├── TreatmentStatus.cs
    │   ├── IncidentSeverity.cs
    │   ├── IncidentStatus.cs
    │   └── VetVisitType.cs
    ├── ValueObjects/
    │   ├── Dosage.cs               (Amount decimal, Unit string)
    │   └── WithdrawalPeriod.cs     (MilkDays int, MeatDays int)
    ├── Events/
    │   ├── VaccinationScheduledEvent.cs
    │   ├── VaccinationAdministeredEvent.cs
    │   ├── TreatmentLoggedEvent.cs
    │   └── DiseaseIncidentReportedEvent.cs
    ├── Exceptions/
    │   ├── OverlappingTreatmentException.cs
    │   └── DeceasedAnimalHealthRecordException.cs
    ├── VaccinationProtocol.cs      (Aggregate Root - Template)
    ├── VaccinationProtocolStep.cs  (Child Entity)
    ├── VaccinationEvent.cs         (Aggregate Root - Administered / Scheduled)
    ├── MedicalTreatment.cs         (Aggregate Root - Medical Log)
    ├── DiseaseIncident.cs          (Aggregate Root - Incident / Outbreak)
    └── Interfaces/
        └── Repositories/
            ├── IVaccinationRepository.cs
            ├── IMedicalTreatmentRepository.cs
            └── IDiseaseIncidentRepository.cs
```
