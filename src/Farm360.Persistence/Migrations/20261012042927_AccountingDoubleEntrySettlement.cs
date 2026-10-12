using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountingDoubleEntrySettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InterestPaidBdt",
                schema: "finance",
                table: "LoanRecords",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrincipalRepaidBdt",
                schema: "finance",
                table: "LoanRecords",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Settlement",
                schema: "finance",
                table: "FinancialTransactions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Cash");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentId",
                schema: "finance",
                table: "FinancialTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DeathDate",
                schema: "app",
                table: "Animals",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_BatchId",
                schema: "finance",
                table: "FinancialTransactions",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_FarmId_SourceDocumentId_Category",
                schema: "finance",
                table: "FinancialTransactions",
                columns: new[] { "FarmId", "SourceDocumentId", "Category" });

            // ── Data: bring existing records under the new accounting rules ──────────────
            // Every statement is idempotent and only touches rows the old code produced.

            // 1. Feed and consumables posted by the feeding/consumable modules were taken out of
            //    stock; the cash left when that stock was bought. Settle them against Inventory so
            //    they stop being counted as a second cash payment.
            migrationBuilder.Sql(@"
UPDATE finance.FinancialTransactions
   SET Settlement = 'Inventory'
 WHERE IsAutomated = 1
   AND Settlement = 'Cash'
   AND ((Category = 'FeedCost' AND SourceModule = 'Feeding')
     OR (Category = 'ConsumableExpense' AND SourceModule = 'Inventory'));");

            // 2. Link automated postings to the document that produced them, so re-posting revises
            //    instead of duplicating. Those modules stored the source id as the reference.
            migrationBuilder.Sql(@"
UPDATE finance.FinancialTransactions
   SET SourceDocumentId = TRY_CONVERT(uniqueidentifier, ReferenceId)
 WHERE IsAutomated = 1
   AND SourceDocumentId IS NULL
   AND TRY_CONVERT(uniqueidentifier, ReferenceId) IS NOT NULL
   AND Category IN ('FeedCost', 'ConsumableExpense', 'MedicineCost', 'VeterinaryCost', 'AnimalSale');");

            migrationBuilder.Sql(@"
UPDATE finance.FinancialTransactions
   SET SourceDocumentId = AnimalId
 WHERE IsAutomated = 1
   AND SourceDocumentId IS NULL
   AND Category = 'AnimalPurchase'
   AND AnimalId IS NOT NULL;");

            // 3. Loan and investor postings are owned by their registers: mark them as system
            //    postings so they cannot be edited out of step from the ledger screen.
            migrationBuilder.Sql(@"
UPDATE finance.FinancialTransactions
   SET IsAutomated = 1, SourceModule = 'Investors'
 WHERE IsAutomated = 0
   AND Category IN ('InvestorCapital', 'InvestorWithdrawal', 'ProfitDistribution');

UPDATE finance.FinancialTransactions
   SET IsAutomated = 1, SourceModule = 'Finance'
 WHERE IsAutomated = 0
   AND Category IN ('LoanDisbursement', 'LoanRepayment');");

            // 4. Split historical loan repayments into principal and interest (flat-rate, pro rata).
            migrationBuilder.Sql(@"
UPDATE finance.LoanRecords
   SET PrincipalRepaidBdt = CASE
           WHEN PrincipalAmountBdt + ROUND(PrincipalAmountBdt * InterestRatePercent / 100, 2) = 0 THEN TotalRepaidBdt
           ELSE CASE WHEN ROUND(TotalRepaidBdt * PrincipalAmountBdt
                          / (PrincipalAmountBdt + ROUND(PrincipalAmountBdt * InterestRatePercent / 100, 2)), 2) > PrincipalAmountBdt
                     THEN PrincipalAmountBdt
                     ELSE ROUND(TotalRepaidBdt * PrincipalAmountBdt
                          / (PrincipalAmountBdt + ROUND(PrincipalAmountBdt * InterestRatePercent / 100, 2)), 2) END
       END
 WHERE TotalRepaidBdt > 0 AND PrincipalRepaidBdt = 0 AND InterestPaidBdt = 0;

UPDATE finance.LoanRecords
   SET InterestPaidBdt = TotalRepaidBdt - PrincipalRepaidBdt
 WHERE TotalRepaidBdt > 0 AND InterestPaidBdt = 0 AND TotalRepaidBdt > PrincipalRepaidBdt;");

            // 5. Death dates for animals recorded dead before the date was stored.
            migrationBuilder.Sql(@"
UPDATE a
   SET a.DeathDate = m.DeathDate
  FROM app.Animals a
  JOIN app.MortalityRecords m ON m.AnimalId = a.Id
 WHERE a.DeathDate IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_BatchId",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_FarmId_SourceDocumentId_Category",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "InterestPaidBdt",
                schema: "finance",
                table: "LoanRecords");

            migrationBuilder.DropColumn(
                name: "PrincipalRepaidBdt",
                schema: "finance",
                table: "LoanRecords");

            migrationBuilder.DropColumn(
                name: "Settlement",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "SourceDocumentId",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "DeathDate",
                schema: "app",
                table: "Animals");
        }
    }
}
