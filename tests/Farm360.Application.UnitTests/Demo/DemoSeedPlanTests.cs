using Farm360.Application.Demo;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Livestock.Enums;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Farm360.Application.UnitTests.Demo;

/// <summary>The demo farm's content: counts, dates relative to today, and the profit/loss story.</summary>
public sealed class DemoSeedPlanTests
{
    private static readonly DateOnly Today = new(2026, 11, 25);
    private readonly DemoSeedPlan _plan = DemoSeedPlan.Create(Today);

    private IEnumerable<DemoAnimalSpec> Cattle => _plan.Animals.Where(a => a.Species != AnimalSpecies.Goat);

    [Fact]
    public void Structure_HasTwoShedsFourPensTwoBatchesAndTheThreeCattleBreeds()
    {
        _plan.Sheds.Should().HaveCount(2);
        _plan.Pens.Should().HaveCount(4);
        _plan.Batches.Select(b => b.Name).Should().Contain(["Eid-ul-Adha 2027 Fattening", "Winter Batch"]);
        _plan.Breeds.Select(b => b.Name).Should().Contain(["Local (Deshi)", "Sahiwal Cross", "Brahman Cross"]);
        _plan.Breeds.Should().OnlyContain(b => b.StandardAdgMin > 0 && b.StandardAdgMax > b.StandardAdgMin, "every breed has an ADG target");
    }

