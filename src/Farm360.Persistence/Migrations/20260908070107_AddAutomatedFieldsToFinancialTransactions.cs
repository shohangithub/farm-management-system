using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomatedFieldsToFinancialTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAutomated",
                schema: "finance",
                table: "FinancialTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceModule",
                schema: "finance",
                table: "FinancialTransactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAutomated",
                schema: "finance",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "SourceModule",
                schema: "finance",
                table: "FinancialTransactions");
        }
    }
}
