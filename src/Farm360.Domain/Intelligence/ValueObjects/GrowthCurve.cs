using Farm360.Domain.Common;
using System.Collections.Generic;

namespace Farm360.Domain.Intelligence.ValueObjects;

public sealed class GrowthCurve : BaseValueObject
{
    public decimal CurrentWeightKg { get; private set; }
    public decimal Projected30DayWeightKg { get; private set; }
    public decimal Projected60DayWeightKg { get; private set; }
    public decimal Projected90DayWeightKg { get; private set; }
    public decimal CurrentAdgKg { get; private set; }
    
    private GrowthCurve() { } // EF Core
    
    public GrowthCurve(
        decimal currentWeightKg,
        decimal projected30DayWeightKg,
        decimal projected60DayWeightKg,
        decimal projected90DayWeightKg,
        decimal currentAdgKg)
    {
        CurrentWeightKg = currentWeightKg;
        Projected30DayWeightKg = projected30DayWeightKg;
        Projected60DayWeightKg = projected60DayWeightKg;
        Projected90DayWeightKg = projected90DayWeightKg;
        CurrentAdgKg = currentAdgKg;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return CurrentWeightKg;
        yield return Projected30DayWeightKg;
        yield return Projected60DayWeightKg;
        yield return Projected90DayWeightKg;
        yield return CurrentAdgKg;
    }
}
