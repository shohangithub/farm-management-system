using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddHealthSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MortalityRecords",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeathDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CauseOfDeath = table.Column<int>(type: "int", nullable: false),
                    DiseaseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PostMortemNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EstimatedEconomicLossBdt = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    DiseaseIncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_MortalityRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MortalityRecords_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalSchema: "app",
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MortalityRecords_DiseaseIncidents_DiseaseIncidentId",
                        column: x => x.DiseaseIncidentId,
                        principalSchema: "app",
                        principalTable: "DiseaseIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VetVisits",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VisitType = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Findings = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Recommendations = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CostBdt = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    NextVisitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VetVisits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mortality_TenantId_Date",
                schema: "app",
                table: "MortalityRecords",
                columns: new[] { "TenantId", "DeathDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MortalityRecords_DiseaseIncidentId",
                schema: "app",
                table: "MortalityRecords",
                column: "DiseaseIncidentId");

            migrationBuilder.CreateIndex(
                name: "UQ_Mortality_AnimalId",
                schema: "app",
                table: "MortalityRecords",
                column: "AnimalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VetVisits_FarmId",
                schema: "app",
                table: "VetVisits",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_VetVisits_TenantId_Date",
                schema: "app",
                table: "VetVisits",
                columns: new[] { "TenantId", "VisitDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MortalityRecords",
                schema: "app");

            migrationBuilder.DropTable(
                name: "VetVisits",
                schema: "app");
        }
    }
}
