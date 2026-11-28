using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTapeMeasureWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GirthInch",
                schema: "app",
                table: "WeightRecords",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LengthInch",
                schema: "app",
                table: "WeightRecords",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Method",
                schema: "app",
                table: "WeightRecords",
                type: "int",
                nullable: false,
                defaultValue: 0); // 0 = WeightMethod.Scale: every existing weight was weighed on a scale.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GirthInch",
                schema: "app",
                table: "WeightRecords");

            migrationBuilder.DropColumn(
                name: "LengthInch",
                schema: "app",
                table: "WeightRecords");

            migrationBuilder.DropColumn(
                name: "Method",
                schema: "app",
                table: "WeightRecords");
        }
    }
}
