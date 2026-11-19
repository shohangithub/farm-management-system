# Farm360 AI — Development Status

**Last updated:** November 2026
**Architecture:** Clean Architecture · DDD · CQRS (MediatR) · EF Core 10 / SQL Server · Hangfire · Redis · Angular 22
**Detailed plans:** `docs/*` · **Open work:** [TODO.md](TODO.md) · **Deploy:** [docs/deployment.md](docs/deployment.md)

## Module status

| Module | Backend (domain → API) | Angular UI | Notes |
|---|---|---|---|
| Identity, auth & multi-tenancy | ✅ | ✅ | JWT + refresh tokens, OTP, tenant query filters, tenant resolution, per-farm access, rate limiting, security headers |
| Subscriptions & billing | ✅ | ✅ | Plans, trials, SSLCommerz checkout + IPN, daily status refresh job |
| User management | 🔄 | 🔄 | Members, roles/permissions, invitations, password reset (in progress) |
| Master data & locations | ✅ | ✅ | Typed master-data lists, BD location hierarchy |
| Organisation, branch, farm, shed, pen | ✅ | ✅ | |
| Livestock | ✅ | ✅ | Animals, batches, weights/BCS, breeding, photos, sale/death exits |
| Health & veterinary | ✅ | ✅ | Vaccination protocols & events, treatments, incidents, mortality, vet visits, withdrawal |
| Smart feeding & nutrition | ✅ | ✅ | Ingredients, formulas, plans (animal/batch/shed/pen), daily entries job, per-animal feed allocation & cost, FCR |
| Inventory | ✅ | ✅ | Items, suppliers, stock movements, daily consumables job, feed/medicine stock integration |
| Finance & accounting | ✅ | ✅ | Transactions, per-animal cost ledgers, overhead allocation (monthly job), trade credit, investors & profit sharing, consolidated reports |
| Dashboard & analytics | ✅ | ✅ | Farm dashboard, breeding/growth analytics |
| Intelligence | ✅ | ✅ | P&L projections, simulation engine, background intelligence events |
| Reporting | ✅ | ✅ | PDF (QuestPDF, Bengali shaping), Excel, CSV renderers; more reports being added |
| Configurable business rules | 🔄 | 🔄 | Config defaults (docs/33) → per-tenant overrides in progress |

## Platform & operations

- ✅ Health probes `/health/live` and `/health/ready` (SQL + pending migrations, Hangfire storage, Redis)
- ✅ Production startup: migrations as a separate step (EF bundle), fail-fast when enabled; demo data Development-only; maintenance backfill opt-in
- ✅ Scale-out: Redis cache + shared Data Protection key ring, optional Hangfire server per instance, forwarded headers
- ✅ Observability: Serilog JSON with correlation/tenant/user ids, optional OpenTelemetry OTLP export
- ✅ Docker images (API, migrator, SPA on nginx), dev and production compose, GitHub Actions CI
- ✅ Tests: Domain, Application, Architecture and API functional suites run in CI; Application integration tests (Testcontainers) run in CI as informational

## Known gaps

See [TODO.md](TODO.md).
