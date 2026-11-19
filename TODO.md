# Farm360 AI — Roadmap (TODO)

**Last updated:** November 2026 · Built modules: see [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)

## Production hardening (before go-live)

- [ ] Rotate the SQL Server password, JWT signing key and Redis password currently present in `appsettings.json`; move them to environment/secret store and remove them from the file (and git history). See `docs/deployment.md` §1.
- [ ] Switch SSLCommerz to the live store (`SslCommerz__IsSandbox=false`) and register the IPN URL.
- [ ] Set up scheduled SQL Server backups + monthly restore test (`docs/deployment.md` §4).
- [ ] Decide on an authenticated Hangfire dashboard (currently Development-only).
- [ ] Make the Application integration test suite (Testcontainers) green in CI, then make it blocking.
- [ ] Reduce the two finance report component stylesheets below the 6 kB `anyComponentStyle` budget.

## Product roadmap

- [ ] **Bengali (বাংলা) UI localisation** — Angular i18n for the SPA (reports already render Bengali).
- [ ] **Messaging providers** — real SMS gateway (OTP, alerts) and email provider; today `LoggingSmsService` / `LoggingEmailService` only log.
- [ ] **Object storage** — S3-compatible `IFileStorageService` (uploads are on local disk; scale-out needs a shared volume until then).
- [ ] **Brand palette** — apply the final Farm360 colour palette / design tokens across the SPA.
- [ ] **Reports** — remaining catalogue items from `docs/32_Farm360_Reporting_Module_Implementation_Plan.md` beyond those currently being added.
- [ ] **User management** — complete invitations, member roles and password reset (in progress).
- [ ] **Per-tenant business rules** — finish tenant overrides of `Farm360:BusinessRules` (in progress).
