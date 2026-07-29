using Farm360.Domain.Common;
using System.Collections.Generic;

namespace Farm360.Domain.Intelligence.ValueObjects;

public sealed class CostProjection : BaseValueObject
{
    public decimal TotalCurrentInvestmentBdt { get; private set; }
    public decimal Projected30DayFeedCostBdt { get; private set; }
    public decimal Projected60DayFeedCostBdt { get; private set; }

    private CostProjection() { } // EF Core

    public CostProjection(
        decimal totalCurrentInvestmentBdt,
        decimal projected30DayFeedCostBdt,
        decimal projected60DayFeedCostBdt)
    {
        TotalCurrentInvestmentBdt = totalCurrentInvestmentBdt;
        Projected30DayFeedCostBdt = projected30DayFeedCostBdt;
        Projected60DayFeedCostBdt = projected60DayFeedCostBdt;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return TotalCurrentInvestmentBdt;
        yield return Projected30DayFeedCostBdt;
        yield return Projected60DayFeedCostBdt;
    }
}
