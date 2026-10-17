using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Units;

// ── DTOs ───────────────────────────────────────────────────────────────────

/// <param name="Factor">Base units in one of this unit — FrostTrack's "Conversion Value".</param>
public sealed record UnitConversionDto(
    Guid Id, Guid MeasureUnitId, string BaseUnitName, string Name, decimal Factor, string? Description, bool IsActive, bool IsBase);

public sealed record MeasureUnitDto(Guid Id, string Name, string? Description, bool IsActive, IReadOnlyList<UnitConversionDto> Conversions);

/// <summary>The units an item can be entered in, its base unit and the default (purchase) unit.</summary>
public sealed record ItemUnitsDto(Guid InventoryItemId, string BaseUnit, Guid? DefaultUnitId, IReadOnlyList<UnitConversionDto> Units);

internal static class UnitMappings
{
    public static UnitConversionDto ToDto(this UnitConversion c, MeasureUnit owner) =>
        new(c.Id, owner.Id, owner.Name, c.Name, c.Factor, c.Description, c.IsActive, owner.IsSelf(c));

    public static MeasureUnitDto ToDto(this MeasureUnit u) =>
        new(u.Id, u.Name, u.Description, u.IsActive,
            u.Conversions.OrderBy(c => c.Factor).Select(c => c.ToDto(u)).ToList());
}

// ── Queries ────────────────────────────────────────────────────────────────

/// <summary>Base units (FrostTrack "Base Units" page), each with its conversions.</summary>
public sealed record GetMeasureUnitsQuery : IRequest<IReadOnlyList<MeasureUnitDto>>;

public sealed class GetMeasureUnitsQueryHandler(IUnitService units) : IRequestHandler<GetMeasureUnitsQuery, IReadOnlyList<MeasureUnitDto>>
{
    public async Task<IReadOnlyList<MeasureUnitDto>> Handle(GetMeasureUnitsQuery request, CancellationToken cancellationToken) =>
        (await units.GetUnitsAsync(cancellationToken)).Select(u => u.ToDto()).ToList();
}

/// <summary>Every conversion with its base unit (FrostTrack "Unit Conversions" page).</summary>
public sealed record GetUnitConversionsQuery : IRequest<IReadOnlyList<UnitConversionDto>>;

public sealed class GetUnitConversionsQueryHandler(IUnitService units) : IRequestHandler<GetUnitConversionsQuery, IReadOnlyList<UnitConversionDto>>
{
    public async Task<IReadOnlyList<UnitConversionDto>> Handle(GetUnitConversionsQuery request, CancellationToken cancellationToken) =>
        (await units.GetUnitsAsync(cancellationToken))
            .SelectMany(u => u.Conversions.Select(c => c.ToDto(u)))
            .OrderBy(c => c.BaseUnitName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Factor)
            .ToList();
}

public sealed record GetItemUnitsQuery(Guid InventoryItemId) : IRequest<ItemUnitsDto>;

public sealed class GetItemUnitsQueryHandler(IUnitService units, IInventoryItemRepository items) : IRequestHandler<GetItemUnitsQuery, ItemUnitsDto>
{
    public async Task<ItemUnitsDto> Handle(GetItemUnitsQuery request, CancellationToken cancellationToken)
    {
        var item = await items.GetByIdAsync(request.InventoryItemId, cancellationToken)
            ?? throw new NotFoundException("InventoryItem", request.InventoryItemId);
        var all = await units.GetUnitsAsync(cancellationToken);
        var owner = all.FirstOrDefault(u => u.IsActive && UnitService.SameUnit(u.Name, item.UnitOfMeasure));
        var list = owner is null ? [] : owner.Conversions.Where(c => c.IsActive).OrderBy(c => c.Factor).Select(c => c.ToDto(owner)).ToList();
        return new ItemUnitsDto(item.Id, item.UnitOfMeasure, item.DefaultUnitId, list);
    }
}

// ── Base unit commands ─────────────────────────────────────────────────────

public sealed record SaveMeasureUnitCommand(Guid? Id, string Name, string? Description, bool IsActive = true) : IRequest<MeasureUnitDto>;

