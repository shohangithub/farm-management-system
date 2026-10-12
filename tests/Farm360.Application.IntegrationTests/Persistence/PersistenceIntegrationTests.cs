using Farm360.Application.IntegrationTests.Infrastructure;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Tenancy;
using Farm360.Persistence.Repositories.Tenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Farm360.Application.IntegrationTests.Persistence;

/// <summary>
/// Behaviour that only a real SQL Server can prove: migrations, global query filters, unique
/// indexes, and repository queries as actually translated to SQL.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class PersistenceIntegrationTests(SqlServerFixture fixture)
{
    [SqlServerFact]
    public async Task EveryMigrationAppliesToAnEmptyDatabase()
    {
        await using var db = fixture.CreateContext(new TestTenantService());

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await db.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();
    }

    [SqlServerFact]
    public async Task TenantFilterHidesAnotherTenantsTransactions()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var farmId = Guid.NewGuid();

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantA)))
        {
            db.FinancialTransactions.Add(FinancialTransaction.Create(
                tenantA, farmId, TransactionType.Income, TransactionCategory.AnimalSale, 1000m, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        await using (var asTenantB = fixture.CreateContext(TestTenantService.For(tenantB)))
        {
            (await asTenantB.FinancialTransactions.Where(t => t.FarmId == farmId).ToListAsync())
                .Should().BeEmpty("tenant B must never see tenant A's rows");
        }

        await using (var asTenantA = fixture.CreateContext(TestTenantService.For(tenantA)))
        {
            (await asTenantA.FinancialTransactions.Where(t => t.FarmId == farmId).ToListAsync())
                .Should().ContainSingle();
        }
    }

    [SqlServerFact]
    public async Task OverheadAllocationUniqueIndexRejectsADuplicateTransactionAnimalPair()
    {
        var tenantId = Guid.NewGuid();
        var animalId = Guid.NewGuid();
        var sourceTransactionId = Guid.NewGuid();

        AnimalOverheadAllocation Allocation() => AnimalOverheadAllocation.Create(
            tenantId, animalId, Guid.NewGuid(), sourceTransactionId,
            TransactionCategory.LaborCost, OverheadBucket.Labour,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30),
            allocatedAmountBdt: 500m, OverheadAllocationMethod.PerHeadDay,
            headDays: 30m, weightAtAllocationKg: 250m, shareFactor: 0.5m, headCountAtAllocation: 2);

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            db.Set<AnimalOverheadAllocation>().Add(Allocation());
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            db.Set<AnimalOverheadAllocation>().Add(Allocation());
            var save = () => db.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>(
                "a re-run must never allocate the same expense to the same animal twice");
        }
    }

    [SqlServerFact]
    public async Task ASoftDeletedAllocationDoesNotBlockItsReplacement()
    {
        // Editing an already-split expense soft-deletes its old rows and writes new ones for the
        // same (transaction, animal) pair. The unique index must only consider live rows.
        var tenantId = Guid.NewGuid();
        var animalId = Guid.NewGuid();
        var sourceTransactionId = Guid.NewGuid();

        AnimalOverheadAllocation Allocation(decimal amount) => AnimalOverheadAllocation.Create(
            tenantId, animalId, Guid.NewGuid(), sourceTransactionId,
            TransactionCategory.LaborCost, OverheadBucket.Labour,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30),
            allocatedAmountBdt: amount, OverheadAllocationMethod.PerHeadDay,
            headDays: 30m, weightAtAllocationKg: 250m, shareFactor: 1m, headCountAtAllocation: 1);

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            var original = Allocation(12_000m);
            db.Set<AnimalOverheadAllocation>().Add(original);
            await db.SaveChangesAsync();

            original.SoftDelete(Guid.NewGuid());
            db.Set<AnimalOverheadAllocation>().Add(Allocation(10_000m));
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            var live = await db.Set<AnimalOverheadAllocation>()
                .Where(a => a.SourceTransactionId == sourceTransactionId)
                .ToListAsync();

            live.Should().ContainSingle().Which.AllocatedAmountBdt.Should().Be(10_000m);
        }
    }

    [SqlServerFact]
    public async Task PendingSubscriptionIsListedAcrossTenantsAndKeepsItsReferenceOnceCompleted()
    {
        var payingTenant = Guid.NewGuid();
        var tranId = $"F360-IT-{Guid.NewGuid():N}";

        await using (var db = fixture.CreateContext(TestTenantService.For(payingTenant)))
        {
            var repo = new TenantSubscriptionRepository(db);
            await repo.AddAsync(TenantSubscriptionRecord.Create(
                payingTenant, SubscriptionTier.Standard, SubscriptionBillingCycle.Monthly,
                3500m, "BDT", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
                "SSLCommerz", tranId, status: "Pending"));
            await db.SaveChangesAsync();
        }

        // The admin reviewing payments belongs to a different tenant, and must still see this one.
        await using (var asAdmin = fixture.CreateContext(TestTenantService.For(Guid.NewGuid())))
        {
            var repo = new TenantSubscriptionRepository(asAdmin);
            (await repo.GetPendingAsync()).Should().Contain(r => r.PaymentReference == tranId);

            var record = await repo.GetByPaymentReferenceAsync(tranId);
            record.Should().NotBeNull();
            record!.MarkCompleted();
            repo.Update(record);
            await asAdmin.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(TestTenantService.For(payingTenant)))
        {
            var repo = new TenantSubscriptionRepository(db);
            (await repo.GetPendingAsync()).Should().NotContain(r => r.PaymentReference == tranId);

            var completed = await repo.GetByPaymentReferenceAsync(tranId);
            completed.Should().NotBeNull("a second gateway callback must still find the record by tran_id");
            completed!.Status.Should().Be("Completed");
        }
    }
}
