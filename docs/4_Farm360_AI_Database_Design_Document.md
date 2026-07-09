# Farm360 AI — Database Design Document (DDD)

**Document ID:** F360-DDD-2026-001  
**Version:** 1.0  
**Status:** Approved for Implementation  
**Prepared by:** Senior Database Architecture & Domain-Driven Design Office  
**Date:** July 2026  
**Parent Documents:** PVD v1.0 · PRD v1.0 · SAD v1.0  
**Classification:** Confidential — Engineering Use  
**Database Engine:** Microsoft SQL Server 2022 (AWS RDS)  
**ORM:** Entity Framework Core 10 (Code-First with Fluent API)

---

> *"A database schema is the most expensive thing to change in a production system. Design it as if you cannot change it — because in practice, you often cannot."*

---

## Table of Contents

1. [Document Overview & Design Philosophy](#1-document-overview--design-philosophy)
2. [Complete Domain Model](#2-complete-domain-model)
3. [Bounded Contexts](#3-bounded-contexts)
4. [Entities](#4-entities)
5. [Value Objects](#5-value-objects)
6. [Aggregates](#6-aggregates)
7. [Domain Services](#7-domain-services)
8. [Entity Relationships](#8-entity-relationships)
9. [ER Diagram](#9-er-diagram)
10. [Database Schema — Catalog](#10-database-schema--catalog)
11. [Table Design — Platform Schema](#11-table-design--platform-schema)
12. [Table Design — Livestock Schema](#12-table-design--livestock-schema)
13. [Table Design — Feeding Schema](#13-table-design--feeding-schema)
14. [Table Design — Health Schema](#14-table-design--health-schema)
15. [Table Design — Inventory Schema](#15-table-design--inventory-schema)
16. [Table Design — Finance Schema](#16-table-design--finance-schema)
17. [Table Design — Audit Schema](#17-table-design--audit-schema)
18. [Soft Delete Design](#18-soft-delete-design)
19. [Audit Columns](#19-audit-columns)
20. [Multi-Tenant Database Design](#20-multi-tenant-database-design)
21. [Migration Strategy](#21-migration-strategy)
22. [Performance Optimization](#22-performance-optimization)
23. [Data Partition Strategy](#23-data-partition-strategy)
24. [Data Retention Strategy](#24-data-retention-strategy)
25. [Appendix](#25-appendix)

---

## 1. Document Overview & Design Philosophy

### 1.1 Purpose

This document defines the complete database architecture for Farm360 AI — from DDD domain model to physical SQL Server table design. It is the authoritative reference for all data persistence decisions, schema evolution rules, and data management policies.

### 1.2 Core Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| **Schema separation** | One schema per bounded context | Mirrors DDD bounded contexts; enables future service extraction; clear ownership |
| **Primary key type** | `UNIQUEIDENTIFIER` (GUID) | Distributed-safe, no coordination needed for ID generation; enables client-side ID generation for offline sync |
| **PK generation strategy** | Sequential GUID (`NEWSEQUENTIALID()`) | Avoids index fragmentation problem of random GUIDs; maintains clustered index performance |
| **Soft delete** | `IsDeleted`, `DeletedAt`, `DeletedByUserId` on every table | Satisfies audit requirements; enables data recovery; required for financial immutability |
| **Audit columns** | 6 standard columns on every table | Traceable to user, timestamp, and version; required for compliance |
| **Tenant isolation** | `TenantId` column on every tenant-scoped table | Combined with EF Core Global Query Filter + SQL Server RLS = defense-in-depth |
| **Money storage** | `DECIMAL(18,4)` with explicit currency code column | Avoids floating-point errors; future multi-currency support |
| **Enumerations** | `TINYINT` in DB, documented in application code | Storage-efficient; join-free; enum values documented in lookup tables for reporting |
| **Optimistic concurrency** | `RowVersion ROWVERSION` on every mutable entity | Prevents lost updates without pessimistic locking; performance-safe |
| **Temporal tables** | SQL Server System-Versioned Temporal Tables on financial entities | Automatic point-in-time recovery for financial data |

### 1.3 Naming Conventions

| Object | Convention | Example |
|---|---|---|
| Schema | lowercase | `platform`, `livestock`, `feeding` |
| Table | PascalCase, plural | `Animals`, `VaccinationRecords` |
| Column | PascalCase | `AnimalId`, `DateOfBirth`, `TenantId` |
| Primary Key | `Id` (UNIQUEIDENTIFIER) | `Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID()` |
| Foreign Key column | `{Entity}Id` | `AnimalId`, `ShedException`, `TenantId` |
| FK constraint name | `FK_{Table}_{ReferencedTable}` | `FK_Animals_Sheds` |
| Index name | `IX_{Table}_{Columns}` | `IX_Animals_TenantId_Status` |
| Unique constraint | `UQ_{Table}_{Columns}` | `UQ_Animals_TenantId_TagId` |
| Check constraint | `CK_{Table}_{Column}` | `CK_Animals_Sex` |
| Default constraint | `DF_{Table}_{Column}` | `DF_Animals_IsDeleted` |

---

## 2. Complete Domain Model

### 2.1 Domain Overview

Farm360 AI operates across **7 bounded contexts**, each representing a coherent subdomain with its own ubiquitous language, aggregates, and persistence responsibility.

```
┌──────────────────────────────────────────────────────────────────────────┐
│                      FARM360 AI — DOMAIN MODEL                          │
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                     IDENTITY CONTEXT                            │    │
│  │  Tenant · Organization · User · Role · Permission · Invitation  │    │
│  │  Subscription · SubscriptionPlan · BillingCycle                 │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                              ↑ referenced by all contexts below          │
│                                                                          │
│  ┌────────────────┐  ┌────────────────┐  ┌──────────────────────────┐  │
│  │  FARM CONTEXT  │  │LIVESTOCK CONTEXT│  │    FEEDING CONTEXT       │  │
│  │  Farm          │  │  Animal         │  │  FeedIngredient           │  │
│  │  Shed          │  │  AnimalBatch    │  │  FeedFormula              │  │
│  │  Pen           │  │  WeightRecord   │  │  FormulaIngredient        │  │
│  │                │  │  BreedingRecord │  │  FeedingSchedule          │  │
│  │                │  │  AnimalTransfer │  │  FeedConsumptionLog       │  │
│  └────────────────┘  └────────────────┘  └──────────────────────────┘  │
│                                                                          │
│  ┌────────────────┐  ┌────────────────┐  ┌──────────────────────────┐  │
│  │ HEALTH CONTEXT │  │INVENTORY CONTEXT│  │    FINANCE CONTEXT       │  │
│  │ VaxProtocol    │  │  InventoryItem  │  │  ChartOfAccount           │  │
│  │ VaxSchedule    │  │  Supplier       │  │  FinancialEntry           │  │
│  │ VaxRecord      │  │  StockBatch     │  │  AnimalCostLedger         │  │
│  │ TreatmentRecord│  │  StockTransaction│  │  BatchProfitLoss         │  │
│  │ DiseaseIncident│  │                 │  │  LoanRecord               │  │
│  │ MortalityRecord│  │                 │  │  LoanRepayment            │  │
│  └────────────────┘  └────────────────┘  └──────────────────────────┘  │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                    AUDIT CONTEXT (Cross-cutting)                  │   │
│  │              AuditLog · Notification · SystemEvent                │   │
│  └──────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Domain Vocabulary (Ubiquitous Language)

| Term | Domain Meaning |
|---|---|
| **Tenant** | A registered organization (farm business) — the root of all data isolation |
| **Farm** | A physical farm location owned by a Tenant |
| **Shed** | A building or enclosed area within a Farm |
| **Pen** | A subdivision within a Shed for grouping animals |
| **Animal** | An individual livestock animal with a unique identity |
| **Batch** | A management group of animals tracked together for performance/P&L |
| **ADG** | Average Daily Gain — weight gained per day; key performance indicator |
| **FCR** | Feed Conversion Ratio — kg feed per kg weight gain; feeding efficiency metric |
| **BCS** | Body Condition Score — 1.0 to 5.0 scale of animal body condition |
| **Vaccination Protocol** | A template defining vaccines, timing, and dosing for a species |
| **Vaccination Schedule** | An instance of a protocol assigned to specific animals with due dates |
| **Treatment Record** | A record of veterinary intervention on a specific animal |
| **Disease Incident** | A documented disease outbreak affecting one or more animals |
| **Feed Formula** | A recipe specifying the mix of ingredients for a ration |
| **Feeding Schedule** | An assignment of a formula to a shed/pen/batch |
| **Stock Batch** | A specific purchase lot of an inventory item with expiry and cost |
| **Stock Transaction** | Any movement of inventory (in, out, adjustment, write-off) |
| **Financial Entry** | A double-entry style income or expense record |
| **Animal Cost Ledger** | A running total of all costs accumulated against a specific animal |
| **Batch P&L** | Profit & Loss statement computed for a management batch |
| **Subscription Tier** | The pricing plan level (Bittho, Khamar, Banik, Corporation, NGO) |

---

## 3. Bounded Contexts

### 3.1 Bounded Context Map

```
                         ┌───────────────────────────────────────┐
                         │         IDENTITY CONTEXT               │
                         │  (Upstream — supplies TenantId,        │
                         │   UserId to ALL other contexts)         │
                         └─────────────────┬─────────────────────┘
                                           │ (Shared Kernel: TenantId, UserId)
          ┌────────────────────────────────┼──────────────────────────────────┐
          ▼                                ▼                                  ▼
  ┌───────────────┐               ┌─────────────────┐              ┌──────────────────┐
  │  FARM CONTEXT │               │LIVESTOCK CONTEXT │              │  FEEDING CONTEXT │
  │               │               │                  │              │                  │
  │ Upstream for  │◄──────────────│ References Farm, │◄─────────────│ References Farm, │
  │ all contexts  │  Open Host    │ Shed, Pen IDs    │  Open Host   │ Shed, Batch IDs  │
  │               │  (FK refs)    │                  │  (FK refs)   │                  │
  └───────────────┘               └─────────────────┘              └──────────────────┘
          ▲                                ▲                                  │
          │                                │                                  ▼
          │                       ┌─────────────────┐              ┌──────────────────┐
          │                       │  HEALTH CONTEXT  │              │INVENTORY CONTEXT │
          │                       │                  │◄─────────────│  Supplies items  │
          │                       │ References Animal│  Event-driven│  to Health via   │
          │                       │ Shed, Batch IDs  │  (medicine   │  domain events   │
          │                       │                  │   deduction) │                  │
          │                       └─────────────────┘              └──────────────────┘
          │                                │                                  │
          │                                └──────────────────┐               │
          │                                                   ▼               ▼
          │                                          ┌────────────────────────────┐
          └──────────────────────────────────────────│      FINANCE CONTEXT        │
                                                     │  Downstream consumer of     │
                                                     │  domain events from all     │
                                                     │  other contexts             │
                                                     └────────────────────────────┘
                                                                  ▲
                                                     ┌────────────┴────────────┐
                                                     │      AUDIT CONTEXT       │
                                                     │  Cross-cutting; receives  │
                                                     │  events from all contexts │
                                                     └──────────────────────────┘
```

### 3.2 Context-to-Schema Mapping

| Bounded Context | Database Schema | Tables Count | Key Responsibility |
|---|---|---|---|
| Identity | `platform` | 10 | Tenants, users, roles, subscriptions, billing |
| Farm | `platform` | 3 | Physical farm structure (Farm, Shed, Pen) |
| Livestock | `livestock` | 6 | Animal lifecycle from birth/purchase to disposal |
| Feeding | `feeding` | 5 | Feed ingredients, formulas, schedules, consumption |
| Health | `health` | 7 | Vaccinations, treatments, incidents, mortality |
| Inventory | `inventory` | 5 | Stock items, suppliers, transactions, valuation |
| Finance | `finance` | 6 | Ledger entries, P&L, cost tracking, loans |
| Audit | `audit` | 3 | Immutable audit logs, notifications, system events |

---

## 4. Entities

### 4.1 Entity Classification

| Entity | Type | Aggregate | Context | Table |
|---|---|---|---|---|
| **Tenant** | Aggregate Root | Tenant | Identity | `platform.Tenants` |
| **Organization** | Aggregate Root | Tenant | Identity | `platform.Organizations` |
| **ApplicationUser** | Aggregate Root | User | Identity | `platform.Users` |
| **OrganizationUser** | Entity | Tenant | Identity | `platform.OrganizationUsers` |
| **Role** | Value Reference | — | Identity | `platform.Roles` |
| **UserInvitation** | Entity | Tenant | Identity | `platform.UserInvitations` |
| **SubscriptionPlan** | Value Reference | — | Identity | `platform.SubscriptionPlans` |
| **Subscription** | Aggregate Root | Subscription | Identity | `platform.Subscriptions` |
| **BillingRecord** | Entity | Subscription | Identity | `platform.BillingRecords` |
| **Farm** | Aggregate Root | Farm | Farm | `platform.Farms` |
| **Shed** | Entity | Farm | Farm | `platform.Sheds` |
| **Pen** | Entity | Farm | Farm | `platform.Pens` |
| **Animal** | Aggregate Root | Animal | Livestock | `livestock.Animals` |
| **AnimalBatch** | Aggregate Root | Batch | Livestock | `livestock.AnimalBatches` |
| **AnimalBatchMember** | Entity | Batch | Livestock | `livestock.AnimalBatchMembers` |
| **WeightRecord** | Entity | Animal | Livestock | `livestock.WeightRecords` |
| **BreedingRecord** | Entity | Animal | Livestock | `livestock.BreedingRecords` |
| **AnimalTransferLog** | Entity | Animal | Livestock | `livestock.AnimalTransferLogs` |
| **AnimalPhoto** | Entity | Animal | Livestock | `livestock.AnimalPhotos` |
| **FeedIngredient** | Aggregate Root | Ingredient | Feeding | `feeding.FeedIngredients` |
| **FeedFormula** | Aggregate Root | Formula | Feeding | `feeding.FeedFormulas` |
| **FormulaIngredient** | Entity | Formula | Feeding | `feeding.FormulaIngredients` |
| **FeedingSchedule** | Entity | Formula | Feeding | `feeding.FeedingSchedules` |
| **FeedConsumptionLog** | Aggregate Root | Consumption | Feeding | `feeding.FeedConsumptionLogs` |
| **ConsumptionDetail** | Entity | Consumption | Feeding | `feeding.ConsumptionDetails` |
| **VaccinationProtocol** | Aggregate Root | Protocol | Health | `health.VaccinationProtocols` |
| **ProtocolScheduleItem** | Entity | Protocol | Health | `health.ProtocolScheduleItems` |
| **AnimalVaccinationSchedule** | Entity | Protocol | Health | `health.AnimalVaccinationSchedules` |
| **VaccinationRecord** | Aggregate Root | VaccinationRecord | Health | `health.VaccinationRecords` |
| **TreatmentRecord** | Aggregate Root | Treatment | Health | `health.TreatmentRecords` |
| **DiseaseIncident** | Aggregate Root | Incident | Health | `health.DiseaseIncidents` |
| **DiseaseIncidentAnimal** | Entity | Incident | Health | `health.DiseaseIncidentAnimals` |
| **VetVisit** | Entity | — | Health | `health.VetVisits` |
| **MortalityRecord** | Aggregate Root | Mortality | Health | `health.MortalityRecords` |
| **InventoryItem** | Aggregate Root | Item | Inventory | `inventory.InventoryItems` |
| **Supplier** | Aggregate Root | Supplier | Inventory | `inventory.Suppliers` |
| **StockBatch** | Entity | Item | Inventory | `inventory.StockBatches` |
| **StockTransaction** | Aggregate Root | Transaction | Inventory | `inventory.StockTransactions` |
| **ChartOfAccount** | Value Reference | — | Finance | `finance.ChartOfAccounts` |
| **FinancialEntry** | Aggregate Root | FinancialEntry | Finance | `finance.FinancialEntries` |
| **AnimalCostLedger** | Entity | Animal | Finance | `finance.AnimalCostLedgers` |
| **BatchProfitLoss** | Read Model | — | Finance | `finance.BatchProfitLoss` (computed) |
| **LoanRecord** | Aggregate Root | Loan | Finance | `finance.LoanRecords` |
| **LoanRepayment** | Entity | Loan | Finance | `finance.LoanRepayments` |
| **AuditLog** | Append-Only Entity | — | Audit | `audit.AuditLogs` |
| **Notification** | Entity | — | Audit | `audit.Notifications` |
| **SystemEvent** | Append-Only Entity | — | Audit | `audit.SystemEvents` |

---

## 5. Value Objects

Value Objects are immutable, identity-less concepts defined entirely by their attributes. In SQL Server, they are persisted as **owned entity columns** on the entity table (no separate table unless they are complex or frequently queried independently).

### 5.1 Value Object Definitions

| Value Object | Columns in DB | Used By | Rationale |
|---|---|---|---|
| **Money** | `Amount DECIMAL(18,4)`, `Currency CHAR(3)` | FinancialEntry, AnimalCostLedger, StockBatch | Avoids float errors; explicit currency for future expansion |
| **AnimalTag** | `TagId NVARCHAR(50)`, `TagType TINYINT` | Animal | TagType: 0=Manual, 1=EarTag, 2=RFID |
| **Weight** | `WeightKg DECIMAL(8,3)`, `WeightUnit TINYINT` | WeightRecord | Unit: 0=KG (always KG in MVP; extensible) |
| **NutritionalProfile** | `DryMatterPct DECIMAL(5,2)`, `CrudeProteinPct DECIMAL(5,2)`, `MetabEnergy DECIMAL(8,4)` | FeedIngredient, FeedFormula | Stored computed values on formula for quick read |
| **Address** | `AddressLine1`, `AddressLine2`, `Upazila`, `District`, `Division`, `PostalCode` | Tenant, Farm, Supplier | Bangladesh-specific administrative divisions |
| **DateRange** | `StartDate DATE`, `EndDate DATE` | FeedingSchedule, VaccinationSchedule | Always store both ends for range queries |
| **BodyConditionScore** | `BcsValue DECIMAL(3,1)` | Animal | 1.0–5.0; stored on animal for latest, on WeightRecord for history |
| **GestationPeriod** | `ExpectedCalvingDate DATE` | BreedingRecord | Computed at domain layer from species gestation |

### 5.2 Enumeration Reference Tables

Enumerations stored as `TINYINT` in domain tables. Reference tables provided for reporting and UI lookups.

```
platform.EnumAnimalSpecies:  (0=CattleBeef, 1=CattleDairy, 2=Goat, 3=Sheep, 4=Poultry...)
platform.EnumAnimalBreeds:   (linked to species; local Bangladesh breed names in Bangla + English)
platform.EnumAnimalSex:      (0=Male, 1=Female)
platform.EnumAnimalStatus:   (0=Active, 1=Sold, 2=Slaughtered, 3=Dead, 4=Quarantined, 5=Transferred)
platform.EnumDisposalReason: (0=Sale, 1=Slaughter, 2=NaturalDeath, 3=Disease, 4=Accident, 5=Unknown)
platform.EnumBreedingMethod: (0=Natural, 1=ArtificialInsemination)
platform.EnumTagType:        (0=Manual, 1=EarTag, 2=RFID)
platform.EnumInventoryCategory: (0=Feed, 1=Medicine, 2=Chemical, 3=Equipment, 4=Other)
platform.EnumStockTransactionType: (0=StockIn, 1=FeedDeduction, 2=MedicineDeduction, 3=ManualStockOut, 4=WriteOff, 5=Adjustment)
platform.EnumFinancialEntryType: (0=Income, 1=Expense)
platform.EnumFinancialEntrySource: (0=Manual, 1=AnimalSale, 2=FeedConsumption, 3=MedicineUse, 4=StockPurchase, 5=MilkSale)
platform.EnumSubscriptionTier: (0=Bittho, 1=Khamar, 2=Banik, 3=Corporation, 4=NGO)
platform.EnumUserRole:        (0=Owner, 1=FarmManager, 2=Veterinarian, 3=Worker, 4=Accountant, 5=Viewer)
```

---

## 6. Aggregates

### 6.1 Aggregate Boundaries and Invariants

An aggregate is a cluster of entities that must be changed together to maintain consistency. Each aggregate has one **Aggregate Root** — the only entry point for state changes.

---

#### Aggregate: Tenant

**Root:** `Tenant`  
**Invariants:**
- A tenant must always have at least one Organization
- A tenant's active animal count must not exceed the subscription tier limit
- A suspended tenant's data is read-only

```
Tenant (Root)
  └── Subscription (1..1)
        └── BillingRecord (0..*)
  └── Organization (1..1)
        └── OrganizationUser (1..*)
        └── UserInvitation (0..*)
```

---

#### Aggregate: Farm

**Root:** `Farm`  
**Invariants:**
- A Farm belongs to exactly one Tenant
- A Shed belongs to exactly one Farm
- A Pen belongs to exactly one Shed
- Deleting a Farm requires all Animals to be disposed or transferred first

```
Farm (Root)
  └── Shed (0..*)
        └── Pen (0..*)
```

---

#### Aggregate: Animal

**Root:** `Animal`  
**Invariants:**
- An animal's TagId must be unique within the organization
- An animal cannot have health records after its death date
- A sold/dead animal is immutable
- Weight cannot predate DateOfBirth
- A quarantined animal cannot be sold or transferred

```
Animal (Root)
  ├── WeightRecord (0..*)
  ├── BreedingRecord (0..*)    [if Female]
  ├── AnimalBatchMember (0..*)  [cross-reference; batch is separate aggregate]
  ├── AnimalTransferLog (0..*)
  └── AnimalPhoto (0..5)
```

---

#### Aggregate: AnimalBatch

**Root:** `AnimalBatch`  
**Invariants:**
- A batch belongs to one Tenant
- A batch can span multiple sheds
- A batch's P&L is finalized when all animals are disposed
- Animals can be added/removed from a batch while it is Active

```
AnimalBatch (Root)
  └── AnimalBatchMember (1..*)   [references Animal.Id, not owns Animal]
```

---

#### Aggregate: FeedFormula

**Root:** `FeedFormula`  
**Invariants:**
- A formula must have ≥ 2 ingredients
- Formula nutritional profile is auto-computed on save
- A formula assigned to active sheds cannot be deleted (only archived)

```
FeedFormula (Root)
  └── FormulaIngredient (2..*)
  └── FeedingSchedule (0..*)
```

---

#### Aggregate: FeedConsumptionLog

**Root:** `FeedConsumptionLog` (per shed per day)  
**Invariants:**
- Only one consumption log per shed per date
- Consumption cannot be logged for future dates
- Posting a log triggers inventory deduction domain event

```
FeedConsumptionLog (Root)
  └── ConsumptionDetail (1..*)   [one per ingredient]
```

---

#### Aggregate: VaccinationProtocol

**Root:** `VaccinationProtocol`  
**Invariants:**
- A protocol is species-specific
- Protocol items define the schedule (initial + boosters)
- Assigning a protocol generates AnimalVaccinationSchedule records

```
VaccinationProtocol (Root)
  └── ProtocolScheduleItem (1..*)
```

---

#### Aggregate: VaccinationRecord, TreatmentRecord, DiseaseIncident, MortalityRecord

These are independent aggregate roots — each represents a complete health event.

---

#### Aggregate: InventoryItem

**Root:** `InventoryItem`  
**Invariants:**
- CurrentStockQuantity must never go below zero (warned; Owner can override)
- Weighted Average Cost recalculated on each StockIn
- Expiry date alerts generated by background job

```
InventoryItem (Root)
  └── StockBatch (0..*)
  └── StockTransaction (0..*)   [immutable after creation]
```

---

#### Aggregate: FinancialEntry

**Root:** `FinancialEntry`  
**Invariants:**
- Entries in a closed period cannot be deleted (reversal entry required)
- Auto-posted entries are read-only (Source ≠ Manual)
- Amount must be > 0

```
FinancialEntry (Root — immutable after close)
```

#### Aggregate: LoanRecord

```
LoanRecord (Root)
  └── LoanRepayment (0..*)
```

---

## 7. Domain Services

### 7.1 Domain Services and Their Database Implications

| Domain Service | Responsibility | DB Operations |
|---|---|---|
| `FcrCalculationService` | Calculates FCR for a batch using total feed consumed vs. total weight gain | Reads FeedConsumptionLogs + WeightRecords across a date range; no writes; result cached |
| `AdgCalculationService` | Calculates ADG from two or more weight records | Reads WeightRecords ordered by date; computes (latestWeight - firstWeight) / days |
| `BreakEvenCalculatorService` | Computes break-even sale price per animal | Reads AnimalCostLedger sum for animal; pure calculation, no write |
| `WeightedAverageCostService` | Recalculates WAC per inventory item after each stock-in | Reads StockBatches; updates InventoryItems.WeightedAverageCost |
| `VaccinationScheduleService` | Generates AnimalVaccinationSchedule records when a protocol is assigned | Bulk insert into health.AnimalVaccinationSchedules |
| `BatchProfitLossService` | Computes P&L snapshot for a batch | Aggregates FinancialEntries linked to batch; updates finance.BatchProfitLoss |
| `AnimalCostPostingService` | Posts costs to AnimalCostLedger from domain events | Inserts into finance.AnimalCostLedgers; updates running total |
| `StockDeductionService` | Deducts stock on FeedConsumption or TreatmentRecord | Updates inventory.InventoryItems.CurrentStockQty; inserts StockTransaction |
| `SubscriptionLimitService` | Checks tenant is within tier limits | Reads platform.Subscriptions + COUNT(livestock.Animals) |

---

## 8. Entity Relationships

### 8.1 Cardinality Map

```
PLATFORM CONTEXT
════════════════
Tenant ──< Organization ──< OrganizationUser >── User
                        ──< UserInvitation
                        ──< Farm ──< Shed ──< Pen
Tenant ──── Subscription ──< BillingRecord

LIVESTOCK CONTEXT  
══════════════════
Farm ──< Animal ──< WeightRecord
                ──< BreedingRecord
                ──< AnimalBatchMember >── AnimalBatch
                ──< AnimalTransferLog
                ──< AnimalPhoto
Shed ──< Animal (current assignment)
Pen  ──< Animal (current assignment)

FEEDING CONTEXT
═══════════════
Tenant ──< FeedIngredient (shared catalog + tenant-specific)
Tenant ──< FeedFormula ──< FormulaIngredient >── FeedIngredient
                       ──< FeedingSchedule (→ Shed/Pen/Batch reference)
Shed ──< FeedConsumptionLog ──< ConsumptionDetail >── FeedIngredient

HEALTH CONTEXT
══════════════
Tenant ──< VaccinationProtocol ──< ProtocolScheduleItem
Animal ──< AnimalVaccinationSchedule >── ProtocolScheduleItem
Animal ──< VaccinationRecord
Animal ──< TreatmentRecord
         ──── InventoryItem (medicine deduction FK)
Animal ──< DiseaseIncidentAnimal >── DiseaseIncident
Animal ──── MortalityRecord

INVENTORY CONTEXT
═════════════════
Farm ──< InventoryItem ──< StockBatch
                       ──< StockTransaction
Supplier ──< StockBatch (optional supplier ref)

FINANCE CONTEXT
═══════════════
Tenant ──< FinancialEntry >── ChartOfAccount
         ──── Animal (optional link)
         ──── AnimalBatch (optional link)
         ──── Shed (optional link)
Animal ──< AnimalCostLedger
AnimalBatch ──── BatchProfitLoss (computed)
Tenant ──< LoanRecord ──< LoanRepayment
```

---

## 9. ER Diagram

> The following ASCII ER diagram shows core cross-context relationships. Each box represents a table; arrows show foreign key directions.

```
═══════════════════════════════ PLATFORM SCHEMA ════════════════════════════════

  ┌──────────────┐    1      1 ┌────────────────┐    1      * ┌──────────────┐
  │   Tenants    │────────────►│  Organizations │────────────►│   Farms      │
  │ (Id, PK)     │             │ (Id, TenantId) │             │(Id,TenantId) │
  └──────────────┘             └────────────────┘             └──────┬───────┘
         │ 1                           │ *                            │ 1
         │                             ▼                              ▼
         │                    ┌────────────────┐             ┌──────────────┐
         │ 1                  │OrgUsers        │             │   Sheds      │
         ▼                    │(OrgId, UserId) │             │(Id, FarmId)  │
  ┌──────────────┐            └────────┬───────┘             └──────┬───────┘
  │Subscriptions │                     │ *                           │ 1
  │(Id,TenantId) │                     ▼                            ▼
  └──────────────┘            ┌────────────────┐             ┌──────────────┐
                              │     Users      │             │    Pens      │
                              │ (Id, global)   │             │(Id, ShedId)  │
                              └────────────────┘             └──────────────┘

══════════════════════════════ LIVESTOCK SCHEMA ═════════════════════════════════

  ┌─────────────────────────────────────────────────────────────────────────┐
  │                              Animals                                    │
  │ Id · TenantId · FarmId · ShedException · PenId(nullable)               │
  │ TagId · TagType · Species · Breed · Sex · Status · DateOfBirth          │
  │ AcquisitionDate · AcquisitionPrice · CurrentShedId · CurrentPenId       │
  │ DamId(self-ref,nullable) · SireId(self-ref,nullable)                    │
  │ LatestWeightKg · LatestWeightDate · BcsValue                            │
  │ IsDeleted · [Audit cols] · RowVersion                                   │
  └────┬────────────────────────────────────────────────────────────────────┘
       │
       ├──────────────────────────────┐
       │ *                            │ *
       ▼                              ▼
  ┌────────────────┐         ┌───────────────────┐
  │  WeightRecords │         │  BreedingRecords   │
  │(Id, AnimalId)  │         │(Id, AnimalId, Sire)│
  └────────────────┘         └───────────────────┘

  ┌──────────────┐    *     * ┌───────────────┐
  │ AnimalBatches│────────────│AnimalBatchMemb│
  │(Id,TenantId) │            │(BatchId,AnmId)│
  └──────────────┘            └───────────────┘

═══════════════════════════════ FEEDING SCHEMA ══════════════════════════════════

  ┌─────────────────┐    *      * ┌─────────────────────┐
  │  FeedIngredients│◄────────────│  FormulaIngredients  │
  │(Id, TenantId)   │             │(FormulaId,IngredId)  │
  └─────────────────┘             └──────────┬──────────┘
                                             │ *
                                             ▼
  ┌─────────────────┐    1      * ┌─────────────────────┐
  │   FeedFormulas  │◄────────────│  FeedingSchedules    │
  │(Id, TenantId)   │             │(FormulaId,ShedId)    │
  └─────────────────┘             └─────────────────────┘

  ┌──────────────────────┐    1   * ┌──────────────────────┐
  │ FeedConsumptionLogs  │──────────│  ConsumptionDetails  │
  │(Id, ShedId, LogDate) │          │(LogId, IngredId, Qty)│
  └──────────────────────┘          └──────────────────────┘

══════════════════════════════ HEALTH SCHEMA ════════════════════════════════════

  ┌────────────────────┐    1   * ┌──────────────────────────┐
  │VaccinationProtocols│──────────│ ProtocolScheduleItems     │
  │(Id, TenantId)      │          │(Id, ProtocolId, SeqNum)  │
  └────────────────────┘          └──────────────────────────┘
            │ * (assigned to)                │ *
            ▼                               ▼
  ┌──────────────────────────────────────────────────────────┐
  │             AnimalVaccinationSchedules                    │
  │ (AnimalId, ScheduleItemId, DueDate, Status)              │
  └──────────────────────────────────────────────────────────┘

  Animals ──< VaccinationRecords
  Animals ──< TreatmentRecords ──── InventoryItems (medicine)
  Animals ──< DiseaseIncidentAnimals >── DiseaseIncidents
  Animals ──── MortalityRecords

══════════════════════════════ INVENTORY SCHEMA ═════════════════════════════════

  ┌─────────────┐    *      1 ┌──────────────────┐    *    1 ┌──────────────┐
  │  StockBatches│────────────►│  InventoryItems  │◄──────────│StockTransact │
  │(Id, ItemId) │             │(Id, TenantId)    │           │(Id, ItemId)  │
  └──────┬──────┘             └──────────────────┘           └──────────────┘
         │
         │ optional
         ▼
  ┌──────────────┐
  │  Suppliers   │
  │(Id, TenantId)│
  └──────────────┘

═══════════════════════════════ FINANCE SCHEMA ══════════════════════════════════

  ┌─────────────────┐    *      1 ┌──────────────────┐
  │ FinancialEntries│────────────►│ ChartOfAccounts  │
  │(Id, TenantId)   │             │(Id, global ref)  │
  └─────────────────┘             └──────────────────┘
         │ optional refs
         ├──► Animals.Id
         ├──► AnimalBatches.Id
         └──► Sheds.Id

  Animals ──< AnimalCostLedgers
  AnimalBatches ──── BatchProfitLoss (calculated)
  Tenants ──< LoanRecords ──< LoanRepayments
```

---

## 10. Database Schema — Catalog

### 10.1 Schema Overview

| Schema | Purpose | Owner |
|---|---|---|
| `platform` | Multi-tenant core: tenants, users, roles, farms, sheds, pens, subscriptions, lookup enums | Platform Team |
| `livestock` | Animal lifecycle management | Livestock Team |
| `feeding` | Feed ingredients, formulas, consumption | Feeding Team |
| `health` | Veterinary health, vaccination, mortality | Health Team |
| `inventory` | Stock management, suppliers | Inventory Team |
| `finance` | Financial entries, P&L, cost ledgers, loans | Finance Team |
| `audit` | Immutable audit logs, notifications | Platform Team |
| `hangfire` | Hangfire background job tables (managed by library) | Platform Team |

---

## 11. Table Design — Platform Schema

### 11.1 `platform.Tenants`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK, DEFAULT NEWSEQUENTIALID()
Name                     NVARCHAR(200)           NOT NULL
Slug                     NVARCHAR(100)           NOT NULL, UNIQUE
SubscriptionTier         TINYINT                 NOT NULL, DEFAULT 1
Status                   TINYINT                 NOT NULL, DEFAULT 0
                                                 -- 0=Active,1=Suspended,2=Deleted
TimeZone                 NVARCHAR(100)           NOT NULL, DEFAULT 'Asia/Dhaka'
DefaultLanguage          CHAR(5)                 NOT NULL, DEFAULT 'bn-BD'
DataRegion               NVARCHAR(50)            NOT NULL, DEFAULT 'ap-south-1'
MaxFarms                 INT                     NOT NULL, DEFAULT 1
MaxAnimals               INT                     NOT NULL, DEFAULT 10
MaxUsers                 INT                     NOT NULL, DEFAULT 1
-- Audit columns (standard)
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
CreatedByUserId          UNIQUEIDENTIFIER        NULL
UpdatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
UpdatedByUserId          UNIQUEIDENTIFIER        NULL
IsDeleted                BIT                     NOT NULL, DEFAULT 0
DeletedAt                DATETIME2(7)            NULL
DeletedByUserId          UNIQUEIDENTIFIER        NULL
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_Tenants: CLUSTERED on Id
  UQ_Tenants_Slug: UNIQUE NONCLUSTERED on Slug
  IX_Tenants_Status: NONCLUSTERED on Status (for background job queries across tenants)
```

---

### 11.2 `platform.Organizations`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL, FK → platform.Tenants(Id)
Name                     NVARCHAR(200)           NOT NULL
Phone                    NVARCHAR(20)            NOT NULL
Email                    NVARCHAR(320)           NOT NULL
FarmType                 TINYINT                 NOT NULL  -- 0=CattleBeef,1=Dairy,2=Goat,3=Mixed
AddressLine1             NVARCHAR(200)           NULL
AddressLine2             NVARCHAR(200)           NULL
Upazila                  NVARCHAR(100)           NULL
District                 NVARCHAR(100)           NOT NULL
Division                 NVARCHAR(100)           NOT NULL
PostalCode               NVARCHAR(10)            NULL
LogoUrl                  NVARCHAR(500)           NULL
-- Audit columns...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_Organizations
  IX_Organizations_TenantId: NONCLUSTERED on TenantId

CONSTRAINTS:
  FK_Organizations_Tenants: TenantId → Tenants(Id)
```

---

### 11.3 `platform.Users`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
Phone                    NVARCHAR(20)            NOT NULL
PhoneVerified            BIT                     NOT NULL, DEFAULT 0
Email                    NVARCHAR(320)           NULL
EmailVerified            BIT                     NOT NULL, DEFAULT 0
FullName                 NVARCHAR(200)           NOT NULL
PasswordHash             NVARCHAR(500)           NULL
TokenVersion             INT                     NOT NULL, DEFAULT 1
  -- Increment to invalidate all tokens
PreferredLanguage        CHAR(5)                 NOT NULL, DEFAULT 'bn-BD'
PreferredTheme           TINYINT                 NOT NULL, DEFAULT 0  -- 0=Light,1=Dark
LastLoginAt              DATETIME2(7)            NULL
LastLoginIp              NVARCHAR(50)            NULL
IsGlobalAdmin            BIT                     NOT NULL, DEFAULT 0
-- Audit columns...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_Users
  UQ_Users_Phone: UNIQUE NONCLUSTERED on Phone (partial: WHERE IsDeleted = 0)
  IX_Users_Email: NONCLUSTERED on Email (where Email IS NOT NULL)

NOTE: Users are GLOBAL — not tenant-scoped. One user can belong to multiple orgs.
No TenantId on this table. Tenant membership via OrganizationUsers.
```

---

### 11.4 `platform.OrganizationUsers`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL, FK → Tenants(Id)
OrganizationId           UNIQUEIDENTIFIER        NOT NULL, FK → Organizations(Id)
UserId                   UNIQUEIDENTIFIER        NOT NULL, FK → Users(Id)
Role                     TINYINT                 NOT NULL
  -- 0=Owner,1=FarmManager,2=Veterinarian,3=Worker,4=Accountant,5=Viewer
IsActive                 BIT                     NOT NULL, DEFAULT 1
AssignedFarmIds          NVARCHAR(MAX)           NULL
  -- JSON array of Farm GUIDs this user can access; NULL = all farms
JoinedAt                 DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
-- Audit columns...

INDEXES:
  PK_OrganizationUsers
  UQ_OrgUsers_OrgId_UserId: UNIQUE on (OrganizationId, UserId) WHERE IsDeleted=0
  IX_OrgUsers_TenantId: NONCLUSTERED on TenantId
  IX_OrgUsers_UserId: NONCLUSTERED on UserId

CONSTRAINTS:
  CK_OrgUsers_Role: Role BETWEEN 0 AND 5
  Only one Owner allowed per Organization (enforced at application level with a filtered index)
  UQ_OrgUsers_OneOwner: UNIQUE FILTERED on (OrganizationId, Role) WHERE Role = 0 AND IsDeleted = 0
```

---

### 11.5 `platform.UserInvitations`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL, FK → Tenants(Id)
OrganizationId           UNIQUEIDENTIFIER        NOT NULL, FK → Organizations(Id)
InvitedByUserId          UNIQUEIDENTIFIER        NOT NULL, FK → Users(Id)
InviteePhone             NVARCHAR(20)            NULL
InviteeEmail             NVARCHAR(320)           NULL
Role                     TINYINT                 NOT NULL
TokenHash                NVARCHAR(500)           NOT NULL  -- hashed invitation token
ExpiresAt                DATETIME2(7)            NOT NULL
Status                   TINYINT                 NOT NULL, DEFAULT 0
  -- 0=Pending, 1=Accepted, 2=Expired, 3=Revoked
AcceptedAt               DATETIME2(7)            NULL
AcceptedByUserId         UNIQUEIDENTIFIER        NULL, FK → Users(Id)
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()

INDEXES:
  PK_UserInvitations
  IX_Invitations_TenantId_Status: NONCLUSTERED on (TenantId, Status)
  IX_Invitations_ExpiresAt: NONCLUSTERED on ExpiresAt (for background cleanup job)
```

---

### 11.6 `platform.SubscriptionPlans`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
Tier                     TINYINT                 NOT NULL, UNIQUE
  -- 0=Bittho,1=Khamar,2=Banik,3=Corporation,4=NGO
NameEn                   NVARCHAR(100)           NOT NULL
NameBn                   NVARCHAR(100)           NOT NULL
MonthlyPriceBDT          DECIMAL(10,2)           NOT NULL
AnnualPriceBDT           DECIMAL(10,2)           NOT NULL
MaxFarms                 INT                     NOT NULL
MaxAnimals               INT                     NOT NULL  -- -1 = unlimited
MaxUsers                 INT                     NOT NULL  -- -1 = unlimited
Features                 NVARCHAR(MAX)           NOT NULL  -- JSON array of feature flags
IsActive                 BIT                     NOT NULL, DEFAULT 1
-- Audit columns...
```

---

### 11.7 `platform.Subscriptions`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL, UNIQUE, FK → Tenants(Id)
PlanId                   UNIQUEIDENTIFIER        NOT NULL, FK → SubscriptionPlans(Id)
Status                   TINYINT                 NOT NULL, DEFAULT 0
  -- 0=Active, 1=GracePeriod, 2=Suspended, 3=Cancelled
BillingCycle             TINYINT                 NOT NULL  -- 0=Monthly, 1=Annual
CurrentPeriodStart       DATE                    NOT NULL
CurrentPeriodEnd         DATE                    NOT NULL
GracePeriodEndsAt        DATETIME2(7)            NULL
CancelledAt              DATETIME2(7)            NULL
PaymentMethod            TINYINT                 NOT NULL  -- 0=bKash,1=Nagad,2=Card,3=Rocket
-- Audit columns...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_Subscriptions
  UQ_Subscriptions_TenantId: UNIQUE on TenantId
  IX_Subscriptions_Status_PeriodEnd: NONCLUSTERED on (Status, CurrentPeriodEnd)
    -- Used by expiry reminder background job
```

---

### 11.8 `platform.BillingRecords`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL, FK → Tenants(Id)
SubscriptionId           UNIQUEIDENTIFIER        NOT NULL, FK → Subscriptions(Id)
AmountBDT                DECIMAL(10,2)           NOT NULL
Status                   TINYINT                 NOT NULL  -- 0=Pending,1=Paid,2=Failed,3=Refunded
PaymentMethod            TINYINT                 NOT NULL
PaymentGatewayTxId       NVARCHAR(200)           NULL
PeriodStart              DATE                    NOT NULL
PeriodEnd                DATE                    NOT NULL
PaidAt                   DATETIME2(7)            NULL
FailureReason            NVARCHAR(500)           NULL
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()

INDEXES:
  PK_BillingRecords
  IX_Billing_TenantId_Status: NONCLUSTERED on (TenantId, Status)
```

---

### 11.9 `platform.Farms`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL, FK → Tenants(Id)
OrganizationId           UNIQUEIDENTIFIER        NOT NULL, FK → Organizations(Id)
Name                     NVARCHAR(200)           NOT NULL
AddressLine1             NVARCHAR(200)           NULL
Upazila                  NVARCHAR(100)           NULL
District                 NVARCHAR(100)           NOT NULL
Division                 NVARCHAR(100)           NOT NULL
TotalAreaAcres           DECIMAL(10,3)           NULL
IsActive                 BIT                     NOT NULL, DEFAULT 1
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_Farms
  IX_Farms_TenantId: NONCLUSTERED on TenantId
  IX_Farms_OrgId: NONCLUSTERED on OrganizationId

CONSTRAINTS:
  FK_Farms_Tenants, FK_Farms_Organizations
```

---

### 11.10 `platform.Sheds`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL, FK → Tenants(Id)
FarmId                   UNIQUEIDENTIFIER        NOT NULL, FK → Farms(Id)
Name                     NVARCHAR(100)           NOT NULL
ShedType                 NVARCHAR(50)            NULL  -- 'Cattle', 'Dairy', 'Goat'
CapacityAnimals          INT                     NULL
Description              NVARCHAR(500)           NULL
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_Sheds
  IX_Sheds_TenantId_FarmId: NONCLUSTERED on (TenantId, FarmId)
  IX_Sheds_FarmId: NONCLUSTERED on FarmId

CONSTRAINTS:
  FK_Sheds_Farms, FK_Sheds_Tenants
  UQ_Sheds_FarmId_Name: UNIQUE on (FarmId, Name) WHERE IsDeleted=0
```

---

### 11.11 `platform.Pens`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL, FK → Tenants(Id)
ShedId                   UNIQUEIDENTIFIER        NOT NULL, FK → Sheds(Id)
Name                     NVARCHAR(100)           NOT NULL
CapacityAnimals          INT                     NULL
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_Pens
  IX_Pens_TenantId_ShedId: NONCLUSTERED on (TenantId, ShedId)

CONSTRAINTS:
  FK_Pens_Sheds, FK_Pens_Tenants
  UQ_Pens_ShedId_Name: UNIQUE on (ShedId, Name) WHERE IsDeleted=0
```

---

## 12. Table Design — Livestock Schema

### 12.1 `livestock.Animals` ← Core Aggregate Root

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL, FK → platform.Tenants(Id)
FarmId                   UNIQUEIDENTIFIER        NOT NULL, FK → platform.Farms(Id)
CurrentShedId            UNIQUEIDENTIFIER        NULL, FK → platform.Sheds(Id)
CurrentPenId             UNIQUEIDENTIFIER        NULL, FK → platform.Pens(Id)
-- Animal Identity
TagId                    NVARCHAR(50)            NOT NULL
TagType                  TINYINT                 NOT NULL, DEFAULT 0
  -- 0=Manual,1=EarTag,2=RFID
EarTagNumber             NVARCHAR(50)            NULL
RfidNumber               NVARCHAR(100)           NULL
Name                     NVARCHAR(100)           NULL  -- optional name
-- Classification
Species                  TINYINT                 NOT NULL
  -- 0=CattleBeef,1=CattleDairy,2=Goat,3=Sheep
BreedId                  UNIQUEIDENTIFIER        NULL, FK → platform.EnumAnimalBreeds(Id)
BreedName                NVARCHAR(100)           NOT NULL  -- denormalized for resilience
Sex                      TINYINT                 NOT NULL  -- 0=Male,1=Female
-- Key dates
DateOfBirth              DATE                    NULL  -- nullable if only est. age known
EstimatedDateOfBirth     BIT                     NOT NULL, DEFAULT 0
AcquisitionDate          DATE                    NOT NULL
AcquisitionType          TINYINT                 NOT NULL  -- 0=Purchased,1=BornOnFarm
AcquisitionPrice         DECIMAL(18,4)           NULL
AcquisitionCurrency      CHAR(3)                 NOT NULL, DEFAULT 'BDT'
SourceDescription        NVARCHAR(200)           NULL  -- seller/origin description
-- Parentage (self-referencing)
DamId                    UNIQUEIDENTIFIER        NULL, FK → livestock.Animals(Id)
SireId                   UNIQUEIDENTIFIER        NULL, FK → livestock.Animals(Id)
-- Current status (denormalized for fast querying)
Status                   TINYINT                 NOT NULL, DEFAULT 0
  -- 0=Active,1=Sold,2=Slaughtered,3=Dead,4=Quarantined,5=Transferred
DisposalDate             DATE                    NULL
DisposalReason           TINYINT                 NULL
-- Denormalized for dashboard performance (updated via domain events)
LatestWeightKg           DECIMAL(8,3)            NULL
LatestWeightDate         DATE                    NULL
AdgKgPerDay              DECIMAL(6,4)            NULL  -- updated after each weight entry
BcsValue                 DECIMAL(3,1)            NULL
  -- CK: BcsValue BETWEEN 1.0 AND 5.0
QuarantineStartDate      DATE                    NULL
QuarantineReason         NVARCHAR(500)           NULL
-- Milk (for dairy animals)
LastCalvingDate          DATE                    NULL
IsInMilk                 BIT                     NOT NULL, DEFAULT 0
MilkWithdrawalEndDate    DATE                    NULL  -- for antibiotic treatment
-- Audit + soft delete + row version
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
CreatedByUserId          UNIQUEIDENTIFIER        NOT NULL
UpdatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
UpdatedByUserId          UNIQUEIDENTIFIER        NOT NULL
IsDeleted                BIT                     NOT NULL, DEFAULT 0
DeletedAt                DATETIME2(7)            NULL
DeletedByUserId          UNIQUEIDENTIFIER        NULL
RowVersion               ROWVERSION              NOT NULL

PRIMARY KEY: CLUSTERED on Id

INDEXES:
  IX_Animals_TenantId_Status: NONCLUSTERED on (TenantId, Status)
    INCLUDE (TagId, Species, Breed, CurrentShedId, LatestWeightKg)
    -- Dashboard herd summary and filter queries
  IX_Animals_TenantId_FarmId: NONCLUSTERED on (TenantId, FarmId)
  IX_Animals_TenantId_ShedId: NONCLUSTERED on (TenantId, CurrentShedId)
    WHERE CurrentShedId IS NOT NULL
  IX_Animals_TagId_TenantId: NONCLUSTERED on (TenantId, TagId)
    WHERE IsDeleted = 0
    -- Uniqueness enforcement + fast tag lookup
  IX_Animals_DamId: NONCLUSTERED on DamId WHERE DamId IS NOT NULL
  IX_Animals_SireId: NONCLUSTERED on SireId WHERE SireId IS NOT NULL
  IX_Animals_Status_DisposalDate: NONCLUSTERED on (Status, DisposalDate)
    WHERE Status IN (1,2,3)  -- sold/slaughtered/dead filter
  IX_Animals_Species_Breed: NONCLUSTERED on (TenantId, Species, BreedId)
    -- Herd composition charts

UNIQUE CONSTRAINT:
  UQ_Animals_TenantId_TagId: UNIQUE FILTERED on (TenantId, TagId)
    WHERE IsDeleted = 0
    -- Prevents duplicate tags within same tenant; allows reuse after soft delete

CHECK CONSTRAINTS:
  CK_Animals_Sex: Sex IN (0, 1)
  CK_Animals_Status: Status BETWEEN 0 AND 5
  CK_Animals_BCS: BcsValue IS NULL OR (BcsValue BETWEEN 1.0 AND 5.0)
  CK_Animals_Species: Species BETWEEN 0 AND 10
  CK_Animals_AcquisitionDate: AcquisitionDate <= CAST(GETUTCDATE() AS DATE)
```

---

### 12.2 `livestock.WeightRecords`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL, FK → Tenants(Id)
AnimalId                 UNIQUEIDENTIFIER        NOT NULL, FK → Animals(Id)
WeightDate               DATE                    NOT NULL
WeightKg                 DECIMAL(8,3)            NOT NULL
WeightUnit               TINYINT                 NOT NULL, DEFAULT 0  -- 0=KG
BcsValue                 DECIMAL(3,1)            NULL
Notes                    NVARCHAR(500)           NULL
RecordedByUserId         UNIQUEIDENTIFIER        NOT NULL, FK → Users(Id)
-- Audit + soft delete...
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
IsDeleted                BIT                     NOT NULL, DEFAULT 0
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_WeightRecords
  IX_WeightRecords_AnimalId_Date: NONCLUSTERED on (AnimalId, WeightDate DESC)
    -- ADG calculation needs last 2 records; dashboard needs latest
  IX_WeightRecords_TenantId_Date: NONCLUSTERED on (TenantId, WeightDate)
    -- Batch weight trend reports

CONSTRAINTS:
  FK_WeightRecords_Animals
  CK_WeightRecords_Weight: WeightKg > 0 AND WeightKg < 5000
  CK_WeightRecords_Date: WeightDate <= CAST(GETUTCDATE() AS DATE)
```

---

### 12.3 `livestock.BreedingRecords`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
DamId                    UNIQUEIDENTIFIER        NOT NULL, FK → Animals(Id)
SireId                   UNIQUEIDENTIFIER        NULL, FK → Animals(Id)
SireDescription          NVARCHAR(200)           NULL  -- if sire not in system
MatingDate               DATE                    NOT NULL
MatingMethod             TINYINT                 NOT NULL  -- 0=Natural,1=AI
PregnancyConfirmed       BIT                     NOT NULL, DEFAULT 0
PregnancyConfirmDate     DATE                    NULL
ExpectedCalvingDate      DATE                    NULL  -- computed from mating + gestation
ActualCalvingDate        DATE                    NULL
CalvingOutcome           TINYINT                 NULL  -- 0=Live,1=Stillborn,2=Abortion
CalfId                   UNIQUEIDENTIFIER        NULL, FK → Animals(Id)  -- born calf
Notes                    NVARCHAR(1000)          NULL
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_BreedingRecords
  IX_Breeding_DamId_Date: NONCLUSTERED on (DamId, MatingDate DESC)
  IX_Breeding_TenantId_ExpectedCalving: NONCLUSTERED on (TenantId, ExpectedCalvingDate)
    WHERE ExpectedCalvingDate IS NOT NULL AND ActualCalvingDate IS NULL
    -- Upcoming calvings reminder
  IX_Breeding_CalfId: NONCLUSTERED on CalfId WHERE CalfId IS NOT NULL

CONSTRAINTS:
  CK_Breeding_DamNotSelf: DamId <> SireId
  CK_Breeding_PregnancyDate: PregnancyConfirmDate IS NULL OR PregnancyConfirmDate >= MatingDate
  CK_Breeding_CalvingDate: ActualCalvingDate IS NULL OR ActualCalvingDate >= MatingDate
```

---

### 12.4 `livestock.AnimalBatches`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL, FK → Tenants(Id)
FarmId                   UNIQUEIDENTIFIER        NOT NULL, FK → Farms(Id)
Name                     NVARCHAR(200)           NOT NULL
BatchType                TINYINT                 NOT NULL  -- 0=Fattening,1=Dairy,2=Breeding,3=General
TargetSaleDate           DATE                    NULL
Status                   TINYINT                 NOT NULL, DEFAULT 0
  -- 0=Active, 1=Completed, 2=Cancelled
StartDate                DATE                    NOT NULL
EndDate                  DATE                    NULL
-- Denormalized for dashboard
AnimalCount              INT                     NOT NULL, DEFAULT 0  -- maintained by domain event
TotalFeedCostBDT         DECIMAL(18,4)           NOT NULL, DEFAULT 0
TotalRevenueBDT          DECIMAL(18,4)           NOT NULL, DEFAULT 0
TotalCostBDT             DECIMAL(18,4)           NOT NULL, DEFAULT 0
Notes                    NVARCHAR(1000)          NULL
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_AnimalBatches
  IX_Batches_TenantId_Status: NONCLUSTERED on (TenantId, Status)
  IX_Batches_FarmId: NONCLUSTERED on FarmId
```

---

### 12.5 `livestock.AnimalBatchMembers`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
BatchId                  UNIQUEIDENTIFIER        NOT NULL, FK → AnimalBatches(Id)
AnimalId                 UNIQUEIDENTIFIER        NOT NULL, FK → Animals(Id)
JoinedDate               DATE                    NOT NULL
LeftDate                 DATE                    NULL
LeftReason               TINYINT                 NULL  -- 0=Sold,1=Dead,2=Transferred,3=Removed
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()

INDEXES:
  PK_AnimalBatchMembers
  UQ_BatchMembers_BatchId_AnimalId: UNIQUE FILTERED on (BatchId, AnimalId)
    WHERE LeftDate IS NULL  -- active member uniqueness
  IX_BatchMembers_AnimalId: NONCLUSTERED on AnimalId
  IX_BatchMembers_TenantId_BatchId: NONCLUSTERED on (TenantId, BatchId)
```

---

### 12.6 `livestock.AnimalTransferLogs`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
AnimalId                 UNIQUEIDENTIFIER        NOT NULL, FK → Animals(Id)
TransferType             TINYINT                 NOT NULL  -- 0=ShedTransfer,1=FarmTransfer
FromFarmId               UNIQUEIDENTIFIER        NULL
FromShedId               UNIQUEIDENTIFIER        NULL
FromPenId                UNIQUEIDENTIFIER        NULL
ToFarmId                 UNIQUEIDENTIFIER        NULL
ToShedId                 UNIQUEIDENTIFIER        NULL
ToPenId                  UNIQUEIDENTIFIER        NULL
TransferDate             DATE                    NOT NULL
TransferredByUserId      UNIQUEIDENTIFIER        NOT NULL
Reason                   NVARCHAR(500)           NULL
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()

INDEXES:
  PK_AnimalTransferLogs
  IX_Transfers_AnimalId: NONCLUSTERED on (AnimalId, TransferDate DESC)
  IX_Transfers_TenantId_Date: NONCLUSTERED on (TenantId, TransferDate)
```

---

### 12.7 `livestock.AnimalPhotos`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
AnimalId                 UNIQUEIDENTIFIER        NOT NULL, FK → Animals(Id)
BlobUrl                  NVARCHAR(1000)          NOT NULL  -- S3 URL
ThumbnailUrl             NVARCHAR(1000)          NULL
DisplayOrder             TINYINT                 NOT NULL, DEFAULT 0
Caption                  NVARCHAR(200)           NULL
FileSizeBytes            INT                     NULL
UploadedAt               DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
UploadedByUserId         UNIQUEIDENTIFIER        NOT NULL
IsDeleted                BIT                     NOT NULL, DEFAULT 0

INDEXES:
  PK_AnimalPhotos
  IX_Photos_AnimalId: NONCLUSTERED on (AnimalId, DisplayOrder) WHERE IsDeleted=0

CONSTRAINTS:
  CK_Photos_MaxPerAnimal enforced at application layer (max 5 per animal)
```

---

## 13. Table Design — Feeding Schema

### 13.1 `feeding.FeedIngredients`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL  -- NULL = system/global ingredient
IsSystemIngredient       BIT                     NOT NULL, DEFAULT 0
  -- System ingredients pre-loaded; tenant cannot delete them
NameEn                   NVARCHAR(200)           NOT NULL
NameBn                   NVARCHAR(200)           NOT NULL
Category                 TINYINT                 NOT NULL  -- 0=Roughage,1=Concentrate,2=Mineral,3=Vitamin,4=Other
UnitOfMeasure            NVARCHAR(20)            NOT NULL  -- 'KG','L','g'
-- Nutritional profile (Value Object)
DryMatterPct             DECIMAL(5,2)            NULL
CrudeProteinPct          DECIMAL(5,2)            NULL
MetabEnergy              DECIMAL(8,4)            NULL  -- MJ/kg DM
CrudeFibrePct            DECIMAL(5,2)            NULL
-- Pricing (latest price for cost calculation)
CurrentCostPerUnit       DECIMAL(18,4)           NULL
CostCurrency             CHAR(3)                 NOT NULL, DEFAULT 'BDT'
IsActive                 BIT                     NOT NULL, DEFAULT 1
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_FeedIngredients
  IX_Ingredients_TenantId_Active: NONCLUSTERED on (TenantId, IsActive)
    INCLUDE (NameEn, NameBn, UnitOfMeasure, CurrentCostPerUnit)
  IX_Ingredients_System: NONCLUSTERED on IsSystemIngredient WHERE IsSystemIngredient=1

NOTE: TenantId for system ingredients = well-known system GUID constant.
EF Core Global Query Filter: WHERE TenantId = @currentTenantId OR IsSystemIngredient = 1
```

---

### 13.2 `feeding.FeedFormulas`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
Name                     NVARCHAR(200)           NOT NULL
TargetSpecies            TINYINT                 NOT NULL
TargetAnimalStage        TINYINT                 NULL  -- 0=Calf,1=Grower,2=Finisher,3=Lactating
-- Computed nutritional profile (Value Object — denormalized for display)
ComputedDmPct            DECIMAL(5,2)            NULL
ComputedCpPct            DECIMAL(5,2)            NULL
ComputedMePct            DECIMAL(8,4)            NULL
TotalDailyQtyKg          DECIMAL(8,3)            NULL  -- total kg per animal per day
-- Cost
EstimatedDailyCostBDT    DECIMAL(10,4)           NULL  -- computed at formula save
IsActive                 BIT                     NOT NULL, DEFAULT 1
Notes                    NVARCHAR(1000)          NULL
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_FeedFormulas
  IX_Formulas_TenantId_Active: NONCLUSTERED on (TenantId, IsActive)
  IX_Formulas_Species: NONCLUSTERED on (TenantId, TargetSpecies)
```

---

### 13.3 `feeding.FormulaIngredients`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
FormulaId                UNIQUEIDENTIFIER        NOT NULL, FK → FeedFormulas(Id)
IngredientId             UNIQUEIDENTIFIER        NOT NULL, FK → FeedIngredients(Id)
QuantityKg               DECIMAL(8,3)            NOT NULL  -- per animal per day
Sequence                 INT                     NOT NULL, DEFAULT 0

INDEXES:
  PK_FormulaIngredients
  UQ_FormulaIngredients: UNIQUE on (FormulaId, IngredientId)
  IX_FormIngr_FormulaId: NONCLUSTERED on FormulaId

CONSTRAINTS:
  FK_FormulaIngredients_Formulas
  FK_FormulaIngredients_Ingredients
  CK_FormIngr_Qty: QuantityKg > 0
```

---

### 13.4 `feeding.FeedingSchedules`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
FormulaId                UNIQUEIDENTIFIER        NOT NULL, FK → FeedFormulas(Id)
AssignmentType           TINYINT                 NOT NULL  -- 0=Shed,1=Pen,2=Batch
ShedId                   UNIQUEIDENTIFIER        NULL, FK → platform.Sheds(Id)
PenId                    UNIQUEIDENTIFIER        NULL, FK → platform.Pens(Id)
BatchId                  UNIQUEIDENTIFIER        NULL, FK → livestock.AnimalBatches(Id)
StartDate                DATE                    NOT NULL
EndDate                  DATE                    NULL  -- NULL = active indefinitely
AnimalCount              INT                     NULL  -- at assignment time; for qty calculation
IsActive                 BIT                     NOT NULL, DEFAULT 1
-- Audit...
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
CreatedByUserId          UNIQUEIDENTIFIER        NOT NULL
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_FeedingSchedules
  IX_Schedules_TenantId_Active: NONCLUSTERED on (TenantId, IsActive)
  IX_Schedules_ShedId_Active: NONCLUSTERED on (ShedId, IsActive) WHERE ShedId IS NOT NULL
  IX_Schedules_BatchId: NONCLUSTERED on BatchId WHERE BatchId IS NOT NULL

CONSTRAINTS:
  CK_Schedules_Assignment: only one of ShedId/PenId/BatchId is NOT NULL
    (enforced via check constraint on CASE expression)
```

---

### 13.5 `feeding.FeedConsumptionLogs`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
ShedId                   UNIQUEIDENTIFIER        NOT NULL, FK → platform.Sheds(Id)
LogDate                  DATE                    NOT NULL
ScheduleId               UNIQUEIDENTIFIER        NULL, FK → FeedingSchedules(Id)
AnimalCount              INT                     NOT NULL  -- animals present that day
TotalFeedKg              DECIMAL(10,3)           NOT NULL
TotalCostBDT             DECIMAL(18,4)           NOT NULL  -- computed
FeedCostPerAnimalBDT     DECIMAL(18,4)           NOT NULL  -- computed
InventoryDeducted        BIT                     NOT NULL, DEFAULT 0
FinancePosted            BIT                     NOT NULL, DEFAULT 0
Notes                    NVARCHAR(500)           NULL
LoggedByUserId           UNIQUEIDENTIFIER        NOT NULL
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()

INDEXES:
  PK_FeedConsumptionLogs
  UQ_Consumption_Shed_Date: UNIQUE on (ShedId, LogDate)
    -- One log per shed per day
  IX_Consumption_TenantId_Date: NONCLUSTERED on (TenantId, LogDate DESC)
  IX_Consumption_ShedId_Date: NONCLUSTERED on (ShedId, LogDate DESC)
  IX_Consumption_InventoryDeducted: NONCLUSTERED on InventoryDeducted
    WHERE InventoryDeducted = 0  -- for background job to process pending deductions
```

---

### 13.6 `feeding.ConsumptionDetails`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
LogId                    UNIQUEIDENTIFIER        NOT NULL, FK → FeedConsumptionLogs(Id)
IngredientId             UNIQUEIDENTIFIER        NOT NULL, FK → FeedIngredients(Id)
QuantityKg               DECIMAL(10,3)           NOT NULL
CostPerKgBDT             DECIMAL(10,4)           NOT NULL  -- price at time of logging
TotalCostBDT             DECIMAL(18,4)           NOT NULL  -- computed

INDEXES:
  PK_ConsumptionDetails
  IX_ConsumptionDetails_LogId: NONCLUSTERED on LogId
  IX_ConsumptionDetails_IngredientId: NONCLUSTERED on IngredientId
    -- Inventory deduction lookup

CONSTRAINTS:
  FK_ConsumptionDetails_Logs
  FK_ConsumptionDetails_Ingredients
  CK_ConsumptionDetails_Qty: QuantityKg > 0
```

---

## 14. Table Design — Health Schema

### 14.1 `health.VaccinationProtocols`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
IsSystemProtocol         BIT                     NOT NULL, DEFAULT 0
NameEn                   NVARCHAR(200)           NOT NULL
NameBn                   NVARCHAR(200)           NULL
TargetSpecies            TINYINT                 NOT NULL
TargetAgeMinDays         INT                     NULL
Description              NVARCHAR(1000)          NULL
IsActive                 BIT                     NOT NULL, DEFAULT 1
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_VaccinationProtocols
  IX_Protocols_TenantId_Species: NONCLUSTERED on (TenantId, TargetSpecies, IsActive)
```

---

### 14.2 `health.ProtocolScheduleItems`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
ProtocolId               UNIQUEIDENTIFIER        NOT NULL, FK → VaccinationProtocols(Id)
SequenceNumber           INT                     NOT NULL  -- 1=initial, 2=booster1, etc.
VaccineName              NVARCHAR(200)           NOT NULL
VaccineNameBn            NVARCHAR(200)           NULL
DoseDescription          NVARCHAR(200)           NOT NULL  -- e.g., "2ml subcutaneous"
RouteOfAdministration    NVARCHAR(100)           NULL
-- Timing
DaysFromBirth            INT                     NULL  -- absolute (for initial dose)
DaysFromPreviousDose     INT                     NULL  -- relative (for boosters)
IntervalType             TINYINT                 NOT NULL  -- 0=FromBirth,1=FromPrevious,2=Annual

INDEXES:
  PK_ProtocolScheduleItems
  IX_ScheduleItems_ProtocolId: NONCLUSTERED on (ProtocolId, SequenceNumber)
```

---

### 14.3 `health.AnimalVaccinationSchedules`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
AnimalId                 UNIQUEIDENTIFIER        NOT NULL, FK → livestock.Animals(Id)
ScheduleItemId           UNIQUEIDENTIFIER        NOT NULL, FK → ProtocolScheduleItems(Id)
DueDate                  DATE                    NOT NULL
Status                   TINYINT                 NOT NULL, DEFAULT 0
  -- 0=Scheduled, 1=Due, 2=Overdue, 3=Completed, 4=Skipped
CompletedDate            DATE                    NULL
VaccinationRecordId      UNIQUEIDENTIFIER        NULL, FK → VaccinationRecords(Id)
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()

INDEXES:
  PK_AnimalVaxSchedules
  IX_VaxSchedule_TenantId_Status_Due: NONCLUSTERED on (TenantId, Status, DueDate)
    INCLUDE (AnimalId, ScheduleItemId)
    -- Critical: vaccination reminder background job
  IX_VaxSchedule_AnimalId: NONCLUSTERED on (AnimalId, DueDate)
  IX_VaxSchedule_DueDate: NONCLUSTERED on DueDate
    WHERE Status IN (0, 1, 2)  -- upcoming and overdue; completed excluded
```

---

### 14.4 `health.VaccinationRecords`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
AnimalId                 UNIQUEIDENTIFIER        NOT NULL, FK → livestock.Animals(Id)
ScheduleId               UNIQUEIDENTIFIER        NULL, FK → AnimalVaccinationSchedules(Id)
VaccineName              NVARCHAR(200)           NOT NULL
VaccineBatchNumber       NVARCHAR(100)           NULL
DateGiven                DATE                    NOT NULL
DoseGiven                NVARCHAR(100)           NOT NULL
RouteOfAdministration    NVARCHAR(100)           NULL
AdministeredByUserId     UNIQUEIDENTIFIER        NOT NULL
AdministeredByName       NVARCHAR(200)           NULL  -- vet name if external
NextDueDate              DATE                    NULL
Notes                    NVARCHAR(500)           NULL
CostBDT                  DECIMAL(10,2)           NULL
InventoryItemId          UNIQUEIDENTIFIER        NULL, FK → inventory.InventoryItems(Id)
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_VaccinationRecords
  IX_VaxRecords_AnimalId_Date: NONCLUSTERED on (AnimalId, DateGiven DESC)
  IX_VaxRecords_TenantId_Date: NONCLUSTERED on (TenantId, DateGiven DESC)

CONSTRAINTS:
  CK_VaxRecords_Date: DateGiven <= CAST(GETUTCDATE() AS DATE)
```

---

### 14.5 `health.TreatmentRecords`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
AnimalId                 UNIQUEIDENTIFIER        NOT NULL, FK → livestock.Animals(Id)
Diagnosis                NVARCHAR(500)           NOT NULL
TreatmentDate            DATE                    NOT NULL
DrugName                 NVARCHAR(200)           NOT NULL
DoseDescription          NVARCHAR(200)           NOT NULL
FrequencyPerDay          DECIMAL(4,2)            NULL  -- e.g., 2.0 = twice daily
DurationDays             INT                     NULL
RouteOfAdministration    NVARCHAR(100)           NULL
AdministeredByUserId     UNIQUEIDENTIFIER        NOT NULL
AdministeredByName       NVARCHAR(200)           NULL
FollowUpDate             DATE                    NULL
Outcome                  NVARCHAR(500)           NULL
CostBDT                  DECIMAL(10,2)           NULL
InventoryItemId          UNIQUEIDENTIFIER        NULL, FK → inventory.InventoryItems(Id)
QuantityUsed             DECIMAL(10,3)           NULL
MilkWithdrawalDays       INT                     NULL  -- for dairy antibiotics
FinancePosted            BIT                     NOT NULL, DEFAULT 0
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_TreatmentRecords
  IX_Treatment_AnimalId_Date: NONCLUSTERED on (AnimalId, TreatmentDate DESC)
  IX_Treatment_TenantId_Date: NONCLUSTERED on (TenantId, TreatmentDate DESC)
  IX_Treatment_FinancePosted: NONCLUSTERED on FinancePosted WHERE FinancePosted = 0
```

---

### 14.6 `health.DiseaseIncidents`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
FarmId                   UNIQUEIDENTIFIER        NOT NULL, FK → platform.Farms(Id)
DiseaseName              NVARCHAR(200)           NOT NULL
Symptoms                 NVARCHAR(1000)          NULL
Diagnosis                NVARCHAR(500)           NULL
IncidentDate             DATE                    NOT NULL
ResolvedDate             DATE                    NULL
AffectedAnimalCount      INT                     NOT NULL, DEFAULT 0
QuarantineApplied        BIT                     NOT NULL, DEFAULT 0
ReportedToVet            BIT                     NOT NULL, DEFAULT 0
ReportedToAuthorities    BIT                     NOT NULL, DEFAULT 0
TreatmentProtocol        NVARCHAR(2000)          NULL
Outcome                  NVARCHAR(500)           NULL
Notes                    NVARCHAR(2000)          NULL
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_DiseaseIncidents
  IX_Incidents_TenantId_Date: NONCLUSTERED on (TenantId, IncidentDate DESC)
  IX_Incidents_FarmId: NONCLUSTERED on FarmId
```

---

### 14.7 `health.DiseaseIncidentAnimals`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
IncidentId               UNIQUEIDENTIFIER        NOT NULL, FK → DiseaseIncidents(Id)
AnimalId                 UNIQUEIDENTIFIER        NOT NULL, FK → livestock.Animals(Id)
IsQuarantined            BIT                     NOT NULL, DEFAULT 0
Outcome                  TINYINT                 NULL  -- 0=Recovered,1=Dead,2=Ongoing
AddedAt                  DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()

INDEXES:
  PK_IncidentAnimals
  UQ_IncidentAnimals: UNIQUE on (IncidentId, AnimalId)
  IX_IncidentAnimals_AnimalId: NONCLUSTERED on AnimalId
```

---

### 14.8 `health.VetVisits`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
FarmId                   UNIQUEIDENTIFIER        NOT NULL
VetName                  NVARCHAR(200)           NOT NULL
VisitDate                DATE                    NOT NULL
VisitType                TINYINT                 NOT NULL -- 0=Routine,1=Emergency,2=Follow-up
Purpose                  NVARCHAR(500)           NULL
Findings                 NVARCHAR(2000)          NULL
Recommendations          NVARCHAR(2000)          NULL
CostBDT                  DECIMAL(10,2)           NULL
NextVisitDate            DATE                    NULL
CreatedAt                DATETIME2(7)            NOT NULL

INDEXES:
  PK_VetVisits
  IX_VetVisits_TenantId_Date: NONCLUSTERED on (TenantId, VisitDate DESC)
  IX_VetVisits_FarmId: NONCLUSTERED on FarmId
```

---

### 14.9 `health.MortalityRecords`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
AnimalId                 UNIQUEIDENTIFIER        NOT NULL, UNIQUE, FK → livestock.Animals(Id)
DeathDate                DATE                    NOT NULL
CauseOfDeath             TINYINT                 NOT NULL
  -- 0=Disease,1=Accident,2=NaturalCauses,3=Unknown,4=Slaughter
DiseaseName              NVARCHAR(200)           NULL
PostMortemNotes          NVARCHAR(2000)          NULL
EstimatedEconomicLossBDT DECIMAL(18,4)           NULL
DiseaseIncidentId        UNIQUEIDENTIFIER        NULL, FK → DiseaseIncidents(Id)
RecordedByUserId         UNIQUEIDENTIFIER        NOT NULL
CreatedAt                DATETIME2(7)            NOT NULL

INDEXES:
  PK_MortalityRecords
  UQ_Mortality_AnimalId: UNIQUE on AnimalId  -- one death per animal
  IX_Mortality_TenantId_Date: NONCLUSTERED on (TenantId, DeathDate DESC)
```

---

## 15. Table Design — Inventory Schema

### 15.1 `inventory.InventoryItems`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
FarmId                   UNIQUEIDENTIFIER        NOT NULL, FK → platform.Farms(Id)
Name                     NVARCHAR(200)           NOT NULL
Category                 TINYINT                 NOT NULL
  -- 0=Feed,1=Medicine,2=Chemical,3=Equipment,4=Other
UnitOfMeasure            NVARCHAR(20)            NOT NULL
ReorderThreshold         DECIMAL(10,3)           NOT NULL, DEFAULT 0
-- Real-time stock (maintained by domain events — denormalized)
CurrentStockQty          DECIMAL(10,3)           NOT NULL, DEFAULT 0
WeightedAverageCostBDT   DECIMAL(18,6)           NOT NULL, DEFAULT 0
  -- WAC recalculated on each stock-in
TotalInventoryValueBDT   DECIMAL(18,4)           NOT NULL, DEFAULT 0
  -- = CurrentStockQty * WeightedAverageCostBDT
LastStockInDate          DATE                    NULL
LastStockOutDate         DATE                    NULL
NearestExpiryDate        DATE                    NULL  -- maintained by background job
IsActive                 BIT                     NOT NULL, DEFAULT 1
-- Linked to FeedIngredient if category = Feed
FeedIngredientId         UNIQUEIDENTIFIER        NULL, FK → feeding.FeedIngredients(Id)
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_InventoryItems
  IX_Items_TenantId_FarmId: NONCLUSTERED on (TenantId, FarmId)
    INCLUDE (Name, Category, CurrentStockQty, ReorderThreshold)
    -- Real-time stock dashboard
  IX_Items_TenantId_LowStock: NONCLUSTERED on (TenantId, CurrentStockQty, ReorderThreshold)
    -- WHERE CurrentStockQty <= ReorderThreshold AND IsActive=1
    -- Low stock alerts
  IX_Items_NearestExpiry: NONCLUSTERED on (TenantId, NearestExpiryDate)
    WHERE NearestExpiryDate IS NOT NULL AND IsActive=1

CONSTRAINTS:
  CK_Items_Stock: CurrentStockQty >= 0
  CK_Items_WAC: WeightedAverageCostBDT >= 0
```

---

### 15.2 `inventory.Suppliers`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
Name                     NVARCHAR(200)           NOT NULL
Phone                    NVARCHAR(20)            NULL
Email                    NVARCHAR(320)           NULL
Address                  NVARCHAR(500)           NULL
Notes                    NVARCHAR(1000)          NULL
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_Suppliers
  IX_Suppliers_TenantId: NONCLUSTERED on TenantId WHERE IsDeleted=0
```

---

### 15.3 `inventory.StockBatches`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
ItemId                   UNIQUEIDENTIFIER        NOT NULL, FK → InventoryItems(Id)
SupplierId               UNIQUEIDENTIFIER        NULL, FK → Suppliers(Id)
LotNumber                NVARCHAR(100)           NULL
Quantity                 DECIMAL(10,3)           NOT NULL
RemainingQty             DECIMAL(10,3)           NOT NULL  -- updated as consumed
UnitCostBDT              DECIMAL(18,4)           NOT NULL
TotalCostBDT             DECIMAL(18,4)           NOT NULL  -- = Qty * UnitCost
ReceivedDate             DATE                    NOT NULL
ExpiryDate               DATE                    NULL
IsFullyConsumed          BIT                     NOT NULL, DEFAULT 0
Notes                    NVARCHAR(500)           NULL
CreatedAt                DATETIME2(7)            NOT NULL
CreatedByUserId          UNIQUEIDENTIFIER        NOT NULL

INDEXES:
  PK_StockBatches
  IX_StockBatches_ItemId: NONCLUSTERED on (ItemId, ReceivedDate DESC)
  IX_StockBatches_Expiry: NONCLUSTERED on (TenantId, ExpiryDate)
    WHERE ExpiryDate IS NOT NULL AND IsFullyConsumed=0
    -- Expiry alert background job
  IX_StockBatches_Active: NONCLUSTERED on (ItemId, IsFullyConsumed)
    WHERE IsFullyConsumed=0  -- WAC calculation

CONSTRAINTS:
  CK_StockBatches_Qty: Quantity > 0
  CK_StockBatches_RemQty: RemainingQty >= 0 AND RemainingQty <= Quantity
```

---

### 15.4 `inventory.StockTransactions`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
ItemId                   UNIQUEIDENTIFIER        NOT NULL, FK → InventoryItems(Id)
TransactionType          TINYINT                 NOT NULL
  -- 0=StockIn,1=FeedDeduction,2=MedicineDeduction,3=ManualStockOut,4=WriteOff,5=Adjustment
TransactionDate          DATE                    NOT NULL
Quantity                 DECIMAL(10,3)           NOT NULL  -- negative for outgoing
StockBatchId             UNIQUEIDENTIFIER        NULL, FK → StockBatches(Id)  -- for StockIn
UnitCostBDT              DECIMAL(18,4)           NULL
TotalValueBDT            DECIMAL(18,4)           NULL
-- Source references (one may be populated depending on type)
FeedConsumptionLogId     UNIQUEIDENTIFIER        NULL, FK → feeding.FeedConsumptionLogs(Id)
TreatmentRecordId        UNIQUEIDENTIFIER        NULL, FK → health.TreatmentRecords(Id)
VaccinationRecordId      UNIQUEIDENTIFIER        NULL, FK → health.VaccinationRecords(Id)
-- Balances after transaction (denormalized for audit trail)
StockBefore              DECIMAL(10,3)           NOT NULL
StockAfter               DECIMAL(10,3)           NOT NULL
Reason                   NVARCHAR(500)           NULL
TransactedByUserId       UNIQUEIDENTIFIER        NOT NULL
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
  -- IMMUTABLE: no update/delete after creation

INDEXES:
  PK_StockTransactions
  IX_StockTrans_TenantId_ItemId_Date: NONCLUSTERED on (TenantId, ItemId, TransactionDate DESC)
    -- Inventory movement ledger
  IX_StockTrans_TenantId_Date: NONCLUSTERED on (TenantId, TransactionDate DESC)
  IX_StockTrans_Type_Date: NONCLUSTERED on (TransactionType, TransactionDate)
    -- Report by transaction type

NOTE: StockTransactions is INSERT-ONLY. No UPDATE or DELETE permissions granted
to application service account on this table.
```

---

## 16. Table Design — Finance Schema

### 16.1 `finance.ChartOfAccounts`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
AccountCode              VARCHAR(20)             NOT NULL, UNIQUE
AccountName              NVARCHAR(200)           NOT NULL
AccountNameBn            NVARCHAR(200)           NOT NULL
EntryType                TINYINT                 NOT NULL  -- 0=Income, 1=Expense
Category                 NVARCHAR(100)           NOT NULL
SortOrder                INT                     NOT NULL
IsSystemAccount          BIT                     NOT NULL, DEFAULT 1
  -- System accounts cannot be deleted by tenants
IsActive                 BIT                     NOT NULL, DEFAULT 1

SEED DATA (system accounts):
  INC-001 | Animal Sale Income       | Income
  INC-002 | Milk Sale Income         | Income
  INC-003 | Byproduct Sale Income    | Income
  INC-004 | Other Income             | Income
  EXP-001 | Animal Purchase          | Expense
  EXP-002 | Feed Cost                | Expense
  EXP-003 | Veterinary Cost          | Expense
  EXP-004 | Labor Cost               | Expense
  EXP-005 | Utilities Cost           | Expense
  EXP-006 | Transport Cost           | Expense
  EXP-007 | Inventory Purchase       | Expense
  EXP-008 | Miscellaneous Expense    | Expense
```

---

### 16.2 `finance.FinancialEntries` ← System-Versioned Temporal Table

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
FarmId                   UNIQUEIDENTIFIER        NOT NULL, FK → platform.Farms(Id)
AccountId                UNIQUEIDENTIFIER        NOT NULL, FK → ChartOfAccounts(Id)
EntryType                TINYINT                 NOT NULL  -- 0=Income, 1=Expense
EntrySource              TINYINT                 NOT NULL  -- 0=Manual,1=AnimalSale,...
AmountBDT                DECIMAL(18,4)           NOT NULL
Currency                 CHAR(3)                 NOT NULL, DEFAULT 'BDT'
EntryDate                DATE                    NOT NULL
AccountingPeriod         CHAR(7)                 NOT NULL  -- 'YYYY-MM' format
  -- Indexed for period reporting; cannot be back-dated to closed period
Description              NVARCHAR(500)           NULL
-- Optional links to source data
AnimalId                 UNIQUEIDENTIFIER        NULL, FK → livestock.Animals(Id)
BatchId                  UNIQUEIDENTIFIER        NULL, FK → livestock.AnimalBatches(Id)
ShedId                   UNIQUEIDENTIFIER        NULL, FK → platform.Sheds(Id)
StockTransactionId       UNIQUEIDENTIFIER        NULL, FK → inventory.StockTransactions(Id)
FeedConsumptionLogId     UNIQUEIDENTIFIER        NULL, FK → feeding.FeedConsumptionLogs(Id)
TreatmentRecordId        UNIQUEIDENTIFIER        NULL, FK → health.TreatmentRecords(Id)
-- Period lock
IsPeriodClosed           BIT                     NOT NULL, DEFAULT 0
  -- Set to 1 by month-end job; prevents deletion (application-enforced)
IsReversed               BIT                     NOT NULL, DEFAULT 0
ReversalEntryId          UNIQUEIDENTIFIER        NULL, FK → FinancialEntries(Id)
-- Temporal table columns
ValidFrom                DATETIME2(7)            GENERATED ALWAYS AS ROW START NOT NULL
ValidTo                  DATETIME2(7)            GENERATED ALWAYS AS ROW END NOT NULL
PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo)
-- Audit...
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
CreatedByUserId          UNIQUEIDENTIFIER        NOT NULL
RowVersion               ROWVERSION              NOT NULL

TEMPORAL: WITH (SYSTEM_VERSIONING = ON, HISTORY_TABLE = finance.FinancialEntriesHistory)

INDEXES:
  PK_FinancialEntries
  IX_Finance_TenantId_Period: NONCLUSTERED on (TenantId, AccountingPeriod)
    INCLUDE (AmountBDT, EntryType, AccountId)
    -- Monthly P&L generation; most critical query
  IX_Finance_TenantId_FarmId_Period: NONCLUSTERED on (TenantId, FarmId, AccountingPeriod)
    -- Per-farm P&L
  IX_Finance_AnimalId: NONCLUSTERED on AnimalId WHERE AnimalId IS NOT NULL
    -- Per-animal cost ledger queries
  IX_Finance_BatchId: NONCLUSTERED on BatchId WHERE BatchId IS NOT NULL
    -- Batch P&L queries
  IX_Finance_EntryDate: NONCLUSTERED on (TenantId, EntryDate DESC)
  IX_Finance_Source: NONCLUSTERED on (TenantId, EntrySource, EntryDate)
    -- Dashboard: today's auto-posted vs. manual

CONSTRAINTS:
  CK_Finance_Amount: AmountBDT > 0
  CK_Finance_Period: AccountingPeriod ~ '^[0-9]{4}-(0[1-9]|1[0-2])$'
```

---

### 16.3 `finance.AnimalCostLedgers`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
AnimalId                 UNIQUEIDENTIFIER        NOT NULL, FK → livestock.Animals(Id)
-- Running totals (updated by domain event handlers)
TotalAcquisitionCostBDT  DECIMAL(18,4)           NOT NULL, DEFAULT 0
TotalFeedCostBDT         DECIMAL(18,4)           NOT NULL, DEFAULT 0
TotalHealthCostBDT       DECIMAL(18,4)           NOT NULL, DEFAULT 0
TotalOtherCostBDT        DECIMAL(18,4)           NOT NULL, DEFAULT 0
TotalCostBDT             AS (TotalAcquisitionCostBDT + TotalFeedCostBDT +
                              TotalHealthCostBDT + TotalOtherCostBDT)  -- COMPUTED
TotalRevenueBDT          DECIMAL(18,4)           NOT NULL, DEFAULT 0
NetProfitBDT             AS (TotalRevenueBDT - TotalCostBDT)            -- COMPUTED
RoiPct                   AS (CASE WHEN TotalCostBDT = 0 THEN 0
                                  ELSE (TotalRevenueBDT - TotalCostBDT)
                                       / TotalCostBDT * 100 END)       -- COMPUTED
IsClosed                 BIT                     NOT NULL, DEFAULT 0
  -- True when animal is disposed
ClosedDate               DATE                    NULL
UpdatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_AnimalCostLedgers
  UQ_CostLedger_AnimalId: UNIQUE on AnimalId  -- one ledger per animal
  IX_CostLedger_TenantId: NONCLUSTERED on TenantId
    INCLUDE (TotalCostBDT, TotalRevenueBDT, NetProfitBDT)
    -- Dashboard financial summary
```

---

### 16.4 `finance.BatchProfitLoss`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
BatchId                  UNIQUEIDENTIFIER        NOT NULL, UNIQUE, FK → livestock.AnimalBatches(Id)
-- Computed aggregates (recalculated by domain event handlers)
TotalAnimalPurchaseCost  DECIMAL(18,4)           NOT NULL, DEFAULT 0
TotalFeedCost            DECIMAL(18,4)           NOT NULL, DEFAULT 0
TotalHealthCost          DECIMAL(18,4)           NOT NULL, DEFAULT 0
TotalOtherCost           DECIMAL(18,4)           NOT NULL, DEFAULT 0
TotalCost                AS (...sum...)           -- COMPUTED
TotalRevenue             DECIMAL(18,4)           NOT NULL, DEFAULT 0
GrossProfit              AS (TotalRevenue - TotalCost)  -- COMPUTED
RoiPct                   AS (...)               -- COMPUTED
AnimalCount              INT                     NOT NULL, DEFAULT 0
DisposedAnimalCount      INT                     NOT NULL, DEFAULT 0
Status                   TINYINT                 NOT NULL  -- 0=InProgress,1=Final
LastCalculatedAt         DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_BatchProfitLoss
  UQ_BatchPL_BatchId: UNIQUE on BatchId
  IX_BatchPL_TenantId: NONCLUSTERED on TenantId
    INCLUDE (TotalCost, TotalRevenue, GrossProfit, RoiPct)
```

---

### 16.5 `finance.LoanRecords`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
FarmId                   UNIQUEIDENTIFIER        NULL
LenderName               NVARCHAR(200)           NOT NULL
LenderType               TINYINT                 NOT NULL  -- 0=Bank,1=NGO,2=Personal,3=Other
PrincipalAmountBDT       DECIMAL(18,4)           NOT NULL
InterestRatePct          DECIMAL(5,4)            NULL
DisbursementDate         DATE                    NOT NULL
MaturityDate             DATE                    NULL
PaidAmountBDT            DECIMAL(18,4)           NOT NULL, DEFAULT 0  -- maintained
OutstandingBalanceBDT    AS (PrincipalAmountBDT - PaidAmountBDT)  -- COMPUTED
Status                   TINYINT                 NOT NULL  -- 0=Active,1=PaidOff,2=Defaulted
Notes                    NVARCHAR(1000)          NULL
-- Audit + soft delete...
RowVersion               ROWVERSION              NOT NULL

INDEXES:
  PK_LoanRecords
  IX_Loans_TenantId_Status: NONCLUSTERED on (TenantId, Status)
```

---

### 16.6 `finance.LoanRepayments`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
LoanId                   UNIQUEIDENTIFIER        NOT NULL, FK → LoanRecords(Id)
PaymentDate              DATE                    NOT NULL
PrincipalPaidBDT         DECIMAL(18,4)           NOT NULL, DEFAULT 0
InterestPaidBDT          DECIMAL(18,4)           NOT NULL, DEFAULT 0
TotalPaidBDT             AS (PrincipalPaidBDT + InterestPaidBDT)  -- COMPUTED
Notes                    NVARCHAR(500)           NULL
RecordedByUserId         UNIQUEIDENTIFIER        NOT NULL
CreatedAt                DATETIME2(7)            NOT NULL

INDEXES:
  PK_LoanRepayments
  IX_Repayments_LoanId_Date: NONCLUSTERED on (LoanId, PaymentDate DESC)
```

---

## 17. Table Design — Audit Schema

### 17.1 `audit.AuditLogs` ← Append-Only, Immutable

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       BIGINT                  PK IDENTITY(1,1)  -- NOT GUID: sequential for performance
TenantId                 UNIQUEIDENTIFIER        NOT NULL
UserId                   UNIQUEIDENTIFIER        NULL  -- NULL for system actions
UserFullName             NVARCHAR(200)           NULL
UserRole                 NVARCHAR(50)            NULL
ImpersonatedByUserId     UNIQUEIDENTIFIER        NULL  -- platform admin impersonation
Action                   NVARCHAR(50)            NOT NULL
  -- 'CREATED','UPDATED','DELETED','EXPORTED','LOGIN','LOGOUT','STATUS_CHANGE'
EntityType               NVARCHAR(100)           NOT NULL
  -- 'Animal','VaccinationRecord','FinancialEntry', etc.
EntityId                 NVARCHAR(100)           NOT NULL  -- GUID as string
EntityDisplayName        NVARCHAR(200)           NULL
PreviousValueJson        NVARCHAR(MAX)           NULL  -- JSON snapshot
NewValueJson             NVARCHAR(MAX)           NULL  -- JSON snapshot
IpAddress                NVARCHAR(45)            NULL  -- IPv4 or IPv6
UserAgent                NVARCHAR(500)           NULL
CorrelationId            NVARCHAR(100)           NULL
Timestamp                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
  -- NEVER NULL; inserted at time of action

INDEXES:
  PK_AuditLogs: CLUSTERED on Id (sequential BIGINT — no fragmentation)
  IX_Audit_TenantId_Timestamp: NONCLUSTERED on (TenantId, Timestamp DESC)
    -- Tenant audit viewer
  IX_Audit_EntityType_EntityId: NONCLUSTERED on (EntityType, EntityId, Timestamp DESC)
    -- Entity-specific audit trail (e.g., all changes to Animal X)
  IX_Audit_UserId_Timestamp: NONCLUSTERED on (UserId, Timestamp DESC)
    -- User activity report
  IX_Audit_CorrelationId: NONCLUSTERED on CorrelationId
    -- Request-level tracing

PERMISSIONS:
  Application service account: INSERT ONLY (no UPDATE, no DELETE)
  DBA read account: SELECT (for support/compliance)
  Application user: no direct access (via API only)

PARTITIONING: Monthly partition on Timestamp column
  Active (< 12 months): online, queryable
  Archive (> 12 months): compressed, moved to cold storage schema
```

---

### 17.2 `audit.Notifications`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       UNIQUEIDENTIFIER        PK DEFAULT NEWSEQUENTIALID()
TenantId                 UNIQUEIDENTIFIER        NOT NULL
RecipientUserId          UNIQUEIDENTIFIER        NOT NULL, FK → Users(Id)
NotificationType         TINYINT                 NOT NULL
  -- 0=VaccinationDue,1=VaccinationOverdue,2=LowStock,3=SubscriptionExpiry...
Priority                 TINYINT                 NOT NULL  -- 0=Info,1=Warning,2=Critical
TitleEn                  NVARCHAR(200)           NOT NULL
TitleBn                  NVARCHAR(200)           NULL
BodyEn                   NVARCHAR(1000)          NOT NULL
BodyBn                   NVARCHAR(1000)          NULL
EntityType               NVARCHAR(100)           NULL
EntityId                 NVARCHAR(100)           NULL
IsRead                   BIT                     NOT NULL, DEFAULT 0
ReadAt                   DATETIME2(7)            NULL
SmsSent                  BIT                     NOT NULL, DEFAULT 0
SmsSentAt                DATETIME2(7)            NULL
PushSent                 BIT                     NOT NULL, DEFAULT 0
CreatedAt                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()
ExpiresAt                DATETIME2(7)            NULL

INDEXES:
  PK_Notifications
  IX_Notifications_UserId_Read: NONCLUSTERED on (RecipientUserId, IsRead, CreatedAt DESC)
    -- Notification center query
  IX_Notifications_TenantId_Date: NONCLUSTERED on (TenantId, CreatedAt DESC)
  IX_Notifications_Expires: NONCLUSTERED on ExpiresAt
    WHERE ExpiresAt IS NOT NULL  -- cleanup job
```

---

### 17.3 `audit.SystemEvents`

```
Column                   Type                    Constraints
─────────────────────────────────────────────────────────────────────────────
Id                       BIGINT                  PK IDENTITY(1,1)
EventType                NVARCHAR(100)           NOT NULL
Severity                 TINYINT                 NOT NULL  -- 0=Info,1=Warning,2=Error,3=Critical
Message                  NVARCHAR(2000)          NOT NULL
ContextJson              NVARCHAR(MAX)           NULL
CorrelationId            NVARCHAR(100)           NULL
TenantId                 UNIQUEIDENTIFIER        NULL  -- may be system-level (null)
UserId                   UNIQUEIDENTIFIER        NULL
Timestamp                DATETIME2(7)            NOT NULL, DEFAULT SYSUTCDATETIME()

INDEXES:
  PK_SystemEvents: CLUSTERED on Id
  IX_SystemEvents_Type_Timestamp: NONCLUSTERED on (EventType, Timestamp DESC)
  IX_SystemEvents_Severity: NONCLUSTERED on (Severity, Timestamp DESC)
    WHERE Severity >= 2  -- errors and criticals
```

---

## 18. Soft Delete Design

### 18.1 Soft Delete Standard

Every business entity table implements soft delete. Hard delete is **never permitted** on business data.

```
Standard Soft Delete Columns on every entity table:
  IsDeleted       BIT            NOT NULL, DEFAULT 0, CONSTRAINT DF_{Table}_IsDeleted
  DeletedAt       DATETIME2(7)   NULL
  DeletedByUserId UNIQUEIDENTIFIER NULL, FK → platform.Users(Id)
```

### 18.2 Soft Delete Rules

| Rule | Implementation |
|---|---|
| **Read filter** | EF Core Global Query Filter: `.HasQueryFilter(e => !e.IsDeleted)` — applied per tenant |
| **Uniqueness** | All unique constraints use `WHERE IsDeleted = 0` filtered index to allow "soft-deleted" records to coexist |
| **Cascading** | Soft-deleting a parent (e.g., Farm) does NOT auto-soft-delete children — explicit business logic required |
| **Financial entries** | Cannot be soft-deleted in closed periods — application enforced |
| **Audit logs** | Never soft-deleted — immutable by design |
| **Stock transactions** | Never soft-deleted — insert-only |
| **Recovery** | `IsDeleted=0` can be restored by Platform Admin within the 90-day data retention window |
| **Purge** | Background job purges soft-deleted records after 90 days for SME tenants; 7 years for financial records |

### 18.3 Soft Delete for Enumerations

Animal status changes (Active → Sold/Dead) are NOT implemented as soft delete. They are legitimate state transitions stored in the `Status` column with `DisposalDate`. The animal record is preserved permanently.

---

## 19. Audit Columns

### 19.1 Standard Audit Column Set

Every entity table (except append-only audit tables) carries these 6 columns:

```
CreatedAt       DATETIME2(7)        NOT NULL, DEFAULT SYSUTCDATETIME()
CreatedByUserId UNIQUEIDENTIFIER    NOT NULL, FK → platform.Users(Id)
UpdatedAt       DATETIME2(7)        NOT NULL, DEFAULT SYSUTCDATETIME()
UpdatedByUserId UNIQUEIDENTIFIER    NOT NULL, FK → platform.Users(Id)
IsDeleted       BIT                 NOT NULL, DEFAULT 0
DeletedAt       DATETIME2(7)        NULL
DeletedByUserId UNIQUEIDENTIFIER    NULL, FK → platform.Users(Id)
RowVersion      ROWVERSION          NOT NULL  -- optimistic concurrency
```

### 19.2 EF Core Audit Interceptor

The `AuditSaveChangesInterceptor` populates audit columns automatically on `SaveChangesAsync()`. Developers never set audit columns manually.

```
On INSERT:
  CreatedAt       = UTC now
  CreatedByUserId = ICurrentUserService.GetUserId()
  UpdatedAt       = UTC now
  UpdatedByUserId = ICurrentUserService.GetUserId()
  IsDeleted       = 0
  TenantId        = ITenantService.GetCurrentTenantId()

On UPDATE:
  UpdatedAt       = UTC now
  UpdatedByUserId = ICurrentUserService.GetUserId()
  Assert: TenantId unchanged (security check)

On soft DELETE (IsDeleted = true):
  DeletedAt       = UTC now
  DeletedByUserId = ICurrentUserService.GetUserId()
  IsDeleted       = 1
```

### 19.3 Optimistic Concurrency with RowVersion

```
RowVersion (ROWVERSION type in SQL Server):
  → 8-byte binary timestamp automatically updated by SQL Server on every row change
  → EF Core maps this as: IsRowVersion() on the property
  → On update, EF Core generates:
      WHERE Id = @id AND RowVersion = @originalRowVersion
  → If 0 rows updated → throws DbUpdateConcurrencyException
  → Application catches → returns 409 Conflict to client
  → Client receives "Record was updated by another user. Refresh to see latest."
```

---

## 20. Multi-Tenant Database Design

### 20.1 TenantId Enforcement Architecture

```
LAYER 1: Application — EF Core Global Query Filter
  ─────────────────────────────────────────────────
  DbContext.OnModelCreating():
    For every entity implementing ITenantEntity:
      builder.HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId
                                  && !e.IsDeleted)
  
  This is AUTOMATIC — no developer remembers to add WHERE TenantId=...
  Every LINQ query is wrapped in this filter at EF Core level.

LAYER 2: Application — Save Interceptor
  ─────────────────────────────────────
  AuditSaveChangesInterceptor.SavingChangesAsync():
    For each Added entity:
      Assert entity.TenantId == _tenantService.CurrentTenantId
    For each Modified entity:
      Assert entity.TenantId == _tenantService.CurrentTenantId
      If mismatch: THROW SecurityException (critical bug detection)

LAYER 3: Database — SQL Server Row-Level Security
  ─────────────────────────────────────────────────
  Session context set on DB connection open:
    EXEC sp_set_session_context N'TenantId', @tenantId, @read_only = 1;
  
  RLS Security Policy (example for Animals table):
    CREATE SECURITY POLICY AnimalsTenantPolicy
    ADD FILTER PREDICATE dbo.fn_tenantPredicate(TenantId)
    ON livestock.Animals WITH (STATE = ON);
  
  Predicate function:
    CREATE FUNCTION fn_tenantPredicate(@TenantId UNIQUEIDENTIFIER)
    RETURNS TABLE WITH SCHEMABINDING AS
    RETURN SELECT 1 AS result
    WHERE CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER) = @TenantId
    OR IS_ROLEMEMBER('db_datareader') = 1; -- Admin bypass

LAYER 4: Database — Unique Constraints scoped to TenantId
  ────────────────────────────────────────────────────────
  All unique constraints include TenantId as the leading column:
  UQ_Animals_TenantId_TagId prevents TagId collision WITHIN a tenant only.
  Different tenants can have same TagId — correct behavior.
```

### 20.2 Background Job Tenant Context

```
System-level background jobs (VaccinationReminderJob):
  → Use a dedicated ApplicationDbContext with RLS bypass role
  → Query ALL tenants: SELECT Id FROM platform.Tenants WHERE Status = 0
  → For each tenant:
      → Create DI scope
      → Set session context: EXEC sp_set_session_context N'TenantId', @tenantId
      → Execute per-tenant query (RLS now filters to this tenant)
      → Dispose scope
  → Tenant isolation maintained per iteration
```

### 20.3 Enterprise Tenant — Dedicated Database

```
For Corporation tier tenants requiring dedicated database:
  → Connection string stored in platform.Tenants.ConnectionStringRef
    (pointer to AWS Secrets Manager secret, not the actual string)
  → TenantDbConnectionResolver reads:
      1. platform.Tenants.ConnectionStringRef
      2. Retrieves connection string from Secrets Manager at runtime
      3. Returns appropriate SqlConnection
  → All application code is identical — only the connection changes
  → RLS not needed (single-tenant database by definition)
  → EF Core Global Query Filter still applied for IsDeleted filter
```

---

## 21. Migration Strategy

### 21.1 EF Core Migration Policy

```
Principles:
  → Code-First: schema is derived from C# models, not the reverse
  → Every schema change = a new migration file
  → Migrations are idempotent (re-running does nothing if already applied)
  → Every destructive migration (column drop, table drop) requires:
      1. A reverse migration script (rollback)
      2. A data migration script (data preservation)
      3. DBA + Tech Lead review

Migration Naming Convention:
  {Timestamp}_{PascalCaseDescription}
  Example:
    20260707001_InitialCreate
    20260707002_AddAnimalBcsColumn
    20260708001_AddInventorySupplierTable
    20260710001_AddFinancialEntryIsPeriodClosed
```

### 21.2 Migration Workflow

```
Developer workflow:
1. Modify C# entity / configuration
2. dotnet ef migrations add {Name} --project Farm360.Infrastructure --startup-project Farm360.Api
3. Review generated Up() and Down() methods
4. Write rollback test: apply migration → verify → revert → verify
5. Commit migration file WITH entity change in same PR

CI/CD workflow:
  → Migration runs as a Kubernetes Job (farm360-migrator) before API pod start
  → Migrator container: dotnet ef database update --connection {prod-connection}
  → Success: migrator job exits 0 → API deployment proceeds
  → Failure: migrator job exits non-zero → deployment blocked → alert fires

Production migration safety rules:
  → No column RENAME in a single migration (add new → copy data → drop old)
  → No NOT NULL column addition without DEFAULT value or data backfill migration
  → Large table migrations (> 1M rows) run in batches during maintenance window
  → Index additions on live tables: CREATE INDEX ... WITH (ONLINE = ON)
```

### 21.3 Multi-Tenant Migration

```
For Shared Database tenants:
  → Single migration applied to shared database
  → All tenant schemas updated simultaneously
  → Migration window: Sunday 02:00–03:00 BDT

For Dedicated Database tenants (Enterprise):
  → Migrator runs against each dedicated database
  → Orchestrated by Hangfire job: MigrateDedicatedTenantsJob
  → Each tenant migrated sequentially; failure of one does not block others
  → Failed migrations logged and alert sent to DBA

Schema Version Tracking:
  → __EFMigrationsHistory table in each database
  → platform.SchemaVersions table tracks which version each tenant's DB is on
  → Dashboard available to Platform Admin showing migration status per tenant
```

---

## 22. Performance Optimization

### 22.1 Index Strategy

**Principle:** Index for the queries you know, not for every column. Over-indexing is as dangerous as under-indexing in a write-heavy system.

```
CRITICAL READ PATH INDEXES (review on every release):

1. Dashboard — Executive Summary
   Query: SELECT COUNT, SUM(costs), health_alerts for all animals in tenant
   Index: IX_Animals_TenantId_Status INCLUDE(LatestWeightKg, CurrentShedId)
   Index: IX_Finance_TenantId_Period INCLUDE(AmountBDT, EntryType)
   Caching: Result cached 5 minutes in Redis

2. Vaccination Due List
   Query: SELECT * FROM AnimalVaccinationSchedules WHERE TenantId=? AND Status IN (1,2) AND DueDate<=?
   Index: IX_VaxSchedule_TenantId_Status_Due (covers exactly this query)

3. Animal Search / Filter
   Query: Multi-column filter on Species, Status, Shed, Age range
   Index: IX_Animals_TenantId_Status (leading columns; WHERE clause filtering)
   Full-text: SQL Server Full-Text Index on (TagId, Name, BreedName) for text search

4. Monthly P&L Report
   Query: GROUP BY AccountId, SUM(AmountBDT) WHERE TenantId=? AND AccountingPeriod=?
   Index: IX_Finance_TenantId_Period INCLUDE(AmountBDT, EntryType, AccountId)
   Read Replica: Reports run on read replica, not primary

5. Inventory Stock Level
   Query: SELECT ItemId, CurrentStockQty, WeightedAvgCost WHERE TenantId=? AND FarmId=?
   Index: IX_Items_TenantId_FarmId INCLUDE(CurrentStockQty, ReorderThreshold)
   Caching: 30 seconds Redis cache; invalidated on every StockTransaction
```

### 22.2 Covering Indexes

```
Covering Index design principle: INCLUDE all columns that appear in SELECT clause
to make the query a single index scan with no key lookup.

Example — Animal List API response:
  SELECT a.Id, a.TagId, a.BreedName, a.Sex, a.Status, a.CurrentShedId,
         a.LatestWeightKg, a.LatestWeightDate, a.AcquisitionDate
  FROM livestock.Animals a
  WHERE a.TenantId = @tid AND a.Status = @status AND a.IsDeleted = 0
  ORDER BY a.AcquisitionDate DESC

  Index:
  CREATE NONCLUSTERED INDEX IX_Animals_TenantId_Status_Cover
  ON livestock.Animals (TenantId, Status, AcquisitionDate DESC)
  INCLUDE (TagId, BreedName, Sex, CurrentShedId, LatestWeightKg, LatestWeightDate)
  WHERE IsDeleted = 0
  → Zero key lookups; entire result from index
```

### 22.3 Read Replica Routing

```
Two connection strings in application:
  → WriteConnectionString: → SQL Server Primary (MSSQL-Primary)
  → ReadConnectionString:  → SQL Server Read Replica (MSSQL-Replica)

EF Core connection selection (via repository):
  IRepository.GetReadOnlyDbContext() → ReadConnectionString
  IRepository.GetWritableDbContext() → WriteConnectionString

Queries routed to Read Replica:
  → All GET endpoints (animal list, health history, dashboard, reports)
  → Background report generation (monthly P&L, inventory valuation)
  → Hangfire report generation jobs

Queries routed to Primary:
  → All Command handlers (state-changing operations)
  → Hangfire jobs that write (vaccination schedule generation)
  → Auth operations (login, token refresh)

Replication lag tolerance: 500ms acceptable for dashboard data
  → Not acceptable for: post-sale balance check, stock deduction
```

### 22.4 Denormalization for Performance

```
Strategically denormalized columns (maintained by domain event handlers):
  
  Animals.LatestWeightKg        — avoids subquery on WeightRecords
  Animals.LatestWeightDate      — avoids MAX() on WeightRecords
  Animals.AdgKgPerDay           — avoids calculation on every dashboard load
  Animals.BcsValue              — latest BCS score for quick display
  
  InventoryItems.CurrentStockQty       — avoids SUM of StockTransactions
  InventoryItems.WeightedAverageCostBDT — avoids WAC recalculation on read
  InventoryItems.TotalInventoryValueBDT — avoids multiplication on every load
  InventoryItems.NearestExpiryDate     — avoids MIN() on StockBatches per item
  
  AnimalBatches.AnimalCount    — avoids COUNT query on AnimalBatchMembers
  AnimalBatches.TotalCostBDT   — avoids SUM on FinancialEntries per batch
  
  BatchProfitLoss table         — entire P&L snapshot; avoids multi-table aggregation
  AnimalCostLedger table        — running cost totals; avoids aggregation on each read

Maintenance: All denormalized values updated synchronously in the same transaction
  as the source event (within the Command handler + domain event handler).
  Read-side data is eventually consistent only for cached dashboard data (max 5 min lag).
```

### 22.5 Query Optimization Standards

```
EF Core Query Guidelines:
  → AsNoTracking() on ALL read-only queries (saves EF change tracking overhead)
  → Explicit Include() for navigation properties; lazy loading DISABLED globally
  → Projection to DTOs (not loading full entities) on list endpoints
  → Pagination: cursor-based (WHERE Id > @lastId ORDER BY Id) for large datasets;
    offset-based for small datasets (reports)
  → No N+1: dashboard loads all related data in ≤ 3 queries using multi-result sets
  → SQL Server query hints: NOLOCK on read-only reporting queries (dirty read acceptable)

Column store indexes (for analytics queries in Phase 2):
  → FinancialEntries: CREATE COLUMNSTORE INDEX CSI_FinancialEntries
    ON finance.FinancialEntries (TenantId, AccountingPeriod, AmountBDT, EntryType)
  → FeedConsumptionLogs: COLUMNSTORE for FCR trend analysis
```

---

## 23. Data Partition Strategy

### 23.1 Partitioning Philosophy

At MVP scale (300 tenants, ~100K animals), partitioning is not required for performance. The design includes partitioning readiness so that adding partitions in Year 2 (1,200 tenants) requires no schema change — only partition function expansion.

### 23.2 Table Partitioning Design

```
Partitioned Tables (implemented from launch for scale-out readiness):

1. audit.AuditLogs — RANGE RIGHT by Month (Timestamp)
   ──────────────────────────────────────────────────
   Partition Function:  pf_AuditLogMonth
   Partition Scheme:    ps_AuditLogMonth → FILEGROUP_CURRENT, FILEGROUP_ARCHIVE
   Active partitions:   Current month + prior 11 months on PRIMARY filegroup
   Archive partitions:  12+ months ago on ARCHIVE filegroup (compressed)
   
   Monthly maintenance (Hangfire job, 1st of month):
     → CREATE new partition for upcoming month
     → SWITCH oldest active partition to ARCHIVE
     → Enable PAGE compression on archived partition
   
   Query benefit: WHERE Timestamp >= '2026-07-01' → single partition scan

2. audit.Notifications — RANGE RIGHT by Month (CreatedAt)
   Same pattern; expire after 6 months

3. finance.FinancialEntries — RANGE RIGHT by AccountingPeriod
   ────────────────────────────────────────────────────────────
   Partition by AccountingPeriod CHAR(7) ('2026-07', '2026-08', etc.)
   Monthly P&L queries access exactly 1 partition → fast
   YTD queries access ≤ 12 partitions → controlled

4. livestock.Animals — Hash partition by TenantId (Phase 3 only)
   ───────────────────────────────────────────────────────────────
   When tenant count > 2,000:
   Hash partitioning spreads tenant data across 4 filegroups
   Most queries include TenantId → partition elimination
   Requires dedicated DB per large Enterprise tenant first
```

### 23.3 Tenant-Level Data Sharding Strategy

```
Phase 1 (MVP — 0–300 tenants):
  → Single SQL Server instance
  → All tenants in shared database
  → TenantId column + Global Query Filter + RLS
  → No sharding

Phase 2 (300–2,000 tenants):
  → Introduce read replica for reporting
  → Enterprise tenants migrated to dedicated DB instances
  → SME tenants remain on shared database
  → Shard key = TenantId for all future design decisions

Phase 3 (2,000–10,000 tenants):
  → Horizontal sharding: Tenants distributed across multiple SQL Server instances
  → Shard routing table: platform.TenantShards maps TenantId → ConnectionStringRef
  → TenantDbConnectionResolver reads shard routing at query time
  → Fully transparent to application code (repository pattern abstraction)

Shard allocation strategy:
  → New tenants assigned to shard with lowest tenant count
  → Enterprise tenants always get dedicated database (separate from shard pool)
  → Shard migration (moving tenant between shards): offline operation + DNS cutover
```

---

## 24. Data Retention Strategy

### 24.1 Data Lifecycle Stages

```
Stage 1: ACTIVE DATA (0–12 months)
  Location:  Primary SQL Server (shared database)
  Access:    Full read/write via application
  Indexes:   All indexes active
  Compression: ROW compression on large tables
  Tables:    All tables

Stage 2: WARM ARCHIVE (12–36 months)
  Location:  Same SQL Server, ARCHIVE filegroup
  Access:    Read-only via API (reports, history)
  Indexes:   Only PK + essential lookup indexes
  Compression: PAGE compression
  Migration: Partition SWITCH operation (near-instant, no data copy)
  Tables:    AuditLogs, Notifications, FinancialEntries (closed periods)

Stage 3: COLD ARCHIVE (36 months – 7 years)
  Location:  AWS S3 (Parquet format) OR SQL Server cold instance
  Access:    Manual extract by Platform Admin for compliance requests
  Format:    Compressed Parquet (for analytics) + encrypted CSV (for compliance)
  Export:    Automated Hangfire job exports and compresses monthly partitions
  Tables:    AuditLogs, FinancialEntries, StockTransactions (financial compliance)

Stage 4: DATA DELETION (Post-retention)
  Animal records:    7 years after animal disposal date
  Financial entries: 7 years after accounting period close (Bangladesh regulatory)
  Audit logs:        7 years
  Health records:    7 years
  User accounts:     90 days after deletion request (GDPR-aligned)
  Photos/blobs:      1 year after account deletion

Stage 5: ACCOUNT DELETION
  Trigger: Owner requests deletion OR 90-day post-suspension
  Process:
    1. Export all tenant data to encrypted archive (7-day notification)
    2. Soft-delete all entity records (IsDeleted = 1)
    3. Purge personal data (Users.Phone, Users.Email → anonymized)
    4. Retain financial records in cold archive per regulatory requirement
    5. Final deletion confirmation email to Owner
```

### 24.2 Retention Policy by Table

| Table | Active Retention | Archive Retention | Legal Hold | Purge |
|---|---|---|---|---|
| `platform.Tenants` | Life of account | 90 days post-deletion | No | 90 days |
| `platform.Users` | Life of account | 90 days post-deletion | No | 90 days (PII anonymized) |
| `livestock.Animals` | Life of account | 7 years | If dispute | 7 years |
| `livestock.WeightRecords` | Life of account | 5 years | No | 5 years |
| `health.VaccinationRecords` | Life of account | 7 years | If DLS audit | 7 years |
| `health.TreatmentRecords` | Life of account | 7 years | If DLS audit | 7 years |
| `finance.FinancialEntries` | Current + 2 prior years | 7 years | Yes | 7 years |
| `finance.AnimalCostLedgers` | Life of account | 7 years | Yes | 7 years |
| `inventory.StockTransactions` | 2 years | 7 years | Yes | 7 years |
| `audit.AuditLogs` | 12 months | 7 years | Yes | 7 years |
| `audit.Notifications` | 6 months | Purge | No | 6 months |
| `platform.BillingRecords` | 2 years | 7 years | Yes | 7 years |
| Animal photos (S3) | Life of account | 1 year post-deletion | No | 1 year |

### 24.3 Data Purge Jobs

```
Hangfire Scheduled Jobs for Data Lifecycle:

PurgeExpiredNotificationsJob    → Daily 04:00 BDT
  DELETE FROM audit.Notifications WHERE ExpiresAt < GETUTCDATE() OR CreatedAt < DATEADD(month,-6,GETUTCDATE())

ArchiveAuditLogsJob             → 1st of month, 03:00 BDT
  PARTITION SWITCH old partition to ARCHIVE filegroup

CompressFinancialArchiveJob     → Quarterly, Sunday 03:00 BDT
  Enable PAGE compression on partitions > 36 months old

PurgeDeletedAccountsJob         → Daily 05:00 BDT
  SELECT tenants where Status=Deleted AND DeletedAt < 90 days ago
  Execute GDPR-compliant anonymization per tenant

ExportColdArchiveJob            → Monthly
  Export partitions > 36 months to S3 Parquet (per tenant, encrypted)
```

---

## 25. Appendix

### 25.1 Table Count Summary

| Schema | Tables | Rows (Est. Year 1) | Size (Est. Year 1) |
|---|---|---|---|
| `platform` | 11 | ~10,000 | < 100 MB |
| `livestock` | 7 | ~500,000 | ~2 GB |
| `feeding` | 6 | ~1,000,000 | ~1 GB |
| `health` | 9 | ~800,000 | ~1.5 GB |
| `inventory` | 4 | ~500,000 | ~500 MB |
| `finance` | 6 | ~2,000,000 | ~3 GB |
| `audit` | 3 | ~10,000,000 | ~5 GB |
| **Total** | **46** | **~15M** | **~13 GB** |

### 25.2 Critical Business Rules Enforced at DB Level

| Rule | DB Enforcement |
|---|---|
| One death record per animal | `UQ_Mortality_AnimalId` unique constraint |
| One P&L record per batch | `UQ_BatchPL_BatchId` unique constraint |
| One subscription per tenant | `UQ_Subscriptions_TenantId` unique constraint |
| Unique animal tag within tenant | `UQ_Animals_TenantId_TagId` filtered unique index |
| One feed log per shed per day | `UQ_Consumption_Shed_Date` unique constraint |
| Stock cannot go negative | `CK_Items_Stock: CurrentStockQty >= 0` |
| Weight must be positive | `CK_WeightRecords_Weight: WeightKg > 0` |
| Financial amount positive | `CK_Finance_Amount: AmountBDT > 0` |
| BCS within valid range | `CK_Animals_BCS: BcsValue BETWEEN 1.0 AND 5.0` |
| One Owner per Organization | Filtered unique index `UQ_OrgUsers_OneOwner` |
| Pregnancy date after mating | `CK_Breeding_PregnancyDate` check constraint |
| Only one batch member per active batch | Filtered unique index `UQ_BatchMembers_BatchId_AnimalId` |

### 25.3 Security — Database Permissions

```
Application Service Account (app_user):
  GRANT SELECT, INSERT, UPDATE ON platform.* TO app_user
  GRANT SELECT, INSERT, UPDATE ON livestock.* TO app_user
  GRANT SELECT, INSERT, UPDATE ON feeding.* TO app_user
  GRANT SELECT, INSERT, UPDATE ON health.* TO app_user
  GRANT SELECT, INSERT, UPDATE ON inventory.* TO app_user
  GRANT SELECT, INSERT, UPDATE ON finance.* TO app_user
  GRANT INSERT ON audit.AuditLogs TO app_user       -- INSERT ONLY
  GRANT INSERT ON audit.SystemEvents TO app_user    -- INSERT ONLY
  GRANT SELECT, INSERT, UPDATE ON audit.Notifications TO app_user
  DENY DELETE ON finance.FinancialEntries TO app_user -- application-level soft delete only
  DENY DELETE ON inventory.StockTransactions TO app_user

Read Replica Service Account (readonly_user):
  GRANT SELECT ON ALL TABLES TO readonly_user

Hangfire Service Account (hangfire_user):
  Full access to hangfire.* schema
  Same as app_user for data access

DBA Account (dba_user):
  Full access (for maintenance only; actions logged)

No direct application user access to DB credentials:
  All credentials via AWS Secrets Manager → fetched at startup
```

### 25.4 Revision History

| Version | Date | Author | Changes |
|---|---|---|---|
| 0.1 | July 2026 | Database Architecture Office | Initial draft |
| 1.0 | July 2026 | Database Architecture Office | Full production-ready design |

---

*This document is the canonical reference for all database design decisions for Farm360 AI. All EF Core entity configurations, migration scripts, and query optimizations must align with this document. Deviations require a formal design review and update to this document.*

---

**Farm360 AI — Database Design Document**  
*© 2026 Farm360 AI. All Rights Reserved.*
