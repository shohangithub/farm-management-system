# Changelog

All notable changes to the Farm360 AI project will be documented in this file.

## [Unreleased]

### Refactored & Hardened (Enterprise Architecture Sprint)
- **Livestock & Health Module Production-Readiness Audit & Certification**:
  - Completed end-to-end audit across backend (Domain, Persistence, CQRS, Minimal APIs, Multi-tenancy, Security) and frontend (Signals, OnPush, Material 22 UI, error handling).
  - Verified cross-module workflows: `RecordMortalityCommand` automatically sets `AnimalStatus.Dead` in Livestock; duplicate mortality pre-validation and database unique index error handling return clean HTTP 409 `ProblemDetails`.
  - Added centralized `parseApiError` utility for parsing RFC 7807 problem details in all Health dialogs.
  - Verified 100% test pass rate across xUnit test suites (150 passing tests) and zero Angular compilation errors.
  - Marked both **Livestock Management** and **Health & Vaccination** modules as **Production Ready** ✅.
- **Smart Feeding & Nutrition Module (Fully Implemented & Verified)**:
  - Created `feeding` schema tables (`FeedIngredients`, `FeedFormulas`, `FormulaIngredients`, `FeedingSchedules`, `FeedConsumptionLogs`, `ConsumptionDetails`) with EF Core configurations and migration `AddFeedingModule`.
  - Implemented Domain Aggregate Roots (`FeedIngredient`, `FeedFormula`, `FeedingSchedule`, `FeedConsumptionLog`) with `NutritionalProfile` Value Object.
  - Implemented Application CQRS commands, queries, DTOs, and FluentValidation validators for ingredient management, ration formulation, schedule assignment, and daily consumption logging.
  - Added FCR calculation engine (`GetFcrAnalyticsQuery`) computing Feed Conversion Ratio ($FCR = \frac{\text{Total Feed (kg)}}{\text{Total Weight Gain (kg)}}$) and monthly trend analytics.
  - Exposed REST Minimal APIs under `/api/v1/feeding/*` secured with `PermissionConstants.FeedingModule` policies.
  - Built Angular 22 frontend module (`src/app/features/feeding`) with Signal reactivity, `OnPush` change detection, Material 22 components, dialogs, and enabled sidebar navigation.
  - Verified 100% solution build (`dotnet build` & `npm run build`) and unit tests pass (154/154 passing tests).
- **Livestock Module Production-Readiness Fixes**:
  - Implemented missing FluentValidation validators for all Livestock commands (`RecordBcsCommand`, `RecordMatingCommand`, `ConfirmPregnancyCommand`, `RecordCalvingCommand`, `CreateBatchCommand`, `UploadAnimalPhotoCommand`, `TransferAnimalCommand`).
  - Enforced a maximum of 5 photos per animal in the domain layer.
  - Added batch filtering to the `GetAnimalList` API and repository.
  - Added strict parameter typing for `AnimalService.recordMating()` in the frontend.
  - Surfaced `BuyerName` and `SaleWeightKg` to the `AnimalDto`.
  - Fixed pregnancy confirmation date validation rules.
  - Removed redundant manual tenant checks and aligned with EF Core Global Query Filter.
  - Standardized error handling to use `NotFoundException` instead of `ArgumentException`.
- **EF Core Global Query Filter Combination** — Fixed EF Core filter overwriting issue by combining `TenantId == CurrentTenantId` and `IsDeleted == false` into a single combined Lambda Expression per entity type in `ApplicationDbContext.OnModelCreating`.
- **AuditSaveChangesInterceptor** — Hardened interceptor to automatically populate `TenantId` on `EntityState.Added` if `TenantId == Guid.Empty` using domain `SetTenantId()` helper, preventing unassigned tenant IDs and enforcing cross-tenant write boundaries.
- **Multi-Channel Tenant Resolution** — Refactored `TenantResolutionMiddleware` to support hierarchical resolution strategies: (1) JWT claim `tenant_id`, (2) `X-Tenant-Id` HTTP Header, (3) Host Subdomain (`{slug}.farm360.ai`). Validates active tenant state against `Tenant` aggregate root and Redis cache.
- **Security Headers Middleware** — Added production security response headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `X-XSS-Protection`, `Referrer-Policy`) to API pipeline in `Program.cs`.
- **Authentication Session Persistence & Auto-Login Lifecycle** — Implemented production session restoration lifecycle:
  - Added `provideAppInitializer` in `app.config.ts` executing `AuthService.initializeSession()` during Angular bootstrap to block router navigation until session state or silent refresh resolves.
  - Upgraded `authInterceptor` with silent token refresh (`/api/v1/auth/refresh`) and concurrent request queuing via `BehaviorSubject<string | null>` for HTTP 401 Unauthorized handling.
  - Enhanced `UserProfileDto` and `GetCurrentUserQuery` in backend to return the user's permissions array (`Permissions`) via `IPermissionService`.
  - Updated `authGuard` to await `authService.isInitialized$` before evaluating authentication state.
  - Added a dark enterprise session restoration splash screen ("Restoring Farm360 Session...") in `AppComponent` to eliminate login screen flashes on page refresh.
