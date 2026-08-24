# Farm360 AI - User Manual

Welcome to **Farm360 AI**, the next-generation Enterprise ERP designed for modern agricultural operations and livestock management. This manual covers how to access the platform, configure your initial organization, and navigate the primary modules.

---

## 1. Getting Started

When you start the Farm360 backend and frontend for the very first time, the platform will automatically seed necessary master data and create a default System Administrator account.

### Initial Login Credentials
Use the following credentials to access the platform for the first time:

- **Phone Number:** `+8801806580501`
- **Password:** `Password123!`

*Note: Once you log in, it is highly recommended to change this password or set up your own personal account.*

---

## 2. Platform Navigation

Farm360 AI features a clean, minimal, and premium Enterprise UI. 

### The Application Shell
- **Sidebar (Left):** Your primary navigation hub. Use this to jump between major modules (Dashboard, Organizations, Master Data, Livestock, Health). The sidebar can be collapsed to maximize screen real estate.
- **Top Header:** Contains global search, quick notifications, user profile settings, and the **Tenant Switcher**.
- **Tenant Switcher:** If you manage multiple organizations or farms, you can switch between them using the dropdown located in the top header. Your permissions and visible data will instantly update to reflect the selected context.

---

## 3. Organizations Module

Before you can add farms, sheds, or livestock, you must configure your core Organization. 

1. Navigate to **Organizations** via the sidebar.
2. Click **Create Organization** to define your core business entity. You will need to provide standard enterprise details (Business Name, Tax ID, Currency, Timezone).
3. Once created, you can define **Farms** underneath this organization.
4. Inside each Farm, you can set up **Sheds** and **Pens** to represent your physical infrastructure.

---

## 4. Master Data Management

The **Master Data** module is where you configure global taxonomies and classifications used throughout the ERP.

- **Animal Breeds & Types:** Standardize the breeds you manage.
- **Health Types (Vaccines & Diseases):** Create master lists of recurring health incidents and vaccinations to ensure data consistency across your farms.
- **Geographic Locations:** Configure Countries, Divisions, Districts, and Upazilas for accurate address reporting.

*Tip: Many of these records are pre-seeded by the application on first run, but you can always add or modify them as your operational needs evolve.*

---

## 5. Livestock Module

The core of Farm360 AI. Here, you track the entire lifecycle of your animals.

- **Dashboard:** Get a bird's eye view of total animals, mortality rates, and average weights.
- **Animals List:** A high-performance data table allowing you to filter, sort, and search your entire herd. You can track RFID tags, birth dates, breed, and current weight.
- **Transfers:** Move animals between different Sheds and Pens. The system maintains an immutable audit log of all movements.

---

## 6. Health & Medical Module

Manage veterinary interventions and health monitoring.

- **Vaccinations:** Schedule and record batch vaccinations. The system tracks dosage, administering veterinarian, and next due dates.
- **Medical Treatments:** Log individual or batch treatments for sick animals, including the medication used, cost, and outcome (Recovered, Ongoing, Fatal).
- **Disease Incidents:** Track outbreaks and link them to specific pens or sheds for biosecurity monitoring.

---

## 7. AI Intelligence & Profit Projections

The **Cattle Profit & Loss Projection** module allows you to simulate the financial outcome of fattening an animal before committing to operational decisions. 

- **Run Simulations**: Open an animal's details page and navigate to the **Profit Projections** tab.
- **Dynamic Levers**: Adjust target days, target daily weight gain, and feed costs.
- **Break-Even Analysis**: The system automatically calculates the exact day your total investment breaks even against the projected meat value, providing actionable insight for the optimal day to sell.

---

## 8. Security and Authentication

Farm360 utilizes enterprise-grade security protocols:

- **JWT + Secure Cookies:** The application uses Short-Lived Access Tokens and HttpOnly Refresh Tokens. 
- **Session Expiry:** If you are inactive for an extended period, your session will gracefully expire. You will be redirected back to the login screen without losing any unsaved work in your current tab.
- **Role-Based Access Control (RBAC):** Every action in the system is verified against your assigned Role (e.g., Owner, FarmManager, Veterinarian, Worker) before execution.

---

## Need Support?

If you encounter an error (such as a `401 Unauthorized` or `500 Server Error`), please contact your system administrator with the **Correlation ID** usually provided at the bottom of the error notification, which helps engineering trace the exact issue in the server logs.
