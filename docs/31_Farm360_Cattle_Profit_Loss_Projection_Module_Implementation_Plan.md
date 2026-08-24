# Farm360 AI — Cattle Profit / Loss Projection Module
## Implementation Plan (Excel-parity Fattening Simulator)

**Source of truth:** `Cattle_Profit_Loss_Formula_Structure.xlsx` (sheets: `Inputs`, `Daily Projection`, `Summary`)
**Target:** Livestock → Animal → **Profit Projection** (per-animal), later extended to Batch
**Status:** Plan — not yet implemented
**Branch suggestion:** `feature/profit-projection-engine`

---

## 1. What the spreadsheet actually does (reverse-engineered spec)

The workbook is a **deterministic, day-stepped fattening simulator**. There is no statistics or ML in it — 13 inputs drive a 120-row daily table, and the Summary reads the row at `day = FatteningPeriod`.

### 1.1 Inputs (`Inputs!B2:B13`)

| # | Input | Excel cell | Sample | Unit |
|---|-------|-----------|--------|------|
| 1 | Starting live weight | B2 | 200 | kg |
| 2 | Purchase price | B3 | 80,000 | BDT |
| 3 | Current meat price | B4 | 680 | BDT/kg meat |
| 4 | Initial meat yield | B5 | 0.50 | ratio of live weight |
| 5 | Daily live-weight gain (ADG) | B6 | 0.70 | kg/day |
| 6 | Meat yield on daily gain | B7 | 0.50 | ratio |
| 7 | Daily feed quantity | B8 | 3.0 | kg/day at starting weight |
| 8 | Feed price | B9 | 44.30 | BDT/kg |
| 9 | Daily grass cost | B10 | 20 | BDT/day |
| 10 | Daily other cost | B11 | 30 | BDT/day |
| 11 | Monthly labor cost | B12 | 1,500 | BDT/month |
| 12 | Fattening period | B13 | 120 | days |

### 1.2 Daily projection (row per day `d`, d = 1..N)

```
LiveWeight(d)      = StartWeight + ADG * d
MeatWeight(d)      = LiveWeight(d) * MeatYieldRatio
FeedQty(d)         = BaseFeedQty * (LiveWeight(d) / StartWeight)     // scales with body weight
FeedCost(d)        = FeedQty(d) * FeedPricePerKg
GrassCost(d)       = DailyGrassCost                                  // flat
OtherCost(d)       = DailyOtherCost                                  // flat
LaborCost(d)       = MonthlyLaborCost / 30                           // flat
DailyTotalCost(d)  = FeedCost + GrassCost + OtherCost + LaborCost
MeatGain(d)        = ADG * MeatYieldOnGain                           // informational column
MeatValue(d)       = MeatWeight(d) * MeatPricePerKg
CumulativeCost(d)  = sum of DailyTotalCost(1..d)
TotalInvestment(d) = PurchasePrice + CumulativeCost(d)
ProfitLoss(d)      = MeatValue(d) - TotalInvestment(d)
ProfitPct(d)       = TotalInvestment(d) == 0 ? 0 : ProfitLoss(d) / TotalInvestment(d)
```

**Behaviours to preserve exactly (parity contract):**
- Day index is **1-based** and day 1 already carries a full day of gain (there is no day-0 row).
- `FeedQty` is **weight-scaled**, not flat — this is the only non-linear term and the reason total cost grows quadratically in `d`.
- `LaborCost` uses a fixed **/30**, not calendar month length.
- `MeatGain` is an unused informational column (kept for UI transparency; it does **not** feed `MeatWeight`, which is recomputed from live weight each day).
- Division-by-zero guard exists on `ProfitPct` only.

### 1.3 Summary (`Summary!B2:B17`)

Reads the day-`N` row plus column totals: starting weight, final weight, total gain, purchase cost, total feed / grass / other / labor cost, total farming cost, total investment, final meat weight, expected sale value, profit/loss, profit %, **break-even selling price per live kg = TotalInvestment / FinalLiveWeight**, and the meat price used.

### 1.4 Closed form (used for O(1) break-even solving and as a test oracle)

```
FlatDaily         = GrassCost + OtherCost + MonthlyLabor/30
FeedRate          = FeedPricePerKg * BaseFeedQty / StartWeight    // BDT per kg-of-body-weight per day
SumLiveWeight(N)  = N*StartWeight + ADG * N(N+1)/2
CumulativeCost(N) = N * FlatDaily + FeedRate * SumLiveWeight(N)
ProfitLoss(N)     = MeatPrice*MeatYield*(StartWeight + ADG*N) - PurchasePrice - CumulativeCost(N)
```

