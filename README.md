# Farm360 AI

Farm360 is a multi-tenant SaaS for livestock farms (cattle fattening and dairy, Bangladesh-first):
animals and batches, health and vaccination, smart feeding with per-animal feed costing, inventory,
finance and per-animal profit and loss, investor profit-sharing, dashboards, analytics and printable
reports (English/Bengali PDF, Excel, CSV). Every tenant (organisation) is isolated by `TenantId`
query filters, and inside a tenant users are limited by role permissions and allowed farms.

## Architecture

```
src/
  Farm360.Domain          Aggregates, value objects, domain events (no dependencies)
  Farm360.Application     CQRS (MediatR), validators, jobs, report definitions
  Farm360.Contracts       DTOs shared by API and clients
  Farm360.Persistence     EF Core 10 / SQL Server, migrations, repositories, seeders
  Farm360.Identity        ASP.NET Identity, JWT + refresh tokens, OTP
  Farm360.Infrastructure  Hangfire, Redis/memory cache, SignalR, Serilog, SSLCommerz, QuestPDF/ClosedXML
  Farm360.Shared          Result/guard primitives
  Farm360.Api             .NET 10 minimal APIs: composition root, middleware, endpoints
  Farm360.Web             Angular 22 SPA (standalone components, Material, Tailwind)
tests/                    Domain, Application, Architecture, API functional, Application integration (Testcontainers)
```

The request pipeline runs forwarded headers, exception handling, correlation ID, request logging,
security headers, CORS, authentication, rate limiting, tenant resolution, authorization and per-farm
access, in that order. Background work runs on Hangfire (SQL Server storage): daily feeding entries,
daily consumables, closing feeding cycles, monthly overhead allocation and subscription status refresh.

## Prerequisites

- .NET SDK 10.0.4xx (`global.json`), plus the local tool `dotnet-ef` (`dotnet tool restore`)
- Node.js 22.22+ or 24.15+ and npm (for the SPA)
- SQL Server 2022 and Redis 7. `docker compose up -d` starts both, plus Seq for logs.

## Run locally

```bash
docker compose up -d                       # SQL Server :1433, Redis :6379, Seq UI :8081
dotnet tool restore
dotnet user-secrets --project src/Farm360.Api set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=Farm360;User Id=sa;Password=Farm360_Dev!2026;TrustServerCertificate=True;MultipleActiveResultSets=True;"
dotnet run --project src/Farm360.Api       # http://localhost:5259, Scalar UI at /scalar, Hangfire at /hangfire
cd src/Farm360.Web && npm ci && npm start  # http://localhost:4200 (proxies /api, /hubs, /uploads)
```

In Development the API applies migrations at startup, seeds the demo tenant and creates the
development platform admin. To run the full stack in containers, use `docker compose --profile app up -d --build`
(API on :5000, SPA on :4300).

## Tests

```bash
dotnet build Farm360.slnx                     # warnings are errors (Directory.Build.props)
dotnet test tests/Farm360.Domain.UnitTests
dotnet test tests/Farm360.Application.UnitTests
dotnet test tests/Farm360.Architecture.Tests
dotnet test tests/Farm360.Api.FunctionalTests  # route table, authorization allow-lists, security middleware
dotnet test tests/Farm360.Application.IntegrationTests   # needs Docker (Testcontainers)
cd src/Farm360.Web && npx ng build --configuration production
```

## Configuration

Settings are read from `appsettings.json`, then `appsettings.{Environment}.json`, then environment
variables (`Section__Key`; a `FARM360_` prefix also works and takes precedence), then command-line
arguments. User secrets are loaded only in Development. **Every secret must come from the environment
or a secret store in production.**

### Secrets

| Setting | Environment variable | Notes |
|---|---|---|
| SQL Server | `ConnectionStrings__DefaultConnection` | App data and Hangfire storage. Use a least-privilege login. |
| Redis | `ConnectionStrings__Redis` | `host:6379,password=...,ssl=...,abortConnect=false` |
| JWT signing key | `Jwt__Secret` | 32 or more random characters. The dev key is flagged CRITICAL at startup outside Development. |
| SSLCommerz | `SslCommerz__StoreId`, `SslCommerz__StorePassword` | Live store credentials. Also set `SslCommerz__IsSandbox=false`. |
| SMS gateway | `Sms__Provider` (`Log`/`BulkSmsBd`), `Sms__BulkSmsBd__ApiKey`, `Sms__BulkSmsBd__SenderId` | Needed for OTP, password reset and invitation SMS. `Log` sends nothing. |
| Email | `Email__Provider` (`Log`/`Smtp`), `Email__Smtp__Host`, `__Port`, `__Username`, `__Password`, `__FromAddress` | Any SMTP relay. Used for email invitations. |
| First platform admin | `Seed__SystemAdmin__Phone`, `Seed__SystemAdmin__Password`, `Seed__SystemAdmin__Email` | Optional. Created once; remove afterwards. |
| QuestPDF licence | `Farm360__BusinessRules__PdfLicence`, `Farm360__BusinessRules__PdfLicenceKey` | Community is free only under USD 1M revenue. |

### Behaviour and hosting

