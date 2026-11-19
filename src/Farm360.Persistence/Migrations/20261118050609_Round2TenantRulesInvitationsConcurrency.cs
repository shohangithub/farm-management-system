using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Round2TenantRulesInvitationsConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Conditional: some databases (e.g. copies restored without indexes) never had this index.
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TenantUsers_TenantId_UserId' AND object_id = OBJECT_ID(N'[app].[TenantUsers]'))
                DROP INDEX [IX_TenantUsers_TenantId_UserId] ON [app].[TenantUsers];");

            // Conditional: some databases (e.g. copies restored without indexes) never had this index.
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DailyConsumableEntries_ConsumableUsagePlanId_ConsumableUsagePlanItemId_EntryDate' AND object_id = OBJECT_ID(N'[inventory].[DailyConsumableEntries]'))
                DROP INDEX [IX_DailyConsumableEntries_ConsumableUsagePlanId_ConsumableUsagePlanItemId_EntryDate] ON [inventory].[DailyConsumableEntries];");

            // SQL Server cannot ALTER a column into rowversion, so drop and re-add it (values are system-generated).
            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "inventory",
                table: "InventoryItems");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "inventory",
                table: "InventoryItems",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            // SQL Server cannot ALTER a column into rowversion, so drop and re-add it (values are system-generated).
            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "feeding",
                table: "DailyFeedingEntries");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "feeding",
                table: "DailyFeedingEntries",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.CreateTable(
                name: "Invitations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SendCount = table.Column<int>(type: "int", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "app",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invitations_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "app",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantBusinessSettings",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FeedAllocation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    MetabolicWeightExponent = table.Column<double>(type: "float", nullable: true),
                    GroupPlanQuantityBasis = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FallbackAnimalWeightKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    OverheadAllocation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    OverheadCostPeriod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Valuation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DefaultDressingPercentage = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    DefaultMeatPricePerKgBdt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DefaultLiveWeightPricePerKgBdt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DefaultReportLanguage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BengaliNumeralsByDefault = table.Column<bool>(type: "bit", nullable: true),
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
                    table.PrimaryKey("PK_TenantBusinessSettings", x => x.Id);
                });

            // Duplicate daily entries (from overlapping job runs) would stop the new unique indexes
            // being created. Keep the posted (confirmed/adjusted) row, else the oldest, and soft-delete
            // only unposted duplicates. Two posted rows for one slot need a person, so stop with a clear message.
            migrationBuilder.Sql(@"
WITH d AS (
    SELECT Id, Status, ROW_NUMBER() OVER (
        PARTITION BY FeedingPlanId, RuleLineId, EntryDate
        ORDER BY CASE WHEN Status IN (2, 4) THEN 0 ELSE 1 END, CreatedAtUtc, Id) AS rn
    FROM feeding.DailyFeedingEntries
    WHERE RuleLineId IS NOT NULL AND IsDeleted = 0)
UPDATE e SET IsDeleted = 1, DeletedAtUtc = SYSUTCDATETIME()
FROM feeding.DailyFeedingEntries e JOIN d ON d.Id = e.Id
WHERE d.rn > 1 AND d.Status NOT IN (2, 4);

IF EXISTS (SELECT 1 FROM feeding.DailyFeedingEntries WHERE RuleLineId IS NOT NULL AND IsDeleted = 0
           GROUP BY FeedingPlanId, RuleLineId, EntryDate HAVING COUNT(*) > 1)
    THROW 50001, 'Round2 migration: some feeding plan/ration line/date has more than one CONFIRMED entry. Review and soft-delete (or reverse) the extra ones, then re-run the migration.', 1;

WITH c AS (
    SELECT Id, Status, ROW_NUMBER() OVER (
        PARTITION BY ConsumableUsagePlanId, ConsumableUsagePlanItemId, EntryDate
        ORDER BY CASE WHEN Status IN (2, 3) THEN 0 ELSE 1 END, CreatedAtUtc, Id) AS rn
    FROM inventory.DailyConsumableEntries
    WHERE IsDeleted = 0)
UPDATE e SET IsDeleted = 1, DeletedAtUtc = SYSUTCDATETIME()
FROM inventory.DailyConsumableEntries e JOIN c ON c.Id = e.Id
WHERE c.rn > 1 AND c.Status NOT IN (2, 3);

IF EXISTS (SELECT 1 FROM inventory.DailyConsumableEntries WHERE IsDeleted = 0
           GROUP BY ConsumableUsagePlanId, ConsumableUsagePlanItemId, EntryDate HAVING COUNT(*) > 1)
    THROW 50002, 'Round2 migration: some consumable plan item/date has more than one CONFIRMED entry. Review and soft-delete (or reverse) the extra ones, then re-run the migration.', 1;
");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId_UserId",
                schema: "app",
                table: "TenantUsers",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_DailyFeedingEntries_Plan_RuleLine_Date",
                schema: "feeding",
                table: "DailyFeedingEntries",
                columns: new[] { "FeedingPlanId", "RuleLineId", "EntryDate" },
                unique: true,
                filter: "[RuleLineId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DailyConsumableEntries_ConsumableUsagePlanId_ConsumableUsagePlanItemId_EntryDate",
                schema: "inventory",
                table: "DailyConsumableEntries",
                columns: new[] { "ConsumableUsagePlanId", "ConsumableUsagePlanItemId", "EntryDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_BranchId",
                schema: "app",
                table: "Invitations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_RoleId",
                schema: "app",
                table: "Invitations",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TenantId_Status",
                schema: "app",
                table: "Invitations",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TokenHash",
                schema: "app",
                table: "Invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TenantBusinessSettings_TenantId_FarmId",
                schema: "app",
                table: "TenantBusinessSettings",
                columns: new[] { "TenantId", "FarmId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "TenantBusinessSettings",
                schema: "app");

            // Conditional: some databases (e.g. copies restored without indexes) never had this index.
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TenantUsers_TenantId_UserId' AND object_id = OBJECT_ID(N'[app].[TenantUsers]'))
                DROP INDEX [IX_TenantUsers_TenantId_UserId] ON [app].[TenantUsers];");

            // Conditional: some databases (e.g. copies restored without indexes) never had this index.
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_DailyFeedingEntries_Plan_RuleLine_Date' AND object_id = OBJECT_ID(N'[feeding].[DailyFeedingEntries]'))
                DROP INDEX [UX_DailyFeedingEntries_Plan_RuleLine_Date] ON [feeding].[DailyFeedingEntries];");

            // Conditional: some databases (e.g. copies restored without indexes) never had this index.
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DailyConsumableEntries_ConsumableUsagePlanId_ConsumableUsagePlanItemId_EntryDate' AND object_id = OBJECT_ID(N'[inventory].[DailyConsumableEntries]'))
                DROP INDEX [IX_DailyConsumableEntries_ConsumableUsagePlanId_ConsumableUsagePlanItemId_EntryDate] ON [inventory].[DailyConsumableEntries];");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "inventory",
                table: "InventoryItems");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "inventory",
                table: "InventoryItems",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "feeding",
                table: "DailyFeedingEntries");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "feeding",
                table: "DailyFeedingEntries",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId_UserId",
                schema: "app",
                table: "TenantUsers",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyConsumableEntries_ConsumableUsagePlanId_ConsumableUsagePlanItemId_EntryDate",
                schema: "inventory",
                table: "DailyConsumableEntries",
                columns: new[] { "ConsumableUsagePlanId", "ConsumableUsagePlanItemId", "EntryDate" });
        }
    }
}