This is a downward-opening quadratic in `N`, so there is always a single **optimal sale day** (`d* = argmax ProfitLoss`) — an insight the spreadsheet cannot give.

### 1.5 Golden numbers (verified against the sample inputs — use as test vectors)

| Day | Live wt (kg) | Meat (kg) | Feed qty | Daily cost | Cum. cost | Investment | Meat value | P/L | P/L % |
|-----|-------------|-----------|----------|-----------|-----------|-----------|-----------|-----|-------|
| 1 | 200.70 | 100.35 | 3.010 | 233.37 | 233.37 | 80,233.37 | 68,238.00 | −11,995.37 | −14.95% |
| 30 | 221.00 | 110.50 | 3.315 | 246.85 | 7,203.29 | 87,203.29 | 75,140.00 | −12,063.29 | −13.83% |
| 60 | 242.00 | 121.00 | 3.630 | 260.81 | 14,825.22 | 94,825.22 | 82,280.00 | −12,545.22 | −13.23% |
| 90 | 263.00 | 131.50 | 3.945 | 274.76 | 22,865.79 | 102,865.79 | 89,420.00 | −13,445.79 | −13.07% |
| **120** | **284.00** | **142.00** | **4.260** | **288.72** | **31,324.99** | **111,324.99** | **96,560.00** | **−14,764.99** | **−13.26%** |

Summary at N=120: total feed 19,324.99 · flat costs 12,000.00 · total farming 31,324.99 · investment 111,324.99 · **break-even 391.99 BDT/live-kg · 783.98 BDT/meat-kg** · optimal sale day **10** (P/L −11,974.58).

> **Business analyst note — surface this in the UI.** With the sample inputs the animal is under water on day 1 and never recovers: purchase price (80,000) already exceeds initial meat value (68,000), and break-even needs **784 BDT/meat-kg** against a market price of **680**. The module's job is therefore not just to show the number — it must show *which lever fixes it* (purchase-price ceiling, required meat price, required ADG, feed-cost cap). That is the difference between porting a spreadsheet and shipping decision support.

---

## 2. Where this lands in the existing architecture

The codebase already has an Intelligence slice with the right shape but **stubbed/mocked maths**:

| Existing | File | State |
|---|---|---|
| `ICostAndProfitEngine` / `CostAndProfitEngine` | `Farm360.Application/Intelligence/Services/CostAndProfitEngine.cs` | hard-coded 50 BDT/kg feed, FCR fallback 8 |
| `ISimulationEngine` / `SimulationEngine` | `Farm360.Application/Intelligence/Services/SimulationEngine.cs` | flat daily cost, hard-coded 500 BDT/kg |
| `GetAnimalFinancialSnapshotQueryHandler` | `Farm360.Application/Features/Intelligence/Queries/GetAnimalFinancialSnapshot/` | explicitly commented "STUB / mock" |
| `IGrowthPredictionEngine`, `GrowthCurve`, `ProfitMargin`, `CostProjection` | `Farm360.Domain/Intelligence/` | real, reusable |
| `IntelligenceEndpoints` | `Farm360.Api/Endpoints/IntelligenceEndpoints.cs` | 3 GETs, ready to extend |
| Angular Intelligence UI | none (animal-detail has MatTabs, no projection tab) | to build |

**Decision:** do **not** bolt the spreadsheet onto the mocked engines. Introduce one pure, tested calculator in the Domain, then make the existing mocked engines delegate to it. That kills three mocks and adds the feature in one pass.

### 2.1 Layering (Clean Architecture, matches `docs/3_Farm360_AI_Software_Architecture_Document.md`)

```
Domain       FatteningProjectionCalculator   <- pure, no I/O, no DI, no clock. The spreadsheet lives here.
Application  ProjectionDefaultsResolver      <- hydrates inputs from Animal/Breed/Inventory
             ProfitProjection queries/cmds   <- MediatR, scenario CRUD
Persistence  ProjectionScenario config + migration
Api          /api/v1/intelligence/... endpoints
Web          livestock/profit-projection feature (signals + echarts)
```

---

## 3. Implementation plan

### Phase 0 — Contract freeze (0.5 day)
**Owner:** Business Analyst + Farm Manager + Principal Architect

