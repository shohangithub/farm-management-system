using Farm360.Application.Common.Interfaces;
using Farm360.Application.Livestock.Queries;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Tasks;

/// <summary>A pending health task with the animal it is for (read model; not tracked).</summary>
public sealed record HealthTaskRow(
    VaccinationEvent Event,
    string AnimalTag,
    Guid FarmId,
    Guid? ShedId,
    Guid? PenId,
    decimal? LatestWeightKg);

/// <summary>A medicine's stock, in its base unit.</summary>
public sealed record HealthTaskStockItem(Guid Id, string Name, string Unit, decimal CurrentStock, bool IsActive);

/// <summary>A stock unit (unit conversion): its name and how many base units one of it holds.</summary>
public sealed record HealthTaskUnit(Guid Id, string Name, decimal Factor);

/// <summary>Reads pending health tasks for the task list and reminders. Implemented in Persistence.</summary>
public interface IHealthTaskSource
{
    /// <summary>Scheduled/overdue tasks dated on or before <paramref name="upTo"/> for animals still on the farm.</summary>
    Task<IReadOnlyList<HealthTaskRow>> GetPendingAsync(Guid farmId, DateOnly upTo, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, HealthTaskStockItem>> GetStockItemsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, HealthTaskUnit>> GetUnitsAsync(IReadOnlyCollection<Guid> unitIds, CancellationToken cancellationToken = default);
}

/// <summary>Which list a task falls in, relative to the farm's today.</summary>
public static class HealthTaskBuckets
{
    public const string Overdue = "overdue";
    public const string Today = "today";
    public const string Tomorrow = "tomorrow";

