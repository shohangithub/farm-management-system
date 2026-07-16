using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchManagementFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GpsCoordinates",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "app",
                table: "Branches");

            migrationBuilder.AddColumn<string>(
                name: "Address_City",
                schema: "app",
                table: "Branches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address_Country",
                schema: "app",
                table: "Branches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address_State",
                schema: "app",
                table: "Branches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address_Street",
                schema: "app",
                table: "Branches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address_ZipCode",
                schema: "app",
                table: "Branches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchCode",
                schema: "app",
                table: "Branches",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                schema: "app",
                table: "Branches",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                schema: "app",
                table: "Branches",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HolidayCalendar",
                schema: "app",
                table: "Branches",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                schema: "app",
                table: "Branches",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                schema: "app",
                table: "Branches",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "app",
                table: "Branches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WorkingHours",
                schema: "app",
                table: "Branches",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId_BranchCode",
                schema: "app",
                table: "Branches",
                columns: new[] { "TenantId", "BranchCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Branches_TenantId_BranchCode",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Address_City",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Address_Country",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Address_State",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Address_Street",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Address_ZipCode",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "BranchCode",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "HolidayCalendar",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Latitude",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Longitude",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "WorkingHours",
                schema: "app",
                table: "Branches");

            migrationBuilder.AddColumn<string>(
                name: "GpsCoordinates",
                schema: "app",
                table: "Branches",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "app",
                table: "Branches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
