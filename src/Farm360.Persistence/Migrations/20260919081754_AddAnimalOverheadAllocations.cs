using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimalOverheadAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnimalOverheadAllocations",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Bucket = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    AllocatedAmountBdt = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    HeadDays = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WeightAtAllocationKg = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ShareFactor = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    HeadCountAtAllocation = table.Column<int>(type: "int", nullable: false),
                    IsBackfilled = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AnimalOverheadAllocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalOverheadAllocations_Farm_Period",
                schema: "finance",
                table: "AnimalOverheadAllocations",
                columns: new[] { "FarmId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalOverheadAllocations_Tenant_Animal",
                schema: "finance",
                table: "AnimalOverheadAllocations",
                columns: new[] { "TenantId", "AnimalId" });

            migrationBuilder.CreateIndex(
                name: "UX_AnimalOverheadAllocations_Transaction_Animal",
                schema: "finance",
                table: "AnimalOverheadAllocations",
                columns: new[] { "SourceTransactionId", "AnimalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimalOverheadAllocations",
                schema: "finance");
        }
    }
}
