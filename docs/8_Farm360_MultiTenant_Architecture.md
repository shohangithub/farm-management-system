# Farm360 AI — Production-Ready Multi-Tenant Architecture

**Document ID:** F360-MTA-2026-001  
**Version:** 1.0  
**Authority:** Chief Software Architect — Farm360 AI  
**Date:** July 2026  
**Governed by:** F360-CONST-2026-001 · SAD v1.0 · DDD v1.0  
**Classification:** Confidential — Engineering Reference  

---

> *"Multi-tenancy is not a feature. It is a fundamental architectural property. A breach in tenant isolation is not a bug — it is a catastrophic security incident. Design for isolation first. Design for performance second. Never reverse that order."*

---

## Table of Contents

1. [Tenancy Model](#1-tenancy-model)
2. [Tenant Isolation Architecture](#2-tenant-isolation-architecture)
3. [Tenant Resolution Pipeline](#3-tenant-resolution-pipeline)
4. [Tenant Context Design](#4-tenant-context-design)
5. [Tenant Cache Architecture](#5-tenant-cache-architecture)
6. [Subscription Architecture](#6-subscription-architecture)
7. [Tenant Settings & Configuration](#7-tenant-settings--configuration)
8. [Tenant Branding](#8-tenant-branding)
9. [Tenant Users](#9-tenant-users)
10. [Tenant Roles & Permissions](#10-tenant-roles--permissions)
11. [Tenant Storage](#11-tenant-storage)
12. [Tenant Audit](#12-tenant-audit)
13. [Soft Delete Architecture](#13-soft-delete-architecture)
14. [Global Query Filter Design](#14-global-query-filter-design)
15. [Cross-Tenant Protection](#15-cross-tenant-protection)
16. [Database Migration Strategy](#16-database-migration-strategy)
17. [Scaling Strategy](#17-scaling-strategy)
18. [Future Database-Per-Tenant Strategy](#18-future-database-per-tenant-strategy)
19. [Architecture & Flow Diagrams](#19-architecture--flow-diagrams)
20. [Sequence Diagrams](#20-sequence-diagrams)
21. [Database Design](#21-database-design)
22. [Security Design](#22-security-design)
23. [Risk Analysis](#23-risk-analysis)
24. [Testing Strategy](#24-testing-strategy)

---

## 1. Tenancy Model

### 1.1 Tenancy Strategy: Phased Evolution

Farm360 AI does NOT commit to a single tenancy model for all time. The model evolves with growth. The application code is **identical across all models** — only the connection resolution changes.

```
┌────────────────────────────────────────────────────────────────────────────┐
│                     TENANCY EVOLUTION ROADMAP                              │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  PHASE 1 — MVP (0–300 tenants)                                            │
│  ─────────────────────────────                                             │
│  Model: Shared Database, Shared Schema                                     │
│  Isolation: TenantId column + EF Core Filter + SQL RLS                     │
│  All tenants → same SQL Server instance → same database                   │
│  Tier coverage: Bittho, Khamar, Banik, NGO                                │
│                                                                            │
│  PHASE 2 — Growth (300–2,000 tenants)                                     │
│  ─────────────────────────────────────                                     │
│  Model: Shared DB for SME + Dedicated DB for Enterprise                    │
│  Isolation: Phase 1 for SME + separate connection for Enterprise           │
│  High-volume Banik tenants → separate schema within shared DB              │
│  Corporation tier → dedicated SQL Server RDS instance                     │
│  Read replica introduced for reporting workloads                           │
│                                                                            │
│  PHASE 3 — Scale (2,000–10,000 tenants)                                   │
│  ──────────────────────────────────────                                    │
│  Model: Horizontal sharding of shared database pool                        │
│  Shard routing: platform.TenantShards → ConnectionStringRef               │
│  All application code unchanged — only TenantDbConnectionResolver changes  │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Tenant Identity

```
Every tenant is uniquely identified by:

  TenantId:  UNIQUEIDENTIFIER (GUID) — internal; never exposed in URLs
  Slug:      NVARCHAR(100)   — URL-friendly, human-readable (e.g., "green-valley-farm")
  
The Slug appears in: JWT claims · Admin portal URLs · Audit log references
The GUID TenantId is used in: Every DB column · Every cache key · Every log entry ·
                               Every SignalR group · Every S3 storage prefix
```

### 1.3 Tenant Lifecycle States

```
                    ┌──────────────┐
     Registration → │  Onboarding  │ ← Guided wizard in progress
                    └──────┬───────┘
                           │ Wizard complete
                           ▼
                    ┌──────────────┐
                    │    Active    │ ← Normal operating state
                    └──────┬───────┘
                           │
           ┌───────────────┼───────────────┐
           ▼               ▼               ▼
    ┌─────────────┐ ┌─────────────┐ ┌──────────────┐
    │GracePeriod  │ │  Suspended  │ │   Deleted    │
    │(7 days read │ │ (full lock) │ │ (GDPR purge) │
    │ only after  │ │             │ │              │
    │ expiry)     │ └──────┬──────┘ └──────────────┘
    └──────┬──────┘        │ 90 days
           │               ▼ no renewal
           │        ┌──────────────┐
           │        │   Deleted    │
           └───────►│              │
                    └──────────────┘
```

---

## 2. Tenant Isolation Architecture

### 2.1 The Six-Layer Isolation Stack

```
┌────────────────────────────────────────────────────────────────────────────┐
│                    TENANT ISOLATION — 6 LAYER STACK                       │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  LAYER 1: EF CORE GLOBAL QUERY FILTER  (Application Layer)                │
│  Every DbSet<T> where T : ITenantEntity:                                  │
│  .HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId        │
│                     && !e.IsDeleted)                                       │
│  → Transparent; developer cannot forget it                                 │
│  → Bypassed ONLY by .IgnoreQueryFilters() (restricted to admin use)       │
│                                                                            │
│  LAYER 2: SAVE CHANGES INTERCEPTOR  (Application Layer)                   │
│  AuditSaveChangesInterceptor.SavingChangesAsync():                         │
│  → On INSERT: Assert entity.TenantId == currentTenantId                   │
│  → On UPDATE: Assert entity.TenantId unchanged                             │
│  → Mismatch: throws SecurityException — treated as critical defect        │
│                                                                            │
│  LAYER 3: SQL SERVER ROW-LEVEL SECURITY  (Database Layer)                 │
│  Session context on every DB connection open:                              │
│  EXEC sp_set_session_context N'TenantId', @tenantId, @read_only = 1       │
│  RLS predicate validates SESSION_CONTEXT matches row TenantId             │
│  → Blocks even raw SQL queries that bypass EF Core                         │
│                                                                            │
│  LAYER 4: UNIQUE CONSTRAINTS SCOPED TO TenantId  (Database Layer)         │
│  All unique constraints include TenantId as leading column                 │
│  UQ_Animals_TenantId_TagId WHERE IsDeleted=0                               │
│  → Tenant A and B can have same TagId — correct behavior                  │
│                                                                            │
│  LAYER 5: CACHE KEY NAMESPACING  (Infrastructure Layer)                   │
│  Format: {tenantId}:{domain}:{entity}:{identifier}:{version}              │
│  → Cache poisoning between tenants is impossible                           │
│                                                                            │
│  LAYER 6: SIGNALR GROUP NAMESPACING  (Infrastructure Layer)               │
│  Group name: "tenant-{tenantId}"                                           │
│  → Real-time events cannot cross tenant boundaries                         │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Isolation Layer Responsibility Matrix

| Threat | L1 Filter | L2 Interceptor | L3 RLS | L4 Constraints | L5 Cache | L6 SignalR |
|---|---|---|---|---|---|---|
| Developer forgets WHERE TenantId | ✅ Blocks | — | ✅ Blocks | — | — | — |
| Raw SQL bypass of EF Core | ❌ Missed | ❌ Missed | ✅ Blocks | — | — | — |
| Handler saves wrong TenantId | ✅ Blocks | ✅ Blocks | ✅ Blocks | — | — | — |
| Tag collision across tenants | — | — | — | ✅ Blocks | — | — |
| Cache key collision | — | — | — | — | ✅ Blocks | — |
| Real-time event to wrong tenant | — | — | — | — | — | ✅ Blocks |

---

## 3. Tenant Resolution Pipeline

### 3.1 Resolution Flow (Every Authenticated Request)

```
HTTP Request
     ▼
[1] CorrelationIdMiddleware
    → Extract or generate X-Correlation-Id
    → Inject into HttpContext.Items + Serilog LogContext
     ▼
[2] JWT Authentication
    → Validate RS256 signature (public key from KMS JWKS)
    → Extract claims: sub (userId), tenant_id, tenant_slug, role, token_version
    → 401 if invalid, expired, or missing
     ▼
[3] TenantResolutionMiddleware
    [3a] Extract tenant_id from JWT → 401 if missing
    [3b] Redis.Get("tenant-ctx:{tenantId}") → TTL: 5 min
         HIT → skip [3c]
    [3c] MISS → SELECT from platform.Tenants → write to Redis
    [3d] Assert Tenant.Status:
         Active       → proceed
         GracePeriod  → set IsReadOnly=true, proceed
         Suspended    → 402 Payment Required
         Deleted      → 404 Not Found
    [3e] Assert JWT.token_version == DB.TokenVersion
         (cached in Redis 30s)
         Mismatch → 401 (token revoked)
    [3f] Populate ITenantService + ICurrentUserService (Scoped DI)
     ▼
[4] Authorization (Policy-Based ABAC + RBAC)
    → 403 if role lacks endpoint permission
     ▼
[5] MediatR Dispatch
    → TenantId flows through ITenantService
    → EF Core filter reads from ITenantService
    → Save Interceptor validates via ITenantService
     ▼
HTTP Response
```

---

## 4. Tenant Context Design

### 4.1 Tenant Context Object (Scoped — lives for one request)

```
TenantContext
├── TenantId:          Guid              Primary isolation key
├── Slug:              string            Human-readable identifier
├── Status:            TenantStatus      Active | GracePeriod | Suspended
├── Tier:              SubscriptionTier  Bittho | Khamar | Banik | Corp | NGO
├── TimeZone:          string            "Asia/Dhaka"
├── DefaultLanguage:   string            "bn-BD" | "en-US"
├── MaxFarms:          int               Tier limit
├── MaxAnimals:        int               -1 = unlimited
├── MaxUsers:          int               -1 = unlimited
├── IsReadOnly:        bool              true when GracePeriod
└── Features:          IReadOnlySet<string>  Enabled feature flags
```

### 4.2 Service Contracts

```
ITenantService (Scoped)
├── TenantContext GetCurrentContext()
├── Guid GetCurrentTenantId()
├── bool IsReadOnly()
├── bool HasFeature(string featureKey)
└── void SetTenant(Guid tenantId)        ← Background jobs ONLY

ICurrentUserService (Scoped)
├── Guid GetUserId()
├── Guid GetTenantId()
├── UserRole GetRole()
├── IReadOnlyList<Guid> GetAssignedFarmIds()   ← null = all farms
├── bool IsOwner()
└── bool HasPermission(string resource, string action)
```

### 4.3 Background Job Tenant Context

```
SYSTEM JOBS (cross-tenant — e.g., VaccinationReminderJob):
  → Query ALL active tenants using system-level context
  → For each tenant:
      → Create IServiceScope
      → Call tenantService.SetTenant(tenant.Id)
      → Set SQL session context
      → Execute per-tenant logic
      → Dispose scope (clears tenant context)

PER-TENANT ENQUEUED JOBS:
  → TenantId MUST be in job arguments
  → SetTenant(args.TenantId) MUST be first operation
  → Verify tenant is Active before proceeding
  → Failure = CRITICAL BUG (Constitution §22 Golden Rule 7)
```

---

## 5. Tenant Cache Architecture

### 5.1 Two-Level Cache Hierarchy

```
         API Pod 1          API Pod 2          API Pod 3
      ┌────────────┐      ┌────────────┐      ┌────────────┐
      │  L1 Cache  │      │  L1 Cache  │      │  L1 Cache  │
      │MemoryCache │      │MemoryCache │      │MemoryCache │
      │  ~1ms      │      │  ~1ms      │      │  ~1ms      │
      └──────┬─────┘      └──────┬─────┘      └──────┬─────┘
             │ L1 miss           │ L1 miss           │ L1 miss
             └───────────────────┼───────────────────┘
                                 ▼
                      ┌────────────────────┐
                      │   Redis (L2)        │
                      │   ~2-5ms           │
                      │   Shared all pods  │
                      └──────────┬─────────┘
                                 │ L2 miss
                                 ▼
                      ┌────────────────────┐
                      │   SQL Server DB     │
                      │   ~10-50ms         │
                      └────────────────────┘

L1 CROSS-POD INVALIDATION:
  Redis pub/sub: "cache-invalidation:{tenantId}"
  On Redis eviction → publish → all pods evict from L1
```

### 5.2 Cache Key Format + TTL Policy

```
FORMAT: {tenantId}:{domain}:{entity}:{identifier}:{version}

Examples:
  {tenantId}:livestock:animal:{animalId}:v1
  {tenantId}:platform:tenant-ctx
  {tenantId}:dashboard:executive:summary
  {tenantId}:identity:token-version:{userId}

TTL POLICY:
  Tenant context:        L1=2min  L2=5min   Invalidation: status/subscription change
  Token version:         L1=30s   L2=2min   Invalidation: password/role/revoke
  Dashboard summary:     L1=1min  L2=5min   Invalidation: animal/finance events
  Animal list:           L1=30s   L2=2min   Invalidation: any animal CRUD
  Animal detail:         L1=1min  L2=5min   Invalidation: animal update
  Inventory status:      L1=15s   L2=30s    Invalidation: any StockTransaction
  Subscription limits:   L1=5min  L2=15min  Invalidation: plan/count change
  Financial entries:     NEVER CACHED — always fresh from DB
```

---

## 6. Subscription Architecture

### 6.1 Tier Limits Matrix

| Feature | Bittho | Khamar | Banik | Corporation | NGO |
|---|---|---|---|---|---|
| Max Farms | 1 | 2 | 5 | Unlimited | 10 |
| Max Animals | 10 | 100 | 1,000 | Unlimited | 500 |
| Max Users | 1 | 3 | 10 | Unlimited | 20 |
| Storage | 100 MB | 1 GB | 10 GB | 100 GB | 5 GB |
| Dedicated DB | ❌ | ❌ | ❌ | ✅ | ❌ |
| AI Features | ❌ | ❌ | ✅ | ✅ | ✅ |

### 6.2 Enforcement Architecture

```
[1] PRE-CHECK (Application Layer — before command executes)
    SubscriptionLimitService:
    → Animal count vs. MaxAnimals (RegisterAnimalCommand)
    → User count vs. MaxUsers (InviteUserCommand)
    → Farm count vs. MaxFarms (CreateFarmCommand)
    → Feature flag check for premium features
    → Throws SubscriptionLimitExceededException → HTTP 402

[2] GRACEFUL DEGRADATION (Middleware — request level)
    TenantResolutionMiddleware:
    → GracePeriod: IsReadOnly=true → writes return 402, reads pass
    → Suspended: all requests return 402

[3] BACKGROUND ENFORCEMENT (Hangfire)
    SubscriptionExpiryCheckerJob (daily 02:00 BDT):
    → Active→GracePeriod when CurrentPeriodEnd <= TODAY
    → Send 7-day warning (SMS + email)
    SubscriptionSuspensionJob:
    → GracePeriod→Suspended when GracePeriodEndsAt <= NOW

[4] DOWNGRADE PROTECTION
    Before plan downgrade:
    → Compare current usage vs. new tier limits
    → Reject if any limit exceeded
    → Return which limits exceeded and by how much
```

### 6.3 Subscription State Machine

```
  ┌───────────┐  Period expires   ┌──────────────┐
  │  Active   │ ─────────────────►│ GracePeriod  │
  │ (Full)    │◄── Renewed ───────│ (Read-only)  │
  └─────┬─────┘                   └──────┬───────┘
        │ Owner cancels                  │ No renewal
        │                               │ within 7 days
        └────────────────┐              │
                         ▼              ▼
                    ┌─────────────────────┐
                    │     Suspended       │
                    │   (All blocked)     │
                    └──────────┬──────────┘
                               │ 90 days
                               ▼
                    ┌─────────────────────┐
                    │      Deleted        │
                    │   (GDPR purge)      │
                    └─────────────────────┘
```

---

## 7. Tenant Settings & Configuration

```
TYPE A: TIER-DEFINED (read-only for tenant)
  Source: platform.SubscriptionPlans.Features (JSON)
  Examples: max_animals, feature:offline_mode, storage_quota_mb
  Tenant cannot override.

TYPE B: TENANT-CONFIGURABLE (Owner-editable)
  Source: platform.TenantSettings (key-value store)
  Examples:
    notifications.vaccination_reminder_days: 7
    alerts.feed_deviation_pct: 20
    ui.date_format: DD/MM/YYYY
    ui.language: bn-BD
    finance.fiscal_year_start_month: 7
    reports.default_currency: BDT

FEATURE FLAGS (3-level hierarchy):
  [1] Global default (platform-level)
  [2] Tier override (subscription plan)
  [3] Tenant override (Platform Admin only — for beta/A-B testing)
  Evaluation: Tenant override → Tier → Global
  Result cached in TenantContext.Features
```

---

## 8. Tenant Branding

**Available to:** Banik, Corporation, NGO tiers.

```
TenantBranding table:
  OrganizationName    — header, reports, PDF exports
  LogoUrl             — S3 presigned URL (tenant's own prefix)
  FaviconUrl          — custom favicon
  PrimaryColorHex     — overrides brand-500 CSS token
  SecondaryColorHex   — overrides accent token
  ReportHeaderText    — PDF export header
  ReportFooterText    — PDF export footer
  CustomDomain        — future: app.greenvalleyfarm.com

Delivery: GET /api/v1/tenants/me/context → BrandingDto in response
Angular: Sets CSS custom properties on document root at startup
Changes: Reflected on next page load (not real-time)
```

---

## 9. Tenant Users

### 9.1 User-Tenant Architecture

```
platform.Users (GLOBAL entity — not tenant-scoped)
   │
   └── platform.OrganizationUsers (TENANT-SCOPED)
         ├── TenantId
         ├── OrganizationId
         ├── UserId → platform.Users
         ├── Role (0-5)
         ├── IsActive
         └── AssignedFarmIds (JSON array; NULL = all farms)

Why global Users:
  → User can belong to multiple organizations (NGO coordinator)
  → Different roles in different organizations
  → Future: consultant read-only access across tenants

SUB-TENANT ISOLATION via AssignedFarmIds:
  → Worker restricted to Farm A only (not Farm B or C)
  → Veterinarian serves Farms A and B (not C)
  → Owner always has AssignedFarmIds = null (all farms)
  → Enforced by ABAC check in every farm-scoped handler
```

### 9.2 User Invitation Flow

```
[1] Owner → POST /api/v1/users/invite
    {phone, role, farmIds}

[2] System generates invitation token (GUID)
    → Hash stored in platform.UserInvitations
    → Expiry: 7 days
    → SMS: OTP + app link to invitee

[3] Invitee registers (new) or logs in (existing)

[4] Invitee → POST /api/v1/users/accept-invitation {token}
    → Validate token hash
    → Create OrganizationUser record
    → Mark invitation Accepted

[5] Owner notified: "{Name} joined your organization"
```

---

## 10. Tenant Roles & Permissions

### 10.1 Hybrid ABAC + RBAC Model

```
RBAC (Role-Based):
  → Coarse-grained: which MODULES a role can access
  → Enforced: API endpoint [Authorize(Policy = "animals:write")]
  → Source: JWT role claim (no DB call)

ABAC (Attribute-Based):
  → Fine-grained: which SPECIFIC RESOURCES a user can access
  → Enforced: handler via ICurrentUserService.GetAssignedFarmIds()
  → Considers: farm assignment, resource ownership

DOMAIN RULES (Business-level):
  → Which ACTIONS are allowed on a resource in its current STATE
  → Enforced: domain entity methods
  → Example: Quarantined animal cannot be sold (regardless of role)
```

### 10.2 Role Permission Matrix

| Permission | Owner | FarmMgr | Vet | Worker | Accountant | Viewer |
|---|---|---|---|---|---|---|
| Tenant Management | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| User Invite/Manage | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| Animal Register | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ |
| Animal Sell/Dispose | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| Health Records | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| Feed Formula | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| Finance Entries | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ |
| Financial Reports | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ |
| All Read Access | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Hangfire Dashboard | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |

---

## 11. Tenant Storage

```
S3 Bucket: farm360-{env}-tenant-files (single bucket, prefix isolation)

Prefix structure:
  tenants/{tenantId}/animals/{animalId}/photos/{filename}
  tenants/{tenantId}/reports/{year}/{month}/{reportId}.pdf
  tenants/{tenantId}/exports/{exportId}.xlsx
  tenants/{tenantId}/logos/{filename}

Access control:
  → Presigned URLs (15-min expiry) — Angular uploads directly to S3
  → No public access on bucket
  → Bucket policy: presigned POST only to tenant's own prefix

Storage quota enforcement:
  → Pre-check before presigned URL: UsedBytes + fileSize <= QuotaBytes
  → 413 if exceeded + upgrade prompt
  → Daily Hangfire job recalculates UsedBytes from S3 ListObjectsV2
```

---

## 12. Tenant Audit

```
MECHANISM 1: APPLICATION AUDIT LOG (audit.AuditLogs)
  → Business-level events: "Animal BD-0041 sold by Rahman"
  → Written by AuditBehavior (MediatR pipeline)
  → INSERT ONLY — no UPDATE, DELETE, soft delete
  → Retention: 7 years
  → Partitioned by month

MECHANISM 2: SQL SERVER TEMPORAL TABLES
  → Automatic row history for: finance.FinancialEntries, finance.AnimalCostLedgers
  → Point-in-time recovery for financial disputes

MECHANISM 3: STRUCTURED LOGS (Serilog → CloudWatch)
  → Infrastructure events: logins, OTP attempts, rate limit hits
  → 90-day retention; Platform Admin access only

audit.AuditLogs schema:
  Id, TenantId, UserId, UserRole, EntityName, EntityId,
  Action, OldValues (JSON), NewValues (JSON),
  CorrelationId, IpAddress (hashed), UserAgent, Timestamp

DB permissions: app_user has INSERT only on audit.AuditLogs
```

---

## 13. Soft Delete Architecture

```
STANDARD COLUMNS on every business entity table:
  IsDeleted         BIT            NOT NULL, DEFAULT 0
  DeletedAt         DATETIME2(7)   NULL
  DeletedByUserId   UNIQUEIDENTIFIER NULL, FK → platform.Users(Id)

Set ONLY by AuditSaveChangesInterceptor — never manually by developer code.

RULES:
  → Hard DELETE never permitted; app_user DELETE permission revoked at DB level
  → AuditLogs and StockTransactions: immutable — no soft delete
  → FinancialEntries in closed period: reversal entry required (not soft delete)
  → All unique constraints: WHERE IsDeleted=0 (allows TagId reuse after delete)
  → EF Global Filter: includes !e.IsDeleted automatically
  → Recovery: Platform Admin can restore within 90-day window
  → Purge: Hangfire job after 90 days (SME) / 7 years (financial data)
  → Cascading: NOT automatic — explicit business logic required per entity
```

---

## 14. Global Query Filter Design

```
COMPOSITION (in ApplicationDbContext.OnModelCreating):

  Tenant-scoped entities (ITenantEntity):
  .HasQueryFilter(e =>
    e.TenantId == _tenantService.GetCurrentTenantId() && !e.IsDeleted)

  Soft-delete-only entities (ISoftDeletable, non-tenant):
  .HasQueryFilter(e => !e.IsDeleted)

  Global reference data (ChartOfAccounts, EnumTables):
  No filter applied.

IgnoreQueryFilters() PERMITTED ONLY in:
  [1] Platform Admin operations (SuperAdmin role + system services)
  [2] Public auth (OTP verification, slug lookup)
  [3] Uniqueness validation (cross-tenant slug check)
  [4] Must include code comment: // INTENTIONAL: reason here

NEVER permitted in any user-facing feature handler.
Architecture test in CI enforces this rule.
```

---

## 15. Cross-Tenant Protection

### 15.1 Attack Vector Analysis

| Attack | L1 Filter | L2 Interceptor | L3 RLS | Result |
|---|---|---|---|---|
| GUID enumeration (guess another tenant's ID) | ✅ Blocks | — | ✅ Blocks | 404 returned |
| JWT claim manipulation | N/A | N/A | N/A | 401 (RS256 signature invalid) |
| Raw SQL injection | ❌ | ❌ | ✅ Blocks | 0 rows, no leak |
| Compromised app_user DB credential | ❌ | ❌ | ✅ Blocks | RLS still enforces |
| Cache key collision | — | — | — | GUID prefix prevents |
| Background job tenant bleed | — | ✅ via SetTenant | ✅ | Architecture test flags |
| SignalR event to wrong tenant | — | — | — | Group name = TenantId |

### 15.2 Response Policy

```
Cross-tenant access attempt → ALWAYS returns 404 (NotFoundException)
                              NEVER returns 403 (would confirm resource exists)
This is an information security requirement — leaking existence is a data breach.
```

---

## 16. Database Migration Strategy

### 16.1 Migration Governance

```
NAMING: {Timestamp}_{PascalCaseDescription}
  20260707001_InitialCreate
  20260707002_AddAnimalBcsColumn

RULES:
  → One migration = one logical change
  → Never batch unrelated changes
  → Down() exists for dev rollback only — never run in production
  → Migration committed in same PR as entity/configuration change

DANGEROUS PATTERNS (require DBA + Tech Lead review):
  → Column rename: 3-step (add new → backfill → drop old in next release)
  → NOT NULL column: requires DEFAULT or backfill migration first
  → Large table index: WITH (ONLINE = ON) mandatory
  → Table drop: 2-release cycle minimum
```

### 16.2 Migration Workflow

```
Developer creates migration → CI tests apply/revert →
Merged to develop → Staging: K8s Migrator Job (exit 0 = proceed; else block) →
Production: same Migrator Job pattern → Sunday 02:00–03:00 BDT window
```

### 16.3 Dedicated Tenant Migration

```
Hangfire Job: MigrateDedicatedTenantsJob
  [1] Get all dedicated tenant connection strings from Secrets Manager
  [2] For each: run EF MigrateAsync()
  [3] Update platform.SchemaVersions per tenant
  [4] Failure of one tenant does NOT block others
  [5] Failed migrations: alert DBA → investigate during business hours
```

---

## 17. Scaling Strategy

| Phase | Tenants | API Pods | DB | Redis | Notes |
|---|---|---|---|---|---|
| **Phase 1 MVP** | 0–300 | 2–4 (HPA) | Single RDS Primary | Single node | Shared DB, no read replica |
| **Phase 2 Growth** | 300–2,000 | 4–12 | Primary + Read Replica + Enterprise dedicated | Cluster (3 shards) | Hangfire in dedicated pods |
| **Phase 3 Scale** | 2,000–10,000 | 12–40 | Horizontal shard pool | Cluster | Shard routing via TenantShards table |

---

## 18. Future Database-Per-Tenant Strategy

### 18.1 Connection Resolution Architecture

```
TenantDbConnectionResolver (called by DbContext factory):

  [1] Read TenantContext.ConnectionStringRef
      NULL → return SharedConnectionString (Shared DB)
      Has value → dedicated DB path

  [2] Fetch from AWS Secrets Manager
      Key: "farm360/{env}/tenant-db/{tenantId}"
      In-process cache: 5 minutes (avoids per-request KMS cost)

  [3] Return connection string → DbContext uses it

  [4] EF Global Filters STILL apply (IsDeleted etc.)
      RLS still runs (no harm on dedicated DB)
      Application code is IDENTICAL regardless of DB type
```

### 18.2 Shared → Dedicated Migration Process

```
[1] PROVISION:  Create RDS instance; store conn string in Secrets Manager
[2] SYNC:       Copy tenant rows from shared → dedicated; verify counts
[3] CUTOVER:    Maintenance window → drain connections → flip ConnectionStringRef
[4] CLEANUP:    Delete tenant rows from shared DB after 30-day observation
```

### 18.3 Phase 3 Shard Assignment

```
platform.TenantShards (TenantId, ShardId, ConnectionStringRef)
platform.Shards (Id, Name, TenantCount, MaxTenants, IsAcceptingNew)

New tenant → assigned to shard with lowest TenantCount
Enterprise → always dedicated DB outside shard pool
```

---

## 19. Architecture & Flow Diagrams

### 19.1 Overall Architecture

```
Internet
   │ HTTPS
   ▼
CloudFront + WAF (OWASP rules, rate limiting, TLS 1.3)
   │ /api/*                    │ /* (static)
   ▼                           ▼
AWS ALB                      S3 (Angular PWA)
   │ round-robin
   ├── API Pod 1 [L1 Cache]
   ├── API Pod 2 [L1 Cache]
   └── API Pod N [L1 Cache]
          │ (shared infrastructure)
          ├── Redis Cluster (L2 Cache, OTP, Rate Limit, SignalR Backplane)
          ├── Hangfire Worker Pods (background jobs)
          ├── SQL Server Primary (write path)
          ├── SQL Server Read Replica (read path — Phase 2)
          ├── S3 (tenant file storage — presigned URLs)
          └── AWS KMS (JWT private key, DB encryption)
```

### 19.2 Tenant Isolation Data Flow

```
Tenant A Request          Tenant B Request
   JWT: tenant_id=A          JWT: tenant_id=B
         │                         │
         ▼                         ▼
  ITenantService           ITenantService
  CurrentTenantId=A        CurrentTenantId=B
         │                         │
         ▼                         ▼
  EF Global Filter         EF Global Filter
  WHERE TenantId=A         WHERE TenantId=B
  AND IsDeleted=0          AND IsDeleted=0
         │                         │
         ▼                         ▼
  SQL Session Ctx          SQL Session Ctx
  TenantId=A               TenantId=B
         │                         │
         ▼                         ▼
  RLS: rows where          RLS: rows where
  TenantId=A only          TenantId=B only
         │                         │
         ▼                         ▼
  Tenant A Data            Tenant B Data
  (perfectly isolated)     (perfectly isolated)
```

---

## 20. Sequence Diagrams

### 20.1 Tenant Registration

```
Browser       API            OtpService      SMS Gateway      DB
  │            │                 │                │            │
  │──register─►│ Validate input  │                │            │
  │            │────GenerateOtp─►│                │            │
  │            │                 │──Hash→Redis────│            │
  │            │◄────OTP hash────│                │            │
  │            │──────────────────────────────────────────────►│
  │            │                 │        SendSms(+880, OTP)   │
  │◄───200─────│                 │                │            │
  │            │                 │                │            │
  │──verify────►│                │                │            │
  │            │────VerifyOtp───►│                │            │
  │            │◄───Valid────────│                │            │
  │            │────────────────────────────────────────────── BEGIN TX
  │            │                 │                │ INSERT Tenant, Org,
  │            │                 │                │ User, OrgUser, Sub
  │            │────────────────────────────────────────────── COMMIT
  │            │ Issue JWT + refresh token        │            │
  │◄───201─────│ {tenantId, accessToken, refreshToken}        │
```

### 20.2 Authenticated Request (Animal Query)

```
Browser    Middleware         MediatR           DB (EF Core + RLS)
  │            │                  │                    │
  │─GET /animals│                 │                    │
  │  Bearer JWT│                  │                    │
  │───────────►│                  │                    │
  │            │[1] CorrelationId │                    │
  │            │[2] JWT Validate  │                    │
  │            │[3] Tenant Resolve (Redis HIT: 0ms)    │
  │            │[4] Auth Policy   │                    │
  │            │────────────────►│                    │
  │            │     [Logging][Validation][Perf]       │
  │            │     [CachingBehavior → MISS]          │
  │            │     Handler: DbContext.Animals        │
  │            │     EF Filter: TenantId=A, IsDeleted=0│
  │            │                 │───── SELECT ───────►│
  │            │                 │     RLS enforced    │
  │            │                 │◄──── Rows ──────────│
  │            │     Project → DTO[]                   │
  │            │     Redis.Set (2min TTL)               │
  │            │     Elapsed: 87ms → Log Info          │
  │◄───200─────│                  │                    │
```

### 20.3 Cross-Tenant Attack (Blocked)

```
Browser (Tenant A)    Middleware       EF Core         SQL Server RLS
  │                       │               │                  │
  │─GET /animals/{B-GUID}─►│               │                  │
  │  Bearer: Tenant A JWT  │               │                  │
  │                       │ JWT Valid      │                  │
  │                       │ TenantId=A    │                  │
  │                       │──────────────►│                  │
  │                       │    SELECT WHERE Id={B-GUID}      │
  │                       │    AND TenantId=A (EF Filter)    │
  │                       │    AND IsDeleted=0               │
  │                       │───────────────────────────────► │
  │                       │    RLS: SESSION_CONTEXT=A        │
  │                       │    Row TenantId=B → REJECTED     │
  │                       │◄──── 0 rows ─────────────────── │
  │                       │◄── NotFoundException ────────────│
  │◄── 404 Not Found ─────│                                  │
  │ (not 403 — no leak    │                                  │
  │  of resource existence)                                   │
```

---

## 21. Database Design

### 21.1 Complete Tenant Table Catalog

```
platform.Tenants
  Id, Name, Slug(UNIQUE), SubscriptionTier, Status, TimeZone,
  DefaultLanguage, DataRegion, MaxFarms, MaxAnimals, MaxUsers,
  TokenVersion(INT), ConnectionStringRef(NULL=shared)
  + 6 audit columns + RowVersion

platform.TenantSettings (key-value, owner-configurable)
  Id, TenantId, SettingKey, SettingValue, DataType,
  LastModifiedAt, LastModifiedByUserId
  INDEX: UQ on (TenantId, SettingKey)

platform.TenantBranding
  Id, TenantId(UNIQUE), LogoUrl, FaviconUrl,
  PrimaryColorHex, SecondaryColorHex,
  ReportHeaderText, ReportFooterText, CustomDomain
  + audit columns

platform.TenantFeatureFlags (platform-admin controlled)
  Id, TenantId, FeatureKey, IsEnabled, ExpiresAt,
  GrantedByUserId(Platform Admin), GrantedAt, Reason
  INDEX: UQ on (TenantId, FeatureKey)

platform.TenantStorage (quota tracking)
  Id, TenantId(UNIQUE), UsedBytes, QuotaBytes, LastCalculatedAt

platform.SchemaVersions (migration tracking)
  Id, TenantId(NULL=shared DB), MigrationId, AppliedAt,
  AppliedBy, Status(0=Applied/1=Failed/2=Pending), ErrorMessage

platform.TenantShards (Phase 3 — shard routing)
  TenantId(PK), ShardId, ConnectionStringRef,
  AssignedAt, MigratedAt(NULL until moved)

platform.Shards (Phase 3)
  Id, Name, TenantCount, MaxTenants, Region, IsAcceptingNew
```

### 21.2 Index Strategy for Isolation Performance

```
ALL multi-row queries use TenantId as LEADING index column:

  IX_{Table}_TenantId_{Discriminator}

Examples:
  IX_Animals_TenantId_Status:  (TenantId, Status)
    INCLUDE(TagId, Species, CurrentShedId, LatestWeightKg)
  IX_Animals_TenantId_FarmId:  (TenantId, FarmId)
  IX_Entries_TenantId_Period:  (TenantId, AccountingPeriod)
    INCLUDE(AmountBDT, EntryType, AccountId)
  IX_VaxSchedule_TenantId_Due: (TenantId, Status, DueDate)

Result: All tenant queries = index seeks (never full-table scans)
TenantId in leading position = SQL Server partition elimination
```

---

## 22. Security Design

### 22.1 Trust Zone Model

```
ZONE 1: UNTRUSTED (Internet)       — HOSTILE until proven otherwise
   │ CloudFront WAF + TLS 1.3
ZONE 2: DMZ (API Layer)            — AUTHENTICATED; not fully trusted
   │ EF Filter + Save Interceptor
ZONE 3: APPLICATION (Handlers)     — AUTHORIZED for tenant; not for all actions
   │ SQL Server RLS
ZONE 4: DATA (SQL Server)          — HARDENED; last line of defense
```

### 22.2 Authentication Security

```
JWT RS256:
  Private key: AWS KMS (HSM-backed — never in application memory)
  Public key: JWKS endpoint (publicly accessible — safe)
  Access token: 15-minute expiry
  Refresh token: 30-day, one-time use, rotating, stored hashed in DB

Token Revocation:
  platform.Users.TokenVersion incremented on:
  password change · role change · deactivation · suspicious activity
  Every request validates JWT.token_version == DB.TokenVersion
  Revocation latency: max 30 seconds (Redis TTL)

OTP Security:
  6 digits · 10-minute expiry · max 3 attempts → 30-min lockout
  Stored as HMAC-SHA256 hash (plaintext never stored)
  OTP values never logged (Serilog masking policy)
```

---

## 23. Risk Analysis

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R-01 | EF Global Filter disabled in feature code | Medium | Critical | ArchitectureTest catches `IgnoreQueryFilters()` in CI |
| R-02 | SQL injection bypasses EF | Low | Critical | Raw SQL forbidden; WAF SQL rules; RLS backstop |
| R-03 | JWT private key exposure | Low | Catastrophic | Key in AWS KMS HSM; rotation policy; never in code |
| R-04 | Cache key collision between tenants | Very Low | High | GUID (128-bit) first segment; architecture test validates format |
| R-05 | Migration failure in production | Medium | High | Migrator Job exit code gate; RDS snapshot pre-migration; rollback runbook |
| R-06 | Background job forgets tenant scope | Medium | Critical | ArchitectureTest validates all jobs call `SetTenant()`; logging |
| R-07 | Dedicated DB connection string exposed | Low | High | Secrets Manager only; app_user never has ARN access |
| R-08 | Subscription limit bypass | Low | Medium | Short cache TTL (5 min); DB count always authoritative |
| R-09 | Redis cache poisoning | Very Low | High | VPC-private; Redis AUTH password; no internet access |
| R-10 | Storage quota bypass | Medium | Low | Pre-check before presigned URL; S3 Object Lambda (Phase 2) |
| R-11 | Tenant bleed via SignalR | Low | High | Groups = TenantId GUID; membership validated at connection |
| R-12 | GDPR non-compliance on deletion | Low | High | Deletion job anonymizes PII; financial records retained but delinked |

**Residual risk after mitigations: Acceptable for MVP launch.**

---

## 24. Testing Strategy

### 24.1 Mandatory Isolation Tests (IntegrationTests/MultiTenancy/)

```
DATA ISOLATION:
  T-001: TenantA_CannotRead_TenantB_Animals
  T-002: TenantA_CannotRead_TenantB_AnimalById → assert 404 (not 403)
  T-003: TenantA_CannotWrite_TenantB_Data → assert 404 + DB unchanged
  T-004: SaveInterceptor_RejectsCrossTenantInsert → assert SecurityException
  T-005: SqlRls_BlocksCrossTenantQuery_EvenWithFilterDisabled → 0 rows

CACHE ISOLATION:
  T-006: CacheKey_TenantA_CannotReturn_TenantB_Data → assert cache miss
  T-007: CacheInvalidation_OnlyInvalidates_OwningTenant

SUBSCRIPTION ENFORCEMENT:
  T-008: AnimalCount_AtLimit_BlocksNewRegistration → assert 402
  T-009: GracePeriod_BlocksWrites_AllowsReads
  T-010: Suspended_BlocksAllOperations → assert 402

BACKGROUND JOB ISOLATION:
  T-011: VaccinationReminderJob_OnlyNotifies_OwnTenant
  T-012: PerTenantJob_WithoutSetTenant_ThrowsOnExecution

USER SCOPE ISOLATION:
  T-013: FarmScoped_User_CannotAccess_OtherFarm → assert 403
  T-014: Worker_Role_CannotAccess_FinanceData → assert 403
```

### 24.2 Pre-Launch Security Penetration Tests

```
□ IDOR: GUID enumeration across all 46 entity types
□ JWT manipulation: forge tenant_id claim (RS256 prevents this)
□ SQL injection: all input fields including filter parameters
□ Rate limiting: verify 429 fires; bypass attempts fail
□ Token theft: use valid token after password change → 401
□ Cache poisoning: adjacent tenant prefix attempt
□ Authorization bypass: all 47 permissions across all 6 roles
□ RLS bypass: SQL Profiler verification of cross-tenant rejection
□ S3 path traversal: ../tenants/{other}/ via upload endpoint
□ SignalR group injection: join another tenant's notification group
```

### 24.3 Architecture Test Rules (enforced in CI)

```
[1] No feature handler calls IgnoreQueryFilters()
[2] No Application.Features.* type references Infrastructure.* types
[3] All ITenantEntity entities extend AuditableEntity
[4] All domain entities have private/init setters (no public)
[5] All background job handlers implement IJobHandler interface
[6] All cache calls go through ICacheService (not Redis directly)
[7] No Endpoints.* type instantiates DbContext or Repository
[8] All cache keys created via CacheKeyBuilder (not string literals)
```

---

## Architecture Finalization Checklist

Before implementation begins:

- [ ] Tenancy model (Shared DB for MVP) approved
- [ ] 6-layer isolation stack reviewed by Security Lead
- [ ] Tenant resolution pipeline approved
- [ ] SQL RLS scripts reviewed by DBA
- [ ] Subscription state machine approved by Product
- [ ] Cache TTL policy approved (performance vs. consistency balance)
- [ ] Storage quota design approved by DevOps
- [ ] Migration maintenance window agreed with Operations
- [ ] Risk register (R-03 key exposure) mitigation confirmed with Security
- [ ] Testing strategy approved by QA Lead

---

*This document is the authoritative reference for all multi-tenancy design on Farm360 AI.*  
*Governed by: F360-CONST-2026-001 — Project Constitution.*  
*© 2026 Farm360 AI Engineering Organization. All Rights Reserved.*
