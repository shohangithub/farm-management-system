# Farm360 AI — Production Stabilization Audit

## 1. Executive Audit Summary

The Farm360 AI application is structurally sound (Clean Architecture, CQRS, DDD) but has **several critical integration gaps** introduced during incremental AI-assisted development. The most severe issues are in the **Authorization and Permission system**, followed by minor Angular UI consistency issues. Core authentication is working. Database structure is correct. There are no circular dependencies or architecture violations.

**Overall Risk Level: HIGH → Will be MEDIUM after the fixes in this plan.**

---

## 2. Architecture Issues

| Severity | Component | Finding |
|---|---|---|
| ✅ OK | Clean Architecture | Layers correctly separated |
| ✅ OK | CQRS Pipeline | `TransactionBehavior`, `ValidationBehavior` correctly chained |
| ✅ OK | MediatR | Handlers, validators registered correctly |
| ✅ OK | FluentValidation | Registered and integrated |
| ⚠️ WARN | `ApiServiceExtensions.cs` | Duplicates CORS and registers services. Never called from `Program.cs` — dead code |

---

## 3. Authentication Issues

| Severity | Finding |
|---|---|
| ✅ OK | JWT HS256 signing correctly configured |
| ✅ OK | JWT secret validated at startup (≥32 chars) |
| ✅ OK | Token claims: `sub`, `tenant_id`, `role`, `tv`, `tier`, `farms`, `sys`, `perms` all emitted |
| ✅ OK | Refresh token rotation implemented |
| ⚠️ WARN | `AuthService.cs` hardcodes `role = "Owner"` and `tenantId = Guid.Empty` — MVP placeholder |
| ✅ OK | Angular `authInterceptor` attaches `Bearer` token to all `/api/` requests |
| ✅ OK | Angular `authGuard` protects all main routes |

---

## 4. Authorization Issues — **CRITICAL**

### 4.1 `PermissionPolicyProvider` and `PermissionHandler` Never Registered

**Root Cause of HTTP 500 on Livestock + HTTP 403 on all other modules:**

`PermissionPolicyProvider` and `PermissionHandler` exist in `Farm360.Api/Authorization/` but are **never registered in the DI container**. The comment in `IdentityServiceExtensions.cs` (line 105) says they are registered by `ApiServiceExtensions`, but `ApiServiceExtensions.AddApiServices()` is **never called from `Program.cs`** and doesn't actually register them anyway.

**Effect:**
- Livestock endpoints use named policy `"Permission:animals.read"` → policy provider not found → `InvalidOperationException` → HTTP 500
- `PermissionHandler` is not invoked anywhere

### 4.2 Wrong JWT Claim Pattern in All Non-Livestock Endpoints

The JWT emits permissions as a single `"perms"` claim: `"organizations.view,farms.view,..."`.

All Organization/Branch/Farm/Shed/Pen/Health endpoints use:
```csharp
.RequireAuthorization(policy => policy.RequireClaim("Permission", "organizations.view"))
```
This looks for a claim **named** `"Permission"` — which does not exist in the JWT. The `"perms"` claim is different. Non-system users always get HTTP 403.

**System user (admin)** bypasses all permission checks in `PermissionHandler` (step 2: `IsSystemUser`), which is why admin works for some operations.

### 4.3 Livestock Permission Code Mismatches

| Endpoint Policy String | Seeded Code | Status |
|---|---|---|
| `animals.read` | `animals.view` | ❌ Mismatch |
| `animals.write` | `animals.create` / `animals.edit` | ❌ Mismatch |
| `animals.sell` | Not seeded | ❌ Missing |
| `animals.quarantine` | Not seeded | ❌ Missing |
| `animals.delete` | `animals.delete` | ✅ Match |

### 4.4 Missing Permissions in DataSeeder `All` List

`PenModule` and some other permissions are defined in `PermissionConstants` but NOT included in the `All` list (lines 139–200) — so they are never seeded into the database.

**Verified missing from `All` list:**
- `PenModule.View/Create/Edit/Delete` — not in `All`
- `MasterDataModule.View/Manage` — not in `All`

---

## 5. Database Issues

| Severity | Finding |
|---|---|
| ✅ OK | Migrations correctly structured |
| ✅ OK | Seed data: Permissions, Roles, RolePermissions seeded on startup |
| ✅ OK | Admin user seeded via `IdentitySeeder` |
| ✅ OK | Soft delete pattern via `IsDeleted` and `IgnoreQueryFilters` correct |
| 🔴 CRITICAL | `TenantResolutionMiddleware` — real Tenant DB lookup is a TODO placeholder. Non-system users with any `tenantId` other than `Guid.Empty` get HTTP 404. Blocks multi-tenant operation |
| ⚠️ WARN | Permission seeding: some `PermissionConstants` entries not in `All` list → not seeded to DB |

