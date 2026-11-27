using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddByProductsValuationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ByProductsPercentOfMeat",
                schema: "app",
                table: "TenantBusinessSettings",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ByProductsValuePerHeadBdt",
                schema: "app",
                table: "TenantBusinessSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ByProductsPercentOfMeat",
                schema: "app",
                table: "TenantBusinessSettings");

            migrationBuilder.DropColumn(
                name: "ByProductsValuePerHeadBdt",
                schema: "app",
                table: "TenantBusinessSettings");
        }
    }
}
