# Farm360 AI — Comprehensive Feature Completion Roadmap

> **Last Updated:** August 20, 2026
> **Architect:** Principal Product Architect / Senior .NET & Angular Architect
> **Scope:** Complete all remaining MVP features (PRD F-048 → F-080 + Platform gaps)
> **Current Tests:** 159/159 Passing (87 Domain + 63 Application + 7 Architecture + 1 Integration + 1 Functional)

---

## User Review Required

> [!IMPORTANT]
> **Executive Approval Needed**
> This plan covers **6 strategic sprints** spanning the remaining ~45 incomplete MVP features.
> Sprints are ordered by business value and technical dependency.
> Please review the scope, phasing, and open questions below, then approve to proceed.

> [!WARNING]
> **Breaking Domain Changes in Sprint 3**
> The Finance module requires expanding `TransactionCategory` enum and adding new domain entities (`AccountCategory`, `AnimalCostLedger`, `LoanRecord`). Existing `FinancialTransaction` records remain compatible, but a new EF Core migration is required.

---

## Open Questions

> [!IMPORTANT]
> **Q1 — Finance: Chart of Accounts Structure**
> The PRD (FR-FM-01) requires a "pre-configured chart of accounts." Two approaches:
> - **Option A (Recommended):** Enum-based `AccountCategory` with pre-seeded categories (Animal Purchase, Feed Cost, Vet Cost, Labor, Utilities, Transport, Misc Expense, Animal Sale, Milk Sale, Byproduct Sale). Simpler, faster to build, sufficient for MVP.
> - **Option B:** A full `ChartOfAccounts` entity allowing user-defined hierarchical account trees. More flexible but significantly more complex.
> **Recommendation:** Option A for MVP, with Option B planned for post-MVP.

> [!IMPORTANT]
> **Q2 — Export: PDF/Excel Library**
> FR-FM-10 and FR-DA-10 require PDF and Excel export. Options:
> - **Option A (Recommended):** `QuestPDF` (MIT) for PDF + `ClosedXML` (MIT) for Excel. Both .NET-native, no external dependencies.
> - **Option B:** Frontend-only export using `jsPDF` + `SheetJS`. Simpler backend but limited formatting.
> **Recommendation:** Option A (server-side generation for consistent formatting and multi-page reports).

> [!IMPORTANT]
> **Q3 — Dashboard Charts: Charting Library**
> FR-DA-03 through FR-DA-06 require interactive charts (herd composition, ADG trends, feed cost trends, vaccination compliance).
> - **Option A (Recommended):** `Chart.js` via `ng2-charts` — lightweight, widely used, excellent Angular integration.
> - **Option B:** `Apache ECharts` via `ngx-echarts` — more powerful but heavier bundle.
> **Recommendation:** Option A for MVP.

---

## Sprint Overview

| Sprint | Name | Duration | PRD Features | Priority |
|---|---|---|---|---|
| **1** | Finance & Accounting Foundation | ~5 days | F-058 → F-070 | 🔴 Critical |
| **2** | Inventory Module Completion | ~3 days | F-048 → F-057 gaps | 🟠 High |
| **3** | Executive Dashboard & Analytics | ~4 days | F-071 → F-080 | 🟠 High |
| **4** | Intelligence UI & Breed Integration | ~3 days | Doc 29 Phases 2.5/3 | 🟡 Medium |
| **5** | Platform Core Features | ~4 days | F-007, F-009, F-012 | 🟡 Medium |
| **6** | Polish, Export & Quality Assurance | ~3 days | F-069, F-010, F-013, QA | 🟢 Low |

**Total Estimated Duration: ~22 working days**

---

## Sprint 1: Finance & Accounting Module (F-058 → F-070)

> **Goal:** Transform the basic financial transaction logger into a full-featured farm accounting module with Chart of Accounts, Per-Animal Cost Ledger, P&L Reports, Break-Even Calculator, and Financial Dashboard.

### Current State Assessment
- ✅ `FinancialTransaction` aggregate root exists with basic `Create()` factory
- ✅ `TransactionType` enum (Income, Expense)
- ✅ `TransactionCategory` enum (7 categories — partially aligned with PRD)
- ✅ Basic `CreateFinancialTransactionCommand` + handler
- ✅ `GetFinancialTransactionsQuery` + `GetFinancialTransactionSummaryQuery`
- ✅ Auto-posting event handlers (`AnimalSoldEventHandler`, `PurchaseOrderFulfilledEventHandler`)
- ✅ Angular Finance Ledger page + basic finance dashboard component
- ❌ Missing: Chart of Accounts hierarchy, Per-Animal Cost Ledger, P&L Reports, Break-Even, ROI, Loan Tracking, Multi-Farm Consolidation, PDF/Excel Export

