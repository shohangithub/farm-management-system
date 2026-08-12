# Farm360 Comprehensive UX Review Report

This document compiles the complete findings from a rigorous User Experience (UX) review of the Farm360 application, evaluated strictly from the perspective of a non-technical farm owner managing daily operations.

---

## 1. Top 10 UX Inconsistencies
*Evaluated using the Livestock module as the primary standard.*

**1. Action Placement (Primary Creation)**
*   **Livestock:** Primary action (`+ Add Animal`) is consistently in the top-right corner of the global page header.
*   **Health/Feeding:** Actions are sometimes buried inside secondary tabs or require selecting an item first.
*   **Why it confuses:** Users develop muscle memory to look top-right for "create" actions. Hiding them forces hunting.
*   **Standard:** Livestock pattern. Place primary "Create/Log" actions prominently in the top-right header.

**2. Form Display (Dialogs vs. Full Pages)**
*   **Livestock:** Editing uses a focused overlay dialog (`animal-edit-dialog`), keeping the user in context.
*   **Health:** Creating a Vaccination Protocol navigates to an entirely new page.
*   **Why it confuses:** Navigating away breaks flow and forces reliance on the browser "Back" button.
*   **Standard:** Livestock pattern. Use overlay dialogs for single-entity data entry.

**3. Empty States Actionability**
*   **Livestock:** Empty states often include a direct button ("No animals here. Click to assign").
*   **Inventory:** Empty stock ledgers display a dead-end "No data available" message.
*   **Why it confuses:** Dead-end states leave users guessing if they need to go to settings or if the system is broken.
*   **Standard:** Livestock pattern. All empty states must include an illustration, explanation, and direct CTA button.

**4. Detail Page Structure (Tabs vs. Scrolling)**
*   **Livestock:** Dense detail pages use horizontal tabs (Overview, Genetics, Health).
*   **Health:** Incident Detail pages stack all information vertically on a long scrolling page.
*   **Why it confuses:** Long scrolls overwhelm users. Tabs allow focused, contextual reading.
*   **Standard:** Livestock pattern. Use horizontal tabs to categorize dense information.

**5. Terminology (Submission Buttons)**
*   **Livestock:** "Save Changes".
*   **Feeding:** "Submit Record".
*   **Why it confuses:** "Submit" feels like sending a request; "Save" feels like updating a database.
*   **Standard:** Livestock pattern. Universally use "Save" or "Save Changes".

**6. Status Badges Design**
*   **Livestock:** Utilizes visual, color-coded pill badges (Green = ACTIVE).
*   **Health/Inventory:** Often uses plain colored text (e.g., "Treated").
*   **Why it confuses:** Pill badges are highly scannable; plain text blends into tables.
*   **Standard:** Livestock pattern. Use standardized color-coded pill badges for all statuses.

**7. Table Actions Layout**
*   **Livestock:** Actions (View, Edit) are direct icon buttons in the far-right column.
*   **Inventory:** Actions are sometimes hidden behind a "More options" (three dots) menu.
*   **Why it confuses:** Hiding frequently used actions adds an unnecessary click to every interaction.
*   **Standard:** Livestock pattern. Expose 3 or fewer actions directly as icons.

**8. Filtering and Search Bar Placement**
*   **Livestock:** Search/Filter placed directly inside the white card containing the data table.
*   **Feed:** Filters are sometimes placed in the grey page header above the card.
*   **Why it confuses:** Visual disconnect makes it harder to realize changing a filter affects the table below.
*   **Standard:** Livestock pattern. Keep controls directly above column headers inside the card.

**9. Success Feedback (Notifications)**
*   **Livestock:** Saving triggers a floating "Toast" notification top-right.
*   **Health:** Logging sometimes only flashes inline text or relies purely on dialog closure.
*   **Why it confuses:** Inconsistent feedback makes users worry silent saves have failed.
*   **Standard:** Livestock pattern. Universally use Toast notifications for create/update/delete operations.

**10. Loading States (Skeletons vs. Spinners)**
*   **Livestock:** Uses "skeleton loaders" mimicking data shapes.
*   **Dashboard/Health:** Uses a generic central spinning circle.
*   **Why it confuses:** Spinners make the app feel slower and cause UI jumping.
*   **Standard:** Livestock pattern. Use skeleton loaders to maintain a premium, stable UI feel.

---

## 2. Daily Operations Simulation (Friction Points)
*Ranked by impact based on a 12-scenario daily farming walkthrough.*