| Key (env var form) | Default | Purpose |
|---|---|---|
| `Database__MigrateOnStartup` | `true` in Development, else `false` | Apply EF migrations at startup. If it fails, the process exits. |
| `Seed__DemoData` | `true` in Development, else `false` | Seed the "demo" tenant, organisation, HQ branch and owner. |
| `Maintenance__RunFeedAllocationBackfillOnStartup` | `false` | Run the one-off 2020–2035 cross-tenant feed-allocation backfill at startup. |
| `Cache__Provider` | `Auto` (`Memory` in appsettings.Production.json) | `Redis` \| `Memory` \| `Auto` (Redis if configured). More than one instance needs `Redis`. |
| `Hangfire__RunServer` | `true` | Process background jobs in this instance. Set `false` on API replicas when one worker instance runs jobs. |
| `Hangfire__WorkerCount` | CPU × 2 (max 20) | Number of Hangfire workers. |
| `ForwardedHeaders__KnownProxies__0`, `ForwardedHeaders__KnownNetworks__0` | none | Proxy IPs or CIDRs that may set `X-Forwarded-For/Proto`. This makes rate limits see client IPs. |
| `ForwardedHeaders__ForwardLimit` | `1` | Number of proxy hops in front of the API. |
| `DataProtection__KeysPath` | none (Redis if used) | Shared key ring location (volume or file share). |
| `Cors__AllowedOrigins__0..n` | see appsettings | SPA origins. |
| `Serilog__Console__Json` / `Serilog__File__Enabled` / `Serilog__Seq__ServerUrl` | `false` / `true` / none | Log format and sinks. The container image sets JSON console output and turns file logging off. |
| `OpenTelemetry__Endpoint` (`__Protocol`, `__ServiceName`) | none | Export OTLP traces and metrics (ASP.NET Core, HttpClient, Kestrel, runtime). |
| `RateLimiting__Auth__PermitPerMinute`, `RateLimiting__Register__PermitPerHour`, `RateLimiting__User__PermitPerMinute` | 10 / 5 / 600 | Rate limits. |

### Health endpoints (anonymous, not rate-limited)

- `GET /health/live`: the process is up. Use it for container liveness. `/health` is an alias.
- `GET /health/ready`: SQL Server is reachable with no pending migrations, Hangfire storage is reachable, and Redis is reachable (only when the Redis cache is in use). Returns JSON; status 503 when unhealthy.

## Deploy with Docker

```bash
cp deploy/.env.example deploy/.env    # fill in secrets (git-ignored)
docker compose --env-file deploy/.env -f docker-compose.prod.yml build
docker compose --env-file deploy/.env -f docker-compose.prod.yml run --rm migrate
docker compose --env-file deploy/.env -f docker-compose.prod.yml up -d
# add --profile bundled-db to run SQL Server in the stack instead of an external server
```

Images: `src/Farm360.Api/Dockerfile` builds the API (target `final`, non-root, port 8080, HEALTHCHECK on
`/health/live`) and the migration runner (target `migrator`). `src/Farm360.Web/Dockerfile` builds the SPA
on nginx (port 8080). It proxies `/api`, `/hubs` and `/uploads` to `API_UPSTREAM`, so the browser sees a
single origin. To call an API on another origin, build with `--build-arg API_BASE_URL=https://api.example.com`.
CI pushes all three images to GHCR on `main`. See [docs/deployment.md](docs/deployment.md) for the production checklist.

## Migrations

Production schema changes are a separate deploy step, not something the app does at startup:

1. CI publishes the `efbundle-linux-x64` artifact (a self-contained `efbundle` plus an idempotent `migrations.sql` for review) and the `farm360-migrator` image.
2. Back up the database.
3. Run `./efbundle --connection "<connection string>"` or `docker compose ... run --rm migrate`.
4. Roll out the new API. `/health/ready` reports unhealthy while migrations are pending.

Local: `dotnet ef migrations add <Name> --project src/Farm360.Persistence --startup-project src/Farm360.Api`.

## One-time maintenance endpoints

These are idempotent and safe to re-run. Each is scoped to the caller's tenant and one farm, and needs the permission shown.

| Endpoint | Permission | What it does |
|---|---|---|
| `POST /api/v1/feeding/allocations/reprocess-exited-animals/{farmId}` | `feeding.delete` | Applies sold or dead animals' exits to feeding: ends plans, skips or reverses entries after the exit date, returns stock, re-splits group entries and re-syncs ledgers. |
| `POST /api/v1/feeding/allocations/resync-costs/{farmId}` | `feeding.delete` | Zeroes skipped entries' allocations and resets every animal ledger's feed cost to its confirmed allocations. |
| `POST /api/v1/feeding/allocations/backfill/{farmId}` (body `{from,to,batchSize?,maxBatches?}`) | `feeding.delete` | Fills in per-animal allocations for historical feeding entries. |
| `POST /api/v1/farms/{farmId}/finance/ledger/backfill-manual-animal-expenses` | `finance.admin` | Posts hand-entered, animal-tagged expenses onto animal cost ledgers. |

## More documentation

- `docs/deployment.md`: production checklist, backups and restore, Hangfire dashboard
- `docs/1_…`–`docs/34_…`: product, architecture, module plans, business rules, accounting policy
- `USER_MANUAL.md`, `DEVELOPMENT_STATUS.md`, `TODO.md`
