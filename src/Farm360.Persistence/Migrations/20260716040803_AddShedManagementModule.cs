using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShedManagementModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sheds",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShedNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ShedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    CurrentOccupancy = table.Column<int>(type: "int", nullable: false),
                    AnimalType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FloorType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoofType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HasVentilation = table.Column<bool>(type: "bit", nullable: false),
                    HasWaterLine = table.Column<bool>(type: "bit", nullable: false),
                    HasFeedLine = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_Sheds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sheds_Tenant_Farm_ShedNumber",
                schema: "app",
                table: "Sheds",
                columns: new[] { "TenantId", "FarmId", "ShedNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sheds_TenantId",
                schema: "app",
                table: "Sheds",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sheds",
                schema: "app");
        }
    }
}
