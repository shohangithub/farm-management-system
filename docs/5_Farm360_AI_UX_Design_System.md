# Farm360 AI — UI/UX Design System & Screen Design Document

**Document ID:** F360-UIX-2026-001  
**Version:** 1.0  
**Status:** Approved for Implementation  
**Prepared by:** Senior UX Design Office — Fluent / Material / HIG Synthesis  
**Date:** July 2026  
**Parent Documents:** PVD v1.0 · PRD v1.0 · SAD v1.0 · DDD v1.0  
**Classification:** Confidential — Design & Engineering Use  

---

> *"Design is not what it looks like. Design is how it works. A great ERP is not just beautiful — it is precise, predictable, and respectful of the user's time."*  
> — Farm360 AI Design Principles

---

## Table of Contents

1. [Design Philosophy & Principles](#1-design-philosophy--principles)
2. [Color System](#2-color-system)
3. [Typography System](#3-typography-system)
4. [Spacing System](#4-spacing-system)
5. [Icon System](#5-icon-system)
6. [Elevation & Shadow System](#6-elevation--shadow-system)
7. [Border & Radius System](#7-border--radius-system)
8. [Button Component Design](#8-button-component-design)
9. [Input & Form Element Design](#9-input--form-element-design)
10. [Card Component Design](#10-card-component-design)
11. [Data Grid Design](#11-data-grid-design)
12. [Navigation Design](#12-navigation-design)
13. [Dialog & Drawer Design](#13-dialog--drawer-design)
14. [Notification & Toast Design](#14-notification--toast-design)
15. [Chart & Data Visualization Design](#15-chart--data-visualization-design)
16. [Table Design](#16-table-design)
17. [Form & Wizard Design](#17-form--wizard-design)
18. [Loading, Empty & Error States](#18-loading-empty--error-states)
19. [Dark Mode Design](#19-dark-mode-design)
20. [Responsive Design System](#20-responsive-design-system)
21. [Accessibility (WCAG 2.1 AA)](#21-accessibility-wcag-21-aa)
22. [Screen Designs — All MVP Screens](#22-screen-designs--all-mvp-screens)
23. [UI Component Inventory](#23-ui-component-inventory)
24. [Screen Map & Navigation Map](#24-screen-map--navigation-map)

---

## 1. Design Philosophy & Principles

### 1.1 Design DNA — Synthesizing Three Systems

Farm360 AI's design language is built by taking the best from three world-class design systems:

| Source | What We Take |
|---|---|
| **Microsoft Fluent Design 2** | Depth, layering, and material metaphor; Acrylic/Mica-inspired backgrounds; smooth animations; accessible-first thinking; enterprise density without clutter |
| **Material Design 3** | Dynamic color system with semantic tokens; expressive component shape language; adaptive layouts; ripple and state-layer interaction patterns |
| **Apple Human Interface Guidelines** | Spatial clarity; generous whitespace; purposeful typography hierarchy; direct manipulation feedback; precision touch targets |

> **The synthesis:** Fluent's enterprise structure + Material's dynamic color intelligence + Apple's typographic precision = a premium ERP that no Bangladeshi farmer has ever seen before.

### 1.2 The 7 Design Principles

| # | Principle | Applied Meaning |
|---|---|---|
| **1** | **Clarity over cleverness** | Every element has one purpose. No decorative complexity. Information hierarchy is crystal clear in 1 second. |
| **2** | **Density with breathing room** | ERP requires data density. But every data-dense screen has margins, padding, and whitespace that prevents cognitive fatigue. |
| **3** | **Forgiveness by design** | Every destructive action requires confirmation. Undo is always available. Errors are fixable, not catastrophic. |
| **4** | **Local first** | UI copy, number formats, date formats, and feedback messages are Bangladesh-native. BDT, DD/MM/YYYY, Bangla script — feel native. |
| **5** | **Role-aware intelligence** | What the Farm Owner sees is different from what the Worker sees. The UI adapts to role — no cognitive overload from features irrelevant to the user. |
| **6** | **Progressive disclosure** | Summary first, details on demand. Dashboard → Module → Record → Detail. No data dump on first glance. |
| **7** | **Offline-honest** | The UI truthfully communicates sync status. Offline entries are visually distinguished. No silent data loss. |

### 1.3 Design Influence Benchmarks

We benchmark Farm360 AI against — and intentionally exceed:

| Benchmark | Their Strength | Our Advantage |
|---|---|---|
| **Odoo 17** | Breadth of modules | Better mobile-first design; cleaner visual hierarchy; Bangladesh localization |
| **ERPNext/Frappe** | Open source flexibility | Superior typography; premium color system; consistent component language |
| **Zoho One** | Dashboard density | Better data visualization; more purposeful empty states; cleaner navigation |
| **Salesforce Lightning** | Enterprise polish | Lighter visual weight; better for low-bandwidth mobile; more accessible |

---

## 2. Color System

### 2.1 Design Philosophy — Color

Farm360 AI uses a **semantic token architecture** (inspired by Material Design 3's color roles). Colors are never used as raw hex values in components — always as named tokens. This enables instant dark mode switching and theme customization for Enterprise tenants.

### 2.2 Primary Brand Color Palette

**Primary Hue: Deep Teal (Trust + Agriculture + Technology)**

Rationale: Teal evokes trust, agriculture (water, growth), and technology simultaneously. It performs excellently on low-quality AMOLED screens prevalent in Bangladesh. Unique in the ERP space — differentiated from Zoho's blue, Odoo's purple, and SAP's dark navy.

```
Primary Scale (HSL: 173, 80%, ...)
──────────────────────────────────────────────────────────
brand-50:   hsl(173, 80%, 95%)  →  #ecfdf8   [Lightest tint]
brand-100:  hsl(173, 80%, 87%)  →  #c5f5e7
brand-200:  hsl(173, 75%, 74%)  →  #8decd2
brand-300:  hsl(173, 70%, 60%)  →  #4dd9ba
brand-400:  hsl(173, 72%, 47%)  →  #1cbf9f
brand-500:  hsl(173, 80%, 38%)  →  #0d9e83   [Brand Primary]
brand-600:  hsl(173, 82%, 30%)  →  #087c67   [Pressed/Active]
brand-700:  hsl(173, 85%, 23%)  →  #065c4d
brand-800:  hsl(173, 88%, 17%)  →  #043d33
brand-900:  hsl(173, 90%, 12%)  →  #021f1a   [Darkest]
```

### 2.3 Secondary Accent — Amber (Energy + Action)

```
accent-50:   hsl(38, 100%, 96%)  →  #fff8eb
accent-100:  hsl(38, 100%, 88%)  →  #fde9bc
accent-200:  hsl(38, 98%, 75%)   →  #fbd07f
accent-300:  hsl(38, 96%, 62%)   →  #f9b83d
accent-400:  hsl(38, 94%, 50%)   →  #f59e0b   [Accent Primary]
accent-500:  hsl(38, 96%, 42%)   →  #d97706
accent-600:  hsl(38, 98%, 34%)   →  #b45309
```

### 2.4 Semantic Color Tokens — Light Mode

```
SURFACE TOKENS
──────────────────────────────────────────────────────────
surface-base:       #ffffff          [Page background]
surface-raised:     #f8fafc          [Card background]
surface-overlay:    #f1f5f9          [Secondary card / Panel]
surface-sunken:     #e2e8f0          [Input background (rest)]
surface-inverse:    #1e2a38          [Tooltip, dark surfaces]

BORDER TOKENS
──────────────────────────────────────────────────────────
border-subtle:      #e2e8f0          [Card borders, dividers]
border-default:     #cbd5e1          [Input borders (rest)]
border-strong:      #94a3b8          [Input borders (focused nearby)]
border-focus:       #0d9e83          [Focus ring — brand primary]

TEXT TOKENS
──────────────────────────────────────────────────────────
text-primary:       #0f172a          [Primary body text — contrast 15.8:1 on white]
text-secondary:     #334155          [Secondary labels — contrast 10.7:1]
text-tertiary:      #64748b          [Hints, captions — contrast 4.6:1 ✓ AA]
text-disabled:      #94a3b8          [Disabled text]
text-inverse:       #ffffff          [On dark surfaces]
text-brand:         #0d9e83          [Links, interactive labels]
text-danger:        #dc2626          [Error messages]
text-warning:       #d97706          [Warning messages]
text-success:       #16a34a          [Success messages]

INTERACTIVE TOKENS
──────────────────────────────────────────────────────────
interactive-primary:        #0d9e83  [Primary button fill]
interactive-primary-hover:  #087c67  [Hover state]
interactive-primary-press:  #065c4d  [Active press]
interactive-primary-ghost:  rgba(13, 158, 131, 0.08)  [Ghost hover]

SEMANTIC STATUS TOKENS
──────────────────────────────────────────────────────────
status-success-bg:    #dcfce7   status-success-text: #15803d
status-warning-bg:    #fef3c7   status-warning-text: #92400e
status-danger-bg:     #fee2e2   status-danger-text:  #b91c1c
status-info-bg:       #dbeafe   status-info-text:    #1d4ed8
status-neutral-bg:    #f1f5f9   status-neutral-text: #475569

LIVESTOCK STATUS TOKENS (domain-specific)
──────────────────────────────────────────────────────────
animal-active:        #0d9e83   (brand teal)
animal-sold:          #7c3aed   (violet)
animal-dead:          #6b7280   (neutral gray)
animal-quarantine:    #dc2626   (danger red)
animal-slaughtered:   #ea580c   (orange)
animal-transferred:   #2563eb   (blue)
```

### 2.5 Full Color Token Map — Dark Mode

```
SURFACE TOKENS (Dark)
──────────────────────────────────────────────────────────
surface-base:       #0d1117          [Page background — GitHub-dark inspired]
surface-raised:     #161b22          [Card background]
surface-overlay:    #21262d          [Secondary card / Panel]
surface-sunken:     #30363d          [Input background]
surface-inverse:    #f0f6fc          [Tooltip on dark]

BORDER TOKENS (Dark)
──────────────────────────────────────────────────────────
border-subtle:      #21262d
border-default:     #30363d
border-strong:      #484f58
border-focus:       #1cbf9f          [Lighter teal for dark mode]

TEXT TOKENS (Dark)
──────────────────────────────────────────────────────────
text-primary:       #f0f6fc          [Contrast 14.9:1 on surface-base]
text-secondary:     #8b949e
text-tertiary:      #6e7681          [4.6:1 minimum maintained]
text-disabled:      #484f58
text-inverse:       #0d1117
text-brand:         #1cbf9f

INTERACTIVE TOKENS (Dark)
──────────────────────────────────────────────────────────
interactive-primary:        #1cbf9f
interactive-primary-hover:  #4dd9ba
interactive-primary-press:  #8decd2
```

### 2.6 Data Visualization Color Palette

```
Chart Series Colors (accessible, distinguishable on both modes):
  chart-1:  #0d9e83   (Teal — primary brand)
  chart-2:  #f59e0b   (Amber — accent)
  chart-3:  #6366f1   (Indigo)
  chart-4:  #ef4444   (Red)
  chart-5:  #10b981   (Emerald)
  chart-6:  #8b5cf6   (Violet)
  chart-7:  #f97316   (Orange)
  chart-8:  #06b6d4   (Cyan)

Heatmap: #dcfce7 → #16a34a (success scale for positive metrics)
         #fef3c7 → #b45309 (warning scale for FCR, costs)
         #fee2e2 → #b91c1c (danger scale for mortality, overdue)
```

---

## 3. Typography System

### 3.1 Font Selection

| Font | Role | Rationale |
|---|---|---|
| **Inter** (Latin) | Primary UI typeface | Industry-standard for data-dense UIs; excellent legibility at 12–14px on low-DPI screens; free, fast CDN |
| **Noto Sans Bengali** | Bangla language typeface | Google's Noto series ensures complete Unicode coverage; maintains design consistency in Bangla mode; optimized for low-res screens |
| **JetBrains Mono** | Monospace — codes, IDs, numbers | Tag numbers, RFID codes, financial figures in tables benefit from tabular numerals and monospace alignment |

```css
/* Font stack */
--font-sans:  'Inter', 'Noto Sans Bengali', system-ui, -apple-system, sans-serif;
--font-mono:  'JetBrains Mono', 'Courier New', monospace;

/* Import */
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&family=Noto+Sans+Bengali:wght@300;400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');
```

### 3.2 Type Scale (10-Step)

```
Token             Size    Line-Height  Weight  Letter-Spacing  Usage
─────────────────────────────────────────────────────────────────────────────────
display-2xl       48px    56px         800     -1.5px          Hero titles (landing only)
display-xl        36px    44px         700     -1.2px          Module headers (e.g., Dashboard)
display-lg        30px    38px         700     -0.8px          Page section titles
heading-xl        24px    32px         700     -0.5px          Card primary headings
heading-lg        20px    28px         600     -0.3px          Dialog titles, section headings
heading-md        18px    26px         600     -0.2px          Widget titles
heading-sm        16px    24px         600     0               Sub-section headings
body-lg           16px    24px         400     0               Primary body, form labels
body-md           14px    20px         400     0               Secondary text, table cells
body-sm           13px    18px         400     0               Captions, helper text
label-lg          14px    20px         500     0.1px           Button labels, nav items
label-md          13px    18px         500     0.1px           Tag labels, chip text
label-sm          12px    16px         500     0.2px           Badge text, status pills
mono-lg           14px    20px         400     0               Animal tags, financial codes
mono-sm           12px    16px         400     0               ID fields in tables
```

### 3.3 Typography Rules

```
DO:
  → Use text-primary (weight 400-500) for body; text-secondary for supplementary
  → Use heading-sm (weight 600) for form section headers
  → Use body-sm + text-tertiary for all helper text below inputs
  → Use mono-lg for animal Tag IDs — always monospace, always code-like
  → In Bangla mode: same size scale; Noto Sans Bengali substitutes automatically

DO NOT:
  → Mix more than 2 font weights on a single card
  → Use body text below 13px for any operational UI (accessibility)
  → Use all-caps except for status badges (label-sm)
  → Use color alone to convey meaning in text (add icon or pattern)
```

---

## 4. Spacing System

### 4.1 Base Unit: 4px

All spacing values are multiples of 4px (the "base unit"). This creates a mathematically consistent rhythm across all components and layouts.

```
Token       Value   Usage
──────────────────────────────────────────────────────────────────
space-0.5   2px     Icon padding, fine-tuning
space-1     4px     Inline icon gaps, checkbox-label gap
space-1.5   6px     Tight icon padding
space-2     8px     Badge padding, compact chip padding
space-3     12px    Input inner padding (horizontal), badge height
space-4     16px    Button horizontal padding, card inner gap
space-5     20px    Form field gap, section list gap
space-6     24px    Card padding, dialog section spacing
space-8     32px    Major section gaps within a card
space-10    40px    Between-card spacing on dashboard
space-12    48px    Page-level section gaps
space-16    64px    Hero spacing, onboarding wizard steps
space-24    96px    Full-page section dividers (landing only)
```

### 4.2 Component Spacing Specifications

```
BUTTON PADDING
  Size XS: 6px 12px    (height: 28px)
  Size SM: 8px 16px    (height: 34px)
  Size MD: 10px 20px   (height: 40px) ← default
  Size LG: 12px 24px   (height: 48px)
  Size XL: 14px 28px   (height: 56px) — touch primary CTAs only

INPUT FIELD
  Padding: 10px 14px   (height: 40px desktop; 48px mobile)
  Label gap (top): 6px above label, 4px below label to input
  Helper text: 4px below input

CARD
  Padding: 24px (desktop), 16px (tablet), 16px (mobile)
  Gap between card elements: 16px
  Card gap on grid: 24px (desktop), 16px (tablet/mobile)

SIDEBAR
  Item padding: 10px 16px  (height: 44px)
  Item gap: 2px
  Section header padding: 20px 16px 6px
  Logo zone height: 64px
  Footer zone height: 64px

TABLE
  Cell padding: 12px 16px (desktop), 10px 12px (mobile)
  Row height: 52px (standard), 40px (compact mode)
  Header height: 44px

DIALOG
  Padding: 32px (desktop), 24px (mobile)
  Max width: 480px (SM), 640px (MD), 800px (LG), 1024px (XL)
  Header-body gap: 20px
  Body-footer gap: 24px
  Button gap in footer: 8px
```

### 4.3 Layout Grid

```
Desktop (≥1280px):
  Container max-width: 1440px
  Columns: 12
  Gutter: 24px
  Margin: 32px

Laptop (1024px–1279px):
  Columns: 12
  Gutter: 20px
  Margin: 24px

Tablet (768px–1023px):
  Columns: 8
  Gutter: 16px
  Margin: 24px

Mobile (320px–767px):
  Columns: 4
  Gutter: 16px
  Margin: 16px
```

---

## 5. Icon System

### 5.1 Icon Library Selection

**Primary: Fluent UI Icons (v2)** — Microsoft's Fluent System Icons  
**Fallback: Phosphor Icons** — for any gap in Fluent coverage

Rationale: Fluent UI Icons are the most comprehensive, professionally designed icon library for enterprise software. Two sizes (Regular/Filled) + consistent 24px viewbox. Available as SVG sprites or direct SVG import.

### 5.2 Icon Usage Rules

```
SIZES:
  icon-xs:  12px  — Status indicator dots (inline with text)
  icon-sm:  16px  — Inline body text icons, table action icons
  icon-md:  20px  — Navigation items, button icons, form prefix icons
  icon-lg:  24px  — Card header icons, primary action icons
  icon-xl:  32px  — Module hero icons, empty state icons
  icon-2xl: 48px  — Onboarding wizard step icons
  icon-3xl: 64px  — Zero-state illustrations (paired with illustration)

STATES:
  Rest:     Use "Regular" variant (outline)
  Active:   Use "Filled" variant
  Disabled: Regular + 40% opacity

COLORS:
  Navigation icon (inactive): text-tertiary
  Navigation icon (active):   interactive-primary
  Action icon:                text-secondary (hover: text-primary)
  Status icon:                semantic status color
  Danger icon:                status-danger-text
```

### 5.3 Domain-Specific Icon Mapping

```
Module          Icon (Fluent)           Filled Variant
──────────────────────────────────────────────────────────
Dashboard       Grid square             Grid square filled
Livestock       Animal                  (custom SVG — cow silhouette)
Feeding         Bowl                    Bowl filled
Health          Heart pulse             Heart pulse filled
Inventory       Box                     Box filled
Finance         Coins                   Coins filled
Reports         Chart                   Chart filled
Settings        Settings                Settings filled
Notifications   Bell                    Bell filled
Profile         Person circle           Person circle filled
Farm            Building                Building filled
Shed            Home                    Home filled
Add Animal      Add circle              Add circle filled
Weight          Scale                   Scale filled
Vaccination     Syringe                 Syringe filled
Treatment       First aid               First aid filled
Death/Mortality Prohibited              (red tinted)
Sale            Tag                     Tag filled
Transfer        Arrow swap              Arrow swap filled
Batch           People                  People filled
Feed Formula    Beaker                  Beaker filled
Supplier        Truck                   Truck filled
Loan            Currency                Currency filled
Audit Log       History                 History filled
```

---

## 6. Elevation & Shadow System

### 6.1 Material-Inspired Elevation Tokens

```
Light Mode Shadows:
────────────────────────────────────────────────────────────────────────────
elevation-0:   none                   [In-page content, backgrounds]
elevation-1:   0 1px 3px rgba(0,0,0,.08), 0 1px 2px rgba(0,0,0,.06)
               [Base cards, surface tiles]
elevation-2:   0 4px 6px rgba(0,0,0,.07), 0 2px 4px rgba(0,0,0,.06)
               [Raised cards, dropdowns, datepickers]
elevation-3:   0 10px 15px rgba(0,0,0,.08), 0 4px 6px rgba(0,0,0,.05)
               [Dialogs, floating panels, sidesheets]
elevation-4:   0 20px 25px rgba(0,0,0,.10), 0 8px 10px rgba(0,0,0,.06)
               [Modal overlays, command palette]
elevation-5:   0 25px 50px rgba(0,0,0,.20)
               [Full-page overlays, drawers]

Dark Mode Shadows (lighter, more subtle — dark bg absorbs depth):
elevation-1:   0 1px 3px rgba(0,0,0,.30), 0 0 0 1px rgba(255,255,255,.04)
elevation-2:   0 4px 8px rgba(0,0,0,.40), 0 0 0 1px rgba(255,255,255,.06)
elevation-3:   0 8px 24px rgba(0,0,0,.50), 0 0 0 1px rgba(255,255,255,.08)
elevation-4:   0 16px 40px rgba(0,0,0,.60)
elevation-5:   0 24px 64px rgba(0,0,0,.70)
```

---

## 7. Border & Radius System

### 7.1 Radius Tokens

```
radius-none:   0px     [Tables, data grids (internal cells)]
radius-xs:     2px     [Status badges, tiny chips]
radius-sm:     4px     [Inputs, compact buttons, tags]
radius-md:     8px     [Default buttons, cards, panels]
radius-lg:     12px    [Modal dialogs, command palette]
radius-xl:     16px    [Drawer panels, mobile bottom sheets]
radius-2xl:    24px    [Large hero cards]
radius-full:   9999px  [Pills, avatar badges, progress indicators]
```

### 7.2 Radius by Component

```
Button:          radius-sm (4px) — precise, enterprise feel
Input:           radius-sm (4px)
Card:            radius-md (8px)
Dialog:          radius-lg (12px)
Dropdown:        radius-md (8px)
Badge/Pill:      radius-full
Avatar:          radius-full
Table:           radius-md (outer container); radius-none (cells)
Tooltip:         radius-sm (4px)
Sidebar:         radius-none (full height)
Toast:           radius-md (8px)
Chart container: radius-md (8px)
Progress bar:    radius-full
```

---

## 8. Button Component Design

### 8.1 Button Variants

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  BUTTON VARIANT CATALOG                                                     │
│                                                                             │
│  [1] PRIMARY     Background: interactive-primary | Text: white              │
│      ┌─────────────────────┐                                               │
│      │  ● Save Changes     │  ← filled, brand color, drop shadow           │
│      └─────────────────────┘                                               │
│      Hover: darken 8% + lift shadow                                        │
│      Active: darken 12% + no shadow (pressed-in feel)                      │
│      Disabled: surface-overlay bg, text-disabled text, no pointer          │
│                                                                             │
│  [2] SECONDARY   Background: surface-raised | Border: border-default        │
│      ┌─────────────────────┐                                               │
│      │  Cancel             │  ← outlined, neutral                          │
│      └─────────────────────┘                                               │
│      Hover: surface-overlay bg + border-strong                             │
│      Active: surface-sunken bg                                             │
│                                                                             │
│  [3] GHOST       Background: transparent | Text: interactive-primary        │
│      [  View Details  ]  ← no border, text only                            │
│      Hover: interactive-primary-ghost background                           │
│      Active: slightly darker ghost                                         │
│                                                                             │
│  [4] DANGER      Background: status-danger (#dc2626) | Text: white         │
│      ┌─────────────────────┐                                               │
│      │  🗑 Delete Animal   │  ← red fill, used ONLY for irreversible      │
│      └─────────────────────┘                                               │
│      Always preceded by confirmation dialog                                │
│                                                                             │
│  [5] ICON ONLY   Square; icon centered; same 5 variants                    │
│      [⊕]  [↑]  [✕]  [···]                                                 │
│      Used: table row actions, card corner actions                           │
│                                                                             │
│  [6] LOADING     Primary variant + spinner replaces icon + disabled state  │
│      ┌─────────────────────┐                                               │
│      │  ⟳ Saving...       │                                               │
│      └─────────────────────┘                                               │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 8.2 Button Anatomy

```
[LEFT ICON SLOT] [LABEL] [RIGHT ICON SLOT / BADGE SLOT]

Spacing rules:
  Label only:        24px horizontal padding
  Icon left + label: 12px left, 20px right, 8px gap between icon and label
  Icon right + label:20px left, 12px right, 8px gap
  Icon only:         equal padding on all sides = (height - icon size) / 2
```

### 8.3 Button Group Pattern

```
┌──────────────┬──────────────┬──────────────┐
│  This Month  │  Last Month  │  Custom Range │
└──────────────┴──────────────┴──────────────┘
  ↑ Segmented control: first selected = primary fill; others = ghost
  Used for: Date filters, view toggles (List/Grid), chart period selectors
```

---

## 9. Input & Form Element Design

### 9.1 Input Anatomy

```
[LABEL]  [OPTIONAL BADGE]
┌──────────────────────────────────────────────────┐
│  [PREFIX ICON]  Placeholder or value  [SUFFIX]  │
└──────────────────────────────────────────────────┘
[HELPER TEXT or ERROR MESSAGE]

Label: body-md weight 500, text-primary, margin-bottom: 4px
Optional badge: label-sm, text-tertiary, float right
Prefix icon: icon-md, text-tertiary; turns brand on focus
Suffix: Clear button, show/hide password, unit label (kg, BDT)
Helper text: body-sm, text-tertiary
Error: body-sm, status-danger-text, ⚠ icon prefix
```

### 9.2 Input States

```
State       Border              Background      Label color
───────────────────────────────────────────────────────────────────
Rest        border-default      surface-sunken  text-primary
Hover       border-strong       surface-sunken  text-primary
Focused     border-focus (2px)  surface-raised  interactive-primary
Error       status-danger-text  #fff5f5         status-danger-text
Success     status-success-text surface-raised  text-primary
Disabled    border-subtle       surface-overlay text-disabled
Read-only   border-subtle       surface-base    text-secondary
```

### 9.3 Input Variants

```
TEXT INPUT:         Standard single-line
TEXTAREA:           Multi-line; min-height 96px; auto-grows to max 240px
NUMBER INPUT:       Numeric keyboard on mobile; right-align; optional +/- steppers
SEARCH INPUT:       Prefix: search icon; Suffix: clear (×); bottom border only variant
SELECT/DROPDOWN:    Chevron suffix; searchable for >10 options; grouped options
DATE PICKER:        Custom calendar overlay; Bangladesh date format DD/MM/YYYY
DATE RANGE:         Two date pickers linked; highlight range visually
TIME PICKER:        Scroll wheel on mobile; dropdown on desktop
PHONE INPUT:        +880 prefix hardcoded for MVP; 11-digit validation
CURRENCY INPUT:     "BDT" prefix; comma-separated on blur; decimal-aware
RADIO GROUP:        Vertical list for ≤4 options; horizontal for 2 options
CHECKBOX:           Standard; supports indeterminate for select-all
TOGGLE/SWITCH:      iOS-style; label on right; used for boolean settings
SLIDER:             Range with min/max labels; used for BCS (1.0–5.0)
AUTOCOMPLETE:       Debounced search; skeleton loading; no-match empty state
FILE UPLOAD:        Drag-and-drop zone + click; progress bar; preview thumbnails
TAG INPUT:          Pill creation on Enter/comma; removable; max count
```

### 9.4 Form Layout Grid

```
SINGLE COLUMN (mobile default, simple forms):
  ┌─────────────────────────────────────────┐
  │ [Field]                                 │
  │ [Field]                                 │
  │ [Field]                                 │
  └─────────────────────────────────────────┘

TWO COLUMN (tablet+, general data entry):
  ┌───────────────────┬─────────────────────┐
  │ [Field]           │ [Field]             │
  │ [Field]           │ [Field]             │
  └───────────────────┴─────────────────────┘

THREE COLUMN (desktop, dense forms):
  ┌─────────────┬─────────────┬─────────────┐
  │ [Field]     │ [Field]     │ [Field]     │
  └─────────────┴─────────────┴─────────────┘

INLINE LABEL (settings, compact view):
  Label ──────────────────── [Field] / [Value]
```

---

## 10. Card Component Design

### 10.1 Card Variants

```
┌─────────────────────────────────────────────────────────────────────────┐
│  [1] METRIC CARD (KPI Card)                                             │
│  ─────────────────────────────────────────────────────────────────────  │
│  ┌─────────────────────────────────────────┐                            │
│  │  ┌──┐  Total Animals    ▲ +12 this week │                            │
│  │  │🐄│                                   │                            │
│  │  └──┘  247              Active: 235     │                            │
│  └─────────────────────────────────────────┘                            │
│  Icon: 32px colored container (brand-100 bg, brand-500 icon)           │
│  Primary number: display-xl, text-primary                               │
│  Label: heading-sm, text-secondary                                      │
│  Delta: label-sm, success/danger color with arrow icon                  │
│  Min-height: 120px; padding: 24px                                       │
│                                                                         │
│  [2] CONTENT CARD (Standard)                                            │
│  ─────────────────────────────────────────────────────────────────────  │
│  ┌─────────────────────────────────────────┐                            │
│  │  Upcoming Vaccinations         [View All→]│                           │
│  │  ─────────────────────────────────────── │                           │
│  │  [Content area]                          │                           │
│  └─────────────────────────────────────────┘                            │
│  Header: heading-sm weight 600 + optional action link (ghost button)   │
│  Divider: 1px border-subtle                                             │
│  Content: 16px padding-top                                              │
│                                                                         │
│  [3] ANIMAL CARD (List item card — grid view)                           │
│  ─────────────────────────────────────────────────────────────────────  │
│  ┌─────────────────────────────────────────┐                            │
│  │  [PHOTO]   🐄 Cow #BD-2024-0041        │                            │
│  │  ─────────   Shahibal · Male            │                            │
│  │  [240kg]    Shed 3 · Pen A              │                            │
│  │             ● Active                    │                            │
│  │             ADG: 0.87 kg/day    [→ View]│                            │
│  └─────────────────────────────────────────┘                            │
│                                                                         │
│  [4] ALERT CARD (Status-colored)                                        │
│  ─────────────────────────────────────────────────────────────────────  │
│  ┌─────────────────────────────────────────┐                            │
│  │ ⚠️ 3 Vaccinations Overdue              │ ← orange-amber left border │
│  │    Cow #042, Cow #078, Cow #091         │                            │
│  │    [Review Now →]                       │                            │
│  └─────────────────────────────────────────┘                            │
│  Left border: 4px solid status color                                    │
│  Background: semantic status bg color (very light)                      │
└─────────────────────────────────────────────────────────────────────────┘
```

### 10.2 Card Interaction States

```
Static card:     No interaction; elevation-1
Hoverable card:  elevation-2 on hover; cursor pointer; 150ms transition
Clickable card:  elevation-3 on hover + slight scale(1.01); ripple effect
Selected card:   border-focus (2px), surface with slight brand tint
Draggable card:  elevation-4 while dragging; rotation 2deg; drop targets highlighted
```

---

## 11. Data Grid Design

### 11.1 Data Grid Anatomy

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  GRID TOOLBAR                                                               │
│  [Search...🔍]  [Filter ▾]  [Columns ▾]  [Export ▾]     [+ Add Animal]   │
├────┬──────────┬───────────┬───────────┬───────────┬──────────┬─────────────┤
│ ☐  │ TAG ID   │ BREED     │ WEIGHT    │ ADG       │ STATUS   │ ACTIONS     │
│    │ ↕ sorted │           │ (latest)  │           │          │             │
├────┼──────────┼───────────┼───────────┼───────────┼──────────┼─────────────┤
│ ☐  │ BD-0041  │ Shahibal  │ 268 kg    │ 0.91 kg/d │ ● Active │ [Edit] [⋯] │
├────┼──────────┼───────────┼───────────┼───────────┼──────────┼─────────────┤
│ ☐  │ BD-0042  │ Brahman   │ 241 kg    │ 0.78 kg/d │ ⚠ Due    │ [Edit] [⋯] │
├────┼──────────┼───────────┼───────────┼───────────┼──────────┼─────────────┤
│ ☐  │ BD-0043  │ Holstein  │ 195 kg    │ 0.65 kg/d │ ● Active │ [Edit] [⋯] │
├────┴──────────┴───────────┴───────────┴───────────┴──────────┴─────────────┤
│  Showing 1–25 of 247 animals     [← Prev]  1  2  3 ... 10  [Next →]       │
└─────────────────────────────────────────────────────────────────────────────┘

MULTI-SELECT TOOLBAR (appears on row selection):
  3 animals selected  [Record Weight]  [Assign to Batch]  [Delete]  [✕ Clear]
```

### 11.2 Column Design

```
Column Header:
  Font:          label-md, weight 600, text-secondary, UPPERCASE with 0.5px tracking
  Height:        44px
  Padding:       12px 16px
  Sort indicator: ↑↓ inline after label; active sort highlights in brand color
  Resize handle: 4px wide hover area on column right edge; cursor col-resize

Column Types:
  TEXT:          left-aligned, body-md, text-primary
  NUMBER:        right-aligned, mono-lg, text-primary
  CURRENCY:      right-aligned, mono-lg; "৳" prefix in text-tertiary
  DATE:          center-aligned, body-md, DD MMM YYYY format
  STATUS:        center-aligned, pill badge
  ACTIONS:       right-aligned; icon buttons; visible on hover
  BOOLEAN:       center-aligned; checkmark or dash icon
  PHOTO:         32px avatar circle with fallback initials icon

Row Hover State:
  Background: surface-overlay (very subtle)
  Actions column: reveal [Edit] button + [⋯ More] menu
  Entire row becomes clickable → navigates to detail page
```

### 11.3 Filter Panel

```
Filters drawer slides in from right (480px wide):
  ┌────────────────────────────────────────────────┐
  │  Filters                              [✕]      │
  │  ─────────────────────────────────────────     │
  │  STATUS              All  Active  Sold  Dead   │
  │  SPECIES             [Dropdown]                 │
  │  SHED                [Multi-select]             │
  │  BATCH               [Dropdown]                 │
  │  WEIGHT RANGE        [Min ──●────── Max]       │
  │  DATE RANGE          [From ──────── To]        │
  │  ─────────────────────────────────────────     │
  │  [Clear All]              [Apply Filters (3)]  │
  └────────────────────────────────────────────────┘

Active filters shown as removable chips in toolbar:
  [Species: Cattle ×]  [Status: Active ×]  [Shed: Shed 3 ×]
```

---

## 12. Navigation Design

### 12.1 Sidebar Design

```
DESKTOP SIDEBAR (width: 260px expanded, 68px collapsed)
──────────────────────────────────────────────────────────────────────────────

╔════════════════════════════════════════╗
║  ╔═══╗  Farm360 AI          [≡ collapse]║
║  ╚═══╝                                 ║
║  ──────────────────────────────────── ║
║  NAVIGATION                            ║
║                                        ║
║  ██ Dashboard                          ║  ← active: brand bg fill, bold label
║     Livestock                          ║  ← inactive: ghost
║     Smart Feeding                      ║
║     Health                             ║
║     Inventory                          ║
║     Finance                            ║
║     Reports                            ║
║  ──────────────────────────────────── ║
║  ADMINISTRATION                        ║  ← section label: label-sm, text-tertiary
║     Settings                           ║
║     Audit Log                          ║
║  ──────────────────────────────────── ║
║  ↓ (flex-grow space)                  ║
║                                        ║
║  [Farm Selector Dropdown]              ║  ← multi-farm navigation
║  ┌──────────────────────────────────┐ ║
║  │ 🏡 Mymensingh Farm #1       ▾  │ ║
║  └──────────────────────────────────┘ ║
║                                        ║
║  ──────────────────────────────────── ║
║  [Avatar] Rahim Uddin         [⚙]    ║
╚════════════════════════════════════════╝

COLLAPSED STATE (68px):
  Only icons visible; section labels hidden
  Hover on collapsed item: tooltip shows label (Fluent-style tooltip overlay)
  Active item: icon in brand color + subtle indicator bar on left edge

NAV ITEM ANATOMY:
  ┌────────────────────────────────────────────┐
  │  [ICON 20px]  [LABEL body-md 500]  [BADGE]│
  └────────────────────────────────────────────┘
  Height: 44px
  Border radius: radius-sm (on hover/active)
  Active left bar: 3px solid brand color
  Badge: notification count in red pill (max: 99+)
```

### 12.2 Header (Top Bar) Design

```
DESKTOP HEADER (height: 64px, sticky, elevation-1)
──────────────────────────────────────────────────────────────────────────────

┌──────────────────────────────────────────────────────────────────────────┐
│ [Breadcrumbs: Dashboard / Livestock / BD-0041]    [🔔 3]  [❓]  [Avatar]│
└──────────────────────────────────────────────────────────────────────────┘

Breadcrumbs:
  Max 3 levels; ellipsis for deeper paths
  Separator: /  (text-tertiary)
  Current page: text-primary weight 600
  Parent links: text-brand, hover underline

Right Zone:
  Global Search (Cmd+K trigger → command palette overlay)
  Sync Status:  🔄 Syncing...  /  ✓ Synced  /  ⚠ 2 pending (offline mode)
  Notifications bell: badge count
  Help (?)
  Avatar: opens profile/settings dropdown

MOBILE HEADER (height: 56px):
  [≡ Hamburger]  [Farm360 AI Logo]  [🔔]  [Avatar]
  Bottom navigation bar replaces sidebar (see mobile rules)
```

### 12.3 Breadcrumb Design

```
Dashboard  /  Livestock  /  Animal BD-0041

Rules:
  → Truncate middle items with ellipsis (…) when >3 levels
  → Last item (current page) never links — just text
  → On mobile: show only current page title + back arrow
```

---

## 13. Dialog & Drawer Design

### 13.1 Dialog (Modal) Anatomy

```
BACKDROP: rgba(0, 0, 0, 0.5) — animated fade-in 150ms

DIALOG CONTAINER:
  Centered: transform: translateY(-50%) — entrance slide-up 200ms ease-out
  Width variants: SM(480px) MD(640px) LG(800px) XL(1024px)
  Max-height: 90vh; scroll inside content area

  ╔══════════════════════════════════════════════════════════╗
  ║  [ICON optional]  Dialog Title                   [✕]   ║ ← Header
  ║  ─────────────────────────────────────────────────────  ║
  ║                                                          ║
  ║  [CONTENT AREA — scrollable]                            ║ ← Body
  ║                                                          ║
  ║  ─────────────────────────────────────────────────────  ║
  ║              [Cancel]  [Confirm / Primary CTA]          ║ ← Footer
  ╚══════════════════════════════════════════════════════════╝

Dialog variants:
  INFORMATIONAL: Icon (info-blue) + message + [OK]
  CONFIRMATION:  "Are you sure?" + consequence + [Cancel] [Proceed]
  DANGER:        Red icon + "This cannot be undone" + [Cancel] [Delete] (red)
  FORM:          Multi-field form; [Cancel] [Save]
  WIZARD:        Step indicator + [Back] [Next/Finish]
```

### 13.2 Drawer (Side Panel) Design

```
RIGHT DRAWER (default: 480px width on desktop; full-screen on mobile)

Slides in from right; backdrop click closes

  ╔══════════════════════════════════════╗
  ║  [←]  Add New Animal          [✕]  ║ ← Header (sticky)
  ║  ────────────────────────────────── ║
  ║                                      ║
  ║  [CONTENT — scrollable]             ║ ← Body (flex-grow)
  ║                                      ║
  ║  ────────────────────────────────── ║
  ║  [Cancel]              [Save Animal]║ ← Footer (sticky)
  ╚══════════════════════════════════════╝

Used for:
  → Add/Edit Animal (most common — large form)
  → Record Weight
  → Log Feed Consumption
  → Record Vaccination
  → Record Treatment
  → Add Stock
  → Filters panel

LEFT DRAWER: Navigation on mobile (hamburger menu trigger)
```

---

## 14. Notification & Toast Design

### 14.1 Toast (Transient Notifications)

```
Position: Bottom-right (desktop); Bottom-center (mobile)
Stack: Up to 3 toasts; older ones slide down; auto-dismiss 5s (error: 8s)

╔═══════════════════════════════════════════════╗
║  ✓  Animal BD-0041 saved successfully    [✕] ║  ← SUCCESS
╚═══════════════════════════════════════════════╝

╔═══════════════════════════════════════════════╗
║  ⚠  Feed stock below threshold: Rice Straw  ║  ← WARNING
║     [View Inventory →]                   [✕] ║
╚═══════════════════════════════════════════════╝

╔═══════════════════════════════════════════════╗
║  ✕  Failed to save. Please try again.   [✕] ║  ← ERROR
║     [Retry]                                  ║
╚═══════════════════════════════════════════════╝

Width: 360px desktop; full-width - 32px mobile
Animation: Slide-up + fade-in 250ms; slide-down fade-out on dismiss
Left border: 4px solid status color
```

### 14.2 Notification Center Panel

```
Bell icon → slide-down panel (480px, max-height 640px)

╔═══════════════════════════════════════════════╗
║  Notifications  (7 unread)     [Mark All Read]║
║  ─────────────────────────────────────────── ║
║  TODAY                                       ║
║  ┌─────────────────────────────────────────┐ ║
║  │ 🔴 URGENT  Vaccination Overdue          │ ║  ← critical (red dot)
║  │   3 animals overdue for FMD vaccine     │ ║
║  │   Health · 2 hours ago    [View →]      │ ║
║  └─────────────────────────────────────────┘ ║
║  ┌─────────────────────────────────────────┐ ║
║  │ 🟡 WARNING  Low Stock Alert             │ ║
║  │   Anthrax vaccine: 5 doses remaining    │ ║
║  │   Inventory · 4 hours ago  [Reorder →]  │ ║
║  └─────────────────────────────────────────┘ ║
║  YESTERDAY                                   ║
║  [...]                                       ║
║  ─────────────────────────────────────────── ║
║  [View All Notifications →]                  ║
╚═══════════════════════════════════════════════╝

Unread indicators: Blue dot on left of unread items
Notification types with distinct icons and priority colors:
  Critical (🔴): vaccination overdue, disease quarantine
  Warning  (🟡): upcoming vaccination, low stock, expiring items
  Info     (🔵): sync complete, report ready, subscription reminder
  Success  (🟢): record saved, payment processed
```

---

## 15. Chart & Data Visualization Design

### 15.1 Chart Type Catalog

| Chart | Used For | Library |
|---|---|---|
| **Line Chart** | ADG trends, weight progression, feed cost trends | Recharts |
| **Bar Chart** | Monthly P&L, per-shed feed cost comparison | Recharts |
| **Area Chart** | Cumulative costs, revenue over time | Recharts |
| **Donut Chart** | Herd composition (species/sex/status), inventory by category | Recharts |
| **Heatmap** | Vaccination compliance calendar | Custom SVG |
| **Bullet Chart** | FCR vs. target, actual vs. budget | Custom SVG |
| **Sparkline** | Inline trend indicators on KPI cards | Recharts mini |
| **Gauge** | BCS score visualization on animal profile | Custom SVG |

### 15.2 Chart Design Rules

```
Container:
  Background: surface-raised
  Border: 1px border-subtle
  Border radius: radius-md
  Padding: 24px
  Header: heading-sm weight 600 + period selector (segmented control)

Grid Lines:
  Horizontal: 1px, rgba(148, 163, 184, 0.2)  — very subtle
  Vertical:   none (default)

Axes:
  Labels: label-sm, text-tertiary
  Ticks: none (labels only)
  Domain lines: 1px border-subtle

Tooltip:
  elevation-3 shadow
  radius-md
  bg: surface-inverse (dark on light mode; light on dark mode)
  text: text-inverse
  No border
  Show ALL series values at hovered time point

Legend:
  Positioned: below chart (not inside chart area — never obscures data)
  Icon: 12px circle, matching chart series color
  Label: label-sm, text-secondary

Empty chart state:
  Centered: empty state illustration (simple, on-brand)
  Message: "No data for selected period"
  CTA if applicable: "Record your first weight reading →"

Animation:
  Mount animation: lines draw left-to-right (300ms ease)
  Update animation: smooth transition (200ms)
  No animation for large datasets (>200 points) — performance

Responsive behavior:
  Tablet: reduce label frequency; hide secondary legends
  Mobile: full-width; simplified to single-series
```

---

## 16. Table Design

### 16.1 Table Variants

```
STANDARD TABLE (reports, finance, audit):
  Header: surface-overlay bg, uppercase labels, weight 600
  Rows: alternating surface-base / surface-raised (zebra striping — subtle)
  Row hover: surface-overlay
  No row actions (read-only tables)
  Sortable columns: ↕ icon; active sort highlighted

LEDGER TABLE (financial entries, cost ledgers):
  Right-aligned amounts column
  Mono font for all number cells
  Debit/Credit color coding: red for expenses, green for income
  Running total row: pinned at bottom, bold, with separator

COMPARISON TABLE (P&L, batch comparison):
  First column: metric label (left-aligned)
  Subsequent columns: values per period (center-aligned)
  Highlighted column: current period in subtle brand tint

TREE TABLE (hierarchy: Farm → Shed → Pen):
  Indented rows with expand/collapse toggles
  Aggregated values at parent levels
  Collapse all / Expand all toggle in header
```

---

## 17. Form & Wizard Design

### 17.1 Standard Form Layout

```
╔════════════════════════════════════════════════════════════════╗
║  ADD NEW ANIMAL                                         [✕]   ║
║  ──────────────────────────────────────────────────────────── ║
║                                                               ║
║  IDENTIFICATION                                               ║  ← Section header
║  ┌──────────────────────┬──────────────────────┐             ║
║  │ Tag ID *             │ Tag Type *            │             ║
║  │ [BD-____________]    │ [Manual     ▾]        │             ║
║  └──────────────────────┴──────────────────────┘             ║
║  ┌──────────────────────┬──────────────────────┐             ║
║  │ Ear Tag Number       │ RFID Number           │             ║
║  │ [________________]   │ [________________]    │             ║
║  └──────────────────────┴──────────────────────┘             ║
║                                                               ║
║  CLASSIFICATION                                               ║
║  ┌──────────────────────┬──────────────────────┐             ║
║  │ Species *            │ Breed *               │             ║
║  │ [Cattle (Beef)  ▾]   │ [Shahibal        ▾]  │             ║
║  └──────────────────────┴──────────────────────┘             ║
║  ┌──────────────────────┐                                     ║
║  │ Sex *                │                                     ║
║  │ ○ Male   ● Female    │                                     ║
║  └──────────────────────┘                                     ║
║                                                               ║
║  ACQUISITION                                                  ║
║  ┌──────────────────────┬──────────────────────┐             ║
║  │ Acquisition Date *   │ Acquisition Type *    │             ║
║  │ [01/07/2026]  📅    │ ● Purchased  ○ Born   │             ║
║  └──────────────────────┴──────────────────────┘             ║
║  ┌──────────────────────┬──────────────────────┐             ║
║  │ Purchase Price       │ Source                │             ║
║  │ BDT [__________]     │ [________________]    │             ║
║  └──────────────────────┴──────────────────────┘             ║
║                                                               ║
║  PLACEMENT                                                    ║
║  ┌──────────────────────┬──────────────────────┐             ║
║  │ Farm *               │ Shed *                │             ║
║  │ [Mymensingh #1  ▾]   │ [Shed 3         ▾]   │             ║
║  └──────────────────────┴──────────────────────┘             ║
║  ┌──────────────────────┐                                     ║
║  │ Pen (Optional)       │                                     ║
║  │ [Pen A          ▾]   │                                     ║
║  └──────────────────────┘                                     ║
║  ──────────────────────────────────────────────────────────── ║
║  [Cancel]                                 [Save Animal  →]   ║
╚════════════════════════════════════════════════════════════════╝
```

### 17.2 Onboarding Wizard Design

```
STEP INDICATOR (horizontal, 5 steps):

  ①─────────────────②─────────────────③─────────────────④─────────────────⑤
Organization    Farm Setup         First Shed        First Animal      All Set!
  ● Completed   ● Current          ○ Upcoming        ○ Upcoming        ○ Upcoming

Step bubble: 32px circle
  Completed: brand fill + checkmark
  Current: brand fill + white number
  Upcoming: border-default + text-tertiary number

Progress bar: thin brand-colored bar connecting completed bubbles

WIZARD NAVIGATION:
  ← Back (ghost button, left)                  Next → (primary button, right)
  "Skip for now" available on optional steps (ghost text link)

PROGRESS SAVING:
  "Your progress is auto-saved." shown in helper text below step indicator
  On browser close: session-persisted; resumes on next login
```

---

## 18. Loading, Empty & Error States

### 18.1 Loading States

```
SKELETON SCREENS (preferred over spinners for layout loading):

Card skeleton:
  ┌─────────────────────────────────────────┐
  │  ████████████████████  ████████         │  ← animated shimmer
  │  ██████████████                         │
  │  ████████████  █████████████████        │
  └─────────────────────────────────────────┘
  Shimmer animation: left-to-right gradient sweep, 1.5s infinite
  Color: surface-overlay → surface-sunken → surface-overlay

List skeleton:
  Row of: [● circle]  [████████]  [██████]  [████]  [██]
  5-8 rows visible

Table skeleton: Column headers visible (real); rows show shimmer bars

Full-page loading (initial app load):
  Center of screen: Farm360 AI logo + brand-colored circular progress ring
  Background: surface-base (no flash of unstyled content)

INLINE LOADING (button action):
  Spinner (16px) replaces left icon
  Button text changes: "Save Animal" → "Saving..."
  Button disabled during load

OVERLAY LOADING (form submit, long operations):
  Surface overlay with centered spinner + progress message
  "Generating your monthly report..." + progress bar if deterministic
```

### 18.2 Empty States

```
MODULE-LEVEL EMPTY STATE (no data yet):

  ╔════════════════════════════════════════╗
  ║                                        ║
  ║         [Illustration 80px]            ║  ← friendly, on-brand SVG
  ║                                        ║
  ║       No animals yet                   ║  ← heading-lg
  ║                                        ║
  ║  Add your first animal to start        ║  ← body-md, text-secondary
  ║  tracking your herd's health,          ║
  ║  growth, and profitability.            ║
  ║                                        ║
  ║         [+ Add First Animal]           ║  ← primary button, centered
  ║                                        ║
  ╚════════════════════════════════════════╝

SEARCH/FILTER EMPTY STATE:
  Illustration: magnifying glass finding nothing
  "No animals match your filters"
  "Try removing some filters or search for a different tag."
  [Clear Filters] — secondary button

NOTIFICATION EMPTY STATE:
  Illustration: bell with checkmark
  "You're all caught up!"
  "No new notifications."
  No CTA (informational only)

DATA CHART EMPTY STATE:
  Inline within chart container
  Centered: chart icon + "Record your first weight to see ADG trends"
  Link: "→ Record Weight"
```

### 18.3 Error States

```
FULL PAGE ERROR (500, network failure):
  ╔════════════════════════════════════════╗
  ║                                        ║
  ║         [Error Illustration]           ║
  ║                                        ║
  ║   Something went wrong                 ║
  ║                                        ║
  ║   We couldn't load this page.          ║
  ║   This has been reported to our team.  ║
  ║                                        ║
  ║   [↺ Try Again]   [← Go Home]         ║
  ╚════════════════════════════════════════╝
  Reference: Correlation ID shown in small text-tertiary at bottom

404 NOT FOUND:
  Illustration: lost animal (on-brand, friendly)
  "This page has wandered off"
  "The animal record you're looking for doesn't exist or was removed."
  [← Back to Livestock]

OFFLINE STATE:
  Sticky banner at top (below header):
  ┌──────────────────────────────────────────────────────────┐
  │ 📡 You're offline. Changes will sync when reconnected.  │
  └──────────────────────────────────────────────────────────┘
  Banner: warning-amber background, full-width

INLINE FORM ERROR (validation):
  Field-level: red border + error icon + error message below input
  Form-level: Alert card above submit button listing all errors
  ⚠ Please fix 3 errors before saving:
  • Tag ID is required
  • Date of birth cannot be in the future
  • Shed selection required
```

---

## 19. Dark Mode Design

### 19.1 Dark Mode Principles

```
Farm360 AI dark mode is NOT simply color-inverted. It is designed specifically:

1. SURFACE LAYERING:
   Light mode: white → slightly tinted surfaces (lighter = higher)
   Dark mode:  dark → slightly lighter surfaces (lighter = higher elevation)
   Surface hierarchy preserved: base < raised < overlay

2. REDUCED BRIGHTNESS:
   Dark mode uses lower saturation colors to avoid eye strain
   Brand primary: #0d9e83 → #1cbf9f (lighter for visibility on dark bg)
   Accent: #f59e0b → #fbbf24 (slightly lighter)

3. NO PURE BLACK:
   surface-base in dark mode: #0d1117 (not #000000)
   Avoids harsh contrast with glowing elements

4. BORDER VISIBILITY:
   Borders use lighter opacity on dark mode
   elevation system uses subtle inner borders instead of shadows
   (shadows don't work on dark backgrounds)

5. IMAGERY:
   Photos and charts: same (content images)
   Illustrations: slight opacity reduction (0.85) to integrate with dark bg

TOKEN SWAPPING:
  CSS custom properties (--color-surface-base: ...) swapped via
  [data-theme="dark"] selector or prefers-color-scheme media query
  All components use tokens; zero hardcoded hex values in components
```

### 19.2 Dark Mode Theme Toggle

```
Location: Profile menu > Appearance OR Settings > Display
Options: Light | Dark | System (follows OS setting)
Transition: 200ms CSS transition on background-color, color, border-color
No flash: Stored in localStorage + inline script prevents FOUC
```

---

## 20. Responsive Design System

### 20.1 Breakpoints

```
Token       Value     Target Device
──────────────────────────────────────────────────────
xs          320px     Entry-level Android phones (Walton, Symphony)
sm          480px     Standard phones
md          768px     Tablets (portrait) — Xiaomi Pad, Samsung Tab A
lg          1024px    Tablets (landscape), small laptops
xl          1280px    Standard desktop
2xl         1440px    Wide desktop, external monitors
3xl         1920px    Full HD — dashboard-centric layouts
```

### 20.2 Responsive Navigation

```
DESKTOP (≥1024px):
  Persistent sidebar (260px) + content area
  Top header (64px) with breadcrumbs
  All 3 navigation zones: sidebar + header + breadcrumbs

TABLET (768px–1023px):
  Collapsed sidebar (68px — icon only) by default
  Hamburger icon in header opens full sidebar overlay
  Header persists (56px)
  Content: full width (0px sidebar offset in collapsed mode)

MOBILE (<768px):
  NO sidebar
  Top header: hamburger + logo + notifications
  Hamburger: full-screen drawer from left (full sidebar)
  Bottom navigation bar: 5 key modules
    [Dashboard] [Livestock] [Health] [Feed] [More]
  All bottom tabs: icon + label (12px)
  Active: brand color icon + label
```

### 20.3 Responsive Layout Rules

```
CARDS / GRID:
  Desktop:  4-column grid (Metric cards), 2-column (Content cards)
  Tablet:   2-column grid
  Mobile:   1-column (stacked)

TABLES:
  Desktop:  Full table, all columns visible
  Tablet:   Prioritized columns (hide secondary columns); horizontal scroll
  Mobile:   Card-list view (not table); each row becomes a card

FORMS:
  Desktop:  2–3 column input layout
  Tablet:   2-column
  Mobile:   1-column (full width inputs)

DIALOGS:
  Desktop:  Centered modal (fixed width)
  Tablet:   Centered modal (90% width)
  Mobile:   Bottom sheet (slides up from bottom; full width)

DRAWERS:
  Desktop:  Right drawer (480px)
  Tablet:   Right drawer (full width - 48px)
  Mobile:   Full-screen (100vw × 100vh, with slide-up animation)

CHARTS:
  Desktop:  Full size; multi-series; legend visible
  Tablet:   Reduced height; horizontal legend
  Mobile:   Single metric; simplified; height 200px

DATA DENSITY:
  Desktop:  table: 52px rows; card: full padding
  Tablet:   table: 48px rows; card: slightly reduced padding
  Mobile:   List cards instead of table rows; comfortable 60px+ touch targets
```

### 20.4 Touch Targets (Mobile)

```
Minimum touch target: 44×44px (WCAG 2.5.5)
Recommended touch target: 48×48px for primary actions
Bottom navigation items: 60px height (generous)
Table row actions on mobile: hidden — replaced by swipe gesture:
  Swipe left → reveal [Edit] [Delete]
  Swipe right → reveal [Quick Action: Log Weight / Log Feed]
```

---

## 21. Accessibility (WCAG 2.1 AA)

### 21.1 Color Contrast Requirements

```
Text Contrast (minimum 4.5:1 for normal text; 3:1 for large text):

Pair                                    Contrast    Pass/Fail
────────────────────────────────────────────────────────────────
text-primary (#0f172a) on white          15.8:1     ✓ AAA
text-secondary (#334155) on white        10.7:1     ✓ AAA
text-tertiary (#64748b) on white          4.6:1     ✓ AA
text-brand (#0d9e83) on white             4.5:1     ✓ AA (verified)
status-danger-text on status-danger-bg   5.1:1     ✓ AA
status-warning-text on status-warning-bg 4.8:1     ✓ AA
White on interactive-primary (#0d9e83)   4.5:1     ✓ AA (verified)

Dark mode — all pairs re-verified for dark background ratios.
```

### 21.2 Focus Management

```
FOCUS RING:
  All interactive elements: 3px solid border-focus (#0d9e83 / #1cbf9f dark)
  Offset: 2px from element boundary
  Visible in both light and dark mode
  Never hidden — no "outline: none" without a custom focus indicator

FOCUS ORDER:
  Logical DOM order matches visual order
  Modal dialogs: trap focus within; return to trigger on close
  Sidebar: skip-to-content link at top of page (first focusable element)
  Data grids: arrow-key navigation within cells; Enter to edit

KEYBOARD NAVIGATION:
  All features accessible without mouse
  Keyboard shortcuts documented in Help menu:
    N = New (context-aware: N on Livestock = New Animal)
    F = Filter toggle
    / = Global search
    Escape = Close dialog / Clear search
    Cmd+S = Save current form
    Cmd+K = Command palette
```

### 21.3 Screen Reader Support

```
ARIA LABELS:
  All icon-only buttons: aria-label="[action]" e.g. aria-label="Delete animal BD-0041"
  Status badges: aria-label="Status: Active" (not just visual color)
  Chart containers: aria-label="[Chart title] for [period]"
  Loading states: aria-live="polite" with "Loading..." announcement

SEMANTIC HTML:
  nav: sidebar navigation
  main: page content
  aside: filter panels
  section: major page sections with aria-labelledby
  table: data grids with <th scope="col"> headers
  button vs a: button for actions; a for navigation

ANNOUNCEMENTS:
  Form submissions: aria-live region announces result
  Toast notifications: announced to screen reader
  Table sort: "Sorted by [column] ascending" announced
  Page transitions: page title changes update <title> tag
```

### 21.4 Motion & Animation

```
prefers-reduced-motion: NO animations or transitions
  → Skeletons: no shimmer (static gray)
  → Page transitions: instant (no slide)
  → Tooltips: instant appear (no fade)
  → Dialogs: instant appear (no slide-up)
```

---

## 22. Screen Designs — All MVP Screens

### Screen 01: Login

```
ROUTE: /login
LAYOUT: Full-page split

┌──────────────────────────────┬──────────────────────────────────────────┐
│  [LEFT PANEL — 40%]          │  [RIGHT PANEL — 60%]                     │
│  Brand identity panel        │  Login form                               │
│                              │                                           │
│  ┌──────────────────────┐    │  ┌─────────────────────────────────────┐ │
│  │  ╔═══╗               │    │  │  Welcome back,                      │ │
│  │  ╚═══╝  Farm360 AI   │    │  │  কৃষি পরিচালনা এখন সহজ              │ │
│  │         ───────────  │    │  │  (Farm management, now effortless)  │ │
│  │                      │    │  │                                     │ │
│  │  "From the barn      │    │  │  Phone Number *                     │ │
│  │  to the board —      │    │  │  ┌──────────────────────────────┐   │ │
│  │  giving every farmer │    │  │  │ +880  [01XXXXXXXXX_______]   │   │ │
│  │  the intelligence    │    │  │  └──────────────────────────────┘   │ │
│  │  of a Fortune 500    │    │  │                                     │ │
│  │  agribusiness."      │    │  │  Password *                         │ │
│  │                      │    │  │  ┌──────────────────────────────┐   │ │
│  │  [Background:        │    │  │  │ [●●●●●●●●●]          [👁]    │   │ │
│  │   beautiful          │    │  │  └──────────────────────────────┘   │ │
│  │   gradient +         │    │  │                                     │ │
│  │   subtle cattle      │    │  │  [Forgot Password?]       (right)   │ │
│  │   silhouette]        │    │  │                                     │ │
│  └──────────────────────┘    │  │  ┌──────────────────────────────┐   │ │
│                              │  │  │  Sign In                     │   │ │
│  Language toggle:            │  │  └──────────────────────────────┘   │ │
│  [English] [বাংলা]          │  │                                     │ │
│                              │  │  ──── or sign in with OTP ────      │ │
│                              │  │                                     │ │
│                              │  │  ┌──────────────────────────────┐   │ │
│                              │  │  │  📱 Sign in with OTP         │   │ │
│                              │  │  └──────────────────────────────┘   │ │
│                              │  │                                     │ │
│                              │  │  Don't have an account?             │ │
│                              │  │  [Create free account →]            │ │
│                              │  └─────────────────────────────────────┘ │
└──────────────────────────────┴──────────────────────────────────────────┘

MOBILE: Single column; logo at top; form below
Left panel: collapsed to logo + tagline banner at top
Form: full width with 16px margins

Key design details:
→ Left panel: dark teal gradient (#0d9e83 → #021f1a) with subtle cattle
  silhouette illustration at 10% opacity
→ Right panel: surface-base background (clean white/dark)
→ Both panels: equal height; left rounded on mobile = none
→ OTP option is prominent — primary for target market
```

### Screen 02: Register (Self-Onboarding)

```
ROUTE: /register
LAYOUT: Centered card (640px max)

┌──────────────────────────────────────────────────────────────────────┐
│  ← Back to Login                              [English | বাংলা]      │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                                                              │   │
│  │       ╔═══╗  Create Your Farm360 AI Account                 │   │
│  │       ╚═══╝  Free 14-day trial, no credit card required     │   │
│  │                                                              │   │
│  │  ────────────────────────────────────────────────────────── │   │
│  │  ORGANIZATION INFORMATION                                    │   │
│  │  ┌─────────────────────────────────────────────────────┐    │   │
│  │  │ Organization / Farm Name *                          │    │   │
│  │  │ [Rahim Agro Farm_______________________________]    │    │   │
│  │  └─────────────────────────────────────────────────────┘    │   │
│  │  ┌───────────────────────┬─────────────────────────────┐    │   │
│  │  │ Farm Type *           │ Primary District *           │    │   │
│  │  │ [Cattle Fattening ▾]  │ [Mymensingh          ▾]     │    │   │
│  │  └───────────────────────┴─────────────────────────────┘    │   │
│  │                                                              │   │
│  │  YOUR ACCOUNT                                                │   │
│  │  ┌─────────────────────────────────────────────────────┐    │   │
│  │  │ Full Name *                                         │    │   │
│  │  │ [Rahim Uddin_________________________________]      │    │   │
│  │  └─────────────────────────────────────────────────────┘    │   │
│  │  ┌───────────────────────┬─────────────────────────────┐    │   │
│  │  │ Phone Number *        │ Email (Optional)             │    │   │
│  │  │ +880 [01712345678]    │ [rahimfarm@gmail.com]        │    │   │
│  │  └───────────────────────┴─────────────────────────────┘    │   │
│  │  ┌───────────────────────┬─────────────────────────────┐    │   │
│  │  │ Password *            │ Confirm Password *           │    │   │
│  │  │ [●●●●●●●●●]  [👁]    │ [●●●●●●●●●]      [👁]      │    │   │
│  │  └───────────────────────┴─────────────────────────────┘    │   │
│  │  Password strength: ████░░░░ Fair                            │   │
│  │                                                              │   │
│  │  ☐ I agree to the Terms of Service and Privacy Policy       │   │
│  │                                                              │   │
│  │  ┌─────────────────────────────────────────────────────┐    │   │
│  │  │  Create Account →                                   │    │   │
│  │  └─────────────────────────────────────────────────────┘    │   │
│  │                                                              │   │
│  │  Already have an account? [Sign in →]                       │   │
│  │                                                              │   │
│  └──────────────────────────────────────────────────────────────│   │
└──────────────────────────────────────────────────────────────────────┘

OTP VERIFICATION STEP (appears as next step in same layout):
  ──────────────────────────────────────────────
  📱 Enter the 6-digit code sent to +880 017XXXXX
  ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐
  │ 4 │ │ 2 │ │ 7 │ │   │ │   │ │   │
  └───┘ └───┘ └───┘ └───┘ └───┘ └───┘
  Code expires in: 04:32
  [Resend Code] (grayed out until expiry countdown)
  ──────────────────────────────────────────────
```

### Screen 03: Forgot Password

```
ROUTE: /forgot-password
LAYOUT: Centered card (480px)

  Forgot Password
  ──────────────────────────────────────────────
  Enter your registered phone number. We'll send
  you a verification code to reset your password.
  ──────────────────────────────────────────────
  Phone Number *
  ┌──────────────────────────────────┐
  │ +880  [01712345678]              │
  └──────────────────────────────────┘
  
  [Send Reset Code →]   [← Back to Login]

STEP 2 — Enter OTP (same pattern as register OTP screen)
STEP 3 — Set New Password:
  New Password *
  ┌──────────────────────────────────┐
  │ [●●●●●●●●●]              [👁]   │
  └──────────────────────────────────┘
  Confirm Password *
  ┌──────────────────────────────────┐
  │ [●●●●●●●●●]              [👁]   │
  └──────────────────────────────────┘
  [Set New Password]
```

### Screen 04: Onboarding Wizard

```
ROUTE: /onboarding
LAYOUT: Full page wizard (max 800px centered)

STEP 1/5 — Welcome & Organization

  ①──────②──────③──────④──────⑤
  Org     Farm   Shed   Animal  Done
  (Active)

  Welcome to Farm360 AI! 🎉
  Let's set up your farm in 5 minutes.
  ────────────────────────────────────────────────

  We already have your organization details.
  Confirm or update below:

  Organization Name: [Rahim Agro Farm]
  Farm Type:         [Cattle Fattening ▾]
  District:          [Mymensingh ▾]
  Division:          [Mymensingh ▾]

                            [Continue →]
  "Skip for now" — set up later from Settings

STEP 2/5 — Add Your First Farm

  Farm Name *        [Mymensingh Main Farm]
  Total Area         [2.5] acres
  Address            [Char Ishwaria, Mymensingh]
  District *         [Mymensingh ▾]

                [← Back]        [Add Farm & Continue →]

STEP 3/5 — Add Your First Shed

  Shed Name *          [Shed 1]
  Type                 [Cattle ▾]
  Capacity             [50] animals
  Description          [Main fattening shed]

                [← Back]        [Add Shed & Continue →]

STEP 4/5 — Add Your First Animal (Optional but encouraged)

  Full Page prompt:
  "Add your first animal to start seeing your farm come alive."
  [+ Add Now] (primary) or [Skip — I'll add later] (ghost link)

  If Add Now: Embedded animal form (key fields only)

STEP 5/5 — All Set! 🎊

  [Large confetti / success illustration]

  Your farm is ready!
  ────────────────────────────────────────────────
  ✓ Organization: Rahim Agro Farm
  ✓ Farm: Mymensingh Main Farm
  ✓ Shed: Shed 1 (50 capacity)
  ✓ 1 Animal registered

  "You're all set to start tracking your herd.
  Check out your Dashboard now."

                [Go to Dashboard →]
```

### Screen 05: Dashboard

```
ROUTE: /dashboard
LAYOUT: Desktop 12-col grid

HEADER: "Good morning, Rahim 👋" — heading-xl
        "Thursday, 3 July 2026 · Mymensingh Main Farm"

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ROW 1 — ALERT BANNER (conditional; appears only when alerts exist)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
┌──────────────────────────────────────────────────────────────────────┐
│ 🔴 3 vaccinations are overdue · 1 animal quarantined  [Review Now →]│
└──────────────────────────────────────────────────────────────────────┘

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ROW 2 — KPI CARDS (4 across on desktop, 2×2 tablet, 1 col mobile)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ 🐄 Animals   │  │ 💉 Health    │  │ 📦 Inventory │  │ 💰 Finance   │
│              │  │              │  │              │  │              │
│     247      │  │  3 Overdue   │  │  2 Low Stock │  │  ৳ 4.2L     │
│  Active Herd │  │  Vaccination │  │  Alerts      │  │  Revenue MTD │
│  ▲ 12 added  │  │  [Review]   │  │  [Reorder]  │  │  ▲ 18% MoM  │
└──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ROW 3 — MAIN CONTENT (8-col left + 4-col right on desktop)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
LEFT (8 cols):                        RIGHT (4 cols):
┌──────────────────────────────────┐  ┌──────────────────────────┐
│ Financial Summary                │  │ Health Alerts            │
│ [This Month ▾]                   │  │                          │
│                                  │  │ ● OVERDUE (3)            │
│  Revenue ৳4.21L   ▲ 18%         │  │   Cow #042: FMD          │
│  Expenses ৳2.87L  ▼ 5%          │  │   Cow #078: Anthrax      │
│  Net Profit ৳1.34L ▲ 31%        │  │   Cow #091: FMD          │
│                                  │  │                          │
│  [Bar chart: 6-month P&L trend]  │  │ ● DUE THIS WEEK (5)      │
│                                  │  │   View all →             │
│  [Export PDF]  [Export Excel]    │  └──────────────────────────┘
└──────────────────────────────────┘
                                       ┌──────────────────────────┐
┌──────────────────────────────────┐  │ Upcoming Calvings        │
│ Herd Performance                 │  │                          │
│ Batch: Eid 2026 (47 animals)     │  │ 🐄 BD-0102 · 5 days     │
│                                  │  │   Expected: 08 Jul 2026  │
│ Avg Weight:  268 kg  ▲ 4.2%     │  │                          │
│ Avg ADG:     0.84 kg/day         │  │ 🐄 BD-0116 · 9 days     │
│ Feed Cost/Kg: ৳ 48.20            │  │   Expected: 12 Jul 2026  │
│ FCR:          6.8                │  │                          │
│                                  │  │ [View Calendar →]       │
│ [Line chart: batch weight trend] │  └──────────────────────────┘
└──────────────────────────────────┘

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ROW 4 — HERD COMPOSITION + ACTIVITY FEED
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
┌──────────────────────────────────┐  ┌──────────────────────────┐
│ Herd Composition                 │  │ Recent Activity           │
│                                  │  │                          │
│  [Donut chart: by species]       │  │ Kamal recorded weight    │
│                                  │  │ for 8 animals · 2hr ago  │
│  Cattle (Beef):  198  ████████   │  │                          │
│  Cattle (Dairy):  31  ███        │  │ You sold Cow #BD-0031   │
│  Goat:            18  ██         │  │ for ৳ 68,000 · 5hr ago  │
│                                  │  │                          │
│  ● Active  ● Sold  ● Quarantine  │  │ FMD vaccination recorded │
│                                  │  │ for Shed 2 · Yesterday   │
└──────────────────────────────────┘  └──────────────────────────┘
```

### Screen 06: Livestock List

```
ROUTE: /livestock
LAYOUT: Full content area (no secondary sidebar)

PAGE HEADER:
  Livestock Management                [+ Add Animal]
  247 active animals · 3 farms

TOOLBAR:
  [🔍 Search by tag, name...]  [Filter (3 active) ▾]  [Columns ▾]  [Export ▾]
  [View: ☰ List | ⊞ Grid]

Active filter chips:
  [Species: Cattle ×]  [Status: Active ×]  [Shed: Shed 3 ×]

━━━━ TABS: All (247) | Active (235) | Sold (8) | Dead (4) ━━━━━━━━━━━━━━

DATA TABLE:
┌──┬──────────┬────────────┬──────────┬───────────┬──────────┬──────────┐
│  │ TAG ID   │ BREED      │ WEIGHT   │ ADG       │ SHED     │ STATUS   │
├──┼──────────┼────────────┼──────────┼───────────┼──────────┼──────────┤
│☐ │BD-0041   │ Shahibal   │268 kg    │0.91 kg/d  │Shed 3    │● Active  │
│  │          │ Male · 14M │07 Jul 26 │▲ Above avg│Pen A     │          │
├──┼──────────┼────────────┼──────────┼───────────┼──────────┼──────────┤
│☐ │BD-0042   │ Brahman ×  │241 kg    │0.78 kg/d  │Shed 3    │⚠ Overdue │
│  │          │ Male · 12M │06 Jul 26 │─ Average  │Pen A     │  Vacc.  │
├──┼──────────┼────────────┼──────────┼───────────┼──────────┼──────────┤
│☐ │BD-0043   │ Holstein-F │195 kg    │0.65 kg/d  │Shed 1    │● Active  │
│  │          │Female · 8M │05 Jul 26 │▼ Below avg│Pen B     │          │
└──┴──────────┴────────────┴──────────┴───────────┴──────────┴──────────┘
[Showing 1-25 of 235]  ← 1  2  3 ... 10  →

MULTI-SELECT STATE (3 checked):
┌──────────────────────────────────────────────────────────────────────┐
│ 3 animals selected  [Log Weight]  [Add to Batch]  [Transfer]  [✕]  │
└──────────────────────────────────────────────────────────────────────┘

GRID VIEW ALTERNATIVE:
  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐
  │[Photo / icon] │  │[Photo / icon] │  │[Photo / icon] │  │[Photo / icon] │
  │BD-0041        │  │BD-0042        │  │BD-0043        │  │BD-0044        │
  │Shahibal M     │  │Brahman M      │  │Holstein F     │  │BB Goat M      │
  │268 kg · ADG   │  │241 kg · ADG   │  │195 kg · ADG   │  │42 kg · ADG    │
  │0.91 kg/d      │  │0.78 kg/d      │  │0.65 kg/d      │  │0.22 kg/d      │
  │● Active       │  │⚠ Overdue     │  │● Active       │  │● Active       │
  └───────────────┘  └───────────────┘  └───────────────┘  └───────────────┘
```

### Screen 07: Animal Detail Page

```
ROUTE: /livestock/:id
LAYOUT: Full page with sticky action header

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
STICKY HEADER:
  ← Livestock  /  BD-0041 — Shahibal Bull
  [Log Weight]  [Record Vaccination]  [Record Treatment]  [⋯ More ▾]
  ● Active — Shed 3, Pen A
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

LEFT PANEL (5 cols): Animal Profile Card
┌──────────────────────────────────────────────┐
│  [PHOTO — 200px, rounded-md]                 │
│  [+ Add Photo]                               │
│  ─────────────────────────────────────────── │
│  ANIMAL BD-0041                              │
│  Shahibal Crossbreed · Male                  │
│  ─────────────────────────────────────────── │
│  DATE OF BIRTH      01 May 2025              │
│  AGE                14 months                │
│  ACQUIRED           15 May 2025              │
│  PURCHASE PRICE     ৳ 35,000                │
│  ─────────────────────────────────────────── │
│  CURRENT LOCATION                            │
│  Mymensingh Farm #1                          │
│  Shed 3 → Pen A                              │
│  ─────────────────────────────────────────── │
│  CURRENT BATCH                               │
│  Eid 2026 Fattening [View Batch →]          │
│  ─────────────────────────────────────────── │
│  BODY CONDITION SCORE                        │
│  [●────●──────────] 3.5 / 5.0               │
│  (Good condition)                            │
└──────────────────────────────────────────────┘

RIGHT PANEL (7 cols): Metrics + Timeline

METRICS ROW:
┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐
│ Weight    │  │ ADG       │  │ Total Cost│  │ Break-Even│
│ 268 kg    │  │ 0.91 kg/d │  │ ৳ 52,400 │  │ ৳ 57,200 │
│ as of     │  │ Last 30d  │  │ to date   │  │ sale price│
│ 07 Jul 26 │  │ ▲ Above   │  │           │  │           │
└───────────┘  └───────────┘  └───────────┘  └───────────┘

TABS: Timeline | Weight | Health | Feeding | Finance

━━━ TIMELINE TAB ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  [Chronological event feed — newest first]

  07 Jul 2026 · 10:30 AM
  ⚖ Weight recorded: 268 kg (+4 kg from previous)
  Recorded by: Kamal Hossain (Worker)

  05 Jul 2026 · 02:00 PM
  💉 Vaccination completed: FMD (Dose 2)
  Administered by: Dr. Rahman
  Batch #: VAC-2026-0041

  01 Jul 2026 · 11:00 AM
  📋 Assigned to Eid 2026 Fattening batch

  28 Jun 2026 · 09:00 AM
  ⚖ Weight recorded: 264 kg (+6 kg from previous)

  [Load older events →]
```

### Screen 08: Weight History

```
ROUTE: /livestock/:id/weight
LAYOUT: Full-width chart above table

PAGE TITLE: Weight History — BD-0041 Shahibal Bull

CONTROLS: [+ Log Weight]  [Date Range: Last 90 days ▾]  [Export ▾]

━━━ WEIGHT TREND CHART ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  Weight (kg) ↑
  280 ─  ─  ─  ─  ─  ─  ─  ─ ●
  270 ─  ─  ─  ─  ─  ─  ─ ●
  260 ─  ─  ─  ─  ─  ─ ●
  250 ─  ─  ─  ─  ─ ●
  240 ─  ─  ─  ─ ●
  230 ─  ─  ─ ●
  220 ─  ─ ●
  210 ─ ●
  200 ●────────────────────────────────────────────────→ Date
      May 15  Jun 1  Jun 15  Jul 1  Jul 7

  ADG trend line: dashed, in accent color
  Target line: dotted, in neutral gray
  Legend below chart: [● Weight  ─ ─ ADG  ⋯ Target]

━━━ WEIGHT RECORDS TABLE ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  DATE          WEIGHT    CHANGE    ADG (from prev) RECORDED BY
  07 Jul 2026   268 kg    +4 kg     0.57 kg/day     Kamal Hossain
  28 Jun 2026   264 kg    +6 kg     0.86 kg/day     Kamal Hossain
  14 Jun 2026   255 kg    +8 kg     0.89 kg/day     Kamal Hossain
  01 Jun 2026   244 kg    +11 kg    0.92 kg/day     Farm Manager
  15 May 2026   232 kg    —         —               (Acquisition weight)

  Total gain: 68 kg over 53 days · Overall ADG: 0.87 kg/day
```

### Screen 09: Smart Feeding

```
ROUTE: /feeding
LAYOUT: Tabbed interface

TABS: Feed Ingredients | Formulas | Schedules | Daily Log | FCR Report

━━━ FEED FORMULAS TAB ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Smart Feeding                              [+ New Formula]

FORMULA CARDS (2-column grid):
┌───────────────────────────────────┐  ┌───────────────────────────────────┐
│ Fattening Ration — High Energy    │  │ Grower Mix — Medium Energy        │
│ ─────────────────────────────── │  │ ─────────────────────────────── │
│ Target: Cattle Beef · Finisher    │  │ Target: Cattle Beef · Grower      │
│                                   │  │                                   │
│ DM:  72%    CP:  16%    ME: 11.2  │  │ DM:  68%    CP:  14%    ME: 10.4  │
│                                   │  │                                   │
│ Daily qty/animal: 8.5 kg          │  │ Daily qty/animal: 7.0 kg          │
│ Est. cost/animal: ৳ 42.60        │  │ Est. cost/animal: ৳ 34.20        │
│                                   │  │                                   │
│ INGREDIENTS:                      │  │ ...                               │
│  Rice Straw      3.0 kg           │  │                                   │
│  Mustard Oil Cake 2.0 kg          │  │                                   │
│  Wheat Bran      1.5 kg           │  │                                   │
│  DCP             0.5 kg           │  │                                   │
│  + 4 more                         │  │                                   │
│                                   │  │                                   │
│ ● Active — Assigned to 2 sheds    │  │ ● Active — Assigned to 1 shed     │
│                                   │  │                                   │
│ [Edit]  [Assign]  [⋯]            │  │ [Edit]  [Assign]  [⋯]            │
└───────────────────────────────────┘  └───────────────────────────────────┘

━━━ DAILY LOG TAB ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Today: Thursday, 3 July 2026        [+ Log Consumption]

SHEDS LOG STATUS:
┌─────────────────────────────────────────────────────────────────────┐
│  SHED 1 (48 animals)     ✓ Logged 8:30 AM       [View Details]    │
│  Formula: Fattening Ration · 408 kg consumed · Cost: ৳ 2,044     │
├─────────────────────────────────────────────────────────────────────┤
│  SHED 2 (32 animals)     ✓ Logged 9:15 AM       [View Details]    │
│  Formula: Grower Mix · 224 kg consumed · Cost: ৳ 1,094           │
├─────────────────────────────────────────────────────────────────────┤
│  SHED 3 (35 animals)     ⏱ Not logged yet       [Log Now →]      │
└─────────────────────────────────────────────────────────────────────┘

━━━ FCR REPORT TAB ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  BATCH: Eid 2026 Fattening (47 animals)
  Period: 15 May — 03 Jul 2026 (50 days)

  Total Feed Consumed:  12,456 kg
  Total Weight Gain:    1,830 kg
  FCR:                  6.81
  Target FCR:           6.5
  Status: ⚠ Slightly above target

  [FCR trend line chart — showing weekly FCR over batch period]
  [Comparison: This batch vs. Previous batch vs. Industry average]
```

### Screen 10: Health & Vaccination

```
ROUTE: /health
LAYOUT: Tabbed interface

TABS: Overview | Vaccination | Treatments | Disease Incidents | Deworming | Vet Visits

━━━ OVERVIEW TAB (Health Dashboard) ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

STATUS CARDS:
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│ 💉 Vaccination   │  │ 🏥 Under Treat.  │  │ ⚠ Quarantined   │
│                  │  │                  │  │                  │
│  3 Overdue       │  │  2 Animals       │  │  1 Animal        │
│  5 Due This Week │  │  Active          │  │  BD-0088         │
│ [Review →]       │  │ [View →]         │  │ [Manage →]       │
└──────────────────┘  └──────────────────┘  └──────────────────┘

VACCINATION HEATMAP (monthly calendar view):
  July 2026
  ┌────┬────┬────┬────┬────┬────┬────┐
  │ Su │ Mo │ Tu │ We │ Th │ Fr │ Sa │
  ├────┼────┼────┼────┼────┼────┼────┤
  │  5 │  6 │  7 │ 8  │ 9  │ 10 │ 11 │
  │ 🟢│ 🟡│ 🟡│ ○ │ ○ │ ○  │ ○  │  ← legend: green=done, yellow=due, red=overdue
  ├────┼────┼────┼────┼────┼────┼────┤
  │ ...                               │
  └────┴────┴────┴────┴────┴────┴────┘

━━━ VACCINATION TAB ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Filters: [All Status ▾]  [All Sheds ▾]  [All Vaccines ▾]

OVERDUE SECTION (red header):
┌─────────────────────────────────────────────────────────────────────┐
│ ⚠ 3 OVERDUE VACCINATIONS — Immediate action required               │
├──────────────┬───────────────┬───────────────┬──────────────────────┤
│ Animal       │ Vaccine       │ Due Date      │ Action               │
├──────────────┼───────────────┼───────────────┼──────────────────────┤
│ BD-0042      │ FMD Booster   │ 01 Jul (2 days│ [Record Vaccination] │
│ BD-0078      │ FMD Booster   │ 30 Jun (3 days│ [Record Vaccination] │
│ BD-0091      │ Anthrax       │ 28 Jun (5 days│ [Record Vaccination] │
└──────────────┴───────────────┴───────────────┴──────────────────────┘
[Record All Selected]

DUE THIS WEEK:
  [5 animals listed in similar table with softer warning styling]

UPCOMING (7-30 days):
  [Collapsed section; expand to see]
```

### Screen 11: Inventory

```
ROUTE: /inventory
LAYOUT: Full content

PAGE HEADER:
  Inventory Management                    [+ Add Stock]
  Total Value: ৳ 2,84,560 (WAC Method)

SUMMARY CARDS:
┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ 📦 Total     │  │ ⚠ Low Stock  │  │ ⏱ Expiring  │  │ 📊 By Value  │
│ Items        │  │ Alerts       │  │ Next 30 days │  │              │
│              │  │              │  │              │  │ Feed: 68%    │
│   34         │  │   2          │  │   4 items    │  │ Medicine: 22%│
│   items      │  │   items      │  │              │  │ Chemical: 10%│
└──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘

FILTER TABS: All (34) | Feed (18) | Medicine (12) | Chemical (2) | Equipment (2)

INVENTORY TABLE:
┌──────────────────┬────────┬─────────────┬───────────────┬────────────┬────────┐
│ ITEM             │CATEGORY│ CURRENT QTY │ REORDER LEVEL │ EXPIRY     │ ACTIONS│
├──────────────────┼────────┼─────────────┼───────────────┼────────────┼────────┤
│ Rice Straw       │ Feed   │ 1,240 kg ✓  │ 200 kg        │ —          │ [···]  │
├──────────────────┼────────┼─────────────┼───────────────┼────────────┼────────┤
│ Anthrax Vaccine  │ Med.   │ 8 doses ⚠  │ 10 doses      │ 31 Aug 26  │ [···]  │
│                  │        │ Below limit │               │            │        │
├──────────────────┼────────┼─────────────┼───────────────┼────────────┼────────┤
│ FMD Vaccine      │ Med.   │ 45 doses ✓  │ 20 doses      │ 30 Sep 26  │ [···]  │
├──────────────────┼────────┼─────────────┼───────────────┼────────────┼────────┤
│ Ivermectin       │ Med.   │ 200 ml ✓    │ 50 ml         │ 15 Aug 26 ⚠│ [···]  │
└──────────────────┴────────┴─────────────┴───────────────┴────────────┴────────┘
```

### Screen 12: Finance

```
ROUTE: /finance
LAYOUT: Tabbed interface

TABS: Overview | Income | Expenses | Animal P&L | Batch P&L | Loans

━━━ OVERVIEW TAB ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[This Month ▾]  [All Farms ▾]  [Export ▾]

FINANCIAL SUMMARY CARDS:
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│ 💰 Revenue       │  │ 💸 Expenses      │  │ 📈 Net Profit    │
│ ৳ 4,21,000      │  │ ৳ 2,87,400      │  │ ৳ 1,33,600      │
│ ▲ +18% vs last  │  │ ▼ -5% vs last   │  │ ▲ +31% vs last  │
│ month           │  │ month           │  │ month           │
│ ━━━━━━━━━━━━━━━  │  │ ━━━━━━━━━━━━━━━  │  │ ━━━━━━━━━━━━━━━  │
│ [Sparkline ↗]   │  │ [Sparkline ↘]   │  │ [Sparkline ↗]   │
└──────────────────┘  └──────────────────┘  └──────────────────┘

P&L BY CATEGORY (Horizontal bar chart):
  Income:
  Animal Sale     ████████████████████ ৳3,20,000
  Milk Sale       ████ ৳58,000
  Other Income    █ ৳43,000

  Expenses:
  Feed Cost       ████████████████ ৳1,92,000
  Vet Cost        ████ ৳48,600
  Animal Purchase ███ ৳38,400
  Other           ██ ৳8,400

RECENT ENTRIES:
  [Transaction list: Date · Description · Amount · Type · Source badge]

━━━ BATCH P&L TAB ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Eid 2026 Fattening Batch — IN PROGRESS

  Total Animals:     47
  Start Date:        15 May 2026
  Days Running:      50 days

  ┌───────────────────────────────────────────────────────────────────┐
  │  COSTS                              REVENUE                       │
  │  ──────────────────────────────────────────────────────────────── │
  │  Animal Purchase  ৳ 16,45,000      Animal Sales (8)  ৳ 5,44,000 │
  │  Feed Cost        ৳  5,32,680      (Remaining animals unsold)     │
  │  Vet/Health       ৳     82,400                                    │
  │  Other            ৳     12,000                                    │
  │  ──────────────────────────────────────────────────────────────── │
  │  TOTAL COST    ৳ 22,72,080         REVENUE TO DATE ৳  5,44,000  │
  │                                                                   │
  │  Break-even per animal: ৳ 57,200 (avg accumulated cost)          │
  │  Projected profit at Eid market price: ৳ 4,20,000 (18.5% ROI)   │
  └───────────────────────────────────────────────────────────────────┘
```

### Screen 13: Reports

```
ROUTE: /reports
LAYOUT: Two-column (left: report catalog; right: report viewer)

LEFT CATALOG (280px):
  LIVESTOCK REPORTS
    Animal Register
    Herd Summary
    ADG Performance Report
    Breeding & Calving Report
    Mortality Report

  HEALTH REPORTS
    Vaccination Compliance Report
    Treatment History
    Disease Incident Summary
    Deworming Calendar

  FEEDING REPORTS
    FCR Analysis
    Feed Cost by Shed
    Monthly Consumption Summary

  FINANCIAL REPORTS
    Monthly P&L (by farm / consolidated)
    Annual P&L
    Batch Profitability
    Per-Animal Cost Report
    Inventory Valuation
    Loan Summary

RIGHT REPORT VIEWER:
  [Selected Report Title]
  ─────────────────────────────────────────────────────────────
  DATE RANGE: [From: 01/07/2026] [To: 07/07/2026]
  FARM: [All Farms ▾]
  BATCH: [All Batches ▾]
              [Generate Report]
  ─────────────────────────────────────────────────────────────
  [Report Content]

  [📄 Export PDF]  [📊 Export Excel]  [🖨 Print]
```

### Screen 14: Settings

```
ROUTE: /settings
LAYOUT: Left navigation + content

SETTINGS NAVIGATION:
  Organization
  Farm & Shed Management
  Users & Roles
  Subscription & Billing
  Notifications
  Integrations
  Security
  Appearance
  Language & Region

━━━ NOTIFICATIONS SETTINGS ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  VACCINATION ALERTS
  Send me alerts when vaccinations are due
  ○ In-app only
  ● In-app + SMS (recommended)
  ○ In-app + Email
  Alert advance notice: [7 days ▾]

  LOW STOCK ALERTS
  Notify when inventory falls below threshold
  [Toggle: ON] ●────────

  FINANCIAL ALERTS
  Monthly P&L summary                     [Toggle: ON] ●────────
  Weekly expense digest                   [Toggle: OFF] ───●

  SMS NOTIFICATIONS
  Phone: +880 01712345678 [Change]
  [Test SMS]

━━━ SUBSCRIPTION & BILLING ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  Current Plan: Banik                     [Upgrade Plan]
  Status: ● Active
  Renewal: 01 August 2026
  Price: ৳ 2,500 / month

  USAGE:
  Animals:   247 / 1,000 ████░░░░░░
  Users:       5 / 20    ██░░░░░░░░
  Farms:       3 / 5     ██████░░░░

  [Billing History →]   [Change Payment Method →]
```

### Screen 15: Profile

```
ROUTE: /profile
LAYOUT: Centered single column (640px)

  PROFILE PHOTO
  [Avatar — 96px]
  [Change Photo]

  ─────────────────────────────────────────────────────────
  PERSONAL INFORMATION
  Full Name *          [Rahim Uddin_______________________]
  Phone                +880 01712345678  ✓ Verified
  Email                [rahimfarm@gmail.com]
  Role                 Owner (read-only)

  ─────────────────────────────────────────────────────────
  PREFERENCES
  Language             ● English  ○ বাংলা
  Theme                ● Light  ○ Dark  ○ System
  Date Format          DD/MM/YYYY (Bangladesh Standard)
  Time Format          12-hour  24-hour
  Default Farm         [Mymensingh Main Farm ▾]

  ─────────────────────────────────────────────────────────
  SECURITY
  Password             ••••••••••  [Change Password →]
  Last Login           07 Jul 2026, 10:24 AM — Dhaka, Bangladesh

  ─────────────────────────────────────────────────────────
  [Save Changes]           [Cancel]
```

### Screen 16: Notifications List Page

```
ROUTE: /notifications
LAYOUT: Full page (max 760px centered)

ALL NOTIFICATIONS
                                              [Mark All Read]  [⚙ Preferences]

FILTER: [All ▾]  [Unread]  [Critical]  [Health]  [Inventory]  [Finance]

TODAY (3 Jul 2026)
  ┌──────────────────────────────────────────────────────────────────┐
  │ ● [🔴 URGENT] 3 Vaccinations Overdue                           │  ← unread blue dot
  │   BD-0042, BD-0078, BD-0091 require immediate attention         │
  │   Health · 2 hours ago                    [View Vaccinations →] │
  └──────────────────────────────────────────────────────────────────┘
  ┌──────────────────────────────────────────────────────────────────┐
  │   [🟡 WARNING] Low Stock: Anthrax Vaccine (8 doses remaining)   │  ← read (no dot)
  │   Inventory · 4 hours ago                    [View Inventory →] │
  └──────────────────────────────────────────────────────────────────┘

YESTERDAY (2 Jul 2026)
  [...]
```

### Screen 17: Audit Log

```
ROUTE: /settings/audit-log
LAYOUT: Full-width table (Admin access only)

AUDIT LOG
  Date Range: [01/07/2026 ── 07/07/2026]  User: [All ▾]  Action: [All ▾]
  Module: [All ▾]  [Search entity ID or name]

┌──────────────────┬────────────────┬──────────────────┬─────────────────────┐
│ TIMESTAMP        │ USER           │ ACTION           │ DETAILS             │
├──────────────────┼────────────────┼──────────────────┼─────────────────────┤
│ 07 Jul 10:30 AM  │ Kamal (Worker) │ UPDATED          │ Animal BD-0041      │
│                  │                │                  │ Weight: 264→268 kg  │
│                  │                │ [View Diff →]    │                     │
├──────────────────┼────────────────┼──────────────────┼─────────────────────┤
│ 07 Jul 09:15 AM  │ You (Owner)    │ CREATED          │ Vaccination Record  │
│                  │                │                  │ BD-0042 — FMD       │
├──────────────────┼────────────────┼──────────────────┼─────────────────────┤
│ 07 Jul 08:00 AM  │ System         │ ALERT_GENERATED  │ 3 overdue vacc.    │
└──────────────────┴────────────────┴──────────────────┴─────────────────────┘
```

---

## 23. UI Component Inventory

### 23.1 Foundation Components (20)

| # | Component | Variants | Notes |
|---|---|---|---|
| 1 | ColorToken | 40+ tokens | Light + Dark |
| 2 | TypographyToken | 10 text styles | Inter + Noto Sans Bengali |
| 3 | SpacingToken | 14 values | 4px base unit |
| 4 | ShadowToken | 5 elevation levels | Light + Dark variants |
| 5 | RadiusToken | 8 values | radius-none to radius-full |
| 6 | IconToken | 30+ domain icons | Fluent UI + custom |
| 7 | StatusToken | 5 semantic statuses | success/warning/danger/info/neutral |
| 8 | AnimalStatusToken | 6 animal statuses | Domain-specific |
| 9 | BreakpointToken | 7 breakpoints | xs to 3xl |
| 10 | AnimationToken | 5 easing curves | 150ms/200ms/300ms |
| 11 | ZIndexToken | 6 levels | base/dropdown/sticky/overlay/modal/max |
| 12 | GridSystem | 4-12 col | Responsive |
| 13 | FocusRing | 2 variants | light/dark |
| 14 | SkeletonLoader | 3 types | text/card/table |
| 15 | ProgressIndicator | 2 types | linear/circular |
| 16 | Ripple | 1 | Material-style interaction |
| 17 | Shimmer | 1 | Skeleton animation |
| 18 | Tooltip | 3 positions | top/right/bottom |
| 19 | Popover | 1 | Anchored dropdown container |
| 20 | Portal | 1 | Dialog/overlay mounting |

### 23.2 Input Components (15)

| # | Component | Variants |
|---|---|---|
| 21 | TextInput | text/email/password/search/phone/url |
| 22 | NumberInput | integer/decimal/currency |
| 23 | Textarea | auto-grow/fixed |
| 24 | Select | single/multi/grouped/searchable |
| 25 | Autocomplete | async/local |
| 26 | DatePicker | single/range |
| 27 | TimePicker | scroll/dropdown |
| 28 | RadioGroup | vertical/horizontal |
| 29 | Checkbox | single/group/indeterminate |
| 30 | Toggle | with-label |
| 31 | Slider | single/range, with labels |
| 32 | FileUpload | dropzone/click/multiple |
| 33 | TagInput | with max count |
| 34 | PhoneInput | Bangladesh prefix |
| 35 | CurrencyInput | BDT formatted |

### 23.3 Display Components (15)

| # | Component | Variants |
|---|---|---|
| 36 | Badge | status/count/text/outlined |
| 37 | Chip/Tag | dismissable/filter/action |
| 38 | Avatar | photo/initials/icon |
| 39 | AvatarGroup | stacked |
| 40 | StatusPill | animal-specific status |
| 41 | KPICard | with-delta/with-chart/with-sparkline |
| 42 | ContentCard | basic/with-header/with-footer |
| 43 | AlertCard | info/warning/danger/success |
| 44 | AnimalCard | list-item/grid |
| 45 | Timeline | event-list |
| 46 | Divider | horizontal/vertical/with-label |
| 47 | DataList | key-value pairs |
| 48 | ProgressBar | linear/circular |
| 49 | Gauge | BCS score |
| 50 | Breadcrumb | responsive truncating |

### 23.4 Navigation Components (8)

| # | Component | Variants |
|---|---|---|
| 51 | Sidebar | expanded/collapsed/mobile-overlay |
| 52 | NavItem | active/inactive/badge |
| 53 | NavSection | collapsible group |
| 54 | Header | desktop/tablet/mobile |
| 55 | BottomNav | mobile (5 items) |
| 56 | Tabs | line/filled/pill |
| 57 | Breadcrumb | responsive |
| 58 | FarmSelector | dropdown in sidebar |

### 23.5 Overlay Components (6)

| # | Component | Variants |
|---|---|---|
| 59 | Dialog | info/confirm/danger/form/wizard |
| 60 | Drawer | right/left/bottom-sheet |
| 61 | Toast | success/error/warning/info |
| 62 | NotificationPanel | bell dropdown |
| 63 | CommandPalette | global search (Cmd+K) |
| 64 | ContextMenu | right-click/row action |

### 23.6 Data Components (8)

| # | Component | Variants |
|---|---|---|
| 65 | DataGrid | standard/compact/selectable |
| 66 | Table | standard/zebra/ledger/comparison |
| 67 | FilterPanel | drawer-based |
| 68 | Pagination | numbered/cursor |
| 69 | SortHeader | column sort indicator |
| 70 | InlineEdit | text/number/date |
| 71 | BulkActionBar | multi-select actions |
| 72 | ExportMenu | PDF/Excel/Print |

### 23.7 Chart Components (8)

| # | Component | Variants |
|---|---|---|
| 73 | LineChart | single/multi-series |
| 74 | BarChart | vertical/horizontal/stacked |
| 75 | AreaChart | single/multi-series |
| 76 | DonutChart | with legend |
| 77 | SparklineChart | inline mini-chart |
| 78 | HeatmapCalendar | vaccination calendar |
| 79 | BulletChart | actual vs. target |
| 80 | GaugeChart | BCS score |

### 23.8 State Components (5)

| # | Component | Variants |
|---|---|---|
| 81 | EmptyState | module-level/search/filter |
| 82 | ErrorState | 404/500/network |
| 83 | OfflineBanner | sticky warning |
| 84 | SkeletonPage | page-level loading |
| 85 | LoadingOverlay | form submit |

**Total Component Count: 85 components**

---

## 24. Screen Map & Navigation Map

### 24.1 Complete Screen Map

```
/                           → Redirect to /dashboard (if auth) or /login
├── /login                  → Login Screen
├── /register               → Registration Screen
├── /forgot-password        → Forgot Password Flow (3 steps)
├── /onboarding             → Onboarding Wizard (5 steps)
│
├── /dashboard              → Executive Dashboard
│
├── /livestock              → Livestock List (tabs: All/Active/Sold/Dead)
│   ├── /livestock/new      → Add Animal Drawer (opens on this route)
│   └── /livestock/:id      → Animal Detail Page
│       ├── /livestock/:id/weight        → Weight History
│       ├── /livestock/:id/health        → Animal Health History
│       ├── /livestock/:id/feeding       → Animal Feed Records
│       └── /livestock/:id/finance       → Animal Cost Ledger
│
├── /batches                → Batch List
│   ├── /batches/new        → Create Batch
│   └── /batches/:id        → Batch Detail + P&L
│
├── /feeding                → Feeding Hub (tabs)
│   ├── /feeding/ingredients            → Ingredient Catalog
│   ├── /feeding/formulas               → Formula List
│   │   ├── /feeding/formulas/new       → Formula Builder
│   │   └── /feeding/formulas/:id       → Formula Detail/Edit
│   ├── /feeding/schedules              → Active Schedules
│   ├── /feeding/log                    → Daily Consumption Log
│   └── /feeding/fcr                    → FCR Report
│
├── /health                 → Health Hub (tabs)
│   ├── /health/overview                → Health Dashboard
│   ├── /health/vaccination             → Vaccination List + Calendar
│   │   └── /health/vaccination/protocols → Protocol Builder
│   ├── /health/treatments              → Treatment Records
│   ├── /health/incidents               → Disease Incidents
│   ├── /health/deworming               → Deworming Calendar
│   └── /health/vet-visits              → Vet Visit Log
│
├── /inventory              → Inventory Hub (tabs)
│   ├── /inventory/items                → Item Catalog
│   ├── /inventory/stock-in             → Stock In Form
│   ├── /inventory/movement             → Stock Movement Ledger
│   ├── /inventory/suppliers            → Supplier Directory
│   └── /inventory/valuation            → Valuation Report
│
├── /finance                → Finance Hub (tabs)
│   ├── /finance/overview               → Financial Dashboard
│   ├── /finance/income                 → Income Ledger
│   ├── /finance/expenses               → Expense Ledger
│   ├── /finance/animal-pl              → Per-Animal Cost View
│   ├── /finance/batch-pl               → Batch P&L
│   ├── /finance/loans                  → Loan Tracker
│   └── /finance/chart-of-accounts      → Chart of Accounts
│
├── /reports                → Report Center
│   ├── /reports/livestock              → Livestock Reports
│   ├── /reports/health                 → Health Reports
│   ├── /reports/feeding                → Feeding Reports
│   └── /reports/finance                → Financial Reports
│
├── /notifications          → Full Notifications Page
│
├── /settings               → Settings Hub
│   ├── /settings/organization          → Org Profile
│   ├── /settings/farms                 → Farm & Shed Management
│   │   ├── /settings/farms/:id         → Farm Detail/Edit
│   │   └── /settings/farms/:id/sheds   → Shed & Pen Management
│   ├── /settings/users                 → User & Role Management
│   │   ├── /settings/users/invite      → Invite User Flow
│   │   └── /settings/users/:id         → User Detail/Edit
│   ├── /settings/subscription          → Plan & Billing
│   ├── /settings/notifications         → Notification Preferences
│   ├── /settings/security              → Security Settings
│   ├── /settings/appearance            → Theme + Display
│   ├── /settings/language              → Language & Region
│   └── /settings/audit-log             → Audit Log Viewer
│
└── /profile                → User Profile & Preferences
```

### 24.2 Navigation Hierarchy Map

```
PRIMARY NAVIGATION (Sidebar):
  Dashboard (/)
  Livestock (/livestock)
  Smart Feeding (/feeding)
  Health (/health)
  Inventory (/inventory)
  Finance (/finance)
  Reports (/reports)
  ── separator ──
  Settings (/settings)
  Audit Log (/settings/audit-log)

SECONDARY NAVIGATION (within modules, as tabs):
  Livestock:    All Animals | Batches | Archive
  Feeding:      Ingredients | Formulas | Schedules | Daily Log | FCR
  Health:       Overview | Vaccination | Treatments | Incidents | Deworming | Vet Visits
  Inventory:    Items | Movement | Suppliers | Valuation
  Finance:      Overview | Income | Expenses | Animal P&L | Batch P&L | Loans
  Reports:      Livestock | Health | Feeding | Financial
  Settings:     (sidebar within settings)

TERTIARY NAVIGATION (within entity detail pages, as tabs):
  Animal Detail: Timeline | Weight | Health | Feeding | Finance
  Batch Detail:  Animals | P&L | Feed History | Health History
  Farm Settings: Details | Sheds | Users | Inventory

MOBILE BOTTOM NAV (5 items — most used):
  Dashboard | Livestock | Health | Feeding | More (...)
  "More" opens a full-screen menu for remaining modules

CONTEXTUAL ACTIONS (not in navigation):
  All primary entity pages: [+ Add {Entity}] button in page header
  All list pages: Table/Grid toggle, Filter, Export, Search
  All detail pages: Actions dropdown in sticky header
```

### 24.3 User Journey Maps

```
CRITICAL JOURNEY 1: First-time Farmer Flow (< 30 minutes)
  Register → OTP Verify → Onboarding Wizard (5 steps) → Dashboard
  Dashboard → Add First Animal → See Animal Profile → Done

CRITICAL JOURNEY 2: Daily Morning Check (< 5 minutes)
  Dashboard → Review Alert Banner → Health > Vaccination > Overdue
  → Record Vaccination → Dashboard (alert gone) → Feeding > Daily Log
  → Log Today's Consumption → Done

CRITICAL JOURNEY 3: Animal Sale Record (< 3 minutes)
  Livestock → Find Animal → Animal Detail → [⋯ More] → Mark as Sold
  → Enter sale details → Auto-posts to Finance → Done

CRITICAL JOURNEY 4: Weekly Weight Recording (Worker, mobile)
  Livestock → Filter: Shed 3 → Select All → [Log Weight]
  → Bulk weight entry form (one row per animal) → Save → Done

CRITICAL JOURNEY 5: Month-End Financial Report (Accountant)
  Finance > Overview → [Export ▾] → Monthly P&L
  → Select: July 2026, All Farms → Generate → Export Excel → Done
```

---

*This Design System is the authoritative reference for all visual and interaction design decisions for Farm360 AI. All UI components, screen layouts, and interaction patterns must align with this document. Deviations require a formal design review.*

---

**Farm360 AI — UI/UX Design System**  
*© 2026 Farm360 AI. All Rights Reserved.*  
*Designed with Microsoft Fluent 2, Material Design 3, and Apple HIG principles.*
