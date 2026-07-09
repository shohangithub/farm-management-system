# 🏛️ Farm360 AI — Project Constitution

**Document ID:** F360-CONST-2026-001  
**Version:** 1.0 — Permanent Reference  
**Status:** RATIFIED — Binding on All Engineering Work  
**Authority:** Chief Software Architect — Farm360 AI  
**Date:** July 2026  
**Source Documents:** PVD v1.0 · PRD v1.0 · SAD v1.0 · DDD v1.0 · UIX v1.0  
**Classification:** Confidential — Engineering Law  

---

> *"This Constitution is the supreme law of the Farm360 AI engineering organization. All code, all decisions, all trade-offs must be measured against it. It does not expire. It is amended only by deliberate architectural review."*

---

## ⚠️ Constitution Enforcement Policy

Before generating **any code**, the following pre-flight verification must be performed:

1. **Layer check** — Does the code belong to the correct layer (Domain / Application / Infrastructure / API / Web)?
2. **Dependency direction check** — Do all references point inward (toward Domain)?
3. **Naming check** — Does every identifier follow the naming standards in §4?
4. **Tenant check** — Does every entity carry `TenantId`? Is it set by the interceptor, not manually?
5. **Validation check** — Is validation in FluentValidation (not in the controller or entity constructor directly)?
6. **Logging check** — Does every log entry include `TenantId`, `UserId`, `CorrelationId`?
7. **Testing check** — Is the code testable without infrastructure dependencies?

If any check fails, the violation must be declared and corrected before code is generated.

---

## Table of Contents