public sealed class SaveMeasureUnitCommandHandler(IMeasureUnitRepository repo, IUnitService units, ITenantService tenant, IUnitOfWork unitOfWork)
    : IRequestHandler<SaveMeasureUnitCommand, MeasureUnitDto>
{
    public async Task<MeasureUnitDto> Handle(SaveMeasureUnitCommand request, CancellationToken cancellationToken)
    {
        var all = await units.GetUnitsAsync(cancellationToken);
        if (all.Any(u => u.Id != request.Id && string.Equals(u.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"A base unit called '{request.Name}' already exists.");

        MeasureUnit unit;
        if (request.Id is { } id)
        {
            unit = await repo.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("MeasureUnit", id);
            var oldName = unit.Name;
            if (!string.Equals(oldName, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                && (await repo.ItemsUsingAsync([oldName], [], cancellationToken)).Count > 0)
                throw new ConflictException($"Items are counted in '{oldName}', so it can't be renamed. Create a new base unit instead.");

            // The conversion that is the base unit itself follows the rename.
            var self = unit.Conversions.FirstOrDefault(unit.IsSelf);
            unit.Rename(request.Name, request.Description);
            self?.Update(unit.Name, 1m, self.Description);
            unit.SetActive(request.IsActive);
            repo.Update(unit);
        }
        else
        {
            unit = new MeasureUnit(Guid.NewGuid(), tenant.TenantId, request.Name, request.Description);
            unit.SetActive(request.IsActive);
            await repo.AddAsync(unit, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return unit.ToDto();
    }
}

/// <summary>Deletes base units (and their conversions) — refused while any item is counted in them.</summary>
public sealed record DeleteMeasureUnitsCommand(IReadOnlyList<Guid> Ids) : IRequest;

public sealed class DeleteMeasureUnitsCommandHandler(IMeasureUnitRepository repo, IUnitOfWork unitOfWork) : IRequestHandler<DeleteMeasureUnitsCommand>
{
    public async Task Handle(DeleteMeasureUnitsCommand request, CancellationToken cancellationToken)
    {
        var targets = new List<MeasureUnit>();
        foreach (var id in request.Ids.Distinct())
            targets.Add(await repo.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("MeasureUnit", id));

        var inUse = await repo.ItemsUsingAsync(
            targets.Select(t => t.Name).ToList(),
            targets.SelectMany(t => t.Conversions).Select(c => c.Id).ToList(),
            cancellationToken);
        if (inUse.Count > 0)
            throw new ConflictException($"In use by {string.Join(", ", inUse)} — change those items first, or set the unit inactive.");

        foreach (var unit in targets)
            repo.Remove(unit);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ── Conversion commands ────────────────────────────────────────────────────

/// <param name="MeasureUnitId">The base unit (FrostTrack "Base Unit").</param>
/// <param name="Factor">How many base units one of this unit holds (FrostTrack "Conversion Value"; Bag 50 kg → 50).</param>
public sealed record SaveUnitConversionCommand(
    Guid MeasureUnitId, Guid? Id, string Name, decimal Factor, string? Description = null, bool IsActive = true) : IRequest<UnitConversionDto>;

public sealed class SaveUnitConversionCommandHandler(IMeasureUnitRepository repo, IUnitOfWork unitOfWork)
    : IRequestHandler<SaveUnitConversionCommand, UnitConversionDto>
{
    public async Task<UnitConversionDto> Handle(SaveUnitConversionCommand request, CancellationToken cancellationToken)
    {
        var target = await repo.GetByIdAsync(request.MeasureUnitId, cancellationToken)
            ?? throw new NotFoundException("MeasureUnit", request.MeasureUnitId);
        if (target.Conversions.Any(c => c.Id != request.Id && string.Equals(c.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"'{request.Name}' already exists for {target.Name}.");

        UnitConversion conversion;
        if (request.Id is { } id)
        {
            conversion = await repo.GetConversionAsync(id, cancellationToken) ?? throw new NotFoundException("UnitConversion", id);
            var owner = conversion.MeasureUnitId == target.Id ? target : await repo.GetByIdAsync(conversion.MeasureUnitId, cancellationToken);
            if (owner is not null && owner.IsSelf(conversion)
                && (request.Factor != 1m || !request.IsActive || conversion.MeasureUnitId != target.Id
                    || !string.Equals(request.Name.Trim(), owner.Name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"'{owner.Name}' is the base unit itself: it always counts as 1 and stays active. Only its description can change.");

            // Changing the value or base unit does not touch past stock: every movement was stored in the base unit.
            conversion.Update(request.Name, request.Factor, request.Description);
            conversion.SetActive(request.IsActive);
            conversion.MoveTo(target.Id);
        }
        else
        {
            conversion = target.AddConversion(request.Name, request.Factor, request.Description);
            conversion.SetActive(request.IsActive);
            repo.Update(target);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return conversion.ToDto(target);
    }
}

/// <summary>Deletes conversions — never a base unit's own, nor one set as an item's purchase unit.</summary>
public sealed record DeleteUnitConversionsCommand(IReadOnlyList<Guid> Ids) : IRequest;

public sealed class DeleteUnitConversionsCommandHandler(IMeasureUnitRepository repo, IUnitOfWork unitOfWork) : IRequestHandler<DeleteUnitConversionsCommand>
{
    public async Task Handle(DeleteUnitConversionsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().ToList();
        var inUse = await repo.ItemsUsingAsync([], ids, cancellationToken);
        if (inUse.Count > 0)
            throw new ConflictException($"Set as the purchase unit of {string.Join(", ", inUse)} — change those items first, or set it inactive.");

        foreach (var id in ids)
        {
            var conversion = await repo.GetConversionAsync(id, cancellationToken) ?? throw new NotFoundException("UnitConversion", id);
            var owner = await repo.GetByIdAsync(conversion.MeasureUnitId, cancellationToken);
            if (owner is not null && owner.IsSelf(conversion))
                throw new ArgumentException($"'{conversion.Name}' is the base unit itself and can't be deleted. Delete the base unit instead.");
            repo.RemoveConversion(conversion);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
