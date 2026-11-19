# Farm360 — Production Deployment Guide

This guide covers the container deployment (`docker-compose.prod.yml`). The same settings apply to
Kubernetes or a VM. The [README](../README.md#configuration) lists every configuration key.

## 1. Pre-flight checklist

### Secrets: rotate before the first production deploy

`appsettings.json` in git currently holds a **real SQL Server connection string (with password)**, a
Redis password and a JWT signing key. Anyone with repository access has these values, so treat all of them as compromised:

- [ ] **SQL Server password.** Change the login's password, or better, create a new least-privilege login, and supply it only through `ConnectionStrings__DefaultConnection`. Then remove the value from `appsettings.json`.
- [ ] **JWT signing key.** Generate one with `openssl rand -base64 48` and set `Jwt__Secret`. Rotating it signs every user out, which is expected. At startup the API logs a CRITICAL message if the development key is in use.
- [ ] **Redis password.** Set `REDIS_PASSWORD` / `ConnectionStrings__Redis`. Never expose port 6379 publicly.
- [ ] **SSLCommerz.** Use the live store ID and password (`SslCommerz__StoreId`, `SslCommerz__StorePassword`) and set `SslCommerz__IsSandbox=false`. `FrontendBaseUrl` and `ApiBaseUrl` must be the public HTTPS URLs, and the IPN URL must be registered in the merchant panel. While sandbox mode is on outside Development, the API logs a CRITICAL message.
- [ ] **Seed admin.** Set `Seed__SystemAdmin__Phone/Password` for the first start only, then remove them and change the password.
- [ ] **Git history.** Remove secrets from history (`git filter-repo`) once they are rotated, or accept that the old values are burned.

### Platform

- [ ] TLS is terminated in front of the `web` container (cloud load balancer, Caddy, Traefik or nginx). Redirect HTTP to HTTPS there and send HSTS.
- [ ] **Forwarded headers.** List the proxies that sit in front of the API in `ForwardedHeaders__KnownProxies__N` (IPs) or `ForwardedHeaders__KnownNetworks__N` (CIDRs). The compose file trusts the Docker bridge range `172.16.0.0/12`, which is where the `web` nginx sits. With an extra load balancer in front of nginx, also trust its address and set `ForwardedHeaders__ForwardLimit=2`. Without this, every request appears to come from the proxy and all users share one rate-limit bucket (10 logins per minute for the whole site).
- [ ] `Cors__AllowedOrigins__0` is the public SPA origin.
- [ ] **Several API instances.** Set `Cache__Provider=Redis` (the Data Protection key ring then lives in Redis too) and share the uploads volume (`/app/wwwroot/uploads`) between instances. Run Hangfire jobs on one instance, or keep `Hangfire__RunServer=true` on all of them (Hangfire coordinates workers through SQL Server).
- [ ] QuestPDF licence: Community is only valid below USD 1M annual revenue (`Farm360__BusinessRules__PdfLicence`).
- [ ] SQL Server edition: the bundled container defaults to **Express** (10 GB per database). Use Standard or a managed service (Azure SQL, RDS) once data grows.

## 2. Deploy

```bash
cp deploy/.env.example deploy/.env                      # fill in; never commit
export COMPOSE="docker compose --env-file deploy/.env -f docker-compose.prod.yml"
$COMPOSE pull || $COMPOSE build                          # CI images: set FARM360_*_IMAGE to ghcr.io/...:<sha>
$COMPOSE run --rm migrate                                # 1. schema (after a backup!)
$COMPOSE up -d                                           # 2. app
curl -fsS https://app.example.com/health/ready           # 3. verify
```

What happens at API startup in Production:

| Step | Default in Production |
|---|---|
| EF migrations | **Not applied** (`Database__MigrateOnStartup=false`). If it is set to true and the migration fails, the process exits. |
| Permissions, system roles, role grants, default breeds | Always (idempotent) |
| Demo tenant | Not seeded (`Seed__DemoData=false`) |
| Platform admin | Only when `Seed__SystemAdmin__*` is set |
| Feed-allocation backfill 2020–2035 | Not run (`Maintenance__RunFeedAllocationBackfillOnStartup=false`) |
| Hangfire recurring jobs | Registered (idempotent), and processed when `Hangfire__RunServer=true` |

Rollback: redeploy the previous image tag. Migrations are forward-only, so if a migration must be undone,
restore the pre-deploy backup (section 4). Keep schema changes backward compatible (expand, then contract) so the
previous image keeps working against the new schema.

## 3. Hangfire dashboard

The dashboard is mapped only in Development and only for local requests. In production it is **not
exposed**. If you need it, do not publish it on the internet. Either:

- run a Development-mode API instance locally against a read replica or the production DB over a VPN or bastion; or
- add an authenticated mapping (a `DashboardOptions.Authorization` filter that validates a PlatformAdmin
  cookie or session) behind an IP allow-list on the proxy. Bearer tokens are not sent on browser navigation,
  so JWT alone does not work.

Job health shows up in `/health/ready` (storage reachability) and in logs (`Hangfire` category, Warning+).

## 4. Backups and restore (SQL Server)

**Policy (minimum):** a full backup daily, a differential every 6 hours, and transaction log backups every
15 minutes (needs the FULL recovery model). Keep 14 days locally and 35+ days off-site, encrypted. Test a
restore every month.

```sql
-- once
ALTER DATABASE Farm360 SET RECOVERY FULL;

-- scheduled (SQL Agent on Standard+, or cron + sqlcmd on Express)
BACKUP DATABASE Farm360 TO DISK = '/var/opt/mssql/backup/Farm360_full_$(date).bak'
  WITH COMPRESSION, CHECKSUM, INIT;
BACKUP DATABASE Farm360 TO DISK = '/var/opt/mssql/backup/Farm360_diff.bak'
  WITH DIFFERENTIAL, COMPRESSION, CHECKSUM, INIT;
BACKUP LOG Farm360 TO DISK = '/var/opt/mssql/backup/Farm360_log.trn' WITH COMPRESSION, CHECKSUM;
```

With the bundled container, run the commands with
`docker compose ... exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "..."`.
`/var/opt/mssql/backup` is the `sqlserver_backups` volume; copy it off the host (for example `rclone` to object storage).
Express has no SQL Agent, so schedule the backups with host cron.

**Restore runbook**

1. Stop writers: `docker compose ... stop api` (and any worker instance).
2. Back up the tail of the log if the database is still readable: `BACKUP LOG Farm360 TO DISK='...tail.trn' WITH NORECOVERY;`
3. Restore:
   ```sql
   RESTORE DATABASE Farm360 FROM DISK='.../Farm360_full.bak' WITH NORECOVERY, REPLACE;
   RESTORE DATABASE Farm360 FROM DISK='.../Farm360_diff.bak' WITH NORECOVERY;
   RESTORE LOG Farm360 FROM DISK='.../Farm360_log.trn' WITH NORECOVERY;  -- repeat in order; STOPAT='2026-…' for point in time
   RESTORE DATABASE Farm360 WITH RECOVERY;
   ```
4. If the backup is older than the deployed image's schema, run the migration bundle.
5. Start the API and check `/health/ready`. Also check Hangfire, because recurring jobs re-register at startup.
6. Flush Redis (`redis-cli FLUSHDB` on the cache DB) so no cached tenant or permission data predates the restore.
   The Data Protection key ring is stored in the same Redis instance, under `Farm360:DataProtection-Keys`. Keep that key, or users must request new password-reset links.

Uploads (`api_uploads` volume) are not in the database. Back them up with the same schedule (for example a nightly `tar` or `rclone sync`).

## 5. Observability

- Logs: compact JSON on stdout. Each event carries `CorrelationId` (echoed in the `X-Correlation-Id` response header), `TenantId` and `TraceId`/`SpanId`, and request-completion events add `UserId` and `ClientIp`. Health probe requests are only logged when they fail.
- Seq: set `Serilog__Seq__ServerUrl`.
- OpenTelemetry: set `OpenTelemetry__Endpoint` (OTLP gRPC, for example `http://otel-collector:4317`, or `OpenTelemetry__Protocol=httpprotobuf` on port 4318).
- Probes: liveness `/health/live` and readiness `/health/ready`. Do not use readiness as the liveness probe, or a database outage will restart every pod.
