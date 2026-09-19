# Farm360 AI — Configurable Business Rules

**Purpose:** every assumption the business owns — how shared feed is split, how overhead reaches an animal, what an animal is "worth", how reports present themselves — lives in configuration, not in code. Changing an assumption is a settings edit and a restart, never a rewrite of the reporting, feeding or costing system.

**Configuration section:** `Farm360:BusinessRules` in `appsettings.json` (or environment variables / AWS Parameter Store using the `Farm360__BusinessRules__<Key>` form).
**Bound to:** `Farm360.Domain/BusinessRules/FarmBusinessRules.cs`
**Read through:** `IFarmBusinessRulesProvider` (`IOptionsMonitor`-backed, so edits are picked up without a redeploy).
**Decided in:** [docs/32 §9](32_Farm360_Reporting_Module_Implementation_Plan.md)

---

## 1. The safety property that makes this work

> **A rule change affects future calculations only.**

Every feed allocation row stores the method, exponent, weight and head count that produced it. A report re-run next year therefore reprints the numbers it was signed off with, even if the rule has since changed. This is deliberate: a configuration edit that silently rewrote last quarter's animal costs would be worse than no configurability at all.

The practical consequence: **changing a rule does not retroactively fix history.** If you switch feed allocation from metabolic weight to equal-per-head, yesterday's rows keep the old split. To restate history you must delete the affected allocation rows and re-run the backfill — a deliberate, auditable act, not a side effect.

---

## 2. The rules

### 2.1 Feed allocation — how shared feed is divided

| Setting | Values | Default |
|---|---|---|
| `FeedAllocation` | `MetabolicWeight` · `EqualPerHead` · `LiveWeight` | `MetabolicWeight` |
| `MetabolicWeightExponent` | decimal | `0.75` |
| `FallbackAnimalWeightKg` | decimal | `150` |

**Why metabolic weight is the default.** A batch, shed or pen plan feeds many animals from one record. Splitting that equally makes a 150 kg calf carry the same feed cost as a 400 kg bull, which flatters the bull's margin and makes the calf look unprofitable — a distortion that then flows into every profitability ranking and sale decision. Ruminant intake scales with metabolic body weight, W^0.75, so that is the default share factor. On a 400 kg vs 100 kg pair, live weight would say 4×; W^0.75 says 2.83×.

`FallbackAnimalWeightKg` covers an animal with no recorded weight. Without it that animal would take a zero share and its feed would be silently absorbed by its pen-mates.

**Applies to:** group plans only. An individual plan names its animal, so its ration is not divided.

### 2.2 Group plan quantity basis — what the number on a rule line means

| Setting | Values | Default |
|---|---|---|
| `GroupPlanQuantityBasis` | `PerHead` · `WholeGroup` | `PerHead` |

**This one changes money by a factor of the head count, so read it carefully.**

Farm360's feeding rule lines are matched on a **per-animal weight band** ("200-250 kg → 3 kg concentrate/day"), and the `WeightPercentage` plan type computes `bodyWeight × %`. Both describe a single animal. A batch plan's daily entry therefore records **one animal's ration**, and the group's real consumption is that ration multiplied by the head count.

Set `WholeGroup` only if your farm records group totals on rule lines instead. Getting this backwards over- or under-states feed cost by the size of the group.

### 2.3 Labour and overhead allocation

| Setting | Values | Default |
|---|---|---|
| `OverheadAllocation` | `PerHeadDay` · `PerLiveWeight` | `PerHeadDay` |
| `OverheadCostPeriod` | `CalendarMonth` · `TransactionDate` | `CalendarMonth` |

Per head-day is the conventional, easily-audited basis: the expense ÷ head-days in the period. `PerLiveWeight` weights by **weight-days** (weight × days present), which suits farms where housing and handling genuinely scale with size — note it is weight-days, not weight, so an animal present for half the period cannot outrank one present throughout on bulk alone.

`OverheadCostPeriod` decides what a single expense is treated as covering. A monthly wage bill is posted on one date but earned across the month; spreading it over the calendar month means an animal sold on the 10th carries ten days of labour rather than all of it or none, purely according to which day the bookkeeper posted the entry. Use `TransactionDate` for genuinely one-off costs such as a single transport run.

**Only indirect categories are allocated:** `LaborCost` → the labour bucket; `Utilities`, `Transport`, `MiscellaneousExpense`, `ConsumableExpense` → the overhead bucket. Feed, veterinary, medicine and animal purchase are excluded, because their own modules already attribute them to an animal and allocating them again would double-count. Expenses that already carry an `AnimalId` are likewise skipped.

