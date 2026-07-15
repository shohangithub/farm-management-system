# Farm360 — Livestock Module Implementation Plan

**Governed by:** F360-CONST-2026-001 · F360-MTA-2026-001 · F360-AUTH-2026-001  
**Schema:** `app` (consistent with existing TenantConfiguration — `app.*`)  
**Pattern:** Clean Architecture · CQRS · DDD · MediatR  
**Status:** IN PROGRESS

---

## Architecture Decisions (Before Any Code)

| Decision | Choice | Rationale |
|---|---|---|
| Schema | `app` | Matches existing `TenantConfiguration` → `app.Tenants`. No conflict. |
| Aggregate Root | `Animal` | All animal state changes go through Animal methods |
| Repository | `IAnimalRepository` in Domain; `AnimalRepository` in Persistence | Clean Architecture §2.1 |
| ID generation | `Guid.NewGuid()` in factory method | Never auto-increment; GUID PKs per Constitution §4.2 |
| Photo storage | Soft reference only in `animal.PhotoUrl` (S3 URL) | Full blob service in Phase 2; URL field is sufficient for MVP |
| Farm entity | `FarmId` as a typed foreign key (Farm entity is a TODO — skeleton placeholder) | Livestock depends on Farm; we carry `FarmId` as a Guid reference |

---

## Layer Sequence (One Layer → Wait → Next Layer)

| # | Layer | Status |
|---|---|---|
| **1** | **Domain Layer** — Enums, Entities, Value Objects, Domain Events, Repository Interface | 🔄 IN PROGRESS |
| 2 | Persistence Layer — EF Configuration, Migration, Repository Implementation | ⬜ Pending approval |
| 3 | Application Layer — Interfaces, Commands, Queries, DTOs, Validators, Behaviors | ⬜ Pending |
| 4 | API Layer — Endpoints, PermissionFilter wiring | ⬜ Pending |
| 5 | Angular — Service, Models, Pages (List, Detail, Form), Routing | ⬜ Pending |
| 6 | Tests — Domain unit tests, Application unit tests, Integration tests | ⬜ Pending |

---

## Layer 1: Domain Layer — File Manifest

```
Farm360.Domain/
├── Livestock/
│   ├── Enums/
│   │   ├── AnimalSpecies.cs
│   │   ├── AnimalSex.cs
│   │   ├── AnimalStatus.cs
│   │   ├── AcquisitionType.cs
│   │   ├── DisposalReason.cs
│   │   └── TagType.cs
│   ├── ValueObjects/
│   │   ├── AnimalTag.cs         (TagId string, TagType)
│   │   └── Weight.cs            (WeightKg decimal)
│   ├── Events/
│   │   ├── AnimalRegisteredEvent.cs
│   │   ├── AnimalSoldEvent.cs
│   │   ├── AnimalDiedEvent.cs
│   │   ├── AnimalTransferredEvent.cs
│   │   ├── AnimalQuarantinedEvent.cs
│   │   └── WeightRecordedEvent.cs
│   ├── Exceptions/
│   │   ├── AnimalQuarantinedException.cs
│   │   ├── InvalidAnimalStateTransitionException.cs
│   │   └── DuplicateAnimalTagException.cs
│   ├── Animal.cs                (Aggregate Root)
│   ├── WeightRecord.cs          (Child entity)
│   ├── BreedingRecord.cs        (Child entity)
│   └── AnimalPhoto.cs           (Child entity)
└── Interfaces/
    └── Repositories/
        └── IAnimalRepository.cs
```
