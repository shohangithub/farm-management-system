# Farm360 AI — Product Requirements Document (PRD)

**Document ID:** F360-PRD-2026-001  
**Version:** 1.0 — MVP  
**Status:** Draft for Engineering & Product Review  
**Prepared by:** Product Management & Solution Architecture Office  
**Date:** July 2026  
**Parent Document:** Farm360 AI Product Vision Document (PVD) v1.0  
**Classification:** Confidential — Internal Use Only  
**Review Cycle:** Quarterly  

---

> *This document is the authoritative source of truth for all product requirements, user stories, acceptance criteria, and technical constraints governing the Farm360 AI MVP release. All engineering, QA, design, and business stakeholders are required to align with this document.*

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Business Requirements](#2-business-requirements)
3. [Functional Requirements](#3-functional-requirements)
4. [Non-Functional Requirements](#4-non-functional-requirements)
5. [Complete Feature List](#5-complete-feature-list)
6. [Module Breakdown](#6-module-breakdown)
7. [Business Rules](#7-business-rules)
8. [User Stories](#8-user-stories)
9. [Acceptance Criteria](#9-acceptance-criteria)
10. [Use Cases](#10-use-cases)
11. [Edge Cases](#11-edge-cases)
12. [Error Handling](#12-error-handling)
13. [Notifications](#13-notifications)
14. [Audit Requirements](#14-audit-requirements)
15. [Localization](#15-localization)
16. [Performance Requirements](#16-performance-requirements)
17. [Security Requirements](#17-security-requirements)
18. [Scalability Requirements](#18-scalability-requirements)
19. [Availability Requirements](#19-availability-requirements)
20. [Backup Strategy](#20-backup-strategy)
21. [Disaster Recovery Strategy](#21-disaster-recovery-strategy)
22. [Future Expansion Plan](#22-future-expansion-plan)
23. [Appendix](#23-appendix)

---

## 1. Executive Summary

### 1.1 Document Purpose

This Product Requirements Document (PRD) defines the complete set of functional, non-functional, and operational requirements for **Farm360 AI** — an enterprise-grade, multi-tenant SaaS platform for livestock farm management in Bangladesh. It serves as the primary contract between the Product team, Engineering team, QA team, and business stakeholders for what must be built, how it must behave, and how it must perform.

### 1.2 Product Overview

**Farm360 AI** is a cloud-hosted, multi-tenant Software-as-a-Service platform that provides integrated livestock management, smart feeding intelligence, veterinary health tracking, inventory control, and financial management capabilities to farms of all sizes — from small single-shed operations to multi-location corporate agribusinesses.

The MVP delivers **7 core modules** accessible via a bilingual (Bangla/English) responsive web application optimized for low-end Android devices on 3G/4G networks in Bangladesh.

### 1.3 Scope of This Document

| In Scope | Out of Scope |
|---|---|
| All 7 MVP modules | Native iOS application |
| Web application (PWA) | IoT hardware integration |
| Multi-tenant architecture | AI/ML model training pipeline |
| Billing & subscription management | Marketplace features |
| Notification system (email, SMS) | Poultry / Fish Farm modules |
| Role-based access control | Advanced AI features |
| API layer for internal consumption | Third-party ERP integration |
| Admin portal | Data analytics platform |

### 1.4 Intended Audience

| Audience | Usage |
|---|---|
| Engineering Team | Primary implementation reference |
| QA & Testing Team | Acceptance criteria and test case derivation |
| UI/UX Designers | Interaction and flow requirements |
| Product Manager | Feature scope and business rule validation |
| DevOps / Infrastructure | NFR and infrastructure requirement planning |
| Business Stakeholders | Business requirement and success metric tracking |
| Security Team | Security and compliance requirement review |

### 1.5 Definitions and Acronyms

| Term | Definition |
|---|---|
| **ADG** | Average Daily Gain — average weight gained by an animal per day (kg/day) |
| **BCS** | Body Condition Score — standardized 1–5 scale assessment of animal body condition |
| **BDT** | Bangladeshi Taka — primary currency |
| **DIM** | Days in Milk — days since last calving for dairy animals |
| **DLS** | Department of Livestock Services — Bangladesh government regulatory body |
| **FCR** | Feed Conversion Ratio — kg of feed consumed per kg of weight gained |
| **FTUE** | First Time User Experience — the onboarding flow for new users |
| **MoU** | Memorandum of Understanding |
| **MRR** | Monthly Recurring Revenue |
| **NPS** | Net Promoter Score |
| **PWA** | Progressive Web Application — web app with offline capabilities |
| **RBAC** | Role-Based Access Control |
| **RPO** | Recovery Point Objective — maximum acceptable data loss window |
| **RTO** | Recovery Time Objective — maximum acceptable system downtime |
| **SLA** | Service Level Agreement |
| **Tenant** | A single registered organization (farm business) in the multi-tenant system |

---

## 2. Business Requirements

### 2.1 Business Context

Bangladesh's livestock sector accounts for approximately 1.7% of national GDP and supports over 20 million rural households. The sector suffers from systemic operational inefficiency driven by the absence of digital management tools. Farm360 AI directly addresses this market gap.

### 2.2 Business Requirements Registry

| ID | Business Requirement | Priority | Source |
|---|---|---|---|
| BR-01 | The platform shall support a self-serve, subscription-based revenue model with no dependency on sales-assisted purchasing for tiers up to Banik | Must Have | PVD §17 |
| BR-02 | The platform shall provide measurable, demonstrable ROI to farmers within the first 30 days of use | Must Have | PVD §4 |
| BR-03 | The platform shall support all five subscription tiers including a perpetual free tier | Must Have | PVD §17 |
| BR-04 | The platform shall be operable in both Bangla and English without any functional limitations in either language | Must Have | PVD §2 |
| BR-05 | The platform shall enable multi-location farm management from a single organizational account | Must Have | PVD §9 |
| BR-06 | The platform shall generate financial reports suitable for submission to Bangladeshi financial institutions | Must Have | PVD §9 |
| BR-07 | The platform shall capture sufficient structured data to support future AI model training | Must Have | PVD §3 |
| BR-08 | The platform shall be accessible on low-cost Android devices (≥ Android 8.0) on 3G networks | Must Have | PVD §10 |
| BR-09 | The platform shall support payment via bKash, Nagad, and Rocket in addition to card-based payments | Must Have | PVD §17 |
| BR-10 | The platform shall maintain full data privacy and not share tenant data with other tenants under any circumstances | Must Have | PVD §10 |
| BR-11 | The platform shall support NGO and cooperative bulk account management for subsidized deployment | Should Have | PVD §17 |
| BR-12 | The platform shall generate DLS-aligned compliance reports | Should Have | PVD §12 |

### 2.3 Stakeholder Requirements

| Stakeholder | Primary Requirement |
|---|---|
| **Small/Medium Farmer** | Bangla-first UI, simple daily data entry, critical alerts on mobile |
| **Corporate Farm Manager** | Real-time multi-location dashboard, consolidated reporting, RBAC |
| **Finance Team** | Accurate per-animal P&L, exportable reports, multi-currency future-readiness |
| **Veterinary Staff** | Complete health history per animal, protocol scheduling, drug log |
| **Platform Administrator** | Tenant management, subscription management, usage monitoring |
| **Business Investor** | MRR growth, retention metrics, data asset growth |
| **NGO/Cooperative** | Bulk account management, donor reporting templates |

---

## 3. Functional Requirements

### 3.1 Functional Requirements — Platform Core

| ID | Requirement | Priority | Module |
|---|---|---|---|
| FR-P-01 | The system shall allow new organizations to self-register with name, phone number, email, and farm type | Must Have | Platform |
| FR-P-02 | The system shall send an OTP verification to the registered phone number (SMS) and/or email during registration | Must Have | Platform |
| FR-P-03 | The system shall provide a guided setup wizard for new tenants covering farm profile, shed setup, and first animal entry | Must Have | Platform |
| FR-P-04 | The system shall enforce tenant data isolation such that no user can access data belonging to another tenant | Must Have | Platform |
| FR-P-05 | The system shall support the following roles: Owner, Farm Manager, Veterinarian, Worker, Accountant, Viewer | Must Have | Platform |
| FR-P-06 | The system shall enforce role permissions such that each role has a defined, non-overlapping set of capabilities | Must Have | Platform |
| FR-P-07 | The system shall allow the Owner to invite users via phone number or email and assign roles | Must Have | Platform |
| FR-P-08 | The system shall support organizational hierarchy: Organization → Farm → Shed → Pen | Must Have | Platform |
| FR-P-09 | The system shall allow a single organization to manage up to N farms as defined by their subscription tier | Must Have | Platform |
| FR-P-10 | The system shall display the correct language (Bangla or English) based on user preference stored in profile | Must Have | Platform |
| FR-P-11 | The system shall support offline data entry for core operations and sync automatically on reconnection | Should Have | Platform |
| FR-P-12 | The system shall track all data modifications with user identity, timestamp, before-value, and after-value | Must Have | Platform |
| FR-P-13 | The system shall allow subscription plan selection, upgrade, and downgrade through the settings panel | Must Have | Platform |
| FR-P-14 | The system shall display a grace period notice 7 days before subscription expiry and lock non-read access after expiry | Must Have | Platform |
| FR-P-15 | The system shall prevent downgrade to a tier that would exceed the new tier's animal/user limits | Must Have | Platform |

### 3.2 Functional Requirements — Livestock Management

| ID | Requirement | Priority |
|---|---|---|
| FR-LM-01 | The system shall allow registration of individual animals with: tag/ID, species, breed, sex, date of birth, acquisition date, acquisition price, and source | Must Have |
| FR-LM-02 | The system shall support the following species in MVP: Cattle (Beef), Cattle (Dairy), Goat | Must Have |
| FR-LM-03 | The system shall support locally relevant breeds including: Shahibal, Brahman Cross, Red Chittagong, Holstein-Friesian, Jersey, Black Bengal | Must Have |
| FR-LM-04 | The system shall support animal identification via manual ID, ear tag number, and RFID tag number fields | Must Have |
| FR-LM-05 | The system shall allow animals to be assigned to a specific Shed and Pen within a Farm | Must Have |
| FR-LM-06 | The system shall support batch/group creation allowing multiple animals to be grouped and managed together | Must Have |
| FR-LM-07 | The system shall allow weight entries to be recorded per animal with date and recorder identity | Must Have |
| FR-LM-08 | The system shall automatically calculate Average Daily Gain (ADG) from sequential weight entries | Must Have |
| FR-LM-09 | The system shall support recording of mating events with sire ID, date, and method (natural/AI) | Must Have |
| FR-LM-10 | The system shall support pregnancy confirmation recording and auto-calculate expected calving date | Must Have |
| FR-LM-11 | The system shall support recording of births (calves) with linkage to dam and sire | Must Have |
| FR-LM-12 | The system shall support animal status transitions: Active → Sold, Slaughtered, Dead, Transferred | Must Have |
| FR-LM-13 | The system shall record sale transactions with buyer name, sale date, sale weight, and sale price | Must Have |
| FR-LM-14 | The system shall present a complete chronological animal timeline for every animal on the platform | Must Have |
| FR-LM-15 | The system shall support searching and filtering animals by: tag, name, breed, species, status, shed, batch, sex, age range, weight range | Must Have |
| FR-LM-16 | The system shall support Body Condition Scoring (1.0–5.0) entry per animal | Should Have |
| FR-LM-17 | The system shall allow photo upload and storage (max 5 photos per animal) | Should Have |
| FR-LM-18 | The system shall allow inter-farm animal transfer within the same organization | Must Have |

### 3.3 Functional Requirements — Smart Feeding

| ID | Requirement | Priority |
|---|---|---|
| FR-SF-01 | The system shall maintain a feed ingredient catalog with: name (Bangla & English), unit of measure, dry matter %, crude protein %, metabolizable energy (MJ/kg DM), and cost per unit | Must Have |
| FR-SF-02 | The system shall pre-populate the ingredient catalog with common Bangladeshi feed ingredients (rice straw, mustard oil cake, khesari, green grass, DCP, urea, wheat bran, rice bran) | Must Have |
| FR-SF-03 | The system shall allow tenants to add custom feed ingredients to their catalog | Must Have |
| FR-SF-04 | The system shall allow creation of feed formulas by combining multiple ingredients with quantities | Must Have |
| FR-SF-05 | The system shall automatically calculate the nutritional profile (CP %, ME, DM %) of a formula based on ingredient data | Must Have |
| FR-SF-06 | The system shall allow assignment of a feed formula to a shed, pen, or batch with a start date | Must Have |
| FR-SF-07 | The system shall allow daily feed consumption to be logged per shed with quantity and date | Must Have |
| FR-SF-08 | The system shall calculate daily feed cost per shed and per animal based on recorded consumption and ingredient prices | Must Have |
| FR-SF-09 | The system shall automatically deduct feed consumption from inventory stock when logged | Must Have |
| FR-SF-10 | The system shall calculate FCR per batch using total feed consumed and total weight gain over the batch period | Must Have |
| FR-SF-11 | The system shall display FCR trend charts for completed and active batches | Must Have |
| FR-SF-12 | The system shall generate feeding reports: daily, weekly, monthly, per batch | Must Have |
| FR-SF-13 | The system shall alert when a shed's feed consumption deviates more than 20% from the assigned formula quantity | Should Have |

### 3.4 Functional Requirements — Health & Vaccination

| ID | Requirement | Priority |
|---|---|---|
| FR-HV-01 | The system shall allow creation of vaccination protocol templates with: vaccine name, target species, recommended age/interval, dose, and route of administration | Must Have |
| FR-HV-02 | The system shall allow protocols to be assigned to individual animals, batches, sheds, or entire farms | Must Have |
| FR-HV-03 | The system shall auto-schedule vaccination due dates for all animals under an assigned protocol | Must Have |
| FR-HV-04 | The system shall send alerts (in-app + push + SMS) when a vaccination is due within 7 days and when it is overdue | Must Have |
| FR-HV-05 | The system shall allow vaccination events to be recorded: vaccine name, batch number, dose, date given, administered by, and next due date | Must Have |
| FR-HV-06 | The system shall allow treatment events to be recorded: diagnosis, drug/medicine, dose, frequency, duration, administered by, and cost | Must Have |
| FR-HV-07 | The system shall automatically deduct medicines used from inventory when a treatment event is recorded | Must Have |
| FR-HV-08 | The system shall support recording of disease incidents: affected animals, symptoms, diagnosis, quarantine status, and resolution | Must Have |
| FR-HV-09 | The system shall support vet visit scheduling with date, vet name, visit type, and notes | Must Have |
| FR-HV-10 | The system shall record animal deaths with: date, cause (disease, accident, natural, unknown), estimated economic loss, and post-mortem notes | Must Have |
| FR-HV-11 | The system shall generate a per-animal health history report including all vaccinations, treatments, and incidents | Must Have |
| FR-HV-12 | The system shall display a herd health status dashboard showing: due vaccinations, animals under treatment, quarantined animals | Must Have |
| FR-HV-13 | The system shall support a deworming calendar with configurable intervals by species and season | Must Have |
| FR-HV-14 | The system shall support milk withdrawal period tracking for dairy animals under antibiotic treatment | Should Have |

### 3.5 Functional Requirements — Inventory Management

| ID | Requirement | Priority |
|---|---|---|
| FR-IV-01 | The system shall support an inventory item catalog with: item name, category (Feed, Medicine, Chemical, Equipment, Other), unit of measure, and reorder threshold | Must Have |
| FR-IV-02 | The system shall support stock-in transactions with: supplier name, quantity, unit cost, expiry date, batch/lot number, and date received | Must Have |
| FR-IV-03 | The system shall support manual stock-out entries in addition to automatic deductions from feed and health modules | Must Have |
| FR-IV-04 | The system shall maintain a real-time current stock level for every inventory item | Must Have |
| FR-IV-05 | The system shall trigger low-stock alerts when an item's quantity falls below its configured reorder threshold | Must Have |
| FR-IV-06 | The system shall track expiry dates and alert on items expiring within 30 days (configurable) | Must Have |
| FR-IV-07 | The system shall support supplier management with: name, phone, address, and purchase history | Should Have |
| FR-IV-08 | The system shall display total inventory valuation at cost using weighted average cost method | Must Have |
| FR-IV-09 | The system shall support recording of stock write-offs with reason (damaged, expired, lost) | Should Have |
| FR-IV-10 | The system shall provide an inventory movement ledger (stock-in / stock-out / adjustments) with date and actor | Must Have |
| FR-IV-11 | The system shall generate an inventory report showing opening stock, received, consumed, and closing stock for any date range | Must Have |

### 3.6 Functional Requirements — Finance Management

| ID | Requirement | Priority |
|---|---|---|
| FR-FM-01 | The system shall maintain a pre-configured chart of accounts for farm operations: Animal Purchase, Feed Cost, Veterinary Cost, Labor Cost, Utilities, Transport, Miscellaneous Expense; Animal Sale, Milk Sale, Byproduct Sale | Must Have |
| FR-FM-02 | The system shall allow manual income recording with: category, amount (BDT), date, description, farm, and optional animal/batch link | Must Have |
| FR-FM-03 | The system shall allow manual expense recording with: category, amount (BDT), date, description, farm, and optional animal/batch/shed link | Must Have |
| FR-FM-04 | The system shall automatically post income entries when animal sale events are recorded in the Livestock module | Must Have |
| FR-FM-05 | The system shall automatically post expense entries when feed consumption, medicine use, and inventory purchases are recorded | Must Have |
| FR-FM-06 | The system shall maintain a running cost ledger per individual animal from acquisition to disposal | Must Have |
| FR-FM-07 | The system shall calculate and display Profit & Loss per batch: total income, total cost, gross profit, and ROI % | Must Have |
| FR-FM-08 | The system shall generate a monthly P&L report by farm, by category, and consolidated across all farms | Must Have |
| FR-FM-09 | The system shall provide a break-even sale price calculator per animal based on accumulated costs | Must Have |
| FR-FM-10 | The system shall support exporting all financial reports to PDF and Excel (XLSX) format | Must Have |
| FR-FM-11 | The system shall display a financial dashboard with: revenue MTD, expenses MTD, net profit MTD, and comparison to prior month | Must Have |
| FR-FM-12 | The system shall support loan/investment recording with: lender name, amount, interest rate, disbursement date, and repayment schedule | Should Have |
| FR-FM-13 | The system shall track loan repayments and display outstanding balance | Should Have |
| FR-FM-14 | The system shall support multi-farm consolidated P&L for organizational-level reporting | Must Have |

### 3.7 Functional Requirements — Dashboard & Analytics

| ID | Requirement | Priority |
|---|---|---|
| FR-DA-01 | The system shall display an executive dashboard with: total animals by status, today's health alerts, current month financials, and low stock alerts | Must Have |
| FR-DA-02 | The system shall display a per-farm summary card with drilldown capability | Must Have |
| FR-DA-03 | The system shall display herd composition charts by species, breed, sex, age group, and status | Must Have |
| FR-DA-04 | The system shall display a vaccination compliance chart showing due, overdue, and completed vaccinations | Must Have |
| FR-DA-05 | The system shall display ADG trend charts by batch over configurable time periods | Must Have |
| FR-DA-06 | The system shall display feed cost per animal per day trend chart | Must Have |
| FR-DA-07 | The system shall display an inventory status panel with items below threshold and items near expiry | Must Have |
| FR-DA-08 | The system shall display a recent activity feed showing the last 20 platform actions by farm staff | Must Have |
| FR-DA-09 | The system shall support custom date range filtering across all dashboard widgets | Should Have |
| FR-DA-10 | The system shall allow dashboard reports to be exported as PDF or PNG | Should Have |
| FR-DA-11 | The system shall display alerts as prioritized notification badges (critical, warning, info) | Must Have |

---

## 4. Non-Functional Requirements

> Non-functional requirements are binding quality constraints. A feature that violates an NFR is considered incomplete regardless of functional correctness.

### 4.1 Summary Table

| Category | Reference |
|---|---|
| Performance | §16 |
| Security | §17 |
| Scalability | §18 |
| Availability | §19 |
| Localization | §15 |
| Usability | §4.2 |
| Maintainability | §4.3 |
| Compliance | §4.4 |

### 4.2 Usability Requirements

| ID | Requirement |
|---|---|
| NFR-UX-01 | A first-time user with basic smartphone literacy must be able to register, complete onboarding, and enter a first animal record in ≤ 30 minutes without external assistance |
| NFR-UX-02 | All primary data entry workflows must be completable in ≤ 5 taps/clicks from the relevant module home screen |
| NFR-UX-03 | The platform must be fully functional on screen widths ≥ 320px |
| NFR-UX-04 | All interactive elements must have minimum touch target size of 44×44px (iOS HIG / WCAG 2.5.5) |
| NFR-UX-05 | The platform must achieve WCAG 2.1 Level AA color contrast ratios for all text elements |
| NFR-UX-06 | Error messages must be written in plain language in the active UI language (Bangla or English), not technical codes |
| NFR-UX-07 | Forms must auto-save in progress entries to prevent data loss on connection drop |
| NFR-UX-08 | The platform must support system dark mode and light mode |

### 4.3 Maintainability Requirements

| ID | Requirement |
|---|---|
| NFR-MN-01 | The codebase must maintain ≥ 80% unit test coverage on business logic layers |
| NFR-MN-02 | All API endpoints must be versioned (e.g., /api/v1/) from initial release |
| NFR-MN-03 | Database schema changes must be applied via versioned migration scripts with rollback capability |
| NFR-MN-04 | Application configuration must be externalized — no hardcoded environment values in code |
| NFR-MN-05 | The platform must emit structured, queryable logs in JSON format to a centralized logging system |
| NFR-MN-06 | Health check endpoints must be available for each service for infrastructure monitoring |
| NFR-MN-07 | Deployment must be fully automated via CI/CD pipelines with no manual production steps |

### 4.4 Compliance Requirements

| ID | Requirement |
|---|---|
| NFR-CP-01 | The platform must comply with Bangladesh ICT Act 2006 Section 57 and relevant data provisions |
| NFR-CP-02 | All financial records must be immutable once the accounting period is closed (no backdated deletion) |
| NFR-CP-03 | The platform must be capable of generating reports aligned with DLS inspection requirements |
| NFR-CP-04 | The platform architecture must be GDPR-compatible to support future regional expansion |
| NFR-CP-05 | Sensitive PII (phone numbers, NID) must be masked in application logs |
| NFR-CP-06 | Payment processing must comply with PCI-DSS Level 1 via certified payment gateway |

---

## 5. Complete Feature List

### MVP Feature Registry

| Feature ID | Feature Name | Module | Priority | Phase |
|---|---|---|---|---|
| F-001 | Tenant Self-Registration | Platform | Must Have | MVP |
| F-002 | OTP Phone/Email Verification | Platform | Must Have | MVP |
| F-003 | Guided Onboarding Wizard | Platform | Must Have | MVP |
| F-004 | Organization Profile Management | Platform | Must Have | MVP |
| F-005 | Farm Profile Management | Platform | Must Have | MVP |
| F-006 | Shed & Pen Setup | Platform | Must Have | MVP |
| F-007 | User Invitation & Role Assignment | Platform | Must Have | MVP |
| F-008 | Role-Based Access Control (6 roles) | Platform | Must Have | MVP |
| F-009 | Language Toggle (Bangla / English) | Platform | Must Have | MVP |
| F-010 | Subscription Plan Management | Platform | Must Have | MVP |
| F-011 | bKash / Nagad / Card Payment Integration | Platform | Must Have | MVP |
| F-012 | Audit Log Viewer | Platform | Must Have | MVP |
| F-013 | Offline Mode (PWA) | Platform | Should Have | MVP |
| F-014 | Animal Registration | Livestock | Must Have | MVP |
| F-015 | Animal Profile & Timeline | Livestock | Must Have | MVP |
| F-016 | Batch / Group Management | Livestock | Must Have | MVP |
| F-017 | Weight Entry & ADG Calculator | Livestock | Must Have | MVP |
| F-018 | Breeding & Pregnancy Tracking | Livestock | Must Have | MVP |
| F-019 | Birth Recording | Livestock | Must Have | MVP |
| F-020 | Animal Transfer (Shed / Farm) | Livestock | Must Have | MVP |
| F-021 | Animal Disposal (Sale/Death/Slaughter) | Livestock | Must Have | MVP |
| F-022 | Animal Search & Advanced Filter | Livestock | Must Have | MVP |
| F-023 | Body Condition Scoring | Livestock | Should Have | MVP |
| F-024 | Animal Photo Management | Livestock | Should Have | MVP |
| F-025 | Feed Ingredient Catalog (with pre-loaded BD ingredients) | Feeding | Must Have | MVP |
| F-026 | Custom Ingredient Addition | Feeding | Must Have | MVP |
| F-027 | Feed Formula Builder | Feeding | Must Have | MVP |
| F-028 | Nutritional Profile Calculator | Feeding | Must Have | MVP |
| F-029 | Feeding Schedule Assignment | Feeding | Must Have | MVP |
| F-030 | Daily Feed Consumption Logger | Feeding | Must Have | MVP |
| F-031 | Feed Cost Calculator | Feeding | Must Have | MVP |
| F-032 | FCR Calculator & Trend Chart | Feeding | Must Have | MVP |
| F-033 | Feed Consumption Reports | Feeding | Must Have | MVP |
| F-034 | Feed Deviation Alert | Feeding | Should Have | MVP |
| F-035 | Vaccination Protocol Builder | Health | Must Have | MVP |
| F-036 | Protocol Assignment (Animal/Batch/Shed) | Health | Must Have | MVP |
| F-037 | Auto Vaccination Schedule Generator | Health | Must Have | MVP |
| F-038 | Vaccination Due Alerts | Health | Must Have | MVP |
| F-039 | Vaccination Event Recording | Health | Must Have | MVP |
| F-040 | Treatment / Medication Logger | Health | Must Have | MVP |
| F-041 | Disease Incident Manager | Health | Must Have | MVP |
| F-042 | Vet Visit Scheduler | Health | Must Have | MVP |
| F-043 | Mortality Recorder | Health | Must Have | MVP |
| F-044 | Deworming Calendar | Health | Must Have | MVP |
| F-045 | Health Status Dashboard | Health | Must Have | MVP |
| F-046 | Animal Health Report | Health | Must Have | MVP |
| F-047 | Milk Withdrawal Tracker | Health | Should Have | MVP |
| F-048 | Inventory Item Catalog | Inventory | Must Have | MVP |
| F-049 | Stock-In Recording | Inventory | Must Have | MVP |
| F-050 | Stock-Out / Consumption Recording | Inventory | Must Have | MVP |
| F-051 | Real-Time Stock Dashboard | Inventory | Must Have | MVP |
| F-052 | Low Stock Alert | Inventory | Must Have | MVP |
| F-053 | Expiry Date Tracker & Alert | Inventory | Must Have | MVP |
| F-054 | Supplier Management | Inventory | Should Have | MVP |
| F-055 | Inventory Valuation Report | Inventory | Must Have | MVP |
| F-056 | Stock Write-Off Recording | Inventory | Should Have | MVP |
| F-057 | Inventory Movement Ledger | Inventory | Must Have | MVP |
| F-058 | Chart of Accounts (Pre-configured) | Finance | Must Have | MVP |
| F-059 | Income Recording | Finance | Must Have | MVP |
| F-060 | Expense Recording | Finance | Must Have | MVP |
| F-061 | Auto-Posting from Other Modules | Finance | Must Have | MVP |
| F-062 | Per-Animal Cost Ledger | Finance | Must Have | MVP |
| F-063 | Batch P&L Report | Finance | Must Have | MVP |
| F-064 | Monthly P&L Report | Finance | Must Have | MVP |
| F-065 | Multi-Farm Consolidated P&L | Finance | Must Have | MVP |
| F-066 | Break-Even Calculator | Finance | Must Have | MVP |
| F-067 | ROI Calculator | Finance | Must Have | MVP |
| F-068 | Financial Dashboard | Finance | Must Have | MVP |
| F-069 | PDF / Excel Export | Finance | Must Have | MVP |
| F-070 | Loan / Investment Tracker | Finance | Should Have | MVP |
| F-071 | Executive Dashboard | Dashboard | Must Have | MVP |
| F-072 | Per-Farm Summary Card | Dashboard | Must Have | MVP |
| F-073 | Herd Composition Charts | Dashboard | Must Have | MVP |
| F-074 | ADG Trend Charts | Dashboard | Must Have | MVP |
| F-075 | Feed Cost Trend Charts | Dashboard | Must Have | MVP |
| F-076 | Financial Snapshot Widget | Dashboard | Must Have | MVP |
| F-077 | Inventory Alerts Panel | Dashboard | Must Have | MVP |
| F-078 | Activity Feed | Dashboard | Must Have | MVP |
| F-079 | Custom Date Range Filter | Dashboard | Should Have | MVP |
| F-080 | Report Export (PDF/PNG) | Dashboard | Should Have | MVP |

**Total MVP Features: 80**  
**Must Have: 65 | Should Have: 15**

---

## 6. Module Breakdown

### 6.1 Module Dependency Map

```
┌─────────────────────────────────────────────┐
│            PLATFORM CORE                    │
│  Auth · Tenancy · RBAC · Billing · Audit    │
└────────────┬────────────────────────────────┘
             │ (foundation for all modules)
    ┌────────┼────────┬─────────────────┐
    ▼        ▼        ▼                 ▼
┌────────┐ ┌──────┐ ┌──────────────┐ ┌──────────┐
│Livestock│ │Health│ │  Inventory   │ │ Finance  │
│ Module  │ │Module│ │   Module     │ │  Module  │
└────────┘ └──────┘ └──────────────┘ └──────────┘
    │          │           │               │
    └──────────┴───────────┴───────────────┤
                                           ▼
                                     ┌──────────┐
                                     │ Feeding  │
                                     │  Module  │
                                     └──────────┘
                                           │
    ┌──────────────────────────────────────┘
    ▼
┌──────────────────────────────────────────────┐
│         DASHBOARD & ANALYTICS                │
│  Aggregates data from all modules above      │
└──────────────────────────────────────────────┘
```

### 6.2 Module Data Flow

| Source Module | Trigger Event | Target Module | Action |
|---|---|---|---|
| Livestock | Animal Sale recorded | Finance | Auto-post income entry (Animal Sale category) |
| Livestock | Animal Purchase recorded | Finance | Auto-post expense entry (Animal Purchase category) |
| Feeding | Feed consumption logged | Inventory | Deduct feed ingredient quantities from stock |
| Feeding | Feed consumption logged | Finance | Auto-post expense entry (Feed Cost category) |
| Health | Treatment medication recorded | Inventory | Deduct medicine quantity from stock |
| Health | Treatment cost entered | Finance | Auto-post expense entry (Veterinary Cost category) |
| Inventory | Stock-in purchase recorded | Finance | Auto-post expense entry (Inventory Purchase category) |

### 6.3 Module Access by Role

| Module | Owner | Farm Mgr | Vet | Worker | Accountant | Viewer |
|---|---|---|---|---|---|---|
| Platform Admin | Full | None | None | None | None | None |
| Livestock | Full | Full | Read | Create/Edit | Read | Read |
| Smart Feeding | Full | Full | Read | Create | Read | Read |
| Health & Vaccination | Full | Full | Full | Read/Create | Read | Read |
| Inventory | Full | Full | Read | Create | Full | Read |
| Finance | Full | Read | None | None | Full | Read |
| Dashboard | Full | Full | Partial | Partial | Full | Read |

---

## 7. Business Rules

### 7.1 Platform Business Rules

| ID | Rule | Enforcement |
|---|---|---|
| BRU-P-01 | An Organization must have exactly one Owner at all times | System-enforced, cannot remove last Owner |
| BRU-P-02 | A user account (phone/email) can belong to multiple organizations with different roles | System-enforced |
| BRU-P-03 | Subscription downgrade takes effect at the end of the current billing cycle, not immediately | Billing engine |
| BRU-P-04 | If the active animal count exceeds the tier limit upon downgrade, the account enters read-only mode until animals are archived | System-enforced |
| BRU-P-05 | A tenant in expired subscription state retains read access to all data for 30 days before the account is suspended | System-enforced |
| BRU-P-06 | Suspended accounts retain data for 90 days before permanent deletion, with 7-day prior warning | System-enforced |
| BRU-P-07 | Free tier (Bittho) is limited to a maximum of 10 active animals and 1 user at all times | System-enforced |

### 7.2 Livestock Business Rules

| ID | Rule | Enforcement |
|---|---|---|
| BRU-LM-01 | Every animal must have a unique Tag/ID within the organization scope | System validation |
| BRU-LM-02 | An animal's Date of Birth cannot be in the future | Form validation |
| BRU-LM-03 | An animal cannot be assigned to a Pen belonging to a different Shed | Relationship validation |
| BRU-LM-04 | An animal's status can only follow valid transitions: Active → Sold, Dead, Slaughtered, Transferred; Transferred → Active (on receiving farm) | State machine |
| BRU-LM-05 | A sold or dead animal is archived and read-only; no further operational records can be added | System-enforced |
| BRU-LM-06 | A female animal must be recorded before it can be set as a dam in a birth record | Referential integrity |
| BRU-LM-07 | Weight entries must not be older than the animal's date of birth | Date validation |
| BRU-LM-08 | ADG is calculated only when ≥ 2 weight entries exist with different dates | Calculation guard |
| BRU-LM-09 | Pregnancy confirmation date must be ≥ mating date | Date validation |
| BRU-LM-10 | Expected calving date is auto-calculated as mating date + gestation period by species (Cattle: 283 days, Goat: 150 days) | Automated calculation |

### 7.3 Smart Feeding Business Rules

| ID | Rule | Enforcement |
|---|---|---|
| BRU-SF-01 | A feed formula must contain at least 2 ingredients | Validation |
| BRU-SF-02 | Feed consumption cannot be logged for a future date | Date validation |
| BRU-SF-03 | Feed consumption deducted from inventory cannot exceed the current available stock; if insufficient, a warning is shown and the user must acknowledge before proceeding | Stock guard with warning |
| BRU-SF-04 | FCR is calculated only when a batch has ≥ 1 weight entry after the first feed consumption entry | Calculation guard |
| BRU-SF-05 | A feeding schedule change takes effect from the next day; historical records are not modified | Immutability rule |

### 7.4 Health Business Rules

| ID | Rule | Enforcement |
|---|---|---|
| BRU-HV-01 | A vaccination cannot be marked as administered for a future date | Date validation |
| BRU-HV-02 | A medicine cannot be administered if the current inventory stock of that medicine is zero (warning shown; override allowed for Vet/Owner roles only) | Stock guard with role-gated override |
| BRU-HV-03 | An animal cannot have two overlapping treatment records for the same drug (prevents accidental duplicate logging) | Duplicate check with user confirmation override |
| BRU-HV-04 | A deceased animal cannot receive new health records after the death date | Status check |
| BRU-HV-05 | Milk production of a dairy animal must be suspended when a milk withdrawal record is active | Status enforcement |
| BRU-HV-06 | A quarantined animal must be released from quarantine before it can be sold or transferred | Status gate |

### 7.5 Finance Business Rules

| ID | Rule | Enforcement |
|---|---|---|
| BRU-FM-01 | All financial entries must be in BDT; other currencies are not supported in MVP | UI restriction |
| BRU-FM-02 | Financial entries for a closed period (prior month after month-end) cannot be deleted, only corrected via reversal entry | Immutability with reversal |
| BRU-FM-03 | The per-animal cost ledger is auto-calculated; manual overrides are not permitted | System-managed |
| BRU-FM-04 | Batch P&L is finalized when the last animal in the batch is disposed (sold/dead/slaughtered); prior to that it shows as "In Progress" | Status-based calculation |
| BRU-FM-05 | A financial export (PDF/Excel) timestamp and user identity are logged in the audit trail | Audit rule |
| BRU-FM-06 | Income from animal sale is posted at the recorded sale price; not at the market price | Actual-cost basis |

### 7.6 Inventory Business Rules

| ID | Rule | Enforcement |
|---|---|---|
| BRU-IV-01 | Stock level cannot go below zero; the system must warn before allowing a deduction that would result in negative stock | Stock guard |
| BRU-IV-02 | Inventory valuation uses the Weighted Average Cost (WAC) method | System-enforced calculation |
| BRU-IV-03 | Expired items are flagged automatically and cannot be selected for active use without a warning and acknowledgement | Expiry gate |
| BRU-IV-04 | A written-off quantity cannot exceed current available stock | Validation |

---

## 8. User Stories

> Format: **As a [role], I want to [action] so that [outcome].**  
> Each story is linked to a functional requirement and has acceptance criteria in §9.

### 8.1 Platform Stories

| ID | User Story | Priority | Linked FR |
|---|---|---|---|
| US-P-01 | As a **Farm Owner**, I want to register my organization online without calling anyone, so that I can start using the platform immediately | Must Have | FR-P-01 |
| US-P-02 | As a **Farm Owner**, I want to be guided through an onboarding wizard when I first log in, so that I can set up my farm structure without needing training | Must Have | FR-P-03 |
| US-P-03 | As a **Farm Owner**, I want to invite my farm manager and assign them the Farm Manager role, so that they can manage daily operations without accessing financial data | Must Have | FR-P-07 |
| US-P-04 | As a **Farm Owner**, I want to switch the platform language to Bangla, so that my workers who are not comfortable with English can also use the system | Must Have | FR-P-10 |
| US-P-05 | As a **Farm Owner**, I want to manage my subscription and upgrade from Khamar to Banik plan, so that I can unlock multi-shed management | Must Have | FR-P-13 |
| US-P-06 | As a **Farm Manager**, I want to use the platform on my phone while on the farm, even when the internet is not available, so that I can still record data in real time | Should Have | FR-P-11 |
| US-P-07 | As a **Platform Admin**, I want to see an audit log of all changes made by users, so that I can investigate any discrepancies | Must Have | FR-P-12 |

### 8.2 Livestock Stories

| ID | User Story | Priority |
|---|---|---|
| US-LM-01 | As a **Farm Manager**, I want to register a new animal with its tag number, breed, weight, and purchase cost, so that I can begin tracking it from day one | Must Have |
| US-LM-02 | As a **Farm Manager**, I want to assign animals to specific sheds and pens, so that I know exactly where every animal is located | Must Have |
| US-LM-03 | As a **Farm Owner**, I want to group animals into a fattening batch, so that I can track performance and profitability for that batch as a whole | Must Have |
| US-LM-04 | As a **Worker**, I want to record the weekly weight of each animal in my shed, so that the manager can see growth progress | Must Have |
| US-LM-05 | As a **Farm Manager**, I want to view the ADG for each batch, so that I can identify underperforming animals early | Must Have |
| US-LM-06 | As a **Farm Manager**, I want to record a calving event and link the calf to its mother, so that I have complete genealogy records | Must Have |
| US-LM-07 | As a **Farm Owner**, I want to mark an animal as sold with the sale price and buyer name, so that the system automatically calculates my profit for that animal | Must Have |
| US-LM-08 | As a **Farm Manager**, I want to search for an animal by ear tag number and see its complete history, so that I can quickly answer questions during a vet visit | Must Have |
| US-LM-09 | As a **Farm Owner**, I want to transfer an animal between my two farm locations, so that I don't need to record it as a sale and re-purchase | Must Have |

### 8.3 Smart Feeding Stories

| ID | User Story | Priority |
|---|---|---|
| US-SF-01 | As a **Farm Manager**, I want to create a feed formula using locally available ingredients like rice straw and mustard oil cake, so that I can standardize feeding across my sheds | Must Have |
| US-SF-02 | As a **Farm Manager**, I want to see the protein and energy content of the feed formula I create, so that I know if it meets the nutritional requirements of my animals | Must Have |
| US-SF-03 | As a **Worker**, I want to log how much feed was given to Shed 3 today, so that the manager knows what was consumed | Must Have |
| US-SF-04 | As a **Farm Manager**, I want to see the Feed Conversion Ratio for my current fattening batch, so that I can decide if I need to change the feed formula | Must Have |
| US-SF-05 | As a **Farm Owner**, I want to see my total feed cost for this month broken down by shed, so that I can identify which shed is most efficient | Must Have |

### 8.4 Health & Vaccination Stories

| ID | User Story | Priority |
|---|---|---|
| US-HV-01 | As a **Farm Manager**, I want to set up a vaccination schedule for all my cattle that automatically calculates due dates, so that I never miss a critical vaccination | Must Have |
| US-HV-02 | As a **Farm Manager**, I want to receive an alert on my phone 7 days before a vaccination is due, so that I can arrange the vaccine in advance | Must Have |
| US-HV-03 | As a **Veterinarian**, I want to record the treatment given to a sick animal including the drugs and doses, so that there is a permanent medical record | Must Have |
| US-HV-04 | As a **Veterinarian**, I want to mark an animal as quarantined when it shows signs of a contagious disease, so that it cannot be transferred or sold accidentally | Must Have |
| US-HV-05 | As a **Farm Owner**, I want to record the death of an animal with the cause, so that I can calculate the economic loss and improve future prevention | Must Have |
| US-HV-06 | As a **Farm Manager**, I want to see all animals that are overdue for vaccination on a single screen, so that I can take action today | Must Have |

### 8.5 Inventory Stories

| ID | User Story | Priority |
|---|---|---|
| US-IV-01 | As a **Farm Manager**, I want to record when I purchase feed ingredients, so that the system automatically tracks my inventory | Must Have |
| US-IV-02 | As a **Farm Manager**, I want to be alerted when my anthrax vaccine stock falls below 10 doses, so that I can reorder before running out | Must Have |
| US-IV-03 | As a **Farm Manager**, I want to see which medicines are expiring in the next 30 days, so that I can use them before they go to waste | Must Have |
| US-IV-04 | As an **Accountant**, I want to see the total value of our current inventory, so that I can include it in our monthly balance sheet | Must Have |

### 8.6 Finance Stories

| ID | User Story | Priority |
|---|---|---|
| US-FM-01 | As a **Farm Owner**, I want to see how much each animal cost me in total (purchase + feed + medicine), so that I know my break-even sale price | Must Have |
| US-FM-02 | As an **Accountant**, I want to generate a monthly profit and loss report and export it to Excel, so that I can share it with the bank for our loan application | Must Have |
| US-FM-03 | As a **Farm Owner**, I want to see the P&L for my Eid fattening batch of 50 cows including all costs, so that I know how profitable this batch really was | Must Have |
| US-FM-04 | As a **Farm Owner** managing 3 farms, I want to see a consolidated P&L across all farms in one report, so that I can present it to my board of directors | Must Have |
| US-FM-05 | As an **Accountant**, I want expenses from feed consumption and medicine use to be automatically posted to the ledger, so that I do not need to re-enter them manually | Must Have |

### 8.7 Dashboard Stories

| ID | User Story | Priority |
|---|---|---|
| US-DA-01 | As a **Farm Owner**, I want to open the app and immediately see a summary of my entire operation — animal count, health alerts, finances — without navigating through menus | Must Have |
| US-DA-02 | As a **Farm Manager**, I want to see which animals are due for vaccination today on my dashboard, so that I can take action first thing in the morning | Must Have |
| US-DA-03 | As a **Farm Owner** with 3 farms, I want to see each farm's performance on separate cards on my dashboard, so that I can quickly identify which farm needs attention | Must Have |

---

## 9. Acceptance Criteria

> Acceptance criteria use Given-When-Then (GWT) format. Each criterion maps to a user story.

### 9.1 Platform Acceptance Criteria

**US-P-01 — Self-Registration**
```
GIVEN a user visits the Farm360 AI registration page
WHEN they enter a valid organization name, phone number, email, and select a farm type
AND submit the form
THEN the system sends an OTP to the provided phone number within 60 seconds
AND the system creates the organization record in a pending state
AND upon OTP verification, the account is activated and the user is redirected to the onboarding wizard
AND the user receives a welcome email/SMS confirmation
```

**US-P-03 — User Invitation**
```
GIVEN an authenticated Farm Owner
WHEN they navigate to Settings → Users and enter an invitee's phone number and select a role
THEN the invitee receives an SMS with a one-time invitation link valid for 48 hours
AND upon clicking the link and completing profile setup, the user appears in the team with the assigned role
AND the Owner receives a confirmation notification that the user joined
```

**US-P-04 — Language Toggle**
```
GIVEN an authenticated user
WHEN they navigate to Profile → Language and select "বাংলা"
THEN the entire UI (labels, buttons, menus, notifications, error messages) switches to Bangla within 1 second
AND the language preference is persisted across sessions
AND no functional feature is unavailable in Bangla mode
```

**US-P-06 — Offline Mode**
```
GIVEN an authenticated user who has previously loaded the platform
WHEN the network connection is lost
THEN the platform remains accessible with the last-synced data visible in read mode
AND data entry forms (animal weight, feed consumption, vaccination record) remain functional
AND newly entered data is stored locally and flagged as "pending sync"
WHEN connectivity is restored
THEN all pending data is automatically synchronized to the server within 30 seconds
AND the user receives a confirmation notification of successful sync
AND any sync conflicts are surfaced for user resolution
```

### 9.2 Livestock Acceptance Criteria

**US-LM-01 — Animal Registration**
```
GIVEN an authenticated Farm Manager or Owner
WHEN they navigate to Livestock → Add Animal and complete all required fields (tag, species, breed, sex, DOB, acquisition date, purchase price)
THEN the animal is created and assigned a unique system ID
AND the animal appears in the livestock list filtered to the relevant shed
AND a cost ledger entry is automatically created for the purchase price
AND the activity log records the registration event
```

**US-LM-04 — Weight Entry**
```
GIVEN an authenticated Worker, Farm Manager, or Owner
WHEN they enter a weight reading for an animal with a valid date
THEN the weight is saved to the animal's record
AND the system recalculates ADG using all weight entries for that animal
AND if ≥ 2 weight entries exist, ADG is displayed on the animal's profile
AND if the new weight represents a decrease of > 10% from the previous entry, a warning flag is shown to the Farm Manager
```

**US-LM-07 — Animal Sale**
```
GIVEN an authenticated Farm Manager or Owner
WHEN they mark an animal as "Sold" with buyer name, sale date, sale weight, and sale price
THEN the animal status changes to "Sold" and becomes read-only
AND an income entry is automatically posted to the Finance module in the "Animal Sale" category
AND the batch P&L recalculates to include this sale
AND the final profit per animal is displayed (Sale Price − Total Accumulated Cost)
AND the animal is removed from active herd counts in the dashboard
```

### 9.3 Health Acceptance Criteria

**US-HV-01 — Vaccination Schedule**
```
GIVEN an authenticated Vet or Farm Manager
WHEN they create a vaccination protocol and assign it to all cattle in Shed 2
THEN the system generates a vaccination due date for each animal based on the protocol's schedule
AND all due dates are visible in the Health → Vaccination Calendar view
AND each animal's profile shows their upcoming vaccination dates
```

**US-HV-02 — Vaccination Alert**
```
GIVEN an active vaccination schedule with due dates assigned
WHEN a vaccination due date is 7 days away
THEN an in-app notification is sent to the Farm Manager and Owner
AND a push notification is sent to enrolled mobile devices
AND an SMS is sent to the Owner's registered phone number
WHEN the due date passes without a recorded vaccination
THEN the alert escalates to "OVERDUE" status and is highlighted in red on the health dashboard
AND a daily reminder SMS is sent until the vaccination is recorded
```

**US-HV-04 — Quarantine**
```
GIVEN an authenticated Vet or Owner
WHEN they mark an animal as quarantined
THEN the animal's status changes to "Quarantined" on its profile and the dashboard
AND the system blocks any attempt to sell or transfer this animal with a clear error message
AND a quarantine alert appears on the health dashboard with the reason and start date
WHEN the quarantine is lifted by a Vet or Owner
THEN the animal returns to "Active" status and is available for normal operations
```

### 9.4 Finance Acceptance Criteria

**US-FM-02 — Monthly P&L Export**
```
GIVEN an authenticated Accountant or Owner
WHEN they navigate to Finance → Reports → Monthly P&L and select a month
THEN the system generates a P&L statement within 5 seconds
AND the report includes: total income by category, total expenses by category, gross profit, and net profit
AND the report is downloadable as a formatted PDF with the organization name, logo, and report date
AND as an Excel (.xlsx) file with raw data in separate sheets per category
AND the export action is logged in the audit trail with the user's identity and timestamp
```

### 9.5 Inventory Acceptance Criteria

**US-IV-02 — Low Stock Alert**
```
GIVEN an inventory item with a configured reorder threshold of 10 doses
WHEN the stock level drops to or below 10 doses (through any deduction method)
THEN an in-app notification is generated and displayed in the Inventory Alerts panel
AND a push notification is sent to the Farm Manager and Owner
AND the item appears highlighted in red on the inventory dashboard
AND the alert is NOT repeated more than once per 24 hours for the same item
```

---

## 10. Use Cases

### UC-01: Eid Fattening Batch — Full Lifecycle

**Actor:** Farm Owner (Rahim Uddin)  
**Goal:** Manage a fattening batch of 60 cattle from purchase through sale for Eid  
**Preconditions:** Farm, sheds, and pens are set up; subscription is Khamar or above

**Main Flow:**
1. Owner creates a batch named "Eid 2026 Batch" with target sale date
2. Owner registers 60 purchased animals, linking each to the batch and Shed 1
3. System auto-posts BDT 36,00,000 as animal purchase expense (60 × BDT 60,000 average)
4. Feed Manager creates a TMR ration formula (rice straw + mustard oil cake + DCP) and assigns it to Shed 1
5. Worker logs daily feed consumption for Shed 1 every morning
6. System deducts feed from inventory and posts feed cost to Finance daily
7. Vet records monthly deworming events; system deducts dewormer from medicine inventory
8. Farm Manager records bi-weekly weight entries; system calculates ADG per animal
9. At week 12, Owner views Batch P&L: total cost accumulated is BDT 80 lakh; break-even per animal is BDT 1,33,333
10. Owner marks all 60 animals as sold at an average of BDT 1,50,000 each
11. System posts BDT 90 lakh income; batch P&L finalizes: Net Profit = BDT 10 lakh (12.5% ROI)
12. Owner exports batch P&L to PDF for bank records

**Alternative Flow — Animal Death:**
- If an animal dies during the batch, Owner records mortality with cause
- System posts economic loss to Finance module
- Dead animal is removed from batch calculations prospectively
- Batch ROI recalculates based on remaining 59 animals

**Exception Flow — Insufficient Feed Stock:**
- When Worker logs feed consumption and inventory has insufficient stock
- System shows warning: "Insufficient stock for [ingredient]. Current: 50 kg. Required: 200 kg. Proceed and manually record?"
- Worker acknowledges and logs the shortfall
- Low stock alert is triggered to Farm Manager

---

### UC-02: Dairy Farm Monthly Operations

**Actor:** Farm Manager (Tanvir Ahmed)  
**Goal:** Manage monthly health and milk production operations for 800 dairy cows  
**Preconditions:** Corporate subscription; all farms and sheds configured; 800 cows registered

**Main Flow:**
1. System sends morning health dashboard: 12 vaccinations due this week, 3 animals under treatment, 1 cow in quarantine
2. Vet opens Health → Vaccination Due and marks 12 cows as vaccinated; system records events and deducts vaccine stock
3. Milk production is logged daily per cow (or per shed total) — tracked in Finance as income
4. Monthly deworming calendar triggers reminder on Day 1 of month
5. Tanvir runs consolidated P&L across 3 farm locations on the 1st — exports to PDF for board meeting
6. Inventory report shows anthrax vaccine stock at 50 doses (below 100-dose threshold) — Tanvir creates a purchase order
7. At month end, Tanvir reviews FCR and milk yield trends, identifies Shed 4 as underperforming

---

### UC-03: New User Onboarding

**Actor:** New Farm Owner (any tier)  
**Goal:** Complete registration and begin entering data within 30 minutes  
**Preconditions:** User has a smartphone with internet access

**Main Flow:**
1. User opens Farm360 AI website / PWA
2. Clicks "Register" — enters name, phone, email, selects "Cattle Fattening" farm type
3. OTP received via SMS — enters OTP — account verified
4. Onboarding Wizard launches: Step 1 — Organization name and location
5. Step 2 — Add first farm (name, address, district)
6. Step 3 — Add first shed (name, capacity)
7. Step 4 — Add first animal (guided with defaults pre-filled)
8. Step 5 — Choose subscription plan (Bittho for free, or paid)
9. User reaches home dashboard with their first animal visible
10. Tutorial tooltip prompts next action: "Log today's feed for Shed 1"

---

## 11. Edge Cases

### 11.1 Platform Edge Cases

| ID | Scenario | Expected Behavior |
|---|---|---|
| EC-P-01 | User enters the same phone number as an existing account | System detects duplicate and offers "Log in instead?" with phone-based OTP login |
| EC-P-02 | OTP expires (> 10 minutes) before user enters it | System shows "OTP expired" and provides a "Resend OTP" button (max 3 resends per hour) |
| EC-P-03 | User loses internet during account creation after OTP verification | System stores partial registration state; user can resume on next session |
| EC-P-04 | Owner account is deleted | System prevents deletion if > 0 other users exist in the organization; Owner must transfer ownership first |
| EC-P-05 | Subscription payment fails on renewal | Platform remains active for 7-day grace period; Owner receives 3 payment failure notifications; access is suspended on Day 8 |
| EC-P-06 | Two users edit the same record simultaneously | Last-write-wins with optimistic locking; conflicting user receives a "Record updated by [user]. Refresh to see latest." warning |
| EC-P-07 | User switches language mid-form | Form content is preserved; labels switch language; in-progress text input is retained |

### 11.2 Livestock Edge Cases

| ID | Scenario | Expected Behavior |
|---|---|---|
| EC-LM-01 | Duplicate animal tag ID entered within same organization | System blocks with validation error: "Tag ID [X] already exists. Please use a unique ID." |
| EC-LM-02 | Animal age is less than minimum for a breeding record | Warning displayed: "Animal is [X] months old. Minimum recommended breeding age for [species] is [Y] months." — User can override with confirmation |
| EC-LM-03 | Weight entry is 50% less than previous entry | Warning displayed: "This weight is significantly lower than the previous entry (50%+ drop). Please confirm." — requires user to tick a confirmation checkbox |
| EC-LM-04 | Dam and sire are the same animal | System blocks with validation error: "A single animal cannot be both sire and dam." |
| EC-LM-05 | Transfer destination farm is at animal capacity for its subscription tier | System blocks transfer with error: "Destination farm [X] has reached its animal limit. Please upgrade the plan or archive inactive animals." |
| EC-LM-06 | Animal DOB is entered as today | Allowed; system validates that no weight/health records precede the DOB |
| EC-LM-07 | User attempts to sell a quarantined animal | System blocks with: "Animal [ID] is under quarantine. Please release from quarantine before recording a sale." |

### 11.3 Feeding Edge Cases

| ID | Scenario | Expected Behavior |
|---|---|---|
| EC-SF-01 | Feed consumption logged for a shed with 0 animals | Warning: "Shed [X] has no active animals assigned. Do you want to continue logging?" — User can confirm |
| EC-SF-02 | Feed formula uses an ingredient with no current stock | Warning highlighted in orange: "Ingredient [X] has zero stock. Log a stock-in entry to update inventory." |
| EC-SF-03 | Same formula assigned to same shed twice | System replaces the previous assignment with the new one and records the change date |
| EC-SF-04 | Feed consumption logged for a date when the shed had no assigned formula | Consumption is recorded but not linked to a formula; Feed Cost is calculated based on ingredient prices if ingredients are specified |

### 11.4 Finance Edge Cases

| ID | Scenario | Expected Behavior |
|---|---|---|
| EC-FM-01 | Animal sold at a price lower than its accumulated cost | System allows the transaction; the P&L correctly shows a negative profit (loss) for that animal and highlights it in red |
| EC-FM-02 | Accountant attempts to delete a financial entry from the previous month | System blocks with: "Financial entries from closed periods cannot be deleted. Use a reversal entry to correct this." |
| EC-FM-03 | Animal purchase cost is entered as BDT 0 | Warning: "Purchase cost is set to zero. This will affect P&L calculations. Confirm?" — requires acknowledgement |
| EC-FM-04 | Export requested for a month with zero transactions | System generates the report with zero values and notes "No transactions recorded in this period." |

### 11.5 Health Edge Cases

| ID | Scenario | Expected Behavior |
|---|---|---|
| EC-HV-01 | Vaccination recorded twice for the same animal on the same day for the same vaccine | System warns: "A record for [Vaccine Name] on [Date] already exists for this animal. Add a duplicate?" — Requires explicit confirmation |
| EC-HV-02 | Treatment drug not in inventory catalog | User can select "Not tracked in inventory" to log the treatment without inventory deduction; treatment is still recorded |
| EC-HV-03 | Vet tries to log treatment for an animal in a farm they don't have access to | System blocks with permission error: "You do not have access to Farm [X] data." |

### 11.6 Inventory Edge Cases

| ID | Scenario | Expected Behavior |
|---|---|---|
| EC-IV-01 | Stock consumption would result in negative stock balance | System warns: "This action would reduce [Item] stock to negative (-X units). Available stock is [Y]. Do you want to continue?" — Owner/Manager can override; Worker cannot |
| EC-IV-02 | Item expiry date set in the past during stock-in entry | Warning: "The entered expiry date ([date]) is in the past. Please confirm this is correct." |
| EC-IV-03 | Item category is changed after stock transactions exist | System allows category change but logs an audit event; historical reports retain the original category |

---

## 12. Error Handling

### 12.1 Error Classification

| Class | Code Range | Meaning | User Experience |
|---|---|---|---|
| **Validation Error** | 4001–4099 | User input does not pass field validation | Inline field-level red border and message |
| **Business Rule Violation** | 4101–4199 | Input violates a business rule | Non-blocking modal warning or inline message with action options |
| **Permission Denied** | 4201–4299 | User role does not allow this action | Toast notification: "You don't have permission to do this." |
| **Not Found** | 4301–4399 | Referenced resource does not exist | "Record not found. It may have been deleted." with back navigation |
| **Conflict** | 4401–4499 | Duplicate or stale data conflict | "This record was updated by [user] at [time]. Refresh to see the latest version." |
| **Server Error** | 5001–5099 | Unexpected server-side failure | "Something went wrong. Our team has been notified. Please try again in a moment." + auto-retry |
| **Offline Error** | 6001–6099 | Action requires connectivity | "You are offline. This action will be saved and completed when you reconnect." |

### 12.2 Error Handling Standards

| Standard | Requirement |
|---|---|
| **No raw error codes** | Users must never see raw HTTP error codes or stack traces |
| **No blame language** | Errors must not imply fault ("Your input was wrong"). Use neutral language ("Please enter a valid date") |
| **Always actionable** | Every error message must include a next step or a button to take action |
| **Bilingual** | All error messages must be available in both Bangla and English |
| **Logged server-side** | All 5xx errors are automatically logged with stack trace, request context, tenant ID, user ID, and timestamp — never exposed to the user |
| **Retry logic** | Network transient errors (timeout, 502, 503) trigger automatic retry (3 attempts, exponential backoff) before showing an error to the user |
| **Graceful degradation** | If a non-critical widget (e.g., a chart) fails to load, it shows an empty state with a refresh button — the rest of the page remains functional |

### 12.3 Form Validation Standards

| Standard | Requirement |
|---|---|
| **Real-time validation** | Required field validation fires on blur (when user leaves the field) |
| **Submit validation** | Full form validation on submit; first invalid field is scrolled into view and focused |
| **Date validation** | Date pickers enforce min/max date bounds at the UI level; server-side re-validates |
| **Numeric bounds** | Weight, quantity, and price fields enforce minimum 0 and platform-defined maximums |
| **Character limits** | All text fields enforce and display remaining character counts |
| **Sanitization** | All text inputs are sanitized server-side to prevent XSS and injection attacks |

---

## 13. Notifications

### 13.1 Notification Types

| Type | Channel | Use Cases |
|---|---|---|
| **In-App** | Platform UI notification center | All alerts, confirmations, activity updates |
| **Push Notification** | Browser push (PWA), future native app | Urgent alerts only: vaccination overdue, critical low stock |
| **SMS** | Bangladesh SMS gateway (e.g., SSL Commerz / Infobip) | Vaccination due/overdue, subscription expiry, OTP |
| **Email** | Transactional email (SendGrid / AWS SES) | Account confirmation, monthly reports, subscription invoices |
| **WhatsApp** (Phase 2) | WhatsApp Business API | Summary alerts for owners who prefer WhatsApp |

### 13.2 Notification Event Registry

| Event | Recipients | Channels | Timing |
|---|---|---|---|
| Registration OTP | Registering user | SMS | Immediate |
| Welcome message | New tenant owner | Email + SMS | Immediate post-verification |
| User invitation sent | Invited user | SMS | Immediate |
| Vaccination due (7 days) | Farm Manager + Owner | In-App + Push + SMS | 7 days before due date |
| Vaccination overdue | Farm Manager + Owner | In-App + Push + SMS | Due date + daily until completed |
| Low stock alert | Farm Manager + Owner | In-App + Push + SMS | When threshold crossed |
| Expiry alert (30 days) | Farm Manager | In-App | Daily (once per item per day) |
| Subscription expiry (7 days) | Owner | In-App + Email + SMS | 7 days before expiry |
| Subscription expired | Owner | In-App + Email + SMS | On expiry date + daily for 7 days |
| Payment success | Owner | Email + SMS | Immediate |
| Payment failure | Owner | Email + SMS | Immediate + 3 retries over 7 days |
| Animal death recorded | Owner | In-App | Immediate |
| Monthly P&L ready | Owner + Accountant | In-App + Email | 1st of each month |
| Sync completed (offline) | Active user | In-App | Immediate after sync |
| Login from new device | Account owner | Email + SMS | Immediate |

### 13.3 Notification Preferences

- Users can configure channel preferences per notification type (e.g., SMS only for overdue vaccinations, email only for monthly reports)
- Farm Owners can configure which roles receive which notification categories
- Notification frequency caps: low-stock alerts for the same item maximum once per 24 hours
- Quiet hours: SMS notifications suppressed between 10:00 PM – 7:00 AM BDT unless classified as critical

### 13.4 SMS Gateway Specification

| Requirement | Detail |
|---|---|
| Sender ID | "Farm360" alphanumeric sender ID |
| Character limit | 160 characters per SMS; messages > 160 chars sent as concatenated SMS |
| Language | Bangla SMS supported via Unicode encoding (70 chars per segment) |
| Delivery receipt | Delivery receipts consumed and logged for audit purposes |
| Failover | Primary gateway (SSL Commerz) → failover (Infobip) if primary fails |
| Cost optimization | Non-urgent notifications batched and sent in off-peak hours |

---

## 14. Audit Requirements

### 14.1 Audit Philosophy

Every significant action in Farm360 AI must be traceable. The audit system serves three purposes: regulatory compliance, operational accountability, and dispute resolution.

### 14.2 Auditable Events

| Category | Audited Events |
|---|---|
| **Authentication** | Login success, login failure (with IP), logout, password change, MFA events |
| **User Management** | User invitation, role change, user deactivation, ownership transfer |
| **Animal Records** | Animal creation, profile edit, status change, deletion (if allowed), transfer |
| **Health Records** | Vaccination recording, treatment logging, quarantine status changes, mortality recording |
| **Finance Records** | Income/expense creation, modification (with reversal entries), report export |
| **Inventory** | Stock-in, stock-out (manual), write-off, threshold changes |
| **Subscription** | Plan change, payment event, account suspension, account reactivation |
| **Data Export** | All PDF/Excel exports with user, timestamp, and record scope |
| **Settings** | Farm/shed/pen create or edit, language change, notification preference change |

### 14.3 Audit Log Data Structure

Each audit record must contain:

```
{
  "id": "uuid",
  "tenantId": "uuid",
  "farmId": "uuid | null",
  "userId": "uuid",
  "userFullName": "string",
  "userRole": "string",
  "action": "CREATED | UPDATED | DELETED | EXPORTED | LOGIN | ...",
  "entityType": "Animal | HealthRecord | FinanceEntry | ...",
  "entityId": "uuid",
  "entityDisplayName": "string",
  "previousValue": "JSON | null",
  "newValue": "JSON | null",
  "ipAddress": "string",
  "deviceInfo": "string",
  "timestamp": "ISO8601 UTC",
  "additionalContext": "JSON | null"
}
```

### 14.4 Audit Log Retention

| Tier | Retention Period | Storage |
|---|---|---|
| Active logs (queryable) | 12 months | Primary database |
| Archived logs | 7 years | Cold storage (S3 Glacier or equivalent) |
| Authentication logs | 2 years | Separate secure log store |

### 14.5 Audit Access

| Role | Access |
|---|---|
| **Organization Owner** | View audit log for their organization only |
| **Platform Admin** | View audit logs for all tenants (for support/compliance) |
| **Farm Manager / Others** | No audit log access |
| **Auditors (external)** | Read-only access to exported audit reports via Owner delegation |

### 14.6 Audit Immutability

- Audit log records are **insert-only** — no update or delete operations are permitted on audit records
- Audit records are written to a separate audit schema/database with separate credentials
- Application service accounts have INSERT-only privileges on the audit log table
- Quarterly integrity checks verify that no audit records have been tampered with

---

## 15. Localization

### 15.1 Supported Locales (MVP)

| Locale | Language | Script | Region | Status |
|---|---|---|---|---|
| `bn-BD` | Bengali (Bangla) | Bengali script | Bangladesh | **Primary — MVP** |
| `en-BD` | English | Latin | Bangladesh (English numerals, BDT currency) | **Secondary — MVP** |

### 15.2 Localization Scope

| Element | Localized |
|---|---|
| All UI labels and buttons | ✅ Yes |
| Error messages | ✅ Yes |
| Notification text (in-app, SMS, email) | ✅ Yes |
| PDF/Excel report content | ✅ Yes |
| Dates (display format) | ✅ Yes (DD/MM/YYYY for Bangladesh) |
| Currency | ✅ Yes (BDT, symbol ৳) |
| Numbers | ✅ Yes (Bengali numerals optional, Latin default) |
| Animal breed names | ✅ Yes (local Bangla names for each breed) |
| Feed ingredient names | ✅ Yes (local Bangla names pre-loaded) |
| System emails subject/body | ✅ Yes |
| Help documentation | ✅ Yes (Phase 2) |
| Mobile push notification text | ✅ Yes |

### 15.3 Translation Management

- Translation strings stored in a centralized key-value store (`i18n/bn.json`, `i18n/en.json`)
- No hardcoded strings in application code; all visible text references translation keys
- New features must include translation strings for both languages before merging to production
- Translation review by a native Bangla-speaking domain expert (farming context) before release
- Missing translation keys fall back to English; missing keys are logged as warnings

### 15.4 Date, Time, and Number Formats

| Format | Bangladesh Standard |
|---|---|
| Date display | DD/MM/YYYY (e.g., 15/07/2026) |
| Time display | 12-hour format with AM/PM |
| Time zone | Asia/Dhaka (UTC+6) |
| Currency | BDT; symbol ৳; thousands separator comma (e.g., ৳1,50,000) |
| Number format | South Asian number system (Lakh/Crore) for large numbers in reports |
| Bengali numerals | Optional toggle in user preferences |

### 15.5 Future Locales (Phase 3)

| Locale | Market |
|---|---|
| `my-MM` | Myanmar (Burmese) |
| `ne-NP` | Nepal (Nepali) |
| `si-LK` | Sri Lanka (Sinhala) |
| `hi-IN` | India (Hindi) |

---

## 16. Performance Requirements

### 16.1 Response Time Requirements

| Operation | P50 Target | P95 Target | P99 Target |
|---|---|---|---|
| Page / route load (first contentful paint) | < 1.0s | < 2.0s | < 3.5s |
| API response (simple CRUD) | < 150ms | < 400ms | < 800ms |
| API response (dashboard aggregation) | < 500ms | < 1.5s | < 3.0s |
| Report generation (monthly P&L) | < 2.0s | < 5.0s | < 10.0s |
| PDF export generation | < 3.0s | < 7.0s | < 15.0s |
| Search results (livestock search) | < 300ms | < 800ms | < 1.5s |
| Offline sync on reconnect | < 10s for < 50 records | < 30s for < 500 records | N/A |

### 16.2 Client-Side Performance

| Metric | Target |
|---|---|
| Lighthouse Performance Score (mobile) | ≥ 80 |
| First Contentful Paint (FCP) | ≤ 1.5s (3G) |
| Largest Contentful Paint (LCP) | ≤ 2.5s (3G) |
| Cumulative Layout Shift (CLS) | < 0.1 |
| Total Blocking Time (TBT) | < 300ms |
| JavaScript bundle size (initial) | < 250KB gzipped |
| Time to Interactive (TTI) | < 3.5s (3G) |

### 16.3 Load Testing Requirements

| Scenario | Requirement |
|---|---|
| Concurrent users per tenant (Enterprise) | 50 simultaneous users without degradation |
| Platform-wide concurrent users (MVP launch) | 500 concurrent users across all tenants |
| Platform-wide concurrent users (Year 1 target) | 5,000 concurrent users across all tenants |
| Peak load (Eid season +200%) | No downtime; auto-scaling activates within 3 minutes |
| Batch data import (100 animals) | Completes within 60 seconds |

### 16.4 Performance Monitoring

- Application Performance Monitoring (APM) using Datadog or New Relic
- Synthetic monitoring: automated end-to-end critical path tests run every 5 minutes
- Real User Monitoring (RUM) tracking actual user performance in Bangladesh
- Performance budget enforcement in CI/CD: build fails if bundle size or Lighthouse score regresses
- Alerting: page load P95 > 4s or API P95 > 1s triggers PagerDuty alert to on-call engineer

---

## 17. Security Requirements

### 17.1 Authentication & Authorization

| Requirement | Detail |
|---|---|
| Authentication method | Phone OTP (primary for Bangladesh users) + Email/Password (secondary) |
| Session management | JWT access tokens (15-minute expiry) + Refresh tokens (30-day expiry, rotating) |
| MFA | TOTP (Google Authenticator) mandatory for Organization Owner on Enterprise and Corporate tiers |
| Password policy | Minimum 8 characters, 1 uppercase, 1 number; bcrypt hashing (cost factor ≥ 12) |
| Account lockout | 5 failed login attempts → 15-minute lockout; 10 attempts → 24-hour lockout with alert |
| Session invalidation | All active sessions invalidated on password change or role change |
| Role enforcement | Enforced server-side on every API request; client-side role checks are visual only |

### 17.2 Data Security

| Requirement | Standard |
|---|---|
| Data at rest encryption | AES-256 on all database storage volumes |
| Data in transit encryption | TLS 1.3 minimum; TLS 1.2 acceptable as fallback; TLS 1.0/1.1 blocked |
| Database credential management | Secrets Manager (AWS Secrets Manager or equivalent); no credentials in code or config files |
| PII handling | Phone numbers, NID numbers, and personal names stored encrypted in database |
| PII in logs | Masked in all application logs (phone: "01XXXXXXXX05", email: "r***@gmail.com") |
| API keys | All API keys stored hashed; displayed once on creation; cannot be retrieved later |
| File upload security | Uploaded files (photos) scanned for malware; file type whitelist enforced server-side |
| SQL injection | Parameterized queries enforced; ORM usage validated; no raw SQL from user input |
| XSS protection | Output encoding enforced; Content Security Policy (CSP) headers configured |
| CSRF protection | CSRF tokens required for all state-changing operations |

### 17.3 Multi-Tenant Security

| Requirement | Detail |
|---|---|
| Tenant isolation enforcement | Every database query is scoped by tenantId at the repository layer; no global queries permitted |
| Cross-tenant access prevention | Automated testing includes negative tests verifying that Tenant A cannot access Tenant B's data |
| Tenant context validation | tenantId extracted from authenticated JWT, not from user input |
| Admin impersonation | Platform admins can view tenant data only via explicitly logged impersonation sessions; all actions carry impersonation flag in audit log |

### 17.4 Infrastructure Security

| Requirement | Detail |
|---|---|
| Network | Application deployed in private VPC; only load balancer exposed publicly on ports 443 and 80 (redirects to 443) |
| Firewall | WAF enabled with OWASP Core Rule Set; DDoS protection via AWS Shield Standard |
| Container security | Docker images scanned for vulnerabilities in CI/CD; no containers running as root |
| Secrets in CI/CD | No secrets in environment variables in CI/CD logs; all secrets via Secrets Manager |
| Dependency management | Automated vulnerability scanning (Dependabot/Snyk); critical CVEs patched within 48 hours |
| Security audit | Annual penetration test by certified third-party security firm (CREST or OSCP certified) |
| Bug bounty | Responsible disclosure policy published; rewards for valid vulnerabilities |

### 17.5 Compliance Security Controls

| Control | Requirement |
|---|---|
| OWASP Top 10 | All items addressed and verified before MVP launch |
| Data retention | User data deleted within 90 days of account deletion request |
| Right to export | Organization owners can export all their data on demand |
| Consent | Terms of service and privacy policy consent recorded with timestamp and version at registration |

---

## 18. Scalability Requirements

### 18.1 Tenant Scalability

| Dimension | MVP Target | Year 2 Target | Year 3 Target |
|---|---|---|---|
| Active Tenants | 300 | 1,200 | 4,000 |
| Total Animals in DB | 100,000 | 500,000 | 2,000,000 |
| Daily API Requests | 500,000 | 3,000,000 | 15,000,000 |
| Concurrent Users (peak) | 500 | 3,000 | 15,000 |
| Data Storage (total) | 200 GB | 1 TB | 5 TB |

### 18.2 Application Scalability Architecture

| Component | Scalability Approach |
|---|---|
| Web Application | Stateless application servers; horizontal scaling behind Application Load Balancer |
| API Layer | Kubernetes-based auto-scaling with HPA (Horizontal Pod Autoscaler) |
| Database (writes) | Primary PostgreSQL instance; connection pooling via PgBouncer |
| Database (reads) | Read replicas for dashboard and report queries; CQRS pattern for heavy read operations |
| File Storage | Object storage (AWS S3 or DigitalOcean Spaces); scales independently |
| Cache Layer | Redis cluster for session, computed dashboard data, and notification deduplication |
| Background Jobs | Queue-based workers (RabbitMQ or AWS SQS) for report generation, notification sending |
| CDN | Static assets and report PDFs served via CDN (CloudFront or Bunny.net) |

### 18.3 Database Scalability Strategy

| Phase | Strategy |
|---|---|
| MVP (0–300 tenants) | Single multi-tenant PostgreSQL database with tenant schema per tenant |
| Phase 2 (300–2,000 tenants) | Introduce read replicas; evaluate shard grouping by region |
| Phase 3 (2,000+ tenants) | Database-per-tier sharding for Enterprise tenants; pooled schema for SME tenants |

### 18.4 Auto-Scaling Triggers

| Metric | Scale-Out Trigger | Scale-In Trigger |
|---|---|---|
| CPU utilization | > 70% for 2 minutes | < 30% for 10 minutes |
| Memory utilization | > 80% for 2 minutes | < 40% for 10 minutes |
| Request queue depth | > 100 queued requests | < 10 queued requests |
| Response time (P95) | > 1.5s for 2 minutes | — |

---

## 19. Availability Requirements

### 19.1 SLA Targets

| Tier | Uptime SLA | Max Downtime/Month | Max Downtime/Year |
|---|---|---|---|
| Bittho (Free) | 99.5% | 3.65 hours | 43.8 hours |
| Khamar (Basic) | 99.9% | 43.8 minutes | 8.7 hours |
| Banik (Professional) | 99.9% | 43.8 minutes | 8.7 hours |
| Corporation (Enterprise) | 99.95% | 21.9 minutes | 4.4 hours |

### 19.2 Maintenance Windows

| Type | Schedule | Duration | Notification |
|---|---|---|---|
| Planned maintenance | Sunday 02:00–04:00 BDT | ≤ 2 hours | 72 hours advance notice via email + in-app banner |
| Emergency patches | Any time | ≤ 30 minutes | 30-minute advance notice when possible |
| Database migrations | Sunday 02:00–03:00 BDT | ≤ 1 hour | Included in weekly maintenance window |

### 19.3 Health Monitoring

| Component | Monitoring Method | Alert Threshold |
|---|---|---|
| Application uptime | Synthetic ping every 60 seconds | No response for 2 consecutive checks |
| API health endpoint | Automated test every 2 minutes | Health endpoint returns non-200 for 2 checks |
| Database connectivity | Application-level check every 30 seconds | Connection failure for 3 consecutive attempts |
| SSL certificate | Expiry monitoring | Alert 30 days before expiry |
| Disk space | Continuous | Alert at 80% full |
| Error rate | Real-time | Alert if 5xx error rate > 1% over 5 minutes |

### 19.4 Incident Response

| Severity | Definition | Response Time | Communication |
|---|---|---|---|
| P0 — Critical | Complete platform outage | 15 minutes | Immediate status page update + SMS to enterprise clients |
| P1 — High | Core module unavailable (e.g., Health module down) | 30 minutes | Status page update within 30 minutes |
| P2 — Medium | Degraded performance; non-core feature down | 2 hours | Status page update within 2 hours |
| P3 — Low | Minor visual bug; cosmetic issue | 24 hours | Internal ticket only |

**Status Page:** Public status page at status.farm360.ai updated in real time during incidents.

---

## 20. Backup Strategy

### 20.1 Backup Scope

| Data Type | Backup Method | Frequency | Retention |
|---|---|---|---|
| PostgreSQL database (full) | Automated snapshot | Daily (02:00 BDT) | 30 days |
| PostgreSQL database (incremental/WAL) | Continuous WAL archiving | Every 15 minutes | 7 days |
| Object storage (files, photos, exports) | Cross-region replication | Continuous | 1 year |
| Application configuration | Version-controlled in Git | On every change | Indefinite |
| Infrastructure configuration (IaC) | Terraform / Pulumi in Git | On every change | Indefinite |
| Audit logs | Separate encrypted backup | Daily | 7 years |

### 20.2 Backup Storage

| Location | Description |
|---|---|
| Primary backup | Same region as production (ap-south-1 / AWS Mumbai) |
| Secondary backup | Geographically separate region (ap-southeast-1 / Singapore) |
| Encryption | All backup files encrypted with AES-256 at rest; encryption keys managed in KMS |
| Access control | Backup storage accessible only to infrastructure automation; no human read access without formal approval |

### 20.3 Backup Verification

| Verification Activity | Frequency |
|---|---|
| Automated backup integrity check (checksum validation) | Daily |
| Restore test (restore to staging environment) | Monthly |
| Full disaster recovery drill | Quarterly |
| Backup retention compliance check | Monthly |

### 20.4 Backup RPO

| Scenario | RPO |
|---|---|
| Database corruption or data loss | ≤ 15 minutes (WAL archiving) |
| Region-level outage | ≤ 1 hour (cross-region backup restore) |
| Accidental record deletion (soft-delete) | Immediate (soft delete with 30-day recovery window) |

---

## 21. Disaster Recovery Strategy

### 21.1 RTO & RPO Targets

| Scenario | RTO | RPO |
|---|---|---|
| Single application server failure | < 5 minutes (auto-scaling) | 0 (stateless servers) |
| Database primary failure | < 15 minutes (automatic failover to standby) | < 15 minutes |
| Full Availability Zone failure | < 30 minutes | < 15 minutes |
| Full Region failure | < 4 hours | < 1 hour |
| Complete platform rebuild from scratch | < 24 hours | < 1 hour |

### 21.2 Recovery Architecture

```
Production Region (ap-south-1 — Mumbai)
├── Primary Database (PostgreSQL — Multi-AZ)
│   ├── Standby replica in AZ-B (auto-failover via RDS Multi-AZ)
│   └── Continuous WAL streaming to DR region
├── Application Cluster (Kubernetes — Multi-AZ)
└── Object Storage (Cross-region replication enabled)

DR Region (ap-southeast-1 — Singapore)
├── DR Database (Read replica — promoted on failover)
├── Application cluster (scaled down; scales up on DR activation)
└── Object Storage (Replica — read-only until DR activation)
```

### 21.3 DR Runbook Overview

**Phase 1: Detection (0–5 min)**
- PagerDuty alert fires from monitoring
- On-call engineer assesses impact via status dashboard
- Incident bridge opened; severity declared

**Phase 2: Assessment (5–15 min)**
- Root cause identified (zone failure vs. region failure vs. application failure)
- RTO/RPO assessment for affected services
- DR decision made by CTO

**Phase 3: Failover (15–60 min for region-level DR)**
- DNS failover initiated (Route 53 health-check-based routing to DR region)
- DR database promoted from read replica to primary
- DR application cluster scaled up to full capacity
- Payment gateway failover verified
- SMS gateway failover to secondary provider

**Phase 4: Validation (60–90 min)**
- End-to-end smoke test in DR environment
- Key customer tenants validated
- Status page updated: "Service restored"

**Phase 5: Post-Incident**
- Post-mortem within 48 hours
- Root cause documented
- Preventive measures identified and ticketed

### 21.4 DR Testing Schedule

| Test Type | Frequency | Lead |
|---|---|---|
| Database failover drill (AZ-level) | Monthly | DevOps Engineer |
| Application failover drill (AZ-level) | Monthly | DevOps Engineer |
| Full region failover drill | Quarterly | CTO + DevOps |
| Full platform rebuild from backup drill | Annually | Entire Engineering Team |

---

## 22. Future Expansion Plan

### 22.1 Phase 2 — Scale & Differentiate (Months 7–18)

#### 22.1.1 Poultry Module (Month 8)

The poultry module will extend the platform to serve broiler and layer operations — the largest livestock segment in Bangladesh by animal count.

| Feature | Description |
|---|---|
| Flock Management | Flock creation, breed, chick arrival, house assignment |
| Broiler Performance Tracking | Daily mortality, daily weight sample, FCR by flock |
| Layer Management | Hen-day production, egg collection, grading, sales |
| Vaccination Program (Poultry) | Newcastle, Gumboro, Marek's — auto-scheduling for flock |
| Biosecurity Checklist | Digital checklist for poultry house biosecurity protocols |
| Flock Close-Out Report | Final performance report on flock completion |

#### 22.1.2 AI Features (Months 9–12)

| Feature | Technology | Training Data Source |
|---|---|---|
| Feed Ration Optimizer | Linear Programming + ML | Ingredient nutritional data + ADG outcomes |
| Health Risk Scorer | Random Forest / LSTM | Weight trends, feed consumption patterns, vaccination history |
| Profit Forecast | Time-series regression | Historical batch P&L, current cost trajectory |
| Breeding Recommendation | Rule-based + ML | Genealogy records, offspring performance data |

#### 22.1.3 Mobile Native App — Android (Month 10)

- React Native Android app (primary) targeting Android 8+
- Offline-first architecture (SQLite local store + sync engine)
- Barcode/QR code scanning for animal tag identification
- Camera integration for animal photo capture
- Push notifications via Firebase Cloud Messaging

#### 22.1.4 Dairy Enhancements (Month 9)

| Feature | Description |
|---|---|
| Milk Production Logging | Daily milk yield per cow (AM/PM sessions) |
| Lactation Curve Tracking | DIM-based lactation performance chart |
| Milk Sales Recording | Daily milk dispatch with buyer and price |
| Dairy Income Dashboard | Milk revenue vs. feed cost per cow per day |
| Somatic Cell Count Tracking | Quality monitoring log |

#### 22.1.5 Labor Management (Month 11)

| Feature | Description |
|---|---|
| Staff Roster | Employee profiles, roles, shift assignments |
| Attendance Tracking | Daily attendance recording per farm |
| Payroll Integration | Monthly salary calculation and payment recording |
| Labor Cost → Finance | Auto-post labor costs to Finance module |

---

### 22.2 Phase 3 — Platform & Ecosystem (Months 19–36)

#### 22.2.1 IoT Integration Layer

| Device | Integration Protocol | Data Frequency |
|---|---|---|
| Smart ear tags | MQTT over TLS | Every 5 minutes |
| Automated weight scales | REST webhook | On each weighing event |
| Milk meters | MQTT | Per milking session |
| Temperature/humidity sensors | MQTT | Every 15 minutes |
| Water trough sensors | MQTT | Every 30 minutes |
| Feed bin sensors | MQTT | Every hour |

Infrastructure: Edge gateway (Raspberry Pi / industrial modem) per farm → MQTT broker (AWS IoT Core) → IoT ingestion microservice → Time-series database (InfluxDB or TimescaleDB) → AI model integration

#### 22.2.2 Marketplace

| Feature | Description | Revenue Model |
|---|---|---|
| Feed Supplier Listing | Verified suppliers list feed products with pricing | 2–3% transaction commission |
| Medicine & Vaccine Marketplace | Verified pharmacy/distributor listings | 2–3% transaction commission |
| Vet Marketplace | Verified licensed vets bookable for teleconsult or on-farm visit | 10–15% booking commission |
| Livestock Trading | Verified health-certified animals listed for sale | 1–2% transaction fee |
| Equipment Store | Farm equipment and IoT sensor listing | 2% commission |

#### 22.2.3 Financial Services Integration

| Service | Partner Model | Integration |
|---|---|---|
| Micro-credit (BNPL) | Partnership with BRAC Bank, DBH, or fintech | Farm360 financial data (farmer-consented) → credit scoring → loan offer |
| Crop/Livestock Insurance | Partnership with insurance companies | Premium calculation based on herd data; claim integration |
| Savings/Investment | RDFI partnership | Automated savings recommendations based on P&L |

#### 22.2.4 Aquaculture Module (Month 30)

| Feature | Description |
|---|---|
| Pond Management | Pond registration, species, stocking density |
| Feed Management | Feed type, daily feeding log, FCR |
| Water Quality Tracking | pH, dissolved oxygen, temperature logs |
| Disease & Treatment Log | Fish disease recording, medicine log |
| Harvest Recording | Harvest weight, grade, price, buyer |
| Pond P&L | Per-pond profitability report |

#### 22.2.5 Regional Expansion — Localization Requirements

| Country | Language | Regulatory Alignment | Launch Target |
|---|---|---|---|
| Myanmar | Burmese (my-MM) | Ministry of Livestock, Fisheries and Rural Development (MLFRD) compliance | Month 30 |
| Nepal | Nepali (ne-NP) | Department of Livestock Services Nepal | Month 32 |
| Sri Lanka | Sinhala (si-LK) + Tamil | Department of Animal Production and Health | Month 34 |

#### 22.2.6 Enterprise API & Integration

| Feature | Description |
|---|---|
| Public REST API | Full CRUD access to all Farm360 data for enterprise clients |
| Webhook System | Real-time event webhooks for integration with external systems |
| SAP Integration Connector | Pre-built connector for SAP HANA (corporate farms) |
| Power BI / Tableau Export | Dataset connector for advanced BI reporting |
| Government Data Integration | API for DLS data submission (pending DLS MoU) |

---

## 23. Appendix

### 23.1 Requirement Traceability Matrix (Sample)

| User Story | Functional Requirement | Business Requirement | Feature ID | Acceptance Criteria |
|---|---|---|---|---|
| US-LM-01 | FR-LM-01 | BR-07 | F-014 | AC-LM-01 |
| US-HV-02 | FR-HV-04 | BR-02 | F-038 | AC-HV-02 |
| US-FM-02 | FR-FM-08, FR-FM-10 | BR-06 | F-064, F-069 | AC-FM-02 |
| US-P-06 | FR-P-11 | BR-08 | F-013 | AC-P-06 |

### 23.2 API Design Principles

| Principle | Standard |
|---|---|
| API Style | RESTful JSON API |
| Versioning | URI versioning (`/api/v1/`) |
| Authentication | Bearer token (JWT) in Authorization header |
| Pagination | Cursor-based pagination for all list endpoints |
| Filtering | Query parameters (`?status=active&species=cattle`) |
| Sorting | `?sort=createdAt&order=desc` |
| Error format | RFC 7807 Problem Details for HTTP APIs |
| Rate limiting | 1,000 requests/hour per user; 10,000 requests/hour per tenant |
| CORS | Whitelist of approved origins only |

### 23.3 Data Retention Policy

| Data Type | Active Retention | Archive Retention | Deletion |
|---|---|---|---|
| Animal records | Life of account | 7 years post-account deletion | After 7-year archive |
| Financial records | Life of account | 7 years (legal compliance) | After 7-year archive |
| Health records | Life of account | 7 years | After 7-year archive |
| Audit logs | 12 months queryable | 7 years archived | After 7-year archive |
| User account data | Life of account | 90 days post-deletion request | 90 days after deletion |
| Photos/media | Life of account | 1 year post-account deletion | After 1-year archive |

### 23.4 Testing Requirements

| Test Type | Minimum Coverage | Owner | Frequency |
|---|---|---|---|
| Unit Tests | ≥ 80% business logic | Engineering | Every commit |
| Integration Tests | All API endpoints covered | Engineering | Every PR |
| End-to-End Tests | All critical user journeys | QA | Every release |
| Performance Tests | Load test to 2× expected peak | DevOps | Monthly + pre-release |
| Security Tests | OWASP Top 10 | Security | Pre-release + quarterly |
| Accessibility Tests | WCAG 2.1 AA | QA + Design | Every release |
| Cross-Browser Tests | Chrome, Firefox, Edge, Samsung Internet | QA | Every release |
| Device Tests | Samsung Galaxy A-series, Walton, Xiaomi (entry level Android) | QA | Every release |

### 23.5 Definition of Done (DoD)

A feature is considered complete when ALL of the following are true:

- [ ] All linked acceptance criteria pass in QA
- [ ] Unit test coverage ≥ 80% for new code
- [ ] Integration tests written and passing for new API endpoints
- [ ] Both Bangla and English translations are complete and reviewed
- [ ] Audit logging is implemented for all state-changing operations
- [ ] Error handling follows the Error Handling Standards (§12)
- [ ] Security review completed (no new vulnerabilities introduced)
- [ ] Performance impact assessed — no regression in Lighthouse score or API response time
- [ ] Product Manager sign-off on acceptance criteria
- [ ] Design review completed (UI matches approved designs)
- [ ] Documentation updated (internal API docs, user-facing help)

### 23.6 Document Revision History

| Version | Date | Author | Summary of Changes |
|---|---|---|---|
| 0.1 | June 2026 | Product Management | Initial outline |
| 0.5 | July 2026 | Product Management + Architecture | Full draft with all sections |
| 1.0 | July 2026 | Product Management | Final review — ready for engineering handoff |

---

*This document is proprietary and confidential. It is the authoritative product specification for Farm360 AI MVP development. Any deviations from requirements defined herein must be approved in writing by the Product Manager and documented as a formal change request.*

---

**Farm360 AI** — *Intelligent Farming. Prosperous Bangladesh.*

*© 2026 Farm360 AI. All Rights Reserved.*
