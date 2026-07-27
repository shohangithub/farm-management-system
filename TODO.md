# Farm360 AI — Task & Architecture Roadmap (TODO)

**Last Updated:** July 22, 2026

---

## Completed Tasks (Architecture & Production Hardening Sprint)

- [x] **Multi-Tenant Global Query Filters** — Combined `TenantId == CurrentTenantId` AND `IsDeleted == false` into single expression in `ApplicationDbContext` to prevent filter overwriting.
- [x] **Audit SaveChanges Interceptor** — Auto-populates `TenantId` when `Guid.Empty` on entity addition, along with `CreatedBy`, `CreatedAtUtc`, `ModifiedBy`, `ModifiedAtUtc`, `DeletedBy`, `DeletedAtUtc`.
- [x] **Multi-Channel Tenant Resolution** — Refactored `TenantResolutionMiddleware` to support JWT claim, `X-Tenant-Id` header, and subdomain resolution with cache/DB verification against `Tenant` aggregate root.
- [x] **Security Headers** — Integrated production HTTP security response headers in `Program.cs`.
- [x] **Regression & Architecture Testing** — Verified 150 tests across solution test projects (79 Domain + 62 Application + 7 Architecture + 1 Integration + 1 Functional).
- [x] **Livestock Module Production-Readiness Fixes** — Completed missing FluentValidation, pregnancy validation, batch filtering, strict types, mapped missing fields, and unified exception handling.

---

## Remaining Module Roadmap

- [ ] **Smart Feeding Module**
  - Feed formulation & rationing models
  - Feeding schedule CQRS handlers
  - Feed consumption & FCR tracking UI
- [ ] **Inventory Control Module**
  - Feed & medicine stock management
  - Purchase order workflows
- [ ] **Finance & Accounting Module**
  - Expense tracking & financial period closing
  - Profitability analytics per animal batch
- [ ] **Executive Dashboard & AI Engine Integration**
  - Operational KPIs & farm health metrics
  - Predictive health alert triggers
