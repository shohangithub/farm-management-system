using Farm360.Application.IntegrationTests.Infrastructure;
using Farm360.Domain.Livestock;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Farm360.Application.IntegrationTests.Persistence;

/// <summary>The breed's optional expected price per kg (sell-or-keep advice) is migrated and round-trips.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class BreedExpectedPriceTests(SqlServerFixture fixture)
{
    [SqlServerFact]
    public async Task ExpectedPricePerKgIsStoredAndClearedWithNull()
    {
        var tenantId = Guid.NewGuid();
        var withPrice = new Breed(Guid.NewGuid(), tenantId, "Sahiwal Cross IT", "", "Crossbred", "", "Beef",
            0.4m, 0.6m, 0.8m, 1.0m, 6m, 8m, 0.6m, 0.9m, 0m, 0m, 0m, 0m, "");
        withPrice.SetExpectedPricePerKg(455.555m);
        var without = new Breed(Guid.NewGuid(), tenantId, "Local IT", "", "Indigenous", "", "Beef",
            0.2m, 0.3m, 0.4m, 0.5m, 7m, 9m, 0.3m, 0.5m, 0m, 0m, 0m, 0m, "");

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            db.Set<Breed>().AddRange(withPrice, without);
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext(TestTenantService.For(tenantId));
        var breeds = await read.Set<Breed>().AsNoTracking().ToDictionaryAsync(b => b.Id);
        breeds[withPrice.Id].ExpectedPricePerKgBdt.Should().Be(455.56m);
        breeds[without.Id].ExpectedPricePerKgBdt.Should().BeNull();
    }
}
