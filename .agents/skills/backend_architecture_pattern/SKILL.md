---
name: backend_architecture_pattern
description: Reusable patterns for implementing Domain-Driven Design (DDD), CQRS with MediatR, and Minimal APIs in the Farm360 .NET backend.
---

# Farm360 Backend Architecture Pattern

Whenever you implement backend features (Entities, CQRS Commands/Queries, or Endpoints) for Farm360, you MUST strictly adhere to the Clean Architecture and Domain-Driven Design constraints. This guide is specifically designed to prevent common build errors and architectural violations.

## 1. Domain Layer (`Farm360.Domain`)
All business rules and state must be encapsulated within Aggregate Roots.

- **Inheritance:** Aggregate roots must inherit from `AuditableEntity` and implement `IAggregateRoot`.
- **Encapsulation:** All property setters must be `private` or `init`. State mutations happen ONLY through domain methods.
- **Instantiation:** Provide a `private` parameterless constructor (for EF Core) and a static factory method (e.g., `public static Animal Create(...)`).
- **Collections:** Expose collections as `IReadOnlyCollection<T>`. Backing fields should be initialized (`private readonly List<T> _items = [];`).
- **Events:** Use domain methods to append events (e.g., `AddDomainEvent(new ItemCreatedEvent(Id));`).

## 2. Application Layer (`Farm360.Application`)
We use the CQRS pattern via MediatR and FluentValidation.

### 🛑 CRITICAL NAMESPACE RULES (DO NOT VIOLATE):
- **`IUnitOfWork`**: MUST be imported from `using Farm360.Application.Common.Interfaces;`. Do NOT use `Farm360.Domain.Common` or reference `Farm360.Persistence`.
- **Exceptions**: Application-level exceptions like `NotFoundException` MUST be imported from `using Farm360.Application.Common.Exceptions;`. Do NOT use `Farm360.Domain.Exceptions` for resource-not-found scenarios.
- **Database Context**: NEVER reference or inject `ApplicationDbContext` in this layer. The Application project has no reference to the Persistence project. You MUST use Repositories (e.g., `IInventoryItemRepository`) and `IUnitOfWork` instead.

### Handlers & CQRS Rules:
- **Commands/Queries:** Defined as `sealed record` and implement `IRequest<ResultType>`.
- **Validation:** Create an `AbstractValidator<TCommand>`. Inject repositories for async checks.
- **Null Checks (IDE0270):** When fetching entities that might be null, use the null-coalescing operator to throw the exception and satisfy IDE0270.

## 3. Persistence Layer (`Farm360.Persistence`)
Entity configurations map the domain model to SQL Server.

- **Configuration:** Implement `IEntityTypeConfiguration<T>`. Configure table names, keys, relationships, and property types.
- **Repositories:** Abstract database access behind interfaces (defined in the Domain layer). Rely on EF Core's Global Query Filters for `TenantId` and `IsDeleted`.
- **Interface Alignment:** Ensure that when adding a new method to a concrete Repository class, you ALWAYS add the corresponding method signature to the repository interface in the Domain project to prevent build failures in the Application layer.

## 4. API Layer (`Farm360.Api`)
We use ASP.NET Core Minimal APIs grouped by extension methods.

- **Mapping:** Create an extension method `MapMyContextEndpoints(this RouteGroupBuilder group)`.
- **Authorization:** Apply `.RequireAuthorization($"Permission:{PermissionConstants.Module.Action}")` to endpoints based on defined RBAC policies.
- **Execution:** Inject `ISender` (MediatR) and execute the command. Return standard `IResult` like `Results.Created`, `Results.Ok`, or `Results.NoContent`.

---

# End-to-End Implementation Flow Example (Animal Registration)

To illustrate the correct way to build a feature, follow this exact flow:

### 1. The Domain Model (`Farm360.Domain/Livestock/Animal.cs`)
```csharp
public sealed class Animal : AuditableEntity, IAggregateRoot
{
    private readonly List<WeightRecord> _weightRecords = [];
    public IReadOnlyCollection<WeightRecord> WeightRecords => _weightRecords.AsReadOnly();

    public Guid FarmId { get; private set; }
    public AnimalSpecies Species { get; private set; }

    private Animal() { } // For EF Core

    public static Animal Create(Guid tenantId, Guid farmId, AnimalSpecies species)
    {
        var animal = new Animal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FarmId = farmId,
            Species = species
        };
        animal.AddDomainEvent(new AnimalRegisteredEvent(animal.Id));
        return animal;
    }
}
```

### 2. The Command and Validator (`Farm360.Application/Livestock/Commands/AnimalCommands.cs`)
```csharp
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

public sealed record RegisterAnimalCommand(Guid FarmId, AnimalSpecies Species, string TagId) : IRequest<AnimalDto>;

public sealed class RegisterAnimalCommandValidator : AbstractValidator<RegisterAnimalCommand>
{
    public RegisterAnimalCommandValidator(IAnimalRepository repository)
    {
        RuleFor(x => x.FarmId).NotEmpty().WithMessage("Farm is required.");
        RuleFor(x => x.TagId).NotEmpty()
            .MustAsync(async (tagId, ct) => !await repository.TagExistsAsync(tagId, null, ct))
            .WithMessage("Tag ID already exists.");
    }
}
```