1. Confirm with the Farm Manager that the 12 inputs are complete for Bangladeshi fattening operations. Known candidates deliberately **out of scope for v1 parity** (log as backlog): mortality risk %, medicine/vaccination cost line, transport and sale commission, interest / cost of capital, dressing-percentage variance by breed, seasonal meat-price curve (Eid premium).
2. Sign off the golden vectors in §1.5 as the acceptance oracle.
3. Fix UI terminology: **"Profit Projection"** (per animal), inputs called **"Assumptions"**, saved sets called **"Scenarios"**.

**Exit criteria:** §1.2 formulas and §1.5 numbers signed off in this document.

---

### Phase 1 — Domain calculator (1.5 days)
**Owner:** Senior .NET Architect + Senior .NET Developer

**New files** under `src/Farm360.Domain/Intelligence/Projections/`:

- `FatteningProjectionInputs.cs` — `sealed record` with the 12 inputs, all `decimal`, plus `int ProjectionDays`.
- `FatteningProjectionDay.cs` — `sealed record` mirroring the 15 Excel columns (Day, LiveWeightKg, MeatWeightKg, FeedQtyKg, FeedCostBdt, GrassCostBdt, OtherCostBdt, LaborCostBdt, DailyTotalCostBdt, MeatGainKg, MeatValueBdt, CumulativeCostBdt, TotalInvestmentBdt, ProfitLossBdt, ProfitPercent).
- `FatteningProjectionSummary.cs` — the 16 Summary rows plus our additions: `BreakEvenPricePerMeatKgBdt`, `BreakEvenDay` (first day P/L >= 0, nullable), `OptimalSaleDay`, `OptimalProfitBdt`, `TotalFeedQtyKg`, `CostPerKgGainBdt`, `RoiPercent`, `DailyProfitRateBdt`.
- `FatteningProjectionResult.cs` — `Inputs`, `IReadOnlyList<FatteningProjectionDay> Days`, `Summary`.
- `FatteningProjectionCalculator.cs` — `public static FatteningProjectionResult Calculate(FatteningProjectionInputs inputs)`; single O(N) loop, `decimal` throughout, **no rounding inside the loop** (round only at the DTO boundary — the spreadsheet does not round, so parity requires we do not either).
- `FatteningProjectionValidator.cs` — guards: `StartWeight > 0`, `ProjectionDays` in `1..1095`, `MeatYield` and `MeatYieldOnGain` in `0..1`, non-negative money, `ADG >= 0`. Throw domain guard failures — never silently clamp.

**Also add** `SolveBreakEven(inputs, target)` using the §1.4 closed form for: required meat price, maximum viable purchase price, required ADG. These power the "what fixes it" UI.

**Tests** — `tests/Farm360.Domain.UnitTests/Intelligence/FatteningProjectionCalculatorTests.cs`:
- Golden-vector theory over the §1.5 table (tolerance 0.01 BDT).
- Closed-form vs. loop agreement over randomised valid inputs (500 cases).
- Edge cases: zero ADG, zero feed, `ProjectionDays = 1`, `TotalInvestment = 0` → `ProfitPct = 0`.
- Validator rejection cases.
- Invariants: cumulative cost strictly increasing; `OptimalSaleDay` is the true argmax.

**Exit criteria:** all golden vectors green; calculator has no project references beyond `Farm360.Domain.Common`; `Farm360.Architecture.Tests` still green.

---

### Phase 2 — Application layer and defaults resolver (2 days)
**Owner:** Senior .NET Developer + Business Analyst

**a) `IProjectionDefaultsResolver` / `ProjectionDefaultsResolver`** in `Farm360.Application/Intelligence/Services/`.
Resolves each input from real data with **explicit provenance**, so the UI can show where a number came from and the user can override any of them:

| Input | Primary source | Fallback chain |
|---|---|---|
| Starting live weight | `Animal.LatestWeightKg` | breed-standard weight-for-age → manual |
| Purchase price | `Animal.AcquisitionPriceBdt` | 0 (manual) |
| ADG | `Animal.AdgKgPerDay` | `GrowthCurve.CurrentAdgKg` → `Breed.AdgAverageFarm` → `Breed.StandardAdgMin` |
| Feed price / kg | weighted-average cost of the farm's active feed `InventoryItem.WeightedAverageCostBdt` (Category = Feed) | farm setting → manual |
| Daily feed qty | `ADG × Breed` FCR mid-point (`(FcrMin+FcrMax)/2`) | manual |
| Meat price / kg | farm-level `MasterDataEntry` (new type `MeatPriceBdtPerKg`) | manual |
| Meat yield ratios | new `Breed.DressingPercentage` (nullable) | 0.50 |
| Grass / other / labor | farm-level master-data defaults | manual |
| Fattening period | 120 | manual |

