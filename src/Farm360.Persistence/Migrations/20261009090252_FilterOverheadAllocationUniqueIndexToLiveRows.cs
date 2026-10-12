using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FilterOverheadAllocationUniqueIndexToLiveRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AnimalOverheadAllocations_Transaction_Animal",
                schema: "finance",
                table: "AnimalOverheadAllocations");

            migrationBuilder.CreateIndex(
                name: "UX_AnimalOverheadAllocations_Transaction_Animal",
                schema: "finance",
                table: "AnimalOverheadAllocations",
                columns: new[] { "SourceTransactionId", "AnimalId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AnimalOverheadAllocations_Transaction_Animal",
                schema: "finance",
                table: "AnimalOverheadAllocations");

            migrationBuilder.CreateIndex(
                name: "UX_AnimalOverheadAllocations_Transaction_Animal",
                schema: "finance",
                table: "AnimalOverheadAllocations",
                columns: new[] { "SourceTransactionId", "AnimalId" },
                unique: true);
        }
    }
}
