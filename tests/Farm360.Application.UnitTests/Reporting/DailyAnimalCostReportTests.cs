using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Reporting.Definitions.Finance;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Health.ValueObjects;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Reporting;

public class DailyAnimalCostReportTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 16);

    private readonly IAnimalFeedAllocationRepository _feed = Substitute.For<IAnimalFeedAllocationRepository>();
    private readonly IMedicalTreatmentRepository _treatments = Substitute.For<IMedicalTreatmentRepository>();
    private readonly IVaccinationRepository _vaccinations = Substitute.For<IVaccinationRepository>();
    private readonly IFinancialTransactionRepository _transactions = Substitute.For<IFinancialTransactionRepository>();
    private readonly IAnimalOverheadAllocationRepository _overheads = Substitute.For<IAnimalOverheadAllocationRepository>();
    private readonly IInventoryItemRepository _items = Substitute.For<IInventoryItemRepository>();
    private readonly IAnimalRepository _animals = Substitute.For<IAnimalRepository>();
    private readonly IAnimalBatchRepository _batches = Substitute.For<IAnimalBatchRepository>();

    private readonly List<AnimalFeedDay> _feedDays = [];
    private readonly List<MedicalTreatment> _treatmentRows = [];
    private readonly List<AnimalOverheadAllocation> _allocations = [];

    public DailyAnimalCostReportTests()
    {
        _feed.GetDailyEatenByFarmAsync(FarmId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => _feedDays.Where(f => f.Date >= ci.ArgAt<DateOnly>(1) && f.Date <= ci.ArgAt<DateOnly>(2)).ToList());
        _treatments.GetByFarmStartedBetweenAsync(FarmId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => _treatmentRows.Where(t => t.StartDate >= ci.ArgAt<DateOnly>(1) && t.StartDate <= ci.ArgAt<DateOnly>(2)).ToList());
        _vaccinations.GetAdministeredByFarmAsync(FarmId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<(VaccinationEvent, string)>());
        _transactions.GetManualVetPostingsAsync(FarmId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<FinancialTransaction>());
        _overheads.GetByFarmAsync(FarmId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => _allocations.Where(a => a.PeriodEnd >= ci.ArgAt<DateOnly>(1) && a.PeriodStart <= ci.ArgAt<DateOnly>(2)).ToList());
    }

    private DailyAnimalCostCalculator Calculator() =>
        new(_feed, _treatments, _vaccinations, _transactions, _overheads, _items);

    [Fact]
    public async Task Monthly_labour_is_spread_over_the_days_the_animal_was_on_the_farm_and_adds_up()
    {
        var animal = NewAnimal("A-1");
        animal.Sell(80_000m, new DateOnly(2026, 10, 10), Guid.NewGuid(), "Buyer", 300m);
        _allocations.Add(Labour(animal, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 100m));

        var costs = await Calculator().CalculateAsync(
            FarmId, new(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)),
            new Dictionary<Guid, Animal> { [animal.Id] = animal }, null, CancellationToken.None);

        costs.Should().HaveCount(10, "the animal was sold on 10 October");
        costs.Max(c => c.Date).Should().Be(new DateOnly(2026, 10, 10));
        costs.Sum(c => c.LabourBdt).Should().Be(100m, "the rounding remainder goes on the last day");
        costs[0].LabourBdt.Should().Be(10m);
    }

    [Fact]
    public async Task Treatment_cost_lands_on_its_start_date_when_the_medicine_has_no_stock_value()
    {
        var animal = NewAnimal("A-2");
        var itemId = Guid.NewGuid();
        _items.GetByIdAsync(itemId, Arg.Any<CancellationToken>()).Returns((Farm360.Domain.Inventory.InventoryItem?)null);
        _treatmentRows.Add(MedicalTreatment.LogTreatment(TenantId, animal.Id, "Fever", "Oxytet", Dosage.Create(10, "ml"),
            WithdrawalPeriod.None, new DateOnly(2026, 11, 3), null, 750m, null, null, itemId, 10m));

        var costs = await Calculator().CalculateAsync(
            FarmId, new(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 16)),
            new Dictionary<Guid, Animal> { [animal.Id] = animal }, null, CancellationToken.None);

        costs.Should().ContainSingle().Which.Should().Match<DailyAnimalCost>(c => c.Date == new DateOnly(2026, 11, 3) && c.VetBdt == 750m);
    }

    [Fact]
    public async Task Animal_report_lists_every_day_to_today_with_the_running_cost_since_arrival()
    {
        var animal = NewAnimal("A-3");
        _animals.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);
        _feedDays.Add(new AnimalFeedDay(animal.Id, new DateOnly(2026, 10, 20), 8m, 400m)); // before the period
        _feedDays.Add(new AnimalFeedDay(animal.Id, new DateOnly(2026, 11, 2), 8m, 420m));
        _feedDays.Add(new AnimalFeedDay(animal.Id, new DateOnly(2026, 11, 5), 9m, 450m));

        var report = new AnimalDailyCostReportDefinition(_animals, Calculator());
        var data = await ReportingTestKit.RunAsync(report, ReportingTestKit.Context(
            new Dictionary<string, string?> { ["animalId"] = animal.Id.ToString(), ["period"] = "current-month" }, today: Today));

        data.TotalRowCount.Should().Be(16, "1 to 16 November; the rest of the month has not happened yet");
        var rows = data.Rows.ToList();
        Cell(rows[0], 7).Should().Be(400m, "the running cost starts with what the animal cost before the period");
        Cell(rows[1], 6).Should().Be(420m);
        Cell(rows[^1], 7).Should().Be(1_270m);
    }

    [Fact]
    public async Task Herd_report_shows_head_count_and_cost_per_head_each_day()
    {
        var a = NewAnimal("H-1");
        var b = NewAnimal("H-2");
        b.Sell(70_000m, new DateOnly(2026, 11, 2), Guid.NewGuid(), "Buyer", 280m);
        _animals.GetPresentDuringPeriodAsync(FarmId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Animal> { a, b });
        _feedDays.Add(new AnimalFeedDay(a.Id, new DateOnly(2026, 11, 1), 8m, 400m));
        _feedDays.Add(new AnimalFeedDay(b.Id, new DateOnly(2026, 11, 1), 8m, 200m));
        _feedDays.Add(new AnimalFeedDay(a.Id, new DateOnly(2026, 11, 3), 8m, 400m));

        var report = new HerdDailyCostReportDefinition(new DailyAnimalCostScope(_animals, _batches), Calculator());
        var data = await ReportingTestKit.RunAsync(report, ReportingTestKit.Context(
            new Dictionary<string, string?> { ["farmId"] = FarmId.ToString(), ["period"] = "2026-11-01..2026-11-03" }, today: Today));

        var rows = data.Rows.ToList();
        rows.Should().HaveCount(3);
        Cell(rows[0], 1).Should().Be(2m);
        Cell(rows[0], 8).Should().Be(300m);
        Cell(rows[2], 1).Should().Be(1m, "H-2 was sold on 2 November");
        Cell(rows[2], 8).Should().Be(400m);
    }

    private static decimal Cell(Farm360.Application.Reporting.Model.ReportRow row, int index) =>
        Convert.ToDecimal(row.Cells[index].V, System.Globalization.CultureInfo.InvariantCulture);

    private static AnimalOverheadAllocation Labour(Animal animal, DateOnly from, DateOnly to, decimal amount) =>
        AnimalOverheadAllocation.Create(TenantId, animal.Id, FarmId, Guid.NewGuid(), TransactionCategory.LaborCost,
            OverheadBucket.Labour, from, to, amount, OverheadAllocationMethod.PerHeadDay, 10m, 0m, 0.1m, 10);

    private static Animal NewAnimal(string tag) =>
        Animal.Create(TenantId, FarmId, AnimalTag.Create(tag, TagType.EarTag), AnimalSpecies.CattleBeef,
            Guid.NewGuid(), AnimalSex.Male, new DateOnly(2025, 1, 1), AcquisitionType.Purchased, new DateOnly(2026, 1, 1), 50_000m, null);
}
