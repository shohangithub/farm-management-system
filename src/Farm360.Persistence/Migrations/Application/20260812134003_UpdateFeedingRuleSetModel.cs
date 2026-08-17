using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations.Application
{
    /// <inheritdoc />
    public partial class UpdateFeedingRuleSetModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseNotes",
                schema: "feeding",
                table: "FeedingRuleSets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlanType",
                schema: "feeding",
                table: "FeedingRuleSets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FeedType",
                schema: "feeding",
                table: "FeedingRuleLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxAgeDays",
                schema: "feeding",
                table: "FeedingRuleLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxWeightKg",
                schema: "feeding",
                table: "FeedingRuleLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinAgeDays",
                schema: "feeding",
                table: "FeedingRuleLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinWeightKg",
                schema: "feeding",
                table: "FeedingRuleLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityValue",
                schema: "feeding",
                table: "FeedingRuleLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseNotes",
                schema: "feeding",
                table: "FeedingRuleSets");

            migrationBuilder.DropColumn(
                name: "PlanType",
                schema: "feeding",
                table: "FeedingRuleSets");

            migrationBuilder.DropColumn(
                name: "FeedType",
                schema: "feeding",
                table: "FeedingRuleLines");

            migrationBuilder.DropColumn(
                name: "MaxAgeDays",
                schema: "feeding",
                table: "FeedingRuleLines");

            migrationBuilder.DropColumn(
                name: "MaxWeightKg",
                schema: "feeding",
                table: "FeedingRuleLines");

            migrationBuilder.DropColumn(
                name: "MinAgeDays",
                schema: "feeding",
                table: "FeedingRuleLines");

            migrationBuilder.DropColumn(
                name: "MinWeightKg",
                schema: "feeding",
                table: "FeedingRuleLines");

            migrationBuilder.DropColumn(
                name: "QuantityValue",
                schema: "feeding",
                table: "FeedingRuleLines");
        }
    }
}
