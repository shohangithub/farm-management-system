# Farm360 AI — Complete Solution Structure

**Document ID:** F360-SOL-2026-001  
**Version:** 1.0  
**Authority:** Chief Software Architect — Farm360 AI  
**Date:** July 2026  
**Governed by:** F360-CONST-2026-001 (Project Constitution)  
**Classification:** Confidential — Engineering Reference  

---

> *"A solution structure is an architectural contract. Every project boundary is a deliberate decision about who owns what, who depends on whom, and what can change independently. Design it for the team you will have in Year 5, not just the team you have today."*

---

## Table of Contents

1. [Solution Overview](#1-solution-overview)
2. [Complete Solution Layout](#2-complete-solution-layout)
3. [Project Explanations — Why Each Project Exists](#3-project-explanations)
4. [Dependency Diagram](#4-dependency-diagram)
5. [Project Reference Map](#5-project-reference-map)
6. [NuGet Package Strategy](#6-nuget-package-strategy)
7. [Angular Workspace Structure](#7-angular-workspace-structure)
8. [Test Project Strategy](#8-test-project-strategy)
9. [Build Order & CI Pipeline Project Graph](#9-build-order--ci-pipeline)
10. [10-Year Maintainability Notes](#10-10-year-maintainability-notes)

---

## 1. Solution Overview

### 1.1 Technology Decisions (from ADRs)

| Technology | Choice | ADR |
|---|---|---|
| Backend Runtime | ASP.NET Core 10 (.NET 10) | ADR-001 |
| Frontend | Angular 22 (PWA, Standalone Components) | ADR-011 |
| Database | SQL Server 2022 (AWS RDS) | ADR-012 |
| Architecture Pattern | Clean Architecture + Modular Monolith | ADR-001, ADR-002 |
| State Management (backend) | CQRS with MediatR | ADR-002 |
| Domain Modelling | Domain-Driven Design | Constitution §2.4 |
| ORM | Entity Framework Core 10 | DDD §1.2 |
| Background Jobs | Hangfire (SQL Server persistence) | ADR-007 |
| Caching | Redis (StackExchange.Redis) | ADR-004 |
| Real-time | SignalR + Redis Backplane | ADR-006 |
| Authentication | JWT RS256 + Rotating Refresh Tokens | ADR-008 |
| Logging | Serilog → CloudWatch / Seq | ADR-009 |
| Validation | FluentValidation + MediatR Pipeline | ADR-010 |

### 1.2 Project Count Summary

| Category | Count |
|---|---|
| Backend source projects | 9 |
| Frontend workspace | 1 |
| Test projects | 5 |
| **Total** | **15** |

---

## 2. Complete Solution Layout

```
Farm360.sln
│
├── src/
│   ├── Farm360.Domain/
│   ├── Farm360.Application/
│   ├── Farm360.Contracts/
│   ├── Farm360.Infrastructure/
│   ├── Farm360.Persistence/
│   ├── Farm360.Identity/
│   ├── Farm360.Api/
│   ├── Farm360.Shared/
│   └── Farm360.Web/                        ← Angular 22 PWA (npm workspace)
│
├── tests/
│   ├── Farm360.Domain.UnitTests/
│   ├── Farm360.Application.UnitTests/
│   ├── Farm360.Application.IntegrationTests/
│   ├── Farm360.Api.FunctionalTests/
│   └── Farm360.Architecture.Tests/
│
├── tools/
│   ├── scripts/
│   │   ├── migrate.ps1
│   │   ├── seed.ps1
│   │   └── build-docker.ps1
│   └── k8s/
│       ├── api-deployment.yaml
│       ├── migrator-job.yaml
│       └── redis-deployment.yaml
│
├── docs/
│   ├── 0_Farm360_AI_Project_Constitution.md
│   ├── 1_Farm360_AI_Product_Vision_Document.md
│   ├── 2_Farm360_AI_Product_Requirements_Document.md
│   ├── 3_Farm360_AI_Software_Architecture_Document.md
│   ├── 4_Farm360_AI_Database_Design_Document.md
│   ├── 5_Farm360_AI_UX_Design_System.md
│   └── adr/
│       ├── ADR-001-Modular-Monolith.md
│       ├── ADR-002-CQRS-MediatR.md
│       └── ...
│
├── .github/
│   └── workflows/
│       ├── ci.yml
│       ├── cd-staging.yml
│       └── cd-production.yml
│
├── docker-compose.yml                       ← Local dev environment
├── docker-compose.override.yml
├── .editorconfig
├── .gitignore
├── Directory.Build.props                    ← Shared MSBuild properties
├── Directory.Packages.props                 ← Central Package Management
└── global.json                              ← .NET SDK version pin
```

---

## 3. Project Explanations

### 3.1 `Farm360.Shared`

**Type:** Class Library  
**Layer:** Cross-Cutting (no layer — referenced by all layers)

**Why it exists:**  
Every large solution needs a set of primitives that are so foundational they must be available everywhere without creating circular dependencies. `Farm360.Shared` is that zero-dependency foundation. It contains ONLY compile-time, infrastructure-free concepts.

**Contents:**
```
Farm360.Shared/
├── Constants/
│   ├── TimeZones.cs             (Asia/Dhaka, timezone IDs)
│   ├── CurrencyCodes.cs         (BDT, USD)
│   ├── DateFormats.cs           (DD/MM/YYYY)
│   └── FeatureFlags.cs          (feature flag key constants)
│
├── Extensions/
│   ├── StringExtensions.cs      (IsNullOrWhiteSpace, Truncate)
│   ├── DateTimeExtensions.cs    (ToUtc, ToLocalBangladesh)
│   ├── GuidExtensions.cs        (IsEmpty)
│   └── DecimalExtensions.cs     (RoundBdt, ToMoneyString)
│
├── Guards/
│   └── Guard.cs                 (Guard.Against.Null, Guard.Against.NegativeOrZero)
│
├── Primitives/
│   ├── Result.cs                (Result<T>, Result.Success, Result.Failure)
│   ├── Error.cs                 (typed error descriptor: Code + Message)
│   ├── PagedRequest.cs          (PageNumber, PageSize)
│   └── PaginatedResult.cs       (Items, TotalCount, TotalPages, etc.)
│
└── Attributes/
    └── SensitiveDataAttribute.cs  (marks PII for Serilog masking)
```

**Dependencies:** None  
**Referenced by:** All other projects  
**10-year note:** This project must NEVER grow large. If something doesn't fit in 30 lines, it belongs elsewhere. Resist feature creep here — it is depended on by everything.

---

### 3.2 `Farm360.Domain`

**Type:** Class Library  
**Layer:** Domain (innermost)

**Why it exists:**  
This is the heart of the system. It contains all business knowledge — what an Animal is, when it can be sold, what makes a valid Feed Formula, what a quarantine restriction means. This layer is the reason the software has value. It must be pure: no frameworks, no databases, no HTTP, no external dependencies. Only C# and `MediatR.Contracts` (for `INotification` on domain events).

If a developer can understand this project without knowing anything about ASP.NET, EF Core, or Angular — the architecture is correct.

**Contents:**
```
Farm360.Domain/
├── Common/
│   ├── BaseEntity.cs            (Id: Guid, domain events collection)
│   ├── AuditableEntity.cs       (extends BaseEntity + 6 audit columns as value-tracked props)
│   ├── BaseValueObject.cs       (structural equality via GetEqualityComponents())
│   ├── IAggregateRoot.cs        (marker interface)
│   └── ITenantEntity.cs         (TenantId: Guid — marks tenant-scoped entities)
│
├── Aggregates/
│   ├── AnimalAggregate/
│   │   ├── Animal.cs            (Aggregate Root)
│   │   ├── WeightRecord.cs
│   │   ├── BreedingRecord.cs
│   │   ├── AnimalTransferLog.cs
│   │   └── AnimalPhoto.cs
│   ├── BatchAggregate/
│   │   ├── AnimalBatch.cs
│   │   └── AnimalBatchMember.cs
│   ├── FarmAggregate/
│   │   ├── Farm.cs
│   │   ├── Shed.cs
│   │   └── Pen.cs
│   ├── FeedingAggregate/
│   │   ├── FeedIngredient.cs
│   │   ├── FeedFormula.cs
│   │   ├── FormulaIngredient.cs
│   │   ├── FeedingSchedule.cs
│   │   ├── FeedConsumptionLog.cs
│   │   └── ConsumptionDetail.cs
│   ├── HealthAggregate/
│   │   ├── VaccinationProtocol.cs
│   │   ├── ProtocolScheduleItem.cs
│   │   ├── AnimalVaccinationSchedule.cs
│   │   ├── VaccinationRecord.cs
│   │   ├── TreatmentRecord.cs
│   │   ├── DiseaseIncident.cs
│   │   ├── DiseaseIncidentAnimal.cs
│   │   ├── VetVisit.cs
│   │   └── MortalityRecord.cs
│   ├── InventoryAggregate/
│   │   ├── InventoryItem.cs
│   │   ├── Supplier.cs
│   │   ├── StockBatch.cs
│   │   └── StockTransaction.cs
│   ├── FinanceAggregate/
│   │   ├── ChartOfAccount.cs
│   │   ├── FinancialEntry.cs
│   │   ├── AnimalCostLedger.cs
│   │   ├── BatchProfitLoss.cs
│   │   ├── LoanRecord.cs
│   │   └── LoanRepayment.cs
│   └── TenantAggregate/
│       ├── Tenant.cs
│       ├── Organization.cs
│       ├── OrganizationUser.cs
│       ├── UserInvitation.cs
│       ├── SubscriptionPlan.cs
│       ├── Subscription.cs
│       └── BillingRecord.cs
│
├── ValueObjects/
│   ├── Money.cs                 (Amount: decimal, Currency: string)
│   ├── AnimalTag.cs             (TagId: string, TagType: TagType)
│   ├── Weight.cs                (WeightKg: decimal, WeightUnit: WeightUnit)
│   ├── NutritionalProfile.cs    (DryMatterPct, CrudeProteinPct, MetabEnergy)
│   ├── Address.cs               (Line1, Line2, Upazila, District, Division, PostalCode)
│   ├── DateRange.cs             (StartDate, EndDate)
│   ├── BodyConditionScore.cs    (Value: decimal — 1.0 to 5.0)
│   ├── GestationPeriod.cs       (ExpectedCalvingDate: DateOnly)
│   └── PhoneNumber.cs           (Value: string — +880 prefix validated)
│
├── DomainEvents/
│   ├── Animals/
│   │   ├── AnimalRegisteredEvent.cs
│   │   ├── AnimalSoldEvent.cs
│   │   ├── AnimalDiedEvent.cs
│   │   ├── AnimalQuarantinedEvent.cs
│   │   ├── AnimalTransferredEvent.cs
│   │   └── WeightRecordedEvent.cs
│   ├── Feeding/
│   │   ├── FeedConsumptionLoggedEvent.cs
│   │   └── FeedFormulaAssignedEvent.cs
│   ├── Health/
│   │   ├── VaccinationRecordedEvent.cs
│   │   ├── TreatmentRecordedEvent.cs
│   │   └── VaccinationProtocolAssignedEvent.cs
│   ├── Inventory/
│   │   ├── StockReceivedEvent.cs
│   │   └── StockDeductedEvent.cs
│   ├── Finance/
│   │   └── FinancialEntryPostedEvent.cs
│   └── Tenants/
│       ├── TenantRegisteredEvent.cs
│       └── SubscriptionChangedEvent.cs
│
├── Enumerations/
│   ├── AnimalStatus.cs          (Active, Sold, Slaughtered, Dead, Quarantined, Transferred)
│   ├── AnimalSpecies.cs         (CattleBeef, CattleDairy, Goat, Sheep)
│   ├── AnimalSex.cs             (Male, Female)
│   ├── AcquisitionType.cs       (Purchased, BornOnFarm)
│   ├── DisposalReason.cs        (Sale, Slaughter, NaturalDeath, Disease, Accident, Unknown)
│   ├── TagType.cs               (Manual, EarTag, RFID)
│   ├── BreedingMethod.cs        (Natural, ArtificialInsemination)
│   ├── InventoryCategory.cs     (Feed, Medicine, Chemical, Equipment, Other)
│   ├── StockTransactionType.cs
│   ├── FinancialEntryType.cs    (Income, Expense)
│   ├── FinancialEntrySource.cs  (Manual, AnimalSale, FeedConsumption, ...)
│   ├── SubscriptionTier.cs      (Bittho, Khamar, Banik, Corporation, NGO)
│   ├── UserRole.cs              (Owner, FarmManager, Veterinarian, Worker, Accountant, Viewer)
│   ├── TenantStatus.cs          (Active, Suspended, Deleted)
│   └── CalvingOutcome.cs        (Live, Stillborn, Abortion)
│
├── Interfaces/
│   ├── Repositories/
│   │   ├── IGenericRepository.cs
│   │   ├── IAnimalRepository.cs
│   │   ├── IFarmRepository.cs
│   │   ├── IFeedFormulaRepository.cs
│   │   ├── IHealthRepository.cs
│   │   ├── IInventoryRepository.cs
│   │   ├── IFinanceRepository.cs
│   │   └── ITenantRepository.cs
│   └── DomainServices/
│       ├── IFcrCalculationService.cs
│       ├── IAdgCalculationService.cs
│       ├── IBreakEvenCalculatorService.cs
│       ├── IWeightedAverageCostService.cs
│       ├── IVaccinationScheduleService.cs
│       ├── IBatchProfitLossService.cs
│       ├── IAnimalCostPostingService.cs
│       ├── IStockDeductionService.cs
│       └── ISubscriptionLimitService.cs
│
├── DomainServices/
│   ├── FcrCalculationService.cs
│   ├── AdgCalculationService.cs
│   ├── BreakEvenCalculatorService.cs
│   └── WeightedAverageCostService.cs
│
└── Exceptions/
    ├── DomainException.cs               (base)
    ├── AnimalQuarantinedException.cs
    ├── InvalidAnimalStateTransitionException.cs
    ├── InsufficientStockException.cs
    ├── ClosedPeriodModificationException.cs
    ├── SubscriptionLimitExceededException.cs
    └── DuplicateAnimalTagException.cs
```

**Dependencies:** `Farm360.Shared`, `MediatR.Contracts`  
**Referenced by:** `Farm360.Application`, `Farm360.Persistence`, `Farm360.Identity`, `Farm360.Infrastructure`, `Farm360.Contracts`  
**10-year note:** This is the most stable project in the solution. A method signature change here cascades everywhere. Treat every public API on a domain entity as a permanent contract. Break it only with a migration plan.

---

### 3.3 `Farm360.Application`

**Type:** Class Library  
**Layer:** Application

**Why it exists:**  
This layer is the use-case engine. It knows WHAT the system does, but not HOW it does it. It defines the MediatR pipeline, all Commands/Queries/Handlers, FluentValidation validators, and the service interfaces that Infrastructure must implement. This is where business workflows live — not business rules (those are in Domain), but the orchestration of those rules.

**Contents:**
```
Farm360.Application/
├── Common/
│   ├── Behaviors/
│   │   ├── LoggingBehavior.cs           (IPipelineBehavior — logs command name)
│   │   ├── ValidationBehavior.cs        (IPipelineBehavior — runs FluentValidation)
│   │   ├── PerformanceBehavior.cs       (IPipelineBehavior — stopwatch + warnings)
│   │   ├── TransactionBehavior.cs       (IPipelineBehavior — wraps Commands in TX)
│   │   ├── CachingBehavior.cs           (IPipelineBehavior — returns from Redis)
│   │   └── AuditBehavior.cs             (IPipelineBehavior — pre/post state capture)
│   │
│   ├── Exceptions/
│   │   ├── AppException.cs              (base application exception)
│   │   ├── NotFoundException.cs         (→ HTTP 404)
│   │   ├── ValidationException.cs       (→ HTTP 422 + field map)
│   │   ├── ForbiddenAccessException.cs  (→ HTTP 403)
│   │   ├── ConflictException.cs         (→ HTTP 409)
│   │   └── TenantSuspendedException.cs  (→ HTTP 402)
│   │
│   ├── Interfaces/
│   │   ├── ICurrentUserService.cs       (UserId, TenantId, Role, AssignedFarmIds)
│   │   ├── ITenantService.cs            (CurrentTenantId, SetTenant, GetTenantInfo)
│   │   ├── IDateTimeService.cs          (UtcNow, BangladeshNow)
│   │   ├── ICacheService.cs             (Get, Set, Remove, RemoveByPrefix)
│   │   ├── IEmailService.cs             (SendAsync)
│   │   ├── ISmsService.cs               (SendOtpAsync, SendNotificationAsync)
│   │   ├── IBlobStorageService.cs       (UploadAsync, DeleteAsync, GetPresignedUrl)
│   │   ├── IAuditService.cs             (RecordAsync)
│   │   ├── INotificationService.cs      (SendToTenantAsync, SendToUserAsync)
│   │   └── IBackgroundJobService.cs     (Enqueue, Schedule, RecurringJob)
│   │
│   ├── Mappings/
│   │   └── MappingProfile.cs            (AutoMapper profile — all DTO mappings)
│   │
│   └── Models/
│       └── ICacheableQuery.cs           (marker interface + CacheKey, CacheDuration)
│
└── Features/
    ├── Animals/
    │   ├── Commands/
    │   │   ├── RegisterAnimal/
    │   │   │   ├── RegisterAnimalCommand.cs
    │   │   │   ├── RegisterAnimalCommandHandler.cs
    │   │   │   └── RegisterAnimalCommandValidator.cs
    │   │   ├── RecordWeight/
    │   │   ├── SellAnimal/
    │   │   ├── RecordAnimalDeath/
    │   │   ├── TransferAnimal/
    │   │   ├── QuarantineAnimal/
    │   │   ├── AssignToShed/
    │   │   └── UpdateAnimalProfile/
    │   ├── Queries/
    │   │   ├── GetAnimalById/
    │   │   │   ├── GetAnimalByIdQuery.cs
    │   │   │   ├── GetAnimalByIdQueryHandler.cs
    │   │   │   └── AnimalDetailDto.cs
    │   │   ├── GetAnimalsByFilter/
    │   │   ├── GetAnimalTimeline/
    │   │   ├── GetAnimalCostLedger/
    │   │   └── GetAnimalWeightHistory/
    │   └── EventHandlers/
    │       ├── AnimalSoldEventHandler.cs   (→ posts to Finance)
    │       └── WeightRecordedEventHandler.cs (→ updates ADG)
    │
    ├── Batches/
    │   ├── Commands/ (CreateBatch, AddAnimalToBatch, CompleteBatch)
    │   └── Queries/ (GetBatchById, GetBatchProfitLoss, GetBatchList)
    │
    ├── Farms/
    │   ├── Commands/ (CreateFarm, UpdateFarm, AddShed, AddPen)
    │   └── Queries/ (GetFarmById, GetFarmHierarchy, GetFarmList)
    │
    ├── Feeding/
    │   ├── Commands/ (CreateIngredient, CreateFormula, LogFeedConsumption, AssignSchedule)
    │   ├── Queries/ (GetFeedFormulas, GetDailyConsumption, GetFcrReport)
    │   └── EventHandlers/
    │       └── FeedConsumptionLoggedEventHandler.cs  (→ deducts inventory)
    │
    ├── Health/
    │   ├── Commands/ (CreateProtocol, AssignProtocol, RecordVaccination,
    │   │              RecordTreatment, CreateDiseaseIncident, RecordMortality)
    │   ├── Queries/ (GetAnimalHealthHistory, GetVaccinationDueList, GetHerdHealthDashboard)
    │   └── EventHandlers/
    │       └── TreatmentRecordedEventHandler.cs  (→ deducts medicine from inventory)
    │
    ├── Inventory/
    │   ├── Commands/ (CreateItem, RecordStockIn, ManualStockOut, WriteOff)
    │   ├── Queries/ (GetInventoryStatus, GetStockMovementLedger, GetInventoryValuation)
    │   └── EventHandlers/
    │       └── StockDeductedEventHandler.cs  (→ updates denormalized fields)
    │
    ├── Finance/
    │   ├── Commands/ (RecordIncome, RecordExpense, RecordLoan, RecordRepayment)
    │   ├── Queries/ (GetMonthlyPL, GetAnimalCostLedger, GetBatchPL,
    │   │             GetFinancialDashboard, GetLoanSummary)
    │   └── EventHandlers/
    │       └── AnimalSoldEventHandler.cs  (→ auto-posts income entry)
    │
    ├── Dashboard/
    │   └── Queries/
    │       ├── GetExecutiveDashboard/
    │       └── GetFarmDashboard/
    │
    ├── Tenants/
    │   ├── Commands/ (RegisterTenant, UpdateOrganization, ChangePlan, SuspendTenant)
    │   └── Queries/ (GetTenantInfo, GetSubscriptionStatus, GetBillingHistory)
    │
    └── Identity/
        ├── Commands/ (SendOtp, VerifyOtp, RefreshToken, RevokeToken,
        │              InviteUser, AcceptInvitation, ChangeUserRole)
        └── Queries/ (GetCurrentUser, GetOrganizationUsers)
```

**Dependencies:** `Farm360.Domain`, `Farm360.Shared`  
**NuGet:** `MediatR`, `FluentValidation.DependencyInjectionExtensions`, `AutoMapper`  
**Referenced by:** `Farm360.Api`, `Farm360.Infrastructure`, `Farm360.Persistence`, `Farm360.Identity`  
**10-year note:** This is where most feature work happens. Every sprint adds Commands and Queries here. Keep folder discipline strict — one folder per feature, one file per Command/Query/Handler/Validator. Never let handlers grow beyond 80 lines; extract to domain services if logic grows.

---

### 3.4 `Farm360.Contracts`

**Type:** Class Library  
**Layer:** Cross-Cutting (sits between Application and Infrastructure boundary)

**Why it exists:**  
This is the **anti-corruption layer** for the future. Today Farm360 AI is a modular monolith. In Phase 3, some bounded contexts may be extracted to microservices. `Farm360.Contracts` holds the **integration event schemas** — the public-facing messages that cross process boundaries. When the Livestock context eventually becomes a separate service, it publishes `AnimalSoldIntegrationEvent`. The Finance service subscribes to it without knowing anything about the Livestock service's internals.

**Also holds:** External API request/response DTOs for any future partner integration (e.g., DLS reporting API, payment gateway callback schemas).

**Contents:**
```
Farm360.Contracts/
├── IntegrationEvents/
│   ├── Animals/
│   │   ├── AnimalSoldIntegrationEvent.cs
│   │   ├── AnimalDiedIntegrationEvent.cs
│   │   └── AnimalRegisteredIntegrationEvent.cs
│   ├── Inventory/
│   │   ├── StockDeductedIntegrationEvent.cs
│   │   └── LowStockAlertIntegrationEvent.cs
│   ├── Finance/
│   │   └── FinancialEntryPostedIntegrationEvent.cs
│   └── Tenants/
│       ├── TenantRegisteredIntegrationEvent.cs
│       └── SubscriptionSuspendedIntegrationEvent.cs
│
├── ExternalDtos/
│   ├── Payment/
│   │   ├── BkashPaymentCallbackDto.cs
│   │   ├── NagadPaymentCallbackDto.cs
│   │   └── PaymentInitiationRequestDto.cs
│   └── Dls/
│       └── DlsVaccinationReportDto.cs
│
└── Envelopes/
    ├── IntegrationEventEnvelope.cs   (EventId, OccurredOn, Version, Payload)
    └── OutboxMessage.cs              (for transactional outbox pattern)
```

**Dependencies:** `Farm360.Shared` only  
**Referenced by:** `Farm360.Infrastructure` (publishes events), `Farm360.Application` (event definitions)  
**10-year note:** This is the **most future-proof project** in the solution. Keep schemas backward-compatible. Never remove a field — add new ones with `[Obsolete]`. Version your event schemas: `v1/AnimalSoldIntegrationEvent`, `v2/AnimalSoldIntegrationEvent`. This will matter the moment you add a second service.

---

### 3.5 `Farm360.Infrastructure`

**Type:** Class Library  
**Layer:** Infrastructure

**Why it exists:**  
This project implements all the **non-database, non-identity infrastructure concerns** — caching, messaging, external HTTP services, background jobs, blob storage, Serilog configuration. It provides the implementations for the interfaces defined in `Farm360.Application` (except DB and Identity, which have their own projects for isolation).

**Contents:**
```
Farm360.Infrastructure/
├── Caching/
│   ├── RedisCacheService.cs           (implements ICacheService)
│   ├── CacheKeyBuilder.cs             (tenantId:{domain}:{entity}:{id})
│   └── CacheInvalidationService.cs    (pub/sub L1 cache invalidation)
│
├── BackgroundJobs/
│   ├── HangfireConfiguration.cs
│   ├── VaccinationReminderJob.cs
│   ├── MonthlyReportGeneratorJob.cs
│   ├── SubscriptionExpiryCheckerJob.cs
│   ├── PurgeExpiredNotificationsJob.cs
│   ├── ArchiveAuditLogsJob.cs
│   ├── ExportColdArchiveJob.cs
│   ├── MigrateDedicatedTenantsJob.cs
│   └── BackgroundJobService.cs        (implements IBackgroundJobService)
│
├── Messaging/
│   ├── SignalR/
│   │   ├── FarmNotificationHub.cs
│   │   └── SignalRNotificationService.cs   (implements INotificationService)
│   └── DomainEventPublisher.cs
│
├── ExternalServices/
│   ├── Sms/
│   │   ├── ISmsProvider.cs            (abstraction over SMS vendors)
│   │   ├── BulkSmsProvider.cs
│   │   └── SmsService.cs              (implements ISmsService)
│   ├── Email/
│   │   ├── IEmailProvider.cs
│   │   ├── SesEmailProvider.cs        (AWS SES)
│   │   └── EmailService.cs            (implements IEmailService)
│   ├── Payment/
│   │   ├── IBkashProvider.cs
│   │   ├── INagadProvider.cs
│   │   ├── BkashProvider.cs
│   │   ├── NagadProvider.cs
│   │   └── PaymentProviderFactory.cs
│   ├── Storage/
│   │   ├── S3BlobStorageService.cs    (implements IBlobStorageService)
│   │   └── S3Configuration.cs
│   └── Ai/
│       └── BedrockAiService.cs        (Phase 2: AWS Bedrock integration stub)
│
├── Logging/
│   └── SerilogConfiguration.cs        (enrichers, sinks, masking policy setup)
│
├── Http/
│   ├── HttpClientConfiguration.cs     (Polly retry/circuit-breaker policies)
│   └── ResiliencePolicies.cs
│
└── DependencyInjection/
    └── InfrastructureServiceExtensions.cs
```

**Dependencies:** `Farm360.Application`, `Farm360.Domain`, `Farm360.Shared`, `Farm360.Contracts`  
**NuGet:** `StackExchange.Redis`, `Hangfire.SqlServer`, `SignalR` (included in ASP.NET Core), `Serilog.AspNetCore`, `Serilog.Sinks.AwsCloudWatch`, `AWSSDK.S3`, `AWSSDK.SecretsManager`, `Polly`  
**Referenced by:** `Farm360.Api`  
**10-year note:** External service providers change — SMS vendors, payment gateways, AI vendors. The provider abstraction pattern (interface → concrete provider → factory) makes swapping providers a single-class change with no ripple to business logic.

---

### 3.6 `Farm360.Persistence`

**Type:** Class Library  
**Layer:** Infrastructure (Database sub-layer)

**Why it exists:**  
Database concerns are separated from general infrastructure because they evolve at a different rate and require different expertise. A developer working on SMS integration should not be in the same project as someone running EF Core migrations. `Farm360.Persistence` owns the `DbContext`, all entity configurations (Fluent API), migrations, interceptors, and seeders. It implements all `IRepository` interfaces defined in `Farm360.Domain`.

**Contents:**
```
Farm360.Persistence/
├── Context/
│   ├── ApplicationDbContext.cs         (extends DbContext; OnModelCreating)
│   ├── ApplicationReadDbContext.cs      (read-only context → read replica conn string)
│   └── ApplicationDbContextFactory.cs  (IDesignTimeDbContextFactory for migrations CLI)
│
├── Configurations/                      ← One file per entity (Fluent API)
│   ├── Platform/
│   │   ├── TenantConfiguration.cs
│   │   ├── OrganizationConfiguration.cs
│   │   ├── UserConfiguration.cs
│   │   ├── OrganizationUserConfiguration.cs
│   │   ├── SubscriptionPlanConfiguration.cs
│   │   ├── SubscriptionConfiguration.cs
│   │   ├── BillingRecordConfiguration.cs
│   │   ├── FarmConfiguration.cs
│   │   ├── ShedConfiguration.cs
│   │   └── PenConfiguration.cs
│   ├── Livestock/
│   │   ├── AnimalConfiguration.cs       (all indexes defined here)
│   │   ├── WeightRecordConfiguration.cs
│   │   ├── BreedingRecordConfiguration.cs
│   │   ├── AnimalBatchConfiguration.cs
│   │   └── AnimalBatchMemberConfiguration.cs
│   ├── Feeding/
│   ├── Health/
│   ├── Inventory/
│   └── Finance/
│
├── Repositories/
│   ├── GenericRepository.cs            (base: CRUD + spec pattern)
│   ├── AnimalRepository.cs
│   ├── FarmRepository.cs
│   ├── FeedFormulaRepository.cs
│   ├── HealthRepository.cs
│   ├── InventoryRepository.cs
│   ├── FinanceRepository.cs
│   └── TenantRepository.cs
│
├── Interceptors/
│   ├── AuditSaveChangesInterceptor.cs  (auto-sets CreatedAt, UpdatedAt, TenantId, etc.)
│   ├── TenantFilterInterceptor.cs      (enforces EF Global Query Filter)
│   └── OutboxInterceptor.cs            (writes domain events to outbox table)
│
├── Migrations/                          ← EF Core generated — NEVER edit manually
│   ├── 20260707001_InitialCreate.cs
│   └── ... (future migrations)
│
├── Seeders/
│   ├── SystemSeeder.cs                  (orchestrates all seeders)
│   ├── IngredientCatalogSeeder.cs       (pre-loads BD feed ingredients)
│   ├── SystemRoleSeeder.cs
│   ├── SubscriptionPlanSeeder.cs
│   └── ChartOfAccountSeeder.cs
│
├── RawSql/
│   ├── RowLevelSecurity/
│   │   └── CreateRlsPolicies.sql        (RLS setup scripts — run post-migration)
│   └── FullTextSearch/
│       └── CreateAnimalFTS.sql          (SQL Server FTS index for animal search)
│
└── DependencyInjection/
    └── PersistenceServiceExtensions.cs
```

**Dependencies:** `Farm360.Domain`, `Farm360.Application`, `Farm360.Shared`  
**NuGet:** `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`, `Microsoft.EntityFrameworkCore.Design`  
**Referenced by:** `Farm360.Api`  
**10-year note:** Migrations accumulate. After 5 years you will have 200+ migration files. Do NOT be tempted to squash migrations against production databases — `__EFMigrationsHistory` is the authority. Squashing is safe only for new environment bootstrapping, never for existing environments.

---

### 3.7 `Farm360.Identity`

**Type:** Class Library  
**Layer:** Infrastructure (Identity sub-layer)

**Why it exists:**  
Identity is separated because it involves its own `DbContext` (ASP.NET Core Identity tables), its own set of external concerns (JWT RS256 key management via AWS KMS, OTP lifecycle in Redis, refresh token rotation), and it is likely to need specialized security review. Keeping it isolated means a security audit can scope to this single project without touching business logic.

**Contents:**
```
Farm360.Identity/
├── Context/
│   └── IdentityDbContext.cs            (ASP.NET Identity tables — separate schema: identity.*)
│
├── Entities/
│   └── ApplicationUser.cs              (extends IdentityUser; adds Phone, TenantId, etc.)
│
├── Services/
│   ├── JwtTokenService.cs              (RS256 signing, jwks.json endpoint data, token generation)
│   ├── RefreshTokenService.cs          (opaque token generation, hashing, rotation)
│   ├── OtpService.cs                   (6-digit generation, Redis storage, attempt tracking)
│   ├── CurrentUserService.cs           (implements ICurrentUserService from Application)
│   └── TenantService.cs                (implements ITenantService from Application)
│
├── Handlers/
│   ├── SendOtpCommandHandler.cs        (generates OTP → sends via ISmsService)
│   ├── VerifyOtpCommandHandler.cs      (verifies OTP → issues JWT + refresh token)
│   ├── RefreshTokenCommandHandler.cs   (validates refresh token → rotates)
│   └── RevokeTokenCommandHandler.cs    (increments TokenVersion)
│
├── Validators/
│   ├── SendOtpCommandValidator.cs      (phone format: +880XXXXXXXXXX)
│   └── VerifyOtpCommandValidator.cs
│
├── Configuration/
│   ├── JwtConfiguration.cs             (JWT settings — key reference, issuer, audience, expiry)
│   └── KmsKeyConfiguration.cs          (AWS KMS key ARN reference)
│
└── DependencyInjection/
    └── IdentityServiceExtensions.cs
```

**Dependencies:** `Farm360.Application`, `Farm360.Domain`, `Farm360.Shared`  
**NuGet:** `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `AWSSDK.KeyManagementService`, `System.IdentityModel.Tokens.Jwt`  
**Referenced by:** `Farm360.Api`  
**10-year note:** The JWT RS256 private key must never live in code or configuration files. The KMS dependency is a security invariant. If the team ever proposes putting the key in `appsettings.json` for "dev convenience", that is a Constitution violation.

---

### 3.8 `Farm360.Api`

**Type:** ASP.NET Core 10 Web Application  
**Layer:** Presentation

**Why it exists:**  
This is the HTTP entry point. It does one thing: receive HTTP requests, authenticate/authorize, dispatch to MediatR, return responses. It has NO business logic. Its only job is translation between HTTP and the application layer.

Uses **Minimal APIs** (not Controllers) — aligned with ASP.NET Core 10 direction.

**Contents:**
```
Farm360.Api/
├── Program.cs                           (composition root — DI wiring, middleware pipeline)
├── appsettings.json                     (non-secret config)
├── appsettings.Development.json
│
├── Endpoints/                           ← One static class per module
│   ├── AnimalsEndpoints.cs
│   ├── BatchesEndpoints.cs
│   ├── FarmsEndpoints.cs
│   ├── FeedingEndpoints.cs
│   ├── HealthEndpoints.cs
│   ├── InventoryEndpoints.cs
│   ├── FinanceEndpoints.cs
│   ├── DashboardEndpoints.cs
│   ├── IdentityEndpoints.cs
│   ├── TenantEndpoints.cs
│   └── NotificationsEndpoints.cs
│
├── Hubs/
│   └── FarmNotificationHub.cs           (SignalR hub — tenant-scoped groups)
│
├── Middleware/
│   ├── GlobalExceptionMiddleware.cs     (RFC 7807 error responses)
│   ├── CorrelationIdMiddleware.cs       (X-Correlation-Id header)
│   ├── RequestLoggingMiddleware.cs      (structured request/response log)
│   └── TenantResolutionMiddleware.cs    (extract + validate TenantId from JWT)
│
├── Filters/
│   └── PermissionFilter.cs             (endpoint-level permission check)
│
├── OpenApi/
│   ├── OpenApiConfiguration.cs         (Swagger/ReDoc config)
│   ├── SecuritySchemeDefinition.cs      (JWT Bearer scheme)
│   └── Examples/                        (example request/response for each endpoint)
│
└── DependencyInjection/
    └── ApiServiceExtensions.cs
```

**Dependencies (project references):**
```
Farm360.Api → Farm360.Application (dispatching)
Farm360.Api → Farm360.Infrastructure (DI registration only)
Farm360.Api → Farm360.Persistence (DI registration + migration runner)
Farm360.Api → Farm360.Identity (DI registration + middleware)
Farm360.Api → Farm360.Shared (primitives)
```

> **Key principle:** `Farm360.Api` references `Farm360.Infrastructure`, `Farm360.Persistence`, and `Farm360.Identity` **ONLY for DI composition at startup**. Endpoint code never imports infrastructure types directly.

**NuGet:** `Serilog.AspNetCore`, `Swashbuckle.AspNetCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `AspNetCoreRateLimit`, `Microsoft.AspNetCore.SignalR`  
**10-year note:** Minimal APIs in groups are easy to version. When `/api/v2/animals` is needed, a new `AnimalsV2Endpoints.cs` is added — the v1 stays untouched. Never break old API versions.

---

### 3.9 `Farm360.Web` (Angular 22 PWA)

**Type:** npm Workspace / Angular Application (not a .NET project)  
**Layer:** Presentation (Frontend)

**Why it exists:**  
The frontend is a Progressive Web Application. It lives in the same repository as the backend (monorepo) for atomic deployments, shared tooling, and synchronized versioning. It is included in the solution as a reference project to enable full-solution builds.

**Included as:** A folder in `src/Farm360.Web/` with an Angular workspace. Referenced in the `.sln` via a folder reference (no C# project file — Angular uses `angular.json` and `package.json`).

*(Full Angular structure documented separately in Constitution §5.2 and UIX document §22.)*

---

## 4. Dependency Diagram

### 4.1 Strict Layer Dependency Diagram

```
                    ┌─────────────────────────────────────────────┐
                    │              Farm360.Shared                  │
                    │   (zero dependencies — foundational only)    │
                    └─────────────────────┬───────────────────────┘
                                          │ referenced by ALL
          ┌───────────────────────────────┼────────────────────────────────┐
          ▼                               ▼                                ▼
┌──────────────────┐            ┌──────────────────┐           ┌──────────────────┐
│  Farm360.Domain  │            │ Farm360.Contracts │           │   (all others)   │
│                  │◄───────────│                  │           │                  │
│  Pure business   │            │ Integration event │           │                  │
│  logic only      │            │ schemas + ext DTOs│           │                  │
└────────┬─────────┘            └──────────────────┘           └──────────────────┘
         │
         │ ◄── DEPENDENCY BOUNDARY (Clean Architecture inward rule) ──►
         │        Nothing above here knows about anything below here
         │
         ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│                          Farm360.Application                                     │
│   Defines interfaces (IRepository, IEmailService, ICacheService, etc.)          │
│   Commands · Queries · Handlers · Validators · Behaviors · DTOs                 │
└────────────────────────────────────────┬─────────────────────────────────────────┘
                                         │
          ┌──────────────────────────────┼─────────────────────────────┐
          ▼                              ▼                             ▼
┌──────────────────┐          ┌──────────────────┐         ┌──────────────────────┐
│Farm360.Persistence│         │ Farm360.Identity  │         │ Farm360.Infrastructure│
│                  │          │                  │         │                      │
│ EF Core DbContext│          │ ASP.NET Identity │         │ Redis, Hangfire,     │
│ Migrations       │          │ JWT RS256        │         │ SignalR, S3, SMS,    │
│ Repositories     │          │ OTP / Refresh    │         │ Email, Polly, Serilog│
│ Interceptors     │          │ Tokens           │         │                      │
└──────────┬───────┘          └────────┬─────────┘         └──────────┬───────────┘
           │                           │                               │
           └───────────────────────────┼───────────────────────────────┘
                                       │
                                       ▼
                          ┌────────────────────────┐
                          │      Farm360.Api        │
                          │                        │
                          │  Minimal API Endpoints  │
                          │  Middleware Pipeline    │
                          │  SignalR Hubs           │
                          │  Swagger / ReDoc        │
                          │  DI Composition Root   │
                          └────────────────────────┘
```

### 4.2 Forbidden Dependencies (Enforced by `Farm360.Architecture.Tests`)

```
FORBIDDEN                                    REASON
──────────────────────────────────────────────────────────────────────────────────
Farm360.Domain      → Farm360.Application    Domain is innermost; knows nothing outer
Farm360.Domain      → Farm360.Infrastructure  Domain cannot depend on implementations
Farm360.Domain      → Farm360.Persistence    Domain defines repo interfaces, not impls
Farm360.Domain      → Farm360.Api            Obvious violation
Farm360.Application → Farm360.Infrastructure  App defines interfaces; infra implements
Farm360.Application → Farm360.Persistence    Same reason
Farm360.Application → Farm360.Api            Application cannot know about HTTP
Farm360.Shared      → any Farm360.* project  Shared is the foundation; nothing lower
```

---

## 5. Project Reference Map

### 5.1 Complete Reference Table

| Project | References |
|---|---|
| `Farm360.Shared` | *(none)* |
| `Farm360.Domain` | `Farm360.Shared` |
| `Farm360.Contracts` | `Farm360.Shared` |
| `Farm360.Application` | `Farm360.Domain`, `Farm360.Shared` |
| `Farm360.Persistence` | `Farm360.Application`, `Farm360.Domain`, `Farm360.Shared` |
| `Farm360.Identity` | `Farm360.Application`, `Farm360.Domain`, `Farm360.Shared` |
| `Farm360.Infrastructure` | `Farm360.Application`, `Farm360.Domain`, `Farm360.Shared`, `Farm360.Contracts` |
| `Farm360.Api` | `Farm360.Application`, `Farm360.Persistence`, `Farm360.Identity`, `Farm360.Infrastructure`, `Farm360.Shared` |

### 5.2 Why `Farm360.Api` References Infrastructure at All

The `Farm360.Api` project references `Farm360.Infrastructure`, `Farm360.Persistence`, and `Farm360.Identity` **EXCLUSIVELY** for dependency injection registration in `Program.cs`. No endpoint, middleware, or hub imports any type from those projects directly. This is the standard Clean Architecture composition root pattern.

If this feels uncomfortable, the alternative is an explicit **Composition Root project** (`Farm360.CompositionRoot`) that `Farm360.Api` references — but this is overkill for a team of 4–6. The rule is enforced via ArchUnit: *"No type in `Farm360.Api.Endpoints.*` may reference a type from `Farm360.Infrastructure.*` or `Farm360.Persistence.*`."*

---

## 6. NuGet Package Strategy

### 6.1 Central Package Management

All NuGet versions are defined in **one file**: `Directory.Packages.props` at the solution root.  
No project specifies its own package versions. This eliminates version drift.

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Core -->
    <PackageVersion Include="MediatR"                                 Version="12.x.x" />
    <PackageVersion Include="MediatR.Contracts"                       Version="2.x.x" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.x.x" />
    <PackageVersion Include="AutoMapper"                              Version="13.x.x" />

    <!-- EF Core -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer"  Version="10.x.x" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Tools"       Version="10.x.x" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design"      Version="10.x.x" />

    <!-- Infrastructure -->
    <PackageVersion Include="StackExchange.Redis"                      Version="2.x.x" />
    <PackageVersion Include="Hangfire.SqlServer"                       Version="1.x.x" />
    <PackageVersion Include="Hangfire.Core"                            Version="1.x.x" />
    <PackageVersion Include="Serilog.AspNetCore"                       Version="8.x.x" />
    <PackageVersion Include="Serilog.Sinks.Seq"                        Version="6.x.x" />
    <PackageVersion Include="Polly"                                    Version="8.x.x" />
    <PackageVersion Include="Polly.Extensions.Http"                    Version="8.x.x" />

    <!-- Identity + Auth -->
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.x.x" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer"      Version="10.x.x" />
    <PackageVersion Include="System.IdentityModel.Tokens.Jwt"                   Version="7.x.x" />
    <PackageVersion Include="AWSSDK.KeyManagementService"                        Version="3.x.x" />

    <!-- API -->
    <PackageVersion Include="Swashbuckle.AspNetCore"                   Version="7.x.x" />
    <PackageVersion Include="AspNetCoreRateLimit"                      Version="4.x.x" />

    <!-- AWS -->
    <PackageVersion Include="AWSSDK.S3"                                Version="3.x.x" />
    <PackageVersion Include="AWSSDK.SecretsManager"                    Version="3.x.x" />
    <PackageVersion Include="AWSSDK.CloudWatchLogs"                    Version="3.x.x" />

    <!-- Testing -->
    <PackageVersion Include="xunit"                                    Version="2.x.x" />
    <PackageVersion Include="xunit.runner.visualstudio"                Version="2.x.x" />
    <PackageVersion Include="FluentAssertions"                         Version="6.x.x" />
    <PackageVersion Include="NSubstitute"                              Version="5.x.x" />
    <PackageVersion Include="Testcontainers.MsSql"                     Version="3.x.x" />
    <PackageVersion Include="Testcontainers.Redis"                     Version="3.x.x" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing"         Version="10.x.x" />
    <PackageVersion Include="NetArchTest.Rules"                        Version="1.x.x" />
    <PackageVersion Include="Bogus"                                    Version="35.x.x" />
    <PackageVersion Include="Respawn"                                  Version="6.x.x" />
  </ItemGroup>
</Project>
```

### 6.2 Packages Per Project

| Project | NuGet Packages |
|---|---|
| `Farm360.Shared` | *(none — pure C#)* |
| `Farm360.Domain` | `MediatR.Contracts` |
| `Farm360.Contracts` | *(none — pure C#)* |
| `Farm360.Application` | `MediatR`, `FluentValidation.DependencyInjectionExtensions`, `AutoMapper`, `Microsoft.Extensions.Logging.Abstractions` |
| `Farm360.Persistence` | `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`, `Microsoft.EntityFrameworkCore.Design` |
| `Farm360.Identity` | `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt`, `AWSSDK.KeyManagementService` |
| `Farm360.Infrastructure` | `StackExchange.Redis`, `Hangfire.SqlServer`, `Hangfire.Core`, `Serilog.AspNetCore`, `Polly`, `AWSSDK.S3`, `AWSSDK.SecretsManager`, `AWSSDK.CloudWatchLogs` |
| `Farm360.Api` | `Swashbuckle.AspNetCore`, `AspNetCoreRateLimit`, `Serilog.AspNetCore` |

### 6.3 Shared MSBuild Properties

```xml
<!-- Directory.Build.props — applies to ALL projects automatically -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
    <RootNamespace>Farm360</RootNamespace>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>CS1591</NoWarn>  <!-- Allow missing XML docs on internal types -->
    <Deterministic>true</Deterministic>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
  </PropertyGroup>
</Project>
```

---

## 7. Angular Workspace Structure

`Farm360.Web` is an **Angular 22 standalone workspace** located at `src/Farm360.Web/`.

```
Farm360.Web/
├── angular.json
├── package.json
├── tsconfig.json
├── tsconfig.app.json
├── tsconfig.spec.json
├── .eslintrc.json
├── ngsw-config.json                ← PWA service worker config (Workbox)
│
├── src/
│   ├── index.html
│   ├── main.ts                     ← bootstrapApplication(AppComponent)
│   ├── styles/
│   │   ├── tokens.css              ← CSS custom properties (color, spacing, typography tokens)
│   │   ├── reset.css
│   │   ├── typography.css
│   │   └── utilities.css
│   │
│   └── app/
│       ├── app.component.ts        ← Root component (router outlet only)
│       ├── app.routes.ts           ← Root routes (all features lazy-loaded)
│       ├── app.config.ts           ← provideRouter, provideHttpClient, provideStore
│       │
│       ├── core/                   ← Singleton services (imported once in app.config)
│       │   ├── auth/
│       │   │   ├── auth.service.ts
│       │   │   ├── auth.guard.ts
│       │   │   └── token-storage.service.ts
│       │   ├── http/
│       │   │   ├── jwt.interceptor.ts
│       │   │   ├── error.interceptor.ts
│       │   │   └── offline-queue.interceptor.ts
│       │   ├── tenant/
│       │   │   └── tenant-context.service.ts
│       │   ├── signalr/
│       │   │   └── notification-hub.service.ts
│       │   └── i18n/
│       │       ├── bn.json
│       │       └── en.json
│       │
│       ├── shared/                 ← Reusable UI (no route, no business state)
│       │   ├── components/
│       │   │   ├── data-table/
│       │   │   ├── confirmation-dialog/
│       │   │   ├── currency-input/
│       │   │   ├── date-picker/
│       │   │   ├── pagination/
│       │   │   ├── status-badge/
│       │   │   └── empty-state/
│       │   ├── pipes/
│       │   │   ├── bdt-currency.pipe.ts
│       │   │   ├── bangla-date.pipe.ts
│       │   │   └── animal-age.pipe.ts
│       │   └── directives/
│       │       ├── has-permission.directive.ts
│       │       └── infinite-scroll.directive.ts
│       │
│       ├── features/               ← Lazy-loaded feature modules
│       │   ├── dashboard/
│       │   │   ├── dashboard.routes.ts
│       │   │   ├── dashboard.component.ts
│       │   │   ├── store/
│       │   │   │   └── dashboard.store.ts       ← NgRx Signal Store
│       │   │   └── components/
│       │   │       ├── executive-summary/
│       │   │       ├── health-alert-panel/
│       │   │       └── stock-alert-panel/
│       │   ├── livestock/
│       │   ├── feeding/
│       │   ├── health/
│       │   ├── inventory/
│       │   ├── finance/
│       │   └── settings/
│       │
│       └── layout/
│           ├── shell/
│           ├── sidebar/
│           ├── topbar/
│           └── notification-panel/
```

---

## 8. Test Project Strategy

### 8.1 `Farm360.Domain.UnitTests`

**What is tested:** Domain entities, value objects, domain services, domain invariants  
**Dependencies:** `Farm360.Domain`, `Farm360.Shared`  
**Mocking:** ZERO mocks — pure in-memory object graph  
**Coverage target:** 95%

```
Farm360.Domain.UnitTests/
├── Entities/
│   ├── AnimalTests.cs               (create, sell, quarantine, death state machines)
│   └── FeedFormulaTests.cs
├── ValueObjects/
│   ├── MoneyTests.cs
│   ├── AnimalTagTests.cs
│   └── BodyConditionScoreTests.cs
└── DomainServices/
    ├── FcrCalculationServiceTests.cs
    └── AdgCalculationServiceTests.cs
```

### 8.2 `Farm360.Application.UnitTests`

**What is tested:** Command/Query handlers, validators, pipeline behaviors  
**Dependencies:** `Farm360.Application`, `Farm360.Domain`, `Farm360.Shared`  
**Mocking:** NSubstitute (all repositories and services mocked)  
**Coverage target:** 85%

```
Farm360.Application.UnitTests/
├── Animals/
│   ├── RegisterAnimalCommandHandlerTests.cs
│   ├── SellAnimalCommandHandlerTests.cs
│   └── RegisterAnimalCommandValidatorTests.cs
├── Feeding/
├── Health/
├── Finance/
└── Behaviors/
    ├── ValidationBehaviorTests.cs
    └── PerformanceBehaviorTests.cs
```

### 8.3 `Farm360.Application.IntegrationTests`

**What is tested:** Full application pipeline end-to-end with real SQL Server and Redis  
**Dependencies:** All source projects  
**Infrastructure:** TestContainers (SQL Server + Redis containers)  
**Pattern:** Each test runs in a transaction rolled back after the test  
**Coverage target:** Minimum 1 test per Command handler

```
Farm360.Application.IntegrationTests/
├── TestBase.cs                      (containers setup, DI, seeding)
├── CustomWebApplicationFactory.cs
├── Helpers/
│   └── TestDataBuilder.cs           (Bogus-powered test data)
└── Features/
    ├── Animals/
    │   ├── RegisterAnimalIntegrationTests.cs
    │   └── SellAnimalIntegrationTests.cs
    ├── Finance/
    └── MultiTenancy/
        └── TenantIsolationTests.cs  ← CRITICAL: verifies cross-tenant cannot happen
```

### 8.4 `Farm360.Api.FunctionalTests`

**What is tested:** Full HTTP request → response cycle  
**Infrastructure:** `WebApplicationFactory<Program>`, TestContainers  
**Scope:** Authentication, authorization, response schemas, status codes, rate limiting

```
Farm360.Api.FunctionalTests/
├── TestWebApplicationFactory.cs
└── Endpoints/
    ├── AnimalsEndpointTests.cs       (POST, GET, PATCH, DELETE — all status codes)
    ├── IdentityEndpointTests.cs
    └── AuthorizationTests.cs         (403 scenarios per role per endpoint)
```

### 8.5 `Farm360.Architecture.Tests`

**What is tested:** Architectural rules are enforced automatically in CI  
**Library:** NetArchTest.Rules  
**Runs in:** Every CI pipeline — not optional, not skippable

```
Farm360.Architecture.Tests/
└── ArchitectureTests.cs

Tests included:
  ✓ Domain has no dependency on Application or Infrastructure
  ✓ Application has no dependency on Infrastructure or Api
  ✓ All handlers are in Application.Features.*
  ✓ All validators are co-located with their command
  ✓ All entities extend BaseEntity or AuditableEntity
  ✓ Domain entities have no public setters
  ✓ No Api.Endpoints.* type references Infrastructure.* or Persistence.*
  ✓ All commands implement IRequest<>
  ✓ All queries implement IRequest<>
  ✓ All domain events implement INotification
```

---

## 9. Build Order & CI Pipeline

### 9.1 Dependency Build Order (MSBuild resolves automatically)

```
1. Farm360.Shared            (no dependencies)
2. Farm360.Domain            (depends on: Shared)
3. Farm360.Contracts         (depends on: Shared)
4. Farm360.Application       (depends on: Domain, Shared)
5. Farm360.Persistence       (depends on: Application, Domain, Shared)
6. Farm360.Identity          (depends on: Application, Domain, Shared)
7. Farm360.Infrastructure    (depends on: Application, Domain, Shared, Contracts)
8. Farm360.Api               (depends on: all above)

Test projects build after their source projects:
9. Farm360.Domain.UnitTests
10. Farm360.Application.UnitTests
11. Farm360.Architecture.Tests
12. Farm360.Application.IntegrationTests
13. Farm360.Api.FunctionalTests
```

### 9.2 CI Pipeline Stages

```
Stage 1 — Code Quality (fast, ~2 min)
  ├── dotnet format --verify-no-changes    (formatting check)
  ├── dotnet build --warnaserror           (zero warnings)
  └── Architecture Tests                   (NetArchTest)

Stage 2 — Unit Tests (~3 min)
  ├── Farm360.Domain.UnitTests
  └── Farm360.Application.UnitTests

Stage 3 — Integration Tests (~8 min, TestContainers)
  ├── Farm360.Application.IntegrationTests
  └── Farm360.Api.FunctionalTests

Stage 4 — Quality Gates
  ├── Coverage gate: ≥ 80% overall
  ├── Angular: ng build --configuration production
  └── Angular: npm run test -- --watch=false

Stage 5 — Docker Build & Push (on develop/main only)
  └── Docker buildx → ECR

Stage 6 — Deploy to Staging (on develop)
  └── K8s: migrator job → API rollout

Stage 7 — Smoke Tests (post-staging deploy)
  └── Critical path API tests
```

---

## 10. Ten-Year Maintainability Notes

### 10.1 What Will Change

| Change | Managed By |
|---|---|
| New bounded context added (Poultry module — Phase 2) | Add new aggregate folder in Domain; new feature folder in Application; new EF schema |
| SMS provider changes (Twilio replaces BulkSMS) | Replace `BulkSmsProvider.cs` only — zero ripple |
| Payment gateway adds bKash Merchant API v2 | Add `BkashV2Provider.cs`; update factory; zero ripple |
| Angular major version upgrade | Isolated in `Farm360.Web`; API contract unchanged |
| SQL Server → PostgreSQL (hypothetical) | Replace `Farm360.Persistence` implementations only — Domain/Application unchanged |
| Scale to microservices (Phase 3) | `Farm360.Contracts` integration events become the inter-service API; bounded context packages become deployable units |
| New subscription tier | Add enum value, update `SubscriptionLimitService`, add seeder — zero schema change needed |
| AI feature (Phase 2) | `ExternalServices/Ai/BedrockAiService.cs` already stubbed |

### 10.2 Stability Hierarchy

```
Most Stable ←─────────────────────────────────────→ Most Volatile
Farm360.Domain  Farm360.Contracts  Farm360.Application  Farm360.Infrastructure  Farm360.Api
```

The further left a project is, the higher the cost of changing it. Invest design effort proportionally — the Domain is where 80% of your design thinking should go.

### 10.3 Team Scaling Notes

| Team Size | Recommended Structure |
|---|---|
| 2–4 developers (MVP) | One team owns all projects; PR process enforces boundaries |
| 5–10 developers | Feature teams own specific bounded contexts; project boundaries enforce ownership |
| 10–20 developers | Extract high-velocity bounded contexts (Livestock, Finance) to separate repos/services; `Farm360.Contracts` becomes a shared NuGet package |
| 20+ developers | Full microservices extraction guided by `Farm360.Contracts` event contracts |

### 10.4 The Promises This Architecture Makes

```
✓ A developer who has never seen the codebase can find any feature in < 2 minutes
✓ Adding a new Command requires touching exactly 3 files (Command + Handler + Validator)
✓ A failing unit test points to a single class — not a 500-line class
✓ No migration can be applied to production without CI verification
✓ Cross-tenant data leakage is architecturally impossible (4 independent enforcement layers)
✓ Any external service (SMS, payment, email, AI) can be swapped in 1 day
✓ The Angular frontend can be replaced with a mobile app with ZERO backend changes
✓ The database can be replaced with the Domain and Application layers untouched
```

---

*This solution structure is the canonical reference for all development decisions on Farm360 AI.*  
*Governed by: F360-CONST-2026-001 — Project Constitution.*  
*© 2026 Farm360 AI Engineering Organization. All Rights Reserved.*