> **Status: implemented and verified.** `OverheadAllocationService` posts into `AnimalCostLedger`, so `TotalCostBdt` is now a **full-absorption cost**. Run it per farm per period via `POST /api/v1/farms/{farmId}/finance/overhead/allocate`. It is idempotent: transactions already allocated are skipped, and the ledger buckets are *recomputed* from the allocation table rather than incremented, so a re-run changes nothing.

### 2.4 Animal valuation

| Setting | Values | Default |
|---|---|---|
| `Valuation` | `MeatYieldMarket` · `LiveWeightMarket` · `CostBasis` | `MeatYieldMarket` |
| `DefaultDressingPercentage` | decimal | `0.50` |
| `DefaultMeatPricePerKgBdt` | decimal | `680` |
| `DefaultLiveWeightPricePerKgBdt` | decimal | `400` |

- **`MeatYieldMarket`** — live weight × dressing percentage × meat price. The default, because cattle here are priced on expected meat yield, and it matches the projection engine in [docs/31](31_Farm360_Cattle_Profit_Loss_Projection_Module_Implementation_Plan.md).
- **`LiveWeightMarket`** — live weight × live-animal rate.
- **`CostBasis`** — acquisition plus accumulated costs. Conservative; the appropriate basis for a balance sheet, where the convention is the lower of cost or net realisable value.

Breed-level dressing percentage overrides the default where recorded. **Every report that prints a value must also print the policy that produced it** — the same animal is legitimately worth three different numbers under the three policies, and a figure without its basis is not an answer.

### 2.5 Report presentation

| Setting | Values | Default |
|---|---|---|
| `DefaultReportLanguage` | `English` · `Bangla` | `English` |
| `BengaliNumeralsByDefault` | bool | `false` |

Every report label carries an `(en, bn)` pair in its definition, so the toggle costs nothing at render time. Numerals are separate from language because printed accounts in Bangladesh go both ways: Bengali prose with Western digits is common in financial documents.

### 2.6 PDF licence

| Setting | Values | Default |
|---|---|---|
| `PdfLicence` | `Community` · `Professional` · `Enterprise` | `Community` |
| `PdfLicenceKey` | string | `null` |

QuestPDF's Community licence is free **only while annual revenue is below USD 1M**. That is a fact about the business, not about the software, so it is configuration rather than a constant.

> **Outstanding — Owner action.** The Community tier is currently declared. Nobody outside the business can verify the revenue figure, so this remains an unconfirmed assumption. Confirm it, and when Farm360 approaches the threshold, buy the licence and change these two settings; no code changes are required. Setting a paid tier without a key fails fast at startup rather than rendering documents under a licence you do not hold.

---

## 3. Worked example

```jsonc
"Farm360": {
  "BusinessRules": {
    "FeedAllocation": "EqualPerHead",     // auditor prefers a flat split
    "Valuation": "CostBasis",             // conservative valuation for the balance sheet
    "DefaultReportLanguage": "Bangla",
    "BengaliNumeralsByDefault": true
  }
}
```

Unspecified keys keep their defaults, so a partial section is valid and the diff stays readable.

Environment-variable form, for containers:

```
Farm360__BusinessRules__FeedAllocation=EqualPerHead
Farm360__BusinessRules__Valuation=CostBasis
```

---

## 4. Where each rule is consumed

| Rule | Consumed by |
|---|---|
| `FeedAllocation`, `MetabolicWeightExponent`, `FallbackAnimalWeightKg`, `GroupPlanQuantityBasis` | `FeedAllocationService` → `FeedAllocationCalculator`, written into every `AnimalFeedAllocation` row |
| `OverheadAllocation`, `OverheadCostPeriod` | `OverheadAllocationService`, written into every `AnimalOverheadAllocation` row |
| `Valuation`, `DefaultDressingPercentage`, `DefaultMeatPricePerKgBdt`, `DefaultLiveWeightPricePerKgBdt` | Animal summary (A5) and Herd Valuation (H7) reports, projection defaults |
| `DefaultReportLanguage`, `BengaliNumeralsByDefault` | `ReportExecutionService`, `ReportValueFormatter`, the Angular report viewer |
| `PdfLicence`, `PdfLicenceKey` | `ReportFonts.EnsureRegistered` at startup |

---

## 5. Testing a rule change

`FeedAllocationCalculatorTests` and `FeedAllocationServiceTests` cover every allocation method, including a 500-herd property test asserting that the shares always reconcile exactly to the group total. `ChangingTheRuleToEqualSplit_ChangesFutureAllocationsOnly` exists specifically to prove the assumption is configuration rather than a hard-coded constant.

Before changing a rule in production: change it in a non-production environment, run one day of the feeding job, and confirm the allocation rows carry the new `Method` while yesterday's rows still carry the old one.
