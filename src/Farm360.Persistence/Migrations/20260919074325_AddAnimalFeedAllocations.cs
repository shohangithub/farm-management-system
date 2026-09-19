using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimalFeedAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnimalFeedAllocations",
                schema: "feeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailyFeedingEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeedingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormulaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ShedId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AllocatedKg = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AllocatedCostBdt = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCostBdtPerKg = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Origin = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    WeightAtAllocationKg = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ShareFactor = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    HeadCountAtAllocation = table.Column<int>(type: "int", nullable: false),
                    IsBackfilled = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AnimalFeedAllocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalFeedAllocations_Tenant_Animal_Date",
                schema: "feeding",
                table: "AnimalFeedAllocations",
                columns: new[] { "TenantId", "AnimalId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalFeedAllocations_Tenant_Farm_Date",
                schema: "feeding",
                table: "AnimalFeedAllocations",
                columns: new[] { "TenantId", "FarmId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "UX_AnimalFeedAllocations_Entry_Animal",
                schema: "feeding",
                table: "AnimalFeedAllocations",
                columns: new[] { "DailyFeedingEntryId", "AnimalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimalFeedAllocations",
                schema: "feeding");
        }
    }
}
