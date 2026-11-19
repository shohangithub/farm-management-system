# syntax=docker/dockerfile:1.7
# =============================================================================
# Farm360 Web (Angular SPA) — production image: node build → nginx
# Build from src/Farm360.Web:
#   docker build -t farm360-web src/Farm360.Web
#
# API location — pick one:
#   (default) same origin: leave API_BASE_URL empty; nginx proxies /api, /hubs and /uploads to
#             API_UPSTREAM (runtime env, default http://api:8080). Required for SignalR, which
#             always connects to the relative /hubs path.
#   separate origin: --build-arg API_BASE_URL=https://api.example.com (baked into the bundle) and
#             add the SPA origin to the API's Cors__AllowedOrigins.
# =============================================================================

# ── 1. Build ─────────────────────────────────────────────────────────────────
FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN --mount=type=cache,id=npm,target=/root/.npm \
    npm ci --no-audit --no-fund

COPY . .
ARG API_BASE_URL=""
RUN sed -i -E "s#apiUrl:[[:space:]]*'[^']*'#apiUrl: '${API_BASE_URL}'#" src/environments/environment.prod.ts \
    && grep -q "apiUrl: '${API_BASE_URL}'" src/environments/environment.prod.ts \
    && npx ng build --configuration production

# ── 2. Serve (non-root nginx, listens on 8080) ───────────────────────────────
FROM nginxinc/nginx-unprivileged:1.27-alpine AS final

# /etc/nginx/templates/*.template are rendered with envsubst at container start.
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist/farm360-web/browser /usr/share/nginx/html

# API_UPSTREAM: where /api, /hubs and /uploads are proxied (same-origin mode).
# NGINX_RESOLVER: DNS used to resolve API_UPSTREAM lazily (127.0.0.11 = Docker's embedded DNS;
#                 on Kubernetes use the cluster DNS service IP).
ENV API_UPSTREAM=http://api:8080 \
    NGINX_RESOLVER=127.0.0.11 \
    NGINX_ENVSUBST_FILTER="^(API_UPSTREAM|NGINX_RESOLVER)$"

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://127.0.0.1:8080/healthz || exit 1
