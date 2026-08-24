using Farm360.Application.Intelligence.Services;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Intelligence.Projections;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Features.Intelligence.Queries.GetAnimalFinancialSnapshot;

public sealed class GetAnimalFinancialSnapshotQueryHandler : IRequestHandler<GetAnimalFinancialSnapshotQuery, AnimalFinancialSnapshotDto>
{
    private readonly IAnimalRepository _animalRepository;
    private readonly IProjectionDefaultsResolver _defaultsResolver;

    public GetAnimalFinancialSnapshotQueryHandler(
        IAnimalRepository animalRepository,
        IProjectionDefaultsResolver defaultsResolver)
    {
        _animalRepository = animalRepository;
        _defaultsResolver = defaultsResolver;
    }

    public async Task<AnimalFinancialSnapshotDto> Handle(GetAnimalFinancialSnapshotQuery request, CancellationToken cancellationToken)
    {
        var animal = await _animalRepository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Farm360.Domain.Livestock.Animal), request.AnimalId);

        var defaults = await _defaultsResolver.ResolveDefaultsAsync(request.AnimalId, cancellationToken);
        var daysOnFarm = (int)(DateTime.UtcNow.Date - animal.CreatedAtUtc.Date).TotalDays;
        if (daysOnFarm <= 0) daysOnFarm = 1;

        var inputs = new FatteningProjectionInputs(
            defaults.StartingLiveWeightKg.Value,
            defaults.PurchasePriceBdt.Value,
            defaults.CurrentMeatPriceBdtPerKg.Value,
            defaults.InitialMeatYieldRatio.Value,
            defaults.DailyLiveWeightGainKg.Value,
            defaults.MeatYieldOnDailyGainRatio.Value,
            defaults.DailyFeedQuantityKgAtStart.Value,
            defaults.FeedPriceBdtPerKg.Value,
            defaults.DailyGrassCostBdt.Value,
            defaults.DailyOtherCostBdt.Value,
            defaults.MonthlyLaborCostBdt.Value,
            Math.Max(daysOnFarm + 60, defaults.FatteningPeriodDays.Value) // Ensure projection covers up to 60 days ahead
        );

        var result = FatteningProjectionCalculator.Calculate(inputs);

        var currentDayResult = result.Days.FirstOrDefault(d => d.Day == daysOnFarm) ?? result.Days[^1];
        
        var target30Days = daysOnFarm + 30;
        var day30Result = result.Days.FirstOrDefault(d => d.Day == target30Days) ?? result.Days[^1];
        var projected30DayCost = day30Result.CumulativeCostBdt - currentDayResult.CumulativeCostBdt;

        var target60Days = daysOnFarm + 60;
        var day60Result = result.Days.FirstOrDefault(d => d.Day == target60Days) ?? result.Days[^1];
        var projected60DayCost = day60Result.CumulativeCostBdt - currentDayResult.CumulativeCostBdt;

        return new AnimalFinancialSnapshotDto(
            request.AnimalId,
            currentDayResult.TotalInvestmentBdt,
            projected30DayCost,
            projected60DayCost,
            currentDayResult.MeatValueBdt, // Using meat value as estimated market value
            currentDayResult.ProfitLossBdt);
    }
}

