# Graph Report - src  (2026-08-19)

## Corpus Check
- 822 files · ~270,451 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 5721 nodes · 12809 edges · 336 communities (298 shown, 38 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 384 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Community 0
- Community 1
- Community 2
- Community 3
- Community 4
- Community 5
- Community 6
- Community 7
- Community 8
- Community 9
- Community 10
- Community 11
- Community 12
- Community 13
- Community 14
- Community 15
- Community 16
- Community 17
- Community 18
- Community 19
- Community 20
- Community 21
- Community 22
- Community 23
- Community 24
- Community 25
- Community 26
- Community 27
- Community 28
- Community 29
- Community 30
- Community 31
- Community 32
- Community 33
- Community 34
- Community 35
- Community 36
- Community 37
- Community 38
- Community 39
- Community 40
- Community 41
- Community 42
- Community 43
- Community 44
- Community 45
- Community 46
- Community 47
- Community 48
- Community 49
- Community 50
- Community 51
- Community 52
- Community 53
- Community 54
- Community 55
- Community 56
- Community 57
- Community 58
- Community 59
- Community 60
- Community 61
- Community 62
- Community 63
- Community 64
- Community 65
- Community 66
- Community 67
- Community 68
- Community 69
- Community 70
- Community 71
- Community 72
- Community 73
- Community 74
- Community 75
- Community 76
- Community 77
- Community 78
- Community 79
- Community 80
- Community 81
- Community 82
- Community 83
- Community 84
- Community 85
- Community 86
- Community 87
- Community 88
- Community 89
- Community 90
- Community 91
- Community 92
- Community 93
- Community 94
- Community 95
- Community 96
- Community 97
- Community 98
- Community 99
- Community 100
- Community 101
- Community 102
- Community 103
- Community 104
- Community 105
- Community 106
- Community 107
- Community 108
- Community 109
- Community 110
- Community 111
- Community 112
- Community 113
- Community 114
- Community 115
- Community 116
- Community 117
- Community 118
- Community 119
- Community 120
- Community 121
- Community 122
- Community 123
- Community 124
- Community 125
- Community 126
- Community 127
- Community 128
- Community 129
- Community 130
- Community 131
- Community 132
- Community 133
- Community 134
- Community 135
- Community 136
- Community 137
- Community 138
- Community 139
- Community 140
- Community 141
- Community 142
- Community 143
- Community 144
- Community 145
- Community 146
- Community 147
- Community 148
- Community 149
- Community 150
- Community 151
- Community 152
- Community 153
- Community 154
- Community 155
- Community 156
- Community 157
- Community 158
- Community 159
- Community 160
- Community 161
- Community 162
- Community 163
- Community 164
- Community 165
- Community 166
- Community 167
- Community 168
- Community 169
- Community 170
- Community 171
- Community 172
- Community 173
- Community 174
- Community 175
- Community 176
- Community 177
- Community 178
- Community 179
- Community 180
- Community 181
- Community 182
- Community 183
- Community 184
- Community 185
- Community 186
- Community 187
- Community 188
- Community 189
- Community 190
- Community 191
- Community 192
- Community 193
- Community 194
- Community 195
- Community 196
- Community 197
- Community 198
- Community 199
- Community 200
- Community 201
- Community 202
- Community 203
- Community 204
- Community 205
- Community 206
- Community 207
- Community 208
- Community 209
- Community 210
- Community 211
- Community 212
- Community 213
- Community 214
- Community 215
- Community 216
- Community 217
- Community 218
- Community 219
- Community 220
- Community 221
- Community 222
- Community 223
- Community 224
- Community 225
- Community 226
- Community 227
- Community 228
- Community 229
- Community 230
- Community 231
- Community 232
- Community 233
- Community 234
- Community 235
- Community 236
- Community 237
- Community 238
- Community 239
- Community 240
- Community 241
- Community 242
- Community 243
- Community 244
- Community 245
- Community 246
- Community 247
- Community 248
- Community 249
- Community 250
- Community 251
- Community 252
- Community 253
- Community 254
- Community 255
- Community 256
- Community 257
- Community 258
- Community 259
- Community 260
- Community 261
- Community 262
- Community 263
- Community 264
- Community 265
- Community 266
- Community 267
- Community 268
- Community 269
- Community 270
- Community 271
- Community 272
- Community 273
- Community 274
- Community 275
- Community 276
- Community 277
- Community 278
- Community 279
- Community 280
- Community 281
- Community 282
- Community 283
- Community 284
- Community 285
- Community 286
- Community 287
- Community 288
- Community 289
- Community 290
- Community 291
- Community 292
- Community 293
- Community 294
- Community 295
- Community 296
- Community 297
- Community 298
- Community 299
- Community 300
- Community 301
- Community 302
- Community 303
- Community 304
- Community 305
- Community 306
- Community 307
- Community 308
- Community 309
- Community 310
- Community 311
- Community 312
- Community 313
- Community 314
- Community 315
- Community 316
- Community 317
- Community 318
- Community 319

## God Nodes (most connected - your core abstractions)
1. `Farm360.Application.Common.Interfaces` - 134 edges
2. `Farm360.Domain.Common` - 96 edges
3. `ApplicationDbContext` - 91 edges
4. `ITenantService` - 77 edges
5. `IUnitOfWork` - 76 edges
6. `PageHeaderComponent` - 61 edges
7. `Farm360.Persistence.Context` - 58 edges
8. `IDomainEvent` - 55 edges
9. `WorkingContextService` - 51 edges
10. `HealthService` - 50 edges

## Surprising Connections (you probably didn't know these)
- `CurrentUserService` --implements--> `ICurrentUserService`  [EXTRACTED]
  Farm360.Identity/Services/IdentityServices.cs → Farm360.Application/Common/Interfaces/IApplicationServices.cs
- `TenantService` --implements--> `ITenantService`  [EXTRACTED]
  Farm360.Identity/Services/IdentityServices.cs → Farm360.Application/Common/Interfaces/IApplicationServices.cs
- `ApplicationDbContext` --references--> `ITenantService`  [EXTRACTED]
  Farm360.Persistence/Context/ApplicationDbContext.cs → Farm360.Application/Common/Interfaces/IApplicationServices.cs
- `EfCoreTransaction` --implements--> `ITransaction`  [EXTRACTED]
  Farm360.Persistence/Context/ApplicationDbContext.cs → Farm360.Application/Common/Interfaces/IApplicationServices.cs
- `ApplicationDbContext` --implements--> `IUnitOfWork`  [EXTRACTED]
  Farm360.Persistence/Context/ApplicationDbContext.cs → Farm360.Application/Common/Interfaces/IApplicationServices.cs

## Import Cycles
- None detected.

## Communities (336 total, 38 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.04
Nodes (47): AdjustDialogData, AdjustFeedingEntryDialogComponent, Component, AssignFeedingPlanDialogComponent, Component, CreateIngredientDialogComponent, Component, AnimalFeedingPlan (+39 more)

### Community 1 - "Community 1"
Cohesion: 0.09
Nodes (28): AnalyticsService, FinanceAnalyticsDto, MonthlyRevenueExpenseDto, Injectable, UserProfile, Injectable, WorkingContextService, FEEDING_ROUTES (+20 more)

### Community 2 - "Community 2"
Cohesion: 0.04
Nodes (37): AssignProtocolDialog, Component, MortalityDetailDialog, Component, Inject, Component, Inject, VetVisitDetailDialogComponent (+29 more)

### Community 3 - "Community 3"
Cohesion: 0.07
Nodes (35): CreateItemDialogComponent, Component, StockInDialogComponent, Component, StockOutDialogComponent, Component, INVENTORY_ROUTES, CreateInventoryItemRequest (+27 more)

### Community 4 - "Community 4"
Cohesion: 0.06
Nodes (28): Farm360.Persistence.Configurations.Organizations, LookupItem, Guid, Branch, BranchStatus, BusinessType, Guid, Organization (+20 more)

### Community 5 - "Community 5"
Cohesion: 0.10
Nodes (30): Farm360.Application.Livestock.Commands, Farm360.Api.Endpoints.Livestock, CancellationToken, Guid, IResult, ISender, RouteGroupBuilder, Task (+22 more)

### Community 6 - "Community 6"
Cohesion: 0.04
Nodes (22): RFC-7807, parseApiError(), CreateFormulaDialogComponent, Component, CreateScheduleDialogComponent, Component, LogConsumptionDialogComponent, Component (+14 more)

### Community 7 - "Community 7"
Cohesion: 0.11
Nodes (20): AnimalTag, CancellationToken, DateOnly, DiseaseIncident, Event, Guid, IEnumerable, IReadOnlyList (+12 more)

### Community 8 - "Community 8"
Cohesion: 0.06
Nodes (22): Farm360.Shared.Primitives, CancellationToken, Guid, IReadOnlyList, Task, IGenericRepository, CancellationToken, DbSet (+14 more)

### Community 9 - "Community 9"
Cohesion: 0.06
Nodes (42): DateTime, Guid, IReadOnlyList, List, BaseEntity, IDomainEvent, FarmCreatedEvent, FarmDeletedEvent (+34 more)

### Community 10 - "Community 10"
Cohesion: 0.08
Nodes (32): Farm360.Api.Endpoints.Analytics, Farm360.Application.Analytics.Queries, Guid, IEndpointRouteBuilder, IResult, ISender, Task, AnalyticsEndpoints (+24 more)

### Community 11 - "Community 11"
Cohesion: 0.08
Nodes (23): DateOnly, Guid, VaccinationEvent, Guid, IReadOnlyCollection, List, VaccinationProtocol, VaccinationProtocolStep (+15 more)

### Community 12 - "Community 12"
Cohesion: 0.06
Nodes (24): AssignBatchDialogComponent, AssignBatchDialogData, Component, MatingDialogComponent, MatingDialogData, Component, QuarantineDialogComponent, QuarantineDialogData (+16 more)

### Community 13 - "Community 13"
Cohesion: 0.08
Nodes (26): UploadPhotoDialogData, AddPhotoRequest, AnimalDto, AnimalListParams, AnimalMovementDto, AnimalPhotoDto, BcsRecordDto, BreedingRecordDto (+18 more)

### Community 14 - "Community 14"
Cohesion: 0.09
Nodes (29): Farm360.Application.Organizations.Commands, Farm360.Application.Organizations.Queries, CancellationToken, Task, ActivateOrganizationCommand, ActivateOrganizationCommandHandler, CancellationToken, Guid (+21 more)

### Community 15 - "Community 15"
Cohesion: 0.09
Nodes (27): Farm360.Application.Organizations.Branches.Queries, Farm360.Api.Endpoints.Organizations, Farm360.Application.Organizations.Branches.Commands, IEndpointRouteBuilder, BranchEndpoints, CancellationToken, Task, ActivateBranchCommand (+19 more)

### Community 16 - "Community 16"
Cohesion: 0.08
Nodes (21): CancellationToken, Guid, Task, Breed, CancellationToken, Guid, IReadOnlyList, Items (+13 more)

### Community 17 - "Community 17"
Cohesion: 0.10
Nodes (15): DateOnly, Guid, IReadOnlyCollection, List, Animal, DateOnly, Guid, BodyConditionScore (+7 more)

### Community 18 - "Community 18"
Cohesion: 0.08
Nodes (14): BatchVaccinationDialogComponent, BatchVaccinationDialogData, Component, Inject, AnimalWeightInput, BatchWeightDialogComponent, BatchWeightDialogData, Component (+6 more)

### Community 19 - "Community 19"
Cohesion: 0.08
Nodes (14): MasterDataComponent, Component, SettingsHubComponent, Component, SETTINGS_ROUTES, MasterDataDropdownComponent, Component, Input (+6 more)

### Community 20 - "Community 20"
Cohesion: 0.09
Nodes (25): Farm360.Application.Auth.Queries, Farm360.Api.Endpoints.Auth, RouteGroupBuilder, AuthEndpoints, LogoutRequest, RefreshTokenApiRequest, RouteGroupBuilder, UsersEndpoints (+17 more)

### Community 21 - "Community 21"
Cohesion: 0.09
Nodes (24): Farm360.Application.Farms.Pens.Commands, CancellationToken, Guid, Task, CreatePenCommand, CreatePenCommandHandler, CreatePenCommandValidator, CancellationToken (+16 more)

### Community 22 - "Community 22"
Cohesion: 0.12
Nodes (29): CancellationToken, Task, DeleteAnimalCommand, DeleteAnimalCommandHandler, QuarantineAnimalCommand, QuarantineAnimalCommandHandler, QuarantineAnimalCommandValidator, RecordAnimalDeathCommand (+21 more)

### Community 23 - "Community 23"
Cohesion: 0.10
Nodes (19): Farm360.Domain.Health.Events, Farm360.Domain.Health, Farm360.Persistence.Queries, Farm360.Persistence.Permissions, Farm360.Persistence.Context, Farm360.Persistence.Repositories.Tenancy, Farm360.Persistence.Repositories, Farm360.Domain.Tenancy.Repositories (+11 more)

### Community 24 - "Community 24"
Cohesion: 0.12
Nodes (20): Farm360.Domain.Dashboard.Interfaces, Farm360.Api.Endpoints.Dashboard, Farm360.Application.Dashboard.DTOs, Farm360.Application.Dashboard.Queries, Guid, IEndpointRouteBuilder, IResult, ISender (+12 more)

### Community 25 - "Community 25"
Cohesion: 0.10
Nodes (27): Farm360.Application.Feeding.Queries.FeedIngredients, Farm360.Application.Feeding.Mappings, Farm360.Application.Feeding.DTOs, ConsumptionDetailDto, FeedConsumptionLogDto, FeedIngredientDto, FeedingScheduleDto, FormulaIngredientDto (+19 more)

### Community 26 - "Community 26"
Cohesion: 0.13
Nodes (13): Guid, FeedIngredient, EntityTypeBuilder, FeedIngredientConfiguration, CancellationToken, DateOnly, Guid, IReadOnlyList (+5 more)

### Community 27 - "Community 27"
Cohesion: 0.14
Nodes (16): CancellationToken, Task, UpdateFeedFormulaCommand, UpdateFeedFormulaCommandHandler, UpdateFeedFormulaCommandValidator, CancellationToken, DateOnly, FeedConsumptionLog (+8 more)

### Community 28 - "Community 28"
Cohesion: 0.14
Nodes (20): ILogger, FeedConsumptionLoggedEventHandler, CancellationToken, DateOnly, Guid, InventoryItem, IReadOnlyList, ItemName (+12 more)

### Community 29 - "Community 29"
Cohesion: 0.10
Nodes (22): Farm360.Application.Farms.DTOs, Farm360.Application.Farms.Commands, Farm360.Application.Farms.Queries, IEndpointRouteBuilder, FarmEndpoints, FarmDto, FarmListDto, FarmMappingExtensions (+14 more)

### Community 30 - "Community 30"
Cohesion: 0.13
Nodes (13): CancellationToken, Func, Stream, Task, TimeSpan, IBlobStorageService, ICacheService, IEmailService (+5 more)

### Community 31 - "Community 31"
Cohesion: 0.12
Nodes (15): BreedDetailDialogComponent, Component, BreedReferenceDialogComponent, Component, BreedSetupDialogComponent, Component, AcquisitionType, SPECIES_LABELS (+7 more)

### Community 32 - "Community 32"
Cohesion: 0.11
Nodes (11): LocationSelectorComponent, Component, Input, Country, District, Division, Union, Upazila (+3 more)

### Community 33 - "Community 33"
Cohesion: 0.06
Nodes (33): @angular/cli, @angular/compiler-cli, @angular-devkit/build-angular, autoprefixer, devDependencies, @angular/cli, @angular/compiler-cli, @angular-devkit/build-angular (+25 more)

### Community 34 - "Community 34"
Cohesion: 0.14
Nodes (14): Farm360.Application.Inventory.Commands.StockTransactions, Farm360.Domain.Inventory.Exceptions, Farm360.Domain.Inventory.Enums, Farm360.Application.Inventory.Commands.InventoryItems, Farm360.Persistence.Repositories.Inventory, Farm360.Application.Health.EventHandlers, Farm360.Application.Inventory.EventHandlers, Farm360.Domain.Inventory.Interfaces.Repositories (+6 more)

### Community 35 - "Community 35"
Cohesion: 0.11
Nodes (21): Farm360.Application.Livestock, Farm360.Application.Livestock.DTOs, Farm360.Application.Livestock.Queries, BreedMappings, BatchDto, PagedBatchListDto, BreedDto, CancellationToken (+13 more)

### Community 36 - "Community 36"
Cohesion: 0.10
Nodes (18): Farm360.Domain.MasterData.Locations, IReadOnlyList, IAggregateRoot, Country, Guid, District, Guid, Division (+10 more)

### Community 37 - "Community 37"
Cohesion: 0.09
Nodes (17): DbContext, EntityTrackedEventArgs, CancellationToken, DbSet, Func, Guid, ModelBuilder, Task (+9 more)

### Community 38 - "Community 38"
Cohesion: 0.10
Nodes (19): Farm360.Domain.Organizations.Repositories, Farm360.Application.Organizations.DTOs, Farm360.Persistence.Repositories.Organizations, Farm360.Domain.Organizations, CancellationToken, Task, DeactivateOrganizationCommand, DeactivateOrganizationCommandHandler (+11 more)

### Community 39 - "Community 39"
Cohesion: 0.12
Nodes (13): FarmDashboardComponent, Component, FarmCardComponent, Component, Input, FarmListComponent, Component, CreateFarmCommand (+5 more)

### Community 40 - "Community 40"
Cohesion: 0.06
Nodes (31): @angular/animations, @angular/cdk, @angular/common, @angular/compiler, @angular/core, @angular/forms, @angular/material, @angular/platform-browser (+23 more)

### Community 41 - "Community 41"
Cohesion: 0.12
Nodes (18): DewormingFrequency, TreatmentStatus, VaccinationStatus, DateOnly, Guid, MedicalTreatment, WithdrawalPeriod, EntityTypeBuilder (+10 more)

### Community 42 - "Community 42"
Cohesion: 0.08
Nodes (18): Farm360.Identity.Seed, Farm360.Identity.DependencyInjection, Farm360.Identity.Context, Farm360.Identity.Entities, Farm360.Identity.Services, Farm360.Identity.Configuration, string, JwtConfiguration (+10 more)

### Community 43 - "Community 43"
Cohesion: 0.10
Nodes (20): Farm360.Application.Common.Behaviors, Farm360.Application.DependencyInjection, Farm360.Application.Inventory.Commands.PurchaseOrders, Farm360.Application.Common.Exceptions, CancellationToken, RequestHandlerDelegate, Task, ITransactionalCommand (+12 more)

### Community 44 - "Community 44"
Cohesion: 0.13
Nodes (12): Farm360.Persistence.Configurations.Farms, FarmStatus, FarmType, Guid, Farm, EntityTypeBuilder, FarmConfiguration, CancellationToken (+4 more)

### Community 45 - "Community 45"
Cohesion: 0.13
Nodes (18): CancellationToken, Guid, Task, CreateShedCommand, CreateShedCommandHandler, CreateShedCommandValidator, CancellationToken, Task (+10 more)

### Community 46 - "Community 46"
Cohesion: 0.07
Nodes (29): angularCompilerOptions, enableI18nLegacyMessageIdFormat, strictInjectionParameters, strictInputAccessModifiers, strictTemplates, compileOnSave, compilerOptions, allowSyntheticDefaultImports (+21 more)

### Community 47 - "Community 47"
Cohesion: 0.11
Nodes (14): Farm360.Domain.Livestock.Repositories, Farm360.Persistence.Configurations.Livestock, Farm360.Persistence.Repositories.Livestock, Farm360.Domain.Livestock.Enums, Farm360.Domain.Livestock, DateTime, Guid, AnimalMovement (+6 more)

### Community 48 - "Community 48"
Cohesion: 0.14
Nodes (11): Farm360.Domain.Feeding, Farm360.Domain.Organizations.Events, Farm360.Domain.Common, Farm360.Domain.Feeding.Events, Farm360.Domain.Feeding.ValueObjects, Farm360.Domain.Feeding.Enums, Farm360.Domain.Feeding.Exceptions, FeedingDomainException (+3 more)

### Community 49 - "Community 49"
Cohesion: 0.08
Nodes (22): Farm360.Api.Endpoints.Health, Farm360.Application.Health.Queries.AnimalHealth, Farm360.Application.Health.Queries.VetVisits, Farm360.Application.Health.Queries.Dashboard, Farm360.Api.Endpoints.Tenants, Farm360.Api.Endpoints.Feeding, Farm360.Application.Health.Queries.MortalityRecords, Farm360.Persistence.DependencyInjection (+14 more)

### Community 50 - "Community 50"
Cohesion: 0.18
Nodes (9): CancellationToken, DateOnly, Guid, IReadOnlyList, Task, AnimalFeedingPlanRepository, DailyFeedingEntryRepository, FeedingReconciliationRepository (+1 more)

### Community 51 - "Community 51"
Cohesion: 0.11
Nodes (10): CreatePenCommand, Pen, PenList, UpdatePenCommand, PenDetailComponent, Component, PenListComponent, Component (+2 more)

### Community 52 - "Community 52"
Cohesion: 0.11
Nodes (18): Farm360.Application.Farms.Sheds.Commands, Farm360.Application.Farms.Sheds.DTOs, Farm360.Application.Farms.Sheds.Queries, Farm360.Api.Endpoints.Farms, IEndpointRouteBuilder, ShedEndpoints, ShedDto, ShedListDto (+10 more)

### Community 53 - "Community 53"
Cohesion: 0.12
Nodes (19): Farm360.Application.Health.Commands.VetVisits, CancellationToken, Guid, Task, CreateVetVisitCommand, CreateVetVisitCommandHandler, CreateVetVisitCommandValidator, CancellationToken (+11 more)

### Community 54 - "Community 54"
Cohesion: 0.12
Nodes (18): Farm360.Application.Inventory.Commands.Suppliers, CancellationToken, Guid, Task, CreateSupplierCommand, CreateSupplierCommandHandler, CreateSupplierCommandValidator, CancellationToken (+10 more)

### Community 55 - "Community 55"
Cohesion: 0.11
Nodes (21): Farm360.Application.Feeding.Commands.FeedingRuleSets, CancellationToken, Guid, IReadOnlyList, Task, CreateFeedingRuleSetCommand, CreateFeedingRuleSetCommandHandler, CreateFeedingRuleSetCommandValidator (+13 more)

### Community 56 - "Community 56"
Cohesion: 0.10
Nodes (17): Guid, IEnumerable, IReadOnlyList, IAuditLogService, ICurrentUserService, INotificationService, IPermissionService, ITenantMembershipService (+9 more)

### Community 57 - "Community 57"
Cohesion: 0.20
Nodes (14): ITenantService, CancellationToken, DateOnly, Guid, IReadOnlyList, ItemName, Items, SupplierName (+6 more)

### Community 58 - "Community 58"
Cohesion: 0.16
Nodes (15): CancellationToken, Task, DeleteFarmCommand, DeleteFarmCommandHandler, CancellationToken, Task, UpdateFarmCommand, UpdateFarmCommandHandler (+7 more)

### Community 59 - "Community 59"
Cohesion: 0.13
Nodes (17): DateOnly, Guid, IEnumerable, IReadOnlyCollection, List, DiseaseIncident, IncidentSeverity, IncidentStatus (+9 more)

### Community 60 - "Community 60"
Cohesion: 0.12
Nodes (11): BranchWidgetComponent, Component, Input, BranchDetailComponent, Component, Branch, CreateBranchCommand, UpdateBranchCommand (+3 more)

### Community 61 - "Community 61"
Cohesion: 0.20
Nodes (12): Farm360.Domain.Interfaces.Repositories, AnimalFeedingPlan, CancellationToken, DailyFeedingEntry, DateOnly, FeedingCycleReconciliation, Guid, IReadOnlyList (+4 more)

### Community 62 - "Community 62"
Cohesion: 0.07
Nodes (20): CancellationToken, RequestHandlerDelegate, Task, TimeSpan, CachingBehavior, ICacheableQuery, CancellationToken, RequestHandlerDelegate (+12 more)

### Community 63 - "Community 63"
Cohesion: 0.10
Nodes (10): Farm360.Persistence.Migrations.ApplicationDb, MigrationBuilder, ModelBuilder, AddHealthModule, MigrationBuilder, ModelBuilder, AddIsDewormingToVaccinationProtocol, MigrationBuilder (+2 more)

### Community 64 - "Community 64"
Cohesion: 0.12
Nodes (16): Farm360.Persistence.Configurations.Feeding, DailyFeedingEntryStatus, FeedCategory, FeedingPlanStatus, FeedingPlanType, FeedingPurpose, ReconciliationStatus, TargetAnimalType (+8 more)

### Community 65 - "Community 65"
Cohesion: 0.13
Nodes (11): PenMappingExtensions, PenStatus, Guid, Pen, EntityTypeBuilder, PenConfiguration, CancellationToken, Guid (+3 more)

### Community 66 - "Community 66"
Cohesion: 0.10
Nodes (10): Farm360.Persistence.Migrations, MigrationBuilder, ModelBuilder, Livestock_AddAnimalAggregate, MigrationBuilder, ModelBuilder, AddLivestockPenAndSaleFields, MigrationBuilder (+2 more)

### Community 67 - "Community 67"
Cohesion: 0.10
Nodes (15): Farm360.Domain.Livestock.Exceptions, Farm360.Domain.Livestock.ValueObjects, AnimalQuarantinedException, InvalidAnimalStateTransitionException, InvalidSaleDateException, InvalidWeightDateException, LivestockDomainException, IEnumerable (+7 more)

### Community 68 - "Community 68"
Cohesion: 0.10
Nodes (10): Farm360.Persistence.Migrations.Application, MigrationBuilder, ModelBuilder, InitialTenancy, MigrationBuilder, ModelBuilder, AddAnimalFeedingPlanModule, MigrationBuilder (+2 more)

### Community 69 - "Community 69"
Cohesion: 0.16
Nodes (18): CancellationToken, Task, Unit, ApprovePurchaseOrderCommand, ApprovePurchaseOrderCommandHandler, CancellationToken, Task, Unit (+10 more)

### Community 70 - "Community 70"
Cohesion: 0.13
Nodes (18): CancellationToken, Task, AssignAnimalsToBatchCommand, AssignAnimalsToBatchCommandHandler, CancellationToken, Guid, Task, CreateBatchCommand (+10 more)

### Community 71 - "Community 71"
Cohesion: 0.08
Nodes (24): AWSSDK.Extensions.NETCore.Setup, Microsoft.Extensions.Caching.StackExchangeRedis, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options, Serilog.AspNetCore, StackExchange.Redis, AWSSDK.S3 (+16 more)

### Community 72 - "Community 72"
Cohesion: 0.12
Nodes (3): AnimalSpecies, AnimalDetailComponent, Component

### Community 73 - "Community 73"
Cohesion: 0.10
Nodes (12): Farm360.Domain.Intelligence.ValueObjects, IEnumerable, BaseValueObject, IEnumerable, AnimalPerformanceScore, IEnumerable, CostProjection, IEnumerable (+4 more)

### Community 74 - "Community 74"
Cohesion: 0.13
Nodes (15): Farm360.Persistence.Configurations.Health, VetVisitType, DateOnly, DateTime, Guid, VetVisit, EntityTypeBuilder, VetVisitConfiguration (+7 more)

### Community 75 - "Community 75"
Cohesion: 0.12
Nodes (15): Farm360.Application.Intelligence.Interfaces, Farm360.Infrastructure.BackgroundServices.Intelligence, Farm360.Application.Intelligence.Services, CancellationToken, Guid, Task, CostAndProfitSnapshot, ICostAndProfitEngine (+7 more)

### Community 76 - "Community 76"
Cohesion: 0.14
Nodes (17): Farm360.Application.Inventory.Mappings, Farm360.Application.Inventory.DTOs, CurrentStockSummaryDto, InventoryValuationReportDto, StockTransactionDto, CancellationToken, Task, GetCurrentStockSummaryQuery (+9 more)

### Community 77 - "Community 77"
Cohesion: 0.14
Nodes (10): ShedStatus, Guid, Shed, EntityTypeBuilder, ShedConfiguration, CancellationToken, Guid, IReadOnlyList (+2 more)

### Community 78 - "Community 78"
Cohesion: 0.14
Nodes (12): DateOnly, Guid, IReadOnlyCollection, List, PurchaseOrder, CancellationToken, Guid, IReadOnlyList (+4 more)

### Community 79 - "Community 79"
Cohesion: 0.12
Nodes (14): Guid, IReadOnlyCollection, List, AnimalBatch, BatchStatus, EntityTypeBuilder, AnimalBatchConfiguration, CancellationToken (+6 more)

### Community 80 - "Community 80"
Cohesion: 0.13
Nodes (11): DASHBOARD_ROUTES, ActionableInsight, ExecutiveDashboardData, InsightSeverity, InsightType, ExecutiveDashboardComponent, Component, DashboardService (+3 more)

### Community 81 - "Community 81"
Cohesion: 0.11
Nodes (17): BackgroundService, Channel, CancellationToken, IAsyncEnumerable, INotification, ValueTask, IIntelligenceEventChannel, CancellationToken (+9 more)

### Community 82 - "Community 82"
Cohesion: 0.12
Nodes (14): Farm360.Domain.Health.ValueObjects, Farm360.Domain.Health.Exceptions, CancellationToken, Guid, Task, LogMedicalTreatmentCommand, LogMedicalTreatmentCommandHandler, LogMedicalTreatmentCommandValidator (+6 more)

### Community 83 - "Community 83"
Cohesion: 0.14
Nodes (14): Farm360.Domain.Organizations.ValueObjects, Farm360.Domain.Organizations.Enums, CancellationToken, Task, UpdateBranchCommand, UpdateBranchCommandHandler, UpdateBranchCommandValidator, CancellationToken (+6 more)

### Community 84 - "Community 84"
Cohesion: 0.11
Nodes (13): Farm360.Domain.Identity, Guid, IReadOnlyCollection, List, Permission, Guid, RolePermission, Guid (+5 more)

### Community 85 - "Community 85"
Cohesion: 0.13
Nodes (17): Farm360.Application.Health.Commands.VaccinationEvents, CancellationToken, Task, BatchRecordVaccinationCommand, BatchRecordVaccinationCommandHandler, BatchRecordVaccinationCommandValidator, CancellationToken, Task (+9 more)

### Community 86 - "Community 86"
Cohesion: 0.28
Nodes (19): Farm360.Application.MasterData.DTOs, CountryDto, DistrictDto, DivisionDto, LocationMappingExtensions, UnionDto, UpazilaDto, VillageDto (+11 more)

### Community 87 - "Community 87"
Cohesion: 0.12
Nodes (16): Farm360.Application.Feeding.Commands.DailyFeedingEntries, CancellationToken, ILogger, Task, AdjustDailyFeedingEntryCommand, AdjustDailyFeedingEntryCommandHandler, CancellationToken, Task (+8 more)

### Community 88 - "Community 88"
Cohesion: 0.12
Nodes (15): AuthorizationHandler, AuthorizationHandlerContext, AuthorizationOptions, AuthorizationPolicy, AuthorizeAttribute, Farm360.Api.Authorization, Task, PermissionHandler (+7 more)

### Community 89 - "Community 89"
Cohesion: 0.12
Nodes (16): Farm360.Domain.Exceptions, Farm360.Persistence.Interceptors, DateTimeOffset, Exception, AccountLockedException, ConflictException, ForbiddenAccessException, NotFoundException (+8 more)

### Community 90 - "Community 90"
Cohesion: 0.16
Nodes (11): Farm360.Persistence.Repositories.Farms, Farm360.Domain.Farms.Enums, Farm360.Domain.Farms.Repositories, Farm360.Domain.Farms, Farm360.Domain.Farms.Events, CancellationToken, Guid, Task (+3 more)

### Community 91 - "Community 91"
Cohesion: 0.15
Nodes (13): Farm360.Persistence.Configurations.Intelligence, Guid, ActionableInsight, InsightSeverity, InsightType, EntityTypeBuilder, ActionableInsightConfiguration, CancellationToken (+5 more)

### Community 92 - "Community 92"
Cohesion: 0.14
Nodes (15): IEndpointRouteBuilder, OrganizationEndpoints, CancellationToken, Stream, Task, IFileStorageService, AddAnimalPhotoCommand, AddAnimalPhotoCommandHandler (+7 more)

### Community 93 - "Community 93"
Cohesion: 0.17
Nodes (16): LookupDto, CancellationToken, IReadOnlyList, Task, GetFarmLookupQuery, GetFarmLookupQueryHandler, CancellationToken, IReadOnlyList (+8 more)

### Community 94 - "Community 94"
Cohesion: 0.17
Nodes (10): OrganizationType, SubscriptionTier, TenantStatus, DateTime, Guid, IReadOnlyCollection, List, Tenant (+2 more)

### Community 95 - "Community 95"
Cohesion: 0.16
Nodes (21): Code, Description, IReadOnlyList, string, Animals, Billing, FarmModule, FeedingModule (+13 more)

### Community 96 - "Community 96"
Cohesion: 0.16
Nodes (14): Farm360.Application.Feeding.Commands.FeedingSchedules, CancellationToken, Guid, Task, CreateFeedingScheduleCommand, CreateFeedingScheduleCommandHandler, CreateFeedingScheduleCommandValidator, CancellationToken (+6 more)

### Community 97 - "Community 97"
Cohesion: 0.11
Nodes (9): ContextSelectorComponent, Component, HeaderComponent, Component, Output, MenuItem, SidebarComponent, Component (+1 more)

### Community 98 - "Community 98"
Cohesion: 0.21
Nodes (9): ShedDashboardComponent, Component, CreateShedCommand, Shed, ShedList, UpdateShedCommand, ShedService, Injectable (+1 more)

### Community 99 - "Community 99"
Cohesion: 0.14
Nodes (3): AnimalStatus, AnimalListComponent, Component

### Community 100 - "Community 100"
Cohesion: 0.16
Nodes (11): FormulaStatus, Guid, IReadOnlyCollection, List, FeedFormula, FormulaIngredient, IEnumerable, NutritionalProfile (+3 more)

### Community 101 - "Community 101"
Cohesion: 0.14
Nodes (9): DateTime, Guid, TenantUser, EntityTypeBuilder, TenantUserConfiguration, CancellationToken, Guid, Task (+1 more)

### Community 102 - "Community 102"
Cohesion: 0.13
Nodes (5): BranchListComponent, Component, BranchList, OrganizationDetailComponent, Component

### Community 103 - "Community 103"
Cohesion: 0.22
Nodes (8): IRefreshTokenService, RefreshTokenResult, SessionRevokeReason, CancellationToken, Guid, int, Task, RefreshTokenService

### Community 104 - "Community 104"
Cohesion: 0.19
Nodes (12): CancellationToken, Task, ActionableInsightDto, AnimalIntelligenceDataResponse, GetAnimalIntelligenceDataQuery, GetAnimalIntelligenceDataQueryHandler, GrowthCurveDto, CancellationToken (+4 more)

### Community 105 - "Community 105"
Cohesion: 0.15
Nodes (13): CauseOfDeath, DateOnly, Guid, MortalityRecord, EntityTypeBuilder, MortalityRecordConfiguration, CancellationToken, Guid (+5 more)

### Community 106 - "Community 106"
Cohesion: 0.16
Nodes (15): AbstractValidator, CancellationToken, Guid, Task, CreateInventoryItemCommand, CreateInventoryItemCommandHandler, CreateInventoryItemCommandValidator, CancellationToken (+7 more)

### Community 107 - "Community 107"
Cohesion: 0.14
Nodes (14): Farm360.Application.Feeding.Queries.AnimalFeedingPlans, Farm360.Application.Feeding.Queries.FeedingSchedules, Farm360.Application.Feeding.Queries.Analytics, Farm360.Application.Feeding.Queries.FeedingRuleSets, Farm360.Application.Feeding.Queries.FeedingReconciliations, Farm360.Application.Feeding.Queries.ConsumptionLogs, Farm360.Application.Feeding.Queries.DailyFeedingEntries, IEndpointRouteBuilder (+6 more)

### Community 108 - "Community 108"
Cohesion: 0.18
Nodes (11): Farm360.Domain.MasterData, Farm360.Domain.MasterData.Enums, Farm360.Persistence.Repositories.MasterData, Farm360.Domain.MasterData.Repositories, Farm360.Domain.MasterData.Events, CancellationToken, Guid, Task (+3 more)

### Community 109 - "Community 109"
Cohesion: 0.18
Nodes (12): Farm360.Application.Health.Mappings, MortalityRecordDto, VetVisitDto, HealthMappingExtensions, CancellationToken, Task, GetMortalityRecordsQuery, GetMortalityRecordsQueryHandler (+4 more)

### Community 110 - "Community 110"
Cohesion: 0.14
Nodes (12): CancellationToken, Task, CancellationToken, Task, CancellationToken, Guid, Task, RecordStockInCommand (+4 more)

### Community 111 - "Community 111"
Cohesion: 0.21
Nodes (13): DeleteInventoryItemCommand, DeleteInventoryItemCommandHandler, CancellationToken, Task, CreateBreedCommand, CreateBreedCommandHandler, CreateBreedCommandValidator, DeleteBreedCommand (+5 more)

### Community 112 - "Community 112"
Cohesion: 0.22
Nodes (10): AnimalStatus, CancellationToken, DbSet, Guid, IEnumerable, IReadOnlyList, Items, Task (+2 more)

### Community 113 - "Community 113"
Cohesion: 0.12
Nodes (18): build, serve, builder, configurations, defaultConfiguration, development, production, buildTarget (+10 more)

### Community 114 - "Community 114"
Cohesion: 0.15
Nodes (6): IncidentSeverity, IncidentStatus, IncidentDetailComponent, Component, IncidentListComponent, Component

### Community 115 - "Community 115"
Cohesion: 0.13
Nodes (4): AnimalSex, AnimalPickerComponent, Component, Input

### Community 116 - "Community 116"
Cohesion: 0.12
Nodes (16): compilerOptions, outDir, types, extends, files, include, src/**/*.d.ts, ./tsconfig.json (+8 more)

### Community 117 - "Community 117"
Cohesion: 0.21
Nodes (13): Farm360.Application.Health.Queries.SpecializedReports, MilkWithdrawalDto, CancellationToken, Task, AnimalHealthReportDto, GetAnimalHealthReportQuery, GetAnimalHealthReportQueryHandler, GetAnimalHealthReportQueryValidator (+5 more)

### Community 118 - "Community 118"
Cohesion: 0.18
Nodes (13): Farm360.Application.Health.Commands.VaccinationProtocols, CancellationToken, Task, AssignProtocolToAnimalsCommand, AssignProtocolToAnimalsCommandHandler, AssignProtocolToAnimalsCommandValidator, CancellationToken, Guid (+5 more)

### Community 119 - "Community 119"
Cohesion: 0.12
Nodes (13): CancellationToken, ILogger, Task, AnimalDiedEventHandler, CancellationToken, ILogger, Task, AnimalTransferredEventHandler (+5 more)

### Community 120 - "Community 120"
Cohesion: 0.20
Nodes (11): MasterDataDto, MasterDataMappingExtensions, CancellationToken, Task, GetMasterDataByIdQuery, GetMasterDataByIdQueryHandler, CancellationToken, IReadOnlyList (+3 more)

### Community 121 - "Community 121"
Cohesion: 0.18
Nodes (9): DateOnly, Guid, IReadOnlyCollection, List, AnimalFeedingPlan, FeedingPlanExclusion, EntityTypeBuilder, AnimalFeedingPlanConfiguration (+1 more)

### Community 122 - "Community 122"
Cohesion: 0.18
Nodes (10): DateOnly, DateTime, Guid, IReadOnlyCollection, List, FeedingCycleReconciliation, FeedingCycleReconciliationLine, EntityTypeBuilder (+2 more)

### Community 123 - "Community 123"
Cohesion: 0.27
Nodes (9): Animal, CancellationToken, Guid, IEnumerable, IReadOnlyList, Items, Task, TotalCount (+1 more)

### Community 124 - "Community 124"
Cohesion: 0.25
Nodes (8): MasterDataType, Guid, MasterDataEntry, CancellationToken, Guid, IReadOnlyList, Task, MasterDataRepository

### Community 125 - "Community 125"
Cohesion: 0.24
Nodes (15): DateTime, DbSet, Guid, ModelBuilder, AuthAuditLog, ExternalProvider, IdentityDbContext, OtpVerification (+7 more)

### Community 126 - "Community 126"
Cohesion: 0.15
Nodes (17): options, assets, browser, index, inlineStyleLanguage, outputPath, polyfills, scripts (+9 more)

### Community 127 - "Community 127"
Cohesion: 0.15
Nodes (11): AppComponent, Component, appConfig, routes, authGuard(), addTokenHeader(), authInterceptor(), refreshTokenSubject (+3 more)

### Community 130 - "Community 130"
Cohesion: 0.14
Nodes (8): IntelligencePanelComponent, Component, SaleSimulationResult, Component, WhatIfSimulatorComponent, AnimalIntelligenceDialogComponent, AnimalIntelligenceDialogData, Component

### Community 131 - "Community 131"
Cohesion: 0.18
Nodes (12): Farm360.Application.Feeding.Commands.AnimalFeedingPlans, CancellationToken, Guid, Task, AssignAnimalFeedingPlanCommand, AssignAnimalFeedingPlanCommandHandler, AssignAnimalFeedingPlanCommandValidator, CancellationToken (+4 more)

### Community 132 - "Community 132"
Cohesion: 0.15
Nodes (9): Farm360.Persistence.Repositories.Intelligence, Farm360.Domain.Intelligence, Farm360.Persistence.Repositories.Dashboard, Farm360.Application.Intelligence.EventHandlers, Farm360.Domain.Intelligence.Enums, Farm360.Domain.Intelligence.Interfaces.Repositories, ActionableInsightDto, IRuleEngine (+1 more)

### Community 133 - "Community 133"
Cohesion: 0.18
Nodes (12): Farm360.Application.Farms.Pens.DTOs, Farm360.Application.Farms.Pens.Queries, IEndpointRouteBuilder, CreatePenRequest, PenEndpoints, UpdatePenRequest, PenListDto, CancellationToken (+4 more)

### Community 134 - "Community 134"
Cohesion: 0.16
Nodes (12): Farm360.Application.Inventory.Queries.Reports, Farm360.Application.Inventory.Queries.InventoryItems, Farm360.Api.Endpoints.Inventory, Farm360.Application.Inventory.Queries.Suppliers, Farm360.Application.Inventory.Queries.StockTransactions, IEndpointRouteBuilder, InventoryEndpoints, SupplierDto (+4 more)

### Community 135 - "Community 135"
Cohesion: 0.15
Nodes (10): Farm360.Persistence.Configurations, Farm360.Domain.Tenancy, TenantUserStatus, DateTime, Guid, AuditLog, EntityTypeBuilder, AuditLogConfiguration (+2 more)

### Community 136 - "Community 136"
Cohesion: 0.19
Nodes (12): Farm360.Application.Health.Commands.DiseaseIncidents, CancellationToken, Guid, Task, ReportDiseaseIncidentCommand, ReportDiseaseIncidentCommandHandler, ReportDiseaseIncidentCommandValidator, CancellationToken (+4 more)

### Community 137 - "Community 137"
Cohesion: 0.21
Nodes (11): Farm360.Application.Inventory.Queries.PurchaseOrders, CancellationToken, Task, GetPurchaseOrderByIdQuery, GetPurchaseOrderByIdQueryHandler, CancellationToken, Task, GetPurchaseOrdersQuery (+3 more)

### Community 138 - "Community 138"
Cohesion: 0.25
Nodes (8): CancellationToken, Task, CancellationToken, Guid, IReadOnlyList, MasterDataEntry, Task, IMasterDataRepository

### Community 139 - "Community 139"
Cohesion: 0.28
Nodes (8): CancellationToken, Dictionary, Guid, List, Task, DataSeeder, permId, roleId

### Community 140 - "Community 140"
Cohesion: 0.19
Nodes (3): LookupService, Injectable, LookupDto

### Community 141 - "Community 141"
Cohesion: 0.22
Nodes (5): CreateOrganizationCommand, UpdateOrganizationCommand, OrganizationService, Injectable, PagedResult

### Community 142 - "Community 142"
Cohesion: 0.17
Nodes (11): Farm360.Application.Feeding.Commands.FeedingReconciliations, CancellationToken, ILogger, Task, ApproveFeedingReconciliationCommand, ApproveFeedingReconciliationCommandHandler, CancellationToken, ILogger (+3 more)

### Community 143 - "Community 143"
Cohesion: 0.17
Nodes (11): Farm360.Application.Feeding.Jobs, CancellationToken, ILogger, Task, CloseFeedingCycleCommand, CloseFeedingCycleCommandHandler, CancellationToken, ILogger (+3 more)

### Community 144 - "Community 144"
Cohesion: 0.24
Nodes (10): Farm360.Api.Converters, DateOnly, Guid, JsonSerializerOptions, NullableDateOnlyJsonConverter, NullableGuidJsonConverter, JsonConverter, Type (+2 more)

### Community 145 - "Community 145"
Cohesion: 0.33
Nodes (6): OtpPurpose, CancellationToken, int, Task, TimeSpan, OtpService

### Community 146 - "Community 146"
Cohesion: 0.24
Nodes (12): IReadOnlyList, PagedResult, DiseaseIncidentDto, CancellationToken, Task, GetDiseaseIncidentListQuery, GetDiseaseIncidentListQueryHandler, BranchListDto (+4 more)

### Community 147 - "Community 147"
Cohesion: 0.21
Nodes (10): AnimalMappings, CancellationToken, Task, RecordBcsCommand, RecordBcsCommandHandler, RecordBcsCommandValidator, AnimalListItemDto, AnimalMovementDto (+2 more)

### Community 148 - "Community 148"
Cohesion: 0.13
Nodes (14): AWSSDK.Extensions.NETCore.Setup, Microsoft.EntityFrameworkCore.Design, Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Tools, Microsoft.Extensions.Caching.StackExchangeRedis, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options (+6 more)

### Community 149 - "Community 149"
Cohesion: 0.39
Nodes (5): CancellationToken, Guid, IReadOnlyList, Task, ExecutiveDashboardRepository

### Community 150 - "Community 150"
Cohesion: 0.19
Nodes (6): IntelligenceSignalRService, Injectable, ActionableInsight, AnimalFinancialSnapshot, AnimalIntelligenceData, GrowthCurve

### Community 151 - "Community 151"
Cohesion: 0.16
Nodes (5): PurchaseOrderDetail, Component, PurchaseOrderList, Component, PurchaseOrderStatus

### Community 152 - "Community 152"
Cohesion: 0.16
Nodes (12): ClaimsPrincipal, DateOnly, DateTime, IDateTimeService, DateOnly, DateTime, Guid, IReadOnlyList (+4 more)

### Community 153 - "Community 153"
Cohesion: 0.23
Nodes (8): Farm360.Domain.Feeding.Interfaces.Repositories, Farm360.Domain.Livestock.Events, Farm360.Application.Feeding.EventHandlers, CancellationToken, ILogger, Task, AnimalSoldEventHandler, AnimalSoldEvent

### Community 154 - "Community 154"
Cohesion: 0.19
Nodes (9): Farm360.Application.Finance.EventHandlers.Integration, Farm360.Persistence.Repositories.Finance, Farm360.Application.Finance.Repositories, Farm360.Domain.Finance, Farm360.Persistence.Configurations.Finance, CancellationToken, Task, AnimalSoldEventHandler (+1 more)

### Community 155 - "Community 155"
Cohesion: 0.16
Nodes (8): Farm360.Application.MasterData.Queries, Farm360.Api.Endpoints.MasterData, Farm360.Persistence.Seed, Farm360.Application.MasterData.Commands, IEndpointRouteBuilder, LocationEndpoints, IEndpointRouteBuilder, MasterDataEndpoints

### Community 156 - "Community 156"
Cohesion: 0.21
Nodes (9): Farm360.Application.Finance.Queries, Farm360.Contracts.Finance, Farm360.Application.Finance.Commands, Farm360.Api.Endpoints.Finance, CancellationToken, Task, GetFinancialTransactionSummaryQuery, GetFinancialTransactionSummaryQueryHandler (+1 more)

### Community 157 - "Community 157"
Cohesion: 0.15
Nodes (5): Farm360.SharedKernel.Guards, Guid, IEnumerable, Guard, GuardClause

### Community 158 - "Community 158"
Cohesion: 0.19
Nodes (9): Farm360.Application.Common.Models, Farm360.Application.Health.Queries.MedicalTreatments, int, PaginationFilter, MedicalTreatmentDto, CancellationToken, Task, GetTreatmentListQuery (+1 more)

### Community 159 - "Community 159"
Cohesion: 0.24
Nodes (10): Farm360.Application.Health.Queries.VaccinationProtocols, VaccinationProtocolDto, CancellationToken, Task, GetVaccinationProtocolDetailQuery, GetVaccinationProtocolDetailQueryHandler, CancellationToken, Task (+2 more)

### Community 160 - "Community 160"
Cohesion: 0.25
Nodes (10): CancellationToken, Task, CreateFinancialTransactionCommand, CreateFinancialTransactionCommandHandler, CancellationToken, IReadOnlyList, Task, GetFinancialTransactionsQuery (+2 more)

### Community 161 - "Community 161"
Cohesion: 0.14
Nodes (11): CancellationToken, Guid, Task, CancellationToken, Guid, List, Task, CancellationToken (+3 more)

### Community 162 - "Community 162"
Cohesion: 0.22
Nodes (10): CancellationToken, DateOnly, Guid, Task, ISimulationEngine, SaleSimulationResult, CancellationToken, Task (+2 more)

### Community 163 - "Community 163"
Cohesion: 0.30
Nodes (10): PagedAnimalListDto, CancellationToken, IReadOnlyList, Task, GetAnimalByIdQuery, GetAnimalByIdQueryHandler, GetAnimalListQuery, GetAnimalListQueryHandler (+2 more)

### Community 164 - "Community 164"
Cohesion: 0.20
Nodes (6): Guid, IReadOnlyCollection, List, Role, EntityTypeBuilder, RoleConfiguration

### Community 165 - "Community 165"
Cohesion: 0.36
Nodes (7): Guid, Village, CancellationToken, Guid, IReadOnlyList, Task, ILocationRepository

### Community 166 - "Community 166"
Cohesion: 0.20
Nodes (9): CancellationToken, Guid, Task, Tenant, ITenantRepository, CancellationToken, Guid, Task (+1 more)

### Community 167 - "Community 167"
Cohesion: 0.14
Nodes (13): description, name, private, $schema, scripts, build, build:prod, e2e (+5 more)

### Community 170 - "Community 170"
Cohesion: 0.21
Nodes (3): Organization, OrganizationListComponent, Component

### Community 171 - "Community 171"
Cohesion: 0.17
Nodes (9): Farm360.Persistence.Configurations.Inventory, InventoryStatus, PurchaseOrderStatus, StockTransactionType, DateOnly, Guid, StockTransaction, EntityTypeBuilder (+1 more)

### Community 172 - "Community 172"
Cohesion: 0.24
Nodes (8): Farm360.Domain.Finance.Enums, TransactionCategory, TransactionType, DateTime, Guid, FinancialTransaction, EntityTypeBuilder, FinancialTransactionConfiguration

### Community 173 - "Community 173"
Cohesion: 0.28
Nodes (8): CancellationToken, Guid, IResult, ISender, RouteGroupBuilder, Task, FinanceEndpoints, CreateFinancialTransactionRequest

### Community 174 - "Community 174"
Cohesion: 0.23
Nodes (9): Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options, Microsoft.NET.Sdk, AutoMapper, FluentValidation, FluentValidation.DependencyInjectionExtensions, MediatR (+1 more)

### Community 175 - "Community 175"
Cohesion: 0.26
Nodes (9): InventoryItemDto, CancellationToken, Task, GetInventoryItemDetailQuery, GetInventoryItemDetailQueryHandler, CancellationToken, Task, GetInventoryItemsQuery (+1 more)

### Community 176 - "Community 176"
Cohesion: 0.26
Nodes (7): DateTime, Guid, AuditableEntity, DateTime, Guid, ISoftDeletable, ITenantEntity

### Community 177 - "Community 177"
Cohesion: 0.23
Nodes (9): DateOnly, Guid, IReadOnlyCollection, List, ConsumptionDetail, FeedConsumptionLog, EntityTypeBuilder, ConsumptionDetailConfiguration (+1 more)

### Community 178 - "Community 178"
Cohesion: 0.21
Nodes (5): InventoryCategory, Guid, InventoryItem, EntityTypeBuilder, InventoryItemConfiguration

### Community 179 - "Community 179"
Cohesion: 0.41
Nodes (7): Guid, Upazila, CancellationToken, Guid, IReadOnlyList, Task, LocationRepository

### Community 180 - "Community 180"
Cohesion: 0.24
Nodes (8): Farm360.Api.Endpoints, Farm360.Application.Intelligence.Queries, Farm360.Application.Features.Intelligence.Queries.GetAnimalFinancialSnapshot, AnimalFinancialSnapshotDto, GetAnimalFinancialSnapshotQuery, CancellationToken, Task, GetAnimalFinancialSnapshotQueryHandler

### Community 181 - "Community 181"
Cohesion: 0.26
Nodes (8): Farm360.Application.Health.Queries.DiseaseIncidents, Farm360.Application.Health.DTOs, DiseaseIncidentDetailDto, VaccinationProtocolStepDto, CancellationToken, Task, GetDiseaseIncidentDetailQuery, GetDiseaseIncidentDetailQueryHandler

### Community 182 - "Community 182"
Cohesion: 0.24
Nodes (7): Farm360.Application.Organizations.Branches.DTOs, BranchDto, BranchMappingExtensions, CancellationToken, Task, GetBranchByIdQuery, GetBranchByIdQueryHandler

### Community 183 - "Community 183"
Cohesion: 0.18
Nodes (4): InventoryMappingExtensions, Supplier, EntityTypeBuilder, SupplierConfiguration

### Community 184 - "Community 184"
Cohesion: 0.24
Nodes (7): CancellationToken, Exception, Guid, Task, FarmNotificationHub, SignalRNotificationService, Hub

### Community 185 - "Community 185"
Cohesion: 0.17
Nodes (11): cli, analytics, prefix, projectType, root, sourceRoot, newProjectRoot, projects (+3 more)

### Community 187 - "Community 187"
Cohesion: 0.17
Nodes (5): DataTableComponent, Component, Input, Output, ViewChild

### Community 188 - "Community 188"
Cohesion: 0.27
Nodes (9): Farm360.Application.Feeding.Commands.ConsumptionLogs, CancellationToken, Guid, Task, ConsumptionIngredientDetailRequest, LogFeedConsumptionCommand, LogFeedConsumptionCommandHandler, LogFeedConsumptionCommandValidator (+1 more)

### Community 189 - "Community 189"
Cohesion: 0.38
Nodes (8): Farm360.Application.Feeding.Queries.FeedFormulas, FeedFormulaDto, CancellationToken, Task, GetFeedFormulaDetailQuery, GetFeedFormulaDetailQueryHandler, GetFeedFormulasQuery, GetFeedFormulasQueryHandler

### Community 190 - "Community 190"
Cohesion: 0.18
Nodes (8): Farm360.Infrastructure.Logging, IConfiguration, IHostEnvironment, IServiceCollection, IConfiguration, IHostEnvironment, SerilogConfiguration, LoggerConfiguration

### Community 191 - "Community 191"
Cohesion: 0.22
Nodes (7): Farm360.Api.Middleware, HttpContext, string, Task, CorrelationIdMiddleware, TenantCacheEntry, TenantResolutionMiddleware

### Community 192 - "Community 192"
Cohesion: 0.27
Nodes (8): Farm360.Application.Health.Commands.MortalityRecords, CancellationToken, Guid, Task, RecordMortalityCommand, RecordMortalityCommandHandler, RecordMortalityCommandValidator, MortalityRecord

### Community 193 - "Community 193"
Cohesion: 0.33
Nodes (7): DateTime, Guid, IEndpointRouteBuilder, IResult, ISender, Task, IntelligenceEndpoints

### Community 194 - "Community 194"
Cohesion: 0.24
Nodes (9): CancellationToken, ILogger, Task, PurchaseOrderFulfilledEventHandler, CancellationToken, Task, PurchaseOrderFulfilledEventHandler, PurchaseOrderFulfilledNotification (+1 more)

### Community 195 - "Community 195"
Cohesion: 0.25
Nodes (6): ScheduleFrequency, DateOnly, Guid, FeedingSchedule, EntityTypeBuilder, FeedingScheduleConfiguration

### Community 196 - "Community 196"
Cohesion: 0.24
Nodes (6): DateOnly, DateTime, Guid, BreedingRecord, EntityTypeBuilder, BreedingRecordConfiguration

### Community 197 - "Community 197"
Cohesion: 0.25
Nodes (6): DateTime, Guid, Notification, NotificationType, EntityTypeBuilder, NotificationConfiguration

### Community 198 - "Community 198"
Cohesion: 0.36
Nodes (7): CancellationToken, Guid, IReadOnlyList, string, Task, TimeSpan, PermissionService

### Community 199 - "Community 199"
Cohesion: 0.22
Nodes (4): CreateProtocolDialogComponent, Component, Inject, Optional

### Community 200 - "Community 200"
Cohesion: 0.24
Nodes (5): Farm360.Identity.Migrations.Identity, MigrationBuilder, ModelBuilder, InitialIdentity, Migration

### Community 201 - "Community 201"
Cohesion: 0.31
Nodes (8): Farm360.Application.Feeding.Commands.FeedFormulas, CancellationToken, Guid, Task, CreateFeedFormulaCommand, CreateFeedFormulaCommandHandler, CreateFeedFormulaCommandValidator, FormulaIngredientRequest

### Community 202 - "Community 202"
Cohesion: 0.36
Nodes (8): Farm360.Application.Health.Queries.VaccinationEvents, VaccinationEventDto, CancellationToken, IReadOnlyList, Task, GetUpcomingVaccinationsQuery, GetUpcomingVaccinationsQueryHandler, GetUpcomingVaccinationsQueryValidator

### Community 203 - "Community 203"
Cohesion: 0.24
Nodes (7): Farm360.Infrastructure.Storage, CancellationToken, ILogger, Stream, Task, LocalFileStorageService, IWebHostEnvironment

### Community 204 - "Community 204"
Cohesion: 0.29
Nodes (7): DbContextEventData, CancellationToken, DbContext, ValueTask, AuditSaveChangesInterceptor, InterceptionResult, SaveChangesInterceptor

### Community 205 - "Community 205"
Cohesion: 0.40
Nodes (5): CancellationToken, Guid, IReadOnlyList, Task, IFinancialTransactionRepository

### Community 206 - "Community 206"
Cohesion: 0.27
Nodes (5): DateOnly, Guid, DailyFeedingEntry, EntityTypeBuilder, DailyFeedingEntryConfiguration

### Community 207 - "Community 207"
Cohesion: 0.22
Nodes (5): DateTime, Guid, AnimalPhoto, EntityTypeBuilder, AnimalPhotoConfiguration

### Community 208 - "Community 208"
Cohesion: 0.40
Nodes (5): CancellationToken, Guid, IReadOnlyList, Task, FinancialTransactionRepository

### Community 209 - "Community 209"
Cohesion: 0.53
Nodes (4): CancellationToken, Guid, Task, HealthDashboardRepository

### Community 211 - "Community 211"
Cohesion: 0.36
Nodes (5): CreateFinancialTransactionRequest, FinancialTransaction, FinancialTransactionSummary, FinanceService, Injectable

### Community 213 - "Community 213"
Cohesion: 0.42
Nodes (7): CancellationToken, IReadOnlyList, Task, FeedingRuleLineDto, FeedingRuleSetDto, GetFeedingRuleSetsQuery, GetFeedingRuleSetsQueryHandler

### Community 214 - "Community 214"
Cohesion: 0.42
Nodes (7): CancellationToken, Task, AnimalRegisteredNotification, IntelligenceEventDispatcher, WeightRecordedNotification, FeedConsumptionLoggedNotification, INotification

### Community 215 - "Community 215"
Cohesion: 0.28
Nodes (5): Guid, PurchaseOrderItem, EntityTypeBuilder, PurchaseOrderConfiguration, PurchaseOrderItemConfiguration

### Community 216 - "Community 216"
Cohesion: 0.22
Nodes (5): ModelBuilder, IdentityDbContextModelSnapshot, ModelBuilder, ApplicationDbContextModelSnapshot, ModelSnapshot

### Community 217 - "Community 217"
Cohesion: 0.39
Nodes (5): CancellationToken, JsonSerializerOptions, Task, TimeSpan, RedisCacheService

### Community 218 - "Community 218"
Cohesion: 0.22
Nodes (8): Microsoft.EntityFrameworkCore.Design, Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Tools, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Options, Microsoft.EntityFrameworkCore, Microsoft.Extensions.Configuration.EnvironmentVariables, Microsoft.Extensions.Configuration.Json

### Community 220 - "Community 220"
Cohesion: 0.33
Nodes (3): FeedingCycleReconciliation, ReconciliationListComponent, Component

### Community 221 - "Community 221"
Cohesion: 0.33
Nodes (4): GlobalSearchComponent, Component, ViewChild, HostListener

### Community 222 - "Community 222"
Cohesion: 0.29
Nodes (5): Assembly, Farm360.Application.Common.Mappings, IMapFrom, MappingProfile, Profile

### Community 223 - "Community 223"
Cohesion: 0.29
Nodes (6): Farm360.Shared.Constants, DateTime, string, TimeSpan, DateFormats, TimeZones

### Community 224 - "Community 224"
Cohesion: 0.36
Nodes (6): Farm360.Application.Feeding.Commands.FeedIngredients, CancellationToken, Task, UpdateFeedIngredientCommand, UpdateFeedIngredientCommandHandler, UpdateFeedIngredientCommandValidator

### Community 225 - "Community 225"
Cohesion: 0.36
Nodes (6): Farm360.Application.Health.Commands.MedicalTreatments, CancellationToken, Task, UpdateTreatmentStatusCommand, UpdateTreatmentStatusCommandHandler, UpdateTreatmentStatusCommandValidator

### Community 226 - "Community 226"
Cohesion: 0.39
Nodes (6): Farm360.Application.Health.Queries.Deworming, DewormingCalendarEventDto, CancellationToken, Task, GetDewormingCalendarQuery, GetDewormingCalendarQueryHandler

### Community 227 - "Community 227"
Cohesion: 0.50
Nodes (6): CancellationToken, IReadOnlyList, Task, AnimalFeedingPlanDto, GetFeedingPlansQuery, GetFeedingPlansQueryHandler

### Community 228 - "Community 228"
Cohesion: 0.50
Nodes (6): CancellationToken, IReadOnlyList, Task, DailyFeedingEntryDto, GetTodayFeedingEntriesQuery, GetTodayFeedingEntriesQueryHandler

### Community 229 - "Community 229"
Cohesion: 0.50
Nodes (6): CancellationToken, List, Task, FeedingReconciliationDto, GetReconciliationsQuery, GetReconciliationsQueryHandler

### Community 230 - "Community 230"
Cohesion: 0.36
Nodes (6): CancellationToken, Task, UpdateVaccinationProtocolCommand, UpdateVaccinationProtocolCommandHandler, UpdateVaccinationProtocolCommandValidator, UpdateVaccinationProtocolStepDto

### Community 231 - "Community 231"
Cohesion: 0.46
Nodes (6): CancellationToken, Task, AnimalHealthHistoryDto, GetAnimalHealthHistoryQuery, GetAnimalHealthHistoryQueryHandler, GetAnimalHealthHistoryQueryValidator

### Community 232 - "Community 232"
Cohesion: 0.25
Nodes (6): CancellationToken, Task, CancellationToken, Guid, List, Task

### Community 233 - "Community 233"
Cohesion: 0.36
Nodes (5): CancellationToken, Guid, Task, TenantUser, ITenantUserRepository

### Community 234 - "Community 234"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddOrganizationModule

### Community 235 - "Community 235"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddBranchManagementFeatures

### Community 236 - "Community 236"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddFarmManagementModule

### Community 237 - "Community 237"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddShedManagementModule

### Community 238 - "Community 238"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddPenManagementModule

### Community 239 - "Community 239"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddMasterDataModule

### Community 240 - "Community 240"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddInventoryModuleEntities

### Community 241 - "Community 241"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddBreedManagement

### Community 242 - "Community 242"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddPurchaseOrderModule

### Community 243 - "Community 243"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddFinanceModule

### Community 244 - "Community 244"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, UpdateFeedingRuleSetModel

### Community 245 - "Community 245"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddInventoryFieldsToMedicalTreatment

### Community 246 - "Community 246"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddLivestockBatchAndBcs

### Community 247 - "Community 247"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, AddHealthSchema

### Community 248 - "Community 248"
Cohesion: 0.25
Nodes (8): schematics, standalone, style, standalone, standalone, @schematics/angular:component, @schematics/angular:directive, @schematics/angular:pipe

### Community 249 - "Community 249"
Cohesion: 0.29
Nodes (3): MortalityListComponent, Component, ViewChild

### Community 250 - "Community 250"
Cohesion: 0.32
Nodes (3): Component, ViewChild, VaccinationProtocolListComponent

### Community 253 - "Community 253"
Cohesion: 0.29
Nodes (3): Farm360.Infrastructure.Messaging, Farm360.Infrastructure.DependencyInjection, InfrastructureServiceExtensions

### Community 254 - "Community 254"
Cohesion: 0.43
Nodes (5): Exception, HttpContext, JsonSerializerOptions, Task, GlobalExceptionMiddleware

### Community 255 - "Community 255"
Cohesion: 0.38
Nodes (5): CancellationToken, ILogger, Task, ExcludeAnimalFromPlanCommand, ExcludeAnimalFromPlanCommandHandler

### Community 256 - "Community 256"
Cohesion: 0.43
Nodes (6): CancellationToken, Guid, Task, CreateFeedIngredientCommand, CreateFeedIngredientCommandHandler, CreateFeedIngredientCommandValidator

### Community 257 - "Community 257"
Cohesion: 0.43
Nodes (6): CancellationToken, Guid, Task, RecordStockOutCommand, RecordStockOutCommandHandler, RecordStockOutCommandValidator

### Community 258 - "Community 258"
Cohesion: 0.43
Nodes (5): CancellationToken, Task, ConfirmPregnancyCommand, ConfirmPregnancyCommandHandler, ConfirmPregnancyCommandValidator

### Community 259 - "Community 259"
Cohesion: 0.43
Nodes (5): CancellationToken, Task, RecordCalvingCommand, RecordCalvingCommandHandler, RecordCalvingCommandValidator

### Community 260 - "Community 260"
Cohesion: 0.43
Nodes (5): CancellationToken, Task, RecordMatingCommand, RecordMatingCommandHandler, RecordMatingCommandValidator

### Community 261 - "Community 261"
Cohesion: 0.38
Nodes (3): Expression, TimeSpan, HangfireBackgroundJobService

### Community 262 - "Community 262"
Cohesion: 0.33
Nodes (3): MigrationBuilder, ModelBuilder, AddFeedingModule

### Community 263 - "Community 263"
Cohesion: 0.29
Nodes (7): extract-i18n, test, builder, options, architect, buildTarget, builder

### Community 264 - "Community 264"
Cohesion: 0.29
Nodes (3): MainLayoutComponent, Component, ViewChild

### Community 274 - "Community 274"
Cohesion: 0.38
Nodes (3): Component, ViewChild, VetVisitListComponent

### Community 276 - "Community 276"
Cohesion: 0.29
Nodes (3): AnimalEditDialogComponent, Component, Inject

### Community 277 - "Community 277"
Cohesion: 0.29
Nodes (3): ConfirmPregnancyDialogComponent, ConfirmPregnancyDialogData, Component

### Community 279 - "Community 279"
Cohesion: 0.38
Nodes (3): BreadcrumbComponent, Component, Input

### Community 280 - "Community 280"
Cohesion: 0.53
Nodes (5): Farm360.Contracts.IntegrationEvents, DateTime, Guid, BaseIntegrationEvent, IIntegrationEvent

### Community 281 - "Community 281"
Cohesion: 0.40
Nodes (3): Farm360.Infrastructure.Caching, Guid, CacheKeyBuilder

### Community 282 - "Community 282"
Cohesion: 0.33
Nodes (5): Microsoft.EntityFrameworkCore.Design, Serilog.AspNetCore, Microsoft.AspNetCore.OpenApi, Scalar.AspNetCore, Microsoft.NET.Sdk.Web

### Community 284 - "Community 284"
Cohesion: 0.40
Nodes (5): CancellationToken, ILogger, Task, DailyEntryConfirmedEventHandler, DailyEntryConfirmedEvent

### Community 285 - "Community 285"
Cohesion: 0.40
Nodes (5): CancellationToken, ILogger, Task, TreatmentLoggedStockDeductionHandler, TreatmentLoggedEvent

### Community 286 - "Community 286"
Cohesion: 0.40
Nodes (5): CancellationToken, ILogger, Task, VaccinationAdministeredNotification, VaccinationAdministeredStockDeductionHandler

### Community 287 - "Community 287"
Cohesion: 0.47
Nodes (4): CancellationToken, Task, DeleteMasterDataCommand, DeleteMasterDataCommandHandler

### Community 288 - "Community 288"
Cohesion: 0.60
Nodes (5): DateTime, Guid, LowStockAlertEvent, StockDeductedEvent, StockReceivedEvent

### Community 289 - "Community 289"
Cohesion: 0.60
Nodes (3): CancellationToken, Task, LoggingSmsService

### Community 290 - "Community 290"
Cohesion: 0.33
Nodes (3): AUTH_ROUTES, LoginComponent, Component

### Community 295 - "Community 295"
Cohesion: 0.33
Nodes (3): RecordBcsDialogComponent, RecordBcsDialogData, Component

### Community 296 - "Community 296"
Cohesion: 0.33
Nodes (3): RecordSaleDialogComponent, RecordSaleDialogData, Component

### Community 297 - "Community 297"
Cohesion: 0.33
Nodes (3): RecordWeightDialogComponent, RecordWeightDialogData, Component

### Community 299 - "Community 299"
Cohesion: 0.33
Nodes (3): Component, ViewChild, UploadPhotoDialogComponent

### Community 301 - "Community 301"
Cohesion: 0.40
Nodes (4): Farm360.Contracts.Envelopes, DateTime, Guid, OutboxMessage

### Community 302 - "Community 302"
Cohesion: 0.40
Nodes (3): Farm360.Persistence.Configurations.MasterData, EntityTypeBuilder, MasterDataConfiguration

### Community 303 - "Community 303"
Cohesion: 0.40
Nodes (3): Farm360.Api.OpenApi, IServiceCollection, OpenApiConfiguration

### Community 304 - "Community 304"
Cohesion: 0.60
Nodes (3): CancellationToken, Task, LoggingEmailService

### Community 305 - "Community 305"
Cohesion: 0.40
Nodes (4): LowStockCount, OutOfStockCount, TotalItems, TotalValueBdt

### Community 306 - "Community 306"
Cohesion: 0.40
Nodes (3): BreedingAnalyticsDto, BreedingDashboardComponent, Component

### Community 307 - "Community 307"
Cohesion: 0.40
Nodes (3): HealthAnalyticsDto, HealthDashboardComponent, Component

### Community 310 - "Community 310"
Cohesion: 0.50
Nodes (3): Attribute, Farm360.Shared.Attributes, SensitiveDataAttribute

### Community 311 - "Community 311"
Cohesion: 0.50
Nodes (3): DateTime, Guid, PurchaseOrderFulfilledEvent

## Knowledge Gaps
- **262 isolated node(s):** `LogoutRequest`, `RefreshTokenApiRequest`, `CreatePenRequest`, `UpdatePenRequest`, `AdministerVaccinationRequest` (+257 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **38 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ApplicationDbContext` connect `Community 37` to `Community 4`, `Community 135`, `Community 10`, `Community 11`, `Community 16`, `Community 17`, `Community 149`, `Community 26`, `Community 30`, `Community 36`, `Community 164`, `Community 165`, `Community 166`, `Community 41`, `Community 171`, `Community 44`, `Community 172`, `Community 177`, `Community 178`, `Community 179`, `Community 50`, `Community 183`, `Community 57`, `Community 59`, `Community 64`, `Community 65`, `Community 195`, `Community 196`, `Community 67`, `Community 197`, `Community 74`, `Community 77`, `Community 206`, `Community 78`, `Community 79`, `Community 207`, `Community 208`, `Community 84`, `Community 215`, `Community 91`, `Community 94`, `Community 100`, `Community 101`, `Community 105`, `Community 121`, `Community 122`, `Community 124`?**
  _High betweenness centrality (0.095) - this node is a cross-community bridge._
- **Why does `Farm360.Application.Common.Interfaces` connect `Community 34` to `Community 258`, `Community 131`, `Community 259`, `Community 133`, `Community 260`, `Community 136`, `Community 14`, `Community 143`, `Community 15`, `Community 146`, `Community 147`, `Community 20`, `Community 21`, `Community 22`, `Community 23`, `Community 24`, `Community 153`, `Community 25`, `Community 27`, `Community 154`, `Community 29`, `Community 152`, `Community 159`, `Community 160`, `Community 287`, `Community 281`, `Community 35`, `Community 36`, `Community 38`, `Community 42`, `Community 43`, `Community 45`, `Community 48`, `Community 49`, `Community 52`, `Community 53`, `Community 54`, `Community 55`, `Community 56`, `Community 182`, `Community 58`, `Community 184`, `Community 188`, `Community 189`, `Community 62`, `Community 192`, `Community 70`, `Community 201`, `Community 203`, `Community 82`, `Community 83`, `Community 213`, `Community 85`, `Community 87`, `Community 88`, `Community 86`, `Community 90`, `Community 89`, `Community 92`, `Community 93`, `Community 224`, `Community 96`, `Community 225`, `Community 227`, `Community 228`, `Community 230`, `Community 104`, `Community 107`, `Community 108`, `Community 111`, `Community 118`, `Community 120`, `Community 253`, `Community 125`?**
  _High betweenness centrality (0.089) - this node is a cross-community bridge._
- **Why does `Farm360.Persistence.Context` connect `Community 23` to `Community 132`, `Community 26`, `Community 154`, `Community 155`, `Community 108`, `Community 34`, `Community 36`, `Community 37`, `Community 38`, `Community 47`, `Community 50`, `Community 63`, `Community 66`, `Community 68`, `Community 216`, `Community 90`, `Community 234`, `Community 235`, `Community 236`, `Community 237`, `Community 238`, `Community 239`, `Community 240`, `Community 241`, `Community 242`, `Community 243`, `Community 244`, `Community 245`, `Community 246`, `Community 247`?**
  _High betweenness centrality (0.040) - this node is a cross-community bridge._
- **What connects `LogoutRequest`, `RefreshTokenApiRequest`, `CreatePenRequest` to the rest of the system?**
  _262 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.041237113402061855 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.09135802469135802 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.04208860759493671 - nodes in this community are weakly interconnected._