using Farm360.Application.Health.Queries.MedicalTreatments;
using Farm360.Application.Health.Queries.MortalityRecords;
using Farm360.Application.Health.Queries.VaccinationEvents;
using Farm360.Application.Livestock.Queries;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Health.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Health;

/// <summary>
/// Health list rows carry the animal's ear tag, read with one set-based lookup per page.
/// </summary>
public sealed class HealthListAnimalTagTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private readonly IAnimalTagNames _tags = Substitute.For<IAnimalTagNames>();

    private void TagsAre(Dictionary<Guid, string> tags) =>
        _tags.GetTagsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(tags);

    [Fact]
    public async Task MortalityRecords_CarryTheAnimalTag()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var records = new List<MortalityRecord>
        {
            MortalityRecord.Record(Tenant, a1, new DateOnly(2026, 3, 1), CauseOfDeath.Disease, "FMD", null, 50000m, null, Guid.NewGuid()),
            MortalityRecord.Record(Tenant, a2, new DateOnly(2026, 4, 1), CauseOfDeath.Accident, null, null, null, null, Guid.NewGuid()),
        };
        var repo = Substitute.For<IMortalityRecordRepository>();
        repo.GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((records, 2));
        TagsAre(new Dictionary<Guid, string> { [a1] = "B-001" });

        var result = await new GetMortalityRecordsQueryHandler(repo, _tags)
            .Handle(new GetMortalityRecordsQuery(), CancellationToken.None);

        result.Items.Single(m => m.AnimalId == a1).AnimalTag.Should().Be("B-001");
        result.Items.Single(m => m.AnimalId == a2).AnimalTag.Should().BeNull("an unknown animal has no tag");
        await _tags.Received(1).GetTagsAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpcomingVaccinations_CarryTheAnimalTag_LookedUpOncePerDistinctAnimal()
    {
        var a1 = Guid.NewGuid();
        var events = new List<VaccinationEvent>
        {
            VaccinationEvent.Schedule(Tenant, a1, null, "FMD", "B1", new DateOnly(2026, 12, 1), null),
            VaccinationEvent.Schedule(Tenant, a1, null, "LSD", "B2", new DateOnly(2026, 12, 5), null),
        };
        var repo = Substitute.For<IVaccinationRepository>();
        repo.GetUpcomingEventsAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(events);
        TagsAre(new Dictionary<Guid, string> { [a1] = "C-007" });

        var result = await new GetUpcomingVaccinationsQueryHandler(repo, _tags)
            .Handle(new GetUpcomingVaccinationsQuery(Guid.NewGuid(), new DateOnly(2026, 12, 31)), CancellationToken.None);

        result.Should().HaveCount(2).And.OnlyContain(v => v.AnimalTag == "C-007");
        await _tags.Received(1).GetTagsAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Treatments_CarryTheAnimalTag()
    {
        var a1 = Guid.NewGuid();
        var treatment = MedicalTreatment.LogTreatment(Tenant, a1, "Fever", "Paracetamol", Dosage.Create(10m, "ml"),
            WithdrawalPeriod.Create(0, 0), new DateOnly(2026, 5, 1), null, 200m, null, null);
        var repo = Substitute.For<IMedicalTreatmentRepository>();
        repo.GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<TreatmentStatus?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((new List<MedicalTreatment> { treatment }, 1));
        TagsAre(new Dictionary<Guid, string> { [a1] = "B-010" });

        var result = await new GetTreatmentListQueryHandler(repo, _tags)
            .Handle(new GetTreatmentListQuery(), CancellationToken.None);

        result.Items.Single().AnimalTag.Should().Be("B-010");
    }
}