Return `ProjectionDefaultsDto` = value + `SourceCode` enum (`AnimalRecord`, `BreedStandard`, `InventoryAverage`, `FarmSetting`, `SystemDefault`) + human label per field. **Nothing is silently mocked** — a field with no data comes back as `SystemDefault` and is flagged in the UI.

**b) Queries / commands** in `Farm360.Application/Intelligence/`:
- `GetProjectionDefaultsQuery(Guid animalId)` → `ProjectionDefaultsResponse`
- `CalculateProfitProjectionQuery(Guid? animalId, FatteningProjectionInputsDto inputs, bool includeDailyRows)` → `ProfitProjectionResponse` — **stateless**, no DB write; `includeDailyRows=false` for dashboard cards
- `SolveBreakEvenQuery(inputs, BreakEvenTarget target)` → required-value response
- `SaveProjectionScenarioCommand` / `UpdateProjectionScenarioCommand` / `DeleteProjectionScenarioCommand` / `GetProjectionScenariosQuery` (Phase 3)
- `CompareProjectionScenariosQuery(Guid[] scenarioIds)` (Phase 5)

**c) Contracts** in `Farm360.Contracts/Intelligence/` — DTOs with rounding applied at this boundary only (money 2 dp, weight 3 dp, ratios 4 dp). FluentValidation validators mirroring the Domain guards, so the API fails fast with 400 + field errors.

**d) Retire the mocks** — `CostAndProfitEngine`, `SimulationEngine`, and `GetAnimalFinancialSnapshotQueryHandler` all delegate to `FatteningProjectionCalculator` + `ProjectionDefaultsResolver`. Delete the `450m` / `500m` / `+5000m` literals and their "mock" comments. Update the affected tests.

**Exit criteria:** no `mock` / `STUB` constants left in the Intelligence slice; integration test proves defaults resolve from a seeded animal + breed + feed item.

---

### Phase 3 — Persistence: saved scenarios (1 day)
**Owner:** Senior .NET Developer + Performance Engineer

- `Farm360.Domain/Intelligence/ProjectionScenario.cs` — `AuditableEntity`, tenant/farm-scoped, `AnimalId?` (null ⇒ farm-level template), `Name`, `Notes`, the 12 inputs as columns (**not** a JSON blob — we want to query and aggregate them), plus denormalised `SnapshotProfitBdt`, `SnapshotProfitPercent`, `SnapshotBreakEvenPerMeatKgBdt`, `CalculatedAtUtc` for fast list views.
- `IProjectionScenarioRepository` in Domain; implementation in `Farm360.Persistence/Repositories/Intelligence/`.
- EF config in `Farm360.Persistence/Configurations/Intelligence/ProjectionScenarioConfiguration.cs` — `decimal(18,4)` for money and weights, index on `(TenantId, FarmId, AnimalId)`, global tenant filter per `docs/8_Farm360_MultiTenant_Architecture.md`.
- Register in `ApplicationDbContext` and `PersistenceServiceExtensions`; add migration `AddProjectionScenarios`.
- Snapshot fields are recomputed on save only — never on read.

**Exit criteria:** migration applies clean up and down; repository integration tests green; tenant-isolation test proves cross-tenant reads return nothing.

---

### Phase 4 — API (0.5 day)
**Owner:** Senior .NET Developer

Extend `IntelligenceEndpoints.cs`:

```
GET    /api/v1/intelligence/animals/{animalId}/projection/defaults
POST   /api/v1/intelligence/projection/calculate            // stateless, body = inputs
POST   /api/v1/intelligence/projection/break-even           // solve for a target
GET    /api/v1/intelligence/animals/{animalId}/projection/scenarios
POST   /api/v1/intelligence/animals/{animalId}/projection/scenarios
PUT    /api/v1/intelligence/projection/scenarios/{id}
DELETE /api/v1/intelligence/projection/scenarios/{id}
GET    /api/v1/intelligence/projection/scenarios/compare?ids=...
GET    /api/v1/intelligence/animals/{animalId}/projection/export.csv   // Phase 6
```

