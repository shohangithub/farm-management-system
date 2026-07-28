# Smart Feeding & Nutrition Module — Implementation Plan

Following a comprehensive production-readiness audit across both backend and frontend, **Livestock Management** and **Health & Vaccination** modules have met 100% of functional, architectural, multi-tenancy, security, UI/UX, and testing requirements. Both modules are officially certified as **Production Ready** ✅.

Per the Module Dependency Map (PRD §6.1 & Database Design §13), the next logical module to implement is **Smart Feeding & Nutrition**.

---

## 🎯 Audit & Certification Summary

| Module | Audit Criteria | Result | Status |
|---|---|---|---|
| **Livestock Module** | DDD Domain Rules, Batch Filtering, Photo Storage, Weight & BCS Tracking, CQRS, Permissions (`animals.view`, etc.), Signal Reactivity, OnPush Detection, Unit Tests (76/76) | PASSED | **Production Ready** ✅ |
| **Health & Vaccination Module** | Mortality Status Propagation (`AnimalStatus.Dead`), Pre-validation & DB Duplicate Index (`409 Conflict`), ProblemDetails Parsing (`error-parser.ts`), Deworming, Withdrawal Tracking, Signal Reactivity, Unit Tests (63/63) | PASSED | **Production Ready** ✅ |

---

## 🚀 Smart Feeding & Nutrition Module Overview

The **Smart Feeding & Nutrition Module** provides intelligent feed formulation, rationing models, feed consumption tracking, Feed Conversion Ratio (FCR) analytics, cost calculations, and feeding schedule management across sheds, pens, and animal batches.

### Business Objectives
1. **Feed Ingredient Catalog & Formulator**: Manage pre-loaded standard ingredients (rice straw, mustard oil cake, maize, concentrate mix) with dry matter (DM%), crude protein (CP%), and metabolizable energy (ME) attributes.
2. **Feed Formula Builder**: Create custom feed formulas/rations with calculated total nutritional profile and cost per kg.
3. **Feeding Schedule Manager**: Assign formulas to sheds, pens, or animal batches with target daily quantity per head and frequency.
4. **Daily Feed Consumption Logger**: Record actual daily feed consumed, automatically calculating variance/deviation against target schedules.
5. **FCR & Performance Analytics**: Calculate Feed Conversion Ratio (FCR = Total Feed Consumed (kg) / Total Weight Gained (kg)) with interactive trend charts.

---

## 📐 Technical Architecture & Schema Design

### 1. Database Schema (`feeding` Schema)
- `feeding.FeedIngredients` (Aggregate Root): Ingredient details, dry matter, crude protein, metabolizable energy, unit cost (BDT).
- `feeding.FeedFormulas` (Aggregate Root): Formula title, species, target age/purpose, total cost per kg, DM%, CP%, ME.
- `feeding.FormulaIngredients` (Owned Entity / Table): FormulaId, IngredientId, Percentage, QuantityKg, CostContribution.
- `feeding.FeedingSchedules` (Entity): FarmId, ShedId/PenId/BatchId reference, FormulaId, TargetQuantityKgPerHead, DailyFrequency, StartDate, EndDate.
- `feeding.FeedConsumptionLogs` (Aggregate Root): FarmId, ShedId/PenId/BatchId reference, LogDate, TotalFeedOfferedKg, TotalRefusalKg (Wastage), NetConsumptionKg, CalculatedCostBdt.

### 2. CQRS Commands & Queries (`Farm360.Application/Feeding`)
- **Commands**:
  - `CreateFeedIngredientCommand` / `UpdateFeedIngredientCommand`
  - `CreateFeedFormulaCommand` / `UpdateFeedFormulaCommand`
  - `CreateFeedingScheduleCommand` / `UpdateFeedingScheduleCommand`
  - `LogFeedConsumptionCommand`
- **Queries**:
  - `GetFeedIngredientsQuery`
  - `GetFeedFormulasQuery` / `GetFeedFormulaDetailQuery`
  - `GetFeedingSchedulesQuery`
  - `GetDailyFeedConsumptionQuery`
  - `GetFcrAnalyticsQuery` (Calculates FCR metrics per shed/batch over time)

### 3. API Endpoints (`/api/v1/feeding`)
- `GET /api/v1/feeding/ingredients` & `POST /api/v1/feeding/ingredients`
- `GET /api/v1/feeding/formulas` & `POST /api/v1/feeding/formulas`
- `GET /api/v1/feeding/schedules` & `POST /api/v1/feeding/schedules`
- `GET /api/v1/feeding/consumption` & `POST /api/v1/feeding/consumption`
- `GET /api/v1/feeding/analytics/fcr`

### 4. Angular UI Pages & Components (`src/app/features/feeding`)
- `FeedingDashboardComponent`: Overall feeding KPIs, daily feed consumption, cost trends, FCR summary.
- `IngredientListComponent` & `IngredientDialogComponent`: Manage feed ingredients.
- `FormulaListComponent`, `FormulaDetailComponent` & `FormulaBuilderDialogComponent`: Dynamic ratio slider & nutritional calculation preview.
- `FeedingScheduleListComponent` & `ScheduleDialogComponent`: Schedule assignments to sheds/pens.
- `ConsumptionLogComponent` & `LogConsumptionDialogComponent`: Daily consumption logging with wastage metrics.

---

## 🛠️ User Review Required

> [!IMPORTANT]
> - **Pre-loaded Bangladeshi Feed Ingredients**: Shall we seed standard South Asian / Bangladeshi feed ingredients (e.g., Rice Straw, Mustard Oil Cake, Wheat Bran, Maize Silage, Green Grass, Molasses, Mineral Mix) into master seed data?
> - **Inventory Auto-Deduction Event Hooks**: When daily feed consumption is logged, domain events (`FeedConsumptionLoggedEvent`) will be emitted to allow seamless integration when the Inventory Module is built next.

---

## 📋 Implementation Order

1. **Phase 1: Domain & Persistence Layers**
   - Create `FeedIngredient`, `FeedFormula`, `FormulaIngredient`, `FeedingSchedule`, `FeedConsumptionLog` domain entities & events.
   - EF Core configurations under `Farm360.Persistence/Configurations/Feeding/` mapped to `feeding` schema.
   - Run EF Core Migration `AddFeedingModule`.

2. **Phase 2: Application CQRS & Validators**
   - Implement DTOs, FluentValidation validators, mapping profiles, and MediatR handlers.
   - Implement FCR calculator logic (`FcrAnalyticsQuery`).

3. **Phase 3: REST API & Authorization**
   - Implement `FeedingEndpoints.cs` with permission policies (`feeding.view`, `feeding.create`, `feeding.edit`).
   - Register feeding permissions in `PermissionConstants.cs` and `DataSeeder.cs`.

4. **Phase 4: Angular 22 Frontend Implementation**
   - Build `FeedingService`, models, routes, and Signal-based OnPush standalone components adhering strictly to `AGENTS.md`.

5. **Phase 5: Automated Testing & Documentation**
   - Add unit tests for Domain & Application handlers.
   - Update `CHANGELOG.md`, `DEVELOPMENT_STATUS.md`, and `TODO.md`.

---

## 🧪 Verification Plan

### Automated Tests
- Run `dotnet test` to execute all unit tests including new Feeding domain and application tests.
- Run `npm run build` in `src/Farm360.Web` to verify zero Angular compilation errors.

### Manual Verification
- Verify ingredient catalog seeding.
- Build a feed formula and check that total percentage equals 100% and nutritional values calculate correctly.
- Assign feeding schedule to a shed and log daily consumption.
- Verify FCR chart calculations.
