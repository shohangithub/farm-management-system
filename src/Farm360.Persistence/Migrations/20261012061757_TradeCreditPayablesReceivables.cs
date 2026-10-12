using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TradeCreditPayablesReceivables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradeBills",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PartyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartyPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BillDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AmountBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdjustedBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TradeBills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradeBillPayments",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AmountBdt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TradeBillPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeBillPayments_TradeBills_BillId",
                        column: x => x.BillId,
                        principalSchema: "finance",
                        principalTable: "TradeBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeBillPayments_BillId",
                schema: "finance",
                table: "TradeBillPayments",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeBillPayments_FarmId",
                schema: "finance",
                table: "TradeBillPayments",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeBillPayments_TenantId",
                schema: "finance",
                table: "TradeBillPayments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeBills_FarmId_Kind",
                schema: "finance",
                table: "TradeBills",
                columns: new[] { "FarmId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeBills_FarmId_SourceDocumentId_Category",
                schema: "finance",
                table: "TradeBills",
                columns: new[] { "FarmId", "SourceDocumentId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeBills_TenantId",
                schema: "finance",
                table: "TradeBills",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeBillPayments",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "TradeBills",
                schema: "finance");
        }
    }
}
