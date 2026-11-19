using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.EventHandlers.Integration;
using Farm360.Application.Health.Commands.MedicalTreatments;
using Farm360.Application.Health.Commands.VaccinationEvents;
using Farm360.Application.Health.EventHandlers;
using Farm360.Application.Livestock.Queries;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Health.ValueObjects;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Events;
using Farm360.Domain.Livestock.Exceptions;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Health;

/// <summary>
/// Sold/dead animals in health: future work closed on exit, new records refused on/after the exit
/// date, and the on-farm presence filter used by pickers.
/// </summary>
public sealed class AnimalExitHealthTests
{
    private static readonly DateOnly SaleDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);

    private static Animal NewAnimal() =>
        Animal.Create(Guid.NewGuid(), Guid.NewGuid(), AnimalTag.Create("C-12", TagType.EarTag),
            AnimalSpecies.CattleBeef, Guid.NewGuid(), AnimalSex.Female, SaleDate.AddYears(-2),
            AcquisitionType.Purchased, SaleDate.AddMonths(-6), 50_000m, null);

    private static Animal SoldAnimal()
    {
        var animal = NewAnimal();
        animal.Sell(90_000m, SaleDate, Guid.NewGuid(), "Karim", null);
        return animal;
    }

    // ── Scheduled health items cancelled on exit ───────────────────────────────

    private static (AnimalExitHealthHandler Handler, VaccinationEvent Pending, VaccinationEvent Given, MedicalTreatment Ongoing, Mock<IUnitOfWork> Uow)
        HandlerWithItems(Guid animalId)
    {
        var tenant = Guid.NewGuid();
        var pending = VaccinationEvent.Schedule(tenant, animalId, null, "FMD", "", SaleDate.AddDays(20), null);
        var given = VaccinationEvent.Schedule(tenant, animalId, null, "HS", "", SaleDate.AddDays(-30), null);
        given.RecordAdministration(SaleDate.AddDays(-30), Guid.NewGuid());
        var ongoing = MedicalTreatment.LogTreatment(tenant, animalId, "Fever", "Oxy", Dosage.Create(10, "ml"),
            WithdrawalPeriod.Create(5, 14), SaleDate.AddDays(-3), null, 400m, null, null);

        var vaccinations = new Mock<IVaccinationRepository>();
        vaccinations.Setup(r => r.GetEventsByAnimalIdAsync(animalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaccinationEvent> { pending, given });
        var treatments = new Mock<IMedicalTreatmentRepository>();
        treatments.Setup(r => r.GetByAnimalIdAsync(animalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicalTreatment> { ongoing });
        var uow = new Mock<IUnitOfWork>();

        return (new AnimalExitHealthHandler(vaccinations.Object, treatments.Object, uow.Object), pending, given, ongoing, uow);
    }

    [Fact]
    public async Task LeftFarm_CancelsPendingVaccinations_ClosesTreatments_KeepsHistory()
    {
        var animalId = Guid.NewGuid();
        var (handler, pending, given, ongoing, uow) = HandlerWithItems(animalId);

        await handler.Handle(new AnimalSoldNotification(new AnimalSoldEvent(Guid.NewGuid(), DateTime.UtcNow, animalId, Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), 1000m, SaleDate, null, null)), CancellationToken.None);

        pending.Status.Should().Be(VaccinationStatus.Cancelled);
        given.Status.Should().Be(VaccinationStatus.Completed);
        ongoing.Status.Should().Be(TreatmentStatus.Completed);
        ongoing.EndDate.Should().Be(SaleDate);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaleNotification_FromSellCommand_ClosesHealthWork()
    {
        var animalId = Guid.NewGuid();
        var (handler, pending, _, _, _) = HandlerWithItems(animalId);
        var sold = new AnimalSoldEvent(Guid.NewGuid(), DateTime.UtcNow, animalId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 90_000m, SaleDate, null, null);

        await handler.Handle(new AnimalSoldNotification(sold), CancellationToken.None);

        pending.Status.Should().Be(VaccinationStatus.Cancelled);
        pending.Notes.Should().Contain("sold");
    }

    [Fact]
    public async Task DiedEvent_ClosesHealthWork_AndSecondRunChangesNothing()
    {
        var animalId = Guid.NewGuid();
        var (handler, pending, _, _, uow) = HandlerWithItems(animalId);
        var died = new AnimalDiedEvent(Guid.NewGuid(), DateTime.UtcNow, animalId, Guid.NewGuid(), DisposalReason.Disease, SaleDate);

        await handler.Handle(died, CancellationToken.None);
        await handler.Handle(died, CancellationToken.None);

        pending.Status.Should().Be(VaccinationStatus.Cancelled);
        pending.Notes.Should().Contain("died");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── New records refused on/after exit ──────────────────────────────────────

    private static LogMedicalTreatmentCommandHandler TreatmentHandler(Animal animal, Mock<IMedicalTreatmentRepository> repo)
    {
        var animals = new Mock<IAnimalRepository>();
        animals.Setup(r => r.GetByIdAsync(animal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(animal);
        var tenant = new Mock<ITenantService>();
        tenant.Setup(t => t.TenantId).Returns(animal.TenantId);
        return new LogMedicalTreatmentCommandHandler(repo.Object, animals.Object, tenant.Object,
            new Mock<IUnitOfWork>().Object, new Mock<MediatR.IPublisher>().Object);
    }

    private static LogMedicalTreatmentCommand Treatment(Guid animalId, DateOnly start) =>
        new(animalId, "Fever", "Oxy", 10, "ml", 0, 0, start, 300, null, null);

    [Fact]
    public async Task LogTreatment_OnOrAfterSaleDate_IsRefused()
    {
        var animal = SoldAnimal();
        var repo = new Mock<IMedicalTreatmentRepository>();

        var act = () => TreatmentHandler(animal, repo).Handle(Treatment(animal.Id, SaleDate), CancellationToken.None);

        await act.Should().ThrowAsync<AnimalHasLeftFarmException>().WithMessage("*sold on*");
        repo.Verify(r => r.Add(It.IsAny<MedicalTreatment>()), Times.Never);
    }

    [Fact]
    public async Task LogTreatment_DatedBeforeSale_IsAllowedAsLateEntry()
    {
        var animal = SoldAnimal();
        var repo = new Mock<IMedicalTreatmentRepository>();

        await TreatmentHandler(animal, repo).Handle(Treatment(animal.Id, SaleDate.AddDays(-5)), CancellationToken.None);

        repo.Verify(r => r.Add(It.IsAny<MedicalTreatment>()), Times.Once);
    }

    [Fact]
    public async Task ScheduleVaccination_ForSoldAnimal_IsRefused()
    {
        var animal = SoldAnimal();
        var animals = new Mock<IAnimalRepository>();
        animals.Setup(r => r.GetByIdAsync(animal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(animal);
        var repo = new Mock<IVaccinationRepository>();
        var handler = new ScheduleVaccinationCommandHandler(repo.Object, animals.Object,
            new Mock<ITenantService>().Object, new Mock<IUnitOfWork>().Object);

        var act = () => handler.Handle(new ScheduleVaccinationCommand(animal.Id, null, "FMD", "", SaleDate.AddDays(10), null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<AnimalHasLeftFarmException>();
        repo.Verify(r => r.AddEvent(It.IsAny<VaccinationEvent>()), Times.Never);
    }

    [Fact]
    public async Task BatchVaccination_IncludingDeadAnimalAfterDeath_IsRefused()
    {
        var alive = NewAnimal();
        var dead = NewAnimal();
        dead.RecordDeath(DisposalReason.Disease, SaleDate, null);
        var animals = new Mock<IAnimalRepository>();
        animals.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Animal> { alive, dead });
        var repo = new Mock<IVaccinationRepository>();
        var handler = new BatchRecordVaccinationCommandHandler(repo.Object, animals.Object,
            new Mock<ITenantService>().Object, new Mock<ICurrentUserService>().Object,
            new Mock<IUnitOfWork>().Object, new Mock<MediatR.IPublisher>().Object);

        var act = () => handler.Handle(new BatchRecordVaccinationCommand([alive.Id, dead.Id], "FMD", "", SaleDate.AddDays(1), null),
            CancellationToken.None);

        await act.Should().ThrowAsync<AnimalHasLeftFarmException>().WithMessage("*died on*");
        repo.Verify(r => r.AddEvent(It.IsAny<VaccinationEvent>()), Times.Never);
    }

    // ── Pickers list on-farm animals only ──────────────────────────────────────

    [Fact]
    public async Task AnimalList_WithPresenceOnFarm_UsesPresenceQuery()
    {
        var repository = new Mock<IAnimalRepository>();
        var listQueries = new Mock<IAnimalListQueries>();
        var onFarm = NewAnimal();
        listQueries.Setup(q => q.GetPagedAsync(It.Is<GetAnimalListQuery>(f => f.Presence == AnimalPresence.OnFarm), 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Animal> { onFarm }.AsReadOnly(), 1));
        var handler = new GetAnimalListQueryHandler(repository.Object, listQueries.Object);

        var result = await handler.Handle(new GetAnimalListQuery(Presence: AnimalPresence.OnFarm), CancellationToken.None);

        result.Items.Should().ContainSingle(a => a.Id == onFarm.Id);
        repository.VerifyNoOtherCalls();
    }
}
