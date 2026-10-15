# Balance Sheet & Financial Statements – Implementation Plan (Farm360)

Goal: an auditor-grade *Statement of Financial Position* for any date (with the prior year side by
side), an *Income Statement* for any period and a *Trial Balance* — all from one engine, so they can
never disagree. Ported from FrostTrack's design (commit `8c407d0`), adapted to Farm360, which is
already further along: every posting is a complete double entry through `AccountChart`.

---

## 1. What Farm360 already has (keep, build on)

* `FinancialTransaction` = one double entry: category account ↔ `SettlementAccount`
  (Cash, Inventory, Livestock, Payable, Receivable). `AccountChart` maps every category to a ledger
  account and computes balances. Livestock and inventory are assets at cost; loans, investor and owner
  money are balance-sheet only; credit deals go through `TradeBill` (AP/AR).
* The balance sheet balances because the bookkeeping balances — no plug.

## 2. Gaps (what FrostTrack's new design adds)

| # | Gap | Effect |
|---|-----|--------|
| 1 | No books-start date / opening balances | A farm that moves onto Farm360 mid-life can't bring its audited position |
| 2 | No fiscal year | Retained earnings is one all-time line; "this year" uses 1 January |
| 3 | No fixed assets / depreciation | Sheds, machinery, vehicles, land are missing; profit overstated |
| 4 | No year-end adjustments | Provisions, accruals, prepayments can't be recorded |
| 5 | Report definition drops totals and the difference row | Printed statement isn't auditor format |
| 6 | P&L / Trial Balance read postings only | Would ignore depreciation and adjustments |

## 3. Design

### 3.1 Accounting settings (per farm) — `finance.FarmAccountingSettings`
* `FiscalYearStartMonth` (1–12, default **7 = July**, Bangladesh fiscal year).
* `BooksStartDate` (`DateOnly?`): the date of the last audited balance sheet. **Optional** — farms whose
  whole history is in Farm360 leave it empty and nothing changes for them.
  When set: postings on or before it are summarised by the opening entries and excluded from the
  statements; everything after it is calculated.

### 3.2 Opening balances & adjustments — `finance.BalanceSheetEntries`
| Field | Notes |
|-------|-------|
| `Code` | `BSE-0001`… |
| `EntryDate` | counts in statements on or after it |
| `EntryType` | `Opening` (position on the start date) / `Adjustment` (year-end, non-cash) |
| `AccountCode` | balance-sheet account the line adds to (list below) |
| `Amount` | signed: + increases the line in its natural direction |
| `ProfitAccountCode` | Adjustment only: the P&L side (e.g. 5050 Utilities for an electricity provision) |
| `ReversalDate` | Adjustment only, optional: on this date the entry is reversed automatically (accruals) |
| `Description`, `Notes` | |

* **Opening** entries are one-sided. A complete opening position nets to zero; an incomplete one shows
  as a red **Difference** — never a plug.
* **Adjustments** are two-sided (account ↔ P&L account), so they always balance and move profit.
* Allowed accounts: 1010 Cash & Bank, 1100 Trade receivables, 1150 Advances, deposits & prepayments,
  1200 Inventory, 1300 Livestock, 2010 Loans, 2100 Trade payables, 2200 Provisions & accrued expenses,
  2300 Other liabilities, 3010 Investor capital, 3200 Owner's capital, 3900 Retained earnings b/f.
* **Generate from ledger**: one click creates the opening entries from the ledger balances as at the
  start date (for farms whose earlier history is already in Farm360) — balanced by construction.

### 3.3 Fixed asset register — `finance.FixedAssets`
* Category (Land, Building & Shed, Machinery & Equipment, Vehicle, Furniture & Office, Other),
  method (`ReducingBalance` rate % — auditor/tax default; or `StraightLine` useful life + residual).
* Acquisition: **Cash** (posts `FixedAssetPurchase`, Dr 1500 / Cr Cash), **Credit** (same + supplier
  bill), **Owner contributed** (`OwnerCapital` settled to 1500), **Opening** (owned before the books
  start: opening WDV as at the start date, part of the opening position).
