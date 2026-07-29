# Farm360 AI: Smart Farm Intelligence Platform Evolution Strategy

> **"Transforming Farm360 from a system of record into a system of intelligence."**

This document outlines the strategic architectural roadmap to evolve Farm360 from a traditional CRUD-based Farm ERP into an AI-driven Decision Support System (DSS). It addresses all 17 strategic deliverables required to prepare the platform for this paradigm shift.

## User Review Required
> [!IMPORTANT]
> **Executive Approval Needed**
> Please review this updated evolution strategy, which now includes the detailed "Calf Lifecycle" predictive modeling use case and the decision to use .NET Hosted Services. Upon your approval, we will proceed to create detailed component-level tasks in `task.md`.

---

## 1. Current System Assessment
Farm360 has successfully established a robust, enterprise-grade foundation:
- **Architecture**: Clean Architecture, DDD, CQRS (MediatR), Minimal APIs, .NET 10.
- **Frontend**: Angular 22, Signal-based reactivity, OnPush change detection, Enterprise Material UI.
- **Core Modules Built**: Multi-Tenant Identity, Organization/Farm hierarchy, Livestock (core records), Health & Vaccination, Smart Feeding (basic FCR & rations).
- **Strengths**: Highly modular, strictly follows SOLID/DRY, strict tenant isolation, excellent separation of concerns, strong domain model encapsulation, comprehensive test coverage.
- **Weaknesses/Limitations**: The system is predominantly reactive. It waits for user input and records what *has happened* (CRUD). It does not yet tell the user what *will happen* or what *should happen*. 

## 2. Gap Analysis
| Capability | Current State | Target State | Gap |
|---|---|---|---|
| **Data Nature** | Historical & Current (CRUD) | Predictive & Prescriptive | Lack of forecasting and recommendation engines. |
| **Workflow** | User initiates actions | System prompts optimized actions | Need Event-Driven Rule/Alert Engines. |
| **Financials** | Basic cost tracking | Per-animal real-time ROI & break-even forecasting | Lack of continuous Profit/Cost Calculation Engine. |
| **Feeding** | Static formulas & logging | Dynamic adjustments based on weight/cost targets | Missing Nutrition & Growth Prediction models. |
| **UI/UX** | Data tables & forms | Insight cards, action prompts, trend graphs | Frontend needs to surface insights proactively. |

## 3. Target Product Vision
Farm360 will become an **Active Decision Support System (DSS)**. It will act as a virtual farm consultant that continuously analyzes farm data to answer:
- *What is the optimal feed ration today to maximize profit?*
- *When is the exact optimal day to sell an animal based on market prices and diminishing growth returns?*
- *Which animals are underperforming against their growth targets?*
- *What operational risks (health, inventory stockouts) require immediate attention?*

## 4. Domain Evolution Plan
To support the DSS, the Domain Layer must expand to include predictive and analytical concepts:
- **Performance Metrics**: `FeedConversionRatio`, `AverageDailyGain` (promoted to rich value objects with target baselines).
- **Growth Targets**: `GrowthCurve`, `TargetWeight`.
- **Financial Projections**: `ProfitMargin`, `BreakEvenPrice`, `CostPerKgGain`.
- **Recommendations**: `ActionableInsight`, `FeedAdjustmentRecommendation`.

*Architectural Boundary*: These concepts will primarily live in a new bounded context (`Farm360.Domain.Intelligence` or `Farm360.Domain.Analytics`) to prevent polluting the core transactional domains (`Livestock`, `Feeding`).

## 5. Decision Support Architecture
We will leverage our existing **Event-Driven Design**. Every business event will trigger the DSS Engine.

**Event Flow Example:**
1. `WeightRecordedEvent` is published by the `Livestock` module.
2. `GrowthAnalysisEngine` (in Application layer) handles the event asynchronously.
3. Engine calculates new ADG and compares it to the `GrowthTarget`.
4. If underperforming, it triggers the `NutritionEngine`.
5. `NutritionEngine` generates a `FeedAdjustmentRecommendation`.
6. `RecommendationEngine` pushes an actionable alert to the frontend via SignalR.

### 5.1 Core Use Case Example: The Calf Predictive Lifecycle
To illustrate exactly how this system solves real farming workflows, here is how the engines will collaborate when a calf is born:

