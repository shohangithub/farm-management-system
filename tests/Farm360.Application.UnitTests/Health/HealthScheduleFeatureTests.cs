using Farm360.Application.Common.Interfaces;
using Farm360.Application.Health.EventHandlers;
using Farm360.Application.Health.Programmes;
using Farm360.Application.Health.Tasks;
using Farm360.Application.Livestock.Commands;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Events;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using Farm360.Domain.Tenancy;
using Farm360.Domain.Tenancy.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Health;

/// <summary>
/// Recurring health tasks (late dose, skip, exit and undo), automatic programmes for purchased
/// animals and the ready-made 108-day market-cattle template.
/// </summary>
public sealed class HealthScheduleFeatureTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly Guid Farm = Guid.NewGuid();

    private static Animal NewAnimal(AcquisitionType type = AcquisitionType.Purchased) =>
        Animal.Create(TenantGuid, Farm, AnimalTag.Create("B-04", TagType.EarTag), AnimalSpecies.CattleBeef, Guid.NewGuid(),
            AnimalSex.Male, Today.AddYears(-2), type, Today.AddDays(-200), 60_000m, null);

    private static VaccinationEvent RecurringTask(Animal animal, DateOnly date, int repeatDays = 90)
    {
        var protocol = VaccinationProtocol.Create(TenantGuid, "Programme", AnimalSpecies.CattleBeef, null);
        var step = protocol.AddStep("Dewormer again", 93, "Dewormer", "", options: new ProtocolStepOptions(
            HealthTaskKind.Medicine, repeatDays, new WeightDoseRule(1m, 150m, DoseRounding.Half, unit: "tablet")));
        return VaccinationEvent.ScheduleFromStep(TenantGuid, animal.Id, protocol.Id, step, date, null);
    }

    private static Mock<IFarmClock> Clock()
    {
        var clock = new Mock<IFarmClock>();
        clock.Setup(c => c.Today).Returns(Today);
        clock.Setup(c => c.TodayForFarm(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns(Today);
        clock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);
        return clock;
    }

    private static Mock<IFarmBusinessRulesProvider> Rules()
    {
        var rules = new Mock<IFarmBusinessRulesProvider>();
        var values = new FarmBusinessRules { FallbackAnimalWeightKg = 150m };
        rules.Setup(r => r.Current).Returns(values);
        rules.Setup(r => r.For(It.IsAny<Guid>(), It.IsAny<Guid?>())).Returns(values);
        return rules;
    }

    private sealed record Harness(
        HealthTaskCompletionService Service,
        Mock<IVaccinationRepository> Repo,
        List<VaccinationEvent> Added,
        Mock<IPublisher> Publisher);

    private static Harness Completion(Animal animal, params VaccinationEvent[] events)
    {
        var all = events.ToList();
        var added = new List<VaccinationEvent>();
        var repo = new Mock<IVaccinationRepository>();
        foreach (var e in events)
            repo.Setup(r => r.GetEventByIdAsync(e.Id, It.IsAny<CancellationToken>())).ReturnsAsync(e);
        repo.Setup(r => r.GetEventsByAnimalIdAsync(animal.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => all.Concat(added).ToList());
        repo.Setup(r => r.AddEvent(It.IsAny<VaccinationEvent>())).Callback<VaccinationEvent>(added.Add);

        var animals = new Mock<IAnimalRepository>();
        animals.Setup(a => a.GetByIdAsync(animal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(animal);
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(Guid.NewGuid());
        var publisher = new Mock<IPublisher>();

        var service = new HealthTaskCompletionService(repo.Object, animals.Object, Rules().Object, user.Object,
            new Mock<IUnitOfWork>().Object, publisher.Object, Clock().Object);
        return new Harness(service, repo, added, publisher);
    }

    // ── Recurring steps ────────────────────────────────────────────────────────

    [Fact]
    public async Task LateAdministration_SchedulesTheNextDoseFromTheActualDate()
    {
        var animal = NewAnimal();
        var due = Today.AddDays(-10);
        var task = RecurringTask(animal, due);
        var h = Completion(animal, task);

        var givenOn = Today.AddDays(-3); // a week late
        await h.Service.CompleteAsync([task.Id], givenOn, null);

        task.Status.Should().Be(VaccinationStatus.Completed);
        h.Added.Should().ContainSingle();
        var next = h.Added[0];
        next.ScheduledDate.Should().Be(givenOn.AddDays(90));
        next.PreviousEventId.Should().Be(task.Id);
        next.DosageQuantity.Should().Be(1m, "an unweighed animal uses the farm's 150 kg fallback");
        next.DoseWeightEstimated.Should().BeTrue();
        h.Publisher.Verify(p => p.Publish(It.IsAny<Farm360.Domain.Health.Events.VaccinationAdministeredEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompletingTwice_DoesNotDuplicateTheNextDose()
    {
        var animal = NewAnimal();
        var task = RecurringTask(animal, Today);
        var h = Completion(animal, task);
        await h.Service.CompleteAsync([task.Id], Today, null);

        // A next dose already exists in the chain: no second one.
        HealthTaskScheduling.NextOccurrence(task, animal, h.Added, DoseWeight.For(null, 150m)).Should().BeNull();
    }

    [Fact]
    public async Task Skip_SchedulesTheNextDoseFromTheSkipDate()
    {
        var animal = NewAnimal();
        var task = RecurringTask(animal, Today.AddDays(-1), repeatDays: 30);
        var h = Completion(animal, task);

        await h.Service.SkipAsync(task.Id, Today, "Animal weak, vet advised to wait");

        task.Status.Should().Be(VaccinationStatus.Skipped);
        h.Added.Should().ContainSingle().Which.ScheduledDate.Should().Be(Today.AddDays(30));
    }

    [Fact]
    public async Task ChainStops_WhenTheAnimalHasLeftTheFarm()
    {
        var animal = NewAnimal();
        var task = RecurringTask(animal, Today.AddDays(-5));
        animal.Sell(90_000m, Today.AddDays(-1), Guid.NewGuid(), "Karim", null);
        var h = Completion(animal, task);

        // The dose was given before the sale and is entered late: allowed, but no next dose.
        await h.Service.CompleteAsync([task.Id], Today.AddDays(-4), null);

        task.Status.Should().Be(VaccinationStatus.Completed);
        h.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task UndoingTheExit_RestartsAStoppedChain_NotBeforeToday()
    {
        var animal = NewAnimal(); // back on the farm after the undo
        var lastDose = RecurringTask(animal, Today.AddDays(-200), repeatDays: 90);
        lastDose.RecordAdministration(Today.AddDays(-200), Guid.NewGuid(), today: Today);

        var added = new List<VaccinationEvent>();
        var repo = new Mock<IVaccinationRepository>();
        repo.Setup(r => r.GetEventsByAnimalIdAsync(animal.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaccinationEvent> { lastDose });
        repo.Setup(r => r.AddEvent(It.IsAny<VaccinationEvent>())).Callback<VaccinationEvent>(added.Add);
        var treatments = new Mock<IMedicalTreatmentRepository>();
        treatments.Setup(t => t.GetByAnimalIdAsync(animal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<MedicalTreatment>());
        var animals = new Mock<IAnimalRepository>();
        animals.Setup(a => a.GetByIdAsync(animal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(animal);
        var uow = new Mock<IUnitOfWork>();

        var handler = new AnimalExitReversedHealthHandler(repo.Object, treatments.Object, uow.Object, Clock().Object, animals.Object, Rules().Object);
        await handler.Handle(new AnimalExitReversedEvent(Guid.NewGuid(), DateTime.UtcNow, animal.Id, TenantGuid, Farm, true,
            Today.AddDays(-150), "mistake", Guid.NewGuid()), CancellationToken.None);

        added.Should().ContainSingle().Which.ScheduledDate.Should().Be(Today, "the due date had passed, so it is due now");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UndoingTheExit_DoesNotAddWhenThePendingDoseIsReopened()
    {
        var animal = NewAnimal();
        var given = RecurringTask(animal, Today.AddDays(-100));
        given.RecordAdministration(Today.AddDays(-100), Guid.NewGuid(), today: Today);
        var next = given.CreateNextOccurrence()!;
        var reason = AnimalExitHealthHandler.SoldReason(Today.AddDays(-20));
        next.CancelForAnimalExit(Today.AddDays(-20), reason);

        var added = new List<VaccinationEvent>();
        var repo = new Mock<IVaccinationRepository>();
        repo.Setup(r => r.GetEventsByAnimalIdAsync(animal.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaccinationEvent> { given, next });
        repo.Setup(r => r.AddEvent(It.IsAny<VaccinationEvent>())).Callback<VaccinationEvent>(added.Add);
        var treatments = new Mock<IMedicalTreatmentRepository>();
        treatments.Setup(t => t.GetByAnimalIdAsync(animal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<MedicalTreatment>());
        var animals = new Mock<IAnimalRepository>();
        animals.Setup(a => a.GetByIdAsync(animal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(animal);

        var handler = new AnimalExitReversedHealthHandler(repo.Object, treatments.Object, new Mock<IUnitOfWork>().Object,
            Clock().Object, animals.Object, Rules().Object);
        await handler.Handle(new AnimalExitReversedEvent(Guid.NewGuid(), DateTime.UtcNow, animal.Id, TenantGuid, Farm, true,
            Today.AddDays(-20), "mistake", Guid.NewGuid()), CancellationToken.None);

        next.Status.Should().Be(VaccinationStatus.Overdue);
        added.Should().BeEmpty();
    }

    // ── Automatic programme on purchase ────────────────────────────────────────

    private static (RegisterAnimalCommandHandler Handler, IPublisher Publisher) RegisterHandler()
    {
        var repo = Substitute.For<IAnimalRepository>();
        var tenants = Substitute.For<ITenantRepository>();
        var tenantService = Substitute.For<ITenantService>();
        tenantService.TenantId.Returns(TenantGuid);
        tenants.GetByIdAsync(TenantGuid, Arg.Any<CancellationToken>()).Returns(Farm360.Domain.Tenancy.Tenant.Create("Farm", "farm", SubscriptionTier.Standard));
        tenants.GetTenantUsageCountsAsync(TenantGuid, Arg.Any<CancellationToken>()).Returns((1, 1, 0));
        var user = Substitute.For<ICurrentUserService>();
        var publisher = Substitute.For<IPublisher>();
        return (new RegisterAnimalCommandHandler(repo, tenants, Substitute.For<IUnitOfWork>(), tenantService, user, publisher), publisher);
    }

    private static RegisterAnimalCommand Register(AcquisitionType type, bool optOut = false) => new(
        Farm, "B-07", TagType.EarTag, AnimalSpecies.CattleBeef, Guid.NewGuid(), AnimalSex.Male,
        new DateOnly(2024, 1, 1), type, new DateOnly(2026, 11, 20), 70_000m, null, null, null, optOut);

    [Fact]
    public async Task RegisteringAPurchasedAnimal_StartsTheAutomaticProgrammes()
    {
        var (handler, publisher) = RegisterHandler();
        var dto = await handler.Handle(Register(AcquisitionType.Purchased), CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<PurchasedAnimalRegisteredNotification>(n => n.AnimalId == dto.Id && n.AcquisitionDate == new DateOnly(2026, 11, 20)
                && n.Species == AnimalSpecies.CattleBeef && n.FarmId == Farm),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(AcquisitionType.BornOnFarm, false)]
    [InlineData(AcquisitionType.Purchased, true)]
    public async Task BornOnFarmOrOptedOut_StartsNoProgramme(AcquisitionType type, bool optOut)
    {
        var (handler, publisher) = RegisterHandler();
        await handler.Handle(Register(type, optOut), CancellationToken.None);

        await publisher.DidNotReceive().Publish(Arg.Any<PurchasedAnimalRegisteredNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutoStart_SchedulesEveryStepFromTheAcquisitionDate()
    {
        var animal = NewAnimal();
        var protocol = MarketCattleProgrammeTemplate.Build(TenantGuid);
        var repo = new Mock<IVaccinationRepository>();
        repo.Setup(r => r.GetAutoStartProtocolsAsync(AnimalSpecies.CattleBeef, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaccinationProtocol> { protocol });
        IEnumerable<VaccinationEvent>? scheduled = null;
        repo.Setup(r => r.AddEvents(It.IsAny<IEnumerable<VaccinationEvent>>())).Callback<IEnumerable<VaccinationEvent>>(e => scheduled = e.ToList());
        var animals = new Mock<IAnimalRepository>();
        animals.Setup(a => a.GetByIdAsync(animal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(animal);

        var handler = new AutoStartHealthProgrammesHandler(repo.Object, animals.Object, Rules().Object, new Mock<IUnitOfWork>().Object,
            NullLogger<AutoStartHealthProgrammesHandler>.Instance, Clock().Object);
        await handler.Handle(new PurchasedAnimalRegisteredNotification(TenantGuid, Farm, animal.Id, AnimalSpecies.CattleBeef, Today), CancellationToken.None);

        var events = scheduled!.ToList();
        events.Should().HaveCount(protocol.Steps.Count);
        events.Min(e => e.ScheduledDate).Should().Be(Today);
        events.Max(e => e.ScheduledDate).Should().Be(Today.AddDays(107));
        var fastVet = events.Single(e => e.VaccineName == "Fast Vet");
        fastVet.DosageQuantity.Should().Be(1m);
        fastVet.DoseWeightEstimated.Should().BeTrue();
    }

    [Fact]
    public async Task AutoStart_ForALateRegistration_LeavesOutOldOneOffSteps()
    {
        var animal = NewAnimal();
        var protocol = MarketCattleProgrammeTemplate.Build(TenantGuid);
        var repo = new Mock<IVaccinationRepository>();
        repo.Setup(r => r.GetAutoStartProtocolsAsync(AnimalSpecies.CattleBeef, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaccinationProtocol> { protocol });
        List<VaccinationEvent> scheduled = [];
        repo.Setup(r => r.AddEvents(It.IsAny<IEnumerable<VaccinationEvent>>())).Callback<IEnumerable<VaccinationEvent>>(e => scheduled = e.ToList());
        var animals = new Mock<IAnimalRepository>();
        animals.Setup(a => a.GetByIdAsync(animal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(animal);

        var handler = new AutoStartHealthProgrammesHandler(repo.Object, animals.Object, Rules().Object, new Mock<IUnitOfWork>().Object,
            NullLogger<AutoStartHealthProgrammesHandler>.Instance, Clock().Object);
        // Bought 30 days ago: days 1..23 are more than a week old.
        await handler.Handle(new PurchasedAnimalRegisteredNotification(TenantGuid, Farm, animal.Id, AnimalSpecies.CattleBeef, Today.AddDays(-30)), CancellationToken.None);

        scheduled.Should().NotBeEmpty();
        scheduled.Min(e => e.ScheduledDate).Should().BeOnOrAfter(Today.AddDays(-7));
        scheduled.Should().Contain(e => e.VaccineName == "LSD টিকা");
        scheduled.Should().NotContain(e => e.VaccineName == "Fast Vet");
    }

    // ── Template ───────────────────────────────────────────────────────────────

    [Fact]
    public void MarketCattleTemplate_FollowsTheVetsProgramme()
    {
        var protocol = MarketCattleProgrammeTemplate.Build(TenantGuid, repeatEveryDays: 120, autoStartForPurchased: true);

        protocol.Title.Should().Be("হাট থেকে কেনা গরু – ১০৮ দিনের কর্মসূচি");
        protocol.AutoStartForPurchased.Should().BeTrue();
        protocol.TargetSpecies.Should().Be(AnimalSpecies.CattleBeef);
        protocol.SpanDays.Should().Be(108);

        int Day(string vaccine) => protocol.Steps.First(s => s.VaccineName == vaccine).TargetAgeDays + 1;
        Day("পটাশ পানিতে পা ধোয়া").Should().Be(1);
        Day("Fast Vet").Should().Be(1);
        Day("FMD টিকা").Should().Be(14);
        Day("LSD টিকা").Should().Be(28);
        Day("Anthrax টিকা").Should().Be(42);

        var fastVet = protocol.Steps.Single(s => s.VaccineName == "Fast Vet").DoseRule!;
        fastVet.Amount.Should().Be(1m);
        fastVet.PerKg.Should().Be(150m);
        fastVet.DoseFor(290m).Should().Be(2m);

        var repeating = protocol.Steps.Where(s => s.RepeatEveryDays is not null).ToList();
        repeating.Should().HaveCount(2);
        repeating.Should().OnlyContain(s => s.RepeatEveryDays == 120);
        repeating.Select(s => s.TargetAgeDays + 1).Should().BeEquivalentTo([94, 108]);

        var decide = protocol.Steps.Single(s => s.ActionLink == MarketCattleProgrammeTemplate.SellOrKeepLink);
        (decide.TargetAgeDays + 1).Should().Be(90);
        decide.Kind.Should().Be(HealthTaskKind.CareTask);

        protocol.Steps.Count(s => s.VaccineName == "জিংক + লিভার টনিক").Should().Be(12, "days 5–10 and 95–100");
        protocol.Steps.Where(s => s.Kind == HealthTaskKind.Medicine).Should().OnlyContain(s => s.InventoryItemId == null,
            "medicines are left for each farm to link to its own stock");
        protocol.Steps.Should().OnlyContain(s => s.StepName.Length <= 100 && s.VaccineName.Length <= 100 && s.DosageInstruction.Length <= 250);
    }

    [Fact]
    public async Task AddingTheTemplate_SavesItForTheTenant()
    {
        var repo = new Mock<IVaccinationRepository>();
        VaccinationProtocol? saved = null;
        repo.Setup(r => r.AddProtocol(It.IsAny<VaccinationProtocol>())).Callback<VaccinationProtocol>(p => saved = p);
        var tenant = new Mock<ITenantService>();
        tenant.Setup(t => t.TenantId).Returns(TenantGuid);
        var uow = new Mock<IUnitOfWork>();

        var handler = new AddMarketCattleProgrammeCommandHandler(repo.Object, tenant.Object, uow.Object);
        var id = await handler.Handle(new AddMarketCattleProgrammeCommand(), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Id.Should().Be(id);
        saved.TenantId.Should().Be(TenantGuid);
        saved.Steps.Where(s => s.RepeatEveryDays is not null).Should().OnlyContain(s => s.RepeatEveryDays == 90);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
