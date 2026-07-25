# Phase 1: Authentication & Organization CRUD — Production-Ready Audit & Fix Plan

## Background

The Farm360 application is a modular monolith built on Clean Architecture + DDD + CQRS + MediatR with an Angular 22 frontend. The `DEVELOPMENT_STATUS.md` marks both Authentication and Organization Management as "COMPLETED", but a deep-dive audit reveals **significant gaps, TODOs, spec violations, and production blockers** that contradict this status.

This plan addresses all findings for the **Authentication (Login)** and **Organization CRUD** modules only.

---

## User Review Required

> [!IMPORTANT]
> **HS256 vs RS256**: The Auth Architecture doc (`9_Farm360_Auth_Architecture.md`) mandates **RS256 with AWS KMS**. The current implementation uses **HS256 (symmetric)**. This plan **retains HS256 for MVP/dev** and defers RS256/KMS to a production-readiness phase, since RS256 requires KMS infrastructure that doesn't exist yet. The JWT comment in `JwtTokenService.cs` already acknowledges this: *"HS256 (upgrade to RS256 via JWKS in production)"*. **Do you agree with this approach, or should we implement RS256 now?**

> [!IMPORTANT]
> **Tenant Resolution at Login**: Currently `AuthService.LoginWithPasswordAsync` hardcodes `tenantId = Guid.Empty` and `role = "Owner"`. This means **every logged-in user gets Guid.Empty as their tenant and "Owner" as their role**, breaking all downstream multi-tenant queries. This plan fixes this by resolving the user's tenant and role from `TenantUser` during login. **Is it acceptable for MVP that a user logs into their first/default tenant, or should we show a tenant picker?**

> [!WARNING]
> **Missing Logout Endpoint Integration**: The frontend `AuthService.logout()` fires `POST /api/v1/auth/logout` with `{ refreshToken }` but **does NOT await the response** and clears session immediately. If the API call fails, the server-side session is never revoked. This plan fixes this.

## Open Questions

> [!IMPORTANT]
> **Q1: BusinessType Enum Mismatch** — The Angular org form hardcodes `1=Farm, 2=Supplier, 3=Buyer, 4=VeterinaryClinic, 5=Cooperative`, but the C# `BusinessType` enum defines `1=SoleProprietorship, 2=Partnership, 3=LLC, 4=Corporation, 5=Cooperative`. Which is correct? The form or the enum? This causes data corruption on every save. **I will align the C# enum to match the form values** since the UI reflects the business intent (Farm, Supplier, Buyer, etc.).

> [!IMPORTANT]
> **Q2: Currency Selector Missing BDT** — The org form only offers USD/EUR/GBP, but the default in both the domain entity and form is **BDT** (Bangladesh Taka). The form will fail validation when creating a new org because the default `BDT` isn't in the dropdown. **I will add BDT as the first option.**

> [!IMPORTANT]
> **Q3: Organization Form Missing Address Section** — The form template has fields for Legal Details but **no Address section** (Street, City, State, Country, ZipCode). The form model defines them, but they're never rendered. **I will add the Address section to the form.**

---

## Architecture Audit Findings

### 🔴 CRITICAL (Breaks core functionality)