---

### 1.1 Domain Layer Enhancements

#### [MODIFY] [TransactionCategory.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Domain/Finance/Enums/TransactionCategory.cs)
Expand the enum to match PRD FR-FM-01's pre-configured chart of accounts:
```csharp
public enum TransactionCategory
{
    // Expense Categories
    AnimalPurchase,
    FeedCost,
    VeterinaryCost,
    LaborCost,
    Utilities,
    Transport,
    MiscellaneousExpense,
    InventoryPurchase,
    MedicineCost,
    
    // Income Categories
    AnimalSale,
    MilkSale,
    ByproductSale,
    OtherIncome,
    
    // System (auto-posted)
    LoanDisbursement,
    LoanRepayment
}
```

#### [NEW] `Farm360.Domain/Finance/AnimalCostLedger.cs`
Per-animal cost accumulation entity (FR-FM-06). Tracks running total cost from acquisition to disposal:
- `AnimalId`, `FarmId`, `AcquisitionCostBdt`, `TotalFeedCostBdt`, `TotalVetCostBdt`, `TotalOverheadBdt`, `TotalCostBdt` (computed)
- Domain method: `RecordCost(TransactionCategory, decimal amount)` — increments the appropriate bucket
- Domain method: `GetBreakEvenPricePerKg(decimal currentWeightKg)` — returns `TotalCostBdt / currentWeightKg`

#### [NEW] `Farm360.Domain/Finance/LoanRecord.cs`
Loan/investment tracking entity (FR-FM-12, FR-FM-13):
- `LenderName`, `PrincipalAmountBdt`, `InterestRatePercent`, `DisbursementDate`, `RepaymentSchedule` (Monthly/Quarterly), `TotalRepaidBdt`, `OutstandingBalanceBdt`
- Domain method: `RecordRepayment(decimal amount, DateTime date)`

#### [MODIFY] [FinancialTransaction.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Domain/Finance/FinancialTransaction.cs)
Add nullable `AnimalId`, `BatchId`, `ShedId` link fields for entity-level cost tracking (FR-FM-02, FR-FM-03). Add domain event `FinancialTransactionCreatedEvent` to trigger `AnimalCostLedger` updates.

---

### 1.2 Persistence Layer

#### [NEW] `Farm360.Persistence/Configurations/Finance/AnimalCostLedgerConfiguration.cs`
EF Core configuration with unique index on `(TenantId, AnimalId)`.

#### [NEW] `Farm360.Persistence/Configurations/Finance/LoanRecordConfiguration.cs`
EF Core configuration for the loan entity.

#### [MODIFY] `Farm360.Persistence/ApplicationDbContext.cs`
Register `DbSet<AnimalCostLedger>` and `DbSet<LoanRecord>`. Apply new EF Core migration `AddFinanceModuleEnhancements`.

#### [NEW] `Farm360.Domain/Finance/Interfaces/IAnimalCostLedgerRepository.cs` + Implementation
- `GetByAnimalIdAsync()`, `GetByFarmIdAsync()`, `GetByBatchIdAsync()`

#### [NEW] `Farm360.Domain/Finance/Interfaces/ILoanRecordRepository.cs` + Implementation

---

### 1.3 Application Layer (CQRS)

#### [NEW] Commands
| Command | Purpose | PRD |
|---|---|---|
| `RecordIncomeCommand` | Manual income entry with entity links | FR-FM-02 |
| `RecordExpenseCommand` | Manual expense entry with entity links | FR-FM-03 |
| `CreateLoanRecordCommand` | Record loan/investment | FR-FM-12 |
| `RecordLoanRepaymentCommand` | Track repayment | FR-FM-13 |

#### [NEW] Queries
| Query | Purpose | PRD |
|---|---|---|
| `GetAnimalCostLedgerQuery` | Per-animal cost breakdown | FR-FM-06 |
| `GetBatchPnLReportQuery` | Batch-level P&L: total income, cost, gross profit, ROI% | FR-FM-07 |
| `GetMonthlyPnLReportQuery` | Monthly P&L by farm, by category | FR-FM-08 |
| `GetConsolidatedPnLReportQuery` | Multi-farm consolidated P&L | FR-FM-14 |
| `GetBreakEvenCalculatorQuery` | Break-even sale price per animal | FR-FM-09 |
| `GetFinancialDashboardQuery` | Revenue MTD, Expenses MTD, Net Profit, MoM comparison | FR-FM-11 |
| `GetLoansQuery` | List loans with outstanding balances | FR-FM-13 |

