using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddIsDewormingToVaccinationProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeworming",
                schema: "app",
                table: "VaccinationProtocols",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeworming",
                schema: "app",
                table: "VaccinationProtocols");
        }
    }
}
