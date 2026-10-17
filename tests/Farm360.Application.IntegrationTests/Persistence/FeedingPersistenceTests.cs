using Farm360.Application.IntegrationTests.Infrastructure;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Persistence.Repositories.Feeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Farm360.Application.IntegrationTests.Persistence;

/// <summary>Feeding writes that add child rows to a loaded aggregate, through the real repositories.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class FeedingPersistenceTests(SqlServerFixture fixture)
{
    [SqlServerFact]
    public async Task PausingAPlanSavesTheExclusion()
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            db.AnimalFeedingPlans.Add(new AnimalFeedingPlan(planId, tenantId, farmId, Guid.NewGuid(),
                FeedingPlanType.FixedQuantity, new DateOnly(2026, 10, 1), null, animalId: Guid.NewGuid()));
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            var repo = new AnimalFeedingPlanRepository(db);
            var plan = await repo.GetByIdAsync(planId);
            plan!.AddExclusion(new DateOnly(2026, 10, 15), "Sick", new DateOnly(2026, 10, 18));
            repo.Update(plan);
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            var plan = await db.AnimalFeedingPlans.Include(p => p.Exclusions).SingleAsync(p => p.Id == planId);
            plan.Exclusions.Should().ContainSingle();
            plan.IsPausedOn(new DateOnly(2026, 10, 16)).Should().BeTrue("a pause covers every day until it resumes");
            plan.IsPausedOn(new DateOnly(2026, 10, 18)).Should().BeFalse();
        }
    }

    [SqlServerFact]
    public async Task UnitsAndTheirPacksSaveAndGrow()
    {
        var tenantId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            var kg = new Farm360.Domain.Inventory.MeasureUnit(unitId, tenantId, "kg");
            kg.AddConversion("Bag (50 kg)", 50m);
            db.MeasureUnits.Add(kg);
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            var repo = new Farm360.Persistence.Repositories.Inventory.MeasureUnitRepository(db);
            var kg = await repo.GetByIdAsync(unitId);
            kg!.AddConversion("Bag (25 kg)", 25m);
            repo.Update(kg);
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            var kg = await new Farm360.Persistence.Repositories.Inventory.MeasureUnitRepository(db).GetByIdAsync(unitId);
            kg!.Conversions.Select(c => c.Name).Should().BeEquivalentTo(["kg", "Bag (50 kg)", "Bag (25 kg)"]);
        }
    }

    [SqlServerFact]
    public async Task CurrentLocationsQueryTranslatesToSql()
    {
        await using var db = fixture.CreateContext(TestTenantService.For(Guid.NewGuid()));
        var repo = new Farm360.Persistence.Repositories.Livestock.AnimalRepository(db);

        var locations = await repo.GetCurrentLocationsByFarmAsync(Guid.NewGuid());

        locations.Should().BeEmpty();
    }

    [SqlServerFact]
    public async Task EditingARuleSetReplacesItsLines()
    {
        var tenantId = Guid.NewGuid();
        var setId = Guid.NewGuid();

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            var set = new FeedingRuleSet(setId, tenantId, "Chart", TargetAnimalType.Cattle, FeedingPurpose.Growth, FeedingPlanType.WeightQuantity);
            set.AddRuleLine(80m, 150m, null, null, FeedCategory.Concentrate, 1.5m);
            db.FeedingRuleSets.Add(set);
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            var repo = new FeedingRuleSetRepository(db);
            var set = await repo.GetByIdAsync(setId);
            set!.ClearLines();
            set.AddRuleLine(81m, 150m, null, null, FeedCategory.Concentrate, 1.5m);
            set.AddRuleLine(151m, 180m, null, null, FeedCategory.Concentrate, 2m);
            repo.Update(set);
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            var set = await db.FeedingRuleSets.Include(s => s.Lines).SingleAsync(s => s.Id == setId);
            set.Lines.Select(l => l.ConcentrateKgPerDay).Should().BeEquivalentTo([1.5m, 2m]);
        }
    }
}
