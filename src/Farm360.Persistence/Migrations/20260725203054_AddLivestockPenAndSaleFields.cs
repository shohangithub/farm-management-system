using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLivestockPenAndSaleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuyerName",
                schema: "app",
                table: "Animals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PenId",
                schema: "app",
                table: "Animals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SaleWeightKg",
                schema: "app",
                table: "Animals",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyerName",
                schema: "app",
                table: "Animals");

            migrationBuilder.DropColumn(
                name: "PenId",
                schema: "app",
                table: "Animals");

            migrationBuilder.DropColumn(
                name: "SaleWeightKg",
                schema: "app",
                table: "Animals");
        }
    }
}