### Critical Impact: The Disjointed Animal Onboarding Workflow
When a new animal arrives, a farmer physically pens it and weighs it. The system forces this into three separate software tasks:
*   During "Register Animal", the form only asks for biological data.
*   The user must save, hunt for the new profile, navigate to a "Location" tab to assign a Pen.
*   The user must then navigate to a "Health" tab to log the initial arrival weight.
**Fix:** The "Add Animal" form must include optional sections for "Placement (Farm/Shed/Pen)" and "Initial Metrics (Weight)".

### High Impact: Weak "Active Farm" Context Feedback
Managing multiple locations requires switching the active farm context. While the switcher exists top-right, the visual feedback is dangerously subtle.
*   The main dashboard and page titles do not loudly proclaim the active farm.
*   Breadcrumbs often show technical IDs (`:branchId`) instead of the farm name.
**Fix:** Prominently display the active working context at the top of the main content area to prevent users from logging data into the wrong farm.

### High Impact: Ambiguous Measurement Units
Recording weight or feed presents numerical input fields without explicit units.
*   If a farmer types "50", it is unclear if the system records 50kg or 50lbs.
**Fix:** All numerical input fields for physical measurements must explicitly display the unit as a label or permanent suffix.

### Medium Impact: Blind Context Switching for Feeding
Feeding requires checking requirements, logging consumption, and ensuring inventory exists. Currently, checking inventory forces the user to leave the Livestock module entirely.
**Fix:** When logging feed consumption, the UI should display inline context: *"Requirement: 5kg | Current Inventory: 120kg"*.

### Medium Impact: Lack of Visual Benchmarks for "Normalcy"
Checking if an animal's growth is normal forces the user to look at a raw table of numbers and mentally calculate against breed standards.
**Fix:** Present a visual growth chart with a standard "benchmark curve" or a simple status badge ("On Track" / "Underweight").

---

## 3. Module-Specific Deep Dives

### 3.1 Organization & Farm Setup
*   **Redundant Navigation Loops:** Clicking "+ New Farm" from a Branch detail tab navigates to a global list where the user must click "+ Add Farm" again. **Fix:** Open creation modals directly from the parent tab.
*   **Technical Map Fields:** The "Map Polygon (GeoJSON)" field expects raw JSON code. **Fix:** Use an interactive map widget or a simple "Total Area" text field.

### 3.2 Health & Vaccination
*   **Ambiguous Terminology:** Dashboard buttons say "Schedule Vaccine" and "Log Treatment". It is unclear if "Schedule" can be used to log a past event, or what the strict difference is between an "Incident" and a "Treatment". **Fix:** Use clear past/future terminology (e.g., "Record Completed Task").
*   **Global Logging Risks:** Clicking "Log Treatment" from the global dashboard forces the user to search for an animal in a dropdown, increasing the risk of selecting the wrong tag. **Fix:** Default to contextual logging from the animal's specific profile.
*   **Hidden Milk Withdrawals:** Active milk/meat withdrawals are hidden behind a quick-link. **Fix:** Treat these as critical system-wide alerts with prominent red badges on the Animal List.

### 3.3 Feeding Module
*   **Rigid Workflows:** The system forces a strict hierarchy (Ingredient -> Formula -> Schedule -> Log). This is too heavy for simple ad-hoc feeding. **Fix:** Implement a "Quick Feed" button to bypass scheduling.
*   **Academic Terminology:** The system uses "Formula" and "Consumption Log". **Fix:** Use standard farming terms like "Ration/Mix" and "Feeding Records".
*   **Missing Variance Indicators:** The consumption log shows scheduled vs. actual as raw numbers. **Fix:** Add visual arrows or percentages to instantly highlight if animals are eating significantly more or less than scheduled.

### 3.4 Inventory Module
*   **Disconnect from Usage:** The Inventory module acts as a pure accountant's ledger (Stock In/Out). **Fix:** Inventory items should explicitly show "Estimated Days Remaining" based on active Feeding Schedules.
*   **Ledger Terminology:** Adjusting a torn bag of feed shouldn't require a "Stock Ledger Adjustment". **Fix:** Provide a prominent "Quick Adjust" or "Record Spoilage" button.
*   **Reactive Alerts:** Alerts trigger when stock hits zero. **Fix:** Alerts should be predictive (e.g., "Corn will run out in 3 days based on current burn rate").
