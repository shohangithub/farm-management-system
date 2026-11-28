using Farm360.Application.IntegrationTests.Infrastructure;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Persistence.Context;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Farm360.Application.IntegrationTests.Persistence;

/// <summary>
/// Money accounts on real SQL Server: the migration gives every farm its "Cash in hand" account and
/// moves the existing cash postings into it; new cash postings that name no account land there too.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class MoneyAccountMigrationTests(SqlServerFixture fixture)
{
    private const string MigrationBefore = "20261127163342_AddTapeMeasureWeight";

    [SqlServerFact]
    public async Task MigrationGivesEachFarmADefaultAccountAndMovesOldCashRowsIntoIt()
    {
        // A database of its own, migrated only up to the release before money accounts.
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"Farm360_IT_Mig_{Guid.NewGuid():N}",
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        var tenantId = Guid.NewGuid();
        var farmA = Guid.NewGuid();
        var farmB = Guid.NewGuid();
        var cashA1 = Guid.NewGuid();
        var cashA2 = Guid.NewGuid();
        var stockA = Guid.NewGuid();
        var cashB = Guid.NewGuid();

        await using var db = new ApplicationDbContext(options, TestTenantService.For(tenantId));
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(MigrationBefore);

            await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO [finance].[FinancialTransactions]
    ([Id],[FarmId],[TenantId],[Type],[Category],[AmountBdt],[TransactionDate],[ReferenceId],[Description],[Notes],[IsAutomated],[Settlement],[CreatedAtUtc],[CreatedBy],[IsDeleted],[RowVersion])
VALUES
    ({0},{4},{6},N'Income', N'MilkSale',      10000, '2026-08-01', N'', N'Milk',  N'', 0, N'Cash',      SYSUTCDATETIME(), {7}, 0, 0x00),
    ({1},{4},{6},N'Expense',N'FeedCost',       3000, '2026-08-02', N'', N'Feed',  N'', 0, N'Cash',      SYSUTCDATETIME(), {7}, 0, 0x00),
    ({2},{4},{6},N'Expense',N'FeedCost',        700, '2026-08-03', N'', N'Used',  N'', 1, N'Inventory', SYSUTCDATETIME(), {7}, 0, 0x00),
    ({3},{5},{6},N'Income', N'OwnerCapital',  50000, '2026-08-01', N'', N'Owner', N'', 0, N'Cash',      SYSUTCDATETIME(), {7}, 0, 0x00);",
                cashA1, cashA2, stockA, cashB, farmA, farmB, tenantId, Guid.Empty);

            await db.Database.MigrateAsync();

            var accounts = await db.MoneyAccounts.IgnoreQueryFilters().Where(a => a.TenantId == tenantId).ToListAsync();
            accounts.Should().HaveCount(2, "one default account for each farm that has postings");
            accounts.Should().OnlyContain(a => a.IsDefault && a.IsActive && a.Name == MoneyAccount.DefaultName && a.Kind == MoneyAccountKind.Cash);

            var rows = await db.FinancialTransactions.IgnoreQueryFilters().Where(t => t.TenantId == tenantId).ToDictionaryAsync(t => t.Id);
            var defaultA = accounts.Single(a => a.FarmId == farmA).Id;
            var defaultB = accounts.Single(a => a.FarmId == farmB).Id;
            rows[cashA1].MoneyAccountId.Should().Be(defaultA);
            rows[cashA2].MoneyAccountId.Should().Be(defaultA);
            rows[cashB].MoneyAccountId.Should().Be(defaultB);
            rows[stockA].MoneyAccountId.Should().BeNull("stock used from the store moved no money");
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [SqlServerFact]
    public async Task CashPostingSavedWithoutAnAccountLandsInTheFarmsDefaultAccount()
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var day = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        Guid cashId;
        Guid creditId;

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            var cash = FinancialTransaction.Create(tenantId, farmId, TransactionType.Expense, TransactionCategory.LaborCost, 1_500m, day);
            var credit = FinancialTransaction.Create(tenantId, farmId, TransactionType.Expense, TransactionCategory.Transport, 800m, day,
                settlement: SettlementAccount.Payable);
            db.FinancialTransactions.AddRange(cash, credit);
            await db.SaveChangesAsync();
            cashId = cash.Id;
            creditId = credit.Id;
        }

        await using var read = fixture.CreateContext(TestTenantService.For(tenantId));
        var account = await read.MoneyAccounts.SingleAsync(a => a.FarmId == farmId);
        account.IsDefault.Should().BeTrue("the first cash posting of a farm with no account creates its default");
        (await read.FinancialTransactions.SingleAsync(t => t.Id == cashId)).MoneyAccountId.Should().Be(account.Id);
        (await read.FinancialTransactions.SingleAsync(t => t.Id == creditId)).MoneyAccountId.Should().BeNull("a deal on credit moved no money");
    }
}
