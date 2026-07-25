# Phase 1 Tasks: Authentication & Organization CRUD

## Backend — Authentication (Identity Layer)
- [x] Verify prior stabilization plan fixes (PermissionPolicyProvider already registered in Program.cs)
- [ ] **C1/C2**: Fix `AuthService.cs` — resolve real tenant + role from `TenantUser` at login & refresh
- [ ] **H3**: Fix `AuthService.cs` — call `userManager.AccessFailedAsync()` on failed login, reset on success
- [ ] **H4**: Fix `AuthService.cs` — update `user.LastLoginAt` on successful login
- [ ] **H5**: Remove stub `forgot-password` and `reset-password` from `AuthEndpoints.cs`

## Backend — Organization CRUD (Domain + Application)
- [ ] **C3**: Fix `BusinessType` enum — align to Farm, Supplier, Buyer, VeterinaryClinic, Cooperative
- [ ] **M4/H8**: Fix `UpdateOrganizationCommandValidator` — add TimeZoneId/LanguageCode rules + full uniqueness check
- [ ] Verify EF global query filter covers GetById tenant isolation (H6/H7) — add comment

## Frontend — Authentication (Angular)
- [ ] **H2**: Fix `auth.service.ts` — await logout API before clearing session
- [ ] **M6**: Fix `auth.interceptor.ts` — reset refresh state on session end
- [ ] **M1**: Rebuild `login.component.html` + `login.component.css` with project design system

## Frontend — Organization CRUD (Angular)
- [ ] **C4**: Fix org form currency dropdown — add BDT as first option
- [ ] **M2**: Add Address section to `organization-form.html`
- [ ] **C3/M5**: Fix BusinessType options in `organization-form.html` and display in `organization-list.component.ts`
