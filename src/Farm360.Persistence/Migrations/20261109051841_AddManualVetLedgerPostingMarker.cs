using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualVetLedgerPostingMarker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LedgerVetPostedAnimalId",
                schema: "finance",
                table: "FinancialTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LedgerVetPostedBdt",
                schema: "finance",
                table: "FinancialTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LedgerVetPostedAnimalId",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "LedgerVetPostedBdt",
                schema: "finance",
                table: "FinancialTransactions");
        }
    }
}