`POST` for calculate rather than GET — 12 inputs will not fit a clean query string, and the payload is not URL-cacheable anyway. Authorization: existing `RequireAuthorization()` plus the farm-scope policy used by the Livestock endpoints. Rate-limit `calculate` per user (the UI debounces, but do not trust the client).

**Exit criteria:** functional tests in `tests/Farm360.Api.FunctionalTests` cover 200 / 400 / 401 / cross-tenant 404 for every route; OpenAPI shows correct schemas.

---

### Phase 5 — Angular feature (3–4 days)
**Owner:** Senior Angular Architect + Senior Angular Developer + UX Architect + Senior UI-UX Developer

**Structure** under `src/Farm360.Web/src/app/features/livestock/`:

```
models/profit-projection.models.ts
services/profit-projection.service.ts          // typed HttpClient, matches Contracts 1:1
pages/animal-profit-projection/                // route /livestock/:id/profit-projection
components/projection-assumptions-panel/       // the 12 inputs
components/projection-summary-cards/           // KPI row
components/projection-charts/                  // echarts
components/projection-daily-table/             // virtual-scrolled Excel-parity table
components/projection-lever-panel/             // "what fixes it" break-even solver
components/projection-scenario-list/           // save / load / compare
```

Plus a compact **Profit Projection** card in the existing animal-detail intelligence tab, deep-linking to the full page. Register the route in `livestock.routes.ts` **above** the `:id` catch-all.

**State and reactivity (Angular 22, signals — match existing feature style):**
- Assumptions held in a `signal<FatteningProjectionInputs>`; a `computed` dirty-flag against resolved defaults marks overridden fields.
- Form → `toObservable` → `debounceTime(300)` → `distinctUntilChanged` (deep) → `switchMap` to `POST /calculate` → result signal. `switchMap` cancels in-flight requests so slider drags cannot race.
- **The server is the single source of truth for the maths.** Deliberately *not* mirroring the formula in TypeScript: two implementations drift, and a drift here means a wrong money number in front of a farm manager. A 300 ms debounce plus a sub-millisecond server calc keeps it feeling live. (If field testing shows latency pain on 3G, the fallback is to ship the Domain calculator to WASM — not to hand-port it.)
- Skeleton loaders on first load; keep the last good result visible while recomputing, so numbers never flicker to empty.

**UX design (per `docs/5_Farm360_AI_UX_Design_System.md`):**
1. **Header** — animal tag, breed, current weight, and a one-line verdict: *"Projected loss of ৳14,765 (−13.3%) on day 120"*, semantic colour token, never colour alone (icon + text).
2. **Assumptions panel** — left rail, sticky on desktop, bottom sheet on mobile. 12 inputs grouped **Animal · Feed · Operating Cost · Market**. Each field: number input + unit suffix + provenance chip ("From breed standard", "From inventory average") + reset-to-default icon. Slider + input pair for the four fields farmers actually flex (ADG, feed price, meat price, period).
3. **KPI cards** — Total Investment · Expected Sale Value · Profit/Loss · Profit % · Break-even ৳/meat-kg · Optimal sale day. Each card carries a tooltip stating its formula in plain language; trust comes from showing the arithmetic.
4. **Charts** (echarts, already a dependency):
   - *Profit curve* — P/L vs. day, zero reference line, break-even crossing annotated, optimal-day marker.
   - *Investment vs. value* — two lines with the gap shaded as profit or loss.
   - *Cost composition* — stacked area (feed / grass / other / labor), showing feed's growing share as the animal gains weight.
   One accessible categorical palette, direct labels over legends where space allows, dark-mode tokens.
5. **Lever panel** — "To break even you need: meat price ≥ ৳784/kg *(+15%)* · purchase price ≤ ৳65,235 · ADG ≥ 1.04 kg/day". This is the module's differentiator over the spreadsheet.
6. **Daily table** — all 15 Excel columns, CDK virtual scroll, sticky header and sticky Day column, highlighted rows for break-even day and optimal day, CSV export. Collapsed by default behind "Show daily breakdown (120 rows)".
7. **Scenarios** — save named assumption sets; compare 2–3 side by side (Base / Optimistic / Pessimistic); preset buttons that seed those three from the breed ADG bands (`AdgPoorManagement` / `AdgAverageFarm` / `AdgIntensiveFattening`).
8. **Accessibility** — WCAG 2.1 AA: labelled inputs, `aria-live` on the verdict line, keyboard-reachable sliders (step 0.05), charts as `role="img"` with summary alt text and the table as the data equivalent, 44 px touch targets.
9. **Localisation** — ৳ formatting through the existing currency pipe / locale config; all strings through the i18n layer (Bangla-ready).

