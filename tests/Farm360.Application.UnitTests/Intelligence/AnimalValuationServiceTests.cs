using System;
using System.Globalization;
using Farm360.Application.Intelligence.Services;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Intelligence;

public class AnimalValuationServiceTests
{
    private readonly TestRulesProvider _rules = new();
    private readonly AnimalValuationService _sut;

    public AnimalValuationServiceTests()
    {
        _sut = new AnimalValuationService(_rules);
    }

    [Fact]
    public void MeatYieldPolicy_rate_is_meat_price_times_dressing()
    {
        _rules.Rules.Valuation = AnimalValuationPolicy.MeatYieldMarket;
        _rules.Rules.DefaultMeatPricePerKgBdt = 680m;
        _rules.Rules.DefaultDressingPercentage = 0.5m;

        _sut.LiveWeightRatePerKgBdt.Should().Be(340m);
        _sut.ValueAtWeight(300m, 1m).Should().Be(102_000m);
    }

    [Fact]
    public void LiveWeightPolicy_uses_live_rate_and_override_wins()
    {
        _rules.Rules.Valuation = AnimalValuationPolicy.LiveWeightMarket;
        _rules.Rules.DefaultLiveWeightPricePerKgBdt = 420m;

        _sut.LiveWeightRatePerKgBdt.Should().Be(420m);
        _sut.ValueAtWeight(100m, 0m).Should().Be(42_000m);
        _sut.ValueAtWeight(100m, 0m, ratePerKgOverride: 500m).Should().Be(50_000m);
    }

    [Fact]
    public void CostBasisPolicy_values_at_cost()
    {
        _rules.Rules.Valuation = AnimalValuationPolicy.CostBasis;
        _sut.ValueAtWeight(300m, 77_000m).Should().Be(77_000m);
    }

    [Fact]
    public void Misconfigured_rules_fall_back_to_rule_defaults()
    {
        _rules.Rules.Valuation = AnimalValuationPolicy.MeatYieldMarket;
        _rules.Rules.DefaultMeatPricePerKgBdt = 0m;
        _rules.Rules.DefaultDressingPercentage = 3m;

        var defaults = new FarmBusinessRules();
        _sut.LiveWeightRatePerKgBdt.Should().Be(Math.Round(defaults.DefaultMeatPricePerKgBdt * defaults.DefaultDressingPercentage, 2));
    }

    [Fact]
    public void CostToDate_prefers_ledger_acquisition_and_sums_buckets()
    {
        var animal = TestAnimals.Create(50_000m, 250m);
        var ledger = AnimalCostLedger.Create(TestAnimals.TenantId, animal.Id, TestAnimals.FarmId, 55_000m);
        ledger.UpdateFeedCost(10_000m);
        ledger.RecordCost(TransactionCategory.VeterinaryCost, 1_000m);
        ledger.UpdateLaborCost(2_000m);
        ledger.UpdateOverheadCost(500m);

        var cost = _sut.CostToDate(animal, ledger);

        cost.PurchaseBdt.Should().Be(55_000m);
        cost.TotalBdt.Should().Be(ledger.TotalCostBdt).And.Be(68_500m);
    }

    [Fact]
    public void CostToDate_without_ledger_falls_back_to_purchase_price_and_feed()
    {
        var animal = TestAnimals.Create(50_000m, 250m);
        _sut.CostToDate(animal, null, fallbackFeedCostBdt: 4_000m).TotalBdt.Should().Be(54_000m);
    }

    [Fact]
    public void CurrentValue_depends_on_status()
    {
        _rules.Rules.Valuation = AnimalValuationPolicy.MeatYieldMarket;
        var active = TestAnimals.Create(50_000m, 300m);
        _sut.CurrentValue(active, null, 60_000m).Should().Be(300m * _sut.LiveWeightRatePerKgBdt);

        var sold = TestAnimals.Create(50_000m, 300m, "T-2");
        var ledger = AnimalCostLedger.Create(TestAnimals.TenantId, sold.Id, TestAnimals.FarmId, 50_000m);
        ledger.RecordSaleRevenue(95_000m);
        sold.Sell(90_000m, new DateOnly(2025, 7, 1), Guid.NewGuid(), null, null);
        _sut.CurrentValue(sold, ledger, 60_000m).Should().Be(95_000m, "realised revenue from the ledger wins");
        _sut.IsOnHand(sold).Should().BeFalse();
    }

    [Theory]
    [InlineData("2024-01-31", "2024-02-29", 1, "1 mo")]
    [InlineData("2024-01-15", "2025-01-14", 11, "11 mo")]
    [InlineData("2024-01-15", "2026-01-10", 23, "1y 11m")]
    [InlineData("2024-01-15", "2026-01-15", 24, "2y")]
    [InlineData("2024-01-15", "2024-01-25", 0, "10 d")]
    public void AnimalAge_counts_calendar_months(string dob, string asOf, int months, string label)
    {
        var d = DateOnly.Parse(dob, CultureInfo.InvariantCulture);
        var a = DateOnly.Parse(asOf, CultureInfo.InvariantCulture);
        AnimalAge.CompletedMonths(d, a).Should().Be(months);
        AnimalAge.Format(d, a).Should().Be(label);
    }
}