| # | Component | Finding | Impact |
|---|-----------|---------|--------|
| C1 | [AuthService.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Identity/Services/AuthService.cs#L31-L33) | `tenantId = Guid.Empty` and `role = "Owner"` hardcoded at login | All JWT tokens carry wrong tenant → tenant query filters return zero results → app is broken for non-system users |
| C2 | [AuthService.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Identity/Services/AuthService.cs#L69) | Same hardcoded `role = "Owner"` in `RefreshTokenAsync` | Same as C1 after token refresh |
| C3 | [BusinessType.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Domain/Organizations/Enums/BusinessType.cs) | Enum values don't match Angular form (SoleProprietorship vs Farm) | Every org create/update saves wrong business type |
| C4 | [organization-form.html](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/organizations/organization-form/organization-form.html#L60-L64) | Currency dropdown missing BDT (the default), so new orgs with BDT default fail on form interaction | Users can't create org without changing currency away from the default |

### 🟠 HIGH (Security/Data integrity)

| # | Component | Finding | Impact |
|---|-----------|---------|--------|
| H1 | [auth.service.ts](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/core/services/auth.service.ts#L45-L46) | Access token stored in `localStorage` | XSS attack vector — tokens are readable by any injected script |
| H2 | [auth.service.ts](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/core/services/auth.service.ts#L132-L138) | `logout()` fire-and-forgets the API call | Server-side session may never be revoked |
| H3 | [AuthService.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Identity/Services/AuthService.cs#L21) | Login does NOT increment `AccessFailedCount` on failure | Lockout policy never triggers — brute force is possible |
| H4 | [AuthService.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Identity/Services/AuthService.cs#L30) | Login does NOT update `LastLoginAt` on success | Audit trail incomplete |
| H5 | [AuthEndpoints.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Api/Endpoints/Auth/AuthEndpoints.cs#L65-L83) | `forgot-password` and `reset-password` are placeholder stubs with `Task.Delay(100)` | Non-functional features exposed as working endpoints |
| H6 | [GetOrganizationByIdQuery.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Application/Organizations/Queries/GetOrganizationByIdQuery.cs#L14-L15) | No tenant check — any authenticated user can fetch any org by ID if they guess the GUID | Cross-tenant data leak |
| H7 | [DeactivateOrganizationCommand.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Application/Organizations/Commands/DeactivateOrganizationCommand.cs#L27) | No tenant check — deactivation by ID without verifying tenant ownership | Cross-tenant mutation |
| H8 | [UpdateOrganizationCommand.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Application/Organizations/Commands/UpdateOrganizationCommand.cs#L47-L48) | Comment says "Complex unique check omitted for brevity" — uniqueness not validated on update | Duplicate org names silently accepted in some cases |

### 🟡 MEDIUM (Functionality/UX gaps)

| # | Component | Finding | Impact |
|---|-----------|---------|--------|
| M1 | [login.component.html](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/auth/login/login.component.html) | Uses Tailwind utility classes directly without the project's design system | Login page renders unstyled (Tailwind CSS is NOT installed — project uses vanilla CSS with utility classes in `styles.scss`) |
| M2 | [organization-form.html](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/organizations/organization-form/organization-form.html) | Address fields (Street, City, State, Country, ZipCode) defined in form model but NOT rendered in template | Users can't enter address data |
| M3 | [auth.guard.ts](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/core/guards/auth.guard.ts#L14) | `isAuthenticated` only checks if `accessToken` exists in localStorage, not if it's valid/expired | Expired tokens show the app shell before 401 redirect kicks in |
| M4 | [UpdateOrganizationCommand.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Application/Organizations/Commands/UpdateOrganizationCommand.cs#L44-L46) | Update validator missing `TimeZoneId` and `LanguageCode` NotEmpty rules (Create validator has them) | Inconsistent validation between create and update |
| M5 | [organization-list.component.ts](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/organizations/organization-list/organization-list.component.ts#L44) | Business type display uses wrong mappings (`1=Farm, 2=Supplier`) — same mismatch as C3 | Wrong labels shown in list |
| M6 | [auth.interceptor.ts](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/core/interceptors/auth.interceptor.ts#L7-L8) | Module-level `isRefreshing` and `refreshTokenSubject` state — not cleaned up between sessions | After logout + re-login, stale refresh state can cause requests to hang |

---

## Proposed Changes

### Component 1: Backend — Authentication Fix (Identity Layer)

---

#### [MODIFY] [AuthService.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Identity/Services/AuthService.cs)

**Fix C1, C2, H3, H4: Resolve tenant/role from DB, track failed attempts, update audit fields**

- Inject `IdentityDbContext` to query `TenantUser` for the user's tenant membership and role.
- Replace `tenantId = Guid.Empty` with actual tenant resolution: query `TenantUser` where `UserId == user.Id`, select the first active tenant membership.
- Replace `role = "Owner"` with the actual role from `TenantUser.RoleId → Role.Name`.
- On failed login: call `userManager.AccessFailedAsync(user)` to increment lockout counter.
- On successful login: reset access failed count, update `LastLoginAt`.
- Move lockout check **before** password validation (prevent timing attacks).

#### [MODIFY] [AuthEndpoints.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Api/Endpoints/Auth/AuthEndpoints.cs)

**Fix H5: Remove placeholder endpoints or mark them clearly**

- Remove the `forgot-password` and `reset-password` endpoint mappings entirely (they are stubs with `Task.Delay`). Dead endpoints that return success for non-functional features are worse than no endpoints at all.
- Add `// Phase 2: Password Reset` comment block.

---

### Component 2: Backend — Organization CRUD Fix (Application + Domain)

---

#### [MODIFY] [BusinessType.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Domain/Organizations/Enums/BusinessType.cs)

**Fix C3: Align enum with actual business intent**

```csharp
public enum BusinessType
{
    Farm = 1,
    Supplier = 2,
    Buyer = 3,
    VeterinaryClinic = 4,
    Cooperative = 5
}
```

#### [MODIFY] [GetOrganizationByIdQuery.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Application/Organizations/Queries/GetOrganizationByIdQuery.cs)

**Fix H6: Add tenant check**

- Inject `ITenantService` and use `GetByIdAndTenantAsync()` (or verify `org.TenantId == tenantService.TenantId` post-fetch).
- Note: EF Core global query filters already enforce tenant isolation on `GetByIdAsync` — however, we should verify this is the case and that the repository uses the DbContext-filtered query. If filters are active, this is already safe. **Verify and document.**

#### [MODIFY] [DeactivateOrganizationCommand.cs](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Application/Organizations/Commands/DeactivateOrganizationCommand.cs)

**Fix H7: Add tenant check (same as H6 — verify global query filter coverage)**

- Same verification as H6. If EF global query filters are active (they are, per `ApplicationDbContext`), then `GetByIdAsync` already filters by tenant. Document this explicitly with a comment.

#### [MODIFY] [UpdateOrganizationCommandValidator](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Application/Organizations/Commands/UpdateOrganizationCommand.cs)

**Fix M4, H8: Add missing validation rules + uniqueness check**

- Add `RuleFor(x => x.TimeZoneId).NotEmpty()` and `RuleFor(x => x.LanguageCode).NotEmpty()` to match Create validator.
- Add `MustAsync(BeUniqueName)` with tenant-scoped uniqueness check (excluding current ID).

---

### Component 3: Frontend — Authentication Fix (Angular)

---

#### [MODIFY] [auth.service.ts](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/core/services/auth.service.ts)

**Fix H1, H2: Secure token storage + proper logout**

- **H1 (Token Storage)**: Migrate from `localStorage` to `sessionStorage` for access tokens. This is a minimal improvement; the ideal solution (httpOnly cookies) requires backend changes. For this phase, `sessionStorage` prevents cross-tab XSS and clears on browser close.
- **H2 (Logout)**: Change `logout()` to properly await the API call and only clear session after success (or on API failure — clear anyway but log).

#### [MODIFY] [auth.interceptor.ts](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/core/interceptors/auth.interceptor.ts)

**Fix M6: Reset refresh state on session clear**

- Reset `isRefreshing = false` and `refreshTokenSubject.next(null)` when a refresh fails and logout is triggered.

#### [MODIFY] [login.component.html](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/auth/login/login.component.html)

**Fix M1: Rebuild login page using the project's actual design system**

- Rewrite the login template to use the project's existing CSS class conventions (`form-group`, `form-label`, `form-control`, dark mode classes from `styles.scss`) instead of raw Tailwind utility classes that don't resolve.
- Match the premium dark enterprise aesthetic of the rest of the app.

#### [MODIFY] [login.component.css](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/auth/login/login.component.css)

- Add login-page-specific styles (centered card layout, branding, gradient background).

---

### Component 4: Frontend — Organization CRUD Fix (Angular)

---

#### [MODIFY] [organization-form.html](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/organizations/organization-form/organization-form.html)

**Fix C4, M2: Fix currency dropdown + add Address section + fix BusinessType labels**

- Add `BDT (৳)` as the first currency option.
- Fix BusinessType options to match the corrected C# enum: `Farm, Supplier, Buyer, VeterinaryClinic, Cooperative`.
- Add Address section with Street, City, State, Country, ZipCode fields between Legal Details and the action buttons.

#### [MODIFY] [organization-list.component.ts](file:///d:/Personel/Farm%20Management%20System/src/Farm360.Web/src/app/features/organizations/organization-list/organization-list.component.ts)

**Fix M5: Fix business type display labels**

- Update the type column cell renderer to use the corrected labels matching the new `BusinessType` enum.

---

## Verification Plan

### Automated Tests

```bash
# Run existing test suites to ensure no regressions
dotnet test d:\Personel\Farm Management System\tests\ --filter "FullyQualifiedName~Organization|FullyQualifiedName~Auth"
```

### Manual Verification

1. **Login flow**: Start API + Angular dev server → navigate to `/auth/login` → login with seeded credentials → verify JWT contains correct `tenant_id` and `role` claims (decode via jwt.io or browser DevTools).
2. **Logout flow**: Click logout → verify server-side session is revoked (check `UserSessions` table).
3. **Organization CRUD**: Create org → verify BusinessType saves correctly → edit org → verify address fields persist → list orgs → verify type labels are correct → deactivate org → verify status change.
4. **Tenant isolation**: Login as User A (Tenant X) → try to GET `/api/v1/organizations/{orgIdOfTenantY}` → verify 404 (not the org data).
5. **Login page styling**: Verify the login page matches the dark enterprise design system.
6. **Failed login lockout**: Enter wrong password 5 times → verify account lockout response (HTTP 423).
