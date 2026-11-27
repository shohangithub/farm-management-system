using Farm360.Application.IntegrationTests.Infrastructure;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Persistence.Queries;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.IntegrationTests.Persistence;

/// <summary>The carried-cost source's grouped queries translate to SQL and pick the right postings.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class LivestockCarriedCostSourceTests(SqlServerFixture fixture)
{
    [SqlServerFact]
    public async Task GroupedQueriesTranslateAndOnlyAnimalTaggedCareCostsAreRead()
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var animalId = Guid.NewGuid();
        var day = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            db.FinancialTransactions.AddRange(
                // Counted: a treatment tagged to the animal, and a refund of part of it.
                FinancialTransaction.Create(tenantId, farmId, TransactionType.Expense, TransactionCategory.VeterinaryCost, 900m, day, animalId: animalId),
                FinancialTransaction.Create(tenantId, farmId, TransactionType.Income, TransactionCategory.VeterinaryCost, 100m, day, animalId: animalId),
                // Not counted: the purchase price, the farm-level feed posting, an untagged cost, a later cost.
                FinancialTransaction.Create(tenantId, farmId, TransactionType.Expense, TransactionCategory.AnimalPurchase, 50_000m, day, animalId: animalId),
                FinancialTransaction.Create(tenantId, farmId, TransactionType.Expense, TransactionCategory.FeedCost, 700m, day, animalId: animalId,
                    isAutomated: true, sourceModule: "Feeding", settlement: SettlementAccount.Inventory),
                FinancialTransaction.Create(tenantId, farmId, TransactionType.Expense, TransactionCategory.Utilities, 300m, day),
                FinancialTransaction.Create(tenantId, farmId, TransactionType.Expense, TransactionCategory.MedicineCost, 400m, day.AddMonths(2), animalId: animalId));
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext(TestTenantService.For(tenantId));
        var data = await new LivestockCarriedCostSource(read).LoadAsync(farmId, DateOnly.FromDateTime(day));

        data.Animals.Should().BeEmpty();
        data.Costs.Should().OnlyContain(c => c.AnimalId == animalId && c.Date == DateOnly.FromDateTime(day));
        data.Costs.Sum(c => c.AmountBdt).Should().Be(800m, "the 900 treatment less the 100 refund; nothing else qualifies");
    }
}
