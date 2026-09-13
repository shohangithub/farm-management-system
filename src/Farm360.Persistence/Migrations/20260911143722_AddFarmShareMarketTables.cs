using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmShareMarketTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FarmShareConfigs",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalShares = table.Column<int>(type: "int", nullable: false),
                    SharePriceBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OwnerShareCount = table.Column<int>(type: "int", nullable: false),
                    AllocatedShareCount = table.Column<int>(type: "int", nullable: false),
                    MinimumPurchaseShares = table.Column<int>(type: "int", nullable: false),
                    IsShareSaleOpen = table.Column<bool>(type: "bit", nullable: false),
                    LastValuationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValuationNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_FarmShareConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShareHoldings",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvestorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShareCount = table.Column<int>(type: "int", nullable: false),
                    AveragePurchasePriceBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalInvestedBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CertificateNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ShareHoldings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShareTransactions",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShareHoldingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvestorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ShareCount = table.Column<int>(type: "int", nullable: false),
                    PricePerShareBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmountBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CounterpartyInvestorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FinancialTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ShareTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FarmShareConfigs_FarmId",
                schema: "finance",
                table: "FarmShareConfigs",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmShareConfigs_TenantId",
                schema: "finance",
                table: "FarmShareConfigs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareHoldings_FarmId_InvestorId",
                schema: "finance",
                table: "ShareHoldings",
                columns: new[] { "FarmId", "InvestorId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShareHoldings_InvestorId",
                schema: "finance",
                table: "ShareHoldings",
                column: "InvestorId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareHoldings_TenantId",
                schema: "finance",
                table: "ShareHoldings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareTransactions_FarmId",
                schema: "finance",
                table: "ShareTransactions",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareTransactions_InvestorId",
                schema: "finance",
                table: "ShareTransactions",
                column: "InvestorId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareTransactions_TenantId",
                schema: "finance",
                table: "ShareTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareTransactions_TransactionDate",
                schema: "finance",
                table: "ShareTransactions",
                column: "TransactionDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FarmShareConfigs",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "ShareHoldings",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "ShareTransactions",
                schema: "finance");
        }
    }
}
