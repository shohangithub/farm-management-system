# 🌾 Farm360 AI — Comprehensive Enterprise User Manual

Welcome to **Farm360 AI**, an end-to-end Enterprise Livestock & Farm Management Platform powered by Artificial Intelligence, Domain-Driven Design (DDD), automated smart feeding workflows, real-time inventory reconciliation, health management, and financial tracking.

---

## 📑 Table of Contents
1. [System Architecture & Tenant Context](#1-system-architecture--tenant-context)
2. [Getting Started & Authentication](#2-getting-started--authentication)
3. [Organizational Hierarchy & Facility Management](#3-organizational-hierarchy--facility-management)
4. [Executive Dashboard & Core Analytics](#4-executive-dashboard--core-analytics)
5. [Livestock Management Subsystem](#5-livestock-management-subsystem)
6. [Health & Veterinary Subsystem](#6-health--veterinary-subsystem)
7. [Smart Feeding & Nutrition Intelligence](#7-smart-feeding--nutrition-intelligence)
8. [Inventory & Stock Management](#8-inventory--stock-management)
9. [Finance & Cost Analytics](#9-finance--cost-analytics)
10. [User Roles & Standard Operating Procedures (SOPs)](#10-user-roles--standard-operating-procedures-sops)
11. [Troubleshooting & FAQs](#11-troubleshooting--faqs)

---

## 1. System Architecture & Tenant Context

Farm360 AI is built on a multi-tenant enterprise architecture. Every data point—from animal tags to inventory balances—is isolated per Organization/Farm context.

### Key Concepts
* **Working Context Selector**: Located in the top header, this allows farm managers and administrators to dynamically switch between **Organizations**, **Branches**, and **Farms**.
* **Automatic Scoping**: When a Farm context is selected, all pages (Feeding, Inventory, Health, Livestock) automatically filter data for that active farm context.

> [!IMPORTANT]  
> Always check your active **Working Context** in the top navigation bar before recording transactions (such as Stock-In receipts or Daily Feeding confirmations) to ensure records are posted to the correct facility.

---

## 2. Getting Started & Authentication

### 2.1 Logging In
1. Navigate to the Farm360 AI URL (`http://<your-domain>/auth/login`).
2. Enter your registered email address and security password.
3. Click **Sign In**.

### 2.2 Error Handling & Security
* If credentials fail or access token expires (401 Unauthorized), the login interface displays explicit error guidance.
* Session tokens auto-refresh securely in the background.

---

## 3. Organizational Hierarchy & Facility Management

Farm360 AI organizes agricultural infrastructure in a 4-level hierarchy:

$$\text{Organization} \longrightarrow \text{Branch} \longrightarrow \text{Farm} \longrightarrow \text{Shed} \longrightarrow \text{Pen}$$

### 3.1 Organizations & Branches (`/organizations`)
* **Organizations**: Corporate entity or commercial farm owner level.
* **Branches**: Geographic regions or business divisions (e.g., *North Sector Ranch*, *East Division Feedlot*).

### 3.2 Farms, Sheds & Pens
* **Farms**: Physical farming locations tied to a branch.
* **Sheds**: Enclosed or semi-enclosed housing structures (e.g., *Dairy Barn A*, *Fattening Shed 2*).
* **Pens**: Specific pens or cubicles inside a shed (e.g., *Pen 101*, *Calf Pen B*). Animals reside in pens, enabling localized animal grouping and bulk feeding operations.

---

## 4. Executive Dashboard & Core Analytics (`/dashboard`)

The Executive Dashboard provides farm owners and managers with high-level operational intelligence:

* **Herd Population**: Total live count, active vs. quarantined vs. sold/deceased status breakdown.
* **Health & Mortality Alerts**: Active disease incidents, urgent vaccinations due today.
* **Feed & Cost Efficiency**: Real-time Feed Conversion Ratio (FCR) tracking and cost per kg weight gain.
* **Inventory Stock Valuation**: Live total stock valuation in local currency (BDT/USD) based on Moving Average Costing.

---

## 5. Livestock Management Subsystem (`/livestock`)

### 5.1 Registering Animals (`/livestock/register`)
To register an individual animal:
1. Click **Register Animal**.
2. Select **Species** (Cattle, Goat, Sheep, Buffalo) and **Breed**.
3. Input **Tag ID / RFID**, Gender, Date of Birth / Age, Purchase Date, and Purchase Weight.
4. Assign the initial location (**Branch $\rightarrow$ Farm $\rightarrow$ Shed $\rightarrow$ Pen**).
5. Click **Save Record**.

### 5.2 Animal Batches (`/livestock/batches`)
* **Group Management**: Batch animals together for feedlot fattening or poultry/dairy herds.
* **Batch Analytics**: Track total batch weight, average weight gain, and batch FCR.

### 5.3 Weight Tracking & Growth Analytics
* Record periodic weight measurements on the **Animal Detail** page (`/livestock/:id`).
* The system computes **Average Daily Gain (ADG)** and alerts managers to under-performing animals.

---

## 6. Health & Veterinary Subsystem (`/health`)

### 6.1 Disease Incidents (`/health/incidents`)
* Log health symptoms or injuries as soon as they are observed.
* Status workflow: `Reported` $\rightarrow$ `Under Treatment` $\rightarrow$ `Recovered` $\rightarrow$ `Closed`.

### 6.2 Medical Treatments (`/health/treatments`)
* Record veterinary treatments, medication dosage, administered routes, and treating veterinarian.
* Integrates with inventory: Medical supplies consumed are deducted automatically.

### 6.3 Vaccination Protocols & Due Schedules (`/health/vaccinations`)
* **Protocols**: Define standard vaccination routines by age and species (e.g., *FMD Vaccine at 4 months*).
* **Due Schedules**: View upcoming and overdue vaccinations across the farm.

### 6.4 Deworming Calendar & Milk Withdrawal (`/health/deworming-calendar`, `/health/milk-withdrawal`)
* **Deworming**: Automated reminders for herd deworming cycles.
* **Milk Withdrawal Tracking**: Prevents milk contamination by highlighting cows currently under antibiotic withdrawal periods.

### 6.5 Mortality Records (`/health/mortality-records`)
* Log animal deaths with cause of death, necropsy reports, and financial write-off handling.

---

## 7. Smart Feeding & Nutrition Intelligence (`/feeding`)

Farm360 AI features an automated **Planned Feeding System** designed to eliminate manual daily log entry for every animal.

```
+---------------------+     +----------------------+     +-----------------------+
|  1. Feeding Rules   | --> |  2. Animal Enrollment| --> | 3. Auto Daily Entries |
| (Fixed / % Weight)  |     |   (Feeding Plans)    |     | (Generated by System) |
+---------------------+     +----------------------+     +-----------------------+
                                                                     |
                                                                     v
+---------------------+     +----------------------+     +-----------------------+
| 5. Inventory Deduct | <-- |  4. Reconciliation   | <-- | 4. Worker Execution   |
| (Approved Postings) |     |  (Manager Approval)  |     |  (Confirm / Adjust)   |
+---------------------+     +----------------------+     +-----------------------+
```

### 7.1 Ingredients & Ration Formulas (`/feeding/ingredients`, `/feeding/formulas`)
* **Ingredients Catalog**: Register raw ingredients (e.g., *Corn Silage*, *Soybean Meal*, *Wheat Bran*) with nutritional values and cost per kg.
* **Ration Formulas**: Create balanced ration formulas combining multiple ingredients with targeted percentage compositions.

### 7.2 Feeding Rule Sets & Rule Conditions (`/feeding/rules`)
Farm managers configure reusable feeding logic models. The system uses **Rule Conditions** to automatically calculate expected feed intake for enrolled livestock:

#### 1. Fixed Quantity per Head Mode (`FixedQuantity`)
* **How it Works**: Every enrolled animal receives a fixed, constant daily ration regardless of individual body weight or age.
* **Fields Required**:
  * **Feed Category**: Select Forage, Concentrate, Mineral, Additive, Silage, or Byproduct.
  * **Quantity**: Daily amount in kilograms (**kg**) per head.
* **Example**: Enroll adult dairy cows in a maintenance plan receiving $5.0\text{ kg}$ of Concentrate and $15.0\text{ kg}$ of Forage per day.

#### 2. Percentage of Body Weight Mode (`WeightPercentage`)
* **How it Works**: Scales daily feed dynamically based on the animal's latest live weight recorded in the system.
* **Fields Required**:
  * **Min Weight (kg) & Max Weight (kg)**: Weight bracket criteria.
  * **Feed Category**: Target feed type.
  * **Quantity/Pct**: Percentage (**%**) of live body weight.
* **Calculation Formula**:
  $$\text{Daily Feed (kg)} = \text{Current Live Weight (kg)} \times \left( \frac{\text{Quantity Value}}{100} \right)$$
* **Example**: For fattening bulls weighing between $300\text{ kg}$ and $500\text{ kg}$, set feed to $3.0\%$ of body weight. A $400\text{ kg}$ bull will automatically be assigned $400 \times 0.03 = 12.0\text{ kg}$ of daily feed.

#### 3. Age-Based Rules Mode (`AgeBased`)
* **How it Works**: Adjusts daily feed automatically as young animals mature through age brackets.
* **Fields Required**:
  * **Min Age (Days) & Max Age (Days)**: Age range in days.
  * **Feed Category**: Target feed type.
  * **Quantity**: Daily ration in kilograms (**kg**).
* **Example**: Calf Rearing Program:
  * **Days 0 – 60** (Calf Starter): $1.5\text{ kg/day}$ Concentrate.
  * **Days 61 – 180** (Weaner Grower): $3.5\text{ kg/day}$ Concentrate.

### 7.3 Assigning Animal Plans (`/feeding/plans`)
Enroll animals onto specific Rule Sets. The system dynamically computes the required daily feed for each animal.

### 7.4 Worker Workflow: Today's Feeding (`/feeding/today`)
* **Shed & Pen Grouping**: Designed for farm workers on mobile/tablets. Feed entries are grouped by Shed and Pen.
* **One-Tap Confirmation**: Workers confirm feeding per pen with a single tap of **Confirm All**, or individually edit actual amounts fed if variations occur.

### 7.5 Daily Cycle Reconciliation (`/feeding/reconciliations`)
* At the end of each daily cycle, system reconciles expected feed vs. actual confirmed feed.
* **Manager Approval**: Managers review feed variances. Upon approval, feed inventory items are automatically deducted from stock balances.

### 7.6 FCR & Feed Analytics
Track Feed Conversion Ratio (FCR), Feed Expenditure, and Cost per KG weight gain to maximize profit margins.

---

## 8. Inventory & Stock Management (`/inventory`)

### 8.1 Current Stock Report (`/inventory/current-stock`)
An enterprise **SAP-Style Stock Report** providing instant stock balances:
* View Item Name, SKU, Category, Current Quantity, Unit of Measure, Average Cost, and Total Valuation.
* Instant KPI cards highlight **Total Valuation**, **Low Stock**, and **Out of Stock** items.

### 8.2 Stock-In Receipts (`/inventory/items` / Stock-In Dialog)
When receiving new feed, medicine, or equipment:
1. Select Item from Catalog. Unit cost defaults automatically from catalog reference cost.
2. Enter Quantity received, Batch Number, Expiry Date, Supplier, and Invoice reference.
3. Post receipt: Inventory balance increases, and moving average unit cost is updated.

### 8.3 Supplier & Purchase Order Management (`/inventory/suppliers`, `/inventory/purchase-orders`)
* Manage vendor contact information and lead times.
* Create, submit, approve, and receive Purchase Orders directly in Farm360 AI.

---

## 9. Finance & Cost Analytics (`/finance`)

### 9.1 General Ledger & Expense Tracking (`/finance`)
* Log farm expenses categorized by Feed, Veterinary, Labor, Utilities, and Maintenance.
* Record revenue streams from Live Animal Sales, Milk Sales, and Dung/Byproduct Sales.

### 9.2 Cost per Animal & Profitability Analysis (`/finance/analytics`)
* Track total cumulative cost incurred per tag ID (Feed cost + Medical cost + Direct expenses).
* Evaluate margin upon animal sale to analyze profit margins by breed and feeding program.

---

## 10. User Roles & Standard Operating Procedures (SOPs)

| Role | Primary Responsibilities & Features Used |
| :--- | :--- |
| **System Administrator** | Organization setup, user management, working context permissions, global settings. |
| **Farm Manager** | Rule Set creation, Feeding Plan assignment, Reconciliation approvals, Purchase Orders, Finance analysis. |
| **Veterinarian** | Disease Incident logging, Medication treatments, Vaccination protocols, Deworming calendar, Mortality records. |
| **Farm Worker / Feed Master** | Daily feeding confirmations on `/feeding/today`, Weight logging, Physical stock counts. |

### Daily Operational Routine (SOP)
1. **06:00 AM — Morning Feeding**: Farm workers open `/feeding/today`, review assigned feed amounts per Pen, distribute feed, and tap **Confirm**.
2. **10:00 AM — Health Inspection**: Veterinarian checks due vaccinations and logs any sick animals in `/health/incidents`.
3. **04:00 PM — Evening Feeding**: Workers complete second feeding session.
4. **05:30 PM — Day-End Reconciliation**: Farm Manager reviews `/feeding/reconciliations`, verifies feed variance, and approves cycle to update inventory balances.

---

## 11. Troubleshooting & FAQs

#### Q1: Why are today's feeding entries not appearing in `/feeding/today`?
**A**: Ensure that animals are assigned to an active Feeding Plan (`/feeding/plans`) and that your active Working Context is set to the correct Farm.

#### Q2: Why did stock balance not decrease after feeding animals?
**A**: Feed consumption is held in pending status until the Farm Manager approves the Daily Reconciliation in `/feeding/reconciliations`. Once approved, stock balances update immediately.

#### Q3: How is Moving Average Unit Cost calculated on Stock-In?
**A**: When new stock is received, the new unit cost is calculated as:
$$\text{New Cost} = \frac{(\text{Existing Quantity} \times \text{Existing Cost}) + (\text{Received Quantity} \times \text{Received Cost})}{\text{Existing Quantity} + \text{Received Quantity}}$$

---

*Farm360 AI — Empowering Modern Agriculture with Intelligent Engineering.*