* Depreciation is calculated, not posted: dated monthly ledger lines Dr 5400 Depreciation /
  Cr 1590 Accumulated depreciation, fiscal-year aware, part periods by days. Land never depreciates.
* Disposal: proceeds post as Income on 5410 (Dr Cash); the net book value is derecognised against
  5410 → 5410 shows the loss (or gain) on disposal.

### 3.4 One engine — `FinancialStatementEngine`
Produces dated ledger lines (debit-positive) from: postings (after the start date), opening /
adjustment / reversal entries, fixed-asset opening values, depreciation and disposals. Every
statement filters the same lines by date:
* Balance sheet as at D = lines ≤ D.
* Profit for a period = P&L lines in the period. Retained earnings b/f = opening 3900 + profit up to
  the fiscal-year start; *Net profit for the year* = profit from the fiscal-year start to D.
* `Difference` = Assets − (Liabilities + Equity) = Σ all lines (zero when the books are complete).

### 3.5 Statements
**Balance Sheet** (auditor layout, current vs previous fiscal-year end):
```
ASSETS
  Non-current assets
    Property, plant & equipment (WDV)
  Current assets
    Livestock (biological assets, at cost)
    Inventories (feed, medicine & supplies)
    Trade receivables
    Advances, deposits & prepayments
    Cash & bank balances
EQUITY & LIABILITIES
  Equity
    Investor capital · Owner's capital · Less: drawings
    Retained earnings b/f · Net profit / (loss) for the year · Less: profit distributed
  Current liabilities
    Trade payables · Loans & borrowings · Provisions & accrued expenses
    Customer advances · Bank overdraft · Other liabilities
  [Difference]  ← only when the books don't balance
```
**Income Statement** (period + prior period): Revenue → Cost of animals sold → Gross profit →
operating expenses (feed, health, labour, overheads, mortality, depreciation, disposal) → Operating
profit → Finance costs → Net profit.
**Trial Balance**: every account incl. depreciation, provisions and opening entries; unbalanced
opening entries show as a Dr/Cr difference.
**Fixed asset schedule** (Annex): cost/WDV b/f, additions, disposals, depreciation for the year, WDV c/f.

## 4. Work items
1. Domain: settings, `BalanceSheetEntry`, `FixedAsset`, `FiscalYear`, `DepreciationCalculator`;
   `TransactionCategory.FixedAssetPurchase`, `AssetDisposal`; `SettlementAccount.FixedAsset`.
2. `AccountChart`: accounts 1150, 1500, 1590, 2200, 2300, 5400, 5410; heads for the new categories.
3. `FinancialStatementEngine` + rewrite `GetBalanceSheetQuery` (comparative), new
   `GetIncomeStatementQuery`, engine-based `GetTrialBalanceQuery`, fixed-asset schedule query.
4. CRUD + endpoints: accounting settings, opening & adjustment entries (+ generate from ledger),
   fixed assets (+ dispose) with their ledger postings.
5. Migration `AddFinancialStatementsSetup`.
6. Report definitions: Balance Sheet (comparative, totals, difference), Income Statement, Trial
   Balance, Fixed Asset Schedule.
7. Angular: Accounts setup page (settings + opening/adjustments), Fixed assets page, Balance sheet
   and Income statement pages, finance sub-navigation.
8. Tests: depreciation maths, engine (balances, FY split, reversal, difference), statements.

## 5. One-time setup for a farm (~15 min)
1. Finance → Accounts setup → fiscal year start (July) and, if the farm has history outside
   Farm360, the books-start date.
2. Opening balances from the last audited statement — or **Generate from ledger**.
3. Fixed assets → register sheds, machinery, vehicles, land (opening WDV for those owned before).
4. Balance sheet at the start date must show **no Difference**.

## 6. Defaults chosen (change if the auditor disagrees)
* Fiscal year July–June. Loans shown under current liabilities (no maturity data yet).
* Livestock at cost (existing policy). Depreciation calculated on the fly (not posted).
* Cash and bank stay one account (1010) — separate bank accounts are a later phase.
