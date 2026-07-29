using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddBreedManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreedName",
                schema: "app",
                table: "Animals");

            migrationBuilder.EnsureSchema(
                name: "intelligence");

            migrationBuilder.AddColumn<Guid>(
                name: "BreedId",
                schema: "app",
                table: "Animals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ActionableInsights",
                schema: "intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActionData = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    IsDismissed = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ActionableInsights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Breeds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MainPurpose = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BestFor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AdgPoorManagement = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdgAverageFarm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdgGoodCommercialFarm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdgIntensiveFattening = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StandardAdgMin = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StandardAdgMax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FcrMin = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FcrMax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MilkYieldMinLiters = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MilkYieldMaxLiters = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FatPercentageMin = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FatPercentageMax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Breeds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionableInsights_AnimalId",
                schema: "intelligence",
                table: "ActionableInsights",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionableInsights_BatchId",
                schema: "intelligence",
                table: "ActionableInsights",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionableInsights_FarmId",
                schema: "intelligence",
                table: "ActionableInsights",
                column: "FarmId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionableInsights",
                schema: "intelligence");

            migrationBuilder.DropTable(
                name: "Breeds");

            migrationBuilder.DropColumn(
                name: "BreedId",
                schema: "app",
                table: "Animals");

            migrationBuilder.AddColumn<string>(
                name: "BreedName",
                schema: "app",
                table: "Animals",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
