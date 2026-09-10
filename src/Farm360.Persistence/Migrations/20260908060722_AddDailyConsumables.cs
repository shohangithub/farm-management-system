using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyConsumables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsumableUsagePlans",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_ConsumableUsagePlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsumableUsagePlanItems",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumableUsagePlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedQuantityPerDay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumableUsagePlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumableUsagePlanItems_ConsumableUsagePlans_ConsumableUsagePlanId",
                        column: x => x.ConsumableUsagePlanId,
                        principalSchema: "inventory",
                        principalTable: "ConsumableUsagePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConsumableUsagePlanItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "inventory",
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyConsumableEntries",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumableUsagePlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumableUsagePlanItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ActualQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitCostAtConsumptionBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalCostBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_DailyConsumableEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyConsumableEntries_ConsumableUsagePlans_ConsumableUsagePlanId",
                        column: x => x.ConsumableUsagePlanId,
                        principalSchema: "inventory",
                        principalTable: "ConsumableUsagePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DailyConsumableEntries_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "inventory",
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableUsagePlanItems_ConsumableUsagePlanId_InventoryItemId",
                schema: "inventory",
                table: "ConsumableUsagePlanItems",
                columns: new[] { "ConsumableUsagePlanId", "InventoryItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableUsagePlanItems_InventoryItemId",
                schema: "inventory",
                table: "ConsumableUsagePlanItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableUsagePlans_TenantId_FarmId_Status",
                schema: "inventory",
                table: "ConsumableUsagePlans",
                columns: new[] { "TenantId", "FarmId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyConsumableEntries_ConsumableUsagePlanId_ConsumableUsagePlanItemId_EntryDate",
                schema: "inventory",
                table: "DailyConsumableEntries",
                columns: new[] { "ConsumableUsagePlanId", "ConsumableUsagePlanItemId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyConsumableEntries_InventoryItemId",
                schema: "inventory",
                table: "DailyConsumableEntries",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyConsumableEntries_TenantId_FarmId_EntryDate",
                schema: "inventory",
                table: "DailyConsumableEntries",
                columns: new[] { "TenantId", "FarmId", "EntryDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumableUsagePlanItems",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "DailyConsumableEntries",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ConsumableUsagePlans",
                schema: "inventory");
        }
    }
}
