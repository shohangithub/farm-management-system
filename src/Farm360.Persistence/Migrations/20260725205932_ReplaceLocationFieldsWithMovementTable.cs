using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceLocationFieldsWithMovementTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PenId",
                schema: "app",
                table: "Animals");

            migrationBuilder.DropColumn(
                name: "ShedId",
                schema: "app",
                table: "Animals");

            migrationBuilder.CreateTable(
                name: "AnimalMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShedId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlacedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlacedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransferReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimalMovements_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalSchema: "app",
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalMovements_AnimalId",
                table: "AnimalMovements",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalMovements_AnimalId_RemovedAtUtc",
                table: "AnimalMovements",
                columns: new[] { "AnimalId", "RemovedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimalMovements");

            migrationBuilder.AddColumn<Guid>(
                name: "PenId",
                schema: "app",
                table: "Animals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShedId",
                schema: "app",
                table: "Animals",
                type: "uniqueidentifier",
                nullable: true);
        }
    }
}