**Day 1: Birth & Initial Registration**
* **Action**: The user logs the birth of a calf and inputs its initial live weight.
* **Engine Response**:
  * **Nutrition Engine**: Instantly calculates the exact daily feed requirement (milk replacer, starter) and the precise protein/energy balance required based on the calf's breed and weight.
  * **Growth Engine**: Projects the target daily weight gain.
  * **Cost Engine**: Forecasts the total feed volume needed for the next week and month, and calculates the exact projected expenditure.
* **UI Display**: The animal's profile immediately surfaces an "Intelligence Panel" showing the *Recommended Daily Ration*, *Expected 30-Day Weight*, and *Projected 30-Day Feed Cost*.

**Day 15: Weight Logging & Re-evaluation**
* **Action**: The user logs the calf's new weight.
* **Engine Response**:
  * **Growth Engine**: Calculates the actual Average Daily Gain (ADG) and compares it against the expected growth curve.
  * **Cost Engine**: Calculates the exact money invested in the calf up to this point (feed + overhead).
  * **Nutrition Engine (Feedback Loop)**: 
     * If growth is *below* target: It automatically suggests a feed adjustment (e.g., "Increase protein by X%" or "Adjust ration volume").
     * If growth is *on track*: It validates the current strategy.
* **UI Display**: The dashboard updates to show *Current Money Invested*, *Growth Status (On Track / Underperforming)*, and the *New Feed Recommendation* to correct any growth deficits.

## 6. New Domain Model Proposal
**New Entities & Value Objects (Intelligence Context):**
- `Breed` (Aggregate Root): Centralized master data for breed intelligence. Replaces free-text `BreedName` on `Animal`. Contains detailed metrics: ADG by farming condition, FCR ranges, expected milk yields, and fat percentages.
- `Insight` (Entity): A generated recommendation (e.g., "Increase CP by 2% for Batch A").
- `MarketPriceTracker` (Value Object): Daily market rates for beef/milk.
- `CostProjection` (Value Object): Forecasted costs over the next 30/60/90 days.
- `AnimalPerformanceScore` (Value Object): Composite score based on health, growth, and cost.

*Note on PerformanceTarget*: The previous simplistic `PerformanceTarget` entity will be deprecated and replaced by the comprehensive `Breed` entity, which holds all target data required by the intelligence engines.

## 7. Module Dependency Map
```text
[Farm360.Web (Angular UI)] 
       │
[Farm360.Api (Minimal APIs)] 
       │
[Farm360.Application] ───► [Farm360.Application.Intelligence (Engines)]
       │                                  │
[Farm360.Domain (Core)] ◄─────────────────┘
 (Livestock, Health, Feeding, Finance, Inventory)
```

## 8. Smart Engine Architecture
We will introduce the following engines as Application-level Services (orchestrated via MediatR handlers responding to domain events):
1. **Rule Engine**: Evaluates domain events against configurable business thresholds.
2. **Growth Prediction Engine**: Extrapolates current ADG to project future weights at 30, 60, 90 days.
3. **Cost & Profit Engine**: Continuously calculates total cost of ownership (TCO) per animal and projects ROI based on current market rates.
4. **Nutrition & Feed Calculation Engine**: Suggests feed formula tweaks to optimize the cost-to-weight-gain ratio.
5. **Recommendation & Alert Engine**: Consolidates outputs from other engines and pushes high-signal alerts to the user.

## 9. Database Evolution Strategy
- **Event Sourcing / Read Models**: For heavy analytics, we will introduce specialized CQRS Read Models (flattened tables) updated via Domain Events to ensure complex metric queries run in <100ms.
- **Time-Series Data**: We will store historical trends (daily cost, daily weight projections) to render predictive charts without heavy on-the-fly computation.

## 10. API Evolution Strategy
- **Insight Endpoints**: Introduce `GET /api/v1/insights/farms/{farmId}` to fetch prioritized recommendations.
- **Simulation Endpoints**: Introduce "What-If" APIs (e.g., `POST /api/v1/analytics/simulate-profit`) allowing the frontend to project ROI if an animal is sold in X days.
- **Real-time Push**: Enhance the existing SignalR `NotificationHub` to push `ActionableInsight` events instantly to the client.

