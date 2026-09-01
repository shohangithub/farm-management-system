# Farm360 Feeding Module Guide

The Feeding Module is designed to handle both **smart, growth-based feeding** (dynamic) and **simple, recurring feeding** (fixed). 
Because the system supports both approaches, some concepts (like Plans vs. Schedules) can seem contradictory or overlapping. This guide clarifies how all the pieces fit together.

---

## 1. Feed Ingredients Catalog
**What it is:** The master dictionary of all raw materials you buy or grow.
**Domain Model:** `FeedIngredient`
**Description:** Defines the nutritional breakdown (Dry Matter, Crude Protein, Energy) and cost of a single ingredient. It links directly to your Inventory to track stock levels.
**Example:** *Napier Grass, Soybean Meal, Yellow Corn, Limestone.*

## 2. Feed Formulas & Ration Builder
**What it is:** The "Recipes" you create by mixing ingredients.
**Domain Model:** `FeedFormula` & `FormulaIngredient`
**Description:** Combines multiple ingredients by percentage to create a final feed mix. The system automatically calculates the combined nutritional profile and total cost per kg based on the ingredients used.
**Example:** *Dairy Cow Peak Lactation Mix (60% Corn, 30% Soya, 10% Minerals).*

## 3. Feeding Rule Sets (The "Smart" Engine)
**What it is:** The business logic that dictates *how much* an animal should eat based on its physical traits.
**Domain Model:** `FeedingRuleSet` & `FeedingRuleLine`
**Description:** A set of conditions (usually based on weight or age). Instead of saying "feed 2kg", you define a curve. 
**Example:** *Calf Growth Rule Set:*
- *Line 1: If weight is 50-100kg -> Feed 1.5kg of Calf Starter Formula.*
- *Line 2: If weight is 101-150kg -> Feed 2.0kg of Calf Grower Formula.*

## 4. Animal Feeding Plans (Dynamic)
**What it is:** The assignment of a "Rule Set" to a specific target (Animal, Batch, Pen, or Shed).
**Domain Model:** `AnimalFeedingPlan`
**Description:** This is a **dynamic** assignment. When you assign a plan, the system evaluates the animal's current weight against the Rule Set to determine the `CurrentRuleLineId` and `CurrentConcentrateKgPerDay`. Whenever a new weight is recorded, the plan *automatically updates* to the next rule line.
**Example:** *Assigning the "Calf Growth Rule Set" to Pen 3. As the calves in Pen 3 gain weight, the system automatically increases their daily feed allowance without manual intervention.*

## 5. Feeding Schedules (Fixed / Overlap Clarification)
**What it is:** A simple, fixed-quantity recurring schedule.
**Domain Model:** `FeedingSchedule`
**Clarification:** *This is where the contradiction usually arises.* While `AnimalFeedingPlan` is "smart" and calculates amounts based on weight, `FeedingSchedule` is "dumb". It is used when you just want to say "Feed X amount to Y target every day" regardless of weight or age. 
**Example:** *Feed 5kg of dry hay to Shed A every morning at 8 AM.* (No complex rules, just a fixed daily task).

## 6. Daily Feeding Records (The Output)
**What it is:** The actual tasks generated for today.
**Domain Model:** `DailyFeedingEntry`
**Description:** A background job runs (usually at midnight) and looks at all active **Feeding Plans** and **Feeding Schedules**. It generates a specific `DailyFeedingEntry` for today.
**Example:** *System generates an entry: "Today: Feed Cow #101 exactly 2.5kg of Peak Lactation Mix."*

## 7. Today's Feeding Workflow (The UI Process)
**What it is:** How farm workers interact with the system on a daily basis.
**Domain Model:** `FeedConsumptionLog`
**Description:** 
1. The farm worker opens the "Today's Feeding" screen and sees the `DailyFeedingEntry` list.
2. They physically feed the animals.
3. They click "Confirm" on the system. If an animal was sick and ate less, they enter the `ActualKg` (e.g., expected 2.5kg, actual 1.0kg) and a reason.
4. Confirming this creates a `FeedConsumptionLog` and deducts the exact amount of ingredients from the Inventory.

## 8. Feeding Reconciliations (Inventory Balancing)
**What it is:** End-of-cycle auditing to handle real-world discrepancies (spillage, waste, spoilage).
**Domain Model:** `FeedingCycleReconciliation`
**Description:** The system assumes that if you confirmed feeding 100kg of Corn, exactly 100kg left the silo. In reality, you might have spilled 5kg. 
**Example:** *At the end of the month, the system says you should have 400kg of Corn left in the silo. You physically measure it and find only 380kg. You create a Reconciliation to log the 20kg variance (shrinkage/waste), which corrects your inventory and updates your financial cost reports.*
