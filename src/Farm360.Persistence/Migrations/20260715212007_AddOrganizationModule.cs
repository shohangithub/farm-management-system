using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Organizations_OrganizationId",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "app",
                table: "Organizations");

            migrationBuilder.RenameColumn(
                name: "Phone",
                schema: "app",
                table: "Organizations",
                newName: "AddressZipCode");

            migrationBuilder.RenameColumn(
                name: "Email",
                schema: "app",
                table: "Organizations",
                newName: "AddressStreet");

            migrationBuilder.RenameColumn(
                name: "Address",
                schema: "app",
                table: "Organizations",
                newName: "LogoUrl");

            migrationBuilder.AddColumn<string>(
                name: "AddressCity",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressCountry",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressState",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessRegistrationNumber",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaxIdentificationNumber",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TradeLicenseNumber",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_TenantId_Name",
                schema: "app",
                table: "Organizations",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Organizations_OrganizationId",
                schema: "app",
                table: "Branches",
                column: "OrganizationId",
                principalSchema: "app",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Organizations_OrganizationId",
                schema: "app",
                table: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_TenantId_Name",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AddressCity",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AddressCountry",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AddressState",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BusinessRegistrationNumber",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "TaxIdentificationNumber",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                schema: "app",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "TradeLicenseNumber",
                schema: "app",
                table: "Organizations");

            migrationBuilder.RenameColumn(
                name: "LogoUrl",
                schema: "app",
                table: "Organizations",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "AddressZipCode",
                schema: "app",
                table: "Organizations",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "AddressStreet",
                schema: "app",
                table: "Organizations",
                newName: "Email");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "app",
                table: "Organizations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "app",
                table: "Organizations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Organizations_OrganizationId",
                schema: "app",
                table: "Branches",
                column: "OrganizationId",
                principalSchema: "app",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
