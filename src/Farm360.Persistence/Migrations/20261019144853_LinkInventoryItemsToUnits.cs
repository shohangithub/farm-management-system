using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkInventoryItemsToUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UnitOfMeasure",
                schema: "inventory",
                table: "InventoryItems",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<Guid>(
                name: "BaseUnitId",
                schema: "inventory",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            // Link existing items to their tenant's base unit by name (case-insensitive). Items whose
            // unit name has no base unit yet are linked by the app on first use (it creates the unit).
            migrationBuilder.Sql(@"
UPDATE i SET i.BaseUnitId = u.Id
FROM inventory.InventoryItems i
CROSS APPLY (SELECT TOP 1 m.Id FROM inventory.MeasureUnits m
             WHERE m.TenantId = i.TenantId AND m.IsDeleted = 0
               AND LOWER(LTRIM(RTRIM(m.Name))) = LOWER(LTRIM(RTRIM(i.UnitOfMeasure)))) u
WHERE i.BaseUnitId IS NULL;");

            // A default unit that no longer exists (or belongs to another base unit) is cleared before the link is enforced.
            migrationBuilder.Sql(@"
UPDATE i SET i.DefaultUnitId = NULL
FROM inventory.InventoryItems i
LEFT JOIN inventory.UnitConversions c ON c.Id = i.DefaultUnitId
WHERE i.DefaultUnitId IS NOT NULL
  AND (c.Id IS NULL OR (i.BaseUnitId IS NOT NULL AND c.MeasureUnitId <> i.BaseUnitId));");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_BaseUnitId",
                schema: "inventory",
                table: "InventoryItems",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_DefaultUnitId",
                schema: "inventory",
                table: "InventoryItems",
                column: "DefaultUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_MeasureUnits_BaseUnitId",
                schema: "inventory",
                table: "InventoryItems",
                column: "BaseUnitId",
                principalSchema: "inventory",
                principalTable: "MeasureUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_UnitConversions_DefaultUnitId",
                schema: "inventory",
                table: "InventoryItems",
                column: "DefaultUnitId",
                principalSchema: "inventory",
                principalTable: "UnitConversions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_MeasureUnits_BaseUnitId",
                schema: "inventory",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_UnitConversions_DefaultUnitId",
                schema: "inventory",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_BaseUnitId",
                schema: "inventory",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_DefaultUnitId",
                schema: "inventory",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "BaseUnitId",
                schema: "inventory",
                table: "InventoryItems");

            migrationBuilder.AlterColumn<string>(
                name: "UnitOfMeasure",
                schema: "inventory",
                table: "InventoryItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);
        }
    }
}