- **Frontend Route & Module Alignment** — Updated Angular components (`farm-list`, `shed-list`, `pen-list`) to support direct and nested route parameters (`snapshot.paramMap` and `parent.snapshot.paramMap`). Verified Angular 22 production bundle compilation with zero build errors.
- **Test Suite Alignment** — Aligned MediatR transactional expectations across `ScheduleVaccinationCommandTests`, `LogMedicalTreatmentCommandTests`, `ReportDiseaseIncidentCommandTests`, and `CreateOrganizationCommandHandlerTests`. Updated Architecture test layer namespace rules. All 150 unit, integration, architecture, and functional tests pass with 100% success rate.

### Fixed
- **Backend Build & Architecture Violations**: Fixed Application layer build failures caused by missing namespaces (`Farm360.Application.Common.Exceptions` and `Farm360.Application.Common.Interfaces`). Removed direct `ApplicationDbContext` references from Application layer commands, strictly enforcing the usage of Repositories and `IUnitOfWork`.
- **Null Checks (IDE0270)**: Simplified null checking in CQRS handlers by utilizing the null-coalescing operator (`?? throw new NotFoundException(...)`).
- **Inventory UI Mismatches**: Corrected gradient anomalies in the Supplier list page by enforcing the globally standard emerald-teal `bg-gradient-to-br` pattern.
- **Authentication System (IDX10703 Error)** - Fixed `IDX10703: Cannot create a 'Microsoft.IdentityModel.Tokens.SymmetricSecurityKey', key length is zero` error by renaming `SecretKey` to `Secret` in `appsettings.Development.json` to match the binding class, and added startup validation to enforce minimum JWT secret length.
- **Organization Module CRUD HTTP 500 Errors** — Root cause: Command handlers (`CreateOrganizationCommand`, `UpdateOrganizationCommand`, `DeactivateOrganizationCommand`) were manually calling `BeginTransactionAsync`/`CommitTransactionAsync` inside the handler body while the `TransactionBehavior` MediatR pipeline was simultaneously managing a transaction, causing a double-nested transaction SQL Server exception. Fixed by:
  - Removing manual `BeginTransactionAsync`/`CommitTransactionAsync` calls from all three handlers.
  - Adding `ITransactionalCommand` marker interface to all three command records so `TransactionBehavior` correctly manages the transaction.
  - Replacing the manual transaction calls with `await _unitOfWork.SaveChangesAsync()`.
- **Organization Name Uniqueness Check** (`OrganizationRepository.ExistsByNameAsync`) — Replaced `EF.Functions.Like(o.Name, name)` with direct equality `o.Name == name`. The `Like` call without wildcards was semantically misleading and triggers CA analyzer errors.
- **Authorization System (HTTP 500 / 403 Errors)** — Fixed a critical integration gap where `PermissionPolicyProvider` and `PermissionHandler` were missing from the DI container, causing HTTP 500 errors on Livestock endpoints. Registered them correctly in `Farm360.Api/Program.cs`. Fixed the JWT claim checking pattern on 30+ endpoints across Organization, Branch, Farm, Shed, Pen, and Health modules to use the correct named policy pattern (e.g., `"Permission:organizations.view"`) instead of `RequireClaim("Permission", ...)` which was looking for a non-existent claim. Corrected Livestock permission codes (`animals.read` → `animals.view`, etc.) and seeded missing permissions to resolve all HTTP 403 errors for non-admin users.
- **Tenant Resolution Middleware** — Updated `TenantResolutionMiddleware` to gracefully handle MVP tenant contexts derived from the JWT `tenant_id` claim, preventing immediate 404 responses for non-system users while the underlying Tenant database entity is being built.
- **Angular 403 Handler** — Added a global HTTP 403 Forbidden handler in `auth.interceptor.ts` using `MatSnackBar` to show a user-friendly "Access Denied" message instead of failing silently.
- **Authentication 307 Redirect Loop & UI Freeze** — Fixed a critical issue where `GET /api/v1/auth/me` requests were caught in a 307 temporary redirect loop that stripped the `Authorization` header, resulting in a 401 Unauthorized response and forcing the UI into an indefinite "Signing In..." state. Fixes applied:
  - Directed the Angular proxy (`proxy.conf.json`) to target the HTTPS port (`7272`) instead of the HTTP port (`5259`), completely bypassing the backend's `app.UseHttpsRedirection()` logic.
  - Improved `login.component.ts` to reliably reset `isLoading = false` if the router cancels a navigation event.
  - Implemented missing `forgot-password`, `reset-password`, and `profile` endpoints to complete the authentication module API surface.
  - Updated `TenantResolutionMiddleware` to gracefully handle the mock/system Admin Tenant ID (`Guid.Empty`) instead of throwing an early `404 Resource not found`.
