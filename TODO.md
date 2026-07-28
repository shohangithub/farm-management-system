# Farm360 AI — Task & Architecture Roadmap (TODO)

**Last Updated:** July 22, 2026

---

## Completed Tasks (Architecture & Production Hardening Sprint)

- [x] **Multi-Tenant Global Query Filters** — Combined `TenantId == CurrentTenantId` AND `IsDeleted == false` into single expression in `ApplicationDbContext` to prevent filter overwriting.
- [x] **Audit SaveChanges Interceptor** — Auto-populates `TenantId` when `Guid.Empty` on entity addition, along with `CreatedBy`, `CreatedAtUtc`, `ModifiedBy`, `ModifiedAtUtc`, `DeletedBy`, `DeletedAtUtc`.
- [x] **Multi-Channel Tenant Resolution** — Refactored `TenantResolutionMiddleware` to support JWT claim, `X-Tenant-Id` header, and subdomain resolution with cache/DB verification against `Tenant` aggregate root.
- [x] **Security Headers** — Integrated production HTTP security response headers in `Program.cs`.
- [x] **Regression & Architecture Testing** — Verified 150 tests across solution test projects (79 Domain + 62 Application + 7 Architecture + 1 Integration + 1 Functional).
- [x] **Livestock Module Production-Readiness Certification** — Verified domain rules, weight/BCS tracking, photo limits, batch filtering, permissions, Signal reactivity, OnPush detection, and unit tests (76/76 passing). Marked as **Production Ready** ✅.
- [x] **Health & Veterinary Module Production-Readiness Certification** — Verified domain rules, mortality status propagation (`AnimalStatus.Dead`), pre-validation & SQL duplicate index handling (`409 Conflict`), ProblemDetails parsing (`error-parser.ts`), deworming, withdrawal tracking, Signal reactivity, and unit tests (63/63 passing). Marked as **Production Ready** ✅.

---

## Remaining Module Roadmap

- [x] **5. Smart Feeding Module** *(COMPLETED ✅)*
  - [x] Feed ingredient catalog & pre-loaded South Asian / BD ingredients
  - [x] Feed formula builder & nutritional profile calculator (DM%, CP%, ME)
  - [x] Feeding schedule assignment to sheds, pens, and animal batches
  - [x] Daily feed consumption & wastage logger
  - [x] Feed Conversion Ratio (FCR = Feed / Weight Gain) trend analytics & chart UI
- [ ] **6. Inventory Control Module**
  - Feed & medicine stock management
  - Purchase order workflows
- [ ] **7. Finance & Accounting Module**
  - Expense tracking & financial period closing
  - Profitability analytics per animal batch
- [ ] **8. Executive Dashboard & AI Engine Integration**
  - Operational KPIs & farm health metrics
  - Predictive health alert triggers
