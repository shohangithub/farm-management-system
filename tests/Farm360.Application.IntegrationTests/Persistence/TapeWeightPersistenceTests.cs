using Farm360.Application.IntegrationTests.Infrastructure;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Farm360.Application.IntegrationTests.Persistence;

/// <summary>AddTapeMeasureWeight: the weight method and tape measurements are migrated and round-trip.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class TapeWeightPersistenceTests(SqlServerFixture fixture)
{
    [SqlServerFact]
    public async Task TapeMeasurementsAndMethodRoundTrip()
    {
        var tenantId = Guid.NewGuid();
        var animal = Animal.Create(tenantId, Guid.NewGuid(), AnimalTag.Create($"TW-{Guid.NewGuid():N}"[..12], TagType.EarTag),
            AnimalSpecies.CattleBeef, Guid.NewGuid(), AnimalSex.Male, new DateOnly(2023, 1, 1),
            AcquisitionType.Purchased, new DateOnly(2024, 1, 1), 50_000m, null);
        var scale = animal.RecordWeight(Weight.Create(230m), new DateOnly(2025, 5, 1), Guid.NewGuid(), null);
        var tape = animal.RecordTapeWeight(62m, 51m, new DateOnly(2025, 6, 1), Guid.NewGuid(), "tape");

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            db.Set<Animal>().Add(animal);
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext(TestTenantService.For(tenantId));
        var records = await read.Set<WeightRecord>().AsNoTracking()
            .Where(w => w.AnimalId == animal.Id)
            .ToDictionaryAsync(w => w.Id);

        records[tape.Id].Method.Should().Be(WeightMethod.Tape);
        records[tape.Id].LengthInch.Should().Be(62m);
        records[tape.Id].GirthInch.Should().Be(51m);
        records[tape.Id].Weight.WeightKg.Should().Be(244.3m);

        records[scale.Id].Method.Should().Be(WeightMethod.Scale);
        records[scale.Id].LengthInch.Should().BeNull();
        records[scale.Id].GirthInch.Should().BeNull();
    }

    [SqlServerFact]
    public async Task ExistingRowsDefaultToScale()
    {
        await using var db = fixture.CreateContext(TestTenantService.For(Guid.NewGuid()));

        var columnDefault = await db.Database
            .SqlQueryRaw<string>(
                "SELECT COLUMN_DEFAULT AS Value FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'app' AND TABLE_NAME = 'WeightRecords' AND COLUMN_NAME = 'Method'")
            .SingleAsync();

        columnDefault.Should().Be("((0))", "rows weighed before tape support are Scale (0)");
    }
}
