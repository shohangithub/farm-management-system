using Farm360.Application.Intelligence.Services;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Intelligence.Projections;
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
/// valuation policy. The forward figures come from a projection that starts today (latest
/// weight, day-0 investment = cost to date), so day 30's cumulative cost <i>is</i> the next
/// 30 days' running cost.
/// </remarks>
public sealed class GetAnimalFinancialSnapshotQueryHandler : IRequestHandler<GetAnimalFinancialSnapshotQuery, AnimalFinancialSnapshotDto>
{
    private const int ShortHorizonDays = 30;
    private const int LongHorizonDays = 60;

    private readonly IAnimalRepository _animalRepository;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IProjectionDefaultsResolver _defaultsResolver;
    private readonly IAnimalValuationService _valuation;

    public GetAnimalFinancialSnapshotQueryHandler(
        IAnimalRepository animalRepository,
        IAnimalCostLedgerRepository ledgerRepository,
        IProjectionDefaultsResolver defaultsResolver,
        IAnimalValuationService valuation)
    {
        _animalRepository = animalRepository;
        _ledgerRepository = ledgerRepository;
        _defaultsResolver = defaultsResolver;
        _valuation = valuation;
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
        var currentValue = _valuation.CurrentValue(animal, ledger, costToDate);

        // Forward: a projection starting today, long enough to reach day 60.
        var inputs = ProjectionInputsFactory.FromToday(defaults, Math.Max(LongHorizonDays, defaults.FatteningPeriodDays.Value));
        var result = FatteningProjectionCalculator.Calculate(inputs);

        var day30 = result.Days.FirstOrDefault(d => d.Day == ShortHorizonDays) ?? result.Days[^1];
        var day60 = result.Days.FirstOrDefault(d => d.Day == LongHorizonDays) ?? result.Days[^1];

        // Sold or dead animals incur no further running cost.
        var onHand = _valuation.IsOnHand(animal);

        return new AnimalFinancialSnapshotDto(
            request.AnimalId,
            Math.Round(costToDate, 2),
            onHand ? Math.Round(day30.CumulativeCostBdt, 2) : 0m,
            onHand ? Math.Round(day60.CumulativeCostBdt, 2) : 0m,
            Math.Round(currentValue, 2),
            Math.Round(currentValue - costToDate, 2));
    }
}