---

## 6. API Issues Summary

| Severity | Endpoint Group | Issue |
|---|---|---|
| 🔴 500 | `GET /api/v1/livestock/animals` | `PermissionPolicyProvider` not registered |
| 🔴 403 | All Organization endpoints | Wrong JWT claim name in `RequireClaim` |
| 🔴 403 | All Branch endpoints | Wrong JWT claim name |
| 🔴 403 | All Farm/Shed/Pen endpoints | Wrong JWT claim name |
| 🔴 403 | All Health endpoints | Wrong JWT claim name |
| ✅ 200 | `POST /api/v1/auth/login` | Working |
| ✅ 200 | `GET /api/v1/auth/me` | Working |
| ✅ 204 | `POST /api/v1/auth/logout` | Working |

---

## 7. Angular Issues

| Severity | Component | Finding |
|---|---|---|
| ✅ OK | `authInterceptor` | Correctly attaches Bearer token |
| ✅ OK | `authGuard` | Correctly protects routes |
| ✅ OK | All form components | `[ngValue]` fixed, error handling present |
| ⚠️ WARN | `auth.interceptor.ts` | On 401, calls `logout()` immediately — no refresh token attempt |
| ⚠️ WARN | `organization-list.component.ts` | HTML string injection in cell renderers — minor XSS risk in TypeScript `cell: () => '<div>...'` |
| ⚠️ WARN | Sidebar | Dashboard route disabled (badge: 'soon') — correct for now |

---

## 8. Security Issues

| Severity | Finding |
|---|---|
| ✅ OK | JWT secret enforced at startup |
| ✅ OK | HTTPS configured, CORS restricted |
| ✅ OK | `GlobalExceptionMiddleware` hides stack traces from clients |
| ⚠️ WARN | `TenantResolutionMiddleware` placeholder allows all requests with `Guid.Empty` tenant |
| ⚠️ WARN | Tokens in `localStorage` (standard for SPAs; acceptable for MVP) |

---

## 9. Performance Issues

| Severity | Finding |
|---|---|
| ✅ OK | `PermissionService` Redis-cached with 5-min TTL |
| ✅ OK | Angular `MasterDataService` caches with `BehaviorSubject` |
| ⚠️ WARN | `PermissionHandler` slow path DB lookup on cache miss |

---

## 10. Root Cause Analysis

### Primary: Missing Authorization DI Registration
`PermissionPolicyProvider` and `PermissionHandler` are never registered → `"Permission:..."` policies throw 500.

### Secondary: Wrong Claim Name Pattern
JWT has `perms` claim. Endpoints check for `"Permission"` claim. These never match → HTTP 403 for all non-system users.

### Tertiary: Livestock Permission Code Mismatch  
Even with the policy provider registered, `"animals.read"` / `"animals.write"` are not seeded codes.

---

## 11. Prioritized Fix Plan

### P0 — Critical Fixes (in this order)

**Fix 1: Register `PermissionPolicyProvider` and `PermissionHandler` in `Program.cs`**

Add to `IdentityServiceExtensions.cs` (or directly in `Program.cs`):
```csharp
services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
services.AddScoped<IAuthorizationHandler, PermissionHandler>();
```

**Fix 2: Standardize all endpoints to use named permission policy pattern**

Replace all inline `RequireClaim` with the named policy pattern that works with `PermissionPolicyProvider`:
```csharp
// Before (broken):
.RequireAuthorization(policy => policy.RequireClaim("Permission", "organizations.view"))

// After (correct):
.RequireAuthorization("Permission:organizations.view")
```

**Fix 3: Fix Livestock permission code mismatches**

Update `LivestockEndpoints.cs`:
- `"Permission:animals.read"` → `"Permission:animals.view"`  
- `"Permission:animals.write"` → `"Permission:animals.create"` (for POST) or `"Permission:animals.edit"` (for PUT)
- `"Permission:animals.sell"` — Add `animals.sell` to `PermissionConstants.Animals` and DataSeeder
- `"Permission:animals.quarantine"` — Add `animals.quarantine` to `PermissionConstants.Animals` and DataSeeder

**Fix 4: Add missing permissions to `PermissionConstants.All` seeder list**

Add `PenModule`, `MasterDataModule`, new Livestock permissions.

### P1 — High Priority

**Fix 5: `TenantResolutionMiddleware`** — Implement a real DB lookup for non-system tenants (or document the current system-only limitation clearly).

### P2 — Medium Priority

**Fix 6: Remove or integrate `ApiServiceExtensions.cs` dead code.**
**Fix 7: Add global 403 handler in Angular interceptor.**

> [!IMPORTANT]
> All changes are surgical and non-breaking. No architecture changes. No module rewrites. Approve to proceed with implementation.
