using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddFinanceModuleEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AnimalId",
                schema: "finance",
                table: "FinancialTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                schema: "finance",
                table: "FinancialTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "finance",
                table: "FinancialTransactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ShedId",
                schema: "finance",
                table: "FinancialTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnimalCostLedgers",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcquisitionCostBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalFeedCostBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVetCostBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalLaborCostBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalOverheadBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SaleRevenueBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
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
                    table.PrimaryKey("PK_AnimalCostLedgers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoanRecords",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LenderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PrincipalAmountBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InterestRatePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DisbursementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Schedule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalRepaidBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_LoanRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_AnimalId",
                schema: "finance",
                table: "FinancialTransactions",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalCostLedgers_FarmId",
                schema: "finance",
                table: "AnimalCostLedgers",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalCostLedgers_TenantId_AnimalId",
                schema: "finance",
                table: "AnimalCostLedgers",
                columns: new[] { "TenantId", "AnimalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanRecords_FarmId",
                schema: "finance",
                table: "LoanRecords",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanRecords_TenantId",
                schema: "finance",
                table: "LoanRecords",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimalCostLedgers",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "LoanRecords",
                schema: "finance");

            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_AnimalId",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "AnimalId",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "BatchId",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "ShedId",
                schema: "finance",
                table: "FinancialTransactions");
        }
    }
}
