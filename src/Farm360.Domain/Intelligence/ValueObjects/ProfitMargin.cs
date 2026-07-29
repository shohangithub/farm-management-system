using Farm360.Domain.Common;
using System.Collections.Generic;

namespace Farm360.Domain.Intelligence.ValueObjects;

public sealed class ProfitMargin : BaseValueObject
{
    public decimal ExpectedRevenueBdt { get; private set; }
    public decimal TotalCostBdt { get; private set; }
    public decimal NetProfitBdt { get; private set; }
    public decimal ReturnOnInvestmentPercentage { get; private set; }

    private ProfitMargin() { } // EF Core

    public ProfitMargin(
        decimal expectedRevenueBdt,
        decimal totalCostBdt,
        decimal netProfitBdt,
        decimal returnOnInvestmentPercentage)
    {
        ExpectedRevenueBdt = expectedRevenueBdt;
        TotalCostBdt = totalCostBdt;
        NetProfitBdt = netProfitBdt;
        ReturnOnInvestmentPercentage = returnOnInvestmentPercentage;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ExpectedRevenueBdt;
        yield return TotalCostBdt;
        yield return NetProfitBdt;
        yield return ReturnOnInvestmentPercentage;
    }
}
