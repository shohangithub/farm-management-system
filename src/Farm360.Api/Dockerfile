# syntax=docker/dockerfile:1.7
# =============================================================================
# Farm360 API — production image
# Build from the REPOSITORY ROOT (the context needs Directory.*.props and every src project):
#   docker build -f src/Farm360.Api/Dockerfile -t farm360-api .
# =============================================================================

# ── 1. Restore (cached until a project file or central package version changes) ──
FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS restore
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Farm360.Api/Farm360.Api.csproj                       src/Farm360.Api/
COPY src/Farm360.Application/Farm360.Application.csproj       src/Farm360.Application/
COPY src/Farm360.Contracts/Farm360.Contracts.csproj           src/Farm360.Contracts/
COPY src/Farm360.Domain/Farm360.Domain.csproj                 src/Farm360.Domain/
COPY src/Farm360.Identity/Farm360.Identity.csproj             src/Farm360.Identity/
COPY src/Farm360.Infrastructure/Farm360.Infrastructure.csproj src/Farm360.Infrastructure/
COPY src/Farm360.Persistence/Farm360.Persistence.csproj       src/Farm360.Persistence/
COPY src/Farm360.Shared/Farm360.Shared.csproj                 src/Farm360.Shared/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/Farm360.Api/Farm360.Api.csproj

# ── 2. Publish ───────────────────────────────────────────────────────────────
FROM restore AS publish
ARG BUILD_CONFIGURATION=Release
COPY src/ src/
# Development overrides (local connection strings, dev JWT key) never ship in the image.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/Farm360.Api/Farm360.Api.csproj \
        -c $BUILD_CONFIGURATION \
        --no-restore \
        -o /app/publish \
        /p:UseAppHost=false \
    && rm -f /app/publish/appsettings.Development.json

# ── 2b. EF Core migrations bundle (optional target: --target migrator) ───────
# Self-contained executable that applies pending ApplicationDbContext migrations and exits.
#   docker build -f src/Farm360.Api/Dockerfile --target migrator -t farm360-migrator .
#   docker run --rm -e ConnectionStrings__DefaultConnection="..." farm360-migrator
FROM restore AS efbundle
COPY dotnet-tools.json ./
COPY src/ src/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet tool restore \
    && dotnet ef migrations bundle \
        --project src/Farm360.Persistence/Farm360.Persistence.csproj \
        --startup-project src/Farm360.Api/Farm360.Api.csproj \
        --context ApplicationDbContext \
        --configuration Release \
        --self-contained -r linux-x64 \
        --output /out/efbundle \
        --force

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
COPY --from=efbundle /out/efbundle /app/efbundle
USER $APP_UID
# The connection string comes from the environment so it never appears in `docker ps`/process args.
ENTRYPOINT ["/bin/sh", "-c", "exec /app/efbundle --connection \"$ConnectionStrings__DefaultConnection\" \"$@\"", "--"]

# ── 3. Runtime ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# curl: container HEALTHCHECK. fontconfig + DejaVu: QuestPDF/ClosedXML font discovery on Linux
# (report body fonts are embedded; these cover fallback glyphs and Excel column measurement).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libfontconfig1 fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publish /app/publish .

# Writable locations for the non-root user:
#   wwwroot/uploads  — LocalFileStorageService (mount a volume; share it between instances)
#   keys             — Data Protection key ring when DataProtection__KeysPath=/app/keys
#   logs             — only if Serilog__File__Enabled=true
RUN mkdir -p /app/wwwroot/uploads /app/keys /app/logs \
    && chown -R $APP_UID:$APP_UID /app/wwwroot/uploads /app/keys /app/logs

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=1 \
    Serilog__File__Enabled=false \
    Serilog__Console__Json=true

EXPOSE 8080
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Farm360.Api.dll"]
