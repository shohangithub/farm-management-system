# Changelog

All notable changes to the Farm360 AI project will be documented in this file.

## [Unreleased]

### Fixed
- **Organization Module CRUD HTTP 500 Errors** — Root cause: Command handlers (`CreateOrganizationCommand`, `UpdateOrganizationCommand`, `DeactivateOrganizationCommand`) were manually calling `BeginTransactionAsync`/`CommitTransactionAsync` inside the handler body while the `TransactionBehavior` MediatR pipeline was simultaneously managing a transaction, causing a double-nested transaction SQL Server exception. Fixed by:
  - Removing manual `BeginTransactionAsync`/`CommitTransactionAsync` calls from all three handlers.
  - Adding `ITransactionalCommand` marker interface to all three command records so `TransactionBehavior` correctly manages the transaction.
  - Replacing the manual transaction calls with `await _unitOfWork.SaveChangesAsync()`.
- **Organization Name Uniqueness Check** (`OrganizationRepository.ExistsByNameAsync`) — Replaced `EF.Functions.Like(o.Name, name)` with direct equality `o.Name == name`. The `Like` call without wildcards was semantically misleading and triggers CA analyzer errors.
- **Angular Form `businessType` Integer Coercion** — Changed `<option [value]="n">` to `<option [ngValue]="n">` to ensure the value is sent as a number (not a string) to the backend. Also added `+formValue.businessType` cast in `onSubmit()`.
- **Angular Error Handling** — Form components now extract `err.error.detail` / `err.error.title` from ProblemDetails responses for user-friendly error messages. Added success message display.


### Added
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