**Exit criteria:** route renders from a cold load in under 1.5 s on a mid-range Android; slider drag stays at 60 fps; the daily table matches the spreadsheet cell for cell on the sample inputs.

---

### Phase 6 — Extensions (2–3 days, after v1 is validated)
**Owner:** Principal Product Architect + Farm Manager

1. **Batch-level projection** — run the calculator per animal in a batch and aggregate (total investment, total expected value, per-head average, best/worst performer). Reuses everything; only a new query and page.
2. **Actuals vs. projection** — overlay recorded weights and real feed/health costs (Finance `FinancialTransaction`, feeding consumption) on the projected curves. Turns the simulator into a variance report and closes the loop with the Finance module.
3. **Excel / PDF export** — a three-sheet XLSX reproducing the original workbook (Inputs / Daily Projection / Summary) so the model stays auditable by people who trust spreadsheets, plus a one-page PDF for lenders.
4. **Insight generation** — feed `ActionableInsight` from projection outcomes: "Sell within 10 days — profit declines ৳4.9/day after that", "Feed is 62% of operating cost and rising", "This animal will not break even at the current market price".
5. **Backlog inputs from Phase 0** — mortality risk, medicine cost line, transport and commission, cost of capital, seasonal meat-price curve.

---

## 4. Cross-cutting concerns

**Performance.** The calculator is O(N) over at most 1095 days with no per-day allocation beyond the record list — expect well under 1 ms for 120 days. Cap `ProjectionDays` at 1095 in the validator. Use `includeDailyRows=false` for cards and batch aggregation to keep payloads small (a 120-row response is roughly 25 KB of JSON; a 500-head batch must not return 60,000 rows). Cache `GetProjectionDefaults` per animal for 60 s. No N+1: the defaults resolver does one animal read, one breed read, one feed-inventory aggregate.

**Precision.** `decimal` end to end, `decimal(18,4)` in SQL, rounding only at the DTO boundary. Parity tests assert against unrounded Domain output.

**Multi-tenancy and security.** Every scenario read/write goes through the tenant-filtered `ApplicationDbContext`; `animalId` is authorised against the caller's farm scope before defaults are resolved. The stateless `calculate` endpoint accepts arbitrary inputs but must still reject unauthenticated callers — the formula is business IP.

**QA.** Test pyramid:
- Domain unit tests — golden vectors, randomised invariants, validators (§1 is the oracle).
- Application integration tests — defaults resolution from seeded data, scenario CRUD, tenant isolation.
- API functional tests — every route, every status code.
- Web unit tests — service mapping, debounce/cancel behaviour, summary formatting.
- E2E happy path — open animal → projection → change ADG → numbers move → save scenario → reload → same numbers.
- **Parity regression suite** — the §1.5 table as a checked-in JSON fixture consumed by both the .NET tests and the Angular service spec, so any change in either layer that breaks Excel parity fails CI.

**Definition of Done.** Excel parity proven by the fixture; no mocked constants left in the Intelligence slice; migration reversible; AA accessibility pass; docs updated (`USER_MANUAL.md` section, `CHANGELOG.md` entry); `graphify update .` run.

---

## 5. Sequencing and effort

| Phase | Work | Est. | Depends on |
|---|---|---|---|
| 0 | Contract freeze | 0.5 d | — |
| 1 | Domain calculator + tests | 1.5 d | 0 |
| 2 | Application, defaults, mock retirement | 2 d | 1 |
| 3 | Persistence (scenarios) | 1 d | 2 (parallel with 4) |
| 4 | API | 0.5 d | 2 |
| 5 | Angular feature | 3–4 d | 4 (can start against a stubbed service after 2) |
| 6 | Extensions | 2–3 d | v1 validated |

**v1 (Phases 0–5): roughly 8–9 working days.** Phase 1 is the entire risk surface — once the golden vectors are green, the rest is plumbing and UI craft.

**Decision to make in Phase 0:** whether v1 stores scenarios at all. Dropping Phase 3 ships a stateless calculator in about 7 days; farmers will immediately ask to save scenarios, so the recommendation is to keep it.