#### [NEW] Event Handlers (Auto-Posting — FR-FM-04, FR-FM-05)
| Handler | Trigger Event | Effect |
|---|---|---|
| `FeedConsumptionPostedHandler` | `FeedConsumptionLoggedEvent` | Posts Feed Cost expense + updates AnimalCostLedger |
| `MedicineUsedPostedHandler` | `TreatmentLoggedEvent` | Posts Vet Cost expense + updates AnimalCostLedger |
| `AnimalCostLedgerUpdater` | `FinancialTransactionCreatedEvent` | Accumulates cost into AnimalCostLedger |

---

### 1.4 API Layer

#### [MODIFY] [FinanceEndpoints.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Api/Endpoints/Finance/FinanceEndpoints.cs)
Add new endpoints:
- `POST /api/v1/farms/{farmId}/income` — Manual income recording
- `POST /api/v1/farms/{farmId}/expenses` — Manual expense recording
- `GET /api/v1/farms/{farmId}/animals/{animalId}/cost-ledger` — Per-animal cost
- `GET /api/v1/farms/{farmId}/batches/{batchId}/pnl` — Batch P&L
- `GET /api/v1/farms/{farmId}/reports/monthly-pnl?month=&year=` — Monthly P&L
- `GET /api/v1/reports/consolidated-pnl?month=&year=` — Multi-farm consolidated P&L
- `GET /api/v1/farms/{farmId}/animals/{animalId}/break-even` — Break-even calculator
- `GET /api/v1/farms/{farmId}/finance-dashboard` — Financial dashboard KPIs
- `POST /api/v1/farms/{farmId}/loans` — Create loan
- `POST /api/v1/farms/{farmId}/loans/{loanId}/repayments` — Record repayment
- `GET /api/v1/farms/{farmId}/loans` — List loans

---

### 1.5 Angular UI

#### [MODIFY] [finance.routes.ts](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/finance/finance.routes.ts)
Add routes: `/finance/dashboard`, `/finance/income`, `/finance/expenses`, `/finance/reports/batch-pnl`, `/finance/reports/monthly-pnl`, `/finance/loans`, `/finance/animal-cost/:animalId`

#### [NEW] Pages & Components
| Component | Description | PRD |
|---|---|---|
| `FinanceDashboardPage` | Revenue/Expense/Profit KPI cards with MoM comparison, mini sparkline charts | FR-FM-11 |
| `IncomeFormDialog` | Premium dialog for manual income entry with category dropdown, entity picker | FR-FM-02 |
| `ExpenseFormDialog` | Premium dialog for manual expense entry | FR-FM-03 |
| `AnimalCostLedgerPage` | Per-animal cost breakdown with stacked bar chart | FR-FM-06 |
| `BatchPnLReportPage` | Batch P&L table with ROI% highlight | FR-FM-07 |
| `MonthlyPnLReportPage` | Monthly P&L with category breakdown | FR-FM-08 |
| `LoanListPage` | Loan tracking with repayment progress bars | FR-FM-12, FM-13 |
| `BreakEvenCalculatorWidget` | Inline widget on animal detail showing break-even price | FR-FM-09 |

---

## Sprint 2: Inventory Module Completion (F-048 → F-057 gaps)

> **Goal:** Close remaining Inventory gaps: Stock Write-Off workflow, Inventory Valuation Report UI, Expiry Alert engine, and Inventory Movement Report.

### Current State Assessment
- ✅ `InventoryItem` aggregate with WAC calculation, `ReceiveStock()`, `DeductStock()`, `AdjustStock()`
- ✅ `StockTransaction` with `WriteOff` enum value already defined
- ✅ `PurchaseOrder` aggregate with full lifecycle (Draft → Approval → Fulfill)
- ✅ `GetInventoryValuationReportQuery` backend exists
- ✅ Angular: Dashboard, Item List, Current Stock, Stock Ledger, Supplier List, PO CRUD pages
- ❌ Missing: Stock Write-Off dedicated UI workflow, Expiry Date Alert Engine, Inventory Movement Report (date-range: opening/received/consumed/closing)

---

### 2.1 Domain Layer

