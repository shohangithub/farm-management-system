# Farm360 AI — Enterprise Reporting Module
## Implementation Plan (SAP / RDLC-class banded reports)

**Requested by:** Owner / Accounts Head — "not HTML print; SAP / RDLC type reports"
**Reference implementation studied:** `D:\Personel\FrostTrack_Web\frosttrack.client\src\app\reports\` (`daily-stock-book`, shared `report-invoice-header` / `report-footer`)
**Status:** Phases 0–2 complete · **GAP-1 and GAP-2 closed** (per-animal feed, labour and overhead) · reports A1, A2, A3, H6 live
**Branch suggestion:** `feature/reporting-platform`
**Related:** [31_Farm360_Cattle_Profit_Loss_Projection_Module_Implementation_Plan.md](31_Farm360_Cattle_Profit_Loss_Projection_Module_Implementation_Plan.md)

---

## 1. What "SAP / RDLC type" actually means here

Reading the FrostTrack report you pointed at, the qualities you are asking for are concrete and testable:

| Quality | What it means in practice |
|---|---|
| **Fixed page geometry** | A4 210×297 mm, 10 mm margins, content laid out in millimetres — not a responsive web page that happens to be printed |
| **Banded structure** | Letterhead → report title → parameter echo box → column header band → detail band → group footers → grand total → page footer |
| **Repeating headers** | Column headers repeat on every page (`thead { display: table-header-group }` in FrostTrack; a real page-header band server-side) |
| **Page X of Y** | Known total page count, printed on every page |
| **Dense, bordered, monochrome** | 11 px Arial, 1 px black rules, grey header fill, right-aligned numerics — designed for a laser printer and a filing cabinet, not a screen |
| **Deterministic** | The same parameters produce a byte-identical document today and next audit season |
| **True exports** | PDF with selectable, searchable text; Excel with real numbers an accountant can pivot — not a picture of a table |

FrostTrack achieves the first five with hand-written Angular + print CSS. That is the right *look* and I will keep it. It is the wrong *mechanism* for Farm360 at 30+ reports, for reasons in §3.

### 1.1 Where Farm360 stands today

| Asset | Location | Verdict |
|---|---|---|
| `reports.view` / `reports.export` / `reports.schedule` permissions | `Farm360.Persistence/Seed/PermissionConstants.cs:52` | **Already defined, unused** — no reports feature exists |
| `PdfExportService` (html2canvas → jsPDF) | `Farm360.Web/src/app/shared/services/pdf-export.service.ts` | **Rasterizes the DOM.** Blurry text, unsearchable, huge files, pagination by pixel slicing. This is the "HTML print" you want to move away from |
| `FeedingReportPdfService` (jsPDF + autoTable, 577 lines) | `Farm360.Web/src/app/features/feeding/services/feeding-report-pdf.service.ts` | Vector text and proper tables — much better — but hand-codes one report's layout in imperative mm coordinates. Not repeatable 30 times |
| Finance report *pages* | `Farm360.Web/src/app/features/finance/pages/{trial-balance, balance-sheet, monthly-pnl-report, batch-pnl-report, investor-pnl, animal-cost-ledger}` | Screen views with server queries behind them. **The queries are reusable; the presentation is not a report** |
| Server-side report queries | `Farm360.Application/Finance/Queries/*`, `Farm360.Application/Health/Queries/Reports/*`, `Farm360.Application/Feeding/Queries/Analytics/*` | Good foundation — roughly 40% of the data work is already done |
| Server-side PDF/XLSX library | none in `Directory.Packages.props` | **To be chosen — Phase 0 decision** |

### 1.2 The Bengali typography problem (read this before choosing a PDF engine)

`PdfExportService` documents its own reasoning in a comment: html2canvas "guarantees 100% accurate Unicode / Bengali (বাংলা) rendering (CTL, ligatures, matras)". That is true, and it is why the rasterized approach was chosen. It is not laziness — it is a workaround for a real constraint:

- **jsPDF + autoTable** draws vector text but has **no complex-text-layout engine**. Bengali conjuncts (ক্ষ, ঙ্গ), matras that reorder around the consonant (কি — the vowel is stored after but drawn before), and ra-phala/ref forms come out broken or reversed. Fine for English and digits; unusable for a Bangla letterhead.
- **html2canvas** hands the shaping to the browser, which is correct — but the output is a bitmap: unsearchable, uncopyable, ~2–5 MB per page, and it degrades on a printer.

So the engine choice is not a style preference. **Any candidate must be validated against a Bengali sample before we commit** (Phase 0 spike). The shortlist and the reason each is a candidate:

| Engine | Licence | Complex-script shaping | Notes |
|---|---|---|---|
| **QuestPDF** (recommended) | Community free **under USD 1M annual revenue**; paid above | Yes — SkiaSharp + HarfBuzz | Fluent C# layout API, real banded pagination, `Page X of Y`, repeating headers as a first-class feature |
| Syncfusion PDF | Community free under revenue/headcount threshold; paid above | Yes | Heavier dependency, strong RDLC heritage |
| iText 8 | AGPL (viral) or commercial | Yes | AGPL is a non-starter for a closed product; commercial is expensive |
| PDFsharp / MigraDoc | MIT | **Weak** — limited shaping | Free, but likely fails the Bengali test. Include in the spike only to confirm |

**Owner decision required in Phase 0:** if Farm360 crosses the QuestPDF revenue threshold, budget the Professional licence. Do not ship on a Community licence the business has outgrown.

For Excel, **ClosedXML** (MIT) — real `.xlsx` with number formats, frozen panes and autofilter. Excel carries no shaping problem; the font does the work.

---

## 2. Report catalog

Marked **R** = data is ready today · **P** = partial, needs a join or a new query · **B** = blocked on a data gap in §4.

### 2.1 Single animal (your list, plus the siblings that fall out of the same data)

| # | Report | Content | State |
|---|---|---|---|
| A1 | **Animal Feeding Report** (monthly, date-wise) | Per day: rule set name, formula/recipe, expected kg, actual kg, variance %, unit cost at consumption, total cost. Grouped by month, sub-totalled per formula. Ingredient breakdown as an optional second band | **B** — see GAP-1 |
| A2 | **Animal Medical & Health Report** (monthly) | Treatments (diagnosis, medication, dosage, start/end, vet, cost), vaccinations, deworming, vet visits, disease incidents, active withdrawal periods. Cost column, monthly sub-totals | **R** |
| A3 | **Animal Weight & Growth Report** | Every weigh-in; per *interval between weigh-ins*: days, gain kg, ADG, ADG vs breed standard band, feed consumed, feed cost, treatments in interval, **actual FCR**, **cost per kg gain**. This is the report that ties feeding and health to outcome | **B** — needs A1's data |
| A4 | **Animal Profit Projection Report** | Assumptions box (12 inputs + provenance), daily projection table, summary, break-even, optimal sale day. Straight render of the doc-31 calculator | **P** — after doc 31 Phase 1 |
| A5 | **Animal Summary / Passport** | Identity, tag, breed, sex, DOB, lineage, acquisition; complete cost history by category; weight history mini-chart; current weight; **current value**; break-even price/kg; P/L to date. The "total costing history and cow current value" ask | **P** — GAP-2, GAP-3 |
| A6 | **Animal Cost Ledger** (date-wise) | Every cost transaction, category, reference document, running balance | **R** — `GetAnimalCostLedgerQuery` exists |
| A7 | **Animal Movement & Location History** | Shed/pen/batch transfers with dates and reasons | **R** |
| A8 | **Animal Breeding & Reproduction Report** | Heats, services, pregnancy checks, calvings, calving interval, days open | **R** |

### 2.2 All animals / herd

| # | Report | State |
|---|---|---|
| H1 | Herd Register (master list with filters: farm, shed, pen, batch, breed, status, age band) | **R** |
| H2 | Herd Feeding Consumption Summary (period; by animal / batch / shed / pen; expected vs actual vs cost) | **P** |
| H3 | Herd Health Summary + Treatment Register (period) | **R** |
| H4 | Vaccination Due & Compliance Report (overdue, due in 7/30 days, compliance %) | **R** |
| H5 | Mortality Register + mortality-rate analysis by cause/shed/age band | **R** |
| H6 | Herd Growth & Performance Ranking (ADG, FCR, cost/kg gain; best and worst performers) | **B** — GAP-1 |
| H7 | **Herd Valuation Report** (current value of every head; the number that feeds the balance sheet) | **P** — GAP-3 |
| H8 | Herd Profit Projection Summary (doc-31 run across the herd) | **P** |
| H9 | Batch P&L | **R** — query exists |
| H10 | Feed Inventory & Consumption Reconciliation (issued vs consumed vs closing stock) | **R** |
| H11 | Purchase / Supplier Register | **R** |
| H12 | **Daily Farm Operations Sheet** — one day, one page: feeding, treatments, weights, movements, mortality. The Farm360 analogue of FrostTrack's Daily Stock Book | **P** |

### 2.3 Finance (mostly porting existing queries into the report platform)

Trial Balance · Balance Sheet · Monthly P&L · Consolidated P&L · Investor P&L · Share Register · Loan Schedule · Cash & Bank Book · General Ledger · Labour & Overhead Allocation Statement. All **R/P** — the queries exist under `Farm360.Application/Finance/Queries/`; they need report definitions and a renderer, not new SQL.

---

## 3. Architecture: a report *platform*, not 30 report pages

### 3.1 Why not the FrostTrack pattern, verbatim

FrostTrack's `daily-stock-book` hand-writes its table, and hand-writes one `getTotalX()` method per numeric column — nine of them for nine columns. That is fine at ten reports. At Farm360's thirty-plus across seven modules it becomes thousands of lines where every report re-derives pagination, totals, print CSS and export, and each one drifts. FrostTrack already spotted the problem and factored out `report-invoice-header` / `report-footer` — exactly the right instinct. This plan carries it two steps further: **the detail band becomes metadata too, and the authoritative render moves to the server.**

### 3.2 The pipeline

```
ReportDefinition (C#, code-first — the ".rdl" equivalent)
    key · title(en,bn) · category · permission · parameter schema
    data query · column metadata · grouping · aggregates · page setup
                │
                ▼
ReportDataSet  (columns + rows + group totals + grand totals + meta header)
                │
    ┌───────────┼────────────┬──────────────┐
    ▼           ▼            ▼              ▼
 JSON        QuestPDF     ClosedXML       CSV
    │        (PDF)         (XLSX)
    ▼
Angular generic Report Viewer  (A4 paginated on-screen, one component for every report)
```

One definition. Four outputs. The on-screen view and the PDF are generated from the same metadata, so **what you see on screen is what prints** — which is the property RDLC gives you and ad-hoc HTML never does.

### 3.3 Server structure (new)

```
Farm360.Application/Reporting/
  Abstractions/     IReportDefinition, IReportDataSource, IReportRenderer, ReportDescriptor
  Model/            ReportDataSet, ReportColumn, ReportGroup, ReportAggregate,
                    ReportParameter, ReportMetaHeader, PageSetup
  Registry/         ReportRegistry (assembly-scanned, keyed by report key)
  Definitions/
      Livestock/    AnimalFeedingReport, AnimalHealthReport, AnimalGrowthReport,
                    AnimalSummaryReport, HerdRegisterReport, HerdValuationReport, …
      Finance/      TrialBalanceReport, BalanceSheetReport, …
      Feeding/      …  Health/  …  Inventory/  …
  Queries/          GetReportCatalogQuery, GetReportMetadataQuery, RunReportQuery
  Services/         ReportExecutionService, ReportParameterBinder, ReportNumberFormatter

Farm360.Infrastructure/Reporting/
  Pdf/              QuestPdfReportRenderer, Farm360ReportDocument (letterhead, bands,
                    page X of Y, signature block), FontProvider (Bengali + Latin)
  Excel/            ClosedXmlReportRenderer
  Csv/              CsvReportRenderer

Farm360.Api/Endpoints/ReportEndpoints.cs
Farm360.Domain/Reporting/ReportRun.cs          (audit — see GAP-4)
Farm360.Domain/Reporting/ReportSchedule.cs     (Phase 7)
```

**A report definition is small.** Sketch for A1:

```csharp
public sealed class AnimalFeedingReportDefinition : IReportDefinition
{
    public string Key => "livestock.animal-feeding";
    public LocalizedText Title => new("Animal Feeding Report", "পশুর খাদ্য প্রতিবেদন");
    public string Permission => PermissionConstants.ReportsModule.View;
    public ReportCategory Category => ReportCategory.Livestock;

    public IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal("animalId", required: true),
        ReportParameter.DateRange("period", defaultValue: DateRangePreset.CurrentMonth),
        ReportParameter.Bool("showIngredients", label: "Show ingredient breakdown", @default: false),
    ];

    public PageSetup Page => PageSetup.A4Portrait with { RepeatHeader = true, ShowPageNumbers = true };

    public IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Date("EntryDate",   "Date",        width: 18),
        ReportColumn.Text("RuleSetName", "Feeding Rule", width: 32),
        ReportColumn.Text("FormulaName", "Recipe",       width: 34),
        ReportColumn.Decimal("ExpectedKg", "Expected (kg)", 2, Align.Right, total: Aggregate.Sum),
        ReportColumn.Decimal("ActualKg",   "Actual (kg)",   2, Align.Right, total: Aggregate.Sum),
        ReportColumn.Percent("VariancePct","Var %",         1, Align.Right),
        ReportColumn.Money("UnitCostBdt",  "Rate ৳/kg",     2, Align.Right),
        ReportColumn.Money("TotalCostBdt", "Cost ৳",        2, Align.Right, total: Aggregate.Sum),
    ];

    public IReadOnlyList<ReportGroup> Groups => [ReportGroup.By("MonthLabel", showFooterTotals: true)];

    public Task<IReadOnlyList<object>> FetchAsync(ReportContext ctx, CancellationToken ct) =>
        ctx.Send(new GetAnimalFeedingLedgerQuery(ctx.Guid("animalId"), ctx.Range("period")));
}
```

Everything else — pagination, repeating headers, totals, PDF, Excel, CSV, the on-screen viewer, the letterhead, `Page 2 of 7` — comes free from the platform. **Adding a report becomes one class plus one query**, typically half a day, not three days of hand-built HTML and print CSS.

### 3.4 API

```
GET  /api/v1/reports/catalog                        // grouped, permission-filtered
GET  /api/v1/reports/{key}/metadata                 // parameter schema + column metadata
POST /api/v1/reports/{key}/run                      // -> ReportDataSet JSON (viewer)
POST /api/v1/reports/{key}/export/pdf               // -> application/pdf
POST /api/v1/reports/{key}/export/xlsx              // -> xlsx
POST /api/v1/reports/{key}/export/csv               // -> text/csv
POST /api/v1/reports/{key}/export/async             // large sets -> job id
GET  /api/v1/reports/runs/{runId}/download          // signed, expiring link
GET  /api/v1/reports/runs?animalId=&key=            // audit history (GAP-4)
```

`POST` for run/export: report parameters are structured and sometimes long (multi-select animal lists); they do not belong in a query string, and these responses are not URL-cacheable. Every endpoint enforces `reports.view`, exports additionally require `reports.export`, and **every report query is farm/tenant-scoped through the existing filtered `ApplicationDbContext`** — a report is the easiest place in an application to leak another tenant's data, so the scope check happens in `ReportExecutionService` before the definition's query ever runs, not inside each definition.

### 3.5 Angular structure (new feature)

```
features/reports/
  pages/report-center/            // catalog: module tree, search, favourites, recently run
  pages/report-viewer/            // /reports/:key — parameters + paginated paper view
  components/report-parameter-panel/   // renders any parameter schema
  components/report-canvas/            // A4 pages, zoom, page nav, thumbnails
  components/report-page/              // one A4 sheet: letterhead + bands + footer
  components/report-letterhead/        // org + farm + logo + title (en/bn)
  components/report-footer/            // page X of Y, generated-by, signature block
  components/report-table-band/        // metadata-driven detail band + group/grand totals
  services/report.service.ts           // catalog, run, export (blob download)
  services/report-preferences.service.ts // last-used parameters per report per user
  models/report.models.ts
routes: /reports, /reports/:key
```

Register `/reports` in `app.routes.ts` and add a **Reports** section to `core/layout/sidebar`, gated on `reports.view`.

**Reuse, don't rebuild:** the A4 SCSS from FrostTrack's `daily-stock-book.component.scss` is good and battle-tested — 210 mm × 297 mm container, 10 mm padding, 11 px Arial, `border-collapse`, `print-color-adjust: exact`, `thead { display: table-header-group }`, `.no-print`. Lift that wholesale into `report-page` / `report-table-band` as the shared paper stylesheet, once, and every report inherits it.

### 3.6 What happens to the existing export code

- **`PdfExportService` (html2canvas)** — deprecate for reports. Keep it only where the deliverable genuinely is a picture of a screen (a chart snapshot pasted into WhatsApp). Every tabular export routes to the server renderer. This is the single change that most directly answers "not like HTML print".
- **`FeedingReportPdfService`** — its layout is good; port it to a `ReportDefinition` and delete the 577 lines of imperative mm-coordinate drawing.
- **Finance report pages** — keep the interactive screens (they are useful for exploration); add a "Print / Export" action on each that opens the corresponding report in the viewer with the same parameters.

---

## 4. Data gaps — the real work, and why planning first was right

These are discovered constraints, not speculation. Each one is a decision the business must make before a line of report code is written.

### GAP-1 — There is no per-animal feed record (blocks A1, A3, H6)
**Fact:** `DailyFeedingEntry` (`Farm360.Domain/Feeding/DailyFeedingEntry.cs`) carries `FeedingPlanId`, `ShedId`, `PenId`, `BatchId` — **no `AnimalId`**. It links to `AnimalFeedingPlan`, whose `AnimalId` is **nullable**, because a plan may be animal-, batch-, shed- or pen-level. `FeedConsumptionLog` is likewise group-level with a `HeadCount`.

So: animals on an individual plan can be reported directly; animals fed under a group plan **have no per-animal feed quantity or cost anywhere in the system.** Your report A1 ("animal base, date wise, every feed quantity, cost, rule name, recipe") cannot be produced for them today.

**Resolution — an allocation policy plus a materialised table.** Options for the Farm Manager and Accounts Head:

| Method | Rule | Argument for |
|---|---|---|
| Equal per head-day | group cost ÷ head-days | Simple, explainable to an auditor |
| **Metabolic-weight-weighted** | share ∝ W^0.75 | Nutritionally correct — a 400 kg bull genuinely eats more than a 150 kg calf, and equal-split makes the calf look unprofitable and the bull look efficient |
| Plan-share | by each animal's planned ration within the group | Respects the ration engine's own intent |

**Recommendation:** metabolic-weight-weighted, with the method recorded on every row.

Add `Farm360.Domain/Feeding/AnimalFeedAllocation.cs` — `AnimalId`, `Date`, `DailyFeedingEntryId`, `FormulaId`, `RuleLineId`, `AllocatedKg`, `AllocatedCostBdt`, `AllocationMethod`, `HeadCountAtAllocation`. Written by the existing daily job (`Farm360.Application/Feeding/Jobs/CreateDailyFeedingEntriesCommand.cs`) at the time feed is recorded. **Persisted, not computed on read** — otherwise every historical report silently changes whenever herd composition or weights change later, and no printed report is ever reproducible. Backfill historical data in a one-off migration job, flagged as `Backfilled` so an auditor can see it.

> This is the highest-value item in the whole plan. It does not only unblock three reports — it makes per-animal profitability true for the first time, and doc 31's projections comparable against actuals.

### GAP-2 — Labour and overhead are never allocated per animal (blocks A5, H7)
`AnimalCostLedger` has `TotalLaborCostBdt` and `TotalOverheadBdt` fields, but nothing populates them per animal. A "total costing history" that silently omits labour and overhead will disagree with the P&L and destroy trust in the report pack. Needs a periodic allocation run (per head-day, or per head-day weighted by species) driven by an Accounts Head policy, written into the ledger with its own transaction category so it is visible and reversible.

### GAP-3 — "Current value" is undefined (blocks A5, H7)
Three defensible definitions, each giving a different number: live weight × live-rate; meat weight × meat rate (doc 31's basis); or cost basis (what we have spent). The Owner and Accounts Head must pick a **farm-level valuation policy**, stored as configuration, and **every report that prints a value must print which policy produced it.** For H7 (Herd Valuation), which feeds the balance sheet, the policy choice is an accounting decision, not a UI preference.

### GAP-4 — Reports are not reproducible (audit requirement)
Feed unit cost is a weighted average that moves; a report run in March and re-run in September will not match, and neither run can prove what it said. Add `Farm360.Domain/Reporting/ReportRun.cs`: report key, parameters JSON, run-by user, run-at UTC, row count, rendered-file SHA-256, and the **rates in force at run time** (meat price, feed WAC, valuation policy). Financial reports store the rendered PDF in blob storage with a signed download link. Without this, the report pack is not audit-grade and the Accounts Head cannot sign it.

### GAP-5 — Bengali shaping
See §1.2. Resolved by the engine choice, validated by the Phase 0 spike.

### GAP-6 — Rasterized PDFs
See §3.6. Resolved by moving rendering server-side.

### GAP-7 — No indexes for report-shaped access
Per-animal-over-a-year queries currently have no supporting index. Needs covering indexes on `AnimalFeedAllocation(AnimalId, Date)`, `MedicalTreatment(AnimalId, StartDate)`, `WeightRecord(AnimalId, RecordedDate)`, `AnimalCostLedger(AnimalId)`, plus `FinancialTransaction(FarmId, TransactionDate, Category)`. Confirm with an execution plan, not by assumption.

---

## 5. UX and report design standard

**Every Farm360 report shares one skeleton** — consistency is what makes a report pack feel like SAP rather than thirty separate screens:

1. **Letterhead** — org logo, organization name, farm name and address, report title in English and Bangla.
2. **Parameter echo box** — bordered, two-column: the exact filters used, plus "Generated by <user> on <date time>". Never let a printed page leave the building without saying what it is a report *of*.
3. **Detail band** — 11 px, 1 px black rules, grey header fill, left-aligned text, **right-aligned numerics with fixed decimals**, group sub-total rows in bold with a light fill.
4. **Grand total** — double top rule, bold.
5. **Page footer** — `Page X of Y`, report key and run id (small, grey) so any printed page can be traced back to its `ReportRun`.
6. **Signature block** — on financial and valuation reports only: Prepared by / Checked by / Approved by, with rules.

**Report Center** — left tree by module, card grid with description and last-run time, search, favourites, "Recently run". **Parameter panel** — persistent, remembers each user's last values per report, validates before enabling Run. **Viewer** — the A4 sheet on a grey canvas, zoom (fit-width / 100% / whole page), page navigation, thumbnail rail for long reports, sticky toolbar with Print · PDF · Excel · CSV · Schedule.

**Drill-down** — an animal tag in a herd report is a link that opens that animal's report with the period carried over. This is what turns a report pack into an investigation tool and is the main thing the FrostTrack reports cannot do.

**Language** — every column label and title carries an `(en, bn)` pair in the definition; a toggle in the toolbar switches the rendered report. Numerals stay Western Arabic by default with a farm-level option for Bengali numerals (১২৩), because printed financial documents in Bangladesh go both ways.

**Dark mode** — the viewer chrome themes; the paper is always white. A report is a piece of paper.

**Mobile** — catalog, parameters and Run work on a phone; the viewer offers "Export PDF" rather than pretending A4 fits a 390 px screen. Do not build a responsive report; build a good export button.

**Accessibility** — WCAG 2.1 AA on the chrome: labelled parameters, keyboard-navigable catalog and pagination, focus management when the viewer loads, `aria-live` announcement of "Report ready, 7 pages". The rendered report is a document, and the exported PDF carries tagged-text structure from QuestPDF.

---

## 6. Performance

| Budget | Target |
|---|---|
| Catalog load | < 300 ms |
| On-screen first page (typical report, ≤ 500 rows) | < 800 ms |
| PDF, 10,000 rows | < 5 s |
| XLSX, 50,000 rows | < 10 s, streamed |
| Herd register, 5,000 animals | async job + download link, never a blocking request |

Rules: `AsNoTracking` and projection straight to the report row DTO — never load aggregates and walk navigation properties; the viewer pages server-side while exports stream the full set; ClosedXML writes in streaming mode for large sheets; async exports run on the existing job infrastructure (`Farm360.Application/Feeding/Jobs/`) and notify via the existing SignalR channel; the catalog and each report's metadata are cached per role for 5 minutes. Add a hard row cap (say 200,000) with a clear error rather than an out-of-memory failure at 3 a.m.

---

## 7. QA strategy

The failure mode that matters is not a crash — it is **a report that prints a number the screens disagree with.** Test for that specifically:

1. **Cross-module reconciliation tests** (the important ones). The total feed cost on A1 for an animal and period must equal the feed component of A6's ledger for the same animal and period, and must equal that animal's contribution to the batch P&L. Assert these as integration tests. When GAP-1's allocation lands, add: the sum of `AnimalFeedAllocation` for a group plan on a date must equal that `DailyFeedingEntry`'s `TotalCostBdt` exactly — allocation must be lossless to the paisa, with the rounding remainder deterministically assigned.
2. **Golden-file rendering tests** — render each report against a seeded dataset, assert page count, extracted text of page 1 and the grand-total line against a checked-in baseline. Catches layout regressions that unit tests never see.
3. **Bengali rendering test** — a fixture with conjuncts (ক্ষ, ঙ্গ), pre-posed matras (কি) and ra-phala; assert the extracted text round-trips and eyeball the baseline image once per release.
4. **Tenant isolation** — for every report key, a test that farm A's parameters cannot return farm B's rows. Parameterised over the registry, so a new report cannot be added without being covered.
5. **Permission matrix** — `reports.view` vs `reports.export` vs no permission, per report.
6. **Performance regression** — the §6 budgets asserted in CI against a seeded 5,000-animal dataset.
7. **Accounts Head UAT** — the Accounts Head signs off the finance pack against the current manual/Excel process before it replaces anything. The Farm Manager signs off A1–A5 against one real animal's paper records. Nothing goes live on developer confidence alone.

---

## 8. Phasing

| Phase | Work | Est. | Depends on |
|---|---|---|---|
| **0** | **Decisions + spike.** Bengali PDF spike across QuestPDF/Syncfusion/PDFsharp (1 day, decides everything downstream). Owner: licence. Accounts Head + Farm Manager: allocation method (GAP-1), labour/overhead policy (GAP-2), valuation policy (GAP-3). Sign off the catalog in §2 and the report skeleton in §5 | 2 d | — |
| **1** | **Reporting platform — server.** Abstractions, registry, `ReportDataSet`, `ReportExecutionService`, QuestPDF renderer with the Farm360 banded document (letterhead, repeating headers, page X of Y, signature block), ClosedXML + CSV renderers, endpoints, `ReportRun` audit entity + migration. Prove it end to end with **one** report (A6 Animal Cost Ledger — data is ready, so the platform is the only variable) | 5 d | 0 |
| **2** | **Reporting platform — Angular.** Report Center, generic viewer, parameter panel, metadata-driven detail band, A4 paper stylesheet lifted from FrostTrack, exports, preferences, sidebar entry, route guards | 5 d | 1 (can start on the metadata contract after 1 is 2 days in) |
| **3** | **Data gaps.** `AnimalFeedAllocation` entity + allocation service + job wiring + historical backfill (GAP-1); labour/overhead allocation run (GAP-2); valuation policy configuration (GAP-3); indexes (GAP-7) | 4 d | 0 · parallel with 2 |
| **4** | **Single-animal pack** — A1–A8 | 5 d | 2, 3 |
| **5** | **Herd pack** — H1–H12 | 6 d | 4 |
| **6** | **Finance pack** — port the existing queries into definitions; add signature blocks and `ReportRun` archiving | 4 d | 2 |
| **7** | **Scheduling & delivery** — `ReportSchedule` entity, cron job, email/WhatsApp delivery, "monthly animal report to the owner on the 1st". The `reports.schedule` permission already exists and is waiting for this | 4 d | 5, 6 |
| **8** | **Hardening** — golden-file suite, reconciliation tests, performance pass, UAT fixes, user manual | 4 d | all |

**Total ≈ 34 working days** for the full pack. Phases 2 and 3 run in parallel, so calendar time is closer to **28 days** with two developers (one .NET, one Angular).

### Suggested first slice for early feedback (≈ 10 days)
Phase 0 → Phase 1 → Phase 2 → **A6 + A1 + A2 only**. That puts a real, printable, exportable report pack for one animal in the Farm Manager's hands in two weeks, validates the platform against the hardest constraint (GAP-1 allocation feeding A1), and lets you course-correct the report skeleton before thirty definitions are written against it.

---

## 9. Decisions taken

Recorded here so they are reviewable rather than buried in code. Items marked **provisional** are engineering defaults chosen to keep Phase 1 moving; each needs a named business owner to confirm or overturn, and each is a configuration change, not a rewrite.

| # | Decision | Taken | Owner to confirm |
|---|---|---|---|
| 1 | **PDF engine: QuestPDF, Community licence.** Validated by the Phase 0 spike — HarfBuzz shapes Bengali conjuncts, reordered matras, reph and ra-phala correctly, as vector text with zero image XObjects. | Settled technically | **Owner** — confirm Farm360 is below the USD 1M revenue threshold; budget the Professional licence before crossing it |
| 2 | **Excel engine: ClosedXML (MIT).** Real numbers with number formats, frozen header, autofilter. | Settled | — |
| 3 | **Typeface: Noto Sans Bengali (SIL OFL 1.1), embedded in the assembly.** Not machine-installed — the API runs in a Linux container with no Bengali font. Licence text ships at `Reporting/Fonts/OFL.txt`. | Settled | — |
| 4 | **Feed allocation for group-fed animals: metabolic-weight-weighted (W^0.75).** Equal-split makes calves look unprofitable and mature bulls look efficient, because a 400 kg bull genuinely eats more than a 150 kg calf. Persisted per row with the method recorded. | **Provisional** | **Farm Manager + Accounts Head** (GAP-1) — this number appears on every per-animal cost report |
| 5 | **Labour and overhead allocation: per head-day**, posted to `AnimalCostLedger` under its own transaction category so it is visible and reversible. | **Provisional** | **Accounts Head** (GAP-2) |
| 6 | **Valuation policy: farm-level configuration** with three options (cost basis / live weight × market rate / meat yield × meat rate). Default `MeatYieldMarket`, matching doc 31 and how cattle are actually priced locally. Every report printing a value also prints the policy that produced it. Balance-sheet feed uses cost basis. | **Provisional** | **Owner + Accounts Head** (GAP-3) |
| 7 | **Default language English, Bangla per-report toggle; Western numerals by default** with a farm-level Bengali-numeral option. Every label carries an `(en, bn)` pair, so switching costs nothing later. | **Provisional** | Owner |
| 8 | **Scope: the 10-day first slice** — platform, then A6 / A1 / A2 — rather than the full 34-day pack up front, so the report skeleton can be corrected before thirty definitions are written against it. | Settled | — |
| 9 | **Scenario storage kept** (not dropped from v1). | Settled | — |

### 9.1 Phase 0 — spike result

Rendered an A4 Bengali report through QuestPDF + a bundled Bengali font and inspected the output. All complex-script cases render correctly: `ক্ষ`, `ঙ্গ`, `স্ত্র` conjuncts; `কি` (the *i*-matra stored after the consonant but drawn before it); `কো` (two-part matra wrapping both sides); `প্র` ra-phala; `র্ক` reph. PDF internals confirmed **0 image XObjects, 4 embedded font files, 265 KB for 4 pages** — genuine vector text, against the multi-megabyte bitmaps html2canvas was producing. Repeating header band and `Page X of Y` both work.

### 9.2 Phase 1 — what was built

| Layer | Delivered |
|---|---|
| Domain | `Reporting/ReportRun.cs` (audit, GAP-4), `IReportRunRepository` |
| Application | `Reporting/Model` (primitives, `ReportColumn`, `ReportParameter`, `ReportDataSet`, `ReportValueFormatter`), `Reporting/Abstractions` (`IReportDefinition`, `ReportDefinition<TRow>`, `ReportContext`, `IReportRenderer`), `Reporting/Registry`, `Reporting/Services/ReportExecutionService`, `Reporting/DependencyInjection` |
| Infrastructure | `Reporting/ReportFonts` (embedded fonts + sfnt validation), `Reporting/Pdf/Farm360ReportDocument` + `QuestPdfReportRenderer`, `Reporting/Excel/ClosedXmlReportRenderer`, `Reporting/Csv/CsvReportRenderer` |
| Persistence | `ReportRunConfiguration`, `ReportRunRepository`, `ReportRuns` DbSet, migration `20260919070242_AddReportRuns` (creates the `reporting` schema and that one table — no drift) |
| Api | `ReportEndpoints` — catalog, metadata, run, export (pdf/xlsx/csv), run history. `reports.view` on the group, `reports.export` on export |
| First report | `Definitions/Livestock/AnimalHealthReportDefinition` (report A2), merging treatments, vaccinations and incidents into one chronological band grouped by month |

**Verified end to end:** 70 rows across 5 month groups → sub-totals 16,546.59 + 17,164.25 + 20,766.49 + 18,549.34 + 22,158.93 = **95,185.60 grand total** (exact); PDF 5 pages with repeating letterhead and column header, group sub-total bands, double-ruled grand total, signature block, `Page X of Y`; XLSX 10.5 KB with live numbers; CSV with raw unformatted values. Solution builds clean under the repo's warnings-as-errors analyzers; all 168 existing tests pass, including the 7 architecture tests.

Two defects found and fixed during verification, both worth noting because they would have shipped silently:
- The first font download returned a GitHub 404 HTML page of plausible size (268 KB), which QuestPDF rejected with an unhelpful "cannot load the provided font data". `ReportFonts` now validates the sfnt signature and names the offending resource.
- Long detail rows were being sliced by page breaks, stranding a text fragment at the top of the next page with every other column blank. Body cells now use `ShowEntire()`.

### 9.3 GAP-1 — closed

Per-animal feed allocation is implemented, configurable and tested. What the join alone could not do, and what was built instead:

**The proposed join is real but partial.** `GetEntriesByAnimalIdAsync` in `FeedingPlanRepositories.cs:158` already performs exactly it — `AnimalFeedingPlans.Where(p => p.AnimalId == animalId)` → plan ids → entries — and **returns `Array.Empty` when the animal has no individual plan**. `AnimalFeedingPlan.AnimalId` is nullable because a plan may cover a batch, shed or pen, so every group-fed animal reported **zero** feed cost, and `GetAnimalCostLedgerQuery` then left `TotalFeedCostBdt` at 0. It read as "no data" rather than "wrong data", which is why it survived this long.

**A second defect found while building.** Feeding rule lines are matched on a per-animal weight band ("200-250 kg → 3 kg/day") and `WeightPercentage` computes `bodyWeight × %` — so a group plan's entry records **one animal's ration**, not the group's total. Nothing multiplied it by head count. A 20-head batch's feed was being recorded at 1/20 of reality. Now handled by `GroupPlanQuantityBasis` (default `PerHead`), and configurable for farms that record group totals instead.

| Delivered | Where |
|---|---|
| `FeedAllocationCalculator` — pure W^0.75 split, lossless via largest-remainder | `Farm360.Domain/Feeding/Allocation/` |
| `AnimalFeedAllocation` entity + repository + EF config + migration `AddAnimalFeedAllocations` | Domain / Persistence |
| `FeedAllocationService` — resolves plan scope to animals, applies the rules | `Farm360.Application/Feeding/Services/` |
| Daily job writes allocations at the moment feed is recorded | `CreateDailyFeedingEntriesCommand` |
| `BackfillAnimalFeedAllocationsCommand` + `POST /api/v1/feeding/allocations/backfill` — idempotent, chunked, resumable | Application / Api |
| Cost ledger now reads allocations instead of the plan join | `GetAnimalCostLedgerQuery` |
| **Report A1** — `feeding.animal-feeding`, date-wise kg / cost / rule / recipe, monthly sub-totals | `Reporting/Definitions/Feeding/` |
| Configurable business rules (all four decisions) | `Farm360.Domain/BusinessRules/`, [docs/33](33_Farm360_Configurable_Business_Rules.md) |

**Date-accurate membership.** `AnimalMovement` carries dated shed/pen occupancy, so the scope lookup resolves the herd *as it stood on the entry date* — backfill reconstructs history correctly for shed and pen plans. Batch membership has no history (`Animal.BatchId` is a current-value column), so batch-scoped backfill is approximate; those rows are flagged `IsBackfilled` so an auditor can tell.

**Verified with test data:** 25 new tests, all passing (193 total). `FeedAllocationCalculatorTests` (14) covers the W^0.75 ratio against the standard formula, order-independence, unweighed animals, and a 500-random-herd property test asserting shares always reconcile exactly to the group total. `FeedAllocationServiceTests` (11) covers the GAP-1 bug directly — group-fed animals now receive non-zero costs — plus ration × head count, the `WholeGroup` alternative, heavier animals carrying more, empty scopes allocating nothing, actual-overrides-planned, provenance capture, and a rule change taking effect. Report A1 rendered to a 4-page A4 PDF whose grand total matches an independent sum.

### 9.5 GAP-2 — closed, and reports A3 / H6 built

`AnimalCostLedger.TotalCostBdt` is now a **full-absorption cost**.

| Delivered | Where |
|---|---|
| `ProportionalSplit` — the lossless largest-remainder split, now shared by feed and overhead | `Farm360.Domain/Common/` |
| `OverheadAllocationCalculator` — head-days and weight-days, pure | `Farm360.Domain/Finance/Allocation/` |
| `AnimalOverheadAllocation` entity + repository + EF config + migration `AddAnimalOverheadAllocations` | Domain / Persistence |
| `OverheadAllocationService` — resolves the herd, splits each expense, recomputes ledgers | `Farm360.Application/Finance/Services/` |
| `AllocateOverheadCommand` + `POST /api/v1/farms/{farmId}/finance/overhead/allocate` | Application / Api |
| `UpdateLaborCost` / `UpdateOverheadCost` — **set**, not accumulate | `AnimalCostLedger` |
| `OverheadCostPeriod` business rule | `FarmBusinessRules`, [docs/33 §2.3](33_Farm360_Configurable_Business_Rules.md) |
| **Report A3** `livestock.animal-growth` — weigh-ins with interval gain, ADG, feed, FCR, cost per kg gain | `Reporting/Definitions/Livestock/` |
| **Report H6** `livestock.herd-performance` — herd ranked worst-first by cost per kg, quartile bands, A4 landscape | `Reporting/Definitions/Livestock/` |

**Idempotency is structural, not incidental.** Transactions already allocated are skipped, and the ledger buckets are then *recomputed* from the allocation table rather than incremented. A re-run therefore writes nothing and changes nothing, which is what makes the job safe to schedule monthly, resume after a failure, or re-run for a corrected month. The unique index `UX_AnimalOverheadAllocations_Transaction_Animal` is the database-level backstop behind the service's own check, so two concurrent runs cannot both decide a transaction is unallocated.

**Only indirect categories are allocated.** `LaborCost` → labour; `Utilities`, `Transport`, `MiscellaneousExpense`, `ConsumableExpense` → overhead. Feed, veterinary, medicine and purchase are excluded because their own modules already attribute them; so are expenses that already carry an `AnimalId`.

### 9.6 Verification run

Executed end to end against a **real SQL Server** — a throwaway `Farm360_GapVerify` LocalDB database created from the migrations. Neither `Farm360_Dev` nor `farm360_prod` was touched.

Seed: one farm, one batch, four head at 150 / 200 / 300 / 450 kg, ten days of batch-level feed at 3.0 kg × 44.30 BDT/kg recorded the pre-GAP-1 way, plus April wages 30,000, electricity 6,000 and a 99,999 feed purchase included as a control.

| Step | Result |
|---|---|
| 1 · narrow backfill (01–03 Apr) | 3 entries → 12 allocation rows |
| 2 · reconcile to the known batch | **36.000 kg / 1,594.80 BDT — exact.** One day's split: 150 kg → 1.935 kg (16.12%), 200 → 2.401 (20.01%), 300 → 3.254 (27.12%), 450 → 4.410 (36.75%); day total 12.000 kg exact. Equal-per-head would have given each 25% |
| 3 · full backfill | +7 entries → 40 rows total, **120.000 kg / 5,316.00 BDT — exact**. Immediate re-run: 0 entries, 0 rows (idempotent) |
| 4 · overhead allocation, April | 2 of 3 transactions allocated, 8 rows, 36,000.00 BDT, 4 ledgers updated. Re-run: 0 allocated, 2 skipped (idempotent). **The 99,999 feed purchase was correctly not allocated** |
| 5 · reports | **A1** 10 rows, 19.630 kg / 869.70 BDT — matches the ledger's feed bucket exactly. **A3** 3 weigh-ins with intervals: 5 days, 3.50 kg gain, 9.82 kg feed, FCR 2.80, 124.24 BDT per kg gain. **H6** 4 animals ranked worst-first in quartile bands, rendered to a 48 KB PDF |

Final ledgers (full absorption): labour 30,000.00 and overhead 6,000.00 reconcile exactly to the source expenses; every animal carries 7,500 + 1,500, correct for `PerHeadDay` when all four were present for all 30 days.

**Caveat on H6's ranking metric.** It ranks by cost per kg of *live weight*, which at these acquisition costs (60–75k against 150–450 kg) is dominated by purchase price — so the ranking currently tracks weight almost inversely and says more about buying than about husbandry. Cost per kg of *gain* over the period would be the more useful management signal. The data for it is now present; say the word and it is a one-line column change.

### 9.4 Still open

- **Phase 2 (Angular) — delivered.** `features/reports/`: `ReportService`, metadata-driven `ReportParameterPanelComponent`, `ReportPaperComponent` (A4 sheet whose bands mirror the QuestPDF document), `ReportCenterComponent` (catalog by module, search) and `ReportViewerComponent` (run, print, export PDF/XLSX/CSV, remembered parameters). Route `/reports` plus a sidebar entry. Angular build clean.
  ~~Entity pickers for Animal/Batch/Farm/Shed/Breed parameters~~ **built** -- `ReportEntityPickerComponent` (`features/reports/components/report-entity-picker/`). Farm and Breed render as plain dropdowns (bounded lists); Animal is a debounced, farm-scoped autocomplete (herds run into the thousands); Batch and Shed are farm-scoped dropdowns. Animal-only reports (A1/A2/A3) fall back to the app's active farm from `WorkingContextService` for scoping, so the user is never asked to pick a farm twice; H6's own `farmId` parameter takes precedence when present. Wired into `ReportParameterPanelComponent` via one new `entity` case -- no per-report frontend change needed.
  Remaining viewer polish, not yet built: client-side page pagination of the on-screen sheet, zoom controls and the thumbnail rail.
- ~~GAP-2~~ **closed — see §9.5.**
- **QuestPDF revenue confirmation outstanding** — the Community tier is declared in configuration; only the business can verify it is entitled to it (docs/33 §2.6).
- ~~A3 and H6~~ **built — see §9.5.**
- `ReportMetaHeader.FarmName` is wired through but not populated.
- Golden-file and cross-module reconciliation tests (§7) are designed but not yet written.
