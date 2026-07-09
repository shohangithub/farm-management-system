# Farm360 AI — Complete UI Blueprint

**Document ID:** F360-UXB-2026-001  
**Version:** 1.0  
**Authority:** Chief UX Architect — Farm360 AI  
**Date:** July 2026  
**Governed by:** F360-CONST-2026-001 · UIX v1.0 · PRD v1.0  
**Classification:** Confidential — Design Reference

---

> *"Design is not decoration. In enterprise software, design is the difference between a tool that accelerates work and a tool that obstructs it. Every click that can be removed should be removed. Every decision that can be made for the user should be made for the user."*

---

## Table of Contents

**Part I — Foundation**
1. [Design Language](#1-design-language)
2. [Shell & Navigation](#2-shell--navigation)
3. [Global Component Library](#3-global-component-library)
4. [ERP UX Principles](#4-erp-ux-principles)

**Part II — Authentication**
5. [Login / Onboarding](#5-login--onboarding)
6. [OTP Verification](#6-otp-verification)
7. [Registration Wizard](#7-registration-wizard)

**Part III — Dashboard**
8. [Executive Dashboard](#8-executive-dashboard)
9. [Farm Dashboard](#9-farm-dashboard)

**Part IV — Livestock**
10. [Animal List](#10-animal-list)
11. [Animal Detail](#11-animal-detail)
12. [Register Animal](#12-register-animal)
13. [Animal Timeline](#13-animal-timeline)
14. [Batch List](#14-batch-list)
15. [Batch Detail](#15-batch-detail)

**Part V — Feeding**
16. [Ingredient Catalog](#16-ingredient-catalog)
17. [Feed Formula Builder](#17-feed-formula-builder)
18. [Daily Consumption Log](#18-daily-consumption-log)
19. [FCR Report](#19-fcr-report)

**Part VI — Health**
20. [Vaccination Due List](#20-vaccination-due-list)
21. [Animal Health History](#21-animal-health-history)
22. [Disease Incident](#22-disease-incident)
23. [Mortality Log](#23-mortality-log)
24. [Vaccination Protocol Manager](#24-vaccination-protocol-manager)

**Part VII — Inventory**
25. [Inventory List](#25-inventory-list)
26. [Stock In Form](#26-stock-in-form)
27. [Stock Movement Ledger](#27-stock-movement-ledger)

**Part VIII — Finance**
28. [Financial Entries](#28-financial-entries)
29. [Record Transaction](#29-record-transaction)
30. [Monthly P&L Report](#30-monthly-pl-report)
31. [Animal Cost Ledger](#31-animal-cost-ledger)
32. [Loan Manager](#32-loan-manager)

**Part IX — Settings**
33. [Farm & Shed Management](#33-farm--shed-management)
34. [User Management](#34-user-management)
35. [Subscription & Billing](#35-subscription--billing)
36. [Tenant Settings & Branding](#36-tenant-settings--branding)

**Part X — Global Patterns**
37. [Notifications Panel](#37-notifications-panel)
38. [Command Palette](#38-command-palette)
39. [Screen Relationships Map](#39-screen-relationships-map)
40. [Component Reuse Strategy](#40-component-reuse-strategy)
41. [User Journeys](#41-user-journeys)
42. [Accessibility Rules](#42-accessibility-rules)

---

# PART I — FOUNDATION

## 1. Design Language

### 1.1 Personality

Farm360 AI's UI must feel like the intersection of three world-class products:

```
MICROSOFT DYNAMICS 365  ×  LINEAR  ×  NOTION
─────────────────────────────────────────────
From Dynamics 365:              From Linear:                 From Notion:
  Command bars                    Keyboard-first UX            Inline editing
  Sortable data grids             ⌘K command palette           Collapsible sections
  Entity cards w/ status          Tag-based labels             Hover-revealed actions
  Contextual right panels         Exceptional empty states     Clean page hierarchy
  Ribbon-like action bars         Focused, minimal chrome      Block-based layout
  Breadcrumb entity trails        Dark mode as default         Breadcrumb w/ icons
  Status badge system             Subtle micro-animations      Typography-first design
```

### 1.2 Design Tokens in Use

```
COLOR PALETTE:
  Brand:             hsl(173, 80%, 38%)  #0d9e83  Deep Teal (primary)
  Brand Hover:       hsl(173, 82%, 30%)  #087c67
  Brand Active:      hsl(173, 82%, 24%)  #065c4d
  Surface Base:      #0d1117             (dark) / #ffffff (light)
  Surface Raised:    #161b22             (dark) / #f8fafc (light)
  Surface Overlay:   #1c2128             (dark) / #f1f5f9 (light)
  Border Subtle:     #21262d             (dark) / #e2e8f0 (light)
  Text Primary:      #f0f6fc             (dark) / #0f172a (light)
  Text Secondary:    #8b949e             (dark) / #334155 (light)
  Text Muted:        #484f58             (dark) / #94a3b8 (light)
  Status Success:    #16a34a  / Badge bg: rgba(22,163,74,0.15)
  Status Warning:    #d97706  / Badge bg: rgba(217,119,6,0.15)
  Status Danger:     #dc2626  / Badge bg: rgba(220,38,38,0.15)
  Status Info:       #0ea5e9  / Badge bg: rgba(14,165,233,0.15)
  Status Neutral:    #6b7280  / Badge bg: rgba(107,114,128,0.15)

TYPOGRAPHY:
  Body (Latin):      Inter, 13px/1.5 (body-sm), 14px/1.5 (body), 16px/1.5 (body-lg)
  Body (Bangla):     Noto Sans Bengali (same sizes, auto-substituted)
  Mono:              JetBrains Mono (animal tags, IDs, financial codes)
  Heading 1:         Inter SemiBold 24px
  Heading 2:         Inter SemiBold 18px
  Heading 3:         Inter Medium 16px
  Label:             Inter Medium 12px uppercase 0.05em tracking

SPACING (4px base):
  xs: 4px   sm: 8px   md: 12px   base: 16px
  lg: 20px  xl: 24px  2xl: 32px  3xl: 48px

RADIUS:
  sm: 4px   md: 6px   lg: 8px   xl: 12px   full: 9999px (tags)

SHADOW (dark mode):
  sm: 0 1px 2px rgba(0,0,0,0.4)
  md: 0 4px 16px rgba(0,0,0,0.5)
  lg: 0 8px 32px rgba(0,0,0,0.6)

MOTION:
  Micro: 100ms ease-out    (button press, toggle)
  Short: 150ms ease-out    (hover states, badges)
  Medium: 250ms ease-out   (panel slide, modal open)
  Long: 350ms ease-out     (page transition, drawer)
```

---

## 2. Shell & Navigation

### 2.1 Application Shell Layout

```
┌─────────────────────────────────────────────────────────────────────────┐
│  TOPBAR (48px height)                                                   │
│  [≡ Sidebar toggle] [Logo] [Breadcrumb trail ···] [🔍 Search] [🔔] [👤]│
├───────────────────┬─────────────────────────────────────────────────────┤
│  SIDEBAR          │  CONTENT AREA                                       │
│  (240px expanded) │  ┌─────────────────────────────────────────────┐   │
│  (48px collapsed) │  │  COMMAND BAR (40px)                         │   │
│                   │  │  [Primary CTA] [Secondary] [···] [Filters]  │   │
│  Dashboard        │  └─────────────────────────────────────────────┘   │
│  Livestock        │  ┌─────────────────────────────────────────────┐   │
│  Feeding          │  │  PAGE CONTENT                               │   │
│  Health           │  │  (scrollable)                               │   │
│  Inventory        │  │                                             │   │
│  Finance          │  │                                             │   │
│  ───────          │  └─────────────────────────────────────────────┘   │
│  Settings         │                           │  CONTEXT PANEL (360px) │
│  Help             │                           │  (slides in on demand) │
└───────────────────┴───────────────────────────┴─────────────────────────┘
```

### 2.2 Sidebar Navigation

```
SIDEBAR STRUCTURE:

  [Farm360 Logo] [Collapse ←]
  
  ─── MAIN ───────────────────
  📊 Dashboard          /dashboard
  🐄 Livestock          /livestock
     ├ Animals
     └ Batches
  🌾 Feeding            /feeding
     ├ Ingredients
     ├ Formulas
     └ Consumption
  💉 Health             /health
     ├ Due Vaccinations
     ├ Protocols
     └ Incidents
  📦 Inventory          /inventory
  💰 Finance            /finance
     ├ Entries
     ├ P&L Reports
     └ Loans
  
  ─── MANAGE ─────────────────
  ⚙️  Settings          /settings
  ❓  Help & Docs       /help

  ─── BOTTOM ──────────────────
  [Subscription tier badge: Khamar]
  [User avatar + name + role]

COLLAPSED MODE (48px):
  Icons only, tooltip on hover
  Section labels hidden
  Active item: teal left border indicator (3px)

ACTIVE STATE:
  Left border: 3px solid brand-500
  Background: rgba(13,158,131,0.08)
  Text: brand-400
```

### 2.3 Topbar

```
LEFT:   [Hamburger toggle] [Farm360 wordmark, 20px] │ [Breadcrumb]
CENTER: [Global search input, 320px wide, ⌘K shortcut shown]
RIGHT:  [🌐 Language toggle (EN/বাং)] [🔔 Notifications badge] [User menu]

User Menu Dropdown:
  [Avatar 32px] [Name] [Role badge]  
  ─────────────────────────
  My Profile
  My Sessions
  Security Settings
  ─────────────────────────
  Switch Language (EN / বাং)
  Switch Theme (🌙 Dark / ☀️ Light)
  ─────────────────────────
  Log Out
  Log Out All Devices
```

### 2.4 Command Bar (Page-Level)

```
Appears below topbar, above page content.
Height: 40px. Background: surface-overlay.

LEFT ZONE:  Primary action button(s)
            [+ Register Animal] [+ Add Batch] (context-sensitive)

CENTER:     (empty or page-specific filters — chips row)

RIGHT ZONE: [Filter ▾] [Sort ▾] [Columns ⊞] [View: ☰ List / ⊞ Grid] [Export ↓]

Command bar is STICKY — stays visible while page scrolls.
```

---

## 3. Global Component Library

### 3.1 Status Badges

```
ANIMAL STATUS BADGES (pill shape, 6px radius, 12px text):
  ● Active        → teal bg    (rgba 13,158,131,0.15)  teal text
  ● Sold          → neutral bg  grey text
  ● Quarantined   → amber bg   amber text
  ● Dead          → danger bg  danger text
  ● Transferred   → info bg    info text

SUBSCRIPTION BADGE (sidebar bottom):
  [Bittho]   grey
  [Khamar]   teal
  [Banik]    amber
  [Corp]     purple
```

### 3.2 Data Table Standards

```
Every data table follows:
  → Sticky header row on scroll
  → Sortable columns: ↑↓ icon on hover; ↑ or ↓ when active
  → Row hover: surface-overlay background (100ms transition)
  → Row selection: checkbox column (hidden until hover/select)
  → Row click: opens context panel (right side, 360px)
  → Double-click or [Open ↗] → navigates to detail page
  → Bulk actions bar appears above table when rows selected
  → Empty state replaces table body (not a separate page)
  → Pagination: "Showing 1–25 of 247 animals" + [← Prev] [1][2][3···] [Next →]
  → Page size selector: [25 ▾] (options: 25, 50, 100)
  → Column widths: user-resizable (saved to localStorage)
  → Column visibility: toggleable via [Columns ⊞] in command bar
  → Keyboard: arrow keys navigate rows; Enter opens; Space selects
```

### 3.3 Context Panel (Right Drawer)

```
WIDTH: 360px on desktop, full-screen overlay on mobile
TRIGGER: Single click on any table row or entity card
ANIMATION: slides in from right, 250ms ease-out
CLOSE: × button, Escape key, click outside

STRUCTURE:
  [← Back] [Entity name + Tag]                [Open ↗] [×]
  ──────────────────────────────────────────────
  [Status badge] [Species] [Age]
  
  QUICK STATS (2×2 grid of metric cards)
  [Weight 45kg] [ADG 0.3kg/d] [Age 8mo] [Value ৳85,000]
  
  TABS: Overview | Health | Feed | Finance
  
  QUICK ACTIONS (command bar within panel):
  [Record Weight] [Vaccinate] [Sell] [···]
  
  RECENT ACTIVITY (timeline, last 5 events)
```

### 3.4 Empty States

```
PATTERN: Centered vertically in content area.
  [Illustration — line art, 160px, teal accent]
  [Heading: "No animals registered yet"]
  [Subtext: "Register your first animal to start tracking its lifecycle."]
  [Primary CTA Button]
  [Secondary link: "Learn more →"]

ILLUSTRATIONS: Custom line-art per module:
  Livestock: cow outline
  Feeding: feed bag
  Health: syringe + heart
  Inventory: warehouse shelf
  Finance: ledger book
  Reports: chart outline

NO "No data found" text without an action path.
```

### 3.5 Loading States

```
SKELETON SCREENS (not spinners for content areas):
  → Table rows: shimmer bars at 70% width (alternating 50%, 70%, 60%)
  → Cards: shimmer rectangle blocks
  → Charts: shimmering placeholder at chart dimensions
  → Stat widgets: 2 shimmer lines

SHIMMER ANIMATION:
  Background gradient sweeping left→right, 1.5s infinite
  Color: surface-raised → border-subtle → surface-raised

INLINE LOADING (for button actions):
  Button text replaced with "Saving…" + spinner (16px)
  Button disabled during operation
  Success: button briefly shows "✓ Saved" (1.5s) then reverts

PAGE-LEVEL LOADING:
  Progress bar at top of content area (Linear-style)
  Thin 2px line, brand-500 color, animates from left to right
```

### 3.6 Error States

```
INLINE FIELD ERRORS:
  Red underline on input
  Error text below field: [⚠ Error message here] (danger-500, 12px)
  
FORM-LEVEL ERROR BANNER:
  [⚠ Please fix 3 errors before saving] (amber background strip)

TOAST NOTIFICATIONS (bottom-right corner):
  Success: ✓ green left border, "Animal registered successfully"
  Error:   ✗ red left border, "Failed to save. Please try again."
  Warning: ⚠ amber left border
  Info:    ℹ teal left border
  Duration: 4 seconds. Manual dismiss ×. Stack up to 3.

PAGE-LEVEL ERRORS:
  500 Server Error: Full-page centered illustration + "Something went wrong" + [Retry] [Report]
  403 Forbidden: Illustration + "You don't have permission to view this"
  404 Not Found: Illustration + "This page doesn't exist" + [Go to Dashboard]
  Offline: Top banner: "⚠ You are offline. Changes will sync when reconnected."
```

---

## 4. ERP UX Principles

```
PRINCIPLE 1: COMMAND BAR IS ALWAYS ACCESSIBLE
  Primary action always top-left of command bar. Never buried in menus.
  Equivalent of Dynamics 365 ribbon — always visible for key workflows.

PRINCIPLE 2: CONTEXT PANEL BEFORE NAVIGATION
  Single click → context panel (quick view + actions)
  Double click → full page navigation
  This reduces navigation depth for power users.

PRINCIPLE 3: KEYBOARD SUPREMACY
  Every action reachable by keyboard. Tab order logical.
  ⌘K opens command palette (all actions, all navigation).
  Every table navigable with arrow keys.
  Power users should never need a mouse.

PRINCIPLE 4: PROGRESSIVE DISCLOSURE
  Show summary → reveal detail on demand.
  Dashboard shows KPIs → drill into list → drill into entity.
  Don't load detail that wasn't requested.

PRINCIPLE 5: BULK OPERATIONS
  Every list supports: select all on page, select across pages, bulk action.
  Bulk actions: export, tag, assign, delete (with confirmation).

PRINCIPLE 6: INLINE EDITING WHERE SAFE
  Non-financial fields: click-to-edit inline (Notion pattern).
  Financial fields and status transitions: require explicit form.
  Inline save is auto (300ms debounce) with undo toast.

PRINCIPLE 7: NEVER LOSE USER DATA
  Unsaved form state → warn on navigation ("You have unsaved changes")
  Auto-save drafts for complex forms (Registration Wizard).
  Browser refresh safe — draft in IndexedDB.

PRINCIPLE 8: DENSITY MATTERS
  Default: comfortable density (Dynamics 365 style)
  Option: compact density (table row height 32px vs 40px)
  Power users set their preference; saved per-user.
```

---

# PART II — AUTHENTICATION

## 5. Login / Onboarding

### Purpose
Entry point for all users. Minimal friction. Phone-first. Feels premium, not generic.

### Wireframe Description
```
[Full viewport split layout — dark mode only for auth pages]

LEFT PANEL (55%): Visual/Brand
  Background: gradient from brand-900 to surface-base (#0d1117)
  Large Farm360 logo (120px)
  Tagline: "Your farm. Managed intelligently."
  Animated subtle particle/grain texture in background
  3 rotating testimonial quotes from Bangladeshi farmers (in Bangla)
  Bottom: "Trusted by 1,200+ farms across Bangladesh" + farm count ticker

RIGHT PANEL (45%): Auth Form
  Background: surface-raised (#161b22)
  Top-right: [EN | বাং] language toggle
  
  CENTER (vertical + horizontal):
    [Farm360 logo — small, 32px]
    [H1: "Welcome back" / "আবার স্বাগতম"]
    [Subtext: "Enter your phone number to continue"]
    
    [Phone input field]
    [+880 prefix locked] [XXXXXXXXXX editable]
    
    [Continue →] (Primary button, full width)
    
    ─── OR ───
    
    [Sign in with Microsoft] (future — disabled in MVP, shown as "Coming soon")
    
    [New to Farm360? Start free →]
    
  Bottom: [Privacy Policy] [Terms of Service] [Help]
```

### Form Fields
```
Phone Number Input:
  - Prefix: "+880" (locked, teal, 40px left segment)
  - Input: numeric keyboard, 10 digits, auto-format
  - Placeholder: "01XXXXXXXXX"
  - Validation: real-time on blur — shows green ✓ or red error
  - Error: "Please enter a valid Bangladesh phone number"
```

### States
```
LOADING: Button shows spinner + "Sending OTP…"
ERROR:   "Account not found" banner (red), + "Register now →" link
LOCKED:  "Too many attempts. Try again in 28 minutes." + countdown timer
```

### Keyboard Shortcuts
```
Enter: Submit phone number (focus on button via Tab)
Tab: Move through form
```

### Permissions
None — public page.

### Responsive Behaviour
```
Mobile (<768px): Left panel hidden. Right panel = full screen. Logo centered top.
Tablet (768–1024px): Left panel 40%, right panel 60%.
Desktop (>1024px): 55% / 45% split as described.
```

---

## 6. OTP Verification

### Purpose
OTP confirmation step. Minimal friction. Clear retry mechanism. Accessible.

### Wireframe Description
```
[Same split layout as login — right panel only changes]

RIGHT PANEL:
  [← Back to phone] (ghost button, top left)
  
  [🔒 Icon — lock with checkmark, 48px, teal]
  [H1: "Enter verification code"]
  [Subtext: "We sent a 6-digit code to +880171XXXX05"]
  
  OTP INPUT FIELD (6 boxes, 48×56px each, spaced 8px):
  [_][_][_][_][_][_]
  Auto-advance on digit entry.
  Auto-submit on 6th digit.
  Paste: auto-split across boxes.
  
  [Verify →] (primary, full width — enabled when 6 digits filled)
  
  [Resend code in 01:30] (countdown timer)
  → After countdown: [Resend OTP] becomes active
  
  Attempt counter: "2 of 3 attempts remaining" (warning amber, appears after 1st failure)
```

### Loading State
```
On auto-submit: All 6 boxes get teal border pulse animation.
"Verifying…" text appears below boxes.
Button disabled.
```

### Error State
```
Wrong OTP:
  All 6 boxes shake animation (100ms, 3px horizontal)
  Boxes clear + red border
  "Incorrect code. 2 attempts remaining."

Expired OTP:
  "Code expired. Please request a new one." + [Resend] active immediately.

Locked:
  "Too many failed attempts. Try again in 28:30" + countdown timer (MM:SS)
```

### Keyboard Shortcuts
```
0-9:    Enter digit (auto-advance)
Backspace: Delete and go back one box
Ctrl+V: Paste 6-digit code (auto-split)
```

---

## 7. Registration Wizard

### Purpose
3-step guided onboarding for new farm owners. Collects: personal info → farm info → subscription selection. Saves draft at each step.

### Wireframe Description
```
RIGHT PANEL: 
  [Progress stepper: ① Personal → ② Your Farm → ③ Done] (top of panel)
  
STEP 1 — Personal Info:
  [H2: "Tell us about yourself"]
  Full name (required)
  Email (optional, with "Optional" chip)
  Language preference: [বাংলা ● | English ○]
  [Continue →]

STEP 2 — Farm Setup:
  [H2: "Set up your first farm"]
  Organization / Farm name (required)
  Division (dropdown: Dhaka, Chattogram, Khulna…)
  District (dropdown — auto-filtered by division)
  Primary livestock type: [🐄 Beef ○] [🥛 Dairy ○] [🐐 Goat ○] [🐑 Sheep ○]
  [Continue →] [← Back]

STEP 3 — Success:
  [✅ Large animated checkmark, teal]
  [H1: "You're all set!"]
  [Subtext: "Your 14-day free trial has started."]
  [Go to Dashboard →] (primary, full width)
  [Watch 2-min tour] (secondary)
```

### Validation
```
Real-time: field-level on blur
Submit: all fields validated; scroll to first error
Draft: auto-saved to sessionStorage on field blur
```

---

# PART III — DASHBOARD

## 8. Executive Dashboard

### Purpose
The farm owner's command center. Provides instant operational awareness across all KPIs. Data-dense but scannable. Answers: "Is my farm healthy right now?"

### Breadcrumb
`Dashboard`

### Widgets (KPI Row — top, always visible)

```
KPI CARDS ROW (5 cards, equal width, horizontal scroll on mobile):
┌─────────────────┐┌─────────────────┐┌─────────────────┐┌─────────────────┐┌─────────────────┐
│ Total Animals   ││ Active Animals  ││ Monthly Revenue ││ Feed Stock (kg) ││ Health Alerts   │
│ [248]           ││ [231] ↑         ││ [৳4,82,000] ↑   ││ [1,240 kg] ⚠️   ││ [3] overdue     │
│ +12 this month  ││ 7 quarantined   ││ +18% vs last mo ││ Est. 8 days     ││ 2 vaccinations  │
└─────────────────┘└─────────────────┘└─────────────────┘└─────────────────┘└─────────────────┘

KPI Card Anatomy:
  - Label (12px muted, uppercase)
  - Value (24px semibold, primary)
  - Trend chip (↑/↓ percentage, green/red background)
  - Sub-text (13px secondary)
  - Left border: 3px colored by status
  - Hover: slight elevation shadow
  - Click: navigates to relevant module
```

### Dashboard Widgets Grid (below KPI row)

```
3-COLUMN GRID (flex, collapses to 1 on mobile):

ROW 1:
┌───────────────────────────────────┐ ┌────────────────────┐ ┌────────────────────┐
│ Monthly Financial Overview (2 col)│ │ Stock Alerts        │ │ Vaccination Due    │
│ [Line chart: Revenue vs Expenses] │ │ ─────────────────── │ │ ─────────────────── │
│ [This month vs last month]        │ │ ⚠️ Rice Bran low    │ │ 🔴 5 Animals TODAY │
│ [Net: ৳1,24,000 profit]           │ │ ⚠️ Urea low          │ │ 🟡 12 this week    │
│                                   │ │ [View Inventory →]  │ │ [Go to Health →]   │
└───────────────────────────────────┘ └────────────────────┘ └────────────────────┘

ROW 2:
┌────────────────────┐ ┌────────────────────┐ ┌───────────────────────────────────┐
│ Recent Animals     │ │ Feed Consumption   │ │ Herd Breakdown (2 col)            │
│ ─────────────────  │ │ ─────────────────  │ │ [Donut chart: by species]         │
│ Cow BD-0241 Active │ │ Today: 840 kg      │ │ Beef 45%  Dairy 32%  Goat 23%    │
│ Goat BD-0242 Active│ │ This week: 5,880kg │ │                                   │
│ [View All →]       │ │ [7-day bar chart]  │ │                                   │
└────────────────────┘ └────────────────────┘ └───────────────────────────────────┘
```

### Cards Detail
```
Each widget card:
  - Surface-raised background
  - 24px padding
  - 8px border-radius
  - Subtle 1px border (border-subtle)
  - [H3 title] top-left + [action link] top-right
  - Loading: skeleton shimmer
  - Error: "Unable to load" + [Retry] inline
  - Drag-to-reorder: ⠿ handle (Owner/FarmManager only — Phase 2)
```

### Filters
```
[Farm selector dropdown] — if multi-farm: filter entire dashboard by farm
[Date range: This Month ▾] — options: Today, This Week, This Month, This Quarter, Custom
Applied to: all financial widgets only
```

### Empty State
```
New account (zero data):
  [Welcome illustration]
  "Start by registering your first animal"
  [Register Animal →] [Set up farm →]
  Checklist: ☐ Add a farm  ☐ Register first animal  ☐ Set up feeding schedule
```

### Loading State
```
Skeleton shimmer on all 5 KPI cards + widget grid simultaneously.
Progressive: KPI cards load first (fast queries), charts load second.
```

### Permissions
```
All roles: Can view dashboard
Viewers: Cannot see financial widgets (finance:read required)
Workers: Cannot see revenue/finance metrics
```

### Keyboard Shortcuts
```
D:     Go to Dashboard (global)
F1–F5: Focus on KPI card 1–5
```

### Responsive Behaviour
```
Desktop (>1280px): 3-column grid
Tablet (768–1280px): 2-column grid
Mobile (<768px): 1-column stack; KPI row horizontal scroll
```

---

## 9. Farm Dashboard

### Purpose
Shed/pen level drill-down. Reached by clicking farm name in dashboard or sidebar.

### Breadcrumb
`Dashboard > [Farm Name]`

### Widgets
```
FARM OVERVIEW HEADER:
  Farm photo (if uploaded) | Farm name | Location | Total sheds/pens | Status

PEN OCCUPANCY MAP:
  Visual grid of pens — each pen is a colored rectangle
  Color: green (occupied) / grey (empty) / amber (near capacity)
  Click pen → context panel showing animals in that pen

SHED BREAKDOWN TABLE:
  Shed name | Pen count | Total animals | Avg weight | Next feeding | Actions
```

---

# PART IV — LIVESTOCK

## 10. Animal List

### Purpose
Master list of all animals in the tenant. Primary daily-use page for farm managers and workers. Power users will visit this 10+ times per day.

### Breadcrumb
`Livestock > Animals`

### Command Bar
```
[+ Register Animal] [+ Import CSV]         [Search…] [Filter ▾] [Sort ▾] [Columns ⊞] [Export ↓]
```

### Table Columns (default visible)
```
☐  |  Tag ID (mono)  |  Name  |  Species  |  Breed  |  Sex  |  Age  |  Weight (kg)  |  ADG  |  Farm/Shed  |  Status  |  Actions
```

```
Column Widths (default, resizable):
  ☐: 32px (fixed)
  Tag ID: 100px (JetBrains Mono font, teal colored, clickable)
  Name: 120px
  Species: 80px (icon + text: 🐄 Beef)
  Breed: 120px
  Sex: 50px (♂/♀)
  Age: 70px (8 mo / 2 yr 3 mo)
  Weight: 80px (right-aligned)
  ADG: 70px (right-aligned, green if positive, red if below target)
  Farm/Shed: 120px
  Status: 90px (status badge)
  Actions: 80px (hover-revealed: [Weight] [···])
```

### Filters
```
FILTER CHIP BAR (appears below command bar when filters active):
  [Species: Cattle × ] [Status: Active × ] [Shed: Shed A × ] [Clear all filters]

FILTER PANEL (opens from [Filter ▾]):
  Accordion sections:
  ▶ Status (checkboxes: Active, Sold, Quarantined, Dead, Transferred)
  ▶ Species (checkboxes: Beef, Dairy, Goat, Sheep)
  ▶ Sex (radio: All, Male, Female)
  ▶ Farm / Shed (hierarchical checkbox tree)
  ▶ Age Range (slider: 0–36 months)
  ▶ Weight Range (slider: 0–800 kg)
  ▶ Acquisition Date (date range picker)
  [Apply] [Clear]
```

### Search
```
Scope: TagId, Name, BreedName (full-text search)
Debounce: 300ms
Result: Table filters live
Placeholder: "Search by tag, name, or breed…"
Keyboard: ⌘F or / focuses search field
```

### Context Panel (on row click)
```
ANIMAL QUICK VIEW:
  [Tag: BD-0241 🐄] Active
  [Photo if available — 160px height, rounded]
  
  Metric chips row:
  [Weight: 285 kg] [ADG: 0.31 kg/d] [Age: 14 mo] [Value: ৳95,000 est.]
  
  TABS: Overview | Health | Feed | Finance
  
  Overview tab:
    Breed: Sahiwal × Local
    Farm: Green Valley Farm
    Shed: Shed A, Pen 3
    Acquisition: 01/03/2026 (৳45,000)
    Status: Active since 01/03/2026
    
  QUICK ACTIONS:
  [Record Weight] [Vaccinate] [Log Feed] [Sell] [Transfer] [···]
  
  [Open full profile ↗] (bottom of panel)
```

### Bulk Actions
```
When rows selected:
  [Export selected] [Print tags] [Batch vaccinate] [Transfer to shed] [Delete] (danger)
  "[N] animals selected — Select all 247 on this search →"
```

### Empty State
```
No animals:
  [Cow line-art illustration, 140px]
  "No animals registered yet"
  "Start tracking your livestock lifecycle"
  [+ Register First Animal] (primary)
  
Filtered with no results:
  [Magnifying glass illustration]
  "No animals match your filters"
  [Clear all filters] (secondary)
```

### Loading State
```
8 skeleton rows (40px height each)
Column headers remain visible
```

### Error State
```
Failed to load:
  [⚠ Could not load animal list]
  [Retry]
```

### Permissions
```
All roles: View (with farm scope)
Worker: Cannot see Actions column for Sell/Transfer
Viewer: No Actions column at all
```

### Keyboard Shortcuts
```
/        : Focus search
N        : New animal (Register)
↑↓       : Navigate table rows
Enter    : Open context panel
↵↵       : Open full detail page (double Enter)
Space    : Select/deselect row
⌘A       : Select all on page
Escape   : Close context panel / clear selection
E        : Export filtered results
```

### Responsive Behaviour
```
Desktop: Full table (all columns)
Tablet: Collapse to: Tag | Name | Status | Weight | Actions
Mobile: Card view (each animal = stacked card with key metrics)
```

---

## 11. Animal Detail

### Purpose
The complete profile of one animal. Equivalent of an entity page in Dynamics 365. Acts as the system of record for an animal's entire lifecycle.

### Breadcrumb
`Livestock > Animals > BD-0241`

### Page Layout
```
┌─────────────────────────────────────────────────────────────────────────────┐
│  ENTITY HEADER                                                              │
│  [Animal photo 80px] [BD-0241] [Status badge: Active] [Species + Breed]    │
│  Farm: Green Valley | Shed: A | Pen: 3 | Owner: Rahman's Farm               │
│  ─────────────────────────────────────────────────────────────────────────  │
│  COMMAND BAR:                                                               │
│  [Record Weight] [Vaccinate] [Log Feed] [Sell Animal] [Transfer] [···▾]    │
│  (···) expands: Quarantine | Record Death | Add Photo | View Tag | Delete   │
├─────────────────┬───────────────────────────────────────────────────────────┤
│  LEFT COL (35%) │  RIGHT COL (65%)                                         │
│                 │                                                           │
│  QUICK STATS    │  TAB BAR: Overview | Weight | Health | Feed | Finance     │
│  ┌───────────┐  │                                                           │
│  │ Weight    │  │  OVERVIEW TAB:                                            │
│  │ 285 kg    │  │  [2-col info grid]                                        │
│  └───────────┘  │  Acquisition Date | Acquisition Price                     │
│  ┌───────────┐  │  Date of Birth    | Estimated Age                         │
│  │ ADG       │  │  Species          | Breed                                 │
│  │ 0.31 kg/d │  │  Sex              | Color                                 │
│  └───────────┘  │  Tag Type         | Tag ID (mono)                         │
│  ┌───────────┐  │  Source           | Acquisition Note                      │
│  │ Age       │  │                                                           │
│  │ 14 mo     │  │  WEIGHT TAB:                                              │
│  └───────────┘  │  [Line chart: weight over time]                           │
│  ┌───────────┐  │  [Table: date | weight | recorded by | note]              │
│  │ Est Value │  │  [+ Record Weight] button at top                          │
│  │ ৳95,000  │  │                                                           │
│  └───────────┘  │  HEALTH TAB: (see §21)                                    │
│                 │  FEED TAB: consumption history                            │
│  LOCATION       │  FINANCE TAB: cost ledger (see §31)                       │
│  [Shed map]     │                                                           │
│                 │                                                           │
│  PHOTOS         │                                                           │
│  [Thumbnail ×3] │                                                           │
│  [+ Add Photo]  │                                                           │
└─────────────────┴───────────────────────────────────────────────────────────┘
```

### Inline Editing
```
Non-sensitive fields: click value → inline input appears → auto-save on blur
Fields: Name, Color, Notes, Breed (with confirmation)
Sensitive fields (status, price): require explicit form/action
Financial fields: require explicit transaction record
```

### Actions
```
[Record Weight] → slide-up modal (date, weight kg, note)
[Vaccinate]     → links to Health module log form
[Sell Animal]   → multi-step modal: Buyer, Date, Price, Weight at sale
[Transfer]      → modal: target Farm, Shed, Pen, Date, Reason
[Quarantine]    → modal: Reason, Start Date, Expected Duration
[Record Death]  → modal: Date, Cause, Weight at death (irreversible — requires confirm)
[Add Photo]     → drag-and-drop uploader, max 5MB/image, max 10 photos
[Delete]        → danger: "Type BD-0241 to confirm deletion" dialog
```

### Permissions
```
Worker:     View + Record Weight + Log Feed. Cannot: Sell, Transfer, Delete.
FarmMgr:    All except Delete.
Owner:      Full access.
Vet:        View all + record health events. Cannot modify financial data.
Accountant: View only. Cannot modify any field.
Viewer:     View only, no financial data.
```

### Keyboard Shortcuts
```
W: Record weight
V: Vaccinate
S: Sell animal
T: Transfer
←: Navigate to previous animal (in current filter context)
→: Navigate to next animal
⌘←: Back to animal list
```

---

## 12. Register Animal

### Purpose
Data entry form for new animal. Must be fast for workers registering multiple animals.

### Breadcrumb
`Livestock > Animals > Register`

### Form Layout
```
TWO-COLUMN FORM (800px max-width, centered):

SECTION 1: Identity
  Tag ID *     [BD-] + [input, mono font]   [Tag Type: Manual | Ear Tag | RFID]
  Name         [Optional text input]
  Species *    [Beef Cattle ● | Dairy Cattle ○ | Goat ○ | Sheep ○]  (radio chips)
  Breed *      [Searchable dropdown: Sahiwal, Local, HF Cross, etc.]
  Sex *        [Male ● | Female ○]

SECTION 2: Acquisition
  Acquisition Date *    [Date picker — DD/MM/YYYY]
  Acquisition Type *    [Purchased ● | Born on Farm ○]
  Acquisition Price     [৳ number input] (optional if Born on Farm)
  Supplier / Source     [Text or dropdown from saved suppliers]

SECTION 3: Location
  Farm *    [Dropdown — user's assigned farms]
  Shed *    [Dropdown — auto-filtered by farm]
  Pen       [Dropdown — auto-filtered by shed]

SECTION 4: Physical (at acquisition)
  Weight at Acquisition    [kg input]
  Body Condition Score     [1.0 – 5.0 slider with labels]
  Date of Birth            [Date picker — optional]
  Color / Markings         [Text, optional]

SECTION 5: Notes
  Notes    [Textarea, 4 rows, optional]

FORM FOOTER:
  [Cancel] [Save & Register Another] [Register Animal →] (primary)
```

### Validation
```
TagId: Real-time uniqueness check (async API call, 500ms debounce)
  → "✓ Tag ID available" (teal, 500ms after typing stops)
  → "✗ This tag ID is already in use" (red)
Required fields: red border + error text on submit
Date validations: acquisition date cannot be in future
```

### Empty/Loading State
```
Breed dropdown loading: skeleton option list
Farm/Shed dropdowns: "Loading…" placeholder with spinner
```

### Keyboard Shortcuts
```
Tab:    Move to next field
Enter:  Submit (when on last field or button)
⌘S:     Save (equivalent to Register button)
Escape: Cancel (with unsaved changes warning)
```

---

## 13. Animal Timeline

### Purpose
Chronological event log for a single animal. Like a medical record + life history.

### Breadcrumb
`Livestock > Animals > BD-0241 > Timeline`

### Page Layout
```
[← Back to Animal Profile]

FILTER BAR: [All ●] [Health ○] [Weight ○] [Feed ○] [Finance ○] [Transfers ○]

TIMELINE (vertical, left-edge icons):

  [🟢 2026-07-07 10:30 AM]  Weight Recorded
    Weight: 285 kg (+5 kg from last, +1.8%) — Recorded by: Rahman
    
  [💉 2026-07-02 09:00 AM]  Vaccinated: FMD Dose 2
    Protocol: Standard FMD · Recorded by: Dr. Karim
    
  [🌾 2026-07-01 08:00 AM]  Feed Consumption
    Green Grass Formula — 12.5 kg
    
  [🔀 2026-06-15 02:00 PM]  Transferred
    From: Shed A, Pen 2 → To: Shed B, Pen 1 · By: Manager Rahim
    
  [📷 2026-06-01]  Photo Added
    [thumbnail 60×60]

EVENT ICON LEGEND (sticky at top of timeline):
  🟢 Weight  💉 Health  🌾 Feed  💰 Finance  🔀 Transfer  📷 Photo
```

### Filters
```
Event type filter chips (horizontally scrollable)
Date range: [Last 30 days ▾]
```

### Empty State
```
[Timeline illustration]
"No events recorded yet"
"Record a weight or health event to begin tracking"
```

---

## 14. Batch List

### Purpose
View and manage animal batches. A batch groups animals for collective feeding, health, and reporting.

### Breadcrumb
`Livestock > Batches`

### Table Columns
```
Batch Name | Species | Animal Count | Start Date | Status | Avg Weight | Avg ADG | FCR | Actions
```

### Cards (alternative Grid view)
```
BATCH CARD:
  [Batch name — H3]
  [Species tag] [Status badge]
  ─────────────────────────
  Animals: 24
  Avg Weight: 185 kg
  Target Weight: 300 kg
  Progress: [████████░░░░] 62%
  Started: 01/03/2026
  ─────────────────────────
  [View Details] [Log Feed]
```

---

## 15. Batch Detail

### Purpose
Deep view into a batch — its animals, feeding plan, health status, and P&L projection.

### Breadcrumb
`Livestock > Batches > [Batch Name]`

### Tabs
```
Overview | Animals | Feeding | Health | P&L Projection
```

### P&L Projection Widget
```
BATCH P&L CARD:
  Total Feed Cost: ৳1,24,000
  Total Medicine: ৳8,200
  Total Acquisition: ৳3,20,000
  ─────────────────────────
  Total Cost: ৳4,52,200
  
  At target weight (300 kg × 24 animals × ৳280/kg):
  Projected Revenue: ৳20,16,000
  
  Projected Profit: ৳15,63,800 (346% ROI)
  Break-even at: 215 kg avg weight
```

---

# PART V — FEEDING

## 16. Ingredient Catalog

### Purpose
Master list of feed ingredients with nutritional profiles. Reference data for formula building.

### Breadcrumb
`Feeding > Ingredients`

### Table Columns
```
Ingredient Name | Category | DM% | CP% | ME (Mcal) | Price/kg | Local/Imported | Actions
```

### Filter Panel
```
Category: [Roughage ☐] [Concentrate ☐] [Mineral ☐] [Supplement ☐]
Source: [Local ☐] [Imported ☐]
Price range: [₳ Min] — [₳ Max]
```

### Context Panel
```
INGREDIENT DETAIL:
  [Ingredient name — H2]
  [Category badge]
  
  NUTRITIONAL PROFILE:
  Dry Matter: 88%
  Crude Protein: 18%
  Crude Fibre: 12%
  Total Digestible Nutrients: 65%
  Metabolizable Energy: 2.4 Mcal/kg
  
  PRICING:
  Current price: ৳28/kg
  Last updated: 05/07/2026
  Supplier: Dhaka Agro Suppliers
  
  USAGE:
  Used in 8 formulas
  Monthly consumption: 1,240 kg
  
  [Edit Ingredient] [View in Formulas]
```

---

## 17. Feed Formula Builder

### Purpose
Design and manage custom feed formulas with nutritional balance checking. Like a spreadsheet but purpose-built.

### Breadcrumb
`Feeding > Formulas > [Formula Name]`

### Page Layout
```
TOP: [Formula name — inline editable H1] [Status: Draft | Active badge] [Save] [···]

┌────────────────────────────────────────┬─────────────────────────────────────┐
│  INGREDIENTS LIST (left, 55%)          │  NUTRITIONAL SUMMARY (right, 45%)   │
│                                        │                                     │
│  [+ Add Ingredient]                    │  TARGET PROFILE for: [Species ▾]   │
│                                        │  ─────────────────────────────────  │
│  Ingredient      | kg | % | Remove    │  Dry Matter:        88% [███████░░] │
│  ─────────────── ├────┼───┼─────────  │  Crude Protein:     18% [██████░░░] │
│  Rice Bran       │ 30 │35%│    ×      │  Crude Fibre:       12% [████░░░░░] │
│  Wheat Bran      │ 20 │23%│    ×      │  ME (Mcal/kg):     2.4 [████████░] │
│  Soybean Meal    │ 15 │18%│    ×      │                                     │
│  Green Grass     │ 15 │18%│    ×      │  BALANCE STATUS:                    │
│  Mineral Mix     │  5 │ 6%│    ×      │  [✓ Protein: Within target range]  │
│  ─────────────── ├────┼───┤          │  [⚠ Energy: 4% below target]       │
│  TOTAL           │ 85 │100%│          │  [✓ Fibre: Within range]           │
│                                        │                                     │
│  COST:                                 │  COST PER KG: ৳22.40               │
│  ৳1,904 per batch (85kg)              │  COST PER ANIMAL/DAY: ৳312          │
│  ৳22.40 per kg                        │                                     │
│                                        │  [🔄 Auto-optimize] (Phase 2 AI)   │
└────────────────────────────────────────┴─────────────────────────────────────┘

FORMULA SETTINGS (bottom section):
  Target species: [Beef Cattle ▾]
  Daily ration per animal: [12] kg
  Feeding frequency: [2×/day ▾]
  Active from: [Date picker]
  Notes: [textarea]
```

### Validation
```
Minimum 2 ingredients (domain rule — error on save if <2)
Total must equal 100% (auto-calculated; error if ≠ 100%)
```

### Empty State (new formula)
```
[Ingredient bag illustration]
"Add your first ingredient"
[+ Add Ingredient] button prominent
"Tip: Start with roughage, then add concentrates"
```

---

## 18. Daily Consumption Log

### Purpose
Log actual feed given to animals or sheds. Fast data entry for workers.

### Breadcrumb
`Feeding > Consumption`

### Log Form
```
DATE: [Today — editable date picker]
FARM: [Dropdown]
SHED / PEN: [Dropdown]
FORMULA: [Dropdown — active formulas for this farm]
QUANTITY: [kg input, large, numeric]
NUMBER OF ANIMALS: [auto-filled from shed, editable]
RECORDED BY: [auto-filled: current user]
NOTES: [optional, 2 rows]

[+ Add Another Shed] (for logging multiple sheds in one session)
[Log Consumption →] (primary)
```

### Consumption History Table
```
Date | Shed | Formula | Qty (kg) | Animals | kg/head | Recorded By | Actions
(read-only; Edit allowed within 24 hours by FarmManager+)
```

---

## 19. FCR Report

### Purpose
Feed Conversion Ratio analysis — the core productivity metric.

### Breadcrumb
`Feeding > FCR Report`

### Filters
```
[Batch ▾] [Date Range ▾] [Species ▾] [Farm ▾]
```

### Widgets
```
FCR SUMMARY CARDS:
  [Current FCR: 6.2] [Target FCR: 5.5] [Status: ⚠ Above Target] [Industry Avg: 6.8]

FCR TREND CHART: Line chart — FCR over time (weekly data points)
FCR BY BATCH TABLE: Batch | Animals | Feed Consumed | Weight Gained | FCR | Status
```

---

# PART VI — HEALTH

## 20. Vaccination Due List

### Purpose
Action-oriented list of animals requiring vaccination. Primary health management page.

### Breadcrumb
`Health > Due Vaccinations`

### Command Bar
```
[+ Record Vaccination] [+ Create Protocol] [📤 Export Due List]
```

### Table Columns
```
Urgency | Tag ID | Name | Farm/Shed | Vaccine | Protocol | Due Date | Days Overdue | Actions
```

### Urgency Indicators
```
🔴 Overdue (past due date) — danger row highlight
🟡 Due Today
🟠 Due This Week
🟢 Due This Month
```

### Bulk Action
```
Select multiple → [Record Bulk Vaccination]
Batch vaccination modal:
  Vaccine product, batch number, administered date, vet/admin name
  [Confirm for {N} animals]
```

### Empty State
```
[Syringe + heart illustration]
"All vaccinations are up to date!"
"Great work keeping your herd healthy."
[View completed vaccinations →]
```

---

## 21. Animal Health History

### Purpose
Full health record for one animal (accessed from Animal Detail > Health tab).

### Layout
```
HEALTH SUMMARY CARDS:
  [Last Vaccination: FMD — 5 days ago]
  [Active Treatments: 0]
  [Disease Incidents: 1 (resolved)]
  [Vet Visits: 3]

HEALTH TIMELINE:
  Same component as §13 but health events only.
  
VACCINATIONS TABLE:
  Date | Vaccine | Protocol | Dose | Batch No | Administered By | Next Due

TREATMENTS TABLE:
  Date | Diagnosis | Medication | Dose | Duration | Vet | Status | Cost

VET VISITS:
  Date | Veterinarian | Purpose | Diagnosis | Prescription | Follow-up Date
```

---

## 22. Disease Incident

### Purpose
Record and track disease outbreaks affecting multiple animals.

### Breadcrumb
`Health > Disease Incidents`

### Incident Form
```
SECTION 1: Incident Details
  Incident Name / Title *
  Disease / Condition * [searchable dropdown or free-text]
  Severity * [Mild ○ | Moderate ○ | Severe ○ | Critical ○]
  Start Date *
  Farm *
  
SECTION 2: Affected Animals
  [Animal multi-select with search]
  Shows: [BD-0241 ×] [BD-0242 ×] [+ Add more]
  OR: [Select all animals in Shed A]

SECTION 3: Response
  Quarantine required: [Yes ○ / No ○]
  Quarantine Shed (if yes): [dropdown]
  Treatment Protocol: [textarea]
  Reporting vet: [dropdown]
  
SECTION 4: Resolution
  Status: [Active | Under Treatment | Resolved]
  Resolved Date: [date picker, shows when Resolved selected]
  Outcome notes: [textarea]
```

### Incident List Table
```
ID | Condition | Severity | Affected | Farm | Start Date | Status | Actions
```

---

## 23. Mortality Log

### Purpose
Record animal deaths with cause and weight. Sensitive — requires confirmation.

### Breadcrumb
`Health > Mortality`

### Record Death Form (Modal)
```
[⚠ This action is irreversible. The animal will be marked as Dead.]

Animal: BD-0241 (auto-filled if accessed from animal profile)
Date of Death *: [date picker — cannot be future]
Cause of Death *: [dropdown: Disease | Accident | Natural | Unknown | Other]
  If Other: [describe text input]
Weight at Death: [kg, optional]
Approximate Sale Value of Carcass: [৳, optional]
Witness / Recorded by: [auto-filled, editable]
Veterinarian (if disease): [dropdown]
Notes: [textarea]

[Cancel] [Confirm Death — Record] (danger button, requires double-confirm)
```

---

## 24. Vaccination Protocol Manager

### Purpose
Create and manage vaccination schedules (multi-dose, age-based).

### Breadcrumb
`Health > Protocols`

### Protocol Detail Layout
```
PROTOCOL HEADER:
  [Protocol name — inline editable H1] [Species badge] [Status: Active/Draft]
  
SCHEDULE BUILDER TABLE:
  Dose # | Vaccine | Timing (e.g., At 2 months) | Repeat Every | Notes | Remove

[+ Add Dose] button below table

ASSIGNMENT SECTION:
  Applied to: [Individual animals ●] [Entire batch ○] [All new animals ○]
  [Assign to Animals] → opens animal multi-select
  
  Currently assigned to: [24 animals] [View list]
```

---

# PART VII — INVENTORY

## 25. Inventory List

### Purpose
Real-time view of all stock across the farm. Workers check this before feeding/treatment.

### Breadcrumb
`Inventory`

### Command Bar
```
[+ Record Stock In] [+ Add Item]                  [Search…] [Filter ▾] [Export ↓]
```

### Table Columns
```
Item Name | Category | Unit | Current Stock | Reorder Level | Status | Last Updated | Value | Actions
```

### Status Badges
```
● Sufficient   → teal
● Low Stock    → amber (current ≤ reorder level)
● Out of Stock → danger (current = 0)
● Excess       → info (current > 3× reorder)
```

### Inventory KPI Row
```
[Total SKUs: 34] [Total Value: ৳2,14,000] [Low Stock Items: 4] [Out of Stock: 1]
```

### Filter Panel
```
Category: Feed | Medicine | Chemical | Equipment | Other
Status: All | Low Stock | Out of Stock | Sufficient
Supplier: [multi-select]
```

### Context Panel
```
ITEM DETAIL:
  [Item name — H2]
  [Category] [Unit]
  
  STOCK LEVELS:
  Current: 840 kg
  Reorder at: 200 kg  [Edit]
  Max Capacity: 2,000 kg
  Progress bar: [████████████░░░░░░░░] 42%
  
  QUICK ACTIONS:
  [+ Stock In] [Stock Adjustment] [View Ledger]
  
  RECENT TRANSACTIONS (last 5):
  Date | Type | Qty | Reference
```

---

## 26. Stock In Form

### Purpose
Record received stock from suppliers.

### Breadcrumb
`Inventory > Stock In`

### Form
```
SECTION 1: What
  Inventory Item *      [searchable dropdown]
  Quantity *            [number input] [Unit: auto-filled, e.g., kg]
  Purchase Price/Unit * [৳ input]
  Total Value           [auto-calculated, read-only]
  Batch / Lot Number    [text, optional]
  Expiry Date           [date picker, optional — for medicine]

SECTION 2: From Where
  Supplier *            [searchable dropdown — create new inline]
  Invoice Number        [text]
  Purchase Date *       [date picker — DD/MM/YYYY]

SECTION 3: Where To
  Farm *                [dropdown]
  Storage Location      [text, optional]

SECTION 4: Note
  Notes                 [textarea, 3 rows]

[Cancel] [Save Stock In →]
```

---

## 27. Stock Movement Ledger

### Purpose
Full audit trail of all stock movements for one item.

### Breadcrumb
`Inventory > [Item Name] > Ledger`

### Table
```
Date | Type (In/Out/Adjustment) | Qty | Balance After | Reference | Recorded By | Cost/Unit | Total Value
```

### Ledger Summary
```
Opening Balance (filter period): 1,240 kg
Total In: 600 kg
Total Out (Feed): 480 kg (auto-deducted by feed consumption logs)
Total Out (Medicine): 12 kg
Adjustments: -8 kg (write-off)
Closing Balance: 1,340 kg
```

---

# PART VIII — FINANCE

## 28. Financial Entries

### Purpose
General ledger of all financial transactions for the tenant. ERP-grade data table.

### Breadcrumb
`Finance > Entries`

### Command Bar
```
[+ Record Income] [+ Record Expense]     [Search…] [Filter ▾] [Period: July 2026 ▾] [Export ↓]
```

### Table Columns
```
Date | Type | Category | Description | Amount (৳) | Reference | Recorded By | Linked To | Actions
```

### Filter Panel
```
Type: [Income ☐] [Expense ☐]
Category: [Animal Sale ☐] [Feed ☐] [Medicine ☐] [Labour ☐] [Equipment ☐] [Other ☐]
Date Range: [Date picker start] — [Date picker end]
Amount Range: [Min ৳] — [Max ৳]
Linked to: [Specific animal search] [Batch search]
```

### Summary Widgets (above table)
```
Period Summary (current filter):
  [Income: ৳8,24,000]  [Expenses: ৳3,42,000]  [Net: ৳4,82,000 profit]  [Entries: 84]
```

### Permissions
```
Finance:read: Accountant, Owner
Finance:write: Accountant, Owner
All other roles: Hidden module (403 if navigated directly)
```

---

## 29. Record Transaction

### Purpose
Input form for income or expense entries.

### Breadcrumb
`Finance > Record [Income/Expense]`

### Form
```
TRANSACTION TYPE SELECTOR (at top):
  [Income ●] [Expense ○]  (changes form fields based on selection)

SECTION 1: What
  Category *     [dropdown — Income: Animal Sale, Milk Sale, Batch Sale, Other
                            Expense: Feed, Medicine, Labour, Equipment, Utility, Loan Repayment, Other]
  Description *  [text input]
  Amount *       [৳ number input, right-aligned, large font]
  Date *         [date picker]

SECTION 2: Link to Entity (optional but encouraged)
  Link to: [Animal ○] [Batch ○] [None ●]
  If Animal: [Animal search input → BD-0241 Sahiwal]
  If Batch: [Batch search]

SECTION 3: References
  Invoice / Reference #   [text]
  Payment Method          [Cash ●] [Bank Transfer ○] [Mobile Banking ○]
  Supplier / Buyer        [text]

SECTION 4: Notes
  Notes                   [textarea, 3 rows]

[Cancel] [Record →] (primary)
```

### Auto-Created Entries
```
Banner at top (informational):
  "ℹ Animal sale entries are auto-created when you record a sale in Livestock.
   You can add supplementary entries here."
```

---

## 30. Monthly P&L Report

### Purpose
Income statement for any selected month. Printable. Exportable.

### Breadcrumb
`Finance > P&L Reports`

### Filters
```
[Farm: All Farms ▾] [Month: July 2026 ▾] [Compare to: June 2026 ▾]
[Print] [Export PDF] [Export Excel]
```

### Report Layout
```
┌─────────────────────────────────────────────────────────────────────┐
│ FARM360 AI — PROFIT & LOSS STATEMENT                                │
│ [Organization name]    July 2026    Generated: 07/07/2026           │
├─────────────────────────────────────────────────────────────────────┤
│ INCOME                                    July       June   Change  │
│  Animal Sales                        ৳6,30,000  ৳5,20,000  +21.2%  │
│  Milk Sales                            ৳94,000    ৳88,000   +6.8%  │
│  Other Income                           ৳0        ৳12,000  -100%   │
│ ─────────────────────────────────────────────────────────────────── │
│ TOTAL INCOME                         ৳7,24,000  ৳6,20,000  +16.8%  │
│                                                                     │
│ EXPENSES                                                            │
│  Feed & Nutrition                    ৳1,84,000  ৳1,72,000   +7.0%  │
│  Veterinary & Medicine                 ৳28,000    ৳32,000   -12.5% │
│  Labour                                ৳45,000    ৳45,000    0.0%  │
│  Equipment & Maintenance               ৳15,000     ৳8,000  +87.5%  │
│  Other Expenses                        ৳12,000    ৳18,000  -33.3%  │
│ ─────────────────────────────────────────────────────────────────── │
│ TOTAL EXPENSES                       ৳2,84,000  ৳2,75,000   +3.3%  │
│                                                                     │
│ ─────────────────────────────────────────────────────────────────── │
│ NET PROFIT / (LOSS)                  ৳4,40,000  ৳3,45,000  +27.5%  │
│ PROFIT MARGIN                            60.8%      55.6%          │
└─────────────────────────────────────────────────────────────────────┘

BELOW REPORT: [Bar chart: Monthly P&L trend — last 12 months]
```

---

## 31. Animal Cost Ledger

### Purpose
Full cost and revenue accounting for one animal (accessed from Animal Detail > Finance tab).

### Layout
```
SUMMARY HEADER:
  Acquisition Cost: ৳45,000
  Total Feed Cost: ৳18,240
  Total Medicine: ৳2,400
  Other Costs: ৳800
  ────────────────────────
  Total Investment: ৳66,440
  
  If sold:
  Sale Price: ৳95,000
  Profit: ৳28,560 (43% ROI)
  
  If active:
  Estimated Value: ৳95,000
  Unrealized Gain: ৳28,560

COST BREAKDOWN DONUT CHART:
  Feed: 69% | Acquisition: 17% | Medicine: 4% | Other: 1%

COST LEDGER TABLE:
  Date | Type | Category | Amount | Running Total
```

---

## 32. Loan Manager

### Purpose
Track farm loans and repayments. Simple ledger.

### Breadcrumb
`Finance > Loans`

### Loan List
```
KPI ROW: [Total Outstanding: ৳2,80,000] [Total Paid: ৳1,20,000] [Active Loans: 3]

TABLE: Lender | Amount | Outstanding | Interest % | Due Date | Status | Actions
```

### Loan Detail / Record Repayment
```
LOAN DETAIL CARD:
  Lender: Islami Bank
  Principal: ৳2,00,000
  Interest Rate: 12% / year
  Start Date: 01/04/2026
  Due Date: 31/03/2027
  Total Due: ৳2,24,000
  Paid: ৳80,000
  Remaining: ৳1,44,000
  [Progress bar: 35.7% paid]
  
REPAYMENT TABLE:
  Date | Amount | Type (Principal/Interest) | Note | Recorded By

[+ Record Repayment] → modal form
```

---

# PART IX — SETTINGS

## 33. Farm & Shed Management

### Purpose
Configure farm hierarchy: Farm → Shed → Pen. Owner only.

### Breadcrumb
`Settings > Farm Management`

### Layout
```
FARM ACCORDION LIST:
  ▼ Green Valley Farm  [Edit] [Add Shed] [Delete]
      Location: Gazipur, Dhaka
      Created: 01/01/2026  |  Animals: 128  |  Sheds: 3
      
      ▼ Shed A  [Edit] [Add Pen] [Delete]
          Capacity: 50 animals  |  Current: 42  |  Type: Cattle
          
          Pens: [Pen 1 (12/15)] [Pen 2 (14/15)] [Pen 3 (16/20)]
          
      ► Shed B  (collapsed)
      ► Shed C  (collapsed)
      
  ► City Farm  (collapsed)

[+ Add New Farm] button at top
```

### Add Shed Modal
```
Shed Name *
Capacity (animals) *
Shed Type: [Cattle ●] [Goat ○] [Mixed ○]
Length (meters)  Width (meters)
Notes

[Cancel] [Save Shed]
```

---

## 34. User Management

### Purpose
Invite and manage team members. Owner and FarmManager can access.

### Breadcrumb
`Settings > Users`

### User List Table
```
Avatar | Name | Phone | Role | Assigned Farms | Status | Last Active | Actions
```

### Role Badge Colors
```
Owner:       Purple
FarmManager: Teal
Veterinarian: Green
Worker:      Blue
Accountant:  Amber
Viewer:      Grey
```

### Invite User Panel (right panel)
```
Phone Number *          [+880 input]
Full Name *             [text]
Role *                  [dropdown: FarmManager | Vet | Worker | Accountant | Viewer]
Assigned Farms          [multi-select — leave blank for all]
Assigned Sheds          [multi-select, optional]
Personal Message        [textarea, sent with invite SMS]

[Cancel] [Send Invitation →]
```

### User Actions (via ··· menu per row)
```
Edit Role
Edit Farm Access
Deactivate User
Re-send Invitation (if pending)
Remove from Organization
```

### Permissions
```
Owner: Full user management
FarmManager: Invite/manage Worker, Viewer only
Others: Cannot access Settings > Users
```

---

## 35. Subscription & Billing

### Purpose
View current plan, usage, and billing history.

### Breadcrumb
`Settings > Subscription`

### Layout
```
CURRENT PLAN CARD:
  [Khamar Plan]  [Active ✓]  [Upgrade →]
  ─────────────────────────────────────────
  Renewal Date: 07/08/2026
  Trial days remaining: 7 days  [Progress bar]
  
  USAGE METERS:
  Animals:   100 / 100  [██████████] ⚠ At limit
  Users:     3 / 3      [██████████] ⚠ At limit  [Upgrade to add more]
  Farms:     1 / 2      [█████░░░░░]
  Storage:   420MB / 1GB [████░░░░░░]

PLAN COMPARISON (on [Upgrade] click — full page):
  Cards for each tier side-by-side
  Current plan highlighted
  [Upgrade] buttons on higher tiers

BILLING HISTORY TABLE:
  Date | Plan | Period | Amount | Status | Invoice
```

---

## 36. Tenant Settings & Branding

### Purpose
Organization-level configuration. Owner only.

### Breadcrumb
`Settings > Organization`

### Sections (vertical accordion)
```
▼ Organization Info
  Organization name *
  Business type: [Individual ●] [Partnership ○] [Company ○]
  Division / District / Upazila
  Phone (public)
  Email (public)
  [Save]

▼ Branding (Banik+ tiers)
  Logo upload: [Drag & drop or click — PNG/JPG, max 2MB, min 200×200px]
  Primary color: [Color picker with hex input]
  [Preview] [Save Branding]

▼ Regional Settings
  Language: [বাংলা ●] [English ○]
  Date format: DD/MM/YYYY (fixed — Bangladesh standard)
  Fiscal year starts: [Month dropdown]
  Currency: BDT ৳ (fixed)

▼ Notifications
  Vaccination reminders: [X days before — number input, default 7]
  Low stock alerts: [X% below reorder level — default 0% (at reorder)]
  Financial report: [Monthly ●] [Weekly ○]
  Delivery: [SMS ☑] [In-app ☑] [Email ☐]

▼ Danger Zone
  [Delete Organization] (danger button — "Type organization name to confirm")
```

---

# PART X — GLOBAL PATTERNS

## 37. Notifications Panel

### Purpose
Real-time alerts delivered via SignalR. Tenant-scoped.

### Trigger
```
🔔 Bell icon in topbar
Badge count: unread notifications (up to 99+)
Animation: badge pulses when new notification arrives
```

### Panel Layout (right-side overlay, 360px)
```
[Notifications] [Mark all read ✓] [Settings ⚙]
[All ●] [Unread ○] [Health ○] [Inventory ○] [Finance ○]

─── TODAY ────────────────────────────────────────────
[🔴 OVERDUE] Vaccination due — 5 animals
  FMD Dose 2 for Batch B was due 2 days ago
  [View →] [Dismiss]
  2 hours ago

[⚠ LOW STOCK] Rice Bran running low
  Current: 240kg, Reorder at: 200kg (8 days estimated)
  [Record Stock In →]
  5 hours ago

─── YESTERDAY ────────────────────────────────────────
[✓ INFO] Monthly report generated
  July 2026 P&L is ready to view
  [View Report →]
  Yesterday 11:00 PM
```

### Empty State
```
[Bell illustration]
"You're all caught up!"
"No new notifications"
```

---

## 38. Command Palette

### Purpose
Universal keyboard launcher. Inspired by Linear's ⌘K. Reaches every page and action.

### Trigger
```
⌘K (Mac) / Ctrl+K (Windows/Linux)
Or: click search bar in topbar
```

### Palette Layout (centered modal overlay, 640px wide)
```
[🔍 Search actions, animals, reports…]

SUGGESTED (no query):
  Recent:
  📄 BD-0241 Sahiwal — Animal
  📊 July P&L Report
  
  Quick Actions:
  ⚡ Register Animal
  ⚡ Record Weight
  ⚡ Log Feed Consumption
  ⚡ Record Vaccination
  ⚡ Stock In

NAVIGATION:
  Go to Dashboard
  Go to Livestock
  Go to Health
  Go to Finance

WHEN TYPING "bd-0241":
  🐄 BD-0241 — Sahiwal, Active, Shed A
  🐄 BD-02410 — Goat, Active, Shed B
  
WHEN TYPING "vac":
  ⚡ Record Vaccination
  📋 Vaccination Protocol Manager
  📊 Vaccination Due Report
```

### Keyboard Navigation
```
↑↓:       Navigate results
Enter:    Execute / Navigate
Escape:   Close
⌘K:       Reopen (if closed)
```

---

## 39. Screen Relationships Map

```
AUTH FLOW:
  Login → OTP Verification → Dashboard (returning user)
  Login → Registration Wizard → Dashboard (new user)

DASHBOARD NAVIGATION:
  Executive Dashboard
  ├── Animal List (via "View All Animals" link)
  ├── Finance (via revenue widget)
  ├── Health > Vaccination Due (via alert widget)
  └── Inventory (via stock alert widget)

LIVESTOCK FLOW:
  Animal List ──────────────► Animal Detail
       │                          │── Weight Tab → [Record Weight modal]
       │                          │── Health Tab → Health History (§21)
       │                          │── Feed Tab → Consumption Log
       │                          └── Finance Tab → Cost Ledger (§31)
       │
       └──────────────────────── Animal Timeline (via "Timeline" on detail)

  Batch List → Batch Detail → Animal List (filtered by batch)

HEALTH FLOW:
  Vaccination Due List → [Record Vaccination] → Animal Health History
  Disease Incident → Animal List (affected animals)
  Protocol Manager → [Assign to Animals] → Animal List

INVENTORY FLOW:
  Inventory List → [Stock In] → Stock Ledger (per item)
  Feed Consumption Log (Feeding) → auto-deducts → Inventory

FINANCE FLOW:
  Financial Entries ← auto-created by: Animal Sale · Feed Consumption (Phase 2)
  Animal Detail > Finance Tab → Cost Ledger
  Monthly P&L → drills into Financial Entries (filtered)
  Loan Manager → Loan Detail → Repayment records

SETTINGS FLOW:
  Settings
  ├── Farm Management (Owner)
  ├── User Management (Owner, FarmManager)
  ├── Subscription (Owner)
  └── Organization Settings (Owner)
```

---

## 40. Component Reuse Strategy

### Shared Components Inventory

| Component | Used In |
|---|---|
| `<DataTable>` | Animal List, Vaccination Due, Inventory List, Financial Entries, Batch List, Users, Billing History |
| `<ContextPanel>` | Animal List, Inventory List, Vaccination Due, Financial Entries |
| `<StatusBadge>` | All list tables, Detail pages, Context panels |
| `<KpiCard>` | Executive Dashboard, Farm Dashboard, Batch Detail |
| `<CommandBar>` | Every page with actions |
| `<EmptyState>` | All list pages, all tab panels |
| `<SkeletonLoader>` | Every loading state |
| `<ToastNotification>` | Every action with feedback |
| `<ConfirmDialog>` | All destructive actions |
| `<DatePicker>` | All date inputs throughout |
| `<SearchInput>` | All list pages |
| `<FilterPanel>` | All list pages |
| `<Timeline>` | Animal Timeline, Health History, Audit Log |
| `<ChartWidget>` | Dashboard, P&L, FCR Report, Weight History |
| `<MoneyInput>` | All financial forms |
| `<PhoneInput>` | Auth forms, User Management |
| `<OtpInput>` | OTP Verification page |
| `<ProgressBar>` | Subscription, Inventory levels, Batch P&L |
| `<Breadcrumb>` | Every authenticated page |

### Design Token Usage

Every component uses CSS custom properties from `tokens.css`:
```
Color tokens:   var(--color-brand-500), var(--color-surface-raised)…
Space tokens:   var(--space-4), var(--space-6)…
Radius tokens:  var(--radius-md), var(--radius-lg)…
Shadow tokens:  var(--shadow-sm), var(--shadow-md)…
Motion tokens:  var(--motion-short), var(--motion-medium)…
```

---

## 41. User Journeys

### Journey 1: Owner's Morning Routine (5 minutes)
```
1. Opens app → Executive Dashboard
2. Scans KPI row (15 seconds)
3. Sees "3 Health Alerts" → clicks → Vaccination Due List
4. Selects 3 overdue animals → Bulk Record Vaccination
5. Returns to Dashboard → KPI now shows "0 Health Alerts"
6. Checks Monthly Revenue widget → P&L looks good
7. Done — all in 5 minutes
```

### Journey 2: Worker Logs Daily Feed (3 minutes)
```
1. Feeding > Consumption (bookmarked or ⌘K "log feed")
2. Date: auto-filled Today
3. Farm: auto-filled (worker has 1 farm)
4. Shed: Shed A
5. Formula: Green Grass (last used — auto-suggested)
6. Quantity: 840 kg
7. [Log Consumption →] → success toast
8. Worker leaves page
```

### Journey 3: Register New Animal Purchase (4 minutes)
```
1. ⌘K → "register animal" → [Register Animal] endpoint
2. Tag ID: BD-0280 → "✓ Available" in 500ms
3. Species: Beef Cattle
4. Breed: Sahiwal (searchable dropdown)
5. Acquisition Date: Today
6. Price: ৳48,000
7. Shed: Shed A, Pen 2
8. [Save & Register Another] → form resets with focus on Tag ID
9. Register 3 more animals in succession
```

### Journey 4: Accountant Reviews July P&L (7 minutes)
```
1. Finance > P&L Reports
2. Month: July 2026
3. Compare to: June 2026
4. Reviews table — sees Equipment costs up 87.5%
5. Drills into Financial Entries → filters Category: Equipment
6. Finds 3 entries — motor pump purchase
7. Exports to Excel for owner review
```

---

## 42. Accessibility Rules

### WCAG 2.1 AA Compliance — Non-Negotiable

```
COLOR CONTRAST:
  Body text on dark bg: #f0f6fc on #0d1117 → 14.9:1 ✓ AAA
  Body text on light bg: #0f172a on #ffffff → 15.8:1 ✓ AAA
  Brand button text: #ffffff on #0d9e83  → 4.6:1 ✓ AA
  Secondary text: #8b949e on #0d1117    → 4.5:1 ✓ AA (just meets)
  Status badges: all verified at ≥4.5:1

TOUCH TARGETS:
  All interactive elements: minimum 44×44px
  Icon buttons: padding to 44px even if icon is 20px
  Table row click target: full row width × 40px height

KEYBOARD NAVIGATION:
  Full keyboard operability — no mouse-only interactions
  Tab order follows logical reading order (top-left → right → down)
  Focus ring: 2px solid var(--color-brand-500), always visible
  NEVER: outline: none without alternative focus indicator

ARIA:
  All icons: aria-label
  All form inputs: associated <label>
  Dynamic content: aria-live="polite" for toast notifications
  Tables: proper <thead>, <th scope="col">, <th scope="row">
  Modals: role="dialog", aria-modal="true", focus trap
  Loading: aria-busy="true", aria-label="Loading…"
  Status badges: role="status", aria-label="Animal status: Active"

SCREEN READER:
  Skip-to-main-content link at top of page
  Page title changes on navigation (document.title)
  Route changes announced: aria-live region

MOTION:
  @media (prefers-reduced-motion: reduce) → disable all animations
  Respect OS-level motion preference
  No content change from motion alone

LANGUAGE:
  <html lang="bn"> or <html lang="en"> based on user preference
  Bangla text: Noto Sans Bengali, correct Unicode (not transliteration)
  Mixed content: <span lang="en"> for Latin within Bangla page
```

---

*This blueprint is the authoritative UI reference for all Farm360 AI frontend development.*  
*Governed by: F360-CONST-2026-001 — Project Constitution · UIX v1.0.*  
*© 2026 Farm360 AI Engineering Organization. All Rights Reserved.*