#### [NEW] `Farm360.Domain/Inventory/Events/StockWriteOffEvent.cs`
Domain event for write-off tracking with reason (`Damaged`, `Expired`, `Lost`).

#### [MODIFY] [InventoryItem.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Domain/Inventory/InventoryItem.cs)
Add `WriteOffStock(decimal quantity, string reason, Guid transactionId)` domain method that calls `DeductStock()` internally and raises `StockWriteOffEvent`.

---

### 2.2 Application Layer

#### [NEW] Commands
| Command | Purpose | PRD |
|---|---|---|
| `RecordStockWriteOffCommand` | Write-off with reason (damaged/expired/lost) | FR-IV-09 |

#### [NEW] Queries
| Query | Purpose | PRD |
|---|---|---|
| `GetInventoryMovementReportQuery` | Opening + Received + Consumed + Closing for date range | FR-IV-11 |
| `GetExpiringItemsQuery` | Items expiring within configurable days (default 30) | FR-IV-06 |

#### [NEW] Background Workers
| Worker | Purpose | PRD |
|---|---|---|
| `ExpiryAlertBackgroundService` | .NET Hosted Service — daily scan for items expiring within threshold, creates `ActionableInsight` alerts | FR-IV-06 |

---

### 2.3 Angular UI

#### [NEW] Pages & Components
| Component | Description | PRD |
|---|---|---|
| `StockWriteOffDialog` | Premium dialog: select item, enter quantity, select reason enum, notes | FR-IV-09 |
| `InventoryValuationReportPage` | Table with WAC valuation per item, category totals, grand total | FR-IV-08 |
| `InventoryMovementReportPage` | Date-range picker + table (Opening Stock, Received, Consumed, Write-Off, Closing Stock) | FR-IV-11 |
| `ExpiringItemsPanel` | Widget on Inventory Dashboard showing items expiring within 30 days with urgency badges | FR-IV-06 |

---

## Sprint 3: Executive Dashboard & Analytics (F-071 → F-080)

> **Goal:** Build the full Executive Dashboard with interactive charts, per-farm summary cards, activity feed, and export capabilities.

### Current State Assessment
- ✅ `GetExecutiveDashboardQuery` backend with KPI metrics (total animals, sick, low stock, income, expense, births, deaths, vaccinations, pregnancies, insights)
- ✅ Angular `ExecutiveDashboardComponent` with KPI cards and insight cards
- ✅ Analytics queries scaffolded (`GetBreedingAnalyticsQuery`, `GetFinanceAnalyticsQuery`, `GetHealthAnalyticsQuery`)
- ❌ Missing: Interactive charts (herd composition, ADG trends, feed cost trends, vaccination compliance), per-farm summary cards with drilldown, activity feed, date range filtering, PDF/PNG export

---

### 3.1 Application Layer

#### [NEW] Queries
| Query | Purpose | PRD |
|---|---|---|
| `GetHerdCompositionQuery` | Breakdown by species, breed, sex, age group, status | FR-DA-03 |
| `GetAdgTrendQuery` | ADG trend data by batch over configurable time periods | FR-DA-05 |
| `GetFeedCostTrendQuery` | Feed cost per animal per day trend data | FR-DA-06 |
| `GetVaccinationComplianceQuery` | Due, overdue, completed vaccination counts | FR-DA-04 |
| `GetFarmSummaryCardsQuery` | Per-farm summary with animal count, health status, revenue | FR-DA-02 |
| `GetRecentActivityFeedQuery` | Last 20 platform actions from AuditLog | FR-DA-08 |

---

### 3.2 Angular UI — Install `ng2-charts` + `chart.js`

```bash
npm install ng2-charts chart.js --save
```

#### [MODIFY] [executive-dashboard.component.html](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/dashboard/pages/executive-dashboard/executive-dashboard.component.html)
Major redesign to include chart widgets and drill-down cards.

#### [NEW] Dashboard Widgets
| Widget | Description | PRD |
|---|---|---|
| `HerdCompositionChartWidget` | Doughnut/pie charts for species, breed, sex, age, status | FR-DA-03 |
| `AdgTrendChartWidget` | Line chart with batch-level ADG trends (solid = actual, dotted = projected) | FR-DA-05 |
| `FeedCostTrendChartWidget` | Line chart showing feed cost per animal per day | FR-DA-06 |
| `VaccinationComplianceChartWidget` | Stacked bar chart (Due, Overdue, Completed) | FR-DA-04 |
| `FarmSummaryCardGrid` | Per-farm summary cards with click-to-drilldown | FR-DA-02 |
| `ActivityFeedWidget` | Recent activity feed (last 20 actions) with user avatars and timestamps | FR-DA-08 |
| `InventoryAlertsPanel` | Low stock + near-expiry items panel | FR-DA-07 |
| `FinancialSnapshotWidget` | Revenue, Expense, Net Profit cards with sparklines | FR-DA-01 |
| `DateRangeFilterBar` | Global date range picker applied across all widgets | FR-DA-09 |

