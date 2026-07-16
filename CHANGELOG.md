# Changelog

All notable changes to the Farm360 AI project will be documented in this file.

## [Unreleased]
### Added
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
