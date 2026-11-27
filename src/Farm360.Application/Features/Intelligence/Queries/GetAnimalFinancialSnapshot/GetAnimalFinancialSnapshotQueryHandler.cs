using Farm360.Application.Intelligence.SellOrKeep;
using Farm360.Application.Intelligence.Services;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Features.Intelligence.Queries.GetAnimalFinancialSnapshot;

/// <summary>
/// "Where does this animal stand today, and what will the next one/two months cost?"
/// </summary>
/// <remarks>
/// Today's figures are actuals: cost to date from the cost ledger and value from the farm's
/// valuation policy. The forward figures come from the Sell-or-keep projection (the same engine as
/// the "Sell or keep?" card and the Fattening plan): the animal's actual cost run-rate (feed eaten
/// in the last 30 days, labour/overhead and health in the last 90), with feed growing with weight.
/// </remarks>
public sealed class GetAnimalFinancialSnapshotQueryHandler : IRequestHandler<GetAnimalFinancialSnapshotQuery, AnimalFinancialSnapshotDto>
{
    private const int ShortHorizonDays = 30;
    private const int LongHorizonDays = 60;

    private readonly IAnimalRepository _animalRepository;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IProjectionDefaultsResolver _defaultsResolver;
    private readonly IAnimalValuationService _valuation;
    private readonly IAnimalSellOrKeepAdvisor _advisor;

    public GetAnimalFinancialSnapshotQueryHandler(
        IAnimalRepository animalRepository,
        IAnimalCostLedgerRepository ledgerRepository,
        IProjectionDefaultsResolver defaultsResolver,
        IAnimalValuationService valuation,
        IAnimalSellOrKeepAdvisor advisor)
    {
        _animalRepository = animalRepository;
        _ledgerRepository = ledgerRepository;
        _defaultsResolver = defaultsResolver;
        _valuation = valuation;
        _advisor = advisor;
    }

    public async Task<AnimalFinancialSnapshotDto> Handle(GetAnimalFinancialSnapshotQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var animal = await _animalRepository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Farm360.Domain.Livestock.Animal), request.AnimalId);

        var defaults = await _defaultsResolver.ResolveDefaultsAsync(request.AnimalId, cancellationToken);
        var ledger = await _ledgerRepository.GetByAnimalIdAsync(request.AnimalId, cancellationToken);

        // Today: actuals.
        var costToDate = ProjectionInputsFactory.InvestmentToDate(defaults);
        // Valued with the animal's resolved price (own sales, breed price, farm rule) — the same price the
        // animal page's "Profit if sold today" card and the Sell-or-Keep card use.
        var currentValue = defaults.MarketPrice is { } price
            ? _valuation.CurrentValue(animal, ledger, costToDate, Farm360.Application.Intelligence.Pricing.AnimalPrice.FromDto(price))
            : _valuation.CurrentValue(animal, ledger, costToDate);

        // Forward: the Sell-or-keep projection's running cost over the next 30 / 60 days. Sold or dead
        // animals incur no further running cost (the advisor returns nothing for them).
        var advice = _valuation.IsOnHand(animal)
            ? await _advisor.AdviseAnimalAsync(request.AnimalId, null, cancellationToken)
            : null;

        return new AnimalFinancialSnapshotDto(
            request.AnimalId,
            Math.Round(costToDate, 2),
            RunningCost(advice, ShortHorizonDays),
            RunningCost(advice, LongHorizonDays),
            Math.Round(currentValue, 2),
            Math.Round(currentValue - costToDate, 2));
    }

    /// <summary>Cost from today to day <paramref name="days"/>; today's cost per day × days when it cannot be projected.</summary>
    private static decimal RunningCost(SellOrKeepAdvice? advice, int days)
    {
        if (advice is null)
            return 0m;
        var points = advice.Projection;
        if (points.Count > days)
            return Math.Round(points[days].CostBdt - points[0].CostBdt, 2);
        return Math.Round(advice.DailyCostBdt * days, 2);
    }
}
