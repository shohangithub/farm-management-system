using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Intelligence.SellOrKeep;

/// <summary>Sell-or-keep advice for one animal on the farm, with its day-by-day projection.</summary>
public sealed record GetAnimalSellOrKeepQuery(Guid AnimalId) : IRequest<SellOrKeepAdvice?>;

public sealed class GetAnimalSellOrKeepQueryHandler(IAnimalSellOrKeepAdvisor advisor, IFarmAccessService farmAccess)
    : IRequestHandler<GetAnimalSellOrKeepQuery, SellOrKeepAdvice?>
{
    public async Task<SellOrKeepAdvice?> Handle(GetAnimalSellOrKeepQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await farmAccess.CanAccessAnimalAsync(request.AnimalId, cancellationToken).ConfigureAwait(false))
            throw new ForbiddenAccessException("You do not have access to this animal.");

        return await advisor.AdviseAnimalAsync(request.AnimalId, null, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Sell-or-keep advice for every animal on a farm (optionally one batch), without projections.</summary>
/// <remarks>The <see cref="FarmId"/> property puts the request through the farm-access check.</remarks>
public sealed record GetFarmSellOrKeepQuery(Guid FarmId, Guid? BatchId = null) : IRequest<IReadOnlyList<SellOrKeepAdvice>>;

public sealed class GetFarmSellOrKeepQueryHandler(IAnimalSellOrKeepAdvisor advisor)
    : IRequestHandler<GetFarmSellOrKeepQuery, IReadOnlyList<SellOrKeepAdvice>>
{
    public Task<IReadOnlyList<SellOrKeepAdvice>> Handle(GetFarmSellOrKeepQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return advisor.AdviseFarmAsync(request.FarmId, null, request.BatchId, null, includeProjection: false, cancellationToken);
    }
}

/// <summary>
/// The Fattening plan for one animal: the Sell-or-keep projection with optional what-if overrides.
/// With no overrides every figure equals the "Sell or keep?" card's.
/// </summary>
public sealed record GetAnimalFatteningPlanQuery(Guid AnimalId, SellOrKeepOverrides Overrides) : IRequest<FatteningPlan?>;

public sealed class GetAnimalFatteningPlanQueryValidator : AbstractValidator<GetAnimalFatteningPlanQuery>
{
    public GetAnimalFatteningPlanQueryValidator()
    {
        RuleFor(x => x.Overrides).NotNull();
        When(x => x.Overrides is not null, () =>
        {
            RuleFor(x => x.Overrides.PricePerKgBdt).GreaterThan(0m).LessThanOrEqualTo(100_000m).When(x => x.Overrides.PricePerKgBdt.HasValue)
                .WithName("Price per kg live");
            RuleFor(x => x.Overrides.PerHeadBdt).GreaterThanOrEqualTo(0m).LessThanOrEqualTo(10_000_000m).When(x => x.Overrides.PerHeadBdt.HasValue)
                .WithName("Extra per head");
            RuleFor(x => x.Overrides.AdgKgPerDay).InclusiveBetween(-5m, 5m).When(x => x.Overrides.AdgKgPerDay.HasValue)
                .WithName("Gain per day");
            RuleFor(x => x.Overrides.FeedPerDayBdt).InclusiveBetween(0m, 1_000_000m).When(x => x.Overrides.FeedPerDayBdt.HasValue)
                .WithName("Feed per day");
            RuleFor(x => x.Overrides.LabourOverheadPerDayBdt).InclusiveBetween(0m, 1_000_000m).When(x => x.Overrides.LabourOverheadPerDayBdt.HasValue)
                .WithName("Labour and overhead per day");
            RuleFor(x => x.Overrides.HealthPerDayBdt).InclusiveBetween(0m, 1_000_000m).When(x => x.Overrides.HealthPerDayBdt.HasValue)
                .WithName("Health per day");
            RuleFor(x => x.Overrides.HorizonDays).InclusiveBetween(1, SellOrKeepOverrides.MaxHorizonDays).When(x => x.Overrides.HorizonDays.HasValue)
                .WithName("Days ahead");
            RuleFor(x => x.Overrides.GrowthSlowdownPercent).InclusiveBetween(0m, 100m).When(x => x.Overrides.GrowthSlowdownPercent.HasValue)
                .WithName("Growth slowdown");
        });
    }
}

public sealed class GetAnimalFatteningPlanQueryHandler(IAnimalSellOrKeepAdvisor advisor, IFarmAccessService farmAccess)
    : IRequestHandler<GetAnimalFatteningPlanQuery, FatteningPlan?>
{
    public async Task<FatteningPlan?> Handle(GetAnimalFatteningPlanQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await farmAccess.CanAccessAnimalAsync(request.AnimalId, cancellationToken).ConfigureAwait(false))
            throw new ForbiddenAccessException("You do not have access to this animal.");

        return await advisor.PlanAnimalAsync(request.AnimalId, request.Overrides, null, cancellationToken).ConfigureAwait(false);
    }
}
