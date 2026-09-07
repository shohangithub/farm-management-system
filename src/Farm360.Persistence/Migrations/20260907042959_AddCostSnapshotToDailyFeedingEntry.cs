using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCostSnapshotToDailyFeedingEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitCostAtConsumptionBdt",
                schema: "feeding",
                table: "DailyFeedingEntries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCostBdt",
                schema: "feeding",
                table: "DailyFeedingEntries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE e
                SET e.UnitCostAtConsumptionBdt = sub.AvgUnitCost,
                    e.TotalCostBdt = ROUND(COALESCE(e.ActualKg, e.ExpectedKg) * sub.AvgUnitCost, 2)
                FROM [feeding].[DailyFeedingEntries] e
                INNER JOIN (
                    SELECT t.ReferenceId, 
                           ROUND(SUM(t.Quantity * t.UnitCostBdt) / NULLIF(SUM(t.Quantity), 0), 2) AS AvgUnitCost
                    FROM [inventory].[StockTransactions] t
                    WHERE t.ReferenceId IS NOT NULL
                    GROUP BY t.ReferenceId
                ) sub ON e.Id = sub.ReferenceId
                WHERE e.UnitCostAtConsumptionBdt IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitCostAtConsumptionBdt",
                schema: "feeding",
                table: "DailyFeedingEntries");

            migrationBuilder.DropColumn(
                name: "TotalCostBdt",
                schema: "feeding",
                table: "DailyFeedingEntries");
        }
    }
}
