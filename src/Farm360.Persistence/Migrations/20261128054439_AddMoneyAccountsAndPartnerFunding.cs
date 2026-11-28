using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMoneyAccountsAndPartnerFunding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LinkedTransactionId",
                schema: "finance",
                table: "FinancialTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MoneyAccountId",
                schema: "finance",
                table: "FinancialTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MoneyAccounts",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Branch = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_FarmId_MoneyAccountId",
                schema: "finance",
                table: "FinancialTransactions",
                columns: new[] { "FarmId", "MoneyAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_MoneyAccountId",
                schema: "finance",
                table: "FinancialTransactions",
                column: "MoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MoneyAccounts_TenantId",
                schema: "finance",
                table: "MoneyAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_MoneyAccounts_Farm_Default",
                schema: "finance",
                table: "MoneyAccounts",
                column: "FarmId",
                unique: true,
                filter: "[IsDefault] = 1 AND [IsDeleted] = 0");

            // ── Data: every farm gets its default "Cash in hand" account, and every existing cash
            // posting moves into it, so each farm's cash is unchanged and fully assigned. Farms are
            // taken from the farm register and from the ledger itself (a posting whose farm row is
            // gone still needs an account). Non-cash postings (stock, livestock, credit) keep NULL.
            migrationBuilder.Sql(@"
INSERT INTO [finance].[MoneyAccounts]
    ([Id], [FarmId], [Name], [Kind], [IsDefault], [IsActive], [CreatedAtUtc], [CreatedBy], [IsDeleted], [TenantId])
SELECT NEWID(), f.[FarmId], N'Cash in hand', N'Cash', 1, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000', 0, f.[TenantId]
FROM (
    SELECT s.[FarmId], s.[TenantId], ROW_NUMBER() OVER (PARTITION BY s.[FarmId] ORDER BY s.[Priority]) AS rn
    FROM (
        SELECT [Id] AS [FarmId], [TenantId], 0 AS [Priority] FROM [app].[Farms] WHERE [IsDeleted] = 0
        UNION ALL
        SELECT DISTINCT [FarmId], [TenantId], 1 AS [Priority] FROM [finance].[FinancialTransactions]
    ) AS s
) AS f
WHERE f.rn = 1
  AND NOT EXISTS (
    SELECT 1 FROM [finance].[MoneyAccounts] m
    WHERE m.[FarmId] = f.[FarmId] AND m.[IsDefault] = 1 AND m.[IsDeleted] = 0);
");

            migrationBuilder.Sql(@"
UPDATE t SET t.[MoneyAccountId] = m.[Id]
FROM [finance].[FinancialTransactions] t
JOIN [finance].[MoneyAccounts] m ON m.[FarmId] = t.[FarmId] AND m.[IsDefault] = 1 AND m.[IsDeleted] = 0
WHERE t.[Settlement] = N'Cash' AND t.[MoneyAccountId] IS NULL;
");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_MoneyAccounts_MoneyAccountId",
                schema: "finance",
                table: "FinancialTransactions",
                column: "MoneyAccountId",
                principalSchema: "finance",
                principalTable: "MoneyAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_MoneyAccounts_MoneyAccountId",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropTable(
                name: "MoneyAccounts",
                schema: "finance");

            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_FarmId_MoneyAccountId",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_MoneyAccountId",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "LinkedTransactionId",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "MoneyAccountId",
                schema: "finance",
                table: "FinancialTransactions");
        }
    }
}
