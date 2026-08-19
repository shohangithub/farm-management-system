using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddInventoryToHealthEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DosageQuantity",
                schema: "app",
                table: "VaccinationProtocolSteps",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryItemId",
                schema: "app",
                table: "VaccinationProtocolSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DosageQuantity",
                schema: "app",
                table: "VaccinationEvents",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryItemId",
                schema: "app",
                table: "VaccinationEvents",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DosageQuantity",
                schema: "app",
                table: "VaccinationProtocolSteps");

            migrationBuilder.DropColumn(
                name: "InventoryItemId",
                schema: "app",
                table: "VaccinationProtocolSteps");

            migrationBuilder.DropColumn(
                name: "DosageQuantity",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "InventoryItemId",
                schema: "app",
                table: "VaccinationEvents");
        }
    }
}
