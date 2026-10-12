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
            // Conditional drop: some databases were built without this index (not from the full
            // migration chain). An unconditional DropIndex failed there and, because migrations run
            // in order, blocked every later migration -- including the one adding Animals.DeathDate.
            migrationBuilder.Sql(DropIndexIfExists);

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
            migrationBuilder.Sql(DropIndexIfExists);

            migrationBuilder.CreateIndex(
                name: "UX_AnimalOverheadAllocations_Transaction_Animal",
                schema: "finance",
                table: "AnimalOverheadAllocations",
                columns: new[] { "SourceTransactionId", "AnimalId" },
                unique: true);
        }

        private const string DropIndexIfExists = @"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = N'UX_AnimalOverheadAllocations_Transaction_Animal'
             AND object_id = OBJECT_ID(N'finance.AnimalOverheadAllocations'))
    DROP INDEX [UX_AnimalOverheadAllocations_Transaction_Animal] ON [finance].[AnimalOverheadAllocations];";
    }
}
