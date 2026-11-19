using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeVetVisitTenantScoped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "app",
                table: "VetVisits",
                newName: "CreatedAtUtc");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "app",
                table: "VetVisits",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                schema: "app",
                table: "VetVisits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                schema: "app",
                table: "VetVisits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "app",
                table: "VetVisits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                schema: "app",
                table: "VetVisits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "app",
                table: "VetVisits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "app",
                table: "VetVisits",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: Array.Empty<byte>());

            // VetVisit is now an ITenantEntity, so the global filter scopes it by TenantId.
            // TenantId already exists (NOT NULL, indexed by IX_VetVisits_TenantId_Date), but rows
            // written by a tenant-less session carry Guid.Empty. Backfill them from their farm.
            migrationBuilder.Sql(@"
UPDATE v
SET    v.TenantId = f.TenantId
FROM   app.VetVisits v
INNER JOIN app.Farms f ON f.Id = v.FarmId
WHERE  v.TenantId = '00000000-0000-0000-0000-000000000000'
  AND  f.TenantId <> '00000000-0000-0000-0000-000000000000';");

            // Rows whose farm cannot be resolved stay unowned: soft-delete them so they are visible
            // to nobody (Guid.Empty is no longer a shared tenant for VetVisit), never to everyone.
            migrationBuilder.Sql(@"
UPDATE app.VetVisits
SET    IsDeleted = 1,
       DeletedAtUtc = SYSUTCDATETIME()
WHERE  TenantId = '00000000-0000-0000-0000-000000000000';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "app",
                table: "VetVisits");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "app",
                table: "VetVisits");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "app",
                table: "VetVisits");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "app",
                table: "VetVisits");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                schema: "app",
                table: "VetVisits");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "app",
                table: "VetVisits");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "app",
                table: "VetVisits");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                schema: "app",
                table: "VetVisits",
                newName: "CreatedAt");
        }
    }
}
