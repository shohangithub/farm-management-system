using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionAndTrialFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingCycle",
                schema: "app",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<bool>(
                name: "HasUsedTrial",
                schema: "app",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrial",
                schema: "app",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TrialDays",
                schema: "app",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAtUtc",
                schema: "app",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantSubscriptions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    BillingCycle = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IsTrial_EndsAt",
                schema: "app",
                table: "Tenants",
                columns: new[] { "IsTrial", "TrialEndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_InvoiceNumber",
                schema: "app",
                table: "TenantSubscriptions",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_TenantId_CreatedAt",
                schema: "app",
                table: "TenantSubscriptions",
                columns: new[] { "TenantId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantSubscriptions",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_IsTrial_EndsAt",
                schema: "app",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                schema: "app",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "HasUsedTrial",
                schema: "app",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsTrial",
                schema: "app",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TrialDays",
                schema: "app",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TrialEndsAtUtc",
                schema: "app",
                table: "Tenants");
        }
    }
}
