# Farm360 AI — Software Architecture Document (SAD)

**Document ID:** F360-SAD-2026-001  
**Version:** 1.0  
**Status:** Approved for Implementation  
**Prepared by:** Principal Software Architecture Office  
**Date:** July 2026  
**Parent Documents:** PRD v1.0, PVD v1.0  
**Classification:** Confidential — Engineering Use  
**Review Cycle:** Per major release

---

> *"Architecture is the set of decisions that are hard to change later. Make them deliberately, document them permanently, and challenge them regularly."*

---

## Table of Contents

1. [Document Overview](#1-document-overview)
2. [High-Level Architecture](#2-high-level-architecture)
3. [Layered Architecture — Clean Architecture](#3-layered-architecture--clean-architecture)
4. [Folder & Project Structure](#4-folder--project-structure)
5. [Dependency Diagram](#5-dependency-diagram)
6. [Communication Flow](#6-communication-flow)
7. [Authentication Flow](#7-authentication-flow)
8. [Authorization Flow](#8-authorization-flow)
9. [Multi-Tenant Design](#9-multi-tenant-design)
10. [Caching Strategy](#10-caching-strategy)
11. [Logging Strategy](#11-logging-strategy)
12. [Exception Handling Strategy](#12-exception-handling-strategy)
13. [Validation Strategy](#13-validation-strategy)
14. [Deployment Architecture](#14-deployment-architecture)
15. [CI/CD Strategy](#15-cicd-strategy)
16. [Cloud Architecture](#16-cloud-architecture)
17. [Scalability Strategy](#17-scalability-strategy)
18. [Coding Standards](#18-coding-standards)
19. [Naming Standards](#19-naming-standards)
20. [Git Branching Strategy](#20-git-branching-strategy)
21. [Documentation Standards](#21-documentation-standards)
22. [Risk Analysis](#22-risk-analysis)
23. [Architecture Decision Records (ADR)](#23-architecture-decision-records-adr)

---

## 1. Document Overview

### 1.1 Purpose

This Software Architecture Document (SAD) defines the complete technical architecture for Farm360 AI — a multi-tenant, enterprise-grade SaaS platform for livestock farm management. It establishes the canonical reference for all architectural decisions, structural patterns, technology selections, and engineering standards that govern the system.

Every section explains not just **what** the architecture is, but **why** each decision was made — because architecture without rationale produces cargo-cult engineering.

### 1.2 Architectural Goals

| Goal | Constraint |
|---|---|
| **Correctness** | The system must enforce business rules consistently across all tenants | 
| **Isolation** | One tenant's data, workload, or failure must never impact another tenant | 
| **Evolvability** | The architecture must absorb new modules, verticals, and AI features without structural rewrites |
| **Observability** | Every operation must be traceable end-to-end with structured logs, metrics, and traces |
| **Security** | Multi-layered defense; zero trust within the application boundary |
| **Performance** | Sub-500ms API responses at P95 on 3G networks across Bangladesh |
| **Operational Simplicity** | A team of 4–6 engineers must be able to run this system in production without a dedicated SRE |

### 1.3 Architectural Constraints

- **Budget constraint:** Cloud-native but cost-conscious — architecture must scale economically from 300 to 4,000 tenants without a complete infrastructure redesign
- **Team constraint:** Initial team of 4–6 full-stack engineers; architecture must minimize operational surface area
- **Network constraint:** Target users operate on 3G networks in rural Bangladesh; frontend must be aggressive about data minimization
- **Regulatory constraint:** Data must reside within AWS Mumbai (ap-south-1) or equivalent for Bangladesh regulatory alignment

### 1.4 Guiding Principles

| Principle | Application |
|---|---|
| **Explicit over implicit** | Business rules are expressed in the domain, not inferred from database structure |
| **Fail fast, fail loudly** | Invalid state should be caught at the boundary (validation layer), not silently propagated |
| **Commands change state; Queries read state** | CQRS enforces a clean mental model that prevents accidental side effects in read paths |
| **Dependency inversion at every boundary** | Higher layers define abstractions; lower layers implement them |
| **Tenant context is sacred** | The TenantId is threaded through every operation; any code path that loses tenant context is a bug |
| **Optimistic by default, pessimistic when required** | Use optimistic concurrency for most operations; pessimistic locking only for critical financial writes |

---

## 2. High-Level Architecture

### 2.1 C4 Model — System Context (Level 1)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            EXTERNAL ACTORS                                  │
│                                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐  ┌─────────────────┐ │
│  │ Farm Owner   │  │ Farm Manager │  │ Veterinarian│  │ Platform Admin  │ │
│  │ (Browser/PWA)│  │ (Browser/PWA)│  │(Browser/PWA)│  │ (Admin Portal)  │ │
│  └──────┬───────┘  └──────┬───────┘  └──────┬──────┘  └────────┬────────┘ │
└─────────┼─────────────────┼─────────────────┼───────────────────┼──────────┘
          │                 │                 │                   │
          ▼                 ▼                 ▼                   ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         FARM360 AI PLATFORM                                 │
│                                                                             │
│   Angular 22 SPA / PWA          ASP.NET Core 10 API                        │
│   ┌────────────────────┐        ┌──────────────────────┐                   │
│   │  Farm360 Web App   │◄──────►│  Farm360 API Gateway  │                  │
│   │  (Angular 22)      │  HTTPS │  (ASP.NET Core 10)    │                  │
│   └────────────────────┘        └──────────────────────┘                   │
│                                                                             │
└──────────────────────────────────────────────────────────┬──────────────────┘
                                                           │
          ┌────────────────────────────────────────────────┼────────────────┐
          ▼                    ▼                           ▼                │
   ┌─────────────┐    ┌──────────────┐            ┌─────────────┐           │
   │  SQL Server │    │    Redis     │            │  Blob Store │           │
   │  (Primary)  │    │  (Cache/    │            │  (Photos,   │           │
   │             │    │   Session)  │            │   Reports)  │           │
   └─────────────┘    └─────────────┘            └─────────────┘           │
                                                                            │
          ┌─────────────────────────────────────────────────────────────────┘
          ▼                    ▼                           ▼
   ┌─────────────┐    ┌──────────────┐            ┌─────────────┐
   │ SMS Gateway │    │ Email Service│            │  bKash/     │
   │(SSL Commerz)│    │ (AWS SES)    │            │  Nagad API  │
   └─────────────┘    └─────────────┘            └─────────────┘
```

**Decision:** A single API backend (modular monolith) is chosen over microservices for the MVP. With a team of 4–6 engineers, microservices would introduce distributed systems complexity (service discovery, inter-service communication, distributed tracing) before we have the data to justify the decomposition. The architecture is designed so that bounded contexts can be extracted to microservices in Phase 3 without rewriting business logic.

---

### 2.2 C4 Model — Container Diagram (Level 2)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         FARM360 AI CONTAINERS                               │
│                                                                             │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │ CLIENT TIER                                                          │  │
│  │                                                                      │  │
│  │  ┌─────────────────────────────────────────────────────────────┐   │  │
│  │  │  Angular 22 SPA (PWA)                                       │   │  │
│  │  │  - Feature modules: Livestock, Feeding, Health, Inventory,  │   │  │
│  │  │    Finance, Dashboard                                        │   │  │
│  │  │  - Standalone components (Angular 17+ pattern)              │   │  │
│  │  │  - Service Worker for offline capability                    │   │  │
│  │  │  - NgRx Signal Store for state management                   │   │  │
│  │  │  - Communication: REST API + SignalR WebSocket               │   │  │
│  │  └─────────────────────────────────────────────────────────────┘   │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                        │ HTTPS / WSS                       │
│  ┌─────────────────────────────────────┼────────────────────────────────┐  │
│  │ API TIER                            ▼                                │  │
│  │                                                                      │  │
│  │  ┌─────────────────────────────────────────────────────────────┐   │  │
│  │  │  ASP.NET Core 10 Web API + SignalR Hub                      │   │  │
│  │  │  - Minimal API endpoints grouped by module                  │   │  │
│  │  │  - MediatR pipeline (CQRS dispatch)                         │   │  │
│  │  │  - SignalR Hub (real-time notifications)                    │   │  │
│  │  │  - Hangfire Server (background job processing)              │   │  │
│  │  │  - JWT Bearer authentication                                │   │  │
│  │  │  - FluentValidation pipeline behaviors                      │   │  │
│  │  │  - Serilog structured logging pipeline                      │   │  │
│  │  └─────────────────────────────────────────────────────────────┘   │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                    │              │              │                          │
│                    ▼              ▼              ▼                          │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │ DATA TIER                                                            │  │
│  │  ┌────────────┐  ┌────────────┐  ┌──────────┐  ┌──────────────────┐ │  │
│  │  │ SQL Server │  │   Redis    │  │ Hangfire │  │   Azure Blob /   │ │  │
│  │  │ (Primary + │  │ (L2 Cache  │  │  DB      │  │   S3 Storage     │ │  │
│  │  │ Read Rep.) │  │ + PubSub)  │  │          │  │                  │ │  │
│  │  └────────────┘  └────────────┘  └──────────┘  └──────────────────┘ │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Layered Architecture — Clean Architecture

### 3.1 Architectural Philosophy

Farm360 AI adopts **Clean Architecture** (as formalized by Robert C. Martin), organized around the **Dependency Rule**: source code dependencies must always point inward — toward the domain. Outer layers depend on inner layers. Inner layers have zero knowledge of outer layers.

This is non-negotiable. A domain entity must never reference a controller, a database context, or an HTTP concept. If it does, we have violated the architecture.

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   ┌─────────────────────────────────────────────────────────┐  │
│   │  PRESENTATION LAYER (Angular SPA + API Controllers)     │  │
│   │  Handles HTTP, Angular routing, UI rendering            │  │
│   │                                                         │  │
│   │   ┌─────────────────────────────────────────────────┐  │  │
│   │   │  APPLICATION LAYER (CQRS + MediatR)             │  │  │
│   │   │  Use cases, Commands, Queries, DTOs, Behaviors  │  │  │
│   │   │                                                 │  │  │
│   │   │   ┌─────────────────────────────────────────┐  │  │  │
│   │   │   │  DOMAIN LAYER (DDD Core)                │  │  │  │
│   │   │   │  Entities, Value Objects, Aggregates    │  │  │  │
│   │   │   │  Domain Events, Repository Interfaces   │  │  │  │
│   │   │   │  Domain Services, Business Rules        │  │  │  │
│   │   │   │                                         │  │  │  │
│   │   │   │  ← NO DEPENDENCIES ON ANYTHING ELSE →   │  │  │  │
│   │   │   └─────────────────────────────────────────┘  │  │  │
│   │   │                                                 │  │  │
│   │   └─────────────────────────────────────────────────┘  │  │
│   │                                                         │  │
│   └─────────────────────────────────────────────────────────┘  │
│                                                                 │
│  INFRASTRUCTURE LAYER (EF Core, Redis, Serilog, External APIs)  │
│  Implements interfaces defined in Domain/Application layers     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

              DEPENDENCIES FLOW INWARD ONLY ←←←
```

### 3.2 Layer Responsibilities

#### Layer 1: Domain Layer (`Farm360.Domain`)

The heart of the system. Contains everything that represents the business as it truly is, independent of any technology.

| Component | Responsibility | Example |
|---|---|---|
| **Entities** | Objects with identity and lifecycle | `Animal`, `Shed`, `VaccinationRecord` |
| **Value Objects** | Immutable, identity-less concepts defined by their attributes | `Money`, `AnimalTag`, `Weight`, `NutritionalProfile` |
| **Aggregates** | Cluster of entities and value objects with one root | `Farm` aggregate (root: `Farm`, children: `Shed`, `Pen`) |
| **Domain Events** | Record that something meaningful happened in the domain | `AnimalSoldEvent`, `VaccinationOverdueEvent` |
| **Repository Interfaces** | Contracts for data persistence (implemented in Infrastructure) | `IAnimalRepository`, `IFeedFormulaRepository` |
| **Domain Services** | Business logic that doesn't naturally fit in one entity | `FcrCalculationService`, `BreakEvenCalculatorService` |
| **Specifications** | Encapsulate query logic as reusable domain concepts | `AnimalsDueForVaccinationSpec`, `ActiveAnimalsInShedException` |
| **Exceptions** | Domain-specific exceptions representing rule violations | `AnimalQuarantinedCannotBeSoldException` |

**Critical Rule:** The Domain layer has **zero NuGet dependencies** except potentially `MediatR.Contracts` for domain event interfaces. No EF Core. No third-party libraries. Pure C#.

---

#### Layer 2: Application Layer (`Farm360.Application`)

Orchestrates use cases. Knows **what** to do but not **how** the infrastructure does it. Uses interfaces defined in Domain.

| Component | Responsibility |
|---|---|
| **Commands** | Represent intent to change state (CQRS write side) |
| **Command Handlers** | Execute the command, enforce business rules via domain, persist via repository |
| **Queries** | Represent intent to read data (CQRS read side) |
| **Query Handlers** | Fetch and project data; may read from read model or database directly |
| **DTOs** | Data Transfer Objects — the shape of data entering and leaving use cases |
| **Pipeline Behaviors** | Cross-cutting concerns as MediatR pipeline decorators (validation, logging, caching, transactions) |
| **Application Services** | Orchestration that spans multiple aggregates in a single use case |
| **Interfaces** | Abstractions for infrastructure concerns (IEmailService, ISmsService, ICurrentUserService) |
| **Mappings** | AutoMapper or manual mapping profiles between domain entities and DTOs |
| **Validators** | FluentValidation validators per command/query |

**Dependencies:** Domain layer only. No EF Core. No HTTP. No framework-specific libraries.

---

#### Layer 3: Infrastructure Layer (`Farm360.Infrastructure`)

Implements every interface defined in Domain and Application layers. This is where the "how" lives.

| Component | Responsibility |
|---|---|
| **Persistence** | EF Core `DbContext`, Entity configurations, Repository implementations, Migrations |
| **Identity** | ASP.NET Core Identity integration, JWT service implementation |
| **Caching** | Redis `IDistributedCache` implementation, cache decorators |
| **Messaging** | SignalR hub implementations, domain event publishing |
| **Background Jobs** | Hangfire job definitions and scheduling configurations |
| **External Services** | SMS gateway client, email client, payment gateway client, blob storage client |
| **Logging** | Serilog sink configurations |
| **Tenant Resolution** | Middleware and services for resolving current tenant from JWT |

**Dependencies:** Domain, Application, and all external packages (EF Core, Redis, Hangfire, Serilog sinks, etc.).

---

#### Layer 4: Presentation Layer (`Farm360.Api`)

The entry point for all HTTP traffic. Thin by design.

| Component | Responsibility |
|---|---|
| **Minimal API Endpoints** | Grouped by module/feature; delegate immediately to MediatR |
| **SignalR Hubs** | Real-time event hub for dashboard and notification streaming |
| **Middleware** | Tenant resolution, exception handling, correlation ID injection |
| **Filters** | Action filters for permission checking, model state |
| **Program.cs** | Composition root — DI wiring, middleware pipeline configuration |

**Design Rule:** No business logic in controllers/endpoints. An endpoint's job is: authenticate → authorize → deserialize → dispatch to MediatR → serialize response. Nothing more.

---

#### Layer 5: Presentation Layer (`Farm360.Web` — Angular 22)

The browser-side presentation tier. A separate build artifact that communicates exclusively via the API.

| Component | Responsibility |
|---|---|
| **Feature Modules** | Encapsulated Angular modules per domain feature |
| **Standalone Components** | Fine-grained UI components with no module overhead |
| **Services** | HTTP client wrappers, business-specific Angular services |
| **State (NgRx Signal Store)** | Reactive state per feature; no global god-store |
| **Guards** | Route-level access control based on user role |
| **Interceptors** | JWT injection, error handling, offline queue |
| **Service Worker** | PWA offline capabilities, background sync |

---

## 4. Folder & Project Structure

### 4.1 Solution Structure (Backend — .NET 10)

```
Farm360.sln
│
├── src/
│   │
│   ├── Farm360.Domain/                    ← Domain Layer
│   │   ├── Common/
│   │   │   ├── BaseEntity.cs
│   │   │   ├── AuditableEntity.cs
│   │   │   ├── BaseValueObject.cs
│   │   │   ├── IAggregateRoot.cs
│   │   │   └── IDomainEvent.cs
│   │   │
│   │   ├── Entities/
│   │   │   ├── Farm.cs
│   │   │   ├── Shed.cs
│   │   │   ├── Pen.cs
│   │   │   ├── Animal.cs
│   │   │   ├── AnimalBatch.cs
│   │   │   ├── WeightRecord.cs
│   │   │   ├── BreedingRecord.cs
│   │   │   ├── FeedFormula.cs
│   │   │   ├── FeedIngredient.cs
│   │   │   ├── FeedConsumptionLog.cs
│   │   │   ├── VaccinationProtocol.cs
│   │   │   ├── VaccinationRecord.cs
│   │   │   ├── TreatmentRecord.cs
│   │   │   ├── DiseaseIncident.cs
│   │   │   ├── InventoryItem.cs
│   │   │   ├── StockTransaction.cs
│   │   │   ├── FinancialEntry.cs
│   │   │   ├── AnimalCostLedger.cs
│   │   │   └── Tenant.cs
│   │   │
│   │   ├── ValueObjects/
│   │   │   ├── Money.cs
│   │   │   ├── AnimalTag.cs
│   │   │   ├── Weight.cs
│   │   │   ├── NutritionalProfile.cs
│   │   │   ├── GestationPeriod.cs
│   │   │   ├── DateRange.cs
│   │   │   └── Address.cs
│   │   │
│   │   ├── Aggregates/
│   │   │   ├── FarmAggregate/
│   │   │   │   └── (Farm is the root; Shed, Pen are part of this aggregate)
│   │   │   ├── AnimalAggregate/
│   │   │   │   └── (Animal is the root; WeightRecord, BreedingRecord are children)
│   │   │   └── BatchAggregate/
│   │   │
│   │   ├── DomainEvents/
│   │   │   ├── AnimalSoldEvent.cs
│   │   │   ├── AnimalDiedEvent.cs
│   │   │   ├── VaccinationOverdueEvent.cs
│   │   │   ├── LowStockDetectedEvent.cs
│   │   │   ├── AnimalQuarantinedEvent.cs
│   │   │   └── FeedConsumptionLoggedEvent.cs
│   │   │
│   │   ├── Enumerations/
│   │   │   ├── AnimalStatus.cs
│   │   │   ├── AnimalSpecies.cs
│   │   │   ├── AnimalSex.cs
│   │   │   ├── DisposalReason.cs
│   │   │   ├── FinancialEntryType.cs
│   │   │   ├── StockTransactionType.cs
│   │   │   └── UserRole.cs
│   │   │
│   │   ├── Exceptions/
│   │   │   ├── DomainException.cs
│   │   │   ├── AnimalQuarantinedException.cs
│   │   │   ├── InsufficientStockException.cs
│   │   │   ├── InvalidAnimalStateTransitionException.cs
│   │   │   └── ClosedPeriodModificationException.cs
│   │   │
│   │   ├── Interfaces/
│   │   │   ├── Repositories/
│   │   │   │   ├── IAnimalRepository.cs
│   │   │   │   ├── IFarmRepository.cs
│   │   │   │   ├── IFeedFormulaRepository.cs
│   │   │   │   ├── IHealthRepository.cs
│   │   │   │   ├── IInventoryRepository.cs
│   │   │   │   ├── IFinanceRepository.cs
│   │   │   │   └── IGenericRepository.cs
│   │   │   └── Services/
│   │   │       ├── IFcrCalculationService.cs
│   │   │       └── IBreakEvenCalculatorService.cs
│   │   │
│   │   └── Specifications/
│   │       ├── BaseSpecification.cs
│   │       ├── AnimalsDueForVaccinationSpec.cs
│   │       ├── LowStockItemsSpec.cs
│   │       └── ActiveAnimalsInBatchSpec.cs
│   │
│   ├── Farm360.Application/               ← Application Layer
│   │   ├── Common/
│   │   │   ├── Behaviors/
│   │   │   │   ├── ValidationBehavior.cs
│   │   │   │   ├── LoggingBehavior.cs
│   │   │   │   ├── PerformanceBehavior.cs
│   │   │   │   ├── TransactionBehavior.cs
│   │   │   │   ├── CachingBehavior.cs
│   │   │   │   └── AuditBehavior.cs
│   │   │   ├── Exceptions/
│   │   │   │   ├── NotFoundException.cs
│   │   │   │   ├── ForbiddenAccessException.cs
│   │   │   │   ├── ValidationException.cs
│   │   │   │   └── ConflictException.cs
│   │   │   ├── Interfaces/
│   │   │   │   ├── ICurrentUserService.cs
│   │   │   │   ├── ITenantService.cs
│   │   │   │   ├── IDateTimeService.cs
│   │   │   │   ├── IEmailService.cs
│   │   │   │   ├── ISmsService.cs
│   │   │   │   ├── IBlobStorageService.cs
│   │   │   │   ├── ICacheService.cs
│   │   │   │   ├── IAuditService.cs
│   │   │   │   └── INotificationService.cs
│   │   │   ├── Models/
│   │   │   │   ├── PaginatedResult.cs
│   │   │   │   ├── Result.cs
│   │   │   │   └── CacheableQuery.cs
│   │   │   └── Mappings/
│   │   │       └── MappingProfile.cs
│   │   │
│   │   ├── Features/
│   │   │   ├── Animals/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── RegisterAnimal/
│   │   │   │   │   │   ├── RegisterAnimalCommand.cs
│   │   │   │   │   │   ├── RegisterAnimalCommandHandler.cs
│   │   │   │   │   │   └── RegisterAnimalCommandValidator.cs
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
│   │   │   │
│   │   │   ├── Feeding/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── CreateFeedFormula/
│   │   │   │   │   ├── AssignFeedingSchedule/
│   │   │   │   │   └── LogFeedConsumption/
│   │   │   │   └── Queries/
│   │   │   │       ├── GetFeedFormulas/
│   │   │   │       ├── GetFeedingReport/
│   │   │   │       └── GetBatchFcr/
│   │   │   │
│   │   │   ├── Health/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── CreateVaccinationProtocol/
│   │   │   │   │   ├── RecordVaccination/
│   │   │   │   │   ├── RecordTreatment/
│   │   │   │   │   ├── ReportDiseaseIncident/
│   │   │   │   │   └── ReleaseFromQuarantine/
│   │   │   │   └── Queries/
│   │   │   │       ├── GetVaccinationsDue/
│   │   │   │       ├── GetAnimalHealthHistory/
│   │   │   │       └── GetHerdHealthStatus/
│   │   │   │
│   │   │   ├── Inventory/
│   │   │   │   ├── Commands/
│   │   │   │   └── Queries/
│   │   │   │
│   │   │   ├── Finance/
│   │   │   │   ├── Commands/
│   │   │   │   └── Queries/
│   │   │   │
│   │   │   ├── Dashboard/
│   │   │   │   └── Queries/
│   │   │   │
│   │   │   ├── Farms/
│   │   │   │   ├── Commands/
│   │   │   │   └── Queries/
│   │   │   │
│   │   │   ├── Tenants/
│   │   │   │   ├── Commands/
│   │   │   │   └── Queries/
│   │   │   │
│   │   │   └── Identity/
│   │   │       ├── Commands/
│   │   │       │   ├── Register/
│   │   │       │   ├── Login/
│   │   │       │   ├── RefreshToken/
│   │   │       │   └── InviteUser/
│   │   │       └── Queries/
│   │   │
│   │   └── DomainEventHandlers/
│   │       ├── AnimalSoldEventHandler.cs
│   │       ├── FeedConsumptionLoggedEventHandler.cs
│   │       ├── VaccinationOverdueEventHandler.cs
│   │       └── LowStockDetectedEventHandler.cs
│   │
│   ├── Farm360.Infrastructure/            ← Infrastructure Layer
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── ApplicationDbContextFactory.cs
│   │   │   ├── Configurations/
│   │   │   │   ├── AnimalConfiguration.cs
│   │   │   │   ├── FarmConfiguration.cs
│   │   │   │   ├── FeedFormulaConfiguration.cs
│   │   │   │   └── (one per entity)
│   │   │   ├── Repositories/
│   │   │   │   ├── GenericRepository.cs
│   │   │   │   ├── AnimalRepository.cs
│   │   │   │   ├── FeedRepository.cs
│   │   │   │   └── (one per domain repository interface)
│   │   │   ├── Migrations/
│   │   │   │   └── (EF Core auto-generated)
│   │   │   ├── Interceptors/
│   │   │   │   ├── AuditSaveChangesInterceptor.cs
│   │   │   │   └── TenantFilterInterceptor.cs
│   │   │   └── Seeders/
│   │   │       ├── IngredientCatalogSeeder.cs
│   │   │       └── SystemRoleSeeder.cs
│   │   │
│   │   ├── Identity/
│   │   │   ├── ApplicationUser.cs
│   │   │   ├── JwtTokenService.cs
│   │   │   ├── OtpService.cs
│   │   │   └── CurrentUserService.cs
│   │   │
│   │   ├── Caching/
│   │   │   ├── RedisCacheService.cs
│   │   │   └── CacheKeyBuilder.cs
│   │   │
│   │   ├── BackgroundJobs/
│   │   │   ├── VaccinationReminderJob.cs
│   │   │   ├── MonthlyReportGeneratorJob.cs
│   │   │   ├── SubscriptionExpiryJob.cs
│   │   │   ├── LowStockCheckerJob.cs
│   │   │   └── HangfireJobScheduler.cs
│   │   │
│   │   ├── Messaging/
│   │   │   ├── SignalR/
│   │   │   │   ├── NotificationHub.cs
│   │   │   │   └── HubConnectionManager.cs
│   │   │   └── DomainEventPublisher.cs
│   │   │
│   │   ├── ExternalServices/
│   │   │   ├── Sms/
│   │   │   │   ├── SslCommerzSmsService.cs
│   │   │   │   └── InfobipSmsService.cs (failover)
│   │   │   ├── Email/
│   │   │   │   └── AwsSesEmailService.cs
│   │   │   ├── Payment/
│   │   │   │   ├── BkashPaymentService.cs
│   │   │   │   └── NagadPaymentService.cs
│   │   │   └── Storage/
│   │   │       └── S3BlobStorageService.cs
│   │   │
│   │   ├── Logging/
│   │   │   └── SerilogConfiguration.cs
│   │   │
│   │   └── DependencyInjection/
│   │       └── InfrastructureServiceExtensions.cs
│   │
│   └── Farm360.Api/                       ← Presentation Layer (API)
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Endpoints/
│       │   ├── AnimalsEndpoints.cs
│       │   ├── FeedingEndpoints.cs
│       │   ├── HealthEndpoints.cs
│       │   ├── InventoryEndpoints.cs
│       │   ├── FinanceEndpoints.cs
│       │   ├── DashboardEndpoints.cs
│       │   ├── FarmsEndpoints.cs
│       │   └── IdentityEndpoints.cs
│       ├── Hubs/
│       │   └── FarmNotificationHub.cs
│       ├── Middleware/
│       │   ├── TenantResolutionMiddleware.cs
│       │   ├── GlobalExceptionMiddleware.cs
│       │   ├── CorrelationIdMiddleware.cs
│       │   └── RequestLoggingMiddleware.cs
│       ├── Filters/
│       │   └── PermissionFilter.cs
│       └── DependencyInjection/
│           └── ApiServiceExtensions.cs
│
├── tests/
│   ├── Farm360.Domain.UnitTests/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   └── DomainServices/
│   │
│   ├── Farm360.Application.UnitTests/
│   │   ├── Animals/
│   │   ├── Feeding/
│   │   ├── Health/
│   │   └── Finance/
│   │
│   ├── Farm360.Application.IntegrationTests/
│   │   ├── TestBase.cs
│   │   ├── CustomWebApplicationFactory.cs
│   │   └── Features/
│   │
│   └── Farm360.Api.FunctionalTests/
│       └── (End-to-end API tests)
│
├── tools/
│   ├── scripts/
│   │   ├── migrate.sh
│   │   └── seed.sh
│   └── analyzers/
│
└── docs/
    ├── architecture/
    ├── adr/
    └── api/
```

### 4.2 Frontend Structure (Angular 22)

```
farm360-web/
│
├── src/
│   ├── app/
│   │   ├── core/                           ← Singleton services, guards, interceptors
│   │   │   ├── auth/
│   │   │   │   ├── auth.service.ts
│   │   │   │   ├── auth.guard.ts
│   │   │   │   └── token-storage.service.ts
│   │   │   ├── http/
│   │   │   │   ├── jwt.interceptor.ts
│   │   │   │   ├── error.interceptor.ts
│   │   │   │   └── offline-queue.interceptor.ts
│   │   │   ├── tenant/
│   │   │   │   └── tenant-context.service.ts
│   │   │   ├── signalr/
│   │   │   │   └── notification-hub.service.ts
│   │   │   └── i18n/
│   │   │       └── (translation loader)
│   │   │
│   │   ├── shared/                         ← Reusable UI components, pipes, directives
│   │   │   ├── components/
│   │   │   │   ├── data-table/
│   │   │   │   ├── confirmation-dialog/
│   │   │   │   ├── alert-badge/
│   │   │   │   ├── currency-input/
│   │   │   │   ├── date-picker/
│   │   │   │   └── loading-spinner/
│   │   │   ├── pipes/
│   │   │   │   ├── bdt-currency.pipe.ts
│   │   │   │   ├── bangla-date.pipe.ts
│   │   │   │   └── animal-age.pipe.ts
│   │   │   └── directives/
│   │   │       ├── has-permission.directive.ts
│   │   │       └── tenant-scope.directive.ts
│   │   │
│   │   ├── features/                       ← Feature modules (lazy-loaded)
│   │   │   ├── dashboard/
│   │   │   │   ├── dashboard.routes.ts
│   │   │   │   ├── dashboard.component.ts
│   │   │   │   ├── store/
│   │   │   │   │   └── dashboard.store.ts  ← NgRx Signal Store
│   │   │   │   └── components/
│   │   │   │
│   │   │   ├── livestock/
│   │   │   │   ├── livestock.routes.ts
│   │   │   │   ├── animal-list/
│   │   │   │   ├── animal-detail/
│   │   │   │   ├── animal-form/
│   │   │   │   ├── batch-management/
│   │   │   │   ├── weight-tracker/
│   │   │   │   └── store/
│   │   │   │       └── livestock.store.ts
│   │   │   │
│   │   │   ├── feeding/
│   │   │   ├── health/
│   │   │   ├── inventory/
│   │   │   ├── finance/
│   │   │   └── settings/
│   │   │       ├── farm-management/
│   │   │       ├── user-management/
│   │   │       └── subscription/
│   │   │
│   │   ├── layout/                         ← Shell layout components
│   │   │   ├── shell/
│   │   │   ├── sidebar/
│   │   │   ├── topbar/
│   │   │   └── notification-panel/
│   │   │
│   │   └── app.routes.ts                   ← Root routing (lazy loads features)
│   │
│   ├── assets/
│   │   ├── i18n/
│   │   │   ├── bn.json
│   │   │   └── en.json
│   │   └── images/
│   │
│   ├── environments/
│   │   ├── environment.ts
│   │   └── environment.prod.ts
│   │
│   ├── styles/
│   │   ├── _variables.scss
│   │   ├── _typography.scss
│   │   ├── _layout.scss
│   │   └── styles.scss
│   │
│   └── service-worker/
│       └── (PWA service worker configuration)
│
├── angular.json
├── package.json
├── tsconfig.json
└── .eslintrc.json
```

---

## 5. Dependency Diagram

### 5.1 Project Reference Graph

```
Farm360.Api
    │
    ├──► Farm360.Application
    │         │
    │         ├──► Farm360.Domain
    │         │         │
    │         │         └── (No external dependencies)
    │         │
    │         └── (Depends on Domain interfaces only)
    │
    └──► Farm360.Infrastructure
              │
              ├──► Farm360.Application (implements interfaces)
              ├──► Farm360.Domain
              └──► [External: EF Core, Redis, Hangfire, Serilog, etc.]

Farm360.Api ──► Farm360.Application
Farm360.Api ──► Farm360.Infrastructure (for DI registration ONLY)
Farm360.Infrastructure ──► Farm360.Application
Farm360.Infrastructure ──► Farm360.Domain
Farm360.Application ──► Farm360.Domain

FORBIDDEN REFERENCES:
Farm360.Domain ──✗──► Farm360.Application
Farm360.Domain ──✗──► Farm360.Infrastructure
Farm360.Domain ──✗──► Farm360.Api
Farm360.Application ──✗──► Farm360.Infrastructure
Farm360.Application ──✗──► Farm360.Api
```

**Enforcement:** A `.editorconfig` rule and `dotnet-architecture-tests` project enforces these boundaries at CI build time using `NetArchTest.Rules`. Any PR introducing a forbidden reference fails the build automatically.

### 5.2 MediatR Pipeline Order

```
HTTP Request
     │
     ▼
[1] CorrelationIdMiddleware        → Injects X-Correlation-ID
[2] RequestLoggingMiddleware       → Logs request start with correlation ID
[3] TenantResolutionMiddleware     → Extracts TenantId from JWT, sets ITenantService
[4] JWT Authentication             → Validates Bearer token
[5] Authorization                  → Policy/permission check
     │
     ▼ Controller/Endpoint dispatches to MediatR
     │
[6] LoggingBehavior               → Logs command/query name + handler start
[7] ValidationBehavior            → Runs FluentValidation; throws if invalid
[8] PerformanceBehavior           → Starts stopwatch
[9] TransactionBehavior           → Wraps Commands in SQL transaction
[10] CachingBehavior              → Returns cached result for ICacheableQuery
[11] AuditBehavior                → Records pre/post state for auditable commands
     │
     ▼ Actual Handler executes
     │
[12] Domain Events dispatched     → After transaction commit
[13] Domain Event Handlers        → Side effects (notifications, inventory deductions)
     │
     ▼ Response returned
[14] PerformanceBehavior          → Logs elapsed time; warns if > 500ms
[15] RequestLoggingMiddleware     → Logs response status code and duration
```

---

## 6. Communication Flow

### 6.1 Synchronous Request-Response (REST)

```
Browser (Angular)
    │
    │  HTTPS POST /api/v1/animals
    │  Headers: Authorization: Bearer {jwt}
    │           X-Tenant-Id: {tenantId} (redundant; extracted from JWT)
    │           X-Correlation-Id: {uuid}
    │           Content-Language: bn-BD
    ▼
AWS Application Load Balancer
    │
    ▼
ASP.NET Core Kestrel (API Pod)
    │
    ├── Middleware: Extract CorrelationId
    ├── Middleware: Resolve Tenant from JWT claim
    ├── Middleware: JWT validation
    ├── Endpoint: POST /animals → RegisterAnimalCommand
    ├── MediatR Pipeline (see §5.2)
    ├── Handler: RegisterAnimalCommandHandler
    │   ├── Domain: Animal.Create(...) → raises AnimalCreatedEvent
    │   ├── Repository: IAnimalRepository.AddAsync(animal)
    │   └── UoW: SaveChangesAsync() → EF Core → SQL Server
    │
    ├── Domain Events published via MediatR
    │   └── AnimalCreatedEventHandler → posts to Finance module (initial cost)
    │
    └── Response: 201 Created { animalId, ... }
    │
    ▼
Browser receives response → NgRx Signal Store updated → UI re-renders
```

### 6.2 Real-Time Communication (SignalR)

```
Scenario: Vaccination overdue alert fires from Hangfire background job

Hangfire Job: VaccinationReminderJob (runs daily at 07:00 BDT)
    │
    │ Queries all overdue vaccinations across all active tenants
    ▼
For each overdue vaccination:
    │
    ├── INotificationService.SendAsync(tenantId, userId, notification)
    │
    ▼
NotificationService:
    ├── Persist notification to DB (for in-app notification center)
    ├── ISmsService.SendAsync(phoneNumber, message)  [critical alerts]
    └── IHubContext<FarmNotificationHub>.Clients
            .Group(tenantId)                         [all users of this tenant]
            .SendAsync("ReceiveNotification", dto)
    │
    ▼
SignalR Hub (WebSocket / SSE / Long Poll fallback)
    │
    ▼
Angular NotificationHubService (client):
    └── hubConnection.on("ReceiveNotification", handler)
        └── NgRx Signal Store: notifications updated
            └── Notification badge count incremented in UI
```

### 6.3 Background Job Communication (Hangfire)

```
Trigger Types:
─────────────

[Scheduled Cron Jobs]
  • VaccinationReminderJob        → Daily 07:00 BDT
  • LowStockCheckerJob            → Every 6 hours
  • SubscriptionExpiryReminderJob → Daily 09:00 BDT
  • MonthlyReportGeneratorJob     → 1st of month, 06:00 BDT

[Enqueued from Application Code]
  • PdfReportGenerationJob        → Triggered when user requests export
  • SmsDeliveryJob                → Triggered from notification pipeline
  • InventoryValuationJob         → Triggered after bulk stock-in

[Continuations (chained jobs)]
  • MonthlyReportGeneratorJob
      → Continues to: EmailReportJob (sends report to Owner)
      → Continues to: SignalR notification (report ready)

Hangfire Dashboard:
  • Accessible at /hangfire (admin role only)
  • Protected by role-based authorization
  • Displays: queued, processing, succeeded, failed jobs
  • Failed jobs: auto-retry 5 times with exponential backoff
```

### 6.4 Domain Event Flow

```
Domain Entity (e.g., Animal)
    │
    │  .AddDomainEvent(new AnimalSoldEvent(animalId, salePrice))
    ▼
In-memory domain event queue (held on entity until SaveChanges)
    │
    │  EF Core SaveChanges interceptor dispatches events after commit
    ▼
MediatR.Publish(domainEvent)
    │
    ├──► AnimalSoldEventHandler:
    │       ├── Post income to Finance module (FinancialEntry.Income)
    │       └── Recalculate batch P&L
    │
    └──► AnimalSoldAuditHandler:
            └── Write enriched audit log entry
```

---

## 7. Authentication Flow

### 7.1 Registration & OTP Flow

```
Step 1: User Submits Registration
────────────────────────────────
Angular → POST /api/v1/identity/register
Body: { organizationName, phone, email, farmType }

Step 2: API Validates & Creates Pending Account
────────────────────────────────────────────────
→ RegisterUserCommand dispatched
→ FluentValidation: phone format, email format, duplicate check
→ ApplicationUser created (status: Pending)
→ OTP generated (6-digit, 10-minute expiry)
→ OTP stored in Redis: key = "otp:{phone}" value = {hashed-otp, expiry, attempt-count}
→ SMS sent via ISmsService
→ Response: 200 OK { message: "OTP sent" } (no user data exposed)

Step 3: User Submits OTP
────────────────────────
Angular → POST /api/v1/identity/verify-otp
Body: { phone, otp }

Step 4: OTP Verification
────────────────────────
→ VerifyOtpCommand dispatched
→ OTP retrieved from Redis; hash compared
→ Attempt count checked (max 3 per OTP lifecycle)
→ If valid: user status → Active; Tenant created; Welcome notification sent
→ OTP key deleted from Redis
→ Access Token (15 min) + Refresh Token (30 days) issued
→ Response: 200 OK { accessToken, refreshToken, tenantId, userId, roles }
```

### 7.2 JWT Token Structure

```
Header: { "alg": "RS256", "typ": "JWT" }

Payload:
{
  "sub": "user-uuid",
  "tenant_id": "tenant-uuid",
  "org_name": "Rahim Farms",
  "email": "rahim@example.com",
  "phone": "+8801711XXXXXX",
  "roles": ["FarmManager"],
  "permissions": ["animals:read", "animals:write", "health:read", "health:write"],
  "farm_ids": ["farm-uuid-1", "farm-uuid-2"],
  "jti": "token-uuid",
  "iat": 1751234567,
  "exp": 1751235467,
  "iss": "farm360.ai",
  "aud": "farm360.api"
}

Signing: RS256 (asymmetric) — private key signs, public key verifies
Key Rotation: 90-day rotation cycle via AWS KMS
```

**Decision:** RS256 over HS256 because with RS256, the public key can be distributed to multiple services (future microservices, third-party integrations) without ever sharing the signing secret. This is a one-time architectural investment that pays off in Phase 3.

### 7.3 Token Refresh Flow

```
Access Token expires (15 minutes)
    │
    ▼
JWT Interceptor (Angular) detects 401 Unauthorized
    │
    ▼
POST /api/v1/identity/refresh
Body: { refreshToken }
    │
    ▼
API: Validates refresh token (not expired, not revoked)
→ Retrieves stored refresh token from DB (stored as hash)
→ Issues new Access Token (15 min)
→ Issues new Refresh Token (30 days — rotating refresh tokens)
→ Invalidates old refresh token (one-time use)
    │
    ▼
Angular: Retries original failed request with new access token
```

### 7.4 Token Revocation

```
Events that revoke ALL active tokens for a user:
  • Password change
  • Role change by Owner
  • Account deactivation
  • Suspicious login detected (new device/location)

Mechanism:
  → User's "TokenVersion" field incremented in DB
  → Token validation checks: token.tokenVersion == user.tokenVersion
  → All existing tokens with old version are rejected
  → O(1) revocation without Redis blacklist overhead at scale
```

---

## 8. Authorization Flow

### 8.1 Authorization Model: ABAC + RBAC Hybrid

Farm360 uses a **hybrid model**: Role-Based Access Control (RBAC) defines the baseline permissions for each role, and Attribute-Based Access Control (ABAC) enforces fine-grained tenant/farm-scope rules.

**Why not pure RBAC?** RBAC alone cannot handle: "Farm Manager of Farm A can see Farm A's animals but not Farm B's animals within the same tenant." This requires attribute-based policy evaluation.

### 8.2 Role Permission Matrix

| Permission | Owner | Farm Mgr | Vet | Worker | Accountant | Viewer |
|---|---|---|---|---|---|---|
| `tenant:manage` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `users:manage` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `farms:manage` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `animals:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `animals:write` | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ |
| `animals:delete` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| `health:read` | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| `health:write` | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `health:quarantine` | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `feeding:write` | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ |
| `inventory:write` | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ |
| `inventory:stock-override` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| `finance:read` | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `finance:write` | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `finance:export` | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ |
| `audit:read` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `subscription:manage` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |

### 8.3 Authorization Pipeline

```
HTTP Request → JWT validated → Claims extracted
    │
    ▼
[Layer 1: Endpoint-level Policy]
  → [Authorize(Policy = "RequireAnimalWrite")]
  → PolicyHandler checks: user.permissions.Contains("animals:write")
  → Fail → 403 Forbidden

[Layer 2: Farm-Scope Check (ABAC)]
  → ICurrentUserService.GetAssignedFarmIds()
  → farmId in request must be in user's assigned farm list
  → Fail → 403 Forbidden (not 404 — don't leak existence)

[Layer 3: Resource-Level Policy]
  → Some operations require specific resource state
  → e.g., "Can only approve a sale if animal is Active and not Quarantined"
  → Enforced in the Command Handler via domain rules

[Layer 4: Tenant Boundary]
  → All repository queries automatically filter by TenantId
  → Global query filter on DbContext ensures this is never bypassed
  → Enforced: an AnimalRepository.GetByIdAsync(id) will return null
    if the animal belongs to a different tenant, even if the id is correct
```

### 8.4 Permission Claims in JWT

Permissions are embedded in the JWT at login time (computed from role + farm assignments). This avoids a DB lookup on every request.

**Cache invalidation:** When a user's role or farm assignments change, their `TokenVersion` is incremented, forcing re-login and a fresh JWT with updated permissions.

---

## 9. Multi-Tenant Design

### 9.1 Tenancy Model Selection

Farm360 uses a **Shared Database, Separate Schema** model for SME tiers, and a **Separate Database** option for Enterprise tenants.

| Model | Tiers | Rationale |
|---|---|---|
| Shared DB, Tenant-scoped rows with Global Query Filter | Free, Khamar, Banik | Cost-efficient; strong isolation via EF Core global filters + RLS |
| Shared DB, Separate Schema per tenant | Banik, NGO (high-volume) | Schema-level isolation; easier tenant data export |
| Dedicated Database | Corporation (Enterprise) | Maximum isolation; custom maintenance windows; data sovereignty |

**Decision rationale:** Starting with shared database with global query filters is the right MVP choice. The Tenant resolution and isolation pattern is identical regardless of the underlying model. Migrating a tenant from shared to dedicated database is an operational operation (data copy + connection string update), not an architectural change.

### 9.2 Tenant Resolution Pipeline

```
Every API request:
    │
    ▼
[1] JWT validated → extract claim: tenant_id = "abc-123"
    │
    ▼
[2] TenantResolutionMiddleware
    → ITenantService.SetCurrentTenant("abc-123")
    → Verifies tenant exists and is active in Redis cache
      (cache miss → DB lookup → cache write, TTL = 5 minutes)
    → Verifies subscription is active (not expired/suspended)
    → If inactive → 402 Payment Required
    │
    ▼
[3] ITenantService.GetCurrentTenantId() available throughout request
    → Injected into: DbContext, Repositories, Services
    │
    ▼
[4] EF Core Global Query Filter (applied at DbContext level)
    → All queries automatically include: WHERE TenantId = @currentTenantId
    → Developer cannot "forget" to filter — the filter is structural
```

### 9.3 EF Core Multi-Tenant Implementation

```
ApplicationDbContext construction:
  → Receives ITenantService via DI
  → OnModelCreating: for each tenant-scoped entity, applies:
    HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId)

Entity base class (TenantEntity):
  → Every domain entity that is tenant-scoped inherits from TenantEntity
  → TenantEntity has: Guid TenantId { get; private set; }
  → TenantId is SET by the AuditSaveChangesInterceptor on insert
    (developer never sets TenantId manually — it's infrastructure-managed)

AuditSaveChangesInterceptor:
  → Before SaveChangesAsync:
    → For all Added entities of type TenantEntity:
        → entity.TenantId = _tenantService.CurrentTenantId (assert not empty)
    → For all Modified entities:
        → Assert entity.TenantId == _tenantService.CurrentTenantId
        → THROW if mismatch — this indicates a critical bug
```

### 9.4 Tenant Context in Background Jobs

Background jobs run outside of an HTTP request, so they have no JWT to extract tenant from.

```
Design: Explicit Tenant Parameterization in Job Arguments

VaccinationReminderJob:
  → Does NOT use ITenantService (no request context)
  → Queries ALL active tenants from a system-level context
    (special ITenantAdminRepository that bypasses tenant filter)
  → For each tenant:
      → Creates a new IServiceScope
      → Sets TenantId explicitly: tenantService.SetTenant(tenant.Id)
      → Executes the per-tenant logic within that scope
      → Disposes the scope
  → Tenant isolation is maintained per iteration

For enqueueable jobs (triggered per tenant):
  → Job arguments include TenantId
  → Job handler receives TenantId → sets on ITenantService before executing
```

### 9.5 Row-Level Security (SQL Server)

As an additional defense layer on the database, SQL Server Row-Level Security is configured:

```
SQL Server RLS Policy:
  → Security predicate function: fn_tenantSecurityPredicate(TenantId)
  → Checks SESSION_CONTEXT(N'TenantId') == row.TenantId
  → Applied as FILTER PREDICATE on all tenant-scoped tables

API sets session context on every DB connection:
  → Before query: EXEC sp_set_session_context N'TenantId', @tenantId
  → This means even if the EF Core global filter is somehow bypassed,
    the SQL Server policy blocks cross-tenant data access at DB level

This is defense-in-depth: the primary defense is EF Core filters;
RLS is the backstop that makes the architecture provably secure.
```

---

## 10. Caching Strategy

### 10.1 Cache Hierarchy

```
Request hits API
    │
    ▼
[L1: In-Memory Cache] (IMemoryCache — per pod, 5MB limit)
    │ HIT → return cached response
    │ MISS ↓
    ▼
[L2: Redis Distributed Cache] (IDistributedCache)
    │ HIT → populate L1, return response
    │ MISS ↓
    ▼
[L3: Database] (SQL Server via EF Core)
    │ → populate L2 (with TTL), populate L1, return response
    ▼
Response returned to client
```

**Why two levels?** L1 (in-memory) eliminates network round-trips for the hottest data (tenant metadata, user permissions) — these are read on every request. L2 (Redis) provides cross-pod consistency — when pod 1 updates an animal record, pod 2's L1 cache is stale but L2 is correct.

### 10.2 Cache Key Strategy

All cache keys follow a deterministic structure:

```
Format: {tenant_id}:{domain}:{entity}:{identifier}:{version}

Examples:
  tenant-abc:animals:list:shed-xyz          → Animal list for a shed
  tenant-abc:animals:detail:animal-id       → Individual animal record
  tenant-abc:dashboard:executive:summary    → Dashboard aggregation
  tenant-abc:inventory:stock:current        → Current stock levels
  tenant-abc:health:vaccinations:due        → Upcoming vaccinations
  system:tenants:active:tenant-id           → Tenant metadata (cross-tenant system cache)

TenantId prefix ensures cache isolation between tenants.
No cache key can ever return data for a different tenant.
```

### 10.3 Cache TTL Policy

| Cache Category | L1 TTL | L2 TTL | Invalidation Trigger |
|---|---|---|---|
| Tenant metadata | 5 min | 30 min | Tenant update |
| User permissions | 5 min | 15 min | Role/farm assignment change |
| Dashboard summary | 30 sec | 5 min | Any relevant data change |
| Animal list (filtered) | 60 sec | 10 min | Animal create/update/delete |
| Animal detail | 5 min | 30 min | Animal update |
| Feed formula list | 10 min | 1 hour | Formula create/update |
| Vaccination due list | 2 min | 10 min | Vaccination recorded |
| Inventory stock levels | 30 sec | 5 min | Any stock transaction |
| Financial monthly P&L | 5 min | 30 min | Any financial entry in period |
| Reference data (breeds, ingredients) | 1 hour | 24 hours | Admin-triggered refresh |

### 10.4 Cache Invalidation Pattern

```
Pattern: Cache-Aside with Event-Driven Invalidation

When AnimalSoldCommand completes successfully:
  1. Domain event: AnimalSoldEvent published
  2. CacheInvalidationEventHandler receives event
  3. Handler invalidates:
     → tenant:animals:list:* (all animal list caches for this tenant)
     → tenant:animals:detail:{animalId}
     → tenant:dashboard:executive:summary
     → tenant:finance:batch-pl:{batchId}
  4. L1 cache invalidation: broadcast to all pods via Redis pub/sub
     → Each pod listens to Redis channel "cache-invalidation:{tenantId}"
     → On message received: remove matching L1 keys

No stale-while-revalidate for financial data — consistency over performance.
Stale-while-revalidate acceptable for dashboard widgets (marked as such).
```

### 10.5 Redis Usage Beyond Caching

| Usage | Purpose | Key Pattern |
|---|---|---|
| OTP storage | Temporary OTP with auto-expiry | `otp:{phone}` |
| Session data | Refresh token tracking | `session:{userId}:{tokenId}` |
| Rate limiting | Per-user and per-tenant request throttling | `ratelimit:{ip}:{endpoint}` |
| SignalR backplane | Cross-pod SignalR message delivery | Managed by SignalR Redis backplane |
| Distributed lock | Prevent concurrent Hangfire job execution per tenant | `lock:job:{jobName}:{tenantId}` |
| Pub/Sub | L1 cache invalidation broadcast | `cache-invalidation:{tenantId}` |

---

## 11. Logging Strategy

### 11.1 Logging Philosophy

Every log entry must answer: **Who did what to which resource, in which tenant, at what time, and what happened?** Logs without tenant context are useless for debugging multi-tenant issues.

### 11.2 Structured Logging with Serilog

```
Serilog Configuration:
  → Output to:
    [1] Console (for local development and container stdout)
    [2] File (rolling daily, 7-day retention for local debugging)
    [3] AWS CloudWatch Logs (production — searchable, centralized)
    [4] Seq (development/staging — rich structured log UI)

Minimum Log Levels:
  → Default: Information
  → Microsoft.EntityFrameworkCore.Database.Command: Warning
    (suppress EF SQL query logs in production; enable in Debug only)
  → Microsoft.AspNetCore.Hosting: Information
  → System.*: Warning

Enrichers (automatically added to every log entry):
  → X-Correlation-Id (from HTTP header / generated if absent)
  → TenantId (from ITenantService)
  → UserId (from ICurrentUserService)
  → UserRole (from JWT claims)
  → MachineName (for container identification)
  → Environment (Development/Staging/Production)
  → Application ("Farm360.Api")
  → Version ("1.0.0")
```

### 11.3 Log Entry Structure

Every log entry is a structured JSON object:

```json
{
  "Timestamp": "2026-07-07T02:00:00.000Z",
  "Level": "Information",
  "MessageTemplate": "Animal {AnimalId} sold for {SalePrice} BDT by {UserId}",
  "Message": "Animal a1b2c3 sold for 150000 BDT by user-xyz",
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

### 11.4 Log Levels Usage Standard

| Level | When to Use | Example |
|---|---|---|
| `Verbose` | Extremely detailed diagnostics — never in production | EF Core query parameters |
| `Debug` | Diagnostic information for development | "Cache hit for key: {key}" |
| `Information` | Normal operational events | "Animal registered: {AnimalId}", "User logged in: {UserId}" |
| `Warning` | Unexpected but recoverable conditions | "OTP attempt {AttemptCount}/3 for {Phone}", "Performance threshold exceeded: {ElapsedMs}ms" |
| `Error` | Failures that affect a single operation | "Failed to send SMS to {Phone}: {Error}" |
| `Fatal` | Application-level failures requiring immediate intervention | "Database connection pool exhausted", "Cannot resolve TenantId from JWT" |

### 11.5 Sensitive Data Masking in Logs

```
Serilog Destructuring Policy:
  → Phone numbers: "+880171XXXX05" (mask middle 6 digits)
  → Email: "r****@gmail.com"
  → JWT tokens: never logged (only jti claim logged)
  → NID numbers: "XXXXXXXXXXXXXXXX" (fully masked)
  → Passwords: never logged
  → OTP values: never logged
  → Bank account numbers: masked

Implementation: Custom Serilog IDestructuringPolicy applied globally.
Any class with [SensitiveData] attribute on properties → masked in all log output.
```

### 11.6 Correlation ID Strategy

```
Every request is assigned a Correlation ID:
  → If X-Correlation-Id header is present → use it (for distributed tracing)
  → If absent → generate UUID v4

Correlation ID is:
  → Added to all log entries (via enricher)
  → Added to all outgoing HTTP calls (ISmsService, IEmailService) as header
  → Added to all Hangfire job arguments
  → Returned in response header: X-Correlation-Id

This allows end-to-end tracing of a user action through
the full system — from browser click to DB query to SMS sent.
```

---

## 12. Exception Handling Strategy

### 12.1 Exception Hierarchy

```
System.Exception
    │
    ├── Application.Common.Exceptions.AppException (base for all app exceptions)
    │       ├── NotFoundException
    │       │     → 404 Not Found
    │       │     → "Animal with ID {id} was not found"
    │       │
    │       ├── ValidationException
    │       │     → 422 Unprocessable Entity
    │       │     → Contains collection of field-level errors
    │       │
    │       ├── ForbiddenAccessException
    │       │     → 403 Forbidden
    │       │     → "You do not have permission to perform this action"
    │       │
    │       ├── ConflictException
    │       │     → 409 Conflict
    │       │     → "Animal tag {tag} already exists in this organization"
    │       │
    │       └── TenantSuspendedException
    │             → 402 Payment Required
    │             → "Your subscription has expired"
    │
    └── Domain.Exceptions.DomainException (domain rule violations)
            ├── AnimalQuarantinedException → 422
            ├── InvalidAnimalStateTransitionException → 422
            ├── InsufficientStockException → 422
            └── ClosedPeriodModificationException → 422
```

### 12.2 Global Exception Middleware

```
GlobalExceptionMiddleware sits at the top of the middleware pipeline:

Request → ... → Exception thrown anywhere below
    │
    ▼
GlobalExceptionMiddleware catches:
    ├── ValidationException
    │     → 422 with { errors: { fieldName: ["message"] } }
    │
    ├── NotFoundException
    │     → 404 with { title, detail, instance (request path) }
    │
    ├── ForbiddenAccessException
    │     → 403
    │
    ├── DomainException (and all subclasses)
    │     → 422 with domain-specific message
    │
    ├── ConflictException
    │     → 409
    │
    └── Any unhandled Exception
          → Log as Error with full stack trace + CorrelationId
          → Return 500 with: { title: "An unexpected error occurred",
                               correlationId: "..." }
          → NEVER expose stack trace or internal details to client

Response format follows RFC 7807 (Problem Details for HTTP APIs):
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

### 12.3 Exception Handling in MediatR Pipeline

```
ValidationBehavior (runs before handler):
  → If FluentValidation returns failures:
    → Throw ValidationException(failures)
  → Handler never sees invalid input

LoggingBehavior (wraps handler):
  → On exception: Log.Error(ex, "Request {RequestName} failed", requestName)
  → Re-throw (GlobalExceptionMiddleware handles final response)

TransactionBehavior (wraps Command handlers):
  → On exception: SqlTransaction.Rollback()
  → Ensures partial writes are never committed
```

---

## 13. Validation Strategy

### 13.1 Validation Layers

```
[Layer 1: Client-Side Validation — Angular]
  → Reactive forms with Angular validators
  → Immediate feedback to user
  → Not trusted by server (defense-in-depth)

[Layer 2: API Model Binding — ASP.NET Core]
  → Automatic 400 Bad Request if JSON is malformed
  → Basic type conversion failures caught here

[Layer 3: FluentValidation — MediatR Pipeline]
  → AUTHORITATIVE validation layer
  → ValidationBehavior runs FluentValidation before every handler
  → Business input rules enforced here

[Layer 4: Domain Validation — Entity Methods]
  → Business invariants enforced at domain model level
  → Cannot construct an invalid domain object
  → Throws DomainException for rule violations

[Layer 5: Database Constraints]
  → UNIQUE constraints, NOT NULL, CHECK constraints
  → Last line of defense — should never be reached if layers 1-4 are correct
  → DbUpdateException caught in error handler, logged as a defect
```

### 13.2 FluentValidation in MediatR Pipeline

```
ValidationBehavior<TRequest, TResponse>:
  → Receives all IValidator<TRequest> from DI (auto-registered)
  → Runs all validators concurrently
  → Collects all failures
  → If any failures: throw ValidationException(failures)
  → If no failures: proceed to next behavior

Validator co-location principle:
  → RegisterAnimalCommandValidator.cs lives NEXT TO RegisterAnimalCommand.cs
  → Finding the validator is instant — same folder as the command

Validator responsibilities:
  → Required field checks
  → Format checks (phone, email, tag format)
  → Range checks (weight > 0, date in past)
  → Async checks (uniqueness check via repository interface)
    e.g., "must async verify tag is unique within tenant"

Cross-field validation:
  → PregnancyConfirmationDate >= MatingDate
  → SaleDate >= AcquisitionDate
  → WeightDate >= DateOfBirth
```

### 13.3 Value Object Validation

```
Value objects self-validate in their factory method:

Money.Create(decimal amount, string currency):
  → if amount < 0: throw DomainException("Money cannot be negative")
  → if string.IsNullOrEmpty(currency): throw DomainException(...)
  → return new Money(amount, currency)

This means:
  → You cannot have a Money object with a negative value — it's impossible
  → The type system enforces the invariant
  → No need to validate Money separately in command validators
```

---

## 14. Deployment Architecture

### 14.1 Containerization

```
Docker Images:
  ┌──────────────────────────────────────────────────────┐
  │  Farm360.Api Docker Image                            │
  │  Base: mcr.microsoft.com/dotnet/aspnet:10.0-alpine  │
  │  Build: mcr.microsoft.com/dotnet/sdk:10.0           │
  │  Multi-stage build: build → publish → final         │
  │  Image size target: < 200MB                         │
  │  User: non-root (app-user, UID 1000)                │
  │  Health check: GET /health (returns 200)            │
  └──────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────┐
  │  Farm360.Web Docker Image (Nginx)                    │
  │  Base: nginx:1.27-alpine                            │
  │  Content: Angular build output (dist/farm360)       │
  │  Nginx: serves SPA, proxies /api/* to API pod       │
  │  Image size target: < 50MB                          │
  └──────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────┐
  │  Farm360.Migrator Docker Image (one-off job)         │
  │  Runs EF Core database migrations                   │
  │  Executed as a Kubernetes Job before API pod startup │
  └──────────────────────────────────────────────────────┘
```

### 14.2 Kubernetes Architecture

```
Kubernetes Cluster (AWS EKS, ap-south-1):

Namespace: farm360-production
────────────────────────────────────────────

Deployments:
  ┌─────────────────────────┐
  │ farm360-api             │   replicas: 3 (min) → 10 (max via HPA)
  │  - ASP.NET Core 10 API  │   CPU limit: 500m  Memory: 512Mi
  │  - Hangfire Server      │   Requests: CPU 250m  Memory: 256Mi
  │  - SignalR Hub          │   Readiness: /health/ready
  └─────────────────────────┘   Liveness: /health/live

  ┌─────────────────────────┐
  │ farm360-web             │   replicas: 2 (min) → 5 (max via HPA)
  │  - Nginx + Angular SPA  │   CPU limit: 200m  Memory: 128Mi
  └─────────────────────────┘

Jobs (one-off):
  ┌─────────────────────────┐
  │ farm360-migrator        │   Runs before API pods start (initContainer pattern)
  └─────────────────────────┘

Services:
  → farm360-api-svc      (ClusterIP: internal communication)
  → farm360-web-svc      (ClusterIP: routed via Ingress)
  → farm360-api-lb       (LoadBalancer: for health checks)

Ingress (AWS ALB Ingress Controller):
  → HTTPS: farm360.ai  → farm360-web-svc (Angular SPA)
  → HTTPS: api.farm360.ai → farm360-api-svc (REST API)
  → HTTPS: *.farm360.ai/hubs/* → farm360-api-svc (SignalR)

Horizontal Pod Autoscaler (HPA):
  → farm360-api: scale on CPU > 70% or memory > 80%
  → farm360-web: scale on CPU > 70%

ConfigMaps:
  → farm360-config (non-secret configuration)

Secrets (via AWS Secrets Manager + External Secrets Operator):
  → farm360-secrets (DB connection strings, JWT keys, API keys)
  → Never stored in Kubernetes Secrets directly
  → External Secrets Operator syncs from AWS Secrets Manager

PodDisruptionBudget:
  → farm360-api: minAvailable: 2 (ensures 2 pods always running during updates)

Affinity Rules:
  → Anti-affinity: API pods spread across different nodes and AZs
```

### 14.3 Database Deployment

```
SQL Server on AWS RDS (Multi-AZ):
  → Instance: db.r6g.large (Production)
  → Multi-AZ: Yes (automatic failover to standby in different AZ)
  → Storage: io1 SSD, 500GB, auto-scale to 2TB
  → Read Replica: 1 replica in ap-south-1b (for dashboard/report queries)
  → Maintenance window: Sunday 03:00–05:00 BDT
  → Automated backups: enabled (35-day retention)
  → Encryption: AWS KMS (at rest + in transit)
  → Parameter group: Custom (optimized for OLTP + reporting mix)
  → Connection pooling: Via application-level PgBouncer equivalent
    (EF Core connection resiliency + Polly retry policies)
```

---

## 15. CI/CD Strategy

### 15.1 Pipeline Overview (GitHub Actions)

```
Developer pushes to feature branch
    │
    ▼
[Trigger: Push to any branch / PR to develop]
────────────────────────────────────────────

Stage 1: CODE QUALITY
  ├── dotnet restore
  ├── dotnet build --no-restore
  ├── dotnet format --verify-no-changes (formatting check)
  ├── dotnet analyzer (Roslyn analyzers — treat warnings as errors)
  ├── Architecture tests: NetArchTest.Rules (dependency direction enforcement)
  └── npm ci (Angular dependencies)
      └── ng lint (ESLint)

Stage 2: TESTING
  ├── dotnet test Farm360.Domain.UnitTests --collect:"XPlat Code Coverage"
  ├── dotnet test Farm360.Application.UnitTests --collect:"XPlat Code Coverage"
  ├── dotnet test Farm360.Application.IntegrationTests
  │   (uses TestContainers: spins up SQL Server and Redis in Docker)
  ├── ng test --no-watch --code-coverage (Angular unit tests)
  ├── Coverage gate: overall coverage must be ≥ 80%
  └── Publish test results to GitHub Actions

Stage 3: SECURITY
  ├── dotnet list package --vulnerable (NuGet vulnerability check)
  ├── npm audit (Node.js vulnerability check)
  ├── Trivy scan (Docker image vulnerability scan)
  └── OWASP ZAP baseline scan (on integration test environment)

Stage 4: BUILD ARTIFACTS
  ├── dotnet publish -c Release (API image)
  ├── ng build --configuration=production (Angular SPA)
  ├── docker build -t farm360-api:{sha} .
  ├── docker build -t farm360-web:{sha} .
  └── Push images to AWS ECR (Elastic Container Registry)

Stage 5: DEPLOY (on merge to develop → staging environment)
  ├── helm upgrade farm360 ./charts/farm360
  │     --namespace farm360-staging
  │     --set api.image.tag={sha}
  │     --set web.image.tag={sha}
  ├── Kubernetes migration job runs
  ├── Wait for rollout: kubectl rollout status deployment/farm360-api
  └── Smoke tests: automated Postman collection (critical path)

Stage 6: DEPLOY TO PRODUCTION (on merge to main — manual approval required)
  ├── Manual approval gate (requires Product Manager + CTO sign-off in GitHub)
  ├── helm upgrade farm360 ./charts/farm360
  │     --namespace farm360-production
  │     --set api.image.tag={sha} --set web.image.tag={sha}
  │     --set replicas.api=3
  ├── Blue-green deployment strategy (Kubernetes canary via Argo Rollouts)
  │   → 10% traffic to new version → monitor for 5 min → 50% → 100%
  │   → Auto-rollback if error rate > 1% during canary
  └── Post-deploy: smoke tests + Datadog deployment marker
```

### 15.2 Branch Protection Rules

```
Branch: main
  → Require PR (no direct push — including admins)
  → Require: 2 reviewer approvals
  → Require: all status checks pass (CI stages 1–4)
  → Require: manual deployment approval
  → No force push; no deletion

Branch: develop
  → Require PR
  → Require: 1 reviewer approval
  → Require: all status checks pass (CI stages 1–3)
```

### 15.3 Environment Strategy

| Environment | Branch | Purpose | Data |
|---|---|---|---|
| Local | feature/* | Developer testing | Local SQL Server + Redis in Docker |
| CI | all branches | Automated testing | TestContainers (ephemeral) |
| Development | develop | Integration testing | Seeded test data |
| Staging | develop | Pre-release validation | Anonymized production snapshot |
| Production | main | Live system | Real tenant data |

---

## 16. Cloud Architecture

### 16.1 AWS Services Architecture

```
REGION: ap-south-1 (Mumbai) — PRIMARY

┌───────────────────────────────────────────────────────────────────────────┐
│  AWS Account: Farm360 Production                                          │
│                                                                           │
│  VPC: 10.0.0.0/16                                                         │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │  Public Subnets (10.0.1.0/24, 10.0.2.0/24)                         │ │
│  │    → Application Load Balancer (HTTPS 443)                          │ │
│  │    → NAT Gateway (outbound internet for private subnets)            │ │
│  └──────────────────────────────────────────────────────────────────────┘ │
│                                │                                          │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │  Private Subnets — Application Tier (10.0.10.0/24, 10.0.11.0/24)  │ │
│  │    → EKS Node Group (EC2 m6g.xlarge, 2–8 nodes, auto-scaling)      │ │
│  │    → EKS Pods: farm360-api, farm360-web                             │ │
│  └──────────────────────────────────────────────────────────────────────┘ │
│                                │                                          │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │  Private Subnets — Data Tier (10.0.20.0/24, 10.0.21.0/24)         │ │
│  │    → RDS SQL Server (Multi-AZ: Primary in AZ-a, Standby in AZ-b)  │ │
│  │    → ElastiCache Redis (Cluster: 2 nodes, Multi-AZ)                │ │
│  └──────────────────────────────────────────────────────────────────────┘ │
│                                                                           │
│  Supporting Services:                                                     │
│    → Route 53 (DNS + health check routing)                                │
│    → CloudFront CDN (static assets, Angular SPA delivery)                 │
│    → S3 (blob storage: animal photos, generated PDFs, report exports)     │
│    → ACM (SSL/TLS certificate management — auto-renewal)                  │
│    → Secrets Manager (all application secrets + DB credentials)           │
│    → KMS (encryption key management for RDS, S3, Secrets Manager)        │
│    → CloudWatch Logs + Metrics (application logs + infrastructure metrics)│
│    → AWS SES (transactional email)                                        │
│    → SNS (internal notifications + webhook triggers)                      │
│    → ECR (Docker image registry)                                          │
│    → IAM (role-based access to all services — least privilege)            │
│    → WAF (Web Application Firewall on ALB — OWASP CRS)                   │
│    → Shield Standard (DDoS protection — included with ALB/CloudFront)    │
│    → Cost Explorer (budget alerts at 80% and 100% of monthly budget)     │
│                                                                           │
└───────────────────────────────────────────────────────────────────────────┘

REGION: ap-southeast-1 (Singapore) — DISASTER RECOVERY
  → RDS Read Replica (promoted to primary on DR activation)
  → S3 Cross-Region Replication (all blobs replicated)
  → EKS cluster (scaled down; scaled up on DR activation)
  → Route 53 failover routing policy
```

### 16.2 CloudFront Configuration

```
Farm360 Angular SPA delivery via CloudFront:
  → Origin: S3 bucket (farm360-web-assets)
  → Behaviors:
    → /api/* → Forward to ALB (API proxy — NOT cached)
    → /hubs/* → Forward to ALB (SignalR — no cache, WebSocket)
    → /* → S3 origin (Angular SPA)
  → Cache policy:
    → index.html: Cache-Control: no-cache, no-store (always fresh)
    → *.js, *.css, *.woff2: Cache-Control: max-age=31536000, immutable
      (content-hashed filenames ensure correct versioning)
  → Compression: Brotli + Gzip enabled
  → HTTP/2 and HTTP/3 enabled
  → WAF attached (OWASP managed rule groups)
  → Geo-restriction: none (future consideration for data sovereignty)
```

---

## 17. Scalability Strategy

### 17.1 Application Tier Scaling

```
Horizontal Pod Autoscaling (HPA):
  farm360-api:
    minReplicas: 3
    maxReplicas: 10
    metrics:
      - CPU: targetUtilization 70%
      - Memory: targetUtilization 80%
      - Custom: HTTP request queue depth > 50 (KEDA)

Scale-up behavior:
  → Scale up immediately (0 stabilization window) — react fast
  → Add 2 pods at a time (not 1) — prevent thrashing
  → New pods ready in ~30s (fast startup via ahead-of-time compilation)

Scale-down behavior:
  → 5-minute stabilization window before scale-down
  → Scale down by 1 pod at a time
  → Respect PodDisruptionBudget (always keep ≥ 2 pods)

Vertical scaling (nodes):
  → AWS EC2 Auto Scaling Group for EKS nodes
  → Scale from 2 to 8 m6g.xlarge nodes
  → Cluster Autoscaler adds nodes when pods are pending
  → Spot instances (40%) mixed with On-Demand (60%) for cost optimization
```

### 17.2 Database Tier Scaling

```
Read Scaling:
  → Read replica in same AZ for low-latency report queries
  → EF Core connection string routing:
    → Commands → primary write connection
    → Queries that implement IReadOnlyQuery → read replica connection
  → Hangfire uses dedicated connection (not from application pool)

Write Scaling:
  → Connection pooling via EF Core connection resiliency
  → PgBouncer-equivalent: SQL Server connection pool managed by RDS Proxy
  → Max pool size: 100 connections (sized for 10 API pods × 10 connections each)

Future (Phase 3 — 4,000+ tenants):
  → Introduce CQRS read store (read model in Redis or separate optimized DB)
  → Tenant sharding: distribute tenant schemas across multiple SQL Server instances
  → Enterprise tenants: dedicated RDS instance per tenant
```

### 17.3 Caching Scaling

```
Redis Cluster:
  → 2-node cluster (primary + replica) in Multi-AZ
  → ElastiCache r7g.medium (6.38GB memory)
  → Scale-out: Add read replicas for read-heavy cache loads
  → Memory eviction: allkeys-lru (evict least recently used when memory full)

Cache warming:
  → On application startup: warm critical caches (tenant metadata, reference data)
  → Hangfire job: nightly cache warming for dashboard aggregations
```

### 17.4 Stateless Design

```
All API pods are stateless:
  → No server-side session state
  → JWT is the auth mechanism (self-contained)
  → SignalR state lives in Redis backplane (not in pod memory)
  → Hangfire state lives in SQL Server (Hangfire's dedicated schema)
  → File uploads streamed directly to S3 (never stored on pod disk)
  → Any pod can handle any request from any tenant

This enables:
  → Zero-downtime deployments (kill any pod, others handle load)
  → Horizontal scaling without coordination
  → Spot instance usage (AWS can terminate pods; they restart elsewhere)
```

---

## 18. Coding Standards

### 18.1 C# / ASP.NET Core Standards

```
Language Version: C# 13 (latest with .NET 10)
Nullable: enabled (project-wide)
Warnings as errors: enabled for Architecture and Domain projects

General Principles:
  → Prefer immutability: readonly fields, init-only properties, record types
  → Prefer explicit over implicit: avoid var for non-obvious types
  → Prefer Result<T> over exceptions for expected failures in domain
  → Prefer async/await throughout; no .Result or .Wait()
  → No static mutable state
  → No sealed classes as a default; seal only when inheritance is prohibited

Domain Layer Standards:
  → Entities: private setters; state changed only via domain methods
  → Value Objects: sealed records with structural equality
  → Domain methods: return void or raise domain events; no return of DTOs
  → Constructors: private; use static factory methods (Animal.Create(...))
  → Collections on entities: exposed as IReadOnlyCollection<T>
  → Never expose ICollection<T> or List<T> — prevents bypassing domain logic

Application Layer Standards:
  → One file per Command, Query, Handler, Validator (co-located, one folder per feature)
  → Command handlers: return the ID of the created/modified aggregate, not the full entity
  → Query handlers: return DTOs or PaginatedResult<T>; never return domain entities
  → No business logic in handlers; delegate to domain services and entities

Infrastructure Standards:
  → EF Core: Fluent API configuration only; no data annotations on domain entities
  → Repositories: generic base + specific implementations; no Linq in handlers
  → All external HTTP calls: wrapped in Polly policies (retry, circuit breaker, timeout)
  → No raw SQL unless for specific performance-critical read model queries (documented)

API Layer Standards:
  → Minimal APIs organized by feature endpoint group
  → Endpoints: no more than 10 lines; dispatch → return
  → API versioning: URL path versioning (/api/v1/)
  → All endpoints return IResult (TypedResults.Ok<T>, TypedResults.Created, etc.)
  → OpenAPI: every endpoint documented with ProducesResponseType attributes
```

### 18.2 Angular / TypeScript Standards

```
Angular Version: Angular 22 (standalone components throughout)
TypeScript strict mode: enabled
ESLint: extended from @angular-eslint/recommended

General Principles:
  → Standalone components everywhere; NgModules only where library requires it
  → OnPush change detection on all components
  → Signal-based reactivity preferred over RxJS for component state
  → RxJS for async operations, HTTP, event streams
  → No any type; strict type everywhere
  → Reactive Forms (no template-driven forms)

Component Standards:
  → Each component: one folder with component.ts, template.html, styles.scss, spec.ts
  → Smart (container) vs. Dumb (presentational) component separation
  → Dumb components: @Input() / @Output() only; no service injection
  → Smart components: inject services; bind to Signal Store
  → Component names: PascalCase with -Component suffix (AnimalListComponent)

Service Standards:
  → providedIn: 'root' for singleton services
  → Feature-scoped services: providedIn specific route/component
  → Services return Observable<T> for HTTP calls
  → Services return Signal<T> for state reads

State Management (NgRx Signal Store):
  → One store per feature (not one global store)
  → Store: withState, withComputed, withMethods pattern
  → No store method calls side effects outside of methods
  → Computed signals for derived state (no subscriptions for computation)
```

---

## 19. Naming Standards

### 19.1 C# Naming Conventions

| Artifact | Convention | Example |
|---|---|---|
| Classes | PascalCase | `AnimalRepository`, `RegisterAnimalCommand` |
| Interfaces | IPascalCase | `IAnimalRepository`, `ITenantService` |
| Methods | PascalCase | `RegisterAnimalAsync`, `CalculateAdg` |
| Properties | PascalCase | `DateOfBirth`, `SalePrice` |
| Private fields | _camelCase | `_tenantService`, `_dbContext` |
| Constants | UPPER_CASE | `MAX_OTP_ATTEMPTS`, `DEFAULT_CACHE_TTL_SECONDS` |
| Parameters | camelCase | `animalId`, `tenantId` |
| Local variables | camelCase | `animal`, `vaccinationRecord` |
| Async methods | Suffix Async | `GetAnimalByIdAsync`, `SaveChangesAsync` |
| Command | PascalCase + Command | `RegisterAnimalCommand` |
| Query | PascalCase + Query | `GetAnimalByIdQuery` |
| Handler | Command/QueryName + Handler | `RegisterAnimalCommandHandler` |
| Validator | Command/QueryName + Validator | `RegisterAnimalCommandValidator` |
| Domain Event | PascalCase + Event | `AnimalSoldEvent` |
| Domain Exception | PascalCase + Exception | `AnimalQuarantinedException` |
| DTO | PascalCase + Dto | `AnimalDetailDto`, `RegisterAnimalRequest` |
| Configuration | Entity + Configuration | `AnimalConfiguration` |
| Test class | ClassUnderTest + Tests | `RegisterAnimalCommandHandlerTests` |

### 19.2 Database Naming Conventions

| Artifact | Convention | Example |
|---|---|---|
| Table | PascalCase, plural | `Animals`, `VaccinationRecords` |
| Column | PascalCase | `AnimalId`, `DateOfBirth` |
| Primary Key | Id | `Id` (GUID) |
| Foreign Key | EntityId | `ShedException`, `TenantId` |
| Index | IX_Table_Column | `IX_Animals_TenantId_Status` |
| Unique constraint | UQ_Table_Column | `UQ_Animals_TenantId_TagId` |
| Schema | lowercase | `dbo` (default), `hangfire`, `audit` |

### 19.3 API Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Route | lowercase, kebab-case | `/api/v1/animals`, `/api/v1/feed-formulas` |
| Query parameter | camelCase | `?farmId=x&pageSize=20` |
| JSON property | camelCase | `{ "animalId": "...", "dateOfBirth": "..." }` |
| HTTP verbs | Standard | GET (read), POST (create), PUT (replace), PATCH (update), DELETE |
| Resource ID in route | /{id} | `/api/v1/animals/{animalId}` |

### 19.4 Angular / TypeScript Naming Conventions

| Artifact | Convention | Example |
|---|---|---|
| Component | PascalCase + Component | `AnimalListComponent` |
| Service | PascalCase + Service | `AnimalService`, `TenantContextService` |
| Guard | PascalCase + Guard | `AuthGuard`, `PermissionGuard` |
| Interceptor | PascalCase + Interceptor | `JwtInterceptor` |
| Pipe | PascalCase + Pipe | `BdtCurrencyPipe` |
| Store | PascalCase + Store | `LivestockStore`, `DashboardStore` |
| Interface (TS) | IPascalCase | `IAnimalDto`, `IFarmSummary` |
| Enum | PascalCase | `AnimalStatus`, `UserRole` |
| File (component) | kebab-case.component.ts | `animal-list.component.ts` |
| File (service) | kebab-case.service.ts | `animal.service.ts` |
| File (store) | kebab-case.store.ts | `livestock.store.ts` |
| Selector | app-kebab-case | `app-animal-list`, `app-dashboard` |

### 19.5 Environment & Infrastructure Naming

| Resource | Convention | Example |
|---|---|---|
| AWS Resources | farm360-{environment}-{resource} | `farm360-prod-api-alb` |
| Docker Images | farm360-{service}:{sha} | `farm360-api:a1b2c3d` |
| Kubernetes Namespace | farm360-{environment} | `farm360-production` |
| Kubernetes Deployment | farm360-{service} | `farm360-api`, `farm360-web` |
| Helm Release | farm360 | `farm360` |
| Git Tags (release) | v{major}.{minor}.{patch} | `v1.2.3` |

---

## 20. Git Branching Strategy

### 20.1 Branch Model: Trunk-Based Development with Short-Lived Feature Branches

```
          ┌─────────────────────────────────────────────────────────────┐
          │  MAIN BRANCH (trunk)                                        │
          │  → Always deployable to production                          │
          │  → Protected: requires PR + 2 approvals + all checks pass   │
          │  → Tagged on each release: v1.0.0, v1.1.0, etc.            │
          └──────────────┬──────────────────────────────────────────────┘
                         │
          ┌──────────────┼──────────────────────────────────────────────┐
          │  DEVELOP BRANCH                                             │
          │  → Integration branch for all feature work                 │
          │  → Deployed to Staging automatically on merge              │
          │  → Protected: requires PR + 1 approval + all checks pass   │
          └──────────────┬──────────────────────────────────────────────┘
                         │
          ┌──────────────┼──────────────────────────────────────────────┐
          │  FEATURE BRANCHES (from develop)                            │
          │  Naming: feature/{ticket-id}-{short-description}           │
          │  Example: feature/F360-142-register-animal-endpoint        │
          │  Lifetime: ≤ 2 days (trunk-based: merge frequently)        │
          │  PR to: develop                                             │
          └─────────────────────────────────────────────────────────────┘

          ┌─────────────────────────────────────────────────────────────┐
          │  BUGFIX BRANCHES (from develop)                             │
          │  Naming: fix/{ticket-id}-{short-description}               │
          │  Example: fix/F360-201-animal-tag-duplicate-validation      │
          │  PR to: develop                                             │
          └─────────────────────────────────────────────────────────────┘

          ┌─────────────────────────────────────────────────────────────┐
          │  HOTFIX BRANCHES (from main — production emergency only)    │
          │  Naming: hotfix/{ticket-id}-{short-description}            │
          │  Example: hotfix/F360-250-tenant-isolation-breach          │
          │  PR to: main AND back-merged to develop                    │
          │  Lifecycle: ≤ 4 hours from branch to production merge      │
          └─────────────────────────────────────────────────────────────┘

          ┌─────────────────────────────────────────────────────────────┐
          │  RELEASE BRANCHES (optional — only for coordinated releases)│
          │  Naming: release/{version}                                  │
          │  Example: release/v1.2.0                                   │
          │  Used for: final testing, cherry-picking, docs finalization │
          └─────────────────────────────────────────────────────────────┘
```

### 20.2 Commit Message Standard (Conventional Commits)

```
Format: <type>(<scope>): <subject>

Types:
  feat     → New feature
  fix      → Bug fix
  docs     → Documentation only
  style    → Formatting, no logic change
  refactor → Refactoring (no feature, no bug)
  perf     → Performance improvement
  test     → Adding/updating tests
  chore    → Build, CI, or tooling changes
  revert   → Reverting a commit

Scope (optional): module name
  animals, feeding, health, inventory, finance, dashboard,
  platform, auth, tenant, infra, ci, docs

Subject: imperative mood, lowercase, no period at end
  → "add weight tracking to animal entity"
  → "fix duplicate tag validation in RegisterAnimalCommandValidator"

Examples:
  feat(animals): add weight tracking with ADG calculation
  fix(health): prevent duplicate vaccination log for same day
  perf(dashboard): cache executive summary with 5-minute TTL
  chore(ci): add architecture test stage to GitHub Actions pipeline
  test(animals): add integration tests for SellAnimal command

Body (optional): explain WHY, not WHAT
Footer: Breaking changes: BREAKING CHANGE: <description>
        Issue references: Closes #142, Refs #138
```

### 20.3 Pull Request Standards

```
PR Template (enforced via .github/pull_request_template.md):
  → Summary: What does this PR do?
  → Motivation: Why is this change needed?
  → Changes: List of specific changes
  → Testing: How was this tested?
  → Screenshots: (for UI changes)
  → Checklist:
    [ ] Tests added/updated
    [ ] Both Bangla and English translations updated
    [ ] Audit logging implemented for state-changing operations
    [ ] No sensitive data in logs
    [ ] No hardcoded values (use configuration)
    [ ] API documentation updated
    [ ] Performance impact considered

PR Size Guidelines:
  → Ideal: < 400 lines of production code changed
  → Warning: 400–800 lines (reviewer fatigue starts here)
  → Block: > 1,000 lines (split the PR; this is a process failure)
```

---

## 21. Documentation Standards

### 21.1 Documentation Types

| Type | Location | Audience | Format | Update Trigger |
|---|---|---|---|---|
| Architecture Decision Records | `/docs/adr/` | Engineering | Markdown | Per architectural decision |
| API Reference | Auto-generated (Swagger/OpenAPI) | Frontend + Integration | OpenAPI 3.1 | Per API change |
| README | Repository root + per project | New developers | Markdown | Per major change |
| Wiki | GitHub Wiki | Team + stakeholders | Markdown | Per sprint |
| Code Comments | Source code | Developers | XML doc comments (C#) / JSDoc (TS) | With code changes |
| Runbooks | `/docs/runbooks/` | DevOps / On-call | Markdown | Per operational procedure |
| Postman Collection | `/docs/api/` | Frontend team | JSON | Per API change |

### 21.2 ADR Format Standard

```
File: /docs/adr/ADR-{number}-{title-kebab}.md

# ADR-{N}: {Title}

**Status:** Proposed | Accepted | Deprecated | Superseded by ADR-{N}  
**Date:** YYYY-MM-DD  
**Deciders:** {List of people involved in the decision}  
**Context level:** System | Component | Module

## Context
What is the situation and problem that motivated this decision?

## Decision Drivers
- Driver 1 (constraint, quality attribute, etc.)
- Driver 2

## Considered Options
1. Option A
2. Option B
3. Option C

## Decision
We chose **Option X** because...

## Consequences
### Positive
- ...

### Negative / Trade-offs
- ...

### Risks
- ...

## Related ADRs
- ADR-{N}: {Reference}
```

### 21.3 API Documentation Standards

```
Every API endpoint must have:
  → Summary: one-line description
  → Description: detailed explanation including business context
  → Request body: schema with all field descriptions
  → Response bodies: all possible status codes documented (200, 201, 400, 401, 403, 404, 422, 500)
  → Authentication: specify required (Bearer token)
  → Authorization: specify required permission (e.g., "animals:write")
  → Example request body
  → Example response bodies (success + failure)

OpenAPI generation:
  → Swashbuckle.AspNetCore auto-generates from XML doc comments + attributes
  → swagger.json published at /api/swagger/v1/swagger.json
  → Swagger UI at /api/swagger (disabled in production; enabled in staging)
  → ReDoc UI at /api/docs (enabled in production for partner integration)
```

---

## 22. Risk Analysis

### 22.1 Architectural Risk Register

| ID | Risk | Category | Probability | Impact | Mitigation |
|---|---|---|---|---|---|
| AR-01 | **Global Query Filter bypass** — A developer queries outside EF Core (raw SQL) and returns cross-tenant data | Security | Medium | Critical | Architecture test enforces no raw SQL in repositories; RLS as backstop; security review checklist in PR template |
| AR-02 | **Tenant context loss in background jobs** — Hangfire job forgets to set TenantId and processes data in system context | Security | Medium | High | Mandatory pattern: all per-tenant jobs accept TenantId parameter; integration tests verify tenant isolation in job execution |
| AR-03 | **JWT secret exposure** — Signing key leaked via logs, environment variables, or code commit | Security | Low | Critical | RS256 (private key in AWS KMS, never in application memory); Secrets Manager for all secrets; git-secrets pre-commit hook |
| AR-04 | **SignalR connection saturation** — 500+ concurrent users saturate WebSocket connections under Eid peak load | Performance | Medium | High | Redis backplane scales SignalR across pods; load test to 1,000 concurrent connections before Eid; fallback to Server-Sent Events |
| AR-05 | **Hangfire job pile-up** — Slow database causes job queue to grow unboundedly during peak | Reliability | Medium | Medium | Job timeout limits (5 min per job); separate Hangfire worker pool from API; circuit breaker on job execution |
| AR-06 | **EF Core migration failure in production** — Bad migration corrupts schema during deployment | Data | Low | Critical | Migration-in-a-Job pattern (migrator Kubernetes Job runs before new pods start); always test migration against staging DB clone; rollback script for every migration |
| AR-07 | **Redis cache poisoning** — Stale or corrupt cache entry causes incorrect data display | Data | Low | Medium | Cache key includes version token; TTL ensures eventual consistency; cache entries are never the source of truth for writes |
| AR-08 | **Domain event handler failure** — AnimalSoldEventHandler throws; Finance module not updated | Reliability | Medium | High | Domain events dispatched after transaction commit; event handlers are idempotent; failed handlers logged and alertable; consider outbox pattern in Phase 2 |
| AR-09 | **Clean Architecture drift** — Developers under deadline pressure add business logic to controllers | Quality | High | High | Architecture tests (NetArchTest) fail the build on violations; ADR explains rationale; team training on CA principles |
| AR-10 | **N+1 query problem** — EF Core lazy loading causes N+1 in list endpoints under load | Performance | High | Medium | Lazy loading disabled globally; explicit Include() required; integration tests with query count assertions on list endpoints |
| AR-11 | **Angular bundle size growth** — Feature additions grow bundle, degrading 3G performance | Performance | High | Medium | Lazy loading enforced for all feature modules; bundle size CI gate (webpack-bundle-analyzer in CI); Lighthouse score gate ≥ 80 |
| AR-12 | **Third-party payment gateway downtime** — bKash/Nagad API down during subscription renewal | Availability | Medium | High | Retry with exponential backoff; grace period of 7 days on payment failure; fallback to bank transfer with manual verification |

---

## 23. Architecture Decision Records (ADR)

---

### ADR-001: Modular Monolith over Microservices

**Status:** Accepted — July 2026  
**Deciders:** CTO, Principal Architect, Senior Engineers

**Context:**  
Farm360 AI is a greenfield product with a 4–6 engineer team. The business requirements span 7 closely related modules that share significant state (Animal records cross-cut Feeding, Health, Inventory, and Finance).

**Options Considered:**

| Option | Pros | Cons |
|---|---|---|
| Microservices | Independent deployability, team autonomy at scale | Distributed system complexity, network failures, 6-engineer team cannot maintain 7+ services |
| Modular Monolith | Simplicity, single deploy, easy refactoring, ACID transactions across modules | All modules scale together, single deployment unit |
| Serverless | Low ops overhead | Cold starts unacceptable for interactive SaaS; complex local development |

**Decision:** Modular monolith with strict vertical slice boundaries enforced by architecture tests. Each feature folder is a bounded context — commands, queries, handlers, validators — that can be extracted to a microservice in Phase 3 without rewriting business logic.

**Consequences:**  
+ Single deployment, simple debugging, ACID transactions across modules  
+ Architecture tests prevent coupling between bounded contexts  
- All modules scale together (mitigated by read replicas and Redis)  
- Must discipline team to maintain module boundaries without runtime enforcement

---

### ADR-002: CQRS with MediatR

**Status:** Accepted  

**Context:**  
The system has asymmetric read/write patterns. Dashboard queries aggregate data from 5+ modules. Write operations (selling an animal) trigger cascading side effects. Without separation, handlers become mixed-concern God classes.

**Decision:** CQRS via MediatR. Commands (write side) go through the full pipeline including transaction behavior and domain event dispatch. Queries (read side) skip the transaction behavior and may hit the read replica or Redis cache.

**Key architectural benefit:** The MediatR pipeline is the perfect place for cross-cutting concerns (validation, logging, caching, transactions, auditing) applied uniformly without AOP magic or inheritance.

**Trade-off:** Increases file count (one file per command/query/handler). Mitigated by IDE navigation and the explicit simplicity of each file.

---

### ADR-003: Domain-Driven Design (DDD) for Domain Modeling

**Status:** Accepted  

**Context:**  
Livestock farm management has real domain complexity: gestation periods vary by species, FCR calculations depend on batch boundaries, quarantine status gates multiple operations, financial entries must be immutable after period close. This is not CRUD.

**Decision:** Apply DDD tactical patterns — Aggregates, Value Objects, Domain Events, Specifications — in the Domain layer. Domain Services for logic that spans multiple aggregates.

**Aggregate boundaries:**
- **Farm Aggregate** (root: Farm, children: Shed, Pen) — farm structure management
- **Animal Aggregate** (root: Animal, children: WeightRecord, BreedingRecord) — lifecycle of one animal
- **Batch Aggregate** (root: AnimalBatch, references: Animal IDs) — group management
- **FeedFormula Aggregate** (root: FeedFormula, children: FormulaIngredient) — ration management

**Why these boundaries?** Each aggregate is the unit of consistency. Updating an animal's weight does not require a transaction lock on the shed. Selling an animal requires locking only the Animal aggregate, not all animals in the farm.

---

### ADR-004: Shared Database with Global Query Filter for Multi-Tenancy

**Status:** Accepted  

**Context:**  
Three tenancy models are viable. The selection must balance cost (shared infrastructure), isolation (data security), and operational simplicity (small team).

**Decision:** Shared database with EF Core Global Query Filters + SQL Server Row-Level Security as a defense-in-depth backstop. Enterprise tenants (Corporation tier) provision a dedicated RDS instance.

**Rationale:** Global Query Filters are structural — they cannot be accidentally omitted by a developer because they're applied at the DbContext level. RLS makes the database itself reject unauthorized queries even if the application layer is compromised. This provides two independent security layers without operational complexity.

**Migration path:** When a tenant requires dedicated database (due to compliance or SLA needs), the operation is: (1) provision new RDS, (2) copy data, (3) update tenant's connection string in Secrets Manager, (4) verify with smoke tests. Zero code changes required.

---

### ADR-005: Redis as L2 Cache with Event-Driven Invalidation

**Status:** Accepted  

**Context:**  
The dashboard aggregates data from 5 modules on every load. Without caching, a single dashboard load generates 10–15 SQL queries. At 500 concurrent users, this saturates the database.

**Decision:** Redis as L2 distributed cache with L1 in-memory per-pod cache for ultra-hot data. Cache invalidation driven by domain events — when data changes, the relevant cache keys are evicted. No time-based eviction as the primary strategy (would allow stale financial data).

**Event-driven invalidation detail:** Domain event handlers call `ICacheService.RemoveAsync(pattern)` after every state-changing operation. Redis pub/sub broadcasts the invalidation to all pods (to clear their L1 caches). This ensures cross-pod consistency within 100ms of a data change.

---

### ADR-006: SignalR for Real-Time Notifications

**Status:** Accepted  

**Context:**  
Farm managers need real-time alerts for vaccination overdues, critical health incidents, and low stock events. Polling-based solutions (Angular setInterval) would create unnecessary database load and deliver stale alerts.

**Decision:** SignalR with Redis backplane. WebSocket as primary transport; Server-Sent Events and Long Polling as automatic fallbacks for networks where WebSocket is blocked.

**Tenant isolation in SignalR:** Each user joins a SignalR Group named by their TenantId on connection. Notifications are broadcast to `Clients.Group(tenantId)` — ensuring cross-tenant notification isolation.

**Why Redis backplane?** Multiple API pods each hold their own SignalR connections. Without a backplane, a notification published on pod 1 only reaches clients connected to pod 1. Redis backplane ensures all pods receive the message and forward to their connected clients.

---

### ADR-007: Hangfire for Background Jobs

**Status:** Accepted  

**Context:**  
The system requires reliable, scheduled background processing: daily vaccination reminders, monthly report generation, subscription expiry checks. These must survive pod restarts and be retried on failure.

**Options:**
| Option | Assessment |
|---|---|
| Quartz.NET | Mature scheduler; no built-in dashboard; more complex setup |
| Hangfire | Production-ready; excellent dashboard; retry logic built-in; SQL Server persistence |
| AWS EventBridge + Lambda | Serverless; no always-on state; cold starts; different programming model |

**Decision:** Hangfire with SQL Server persistence (dedicated `hangfire` schema). Hangfire Server runs embedded in the API pods (not a separate service — team is too small to manage a separate job service).

**Operational note:** Hangfire dashboard at `/hangfire` is protected by Owner/Admin role authorization. Failed jobs are visible and re-triable from the dashboard without a code deployment.

---

### ADR-008: JWT with RS256 and Rotating Refresh Tokens

**Status:** Accepted  

**Context:**  
The system needs stateless authentication for horizontal scaling. Token security must prevent token reuse attacks. The architecture must support future microservices that can verify tokens without contacting the auth server.

**Decision:** 
- **Access tokens:** JWT RS256, 15-minute expiry. Short expiry limits damage from token theft.
- **Refresh tokens:** Opaque, stored hashed in DB, 30-day expiry, rotating (each use invalidates and issues a new one). Rotation means a stolen refresh token is detected immediately when the legitimate user next refreshes.
- **Key management:** Private key in AWS KMS. Public key distributed via `/.well-known/jwks.json` endpoint.

**OTP for Bangladesh market:** Phone OTP as primary auth (not password) — Bangladesh users are highly mobile-number-centric and resistant to password-based flows.

---

### ADR-009: Serilog with Structured Logging

**Status:** Accepted  

**Context:**  
Multi-tenant debugging requires correlating logs across a request's entire journey. Unstructured text logs make tenant-specific investigation extremely slow.

**Decision:** Serilog with structured JSON output. Every log entry carries: TenantId, UserId, CorrelationId, UserRole. Serilog enrichers inject these automatically from the ambient HTTP context and ITenantService.

**Production sinks:** CloudWatch Logs (searchable, 90-day retention). CloudWatch Logs Insights for ad-hoc log queries. Cloudwatch alarms on error rate.

**Development sink:** Seq for the rich structured log UI — searchable by any property with minimal configuration.

**Sensitive data policy:** Custom IDestructuringPolicy masks phone numbers, emails, and NID numbers before any log output — enforced at the Serilog pipeline level, not per-log-statement (too unreliable).

---

### ADR-010: FluentValidation for All Input Validation

**Status:** Accepted  

**Context:**  
Validation rules are business logic. If validation lives in controllers as data annotations, it cannot be unit-tested in isolation and couples business rules to the HTTP layer.

**Decision:** FluentValidation with MediatR ValidationBehavior. All validation runs before any handler executes. Validators are co-located with their commands in the Application layer. Async validators (uniqueness checks) run via IAnimalRepository injected into the validator.

**No Data Annotations on domain entities:** Data annotations mix infrastructure concerns (database constraints, serialization hints) with domain concerns. Domain entities use private setters and factory methods for invariant enforcement — not annotations.

---

### ADR-011: Angular 22 Standalone Components with NgRx Signal Store

**Status:** Accepted  

**Context:**  
Angular 22 makes standalone components the default. NgRx Signal Store is the modern, signal-based state management approach that aligns with Angular's direction toward fine-grained reactivity.

**Decision:** All components are standalone. No NgModules except where third-party libraries require them. NgRx Signal Store (one per feature, not one global store) for reactive state management. RxJS for HTTP calls and event streams (where observables are the natural fit).

**PWA:** @angular/service-worker with Workbox for offline caching. Critical paths (animal list, vaccination schedule) cached for offline use. Data entry forms queue offline mutations for sync on reconnect.

---

### ADR-012: SQL Server as Primary Database

**Status:** Accepted  

**Context:**  
The system requires: ACID transactions across multiple tables (animal sale → inventory deduction → finance entry), full-text search (animal search), complex reporting queries, Row-Level Security for multi-tenancy.

**Options Considered:**
| Database | Assessment |
|---|---|
| PostgreSQL | Excellent; open source; strong JSON support; but team expertise is on SQL Server |
| SQL Server | Team expertise; best .NET integration; RLS built-in; excellent Azure + AWS RDS support |
| MySQL | Simpler; less feature-rich for complex reporting; no RLS |

**Decision:** SQL Server on AWS RDS. The team's existing SQL Server expertise reduces operational risk. SQL Server's Row-Level Security integrates cleanly with the multi-tenant security design. AWS RDS provides managed operations (patching, failover, backups) appropriate for a small team.

**Future:** If regional expansion requires open-source for cost or licensing reasons, the Clean Architecture approach means the database can be swapped by replacing repository implementations — no domain or application layer changes.

---

*This document is the authoritative technical reference for the Farm360 AI engineering organization. All architectural decisions documented herein are binding until formally superseded by a new ADR. Engineers who disagree with an architectural decision should raise a new ADR with their proposal — not silently deviate from the documented architecture.*

---

**Farm360 AI — Software Architecture Document**  
*© 2026 Farm360 AI Engineering Organization. All Rights Reserved.*
