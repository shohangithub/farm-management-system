using Farm360.Application.Common.Interfaces;
using Farm360.Application.Intelligence.Interfaces;
using Farm360.Application.Intelligence.SellOrKeep;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Services;

/// <summary>
/// "What if I sell on this date?" — read off the Sell-or-keep projection (the same engine, price and
/// actual cost run-rate as the "Sell or keep?" card and the Fattening plan), extended to the date.
/// </summary>
public class SimulationEngine : ISimulationEngine
{
    private readonly IFarmClock _clock;
    private readonly IAnimalSellOrKeepAdvisor _advisor;

    public SimulationEngine(IAnimalSellOrKeepAdvisor advisor, IFarmClock? clock = null)
    {
        _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;
        _advisor = advisor;
    }

    public async Task<SaleSimulationResult?> SimulateSaleAsync(Guid animalId, DateOnly targetDate, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var daysFromNow = targetDate.DayNumber - today.DayNumber;
        if (daysFromNow <= 0 || daysFromNow > SellOrKeepOverrides.MaxHorizonDays)
            return null;

        // Null for an animal that has left the farm: a realised result, not a future sale.
        var plan = await _advisor.PlanAnimalAsync(animalId, new SellOrKeepOverrides { HorizonDays = Math.Max(daysFromNow, _advisor.Assumptions.HorizonDays) }, today, cancellationToken);
        var points = plan?.Plan.Projection;
        if (points is null || points.Count <= daysFromNow)
            return null;

        var start = points[0];
        var target = points[daysFromNow];
        return new SaleSimulationResult(
            AnimalId: animalId,
            TargetDate: targetDate,
            DaysFromNow: daysFromNow,
            ProjectedWeightKg: target.WeightKg,
            EstimatedSalePriceBdt: target.ValueBdt,
            // Running cost from today to the sale date: feed + labour & overhead + health.
            ProjectedAdditionalCostBdt: target.CostBdt - start.CostBdt,
            // Cost to date + the running cost above.
            ProjectedTotalCostBdt: target.CostBdt,
            ProjectedProfitMarginBdt: target.ProfitBdt);
    }
}
