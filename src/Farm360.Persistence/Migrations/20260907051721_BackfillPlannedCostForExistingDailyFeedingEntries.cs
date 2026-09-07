using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPlannedCostForExistingDailyFeedingEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. First priority: Backfill confirmed entries with unambiguous StockTransaction link
            migrationBuilder.Sql(@"
                UPDATE e
                SET e.UnitCostAtConsumptionBdt = st.UnitCostBdt,
                    e.TotalCostBdt = ROUND(COALESCE(e.ActualKg, e.ExpectedKg) * st.UnitCostBdt, 2)
                FROM [feeding].[DailyFeedingEntries] e
                INNER JOIN [inventory].[StockTransactions] st ON e.InventoryTransactionId = st.Id
                WHERE e.UnitCostAtConsumptionBdt IS NULL AND st.UnitCostBdt > 0;
            ");

            // 2. Second priority: Backfill planned or remaining entries from the associated FeedFormula total cost per kg
            migrationBuilder.Sql(@"
                UPDATE e
                SET e.UnitCostAtConsumptionBdt = f.TotalCostPerKgBdt,
                    e.TotalCostBdt = ROUND(COALESCE(e.ActualKg, e.ExpectedKg) * ISNULL(f.TotalCostPerKgBdt, 0), 2)
                FROM [feeding].[DailyFeedingEntries] e
                INNER JOIN [feeding].[FeedFormulas] f ON e.FormulaId = f.Id
                WHERE e.UnitCostAtConsumptionBdt IS NULL AND f.TotalCostPerKgBdt IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