    [Fact]
    public void Animals_AreAboutTwentyFourCattleBoughtFor55kTo95kPlusFourGoats()
    {
        Cattle.Should().HaveCount(24);
        _plan.Animals.Count(a => a.Species == AnimalSpecies.Goat).Should().Be(4);
        Cattle.Should().OnlyContain(a => a.PriceBdt >= 55_000m && a.PriceBdt <= 95_000m);
        _plan.Animals.Select(a => a.Tag).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Dates_AreRelativeToToday_TheFarmStartedAbout150DaysAgo()
    {
        _plan.StartDate.Should().Be(Today.AddDays(-150));
        Cattle.Should().OnlyContain(a => a.PurchaseDate >= Today.AddDays(-150) && a.PurchaseDate <= Today.AddDays(-120));
        _plan.FeedingStartDate.Should().Be(Today.AddDays(-60));
        _plan.IndividualPlansStartDate.Should().BeAfter(_plan.FeedingStartDate).And.BeBefore(Today);

        var allDates = _plan.Weighings.Select(w => w.Date)
            .Concat(_plan.Purchases.Select(p => p.Date))
            .Concat(_plan.Purchases.SelectMany(p => p.LaterPayments).Select(p => p.Date))
            .Concat(_plan.Sales.Select(s => s.Date))
            .Concat(_plan.Sales.SelectMany(s => s.Collections).Select(c => c.Date))
            .Concat(_plan.Expenses.Select(e => e.Date))
            .Concat(_plan.Treatments.Select(t => t.Date))
            .Concat([_plan.Death.Date, _plan.Incident.Date, _plan.VetVisit.Date, _plan.Loan.Date, _plan.Investor.Date]);
        allDates.Should().OnlyContain(d => d <= Today && d >= _plan.BooksStartDate, "nothing is dated in the future or before the books start");
    }

    [Fact]
    public void Plan_ShiftsWithToday()
    {
        var later = DemoSeedPlan.Create(Today.AddDays(30));

        later.StartDate.Should().Be(_plan.StartDate.AddDays(30));
        later.Sales.Select(s => s.Date).Should().Equal(_plan.Sales.Select(s => s.Date.AddDays(30)));
        later.Animals.Select(a => a.PurchaseDate).Should().Equal(_plan.Animals.Select(a => a.PurchaseDate.AddDays(30)));
    }

    [Fact]
    public void Weighings_AreEveryFourteenDays_WithRealisticGainAndAFewSlowGrowers()
    {
        foreach (var animal in _plan.Animals)
        {
            var days = _plan.Weighings.Where(w => w.Tag == animal.Tag).Select(w => w.Date).OrderBy(d => d).ToList();
            days.First().Should().Be(animal.PurchaseDate);
            var sale = _plan.Sales.FirstOrDefault(s => s.Tag == animal.Tag);
            var regular = sale is null ? days : days.Where(d => d != sale.Date).ToList();
            regular.Zip(regular.Skip(1), (a, b) => b.DayNumber - a.DayNumber).Should().OnlyContain(gap => gap == 14);
            if (_plan.ExitDate(animal.Tag) is { } exit)
                days.Should().OnlyContain(d => d <= exit);
        }

        var normal = Cattle.Where(a => !a.SlowGrower).ToList();
        normal.Should().OnlyContain(a => a.AdgKgPerDay >= 0.5m && a.AdgKgPerDay <= 0.9m);
        Cattle.Count(a => a.SlowGrower).Should().BeInRange(2, 4);
    }

    [Fact]
    public void Sales_AreFourInTheLastThirtyDays_ThreeAtAProfitAndOneAtALoss()
    {
        _plan.Sales.Should().HaveCount(4);
        _plan.Sales.Should().OnlyContain(s => s.Date > Today.AddDays(-30) && s.Date <= Today);

        decimal Margin(DemoSaleSpec s) => s.PriceBdt - _plan.Animal(s.Tag).PriceBdt;

        // Profitable even after ~5 months of feed, labour and overhead (well over 25,000 BDT a head).
        _plan.Sales.Count(s => Margin(s) > 30_000m).Should().Be(3);
        _plan.Sales.Count(s => Margin(s) < 0m).Should().Be(1);
    }

    [Fact]
    public void OneSale_IsOnCredit_WithAPartialCollection()
    {
        var credit = _plan.Sales.Where(s => s.OnCredit).Should().ContainSingle().Subject;

        credit.CreditPaidNowBdt.Should().BeGreaterThan(0m).And.BeLessThan(credit.PriceBdt);
        var collected = credit.CreditPaidNowBdt!.Value + credit.Collections.Sum(c => c.AmountBdt);
        collected.Should().BeLessThan(credit.PriceBdt, "part of the receivable is still outstanding");
        credit.Collections.Should().OnlyContain(c => c.Date > credit.Date && c.Date <= Today);
    }

    [Fact]
    public void OneAnimal_DiedOfDisease_AndNoAnimalIsSoldInsideAMeatWithdrawalPeriod()
    {
        _plan.Death.Cause.Should().Be(CauseOfDeath.Disease);
        _plan.Sales.Should().NotContain(s => s.Tag == _plan.Death.Tag);

        foreach (var sale in _plan.Sales)
        {
            _plan.Treatments.Should().NotContain(t => t.Tag == sale.Tag && t.Date.AddDays(t.MeatWithdrawalDays) >= sale.Date);
        }
    }

    [Fact]
    public void Inventory_HasTheFeedsMedicinesAndCreditPurchasesWithPartPayments()
    {
        var names = _plan.Items.Select(i => i.Name).ToList();
        foreach (var expected in new[] { "Wheat Bran", "Rice Polish", "Khesari", "Mustard Oil Cake", "Maize", "Straw", "Green Grass", "Molasses", "Salt", "DCP", "Oxytetracycline", "Ivermectin", "FMD", "LSD" })
            names.Should().Contain(n => n.Contains(expected, StringComparison.OrdinalIgnoreCase), $"the demo stocks {expected}");

        _plan.Suppliers.Should().NotBeEmpty();
        _plan.Purchases.Where(p => p.OnCredit).Should().NotBeEmpty()
            .And.OnlyContain(p => p.CreditPaidNowBdt < p.TotalBdt);
        _plan.Purchases.Where(p => p.OnCredit).SelectMany(p => p.LaterPayments).Should().NotBeEmpty();
        _plan.Recipes.Should().HaveCount(2).And.OnlyContain(r => r.Ingredients.Sum(i => i.Percent) == 100m);
    }

    [Fact]
    public void FeedPurchases_CoverTheHeaviestRationsForTheWholeFeedingPeriod()
    {
        // Confirming feed fails when stock is short, so purchases must cover the heaviest chart band.
        var maxConcentratePerDay = Cattle.Count() * _plan.Charts[0].Bands[^1].ConcentrateKg + 4 * _plan.Charts[1].Bands[^1].ConcentrateKg;
        var wheatBranNeeded = maxConcentratePerDay * 0.30m * DemoSeedPlan.FeedingHistoryDays;
        _plan.Purchases.Where(p => p.ItemKey == "wheatbran").Sum(p => p.Quantity).Should().BeGreaterThan(wheatBranNeeded);
    }

    [Fact]
    public void Finance_HasMonthlyExpensesOverheadPeriodsALoanRepaymentAndAnInvestor()
    {
        _plan.Expenses.Select(e => e.Category).Distinct().Should().Contain(["LaborCost", "Utilities", "Transport"]);
        _plan.OverheadPeriods.Should().NotBeEmpty().And.OnlyContain(p => p.From <= p.To && p.To < Today);
        _plan.Loan.Repayments.Should().ContainSingle();
        _plan.Investor.AmountBdt.Should().BePositive();
        _plan.OpeningCashBdt.Should().Be(_plan.OpeningOwnerCapitalBdt, "the opening balance sheet balances");
    }

    [Fact]
    public void FeedingOutcomes_AreMostlyConfirmed_WithAFewAdjustedOrSkipped()
    {
        var outcomes = new List<DemoFeedingOutcome>();
        for (var day = _plan.FeedingStartDate; day < Today; day = day.AddDays(1))
        {
            foreach (var animal in _plan.Animals)
            {
                outcomes.Add(_plan.FeedingOutcomeFor(day, animal.Tag, 0));
                outcomes.Add(_plan.FeedingOutcomeFor(day, animal.Tag, 1));
            }
        }

        outcomes.Count(o => o.Action == DemoFeedingAction.Confirm).Should().BeGreaterThan(outcomes.Count * 9 / 10);
        outcomes.Should().Contain(o => o.Action == DemoFeedingAction.Adjust);
        outcomes.Should().Contain(o => o.Action == DemoFeedingAction.Skip);
        outcomes.Where(o => o.Action == DemoFeedingAction.Adjust).Should().OnlyContain(o => o.Factor > 0.8m && o.Factor < 1m);
    }
}