---

## Sprint 4: Intelligence UI & Breed Integration (Doc 29 Phases 2.5/3)

> **Goal:** Surface the already-built Intelligence engines (Growth Prediction, Cost & Profit, Rule Engine, Simulation) in the Angular frontend with rich "Intelligence Panels" on animal profiles and batch views.

### Current State Assessment
- ✅ `Breed` aggregate root fully built with growth/FCR/dairy metrics
- ✅ `IBreedRepository` with full CRUD + paged query
- ✅ `GrowthPredictionEngine` — ADG calculation with breed baseline fallback
- ✅ `CostAndProfitEngine` — daily feed cost estimation, total investment calculation
- ✅ `RuleEngine` — underperformance detection generating `ActionableInsight`
- ✅ `SimulationEngine` — "What-If" sale date simulation
- ✅ Intelligence API endpoints (`/api/v1/intelligence/*`) exist
- ❌ Missing: Angular "Intelligence Panel" on Animal Detail, Breed Management Setup UI, "What-If" Simulator UI, SignalR push for real-time insights

---

### 4.1 Angular UI — Breed Management

#### [NEW] `features/livestock/pages/breed-list/`
Paginated breed list with search, category/purpose filters. Grid cards with gradient icon badges, growth metric previews.

#### [NEW] `features/livestock/pages/breed-form/`
Full CRUD form for breed creation/editing. Sections: Basic Info, Growth Targets (ADG by condition), Feed Efficiency (FCR), Dairy Metrics.

---

### 4.2 Angular UI — Intelligence Panels

#### [NEW] `features/livestock/components/intelligence-panel/`
Embedded panel on `AnimalDetailComponent` showing:
- **Growth Curve Card**: Current Weight → Projected 30/60/90-day weights (with ADG indicator vs breed target)
- **Cost & Investment Card**: Total Investment BDT, Projected 30-Day Feed Cost, Break-Even Price/Kg
- **Insights Card**: Active `ActionableInsight` alerts for this animal (from Rule Engine)
- **"What-If" Sale Simulator**: Date picker → Projected Weight, Sale Price, Total Cost, Profit Margin

#### [NEW] `features/livestock/components/batch-intelligence-panel/`
Aggregate intelligence for batch-level views — average ADG, batch cost, batch P&L projection.

---

## Sprint 5: Platform Core Features (F-007, F-009, F-012)

> **Goal:** Implement missing platform features: User/Team Management, Audit Log Viewer, and i18n (Bangla/English).

### Current State Assessment
- ✅ Backend: `TenantUser`, `Role`, `Permission`, `RolePermission` entities exist
- ✅ Backend: `AuditLog` entity and `AuditSaveChangesInterceptor` exist
- ❌ Missing: Angular UI for user invitation & team management
- ❌ Missing: Angular Audit Log Viewer page
- ❌ Missing: i18n/localization infrastructure (Bangla + English)

---

### 5.1 User & Team Management (F-007)

#### [NEW] Backend
- `InviteUserCommand` — Sends invitation email/SMS with OTP to join tenant
- `GetTenantUsersQuery` — Paginated list of users with role assignments
- `UpdateUserRoleCommand` — Assign/change user roles
- `DeactivateUserCommand` — Soft deactivation

#### [NEW] Angular UI
| Component | Description |
|---|---|
| `UserListPage` | Paginated user list with role badges, status indicators, and action buttons |
| `InviteUserDialog` | Premium dialog for inviting by phone/email, role assignment |
| `UserRoleEditorDialog` | Role assignment dropdown with permission preview |

---

### 5.2 Audit Log Viewer (F-012)

#### [NEW] `GetAuditLogsQuery` — Paginated query with date range, action type, and user filters
#### [NEW] `AuditLogListPage` — Filterable, searchable table showing entity changes, before/after values, actor, and timestamp

---

### 5.3 Internationalization (F-009)

