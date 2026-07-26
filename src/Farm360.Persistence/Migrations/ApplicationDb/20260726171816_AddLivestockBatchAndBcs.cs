using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddLivestockBatchAndBcs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                schema: "app",
                table: "Animals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LatestBcs",
                schema: "app",
                table: "Animals",
                type: "decimal(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnimalBatches",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AnimalBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BodyConditionScores",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    RecordedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EvaluatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BodyConditionScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BodyConditionScores_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalSchema: "app",
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Animals_BatchId",
                schema: "app",
                table: "Animals",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalBatches_TenantId",
                schema: "app",
                table: "AnimalBatches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalBatches_TenantId_FarmId",
                schema: "app",
                table: "AnimalBatches",
                columns: new[] { "TenantId", "FarmId" });

            migrationBuilder.CreateIndex(
                name: "IX_BodyConditionScores_AnimalId",
                schema: "app",
                table: "BodyConditionScores",
                column: "AnimalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Animals_AnimalBatches_BatchId",
                schema: "app",
                table: "Animals",
                column: "BatchId",
                principalSchema: "app",
                principalTable: "AnimalBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Animals_AnimalBatches_BatchId",
                schema: "app",
                table: "Animals");

            migrationBuilder.DropTable(
                name: "AnimalBatches",
                schema: "app");

            migrationBuilder.DropTable(
                name: "BodyConditionScores",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_Animals_BatchId",
                schema: "app",
                table: "Animals");

            migrationBuilder.DropColumn(
                name: "BatchId",
                schema: "app",
                table: "Animals");

            migrationBuilder.DropColumn(
                name: "LatestBcs",
                schema: "app",
                table: "Animals");
        }
    }
}
