using Farm360.Application.IntegrationTests.Infrastructure;
using Farm360.Domain.BusinessRules;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Farm360.Application.IntegrationTests.Persistence;

/// <summary>
/// The by-products farm rules (per head and % of meat value) are migrated and round-trip on both the
/// tenant-wide row and a per-farm override row.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ByProductsValuationRulesTests(SqlServerFixture fixture)
{
    [SqlServerFact]
    public async Task MigrationIsAppliedAndRulesRoundTripPerTenantAndPerFarm()
    {
        await using (var check = fixture.CreateContext(new TestTenantService()))
        {
            (await check.Database.GetAppliedMigrationsAsync())
                .Should().Contain(m => m.EndsWith("_AddByProductsValuationRules", StringComparison.Ordinal));
            (await check.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        }

        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var tenantRow = TenantBusinessSettings.Create(tenantId, null,
            new BusinessRulesOverrides(ByProductsPercentOfMeat: 0.12m));
        var farmRow = TenantBusinessSettings.Create(tenantId, farmId,
            new BusinessRulesOverrides(ByProductsValuePerHeadBdt: 3_000m));

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            db.Set<TenantBusinessSettings>().AddRange(tenantRow, farmRow);
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext(TestTenantService.For(tenantId));
        var rows = await read.Set<TenantBusinessSettings>().AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .ToListAsync();

        var tenant = rows.Single(r => r.FarmId == null);
        var farm = rows.Single(r => r.FarmId == farmId);
        tenant.ByProductsPercentOfMeat.Should().Be(0.12m);
        tenant.ByProductsValuePerHeadBdt.Should().BeNull("not overridden: inherits the platform default of 0");
        farm.ByProductsValuePerHeadBdt.Should().Be(3_000m);
        farm.ByProductsPercentOfMeat.Should().BeNull();

        // farm → tenant → platform: the farm gets both.
        var effective = FarmBusinessRulesResolution.Apply(
            FarmBusinessRulesResolution.Apply(new FarmBusinessRules(), tenant.ToOverrides()), farm.ToOverrides());
        effective.ByProductsPercentOfMeat.Should().Be(0.12m);
        effective.ByProductsValuePerHeadBdt.Should().Be(3_000m);
    }
}