    public static string For(DateOnly scheduled, DateOnly today) =>
        scheduled < today ? Overdue : scheduled == today ? Today : Tomorrow;
}

public sealed record HealthTaskDto(
    Guid EventId,
    Guid AnimalId,
    string AnimalTag,
    Guid? ShedId,
    string? ShedName,
    string? PenName,
    string TaskName,
    string? StepName,
    string? Instruction,
    HealthTaskKind Kind,
    DateOnly ScheduledDate,
    string Bucket,
    int DaysOverdue,
    decimal? Dose,
    string? DoseUnit,
    bool WeightBased,
    decimal? DoseWeightKg,
    bool DoseWeightEstimated,
    Guid? InventoryItemId,
    string? InventoryItemName,
    decimal? StockAvailable,
    string? StockUnit,
    bool StockShort,
    bool NeedsStockLink,
    int? RepeatEveryDays,
    string? ActionLink,
    Guid? ProtocolId);

public sealed record HealthTasksDto(DateOnly Today, IReadOnlyList<HealthTaskDto> Items);

/// <summary>
/// "Today's tasks": overdue, today's and tomorrow's health tasks on a farm, with the dose worked out
/// from each animal's latest weight (or the farm's fallback weight, marked estimated) and a warning
/// when the linked medicine's stock will not cover the tasks listed before it.
/// </summary>
public sealed record GetHealthTasksQuery(Guid FarmId) : IRequest<HealthTasksDto>;

public sealed class GetHealthTasksQueryValidator : AbstractValidator<GetHealthTasksQuery>
{
    public GetHealthTasksQueryValidator()
    {
        RuleFor(q => q.FarmId).NotEmpty();
    }
}

internal sealed class GetHealthTasksQueryHandler(
    IHealthTaskSource source,
    ITenantService tenantService,
    IFarmBusinessRulesProvider rules,
    IFarmClock? clock = null,
    IAnimalLocationNames? locations = null) : IRequestHandler<GetHealthTasksQuery, HealthTasksDto>
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task<HealthTasksDto> Handle(GetHealthTasksQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantService.TenantId;
        var today = _clock.TodayForFarm(tenantId, request.FarmId);
        var rows = await source.GetPendingAsync(request.FarmId, today.AddDays(1), cancellationToken);
        if (rows.Count == 0)
            return new HealthTasksDto(today, []);

        var fallbackKg = rules.For(tenantId, request.FarmId).FallbackAnimalWeightKg;
        var items = await source.GetStockItemsAsync(
            rows.Where(r => r.Event.InventoryItemId is not null).Select(r => r.Event.InventoryItemId!.Value).Distinct().ToList(), cancellationToken);
        var units = await source.GetUnitsAsync(
            rows.Where(r => r.Event.DoseUnitId is not null).Select(r => r.Event.DoseUnitId!.Value).Distinct().ToList(), cancellationToken);

        IReadOnlyDictionary<Guid, string> sheds = new Dictionary<Guid, string>();
        IReadOnlyDictionary<Guid, string> pens = new Dictionary<Guid, string>();
        if (locations is not null)
        {
            sheds = await locations.GetShedNamesAsync(rows.Where(r => r.ShedId is not null).Select(r => r.ShedId!.Value).Distinct().ToList(), cancellationToken);
            pens = await locations.GetPenNamesAsync(rows.Where(r => r.PenId is not null).Select(r => r.PenId!.Value).Distinct().ToList(), cancellationToken);
        }

        var ordered = rows
            .OrderBy(r => r.Event.ScheduledDate)
            .ThenBy(r => r.AnimalTag, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Event.VaccineName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Stock used by the tasks above each row, per item (base unit), in list order.
        var runningNeed = new Dictionary<Guid, decimal>();
        var result = new List<HealthTaskDto>(ordered.Count);

        foreach (var row in ordered)
        {
            var e = row.Event;
            var weight = DoseWeight.For(row.LatestWeightKg, fallbackKg);
            var rule = e.DoseRule;
            var dose = rule is not null ? rule.DoseFor(weight.WeightKg) : e.DosageQuantity;

            HealthTaskStockItem? item = null;
            if (e.InventoryItemId is { } itemId) items.TryGetValue(itemId, out item);
            HealthTaskUnit? unit = null;
            if (e.DoseUnitId is { } unitId) units.TryGetValue(unitId, out unit);

            var doseUnit = rule?.Unit ?? unit?.Name ?? item?.Unit;

            var stockShort = false;
            if (!e.IsCareTask && item is not null && dose is > 0)
            {
                var baseNeed = dose.Value * (unit?.Factor ?? 1m);
                var need = runningNeed.GetValueOrDefault(item.Id) + baseNeed;
                runningNeed[item.Id] = need;
                stockShort = !item.IsActive || item.CurrentStock < need;
            }

            result.Add(new HealthTaskDto(
                e.Id,
                e.AnimalId,
                row.AnimalTag,
                row.ShedId,
                row.ShedId is { } s ? sheds.GetValueOrDefault(s) : null,
                row.PenId is { } p ? pens.GetValueOrDefault(p) : null,
                e.VaccineName,
                e.StepName,
                e.Instruction,
                e.Kind,
                e.ScheduledDate,
                HealthTaskBuckets.For(e.ScheduledDate, today),
                e.ScheduledDate < today ? today.DayNumber - e.ScheduledDate.DayNumber : 0,
                e.IsCareTask ? null : dose,
                e.IsCareTask ? null : doseUnit,
                rule is not null,
                rule is not null ? weight.WeightKg : null,
                rule is not null && weight.Estimated,
                e.InventoryItemId,
                item?.Name,
                item?.CurrentStock,
                item?.Unit,
                stockShort,
                e.Kind == HealthTaskKind.Medicine && e.InventoryItemId is null,
                e.RepeatEveryDays,
                e.ActionLink,
                e.ProtocolId));
        }

        // Overdue first (oldest first), then today, then tomorrow.
        return new HealthTasksDto(today, result
            .OrderBy(t => t.Bucket switch { HealthTaskBuckets.Overdue => 0, HealthTaskBuckets.Today => 1, _ => 2 })
            .ThenBy(t => t.ScheduledDate)
            .ToList());
    }
}