- **Angular Form `businessType` Integer Coercion** — Changed `<option [value]="n">` to `<option [ngValue]="n">` to ensure the value is sent as a number (not a string) to the backend. Also added `+formValue.businessType` cast in `onSubmit()`.
- **Angular Error Handling** — Form components now extract `err.error.detail` / `err.error.title` from ProblemDetails responses for user-friendly error messages. Added success message display.


### Added
- **Inventory Module**:
  - Implemented backend CQRS Commands (`DeleteSupplierCommand`, `DeleteInventoryItemCommand`), Handlers, and Minimal API Endpoints for deleting Suppliers and Inventory Items.
  - Implemented frontend deletion functionality using `ConfirmationDialogComponent` (Premium Dialog Pattern) on `supplier-list.component.ts` and `inventory-item-list.component.ts`.
- **Backend Architecture Skill (Customization)**: Massively upgraded the `backend_architecture_pattern` SKILL.md file to serve as a definitive, full CRUD + Pagination reference guide. It explicitly outlines how to prevent common architectural faults (e.g., namespace errors, leaky context boundaries).

### Changed
- **Authentication API & UI**
  - Added `IAuthService` and implementation for Login, Token Refresh, Logout, and User Registration.
  - Added `/api/v1/auth/login`, `/refresh`, `/logout`, `/register`, and `/me` endpoints.
  - Angular `AuthService`, `authInterceptor`, and `authGuard` implemented for session management.
  - Angular Standalone Login Component (`<app-login>`) with responsive layout and error handling.
  - Protected application routes requiring an active JWT token.
- Enterprise UI Shared Components (`PageHeaderComponent`, `DataTableComponent`, `ConfirmationDialogComponent`, `EmptyStateComponent`, `LoadingComponent`, `BreadcrumbComponent`).

### Changed
- Migrated Livestock module UI to use the new Enterprise UI Shared Components and Angular Material.
- **Enterprise Application Shell** (Angular UI).
  - Integrated Angular Material (`@angular/material` 22).
  - Created `MainLayoutComponent` with responsive `mat-sidenav`.
  - Created `HeaderComponent` with Context Switcher, Global Search, Notifications, and Profile menu.
  - Implemented dynamic Dark/Light Mode toggling.
- **Pen Management Module** (Domain, Persistence, Application CQRS, API Endpoints, Angular UI).
  - Drag-and-drop ready architecture for Pen Dashboard.
  - Animal assignment ready properties.
  - Pen capacity indicators in UI.
- **Master Data Module** (Domain, Persistence, Application CQRS, API Endpoints, Angular UI).
  - Generic Master Data Architecture for 14 reference types (Breed, Animal Type, Feed Type, etc.).
  - Explicit Hierarchical Location Entities (Country -> Division -> District -> Upazila -> Union -> Village).
  - Angular Caching `MasterDataService` and `LocationService`.
  - Reusable `<app-master-data-dropdown>` and `<app-location-selector>` Angular Standalone UI Components.
- **Shed Management Module:**
  - `Shed` Aggregate Root inside `Farm360.Domain.Farms` context.
  - Shed Management API (`/api/v1/farms/{farmId}/sheds`).
  - Shed UI: List, Details, Create/Edit Forms, and Dashboard Widget.
  - Cross-context validation preventing duplicate sheds per farm.