## 11. Frontend Evolution Strategy
- **Shift from Forms to Dashboards**: Detail pages (like `AnimalDetail`) will feature an "Intelligence Panel" at the top, showing Projected Value, Current Cost, and active Recommendations.
- **Micro-animations**: Use subtle pulse animations for urgent insights (following the Premium UI mandate).
- **Interactive Charts**: Implement forward-looking charts (dotted lines for predictive trends) using robust charting libraries.

## 12. Dashboard & KPI Strategy
- **Executive Dashboard**: Overall Farm Profitability, Expected Cash Flow, High-Risk Alerts.
- **Operational Dashboard**: Feed Inventory Runway (Days remaining), Vaccination Due Today, Underperforming Batches.
- **New KPIs**: Cost per Kg of Gain, Projected Profit Margin, Feed Wastage Percentage.

## 13. Reporting Strategy
- **Proactive Reporting**: Shift from "generate report" to "scheduled insights" emailed or sent via SMS/WhatsApp to the farm manager.
- **Predictive Reports**: "End of Month Projection Report" detailing expected costs and sales revenues.

## 14. AI & Analytics Roadmap
1. **Heuristics (Now)**: Hardcoded expert rules (e.g., If ADG < Target, suggest +Energy).
2. **Statistical Algorithms (Next)**: Linear regressions for growth curves and cost forecasting.
3. **Machine Learning (Future)**: Train ML models on aggregated, anonymized tenant data to predict disease outbreaks and optimize feed formulas dynamically.

## 15. Implementation Roadmap (Prioritized Phases)

### Phase 1: Foundation & Financial Intelligence (Immediate Next Step)
- Create `Farm360.Domain.Intelligence` bounded context.
- Implement the **Cost & Profit Engine** (calculates exact daily cost per animal based on feed + health + overhead).
- Enhance UI to display Real-time Profit/Loss per animal/batch.

### Phase 2: Growth & Nutrition Intelligence
- Implement the **Growth Prediction Engine** (ADG extrapolation).
- Connect `WeightRecordedEvent` to trigger predictive recalculations.
- Implement the **Rule Engine** to detect underperforming animals and suggest feed adjustments.

### Phase 2.5: Breed Master Data Intelligence
- **Domain Refactoring**: Introduce `Breed` aggregate root. Remove `BreedName` (string) from `Animal` and replace with `BreedId` (Guid).
- **Engine Updates**: Refactor `GrowthPredictionEngine` and `CostAndProfitEngine` to query the new `Breed` repository for precise targets (ADG, FCR) instead of generic estimations.
- **Frontend Setup**: Build the Breed Management Setup page (CRUD) in Angular. Update the Animal Entry Form to use a dynamic Breed dropdown instead of an open text field.

### Phase 3: Proactive UI & DSS Dashboards
- Build the "Smart Consultant" UI (Insight cards, recommendation alerts).
- Implement SignalR push for real-time decision prompts.
- Build "What-If" simulation UI (e.g., "Should I sell today or in 30 days?").

## 16. Risk Assessment
- **Performance Risk**: Heavy calculations on every event could slow down transactional writes. 
  *Mitigation*: Run engines asynchronously using .NET Hosted Services (see Refactoring Recommendations).
- **Data Quality Risk**: Predictive models fail if baseline data (feed costs, weights) is missing.
  *Mitigation*: Engines must handle sparse data gracefully and prompt the user to input missing data (e.g., "Log weight today to unlock profit projections").
- **UI Clutter Risk**: Too many alerts cause alert fatigue.
  *Mitigation*: Implement an Alert Prioritization Engine; only show top 3 highest-value insights.

## 17. Refactoring Recommendations
- **Domain Events**: Ensure all state changes in `Livestock` and `Feeding` emit rich domain events containing before/after states.
- **Background Processing (.NET Hosted Services)**: As per the architectural decision, we will standardize on **.NET Hosted Services (`IHostedService` / `BackgroundService`)** utilizing `System.Threading.Channels` for processing DSS rules asynchronously. This avoids the database overhead of Hangfire for transient rule evaluation while providing robust, high-performance background execution.
- **Frontend State**: Ensure NgRx Signal Store can reactively merge incoming SignalR insights with existing state without manual reloads.

---

> [!NOTE]
> Please provide your feedback or approval on this strategy. Once approved, I will translate Phase 1 into actionable development tasks and begin execution.