- Install `@angular/localize` and configure `angular.json` for `bn` (Bangla) and `en` (English) locales
- Extract translation keys using `ng extract-i18n`
- Create `messages.bn.xlf` for Bangla translations
- Add language toggle in `HeaderComponent`
- Use `$localize` template literals in all user-facing text

---

## Sprint 6: Export, Polish & Quality Assurance

> **Goal:** Implement PDF/Excel export, visual polish pass, and comprehensive test coverage.

### 6.1 Server-Side Report Export (F-069, F-010)

#### [NEW] Backend: `Farm360.Infrastructure/Reporting/`
- Install `QuestPDF` (NuGet) for PDF generation
- Install `ClosedXML` (NuGet) for Excel (XLSX) generation
- Create `IReportExporter` interface with `ExportToPdfAsync()` and `ExportToExcelAsync()`
- Implement exporters for: Monthly P&L, Batch P&L, Inventory Valuation, Animal Cost Ledger

#### [NEW] API Endpoints
- `GET /api/v1/farms/{farmId}/reports/monthly-pnl/export?format=pdf|xlsx`
- `GET /api/v1/farms/{farmId}/reports/batch-pnl/export?format=pdf|xlsx`
- `GET /api/v1/farms/{farmId}/reports/inventory-valuation/export?format=pdf|xlsx`

#### [NEW] Angular: Export buttons on all report pages
Download buttons with format selector (PDF/Excel) on BatchPnL, MonthlyPnL, InventoryValuation, and AnimalCostLedger pages.

---

### 6.2 Dashboard Export (FR-DA-10)
- Frontend-only PNG export using `html2canvas` for dashboard widgets
- "Export Dashboard" button in `DateRangeFilterBar`

---

### 6.3 Unit & Integration Tests

| Test Suite | Scope | Estimated Tests |
|---|---|---|
| `Finance.Domain.UnitTests` | AnimalCostLedger rules, LoanRecord repayment, TransactionCategory validation | ~15 tests |
| `Finance.Application.UnitTests` | All new command/query handlers, validators | ~25 tests |
| `Inventory.Application.UnitTests` | WriteOff, Movement Report, Expiry queries | ~10 tests |
| `Dashboard.Application.UnitTests` | Chart data queries, Activity Feed | ~10 tests |
| `Intelligence.Application.UnitTests` | Simulation engine edge cases | ~8 tests |
| `Architecture.Tests` | Verify new layers follow dependency rules | Update existing 7 |

**Target: ~70 new tests → Total: ~230 tests**

---

### 6.4 Visual Polish Pass
- Ensure all new pages follow glassmorphic container pattern
- Verify dark mode rendering across all new components
- Ensure all forms use Premium Dialog Pattern
- Ensure all list pages use `<app-page-header>`, `<app-loading>`, `<app-empty-state>`
- Responsive layout verification (mobile, tablet, desktop)

---

## Verification Plan

### Automated Tests
```bash
# Backend
dotnet build
dotnet test --logger "console;verbosity=detailed"

# Frontend
cd src/Farm360.Web
npm run build -- --configuration production
ng test --watch=false --browsers=ChromeHeadless
```

### Manual Verification
- [ ] Create a financial transaction (Income + Expense) and verify ledger
- [ ] Verify auto-posting: record animal sale → verify income transaction auto-created
- [ ] Verify auto-posting: log feed consumption → verify expense auto-posted
- [ ] View per-animal cost ledger and verify cost accumulation
- [ ] Generate Batch P&L report and verify ROI calculation
- [ ] Record stock write-off and verify inventory balance update
- [ ] View inventory movement report with date range
- [ ] View executive dashboard with all chart widgets populated
- [ ] Export Monthly P&L as PDF and Excel
- [ ] Test "What-If" sale simulator with different target dates
- [ ] Verify Breed Management CRUD
- [ ] Test dark mode across all new pages
- [ ] Verify mobile responsiveness

---

## Proposed Changes Summary

| Sprint | New Files | Modified Files | New Tests |
|---|---|---|---|
| **1. Finance** | ~25 | ~8 | ~40 |
| **2. Inventory** | ~8 | ~4 | ~10 |
| **3. Dashboard** | ~15 | ~5 | ~10 |
| **4. Intelligence UI** | ~10 | ~3 | ~8 |
| **5. Platform** | ~12 | ~4 | ~5 |
| **6. Export & QA** | ~8 | ~6 | ~5 |
| **Total** | **~78** | **~30** | **~78** |
