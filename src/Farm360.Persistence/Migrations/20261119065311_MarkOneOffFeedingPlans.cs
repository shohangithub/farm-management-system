using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarkOneOffFeedingPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOneOff",
                schema: "feeding",
                table: "AnimalFeedingPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Backfill: one-day, already-completed individual plans created to hold a manual feed entry
            // (or a daily feed sheet line). They carry the entry reason, or a "Direct · …" rule set.
            migrationBuilder.Sql(@"
UPDATE p SET p.IsOneOff = 1
FROM feeding.AnimalFeedingPlans p
WHERE p.AnimalId IS NOT NULL
  AND p.EndDate = p.StartDate
  AND p.Status = 3
  AND (EXISTS (SELECT 1 FROM feeding.DailyFeedingEntries e
               WHERE e.FeedingPlanId = p.Id
                 AND (e.AdjustmentReason LIKE N'Manual entry%' OR e.AdjustmentReason LIKE N'Daily feed sheet%'))
       OR EXISTS (SELECT 1 FROM feeding.FeedingRuleSets r
                  WHERE r.Id = p.FeedingRuleSetId AND r.Name LIKE N'Direct · %'));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOneOff",
                schema: "feeding",
                table: "AnimalFeedingPlans");
        }
    }
}
