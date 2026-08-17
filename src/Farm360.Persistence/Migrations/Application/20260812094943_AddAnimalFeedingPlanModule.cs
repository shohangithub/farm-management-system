using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddAnimalFeedingPlanModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InventoryItemId",
                schema: "feeding",
                table: "FeedIngredients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FeedingPlanId",
                schema: "feeding",
                table: "FeedConsumptionLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnimalFeedingPlans",
                schema: "feeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ShedId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FeedingRuleSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanType = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentRuleLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentConcentrateKgPerDay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrentRoughageKgPerDay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TriggeredByWeightKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalFeedingPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyFeedingEntries",
                schema: "feeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeedingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShedId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FormulaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    InventoryTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyFeedingEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedingCycleReconciliations",
                schema: "feeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalExpectedKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalActualKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedingCycleReconciliations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedingRuleSets",
                schema: "feeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Species = table.Column<int>(type: "int", nullable: false),
                    BreedId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgeFromDays = table.Column<int>(type: "int", nullable: true),
                    AgeToDays = table.Column<int>(type: "int", nullable: true),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedingRuleSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedingPlanExclusions",
                schema: "feeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalFeedingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExclusionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ResumesOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedingPlanExclusions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedingPlanExclusions_AnimalFeedingPlans_AnimalFeedingPlanId",
                        column: x => x.AnimalFeedingPlanId,
                        principalSchema: "feeding",
                        principalTable: "AnimalFeedingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedingCycleReconciliationLines",
                schema: "feeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeedingCycleReconciliationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedQty = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualQty = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdjustmentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedingCycleReconciliationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedingCycleReconciliationLines_FeedingCycleReconciliations_FeedingCycleReconciliationId",
                        column: x => x.FeedingCycleReconciliationId,
                        principalSchema: "feeding",
                        principalTable: "FeedingCycleReconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedingRuleLines",
                schema: "feeding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeedingRuleSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeightFromKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WeightToKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FormulaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConcentrateKgPerDay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RoughageKgPerDay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SessionsPerDay = table.Column<int>(type: "int", nullable: false),
                    ProteinTargetPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedingRuleLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedingRuleLines_FeedingRuleSets_FeedingRuleSetId",
                        column: x => x.FeedingRuleSetId,
                        principalSchema: "feeding",
                        principalTable: "FeedingRuleSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedIngredients_InventoryItemId",
                schema: "feeding",
                table: "FeedIngredients",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedConsumptionLogs_FeedingPlanId",
                schema: "feeding",
                table: "FeedConsumptionLogs",
                column: "FeedingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalFeedingPlans_AnimalId",
                schema: "feeding",
                table: "AnimalFeedingPlans",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalFeedingPlans_BatchId",
                schema: "feeding",
                table: "AnimalFeedingPlans",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalFeedingPlans_PenId",
                schema: "feeding",
                table: "AnimalFeedingPlans",
                column: "PenId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalFeedingPlans_ShedId",
                schema: "feeding",
                table: "AnimalFeedingPlans",
                column: "ShedId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalFeedingPlans_TenantId_FarmId_Status",
                schema: "feeding",
                table: "AnimalFeedingPlans",
                columns: new[] { "TenantId", "FarmId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyFeedingEntries_FeedingPlanId",
                schema: "feeding",
                table: "DailyFeedingEntries",
                column: "FeedingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyFeedingEntries_Status",
                schema: "feeding",
                table: "DailyFeedingEntries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DailyFeedingEntries_TenantId_FarmId_EntryDate",
                schema: "feeding",
                table: "DailyFeedingEntries",
                columns: new[] { "TenantId", "FarmId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedingCycleReconciliationLines_FeedingCycleReconciliationId",
                schema: "feeding",
                table: "FeedingCycleReconciliationLines",
                column: "FeedingCycleReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedingCycleReconciliations_TenantId_FarmId_PeriodStart_PeriodEnd",
                schema: "feeding",
                table: "FeedingCycleReconciliations",
                columns: new[] { "TenantId", "FarmId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedingPlanExclusions_AnimalFeedingPlanId",
                schema: "feeding",
                table: "FeedingPlanExclusions",
                column: "AnimalFeedingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedingRuleLines_FeedingRuleSetId",
                schema: "feeding",
                table: "FeedingRuleLines",
                column: "FeedingRuleSetId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedingRuleSets_TenantId_Species_Purpose",
                schema: "feeding",
                table: "FeedingRuleSets",
                columns: new[] { "TenantId", "Species", "Purpose" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyFeedingEntries",
                schema: "feeding");

            migrationBuilder.DropTable(
                name: "FeedingCycleReconciliationLines",
                schema: "feeding");

            migrationBuilder.DropTable(
                name: "FeedingPlanExclusions",
                schema: "feeding");

            migrationBuilder.DropTable(
                name: "FeedingRuleLines",
                schema: "feeding");

            migrationBuilder.DropTable(
                name: "FeedingCycleReconciliations",
                schema: "feeding");

            migrationBuilder.DropTable(
                name: "AnimalFeedingPlans",
                schema: "feeding");

            migrationBuilder.DropTable(
                name: "FeedingRuleSets",
                schema: "feeding");

            migrationBuilder.DropIndex(
                name: "IX_FeedIngredients_InventoryItemId",
                schema: "feeding",
                table: "FeedIngredients");

            migrationBuilder.DropIndex(
                name: "IX_FeedConsumptionLogs_FeedingPlanId",
                schema: "feeding",
                table: "FeedConsumptionLogs");

            migrationBuilder.DropColumn(
                name: "InventoryItemId",
                schema: "feeding",
                table: "FeedIngredients");

            migrationBuilder.DropColumn(
                name: "FeedingPlanId",
                schema: "feeding",
                table: "FeedConsumptionLogs");
        }
    }
}