1. [Project Philosophy](#1-project-philosophy)
2. [Architecture Principles](#2-architecture-principles)
3. [Coding Standards](#3-coding-standards)
4. [Naming Standards](#4-naming-standards)
5. [Folder Standards](#5-folder-standards)
6. [API Standards](#6-api-standards)
7. [DTO Standards](#7-dto-standards)
8. [CQRS Standards](#8-cqrs-standards)
9. [Validation Standards](#9-validation-standards)
10. [Exception Standards](#10-exception-standards)
11. [Logging Standards](#11-logging-standards)
12. [Database Standards](#12-database-standards)
13. [Migration Rules](#13-migration-rules)
14. [Git Commit Convention](#14-git-commit-convention)
15. [Branching Strategy](#15-branching-strategy)
16. [Pull Request Standards](#16-pull-request-standards)
17. [Unit Testing Standards](#17-unit-testing-standards)
18. [Integration Testing Standards](#18-integration-testing-standards)
19. [UI Standards](#19-ui-standards)
20. [Performance Rules](#20-performance-rules)
21. [Security Rules](#21-security-rules)
22. [Multi-Tenant Rules](#22-multi-tenant-rules)
23. [Documentation Rules](#23-documentation-rules)

---

## 1. Project Philosophy

### 1.1 What Farm360 AI Is

Farm360 AI is the **operating system for modern livestock farms in Bangladesh**. It is an enterprise-grade, multi-tenant SaaS platform that unifies livestock management, smart feeding, veterinary health, inventory control, and financial intelligence into a single AI-powered operations platform.

It is **not** a digitized spreadsheet. It is **not** a simple CRUD app. It carries real domain complexity — gestation periods, FCR calculations, quarantine gates, financial period closures, multi-tenant isolation, and AI readiness.

### 1.2 Core Values (Engineering Interpretation)

| Value | Engineering Application |
|---|---|
| **Farmer First** | Every feature must trace back to a user story. No gold-plating. No features for technology's sake. |
| **Radical Simplicity** | Simple code that works beats clever code that might. Prefer boring, explicit, maintainable solutions. |
| **Local Context** | BDT currency, Bangla/English UI, Bangladeshi breeds and feed ingredients, `Asia/Dhaka` timezone, `DD/MM/YYYY` date format, `+880` phone prefix. |
| **Data Trust** | Farmers own their data. Tenant isolation is non-negotiable and non-bypassable. |
| **Continuous Intelligence** | The system must be structured to capture clean, queryable data that powers future AI models. |

### 1.3 Architectural Goals (Binding)

| Goal | Minimum Bar |
|---|---|
| **Correctness** | Business rules enforced at the domain layer, not in controllers or database triggers |
| **Isolation** | One tenant's data, workload, or failure must NEVER impact another tenant |
| **Evolvability** | New modules and AI features must be addable without structural rewrites |
| **Observability** | Every operation is traceable end-to-end: correlation ID → tenant ID → user ID → DB query |
| **Security** | Multi-layered defense; zero-trust within the application boundary |
| **Performance** | P95 API response ≤ 500ms; page load ≤ 2s on 3G |
| **Operational Simplicity** | 4–6 engineers must run this in production without a dedicated SRE |

### 1.4 Non-Negotiable Platform Requirements

- **Uptime SLA:** 99.9% (≤ 8.7 hours downtime/year)
- **RTO:** < 1 hour for critical failures
- **RPO:** < 15 minutes (near-real-time backup)
- **Data residency:** AWS Mumbai (ap-south-1) or equivalent
- **Encryption at rest:** AES-256
- **Encryption in transit:** TLS 1.3 mandatory
- **OWASP Top 10 compliance:** mandatory before MVP launch

---

## 2. Architecture Principles

### 2.1 Clean Architecture — The Dependency Rule

Farm360 AI uses **Clean Architecture**. The Dependency Rule is absolute:

```
Source code dependencies MUST ALWAYS point inward — toward the Domain.
Outer layers depend on inner layers. Inner layers have ZERO knowledge of outer layers.
```

#### Layer Stack

```
┌─ PRESENTATION (Farm360.Api + Farm360.Web) ──────────────────┐
│   ┌─ APPLICATION (Farm360.Application) ──────────────────┐  │
│   │   ┌─ DOMAIN (Farm360.Domain) ─────────────────────┐  │  │
│   │   │  Entities · Value Objects · Aggregates        │  │  │
│   │   │  Domain Events · Repository Interfaces        │  │  │
│   │   │  Domain Services · Specifications             │  │  │
│   │   │  ← ZERO EXTERNAL DEPENDENCIES →               │  │  │
│   │   └───────────────────────────────────────────────┘  │  │
│   └────────────────────────────────────────────────────────┘  │
│                                                                │
│  INFRASTRUCTURE (Farm360.Infrastructure)                       │
│  Implements all interfaces defined in Domain + Application     │
└────────────────────────────────────────────────────────────────┘
```

#### Forbidden References (enforced by NetArchTest in CI)

```
Farm360.Domain     ──✗──► Farm360.Application
Farm360.Domain     ──✗──► Farm360.Infrastructure
Farm360.Domain     ──✗──► Farm360.Api
Farm360.Application ──✗──► Farm360.Infrastructure
Farm360.Application ──✗──► Farm360.Api
```

### 2.2 Modular Monolith (ADR-001)

The MVP is a **modular monolith** — NOT microservices. One deploy unit. ACID transactions across modules. The bounded context structure allows extraction to microservices in Phase 3 without rewriting business logic.

**Bounded Contexts:**
- Platform (Identity, Tenant, Subscription)
- Farm (Farm, Shed, Pen)
- Livestock (Animal, Batch)
- Feeding (Ingredient, Formula, Consumption)
- Health (Vaccination, Treatment, Incident, Mortality)
- Inventory (Items, Stock, Supplier)
- Finance (Entries, Cost Ledger, P&L, Loans)
- Audit (Logs, Notifications, Events)

### 2.3 CQRS with MediatR (ADR-002)

All operations are dispatched through **MediatR**:
- **Commands** — change state, go through full pipeline including transaction
- **Queries** — read state, skip transaction, may use read replica

No business logic lives in controllers. Controllers do: authenticate → authorize → deserialize → dispatch → serialize. Nothing more.

### 2.4 Domain-Driven Design (ADR-003)

**Aggregate Boundaries (canonical — do not violate):**

| Aggregate | Root | Children |
|---|---|---|
| Tenant | `Tenant` | `Subscription`, `Organization`, `OrganizationUser` |
| Farm | `Farm` | `Shed`, `Pen` |
| Animal | `Animal` | `WeightRecord`, `BreedingRecord`, `AnimalTransferLog`, `AnimalPhoto` |
| Batch | `AnimalBatch` | `AnimalBatchMember` (references Animal, not owns) |
| FeedFormula | `FeedFormula` | `FormulaIngredient`, `FeedingSchedule` |
| FeedConsumption | `FeedConsumptionLog` | `ConsumptionDetail` |
| VaccinationProtocol | `VaccinationProtocol` | `ProtocolScheduleItem` |
| InventoryItem | `InventoryItem` | `StockBatch`, `StockTransaction` |
| FinancialEntry | `FinancialEntry` | (immutable root) |
| LoanRecord | `LoanRecord` | `LoanRepayment` |

### 2.5 The MediatR Pipeline Order

Every request flows through this pipeline in exact order:

```
[1]  CorrelationIdMiddleware      → Inject X-Correlation-ID
[2]  RequestLoggingMiddleware     → Log request start
[3]  TenantResolutionMiddleware   → Extract TenantId from JWT
[4]  JWT Authentication           → Validate Bearer token
[5]  Authorization                → Policy/permission check
     ↓ MediatR dispatch
[6]  LoggingBehavior              → Log command/query name
[7]  ValidationBehavior           → Run FluentValidation; throw if invalid
[8]  PerformanceBehavior          → Start stopwatch
[9]  TransactionBehavior          → Wrap Commands in SQL transaction
[10] CachingBehavior              → Return cached result for ICacheableQuery
[11] AuditBehavior                → Record pre/post state
     ↓ Handler executes
[12] Domain Events dispatched     → After transaction commit
[13] Domain Event Handlers        → Side effects (notifications, deductions)
[14] PerformanceBehavior          → Log elapsed time; warn if > 500ms
[15] RequestLoggingMiddleware     → Log response status + duration
```

### 2.6 Guiding Principles

| Principle | Rule |
|---|---|
| **Explicit over implicit** | Business rules live in the domain, not inferred from DB structure |
| **Fail fast, fail loudly** | Invalid state caught at the boundary (validation layer), never silently propagated |
| **Commands change state; Queries read state** | CQRS enforces this — no accidental side effects in read paths |
| **Dependency inversion at every boundary** | Higher layers define abstractions; lower layers implement them |
| **Tenant context is sacred** | `TenantId` threads through every operation; losing it is a bug |
| **Optimistic by default, pessimistic when required** | Optimistic concurrency for most; pessimistic only for critical financial writes |

---

## 3. Coding Standards

### 3.1 C# / .NET Standards

```
Language:          C# 13 (latest with .NET 10)
Nullable:          Enabled project-wide (NullReferenceException is a defect)
Warnings as errors: Enabled for Domain and Application projects
Target Framework:  net10.0
```

**General Rules:**

| Rule | Rationale |
|---|---|
| Prefer immutability — `readonly`, `init`, `record` | Prevents accidental mutation; thread-safe |
| Prefer `async/await` throughout; **never** `.Result` or `.Wait()` | Deadlock prevention |
| Prefer explicit types over `var` for non-obvious types | Readability in code review |
| No static mutable state anywhere | Testing and threading safety |
| No `sealed` classes as default; seal only when inheritance is explicitly prohibited | Extensibility |

**Domain Layer Rules (HARD RULES — violations fail code review):**

| Rule |
|---|
| Entities: private setters; state changes ONLY via domain methods |
| Value Objects: `sealed record` with structural equality |
| Domain methods: return `void` or raise domain events; NEVER return DTOs |
| Constructors: `private`; use static factory methods (`Animal.Create(...)`) |
| Collections on entities: exposed as `IReadOnlyCollection<T>` ONLY |
| NEVER expose `ICollection<T>` or `List<T>` (bypasses domain logic) |
| Domain layer: ZERO NuGet dependencies except `MediatR.Contracts` |

**Application Layer Rules:**

| Rule |
|---|
| One file per Command, Query, Handler, Validator (co-located in one folder) |
| Command handlers: return the ID of created/modified aggregate, not the full entity |
| Query handlers: return DTOs or `PaginatedResult<T>`; NEVER return domain entities |
| No business logic in handlers — delegate to domain services and entities |

**Infrastructure Layer Rules:**

| Rule |
|---|
| EF Core: Fluent API configuration ONLY; no data annotations on domain entities |
| Repositories: generic base + specific implementations; no LINQ in handlers |
| All external HTTP calls: wrapped in Polly policies (retry, circuit breaker, timeout) |
| No raw SQL except for specific performance-critical read model queries (must be documented) |

**API Layer Rules:**

| Rule |
|---|
| Minimal APIs grouped by module/feature |
| Endpoints: ≤ 10 lines; dispatch → return |
| URL path versioning: `/api/v1/` |
| All endpoints return `IResult` (`TypedResults.Ok<T>`, `TypedResults.Created`, etc.) |
| Every endpoint documented with `ProducesResponseType` attributes |

### 3.2 Angular / TypeScript Standards

```
Angular Version:   Angular 22 (standalone components throughout)
TypeScript:        Strict mode enabled
ESLint:            @angular-eslint/recommended
```

| Rule |
|---|
| Standalone components everywhere; NgModules only where 3rd-party requires |
| OnPush change detection on ALL components |
| Signal-based reactivity preferred for component state; RxJS for HTTP and event streams |
| **No `any` type** — strict typing everywhere |
| Reactive Forms ONLY (no template-driven forms) |
| Smart (container) vs. Dumb (presentational) component separation |
| Dumb components: `@Input()` / `@Output()` only; no service injection |
| Smart components: inject services; bind to NgRx Signal Store |
| One NgRx Signal Store per feature (not one global store) |

---

## 4. Naming Standards

### 4.1 C# Naming Conventions

| Artifact | Convention | Example |
|---|---|---|
| Class | PascalCase | `AnimalRepository`, `RegisterAnimalCommand` |
| Interface | `I` + PascalCase | `IAnimalRepository`, `ITenantService` |
| Method | PascalCase | `RegisterAnimalAsync`, `CalculateAdg` |
| Property | PascalCase | `DateOfBirth`, `SalePrice` |
| Private field | `_camelCase` | `_tenantService`, `_dbContext` |
| Constant | `UPPER_CASE` | `MAX_OTP_ATTEMPTS`, `DEFAULT_CACHE_TTL_SECONDS` |
| Parameter | camelCase | `animalId`, `tenantId` |
| Local variable | camelCase | `animal`, `vaccinationRecord` |
| Async method | Suffix `Async` | `GetAnimalByIdAsync`, `SaveChangesAsync` |
| Command | PascalCase + `Command` | `RegisterAnimalCommand` |
| Query | PascalCase + `Query` | `GetAnimalByIdQuery` |
| Handler | Command/QueryName + `Handler` | `RegisterAnimalCommandHandler` |
| Validator | Command/QueryName + `Validator` | `RegisterAnimalCommandValidator` |
| Domain Event | PascalCase + `Event` | `AnimalSoldEvent` |
| Domain Exception | PascalCase + `Exception` | `AnimalQuarantinedException` |
| DTO | PascalCase + `Dto` or `Request`/`Response` | `AnimalDetailDto`, `RegisterAnimalRequest` |
| EF Configuration | Entity + `Configuration` | `AnimalConfiguration` |
| Test class | ClassUnderTest + `Tests` | `RegisterAnimalCommandHandlerTests` |
| Background job | PascalCase + `Job` | `VaccinationReminderJob` |
| Domain Service | PascalCase + `Service` | `FcrCalculationService` |

### 4.2 Database Naming Conventions

| Artifact | Convention | Example |
|---|---|---|
| Schema | lowercase | `platform`, `livestock`, `feeding` |
| Table | PascalCase, **plural** | `Animals`, `VaccinationRecords` |
| Column | PascalCase | `AnimalId`, `DateOfBirth` |
| Primary Key column | `Id` (UNIQUEIDENTIFIER) | `Id` |
| Foreign Key column | `{Entity}Id` | `AnimalId`, `TenantId`, `ShedId` |
| FK constraint | `FK_{Table}_{ReferencedTable}` | `FK_Animals_Sheds` |
| Index | `IX_{Table}_{Columns}` | `IX_Animals_TenantId_Status` |
| Unique constraint | `UQ_{Table}_{Columns}` | `UQ_Animals_TenantId_TagId` |
| Check constraint | `CK_{Table}_{Column}` | `CK_Animals_Sex` |
| Default constraint | `DF_{Table}_{Column}` | `DF_Animals_IsDeleted` |

### 4.3 API Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Route | lowercase, kebab-case | `/api/v1/animals`, `/api/v1/feed-formulas` |
| Query parameter | camelCase | `?farmId=x&pageSize=20` |
| JSON property | camelCase | `{ "animalId": "...", "dateOfBirth": "..." }` |
| HTTP verb | Standard semantics | GET=read, POST=create, PUT=replace, PATCH=partial update, DELETE=remove |
| Resource ID in route | `/{entityId}` | `/api/v1/animals/{animalId}` |

### 4.4 Angular / TypeScript Naming Conventions

| Artifact | Convention | Example |
|---|---|---|
| Component class | PascalCase + `Component` | `AnimalListComponent` |
| Service | PascalCase + `Service` | `AnimalService` |
| Guard | PascalCase + `Guard` | `AuthGuard`, `PermissionGuard` |
| Interceptor | PascalCase + `Interceptor` | `JwtInterceptor` |
| Pipe | PascalCase + `Pipe` | `BdtCurrencyPipe` |
| NgRx Signal Store | PascalCase + `Store` | `LivestockStore` |
| TypeScript interface | `I` + PascalCase | `IAnimalDto`, `IFarmSummary` |
| Enum | PascalCase | `AnimalStatus`, `UserRole` |
| Component file | kebab-case`.component.ts` | `animal-list.component.ts` |
| Service file | kebab-case`.service.ts` | `animal.service.ts` |
| Store file | kebab-case`.store.ts` | `livestock.store.ts` |
| Component selector | `app-kebab-case` | `app-animal-list` |

### 4.5 Infrastructure & Environment Naming

| Resource | Convention | Example |
|---|---|---|
| AWS Resource | `farm360-{env}-{resource}` | `farm360-prod-api-alb` |
| Docker image | `farm360-{service}:{sha}` | `farm360-api:a1b2c3d` |
| Kubernetes namespace | `farm360-{environment}` | `farm360-production` |
| Kubernetes deployment | `farm360-{service}` | `farm360-api` |
| Git release tag | `v{major}.{minor}.{patch}` | `v1.2.3` |

---

## 5. Folder Standards

### 5.1 Backend Solution Structure (`Farm360.sln`)

```
Farm360.sln
│
├── src/
│   ├── Farm360.Domain/                    ← Domain Layer (ZERO dependencies)
│   │   ├── Common/                        (BaseEntity, AuditableEntity, BaseValueObject, interfaces)
│   │   ├── Entities/                      (one file per entity)
│   │   ├── ValueObjects/                  (Money, AnimalTag, Weight, NutritionalProfile, etc.)
│   │   ├── Aggregates/                    (FarmAggregate/, AnimalAggregate/, BatchAggregate/)
│   │   ├── DomainEvents/                  (one file per event)
│   │   ├── Enumerations/                  (AnimalStatus, AnimalSpecies, UserRole, etc.)
│   │   ├── Exceptions/                    (DomainException + specific subclasses)
│   │   ├── Interfaces/
│   │   │   ├── Repositories/              (IAnimalRepository, IFarmRepository, etc.)
│   │   │   └── Services/                  (IFcrCalculationService, etc.)
│   │   └── Specifications/                (BaseSpecification + specific specs)
│   │
│   ├── Farm360.Application/               ← Application Layer
│   │   ├── Common/
│   │   │   ├── Behaviors/                 (ValidationBehavior, LoggingBehavior, PerformanceBehavior,
│   │   │   │                               TransactionBehavior, CachingBehavior, AuditBehavior)
│   │   │   ├── Exceptions/               (NotFoundException, ForbiddenAccessException,
│   │   │   │                               ValidationException, ConflictException)
│   │   │   ├── Interfaces/               (ICurrentUserService, ITenantService, IDateTimeService,
│   │   │   │                               IEmailService, ISmsService, IBlobStorageService,
│   │   │   │                               ICacheService, IAuditService, INotificationService)
│   │   │   ├── Models/                   (PaginatedResult, Result, CacheableQuery)
│   │   │   └── Mappings/                 (MappingProfile.cs)
│   │   │
│   │   ├── Features/
│   │   │   ├── Animals/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── RegisterAnimal/   (Command + Handler + Validator in same folder)
│   │   │   │   │   ├── RecordWeight/
│   │   │   │   │   ├── SellAnimal/
│   │   │   │   │   ├── RecordAnimalDeath/
│   │   │   │   │   ├── TransferAnimal/
│   │   │   │   │   └── QuarantineAnimal/
│   │   │   │   └── Queries/
│   │   │   │       ├── GetAnimalById/
│   │   │   │       ├── GetAnimalTimeline/
│   │   │   │       ├── GetAnimalsByFilter/
│   │   │   │       └── GetAnimalCostLedger/
│   │   │   ├── Feeding/
│   │   │   ├── Health/
│   │   │   ├── Inventory/
│   │   │   ├── Finance/
│   │   │   ├── Dashboard/
│   │   │   ├── Farms/
│   │   │   ├── Tenants/
│   │   │   └── Identity/
│   │   │
│   │   └── DomainEventHandlers/
│   │
│   ├── Farm360.Infrastructure/            ← Infrastructure Layer
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── Configurations/           (one per entity: AnimalConfiguration.cs)
│   │   │   ├── Repositories/             (GenericRepository + specific repositories)
│   │   │   ├── Migrations/               (EF Core auto-generated — never edit by hand)
│   │   │   ├── Interceptors/             (AuditSaveChangesInterceptor, TenantFilterInterceptor)
│   │   │   └── Seeders/                  (IngredientCatalogSeeder, SystemRoleSeeder)
│   │   ├── Identity/                     (ApplicationUser, JwtTokenService, OtpService, CurrentUserService)
│   │   ├── Caching/                      (RedisCacheService, CacheKeyBuilder)
│   │   ├── BackgroundJobs/               (VaccinationReminderJob, MonthlyReportGeneratorJob, etc.)
│   │   ├── Messaging/                    (SignalR/, DomainEventPublisher)
│   │   ├── ExternalServices/             (Sms/, Email/, Payment/, Storage/)
│   │   ├── Logging/                      (SerilogConfiguration.cs)
│   │   └── DependencyInjection/          (InfrastructureServiceExtensions.cs)
│   │
│   └── Farm360.Api/                      ← Presentation Layer (API)
│       ├── Program.cs                    (composition root)
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Endpoints/                    (one file per module: AnimalsEndpoints, FeedingEndpoints, etc.)
│       ├── Hubs/                         (FarmNotificationHub.cs)
│       ├── Middleware/                   (TenantResolutionMiddleware, GlobalExceptionMiddleware,
│       │                                  CorrelationIdMiddleware, RequestLoggingMiddleware)
│       ├── Filters/                      (PermissionFilter.cs)
│       └── DependencyInjection/          (ApiServiceExtensions.cs)
│
├── tests/
│   ├── Farm360.Domain.UnitTests/
│   ├── Farm360.Application.UnitTests/
│   ├── Farm360.Application.IntegrationTests/  (uses TestContainers)
│   └── Farm360.Api.FunctionalTests/
│
├── tools/
│   └── scripts/                          (migrate.sh, seed.sh)
│
└── docs/
    ├── architecture/
    ├── adr/
    └── api/
```

### 5.2 Frontend Structure (`farm360-web/`)

```
farm360-web/src/app/
├── core/                     ← Singleton services (loaded once)
│   ├── auth/                 (auth.service.ts, auth.guard.ts, token-storage.service.ts)
│   ├── http/                 (jwt.interceptor.ts, error.interceptor.ts, offline-queue.interceptor.ts)
│   ├── tenant/               (tenant-context.service.ts)
│   ├── signalr/              (notification-hub.service.ts)
│   └── i18n/
│
├── shared/                   ← Reusable UI (no route, no state)
│   ├── components/           (data-table/, confirmation-dialog/, currency-input/, etc.)
│   ├── pipes/                (bdt-currency.pipe.ts, bangla-date.pipe.ts, animal-age.pipe.ts)
│   └── directives/           (has-permission.directive.ts, tenant-scope.directive.ts)
│
├── features/                 ← Lazy-loaded feature modules
│   ├── dashboard/            (routes + component + store/ + components/)
│   ├── livestock/            (routes + animal-list/ + animal-detail/ + store/)
│   ├── feeding/
│   ├── health/
│   ├── inventory/
│   ├── finance/
│   └── settings/             (farm-management/, user-management/, subscription/)
│
├── layout/                   ← Shell layout
│   ├── shell/
│   ├── sidebar/
│   ├── topbar/
│   └── notification-panel/
│
└── app.routes.ts             (root routing — lazy loads all features)
```

---

## 6. API Standards

### 6.1 RESTful Design

- All routes: `/api/v1/{resource}` — **versioning from day one**
- Route segments: lowercase, kebab-case (`/feed-formulas`, `/animal-batches`)
- Resource IDs in route: `/{resourceId}` (e.g., `/api/v1/animals/{animalId}`)
- Query parameters: camelCase (`?farmId=x&pageSize=20&pageNumber=1`)
- JSON body and response: camelCase properties

### 6.2 HTTP Verb Semantics

| Verb | Use Case | Response |
|---|---|---|
| `GET` | Read resource(s) | 200 OK + body |
| `POST` | Create resource | 201 Created + `Location` header + created ID |
| `PUT` | Full replace | 200 OK |
| `PATCH` | Partial update | 200 OK |
| `DELETE` | Soft delete | 204 No Content |

### 6.3 Pagination Standard

All list endpoints must support pagination:
```json
{
  "items": [...],
  "pageNumber": 1,
  "pageSize": 25,
  "totalCount": 247,
  "totalPages": 10,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

Default `pageSize`: 25. Maximum `pageSize`: 100.

### 6.4 Response Envelope

Success responses return the resource directly (no envelope wrapper).  
Error responses follow **RFC 7807 Problem Details**:

```json
{
  "type": "https://farm360.ai/errors/not-found",
  "title": "Resource Not Found",
  "status": 404,
  "detail": "Animal with ID 'abc-123' was not found.",
  "instance": "/api/v1/animals/abc-123",
  "correlationId": "req-xyz",
  "timestamp": "2026-07-07T02:00:00Z"
}
```

### 6.5 Authentication Header

Every authenticated request must carry:
```
Authorization: Bearer {accessToken}
X-Correlation-Id: {uuid}          (generated by client; if absent, server generates)
Content-Language: bn-BD or en-US  (for localized error messages)
```

### 6.6 Rate Limiting

All endpoints are rate-limited. Limits expressed as:
- `X-RateLimit-Limit`: requests per window
- `X-RateLimit-Remaining`: remaining requests
- `X-RateLimit-Reset`: Unix timestamp of window reset
- `429 Too Many Requests` when limit exceeded

### 6.7 OpenAPI Documentation Requirements

Every endpoint must document:
- One-line summary
- Detailed description (with business context)
- Request body schema with field descriptions
- All response codes: 200/201, 400, 401, 403, 404, 409, 422, 429, 500
- Required permission (e.g., `animals:write`)
- Example request + response bodies

---

## 7. DTO Standards

### 7.1 DTO Naming

| DTO Type | Suffix | Example |
|---|---|---|
| Command input | `Command` | `RegisterAnimalCommand` |
| Query input | `Query` | `GetAnimalByIdQuery` |
| API request | `Request` | `RegisterAnimalRequest` |
| API response / read model | `Dto` | `AnimalDetailDto`, `AnimalSummaryDto` |
| Paginated wrapper | `PaginatedResult<T>` | `PaginatedResult<AnimalSummaryDto>` |

### 7.2 DTO Rules

| Rule |
|---|
| DTOs are **plain C# records** with `init` properties (immutable after construction) |
| DTOs are **never domain entities** — map explicitly using AutoMapper or manual mappings |
| DTOs live in the **Application layer** only |
| Command DTOs carry the validated input; handler creates domain entities from them |
| Query DTOs (response) carry only the fields needed for the UI — never the full entity |
| Financial amounts always carry a `Currency` field alongside `Amount` |
| Dates are serialized as **ISO 8601 UTC strings** (`"2026-07-07T02:00:00Z"`) |
| IDs are serialized as **lowercase GUIDs** (`"550e8400-e29b-41d4-a716-446655440000"`) |

### 7.3 Example DTO Pattern

```csharp
// Command
public record RegisterAnimalCommand(
    Guid FarmId,
    Guid ShedException,
    string TagId,
    int Species,
    string BreedName,
    int Sex,
    DateOnly AcquisitionDate,
    decimal? AcquisitionPrice
) : IRequest<Guid>;

// Response DTO
public record AnimalDetailDto(
    Guid Id,
    string TagId,
    string BreedName,
    string SpeciesName,
    string Status,
    decimal? LatestWeightKg,
    decimal? AdgKgPerDay,
    DateOnly AcquisitionDate,
    decimal? AcquisitionPrice
);
```

---

## 8. CQRS Standards

### 8.1 Command Rules

| Rule |
|---|
| Commands represent **intent to change state** |
| Command class: `{Action}{Entity}Command` — `IRequest<Guid>` or `IRequest<Result>` |
| Command handler returns the **ID** of the created/modified resource (not the entity itself) |
| Command handlers are wrapped in `TransactionBehavior` — SQL transaction guaranteed |
| Commands dispatch **domain events** after the transaction commits |
| Commands go through the **full MediatR pipeline** (including ValidationBehavior) |
| Commands **always** route to the **write database** (primary) |

### 8.2 Query Rules

| Rule |
|---|
| Queries represent **intent to read state** |
| Query class: `Get{Entity}By{Criteria}Query` — `IRequest<T>` where T is a DTO |
| Query handlers return **DTOs or PaginatedResult<DTO>** — NEVER domain entities |
| Queries skip `TransactionBehavior` |
| Queries implementing `ICacheableQuery` pass through `CachingBehavior` |
| Queries route to the **read replica** (where applicable) |
| Query handlers use `AsNoTracking()` on ALL queries |
| Query handlers use explicit `Include()` — lazy loading is globally DISABLED |

### 8.3 Domain Event Rules

| Rule |
|---|
| Domain events are raised **inside domain entities** using `AddDomainEvent(event)` |
| Events are dispatched by `AuditSaveChangesInterceptor` **after** `SaveChangesAsync()` completes |
| Event handlers are **idempotent** (re-running does not corrupt state) |
| Event handlers for finance and inventory run **synchronously** in the same request scope |
| Event handlers for notifications may run **asynchronously** via Hangfire |

---

## 9. Validation Standards

### 9.1 Validation Layer Stack

```
[L1] Client-side (Angular Reactive Forms)  → UX only; NEVER trusted by server
[L2] API Model Binding (ASP.NET Core)      → Type conversion; 400 on malformed JSON
[L3] FluentValidation (MediatR Pipeline)   → AUTHORITATIVE validation layer
[L4] Domain Validation (Entity Methods)    → Business invariants; throws DomainException
[L5] Database Constraints (SQL Server)     → Last resort; DbUpdateException = defect
```

### 9.2 FluentValidation Rules

| Rule |
|---|
| **One validator per Command/Query** — co-located in the same folder |
| File name: `{CommandName}Validator.cs` |
| Validators are auto-registered from the Application assembly via DI |
| `ValidationBehavior` runs ALL validators and collects ALL failures before throwing |
| Async validators (uniqueness checks) inject repository interfaces |
| Field-level errors returned as `{ "fieldName": ["error message"] }` |
| Error messages are written in **English** (localized client-side) |

### 9.3 Domain Invariants

| Rule |
|---|
| Value objects self-validate in their factory method — invalid values cannot be constructed |
| Domain entity state transitions validated inside domain methods |
| Domain exceptions thrown for invariant violations (NOT application exceptions) |
| Examples: `AnimalQuarantinedException`, `InsufficientStockException` |

### 9.4 Business Rules That Must Be Validated

| Context | Rule |
|---|---|
| Animal | TagId unique within tenant (async validator) |
| Animal | WeightDate ≥ DateOfBirth |
| Animal | SaleDate ≥ AcquisitionDate |
| Breeding | PregnancyConfirmDate ≥ MatingDate |
| Breeding | CalvingDate ≥ MatingDate |
| Breeding | Dam ≠ Sire (cannot be same animal) |
| Feed | Formula must have ≥ 2 ingredients |
| Stock | Consumption cannot exceed current stock (warning; Owner can override) |
| Finance | Amount > 0 |
| Tenant | Cannot downgrade if current animal/user count exceeds new tier limit |

---

## 10. Exception Standards

### 10.1 Exception Hierarchy

```
System.Exception
│
├── Application.Common.Exceptions.AppException  (base for all app exceptions)
│   ├── NotFoundException              → HTTP 404
│   ├── ValidationException            → HTTP 422 (field-level errors map)
│   ├── ForbiddenAccessException       → HTTP 403
│   ├── ConflictException              → HTTP 409
│   └── TenantSuspendedException       → HTTP 402
│
└── Domain.Exceptions.DomainException  (domain rule violations)
    ├── AnimalQuarantinedException      → HTTP 422
    ├── InvalidAnimalStateTransitionException → HTTP 422
    ├── InsufficientStockException      → HTTP 422
    └── ClosedPeriodModificationException    → HTTP 422
```

### 10.2 Global Exception Middleware Rules

| Rule |
|---|
| `GlobalExceptionMiddleware` sits at the **top** of the middleware pipeline |
| NEVER expose stack traces or internal error details to the client |
| ALL exceptions return RFC 7807 Problem Details format |
| Unhandled exceptions: log as `Error` with full stack trace + CorrelationId → return 500 |
| `correlationId` is ALWAYS included in the error response |

### 10.3 Exception Rules

| Rule |
|---|
| Throw **typed exceptions** — not `Exception` directly |
| Use `NotFoundException` for missing resources — NEVER return 404 from a 200 response |
| Use `ForbiddenAccessException` for authorization failures — NOT 404 (don't leak existence) |
| Do NOT use exceptions for control flow — only for actual exceptional conditions |
| `DomainException` subclasses are thrown by entities/domain services |
| `AppException` subclasses are thrown by handlers and application services |

---

## 11. Logging Standards

### 11.1 Logging Philosophy

> Every log entry must answer: **Who did what to which resource, in which tenant, at what time, and what happened?**

### 11.2 Mandatory Log Properties (Every Entry)

Every single log entry MUST carry:

| Property | Source |
|---|---|
| `TenantId` | `ITenantService` (via Serilog enricher) |
| `UserId` | `ICurrentUserService` (via Serilog enricher) |
| `UserRole` | JWT claims (via Serilog enricher) |
| `CorrelationId` | `X-Correlation-Id` header (via Serilog enricher) |
| `MachineName` | Environment (via Serilog enricher) |
| `Environment` | `ASPNETCORE_ENVIRONMENT` (via Serilog enricher) |
| `Application` | `"Farm360.Api"` (via Serilog enricher) |
| `Version` | Assembly version (via Serilog enricher) |

### 11.3 Log Level Standard

| Level | When to Use | Example |
|---|---|---|
| `Verbose` | Extreme detail — **never in production** | EF Core query parameters |
| `Debug` | Development diagnostic | "Cache hit for key: {key}" |
| `Information` | Normal operational events | "Animal registered: {AnimalId}", "User logged in: {UserId}" |
| `Warning` | Unexpected but recoverable | "OTP attempt {n}/3 for {Phone}", "Performance exceeded {ms}ms" |
| `Error` | Single operation failure | "Failed to send SMS to {Phone}: {Error}" |
| `Fatal` | Application-level failure | "DB connection pool exhausted", "Cannot resolve TenantId from JWT" |

### 11.4 Structured Log Format

```json
{
  "Timestamp": "2026-07-07T02:00:00.000Z",
  "Level": "Information",
  "MessageTemplate": "Animal {AnimalId} sold for {SalePrice} BDT by {UserId}",
  "Properties": {
    "AnimalId": "a1b2c3",
    "SalePrice": 150000,
    "UserId": "user-xyz",
    "TenantId": "tenant-abc",
    "UserRole": "Owner",
    "CorrelationId": "req-12345",
    "MachineName": "farm360-api-pod-7f9d",
    "Environment": "Production",
    "Application": "Farm360.Api",
    "Version": "1.2.0",
    "ElapsedMs": 245
  }
}
```

### 11.5 Sensitive Data Masking (MANDATORY)

The following data is NEVER logged as-is. Custom `IDestructuringPolicy` enforces this globally:

| Data | Masked Format |
|---|---|
| Phone numbers | `+880171XXXX05` (mask middle 6 digits) |
| Email addresses | `r****@gmail.com` |
| JWT tokens | Never logged (only `jti` claim) |
| NID numbers | Fully masked |
| Passwords | Never logged |
| OTP values | Never logged |
| Bank account numbers | Masked |

### 11.6 EF Core SQL Logging

```
EF Core SQL query logs: DISABLED in Production and Staging
EF Core SQL query logs: ENABLED in Development only (level = Debug)
```

### 11.7 Log Sinks

| Sink | Environment | Purpose |
|---|---|---|
| Console (stdout) | All | Container stdout for K8s |
| File (rolling daily, 7-day retention) | Local / Dev | Local debugging |
| AWS CloudWatch Logs | Staging + Production | Centralized, searchable, 90-day retention |
| Seq | Development / Staging | Rich structured log UI |

---

## 12. Database Standards

### 12.1 Core Database Design Decisions (Non-Negotiable)

| Decision | Standard |
|---|---|
| Database engine | Microsoft SQL Server 2022 (AWS RDS) |
| ORM | Entity Framework Core 10, Code-First with Fluent API ONLY |
| Primary key type | `UNIQUEIDENTIFIER` (GUID) — `DEFAULT NEWSEQUENTIALID()` |
| Money storage | `DECIMAL(18,4)` with explicit `Currency CHAR(3)` column |
| Enumerations | `TINYINT` in DB; documented as C# enums in application code |
| Optimistic concurrency | `RowVersion ROWVERSION` on every mutable entity table |
| Soft delete | `IsDeleted BIT`, `DeletedAt DATETIME2(7)`, `DeletedByUserId` on every entity table |
| Temporal tables | System-Versioned Temporal Tables on all financial entities |
| Audit columns | 6 standard columns on EVERY entity table (see §12.2) |
| Tenant isolation | `TenantId UNIQUEIDENTIFIER NOT NULL` on every tenant-scoped table |
| Schema separation | One schema per bounded context |

### 12.2 Standard Audit Columns (Every Entity Table)

```sql
CreatedAt       DATETIME2(7)        NOT NULL DEFAULT SYSUTCDATETIME()
CreatedByUserId UNIQUEIDENTIFIER    NOT NULL FK → platform.Users(Id)
UpdatedAt       DATETIME2(7)        NOT NULL DEFAULT SYSUTCDATETIME()
UpdatedByUserId UNIQUEIDENTIFIER    NOT NULL FK → platform.Users(Id)
IsDeleted       BIT                 NOT NULL DEFAULT 0
DeletedAt       DATETIME2(7)        NULL
DeletedByUserId UNIQUEIDENTIFIER    NULL FK → platform.Users(Id)
RowVersion      ROWVERSION          NOT NULL
```

**These columns are set by `AuditSaveChangesInterceptor` automatically. Developers NEVER set them manually.**

### 12.3 Schema-to-Bounded-Context Mapping

| Schema | Bounded Context | Owner |
|---|---|---|
| `platform` | Identity, Farm, Subscription, Lookup Enums | Platform Team |
| `livestock` | Animal Lifecycle | Livestock Team |
| `feeding` | Feed Ingredients, Formulas, Consumption | Feeding Team |
| `health` | Vaccinations, Treatments, Incidents, Mortality | Health Team |
| `inventory` | Stock Management, Suppliers | Inventory Team |
| `finance` | Financial Entries, P&L, Ledgers, Loans | Finance Team |
| `audit` | Audit Logs, Notifications, System Events | Platform Team |
| `hangfire` | Background Job tables (library-managed) | Platform Team |

### 12.4 EF Core Configuration Rules

| Rule |
|---|
| Fluent API configuration ONLY — no data annotations on domain entities |
| One configuration class per entity: `{Entity}Configuration.cs` |
| Configuration classes implement `IEntityTypeConfiguration<T>` |
| Global Query Filters applied in `OnModelCreating`: `!e.IsDeleted && e.TenantId == currentTenantId` |
| `AsNoTracking()` on ALL read-only queries |
| Lazy loading: **DISABLED globally** |
| Explicit `Include()` required for navigation properties |

### 12.5 Soft Delete Rules

| Rule |
|---|
| Hard delete is **NEVER permitted** on business data |
| `AuditLogs` and `StockTransactions` are **immutable** — no soft delete |
| `FinancialEntries` in closed periods **cannot** be soft-deleted (reversal required) |
| All unique constraints use `WHERE IsDeleted = 0` filtered index |
| EF Core Global Query Filter automatically excludes soft-deleted records |
| Recovery: Platform Admin can restore within 90-day window |
| Purge: Background job purges after 90 days (SME) / 7 years (financial records) |

### 12.6 Index Strategy Rules

| Rule |
|---|
| Index for known query patterns — NOT for every column |
| All composite indexes: `TenantId` as the **leading column** |
| Covering indexes: INCLUDE all SELECT columns to eliminate key lookups |
| Large table indexes: `CREATE INDEX ... WITH (ONLINE = ON)` to avoid locks |
| Over-indexing is as dangerous as under-indexing in a write-heavy system |
| Full-text index on `Animals` (`TagId`, `Name`, `BreedName`) for text search |
| Index strategy reviewed on every release |

---

## 13. Migration Rules

### 13.1 EF Core Migration Policy

| Rule |
|---|
| Code-First: schema is derived from C# models; NEVER reverse-engineer from DB |
| Every schema change = exactly one new migration file |
| Migrations are **idempotent** — re-running does nothing if already applied |
| Migration file committed in the **same PR** as the entity/configuration change |
| **NEVER edit** a migration file that has been applied to any environment |

### 13.2 Migration Naming Convention

```
{Timestamp}_{PascalCaseDescription}

Examples:
  20260707001_InitialCreate
  20260707002_AddAnimalBcsColumn
  20260708001_AddInventorySupplierTable
  20260710001_AddFinancialEntryIsPeriodClosed
```

### 13.3 Migration Safety Rules (Production)

| Rule |
|---|
| No column **RENAME** in a single migration (add new → copy data → drop old — 3 separate migrations) |
| No `NOT NULL` column addition without `DEFAULT` value or preceding data backfill migration |
| Large table migrations (> 1M rows) run in batches during maintenance window |
| Every destructive migration (drop column, drop table) requires: rollback script + DBA review |
| `CREATE INDEX ... WITH (ONLINE = ON)` for indexes on live tables |

### 13.4 Migration Workflow

```
Developer:
1. Modify C# entity/configuration
2. dotnet ef migrations add {Name} --project Farm360.Infrastructure --startup-project Farm360.Api
3. Review generated Up() and Down() methods
4. Write rollback test: apply → verify → revert → verify
5. Commit migration WITH entity change in same PR

CI/CD:
→ Migration runs as Kubernetes Job (farm360-migrator) BEFORE API pod startup
→ dotnet ef database update --connection {connection}
→ Success (exit 0) → API deployment proceeds
→ Failure (non-zero) → deployment BLOCKED → alert fires
```

### 13.5 Maintenance Window

- Scheduled migration window: **Sunday 02:00–03:00 BDT**
- Unplanned production migrations: only for critical hotfixes; require CTO approval

---

## 14. Git Commit Convention

### 14.1 Format (Conventional Commits)

```
<type>(<scope>): <subject>

[Optional body — explain WHY, not WHAT]

[Optional footer — Closes #issue, BREAKING CHANGE: description]
```

### 14.2 Types

| Type | Use |
|---|---|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Formatting; no logic change |
| `refactor` | Refactoring (no feature, no bug) |
| `perf` | Performance improvement |
| `test` | Adding or updating tests |
| `chore` | Build, CI, tooling changes |
| `revert` | Reverting a previous commit |

### 14.3 Scopes

```
animals · feeding · health · inventory · finance · dashboard ·
platform · auth · tenant · infra · ci · docs · ui
```

### 14.4 Subject Rules

- Imperative mood, lowercase, **no period** at end
- Maximum 72 characters
- Describe the change, not the "what I did"

### 14.5 Examples

```
feat(animals): add weight tracking with ADG calculation
fix(health): prevent duplicate vaccination log for same day
perf(dashboard): cache executive summary with 5-minute TTL
chore(ci): add architecture test stage to GitHub Actions pipeline
test(animals): add integration tests for SellAnimal command
docs(api): add OpenAPI examples for animal registration endpoint
feat(tenant): implement subscription grace period enforcement
BREAKING CHANGE: renamed RegisterAnimalRequest.TagNumber to TagId
```

---

## 15. Branching Strategy

### 15.1 Branch Model: Trunk-Based Development

```
main (trunk)
├── Always deployable to production
├── Protected: PR required + 2 approvals + all CI checks
├── Tagged on each release: v1.0.0, v1.1.0, etc.
└── No direct push — INCLUDING admins

develop
├── Integration branch for all feature work
├── Deployed to Staging automatically on merge
├── Protected: PR required + 1 approval + all CI checks
└── Merged to main for production release

feature/{ticket-id}-{short-description}
├── Created from: develop
├── Merged to: develop
├── Lifetime: ≤ 2 days (merge frequently — trunk-based)
└── Example: feature/F360-142-register-animal-endpoint

fix/{ticket-id}-{short-description}
├── Created from: develop
├── Merged to: develop
└── Example: fix/F360-201-animal-tag-duplicate-validation

hotfix/{ticket-id}-{short-description}
├── Created from: main (PRODUCTION EMERGENCY ONLY)
├── Merged to: main AND back-merged to develop
├── Lifecycle: ≤ 4 hours from branch to production merge
└── Example: hotfix/F360-250-tenant-isolation-breach

release/{version}
├── Optional — only for coordinated releases
├── Used for: final testing, cherry-picks, docs finalization
└── Example: release/v1.2.0
```

### 15.2 Branch Protection Rules

```
Branch: main
  → Require PR (no direct push — including admins)
  → Require: 2 reviewer approvals
  → Require: all status checks pass (CI stages 1–4)
  → Require: manual deployment approval gate
  → No force push; no deletion

Branch: develop
  → Require PR
  → Require: 1 reviewer approval
  → Require: all status checks pass (CI stages 1–3)
```

---

## 16. Pull Request Standards

### 16.1 PR Size Limits

| Size | Lines Changed | Policy |
|---|---|---|
| Ideal | < 400 | Always mergeable |
| Warning | 400–800 | Add detailed description; reviewer fatigue risk |
| **Blocked** | **> 1,000** | **Must be split — this is a process failure** |

### 16.2 PR Template (Mandatory Checklist)

```markdown
## Summary
What does this PR do?

## Motivation
Why is this change needed?

## Changes
- List of specific changes

## Testing
How was this tested?

## Screenshots
(Required for UI changes)

## Checklist
- [ ] Tests added/updated (coverage gate: ≥ 80%)
- [ ] Both Bangla and English translations updated (for any UI text)
- [ ] Audit logging implemented for all state-changing operations
- [ ] No sensitive data in logs (phone, email, NID masked)
- [ ] No hardcoded configuration values (use appsettings / env vars)
- [ ] API documentation (OpenAPI) updated
- [ ] Architecture tests pass (dependency direction verified)
- [ ] Performance impact considered (no N+1, no missing indexes)
- [ ] TenantId isolation maintained (no cross-tenant query possible)
- [ ] Migration file committed with entity change (if DB change)
- [ ] Rollback plan documented (if schema change)
```

### 16.3 Review Standards

| Reviewer | Responsibility |
|---|---|
| Technical reviewer | Code quality, architecture compliance, test coverage |
| Domain reviewer | Business rule correctness, domain model integrity |
| Security reviewer | Multi-tenant isolation, input validation, sensitive data handling |

---

## 17. Unit Testing Standards

### 17.1 Coverage Requirements

| Layer | Minimum Coverage |
|---|---|
| Domain (Entities, Value Objects, Domain Services) | 95% |
| Application (Commands, Queries, Handlers, Validators) | 85% |
| Infrastructure (only pure logic, not EF Core) | 60% |
| **Overall Gate** | **≥ 80% (CI fails if below)** |

### 17.2 Test Project Structure

```
Farm360.Domain.UnitTests/
├── Entities/            (AnimalTests.cs, FeedFormulaTests.cs)
├── ValueObjects/        (MoneyTests.cs, AnimalTagTests.cs)
└── DomainServices/      (FcrCalculationServiceTests.cs)

Farm360.Application.UnitTests/
├── Animals/             (RegisterAnimalCommandHandlerTests.cs)
├── Feeding/
├── Health/
└── Finance/
```

### 17.3 Unit Test Rules

| Rule |
|---|
| Test class name: `{ClassUnderTest}Tests` |
| Test method name: `{Method}_{Scenario}_{ExpectedResult}` |
| Use **AAA pattern**: Arrange — Act — Assert |
| Mock all external dependencies (no real DB, no real HTTP) |
| Domain tests: ZERO mocks — test pure business logic |
| Application handler tests: mock repositories and services |
| One assert per test (or logically grouped asserts for the same outcome) |
| Test both happy path AND failure cases for every handler |
| Test every domain invariant — every `DomainException` path |

### 17.4 Test Method Naming Examples

```csharp
// Domain
Animal_WhenQuarantined_CannotBeSold()
Money_WhenNegativeAmount_ThrowsDomainException()
FeedFormula_WhenLessThanTwoIngredients_ThrowsDomainException()

// Application
RegisterAnimalCommandHandler_WhenTagAlreadyExists_ThrowsConflictException()
SellAnimalCommandHandler_WhenAnimalNotFound_ThrowsNotFoundException()
GetAnimalByIdQueryHandler_WhenAnimalExists_ReturnsCorrectDto()
```

### 17.5 Framework

- Test framework: **xUnit**
- Mocking: **NSubstitute** (preferred) or Moq
- Assertions: **FluentAssertions**
- Domain test data: Builder pattern (`AnimalBuilder.ValidAnimal().Build()`)

---

## 18. Integration Testing Standards

### 18.1 Integration Test Scope

Integration tests verify the full application pipeline from MediatR dispatch through to the real database.

```
Farm360.Application.IntegrationTests/
├── TestBase.cs                    (sets up TestContainers, seeds data)
├── CustomWebApplicationFactory.cs (replaces production DI with test config)
└── Features/
    ├── Animals/                   (RegisterAnimalIntegrationTests.cs)
    ├── Health/
    └── Finance/
```

### 18.2 TestContainers Setup

Integration tests use **TestContainers** — Docker containers spun up fresh per test session:

```
SQL Server container (for EF Core + migrations)
Redis container     (for caching and OTP)
```

### 18.3 Integration Test Rules

| Rule |
|---|
| Each test class inherits `TestBase` which handles setup/teardown |
| Each test runs in a **transaction that is rolled back** after — no test pollution |
| Tests must not depend on execution order |
| Tests verify: command executes → DB state changes → domain events fire → side effects correct |
| Tests verify multi-tenant isolation: different tenant cannot access another's data |
| Minimum 1 integration test per Command handler |
| Coverage gate: **≥ 80% on Application layer** overall (unit + integration combined) |

### 18.4 API Functional Tests

```
Farm360.Api.FunctionalTests/
→ Full HTTP request → response tests using WebApplicationFactory
→ Verify: authentication, authorization, correct status codes, response schema
→ Critical path smoke tests run post-deployment to staging
```

---

## 19. UI Standards

### 19.1 Design System Foundation

Farm360 AI synthesizes three world-class design systems:

| Source | Contribution |
|---|---|
| **Microsoft Fluent Design 2** | Depth, layering, enterprise density, accessibility-first, smooth animations |
| **Material Design 3** | Dynamic color system, semantic tokens, expressive component shapes |
| **Apple HIG** | Spatial clarity, generous whitespace, precision typography, direct manipulation |

### 19.2 Color System (Canonical Tokens — Light Mode)

**Primary Brand: Deep Teal (HSL 173, 80%)**

```
brand-500:  hsl(173, 80%, 38%) → #0d9e83   [Brand Primary]
brand-600:  hsl(173, 82%, 30%) → #087c67   [Hover/Active]
```

**Semantic Tokens:**
```
interactive-primary:          #0d9e83
interactive-primary-hover:    #087c67
interactive-primary-press:    #065c4d
surface-base:                 #ffffff
surface-raised:               #f8fafc
text-primary:                 #0f172a   (contrast 15.8:1 ✓ AAA)
text-secondary:               #334155   (contrast 10.7:1 ✓ AAA)
border-focus:                 #0d9e83
status-danger:                #dc2626
status-warning:               #d97706
status-success:               #16a34a
```

### 19.3 Dark Mode

Dark mode is **mandatory**. All color tokens have dark mode equivalents:
```
surface-base (dark):    #0d1117
surface-raised (dark):  #161b22
text-primary (dark):    #f0f6fc   (contrast 14.9:1 ✓ AAA)
interactive-primary:    #1cbf9f
```

### 19.4 Typography

| Font | Role |
|---|---|
| **Inter** | Primary Latin UI typeface |
| **Noto Sans Bengali** | Bangla language (same size scale, auto-substituted) |
| **JetBrains Mono** | Animal tags, IDs, financial codes — always monospace |

Minimum body text size: **13px** (body-sm). Never below this for operational UI.

### 19.5 Spacing System

Base unit: **4px**. All spacing is a multiple of 4px:
- `space-4` = 16px (standard button padding)
- `space-6` = 24px (card padding desktop)
- `space-8` = 32px (major section gap)

### 19.6 Component Rules

| Rule |
|---|
| All components: **OnPush** change detection |
| All components: **standalone** (no NgModules) |
| Touch targets: minimum **44×44px** (WCAG 2.5.5) |
| Buttons: 4 variants — Primary, Secondary, Ghost, Danger |
| Danger buttons (delete, irreversible): **ALWAYS preceded by confirmation dialog** |
| Every destructive action: requires confirmation ("Type the name to confirm" for critical) |
| Form validation: inline error messages in user's active language |
| Loading states: skeleton screens (not spinners for content) |
| Empty states: meaningful illustration + CTA, never "No data found" |

### 19.7 Localization Rules

| Rule |
|---|
| **All user-facing strings** must exist in BOTH `bn.json` and `en.json` |
| Currency: BDT with `৳` symbol, Bengali numerals in Bangla mode |
| Date format: `DD/MM/YYYY` (Bangladesh standard) |
| Phone input: `+880` prefix hardcoded |
| Language toggle: user-level preference stored in profile |
| Never hardcode display strings in components — always use i18n keys |

### 19.8 Accessibility (WCAG 2.1 AA — Mandatory)

| Rule |
|---|
| All text: ≥ 4.5:1 contrast ratio (body); ≥ 3:1 (large text, UI components) |
| All interactive elements: keyboard navigable |
| All form inputs: associated `<label>` |
| All images: `alt` attributes |
| Focus ring: always visible (`border-focus` token) — NEVER `outline: none` without replacement |
| ARIA roles on all custom interactive components |

### 19.9 Performance (Frontend)

| Metric | Target |
|---|---|
| Lighthouse score | ≥ 80 (mobile) |
| Page load on 3G | ≤ 2 seconds |
| Bundle size gate | CI fails if bundle exceeds threshold |
| All feature modules: **lazy-loaded** |
| Angular bundle size analyzed per PR (webpack-bundle-analyzer) |
| `index.html`: `Cache-Control: no-cache, no-store` |
| Hashed JS/CSS assets: `Cache-Control: max-age=31536000, immutable` |

---

## 20. Performance Rules

### 20.1 API Performance Targets

| Metric | Target |
|---|---|
| API response time (P95) | ≤ 500ms |
| API response time (P99) | ≤ 1 second |
| Report generation | ≤ 5 seconds (monthly P&L) |
| Page load (3G) | ≤ 2 seconds |
| Concurrent users per Enterprise tenant | 50 simultaneous |

### 20.2 Database Performance Rules

| Rule |
|---|
| `AsNoTracking()` on ALL read-only EF Core queries |
| Explicit `Include()` required — lazy loading DISABLED globally |
| Projection to DTOs on list endpoints (not loading full entities) |
| No N+1 queries — dashboard loads all data in ≤ 3 queries |
| Pagination: cursor-based for large datasets; offset-based for reports |
| Covering indexes for all critical read paths (see §12.6) |
| Denormalized columns maintained by domain event handlers (see §12.1) |

### 20.3 Caching Rules

| Rule |
|---|
| L1 cache: in-memory per-pod (`IMemoryCache`) — ultra-hot data only |
| L2 cache: Redis distributed (`IDistributedCache`) — cross-pod consistency |
| Cache key format: `{tenantId}:{domain}:{entity}:{identifier}:{version}` |
| Financial data: **NO stale-while-revalidate** — consistency over performance |
| Dashboard widgets: stale-while-revalidate acceptable (5 min max lag) |
| Cache invalidated by domain events (event-driven invalidation pattern) |
| Redis pub/sub broadcasts L1 cache invalidation across all pods |

### 20.4 Read Replica Routing

| Route To | Connection |
|---|---|
| All GET endpoints (list, detail, dashboard, reports) | Read replica |
| All Command handlers | Primary write |
| Background report generation | Read replica |
| Auth operations (login, token refresh) | Primary write |
| Hangfire jobs that write | Primary write |

### 20.5 Query Performance Monitoring

- `PerformanceBehavior` logs a **Warning** if any handler takes > 500ms
- `PerformanceBehavior` logs an **Error** if any handler takes > 2000ms
- EF Core: SQL query logging **disabled** in Production
- AWS CloudWatch alarms: trigger on P95 > 500ms for 5 consecutive minutes

---

## 21. Security Rules

### 21.1 Authentication

| Rule |
|---|
| JWT RS256 (asymmetric) — private key signs, public key verifies |
| Access token expiry: **15 minutes** |
| Refresh token: opaque, stored hashed in DB, 30-day expiry, **rotating** |
| Key management: private key in **AWS KMS** — never in application memory |
| Public key distributed via `/.well-known/jwks.json` |
| OTP authentication: 6-digit, 10-minute expiry, max 3 attempts, stored hashed in Redis |
| MFA: mandatory for Owner and Admin roles in Enterprise tier |

### 21.2 Authorization

| Rule |
|---|
| ABAC + RBAC hybrid model |
| Layer 1: Endpoint-level policy (`[Authorize(Policy = "RequireAnimalWrite")]`) |
| Layer 2: Farm-scope ABAC check (`ICurrentUserService.GetAssignedFarmIds()`) |
| Layer 3: Resource-level policy (domain rules in Command Handler) |
| Layer 4: Tenant boundary (EF Core Global Query Filter + SQL RLS) |
| Return `403 Forbidden` (not `404`) for authorization failures — don't leak existence |

### 21.3 Data Security

| Rule |
|---|
| All data at rest: AES-256 encryption (AWS KMS) |
| All data in transit: TLS 1.3 mandatory |
| SQL Server RLS as a defense-in-depth backstop against cross-tenant queries |
| Application secrets: AWS Secrets Manager ONLY — never in `appsettings.json` |
| Never log secrets, tokens, passwords, OTPs, or unmasked PII |
| `[SensitiveData]` attribute on DTO properties triggers automatic masking in all logs |

### 21.4 Input Security

| Rule |
|---|
| OWASP Top 10 compliance: mandatory before MVP launch |
| SQL injection: prevented by EF Core parameterized queries; raw SQL forbidden except documented exceptions |
| XSS: Angular's default HTML sanitization; additional sanitization for any `innerHTML` binding |
| CSRF: JWT-based auth (stateless) eliminates CSRF; SameSite cookie if any cookie used |
| AWS WAF with OWASP managed rule groups on CloudFront + ALB |

### 21.5 API Security

| Rule |
|---|
| Rate limiting: per-tenant and per-user (Redis-backed) |
| `429 Too Many Requests` with `Retry-After` header |
| API versioning mandatory — `/api/v1/` from day one |
| Swagger UI: **disabled in Production** (enabled in Staging only) |
| ReDoc API reference: enabled in Production for partner integration |
| All API endpoints require authentication except: `/api/v1/identity/register`, `/api/v1/identity/verify-otp`, `/.well-known/jwks.json`, `/health` |

### 21.6 Token Security

| Rule |
|---|
| Token revocation: `TokenVersion` field on User; increment to invalidate all active tokens |
| Events triggering full revocation: password change, role change, account deactivation, suspicious login |
| Refresh token: one-time use (rotating) — theft detected immediately on next use |
| `git-secrets` pre-commit hook: blocks commits containing secret patterns |

---

## 22. Multi-Tenant Rules

### 22.1 Tenancy Model

| Tier | Model |
|---|---|
| Bittho, Khamar, Banik | Shared Database, EF Core Global Query Filter + SQL RLS |
| Banik (high-volume), NGO | Shared Database, separate schema per tenant |
| Corporation (Enterprise) | Dedicated Database |

### 22.2 The Golden Rules of Multi-Tenancy (Never Violate)

```
RULE 1: Every tenant-scoped entity MUST have TenantId UNIQUEIDENTIFIER NOT NULL
RULE 2: TenantId is set ONLY by AuditSaveChangesInterceptor — NEVER by developer code
RULE 3: EF Core Global Query Filter MUST filter by TenantId on every entity
RULE 4: SQL Server RLS policy MUST be applied to every tenant-scoped table
RULE 5: Cache keys MUST be prefixed with TenantId
RULE 6: SignalR notifications MUST be sent to TenantId-named groups only
RULE 7: Background jobs MUST explicitly set TenantId scope before processing
RULE 8: Repository queries MUST never disable the Global Query Filter
```

### 22.3 Tenant Resolution Pipeline

```
Every API request:
[1] JWT validated → extract claim: tenant_id
[2] TenantResolutionMiddleware
    → Verify tenant exists and is active (Redis cache → DB)
    → Verify subscription is active
    → If inactive → 402 Payment Required
[3] ITenantService.GetCurrentTenantId() available throughout request
[4] EF Core Global Query Filter automatically applied to all queries
```

### 22.4 Background Job Tenant Context

```
System jobs (VaccinationReminderJob):
  → Do NOT use ITenantService (no request context)
  → Query ALL active tenants using system-level context
  → For each tenant:
      → Create new IServiceScope
      → Set TenantId explicitly: tenantService.SetTenant(tenant.Id)
      → Execute per-tenant logic
      → Dispose scope

Per-tenant enqueued jobs:
  → Job arguments INCLUDE TenantId
  → Job handler sets TenantId on ITenantService before executing
```

### 22.5 Tenant Subscription Enforcement

| Rule |
|---|
| Active animal count checked against subscription tier limit on every animal creation |
| User count checked against tier limit on every user invitation |
| Farm count checked against tier limit on every farm creation |
| Downgrade blocked if current usage exceeds new tier's limits |
| Subscription expiry: 7-day grace period → read-only access → full suspension |

### 22.6 Data Isolation Verification

Integration tests MUST include:
```
Test: TenantA user cannot read TenantB's animals
Test: TenantA user cannot write to TenantB's records
Test: Background job processes only its assigned tenant's data
Test: Cache key for TenantA does not return TenantB's data
```

---

## 23. Documentation Rules

### 23.1 Documentation Hierarchy

| Document Type | Location | Audience | Update Trigger |
|---|---|---|---|
| This Constitution | `docs/constitution.md` | All engineers | Architectural review |
| Architecture Decision Records (ADR) | `docs/adr/ADR-{N}-{title}.md` | Engineering | Per architectural decision |
| API Reference | Auto-generated (Swagger/OpenAPI 3.1) | Frontend + Partners | Per API change |
| README | Repo root + per project | New developers | Per major change |
| Runbooks | `docs/runbooks/` | DevOps / On-call | Per operational procedure |
| Code Comments | Source code | Developers | With code changes |
| Postman Collection | `docs/api/` | Frontend team | Per API change |

### 23.2 ADR Format Standard

Every architectural decision is recorded in an ADR:

```markdown
File: docs/adr/ADR-{N}-{title-kebab-case}.md

# ADR-{N}: {Title}

**Status:** Proposed | Accepted | Deprecated | Superseded by ADR-{N}
**Date:** YYYY-MM-DD
**Deciders:** {List of people involved}
**Context level:** System | Component | Module

## Context
What situation motivated this decision?

## Decision Drivers
- Driver 1
- Driver 2

## Considered Options
1. Option A
2. Option B

## Decision
We chose **Option X** because...

## Consequences
### Positive
### Negative / Trade-offs
### Risks

## Related ADRs
```

### 23.3 Code Documentation Rules

| Rule |
|---|
| All `public` methods on domain entities: XML doc comments (`/// <summary>`) |
| All interfaces: XML doc comments on every method |
| Complex business logic: inline comments explaining **WHY**, not **WHAT** |
| No commented-out code in production — use `git revert` instead |
| No TODO comments without a linked ticket ID: `// TODO: F360-142 — implement rate limiting` |

### 23.4 OpenAPI Documentation Rules

Every endpoint must document:
- Summary (one line)
- Description (business context, not just technical)
- All request body fields with description
- All response codes: 200/201, 400, 401, 403, 404, 409, 422, 429, 500
- Required permission (`animals:write`)
- At least one example request body
- At least one example success + one example error response

---

## 🔐 Ratification Statement

This Constitution was derived from and is fully consistent with:

| Source | Document |
|---|---|
| PVD v1.0 | Farm360 AI Product Vision Document |
| PRD v1.0 | Farm360 AI Product Requirements Document |
| SAD v1.0 | Farm360 AI Software Architecture Document |
| DDD v1.0 | Farm360 AI Database Design Document |
| UIX v1.0 | Farm360 AI UX Design System Document |

> *Every engineering decision made on Farm360 AI must be traceable back to this Constitution. Silence from this document means you must ask for clarification — not make an assumption.*

---

*© 2026 Farm360 AI Engineering Organization. All Rights Reserved.*  
*Chief Software Architect — Farm360 AI*  
*Document ID: F360-CONST-2026-001*