### 3. The Command Handler (`Farm360.Application/Livestock/Commands/AnimalCommands.cs`)
```csharp
public sealed class RegisterAnimalCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork,
    ITenantService tenantService) : IRequestHandler<RegisterAnimalCommand, AnimalDto>
{
    public async Task<AnimalDto> Handle(RegisterAnimalCommand request, CancellationToken cancellationToken)
    {
        // 1. Invoke Factory Method
        var animal = Animal.Create(
            tenantId: tenantService.TenantId,
            farmId: request.FarmId,
            species: request.Species
        );

        // 2. Perform Repository Actions
        repository.Add(animal);

        // 3. Persist Changes
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Return DTO mapping
        return animal.ToDto();
    }
}
```

### 4. The API Endpoint (`Farm360.Api/Endpoints/Livestock/LivestockEndpoints.cs`)
```csharp
public static class LivestockEndpoints
{
    public static RouteGroupBuilder MapLivestockEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Livestock").RequireAuthorization();

        group.MapPost("/animals", RegisterAnimal)
            .WithName("RegisterAnimal")
            .WithSummary("Register a new animal")
            .Produces<AnimalDto>(201)
            .Produces(422)
            .RequireAuthorization("Permission:animals.create");

        return group;
    }

    private static async Task<IResult> RegisterAnimal(
        RegisterAnimalCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Results.Created($"/api/v1/livestock/animals/{result.Id}", result);
    }
}
```

---

# End-to-End Implementation Flow Example (Delete)

To illustrate the correct way to build a delete feature, follow this flow:

### 1. The Command (`Farm360.Application/Inventory/Commands/DeleteSupplierCommand.cs`)
```csharp
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Commands.Suppliers;

public sealed record DeleteSupplierCommand(Guid Id) : IRequest;
```

### 2. The Command Handler (`Farm360.Application/Inventory/Commands/DeleteSupplierCommand.cs`)
```csharp
internal sealed class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand>
{
    private readonly ISupplierRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSupplierCommandHandler(ISupplierRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch Entity (and use ?? throw new NotFoundException to handle IDE0270 null check)
        var supplier = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Supplier", request.Id);

        // 2. Perform Repository Action
        _repository.Delete(supplier);

        // 3. Persist Changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

### 3. The API Endpoint (`Farm360.Api/Endpoints/Inventory/InventoryEndpoints.cs`)
```csharp
group.MapDelete("/suppliers/{id:guid}", async (
    Guid id,
    ISender sender,
    CancellationToken ct) =>
{
    var command = new DeleteSupplierCommand(id);
    await sender.Send(command, ct);
    return Results.NoContent();
})
.RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Delete}")
.WithName("DeleteSupplier");
```

---

# End-to-End Implementation Flow Example (Update)

To illustrate how to perform an update using Domain Methods:

### 1. The Command (`Farm360.Application/Inventory/Commands/UpdateSupplierCommand.cs`)
```csharp
public sealed record UpdateSupplierCommand(Guid Id, string Name) : IRequest;
```

### 2. The Command Handler
```csharp
internal sealed class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand>
{
    private readonly ISupplierRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSupplierCommandHandler(ISupplierRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch Entity
        var supplier = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Supplier", request.Id);

        // 2. Perform Domain Action (State mutation must happen via domain methods)
        supplier.UpdateDetails(request.Name);

        // 3. Update Repository & Save
        _repository.Update(supplier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

### 3. The API Endpoint
```csharp
group.MapPut("/suppliers/{id:guid}", async (Guid id, [FromBody] UpdateSupplierCommand command, ISender sender, CancellationToken ct) =>
{
    if (id != command.Id) return Results.BadRequest("Route ID does not match command ID.");
    await sender.Send(command, ct);
    return Results.NoContent();
})
.RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Edit}");
```

---

# End-to-End Implementation Flow Example (Read / Get By Id)

Queries should bypass the UnitOfWork and return DTOs.

### 1. The Query
```csharp
public sealed record GetSupplierByIdQuery(Guid Id) : IRequest<SupplierDto?>;
```

### 2. The Query Handler
```csharp
internal sealed class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, SupplierDto?>
{
    private readonly ISupplierRepository _repository;

    public GetSupplierByIdQueryHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<SupplierDto?> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var supplier = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return supplier?.ToDto();
    }
}
```

### 3. The API Endpoint
```csharp
group.MapGet("/suppliers/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
{
    var supplier = await sender.Send(new GetSupplierByIdQuery(id), ct);
    return supplier is null ? Results.NotFound() : Results.Ok(supplier);
})
.RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.View}");
```

---

# End-to-End Implementation Flow Example (Read Paginated List)

For paginated lists, rely on the `pagination_pattern` skill for the EF Core LINQ constraints, but here is the standard CQRS architecture:

### 1. The Query
```csharp
public sealed record GetSuppliersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? SortBy = null,
    bool SortDesc = false) : IRequest<PagedResult<SupplierDto>>;
```

### 2. The Query Handler
```csharp
internal sealed class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, PagedResult<SupplierDto>>
{
    private readonly ISupplierRepository _repository;

    public GetSuppliersQueryHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var (items, count) = await _repository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.SortBy,
            request.SortDesc,
            cancellationToken);

        var dtos = items.Select(x => x.ToDto()).ToList();
        return new PagedResult<SupplierDto>(dtos, count, request.PageNumber, request.PageSize);
    }
}
```

### 3. The API Endpoint
```csharp
group.MapGet("/suppliers", async ([AsParameters] GetSuppliersQuery query, ISender sender, CancellationToken ct) =>
{
    var result = await sender.Send(query, ct);
    return Results.Ok(result);
})
.RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.View}");
```
> [!NOTE]
> Ensure you use `[AsParameters]` for minimal API GET endpoints so that the parameters bind correctly from the querystring to the MediatR Query object. For a comprehensive guide on frontend mapping and EF Core LINQ behavior for pagination, read `pagination_pattern/SKILL.md`.
