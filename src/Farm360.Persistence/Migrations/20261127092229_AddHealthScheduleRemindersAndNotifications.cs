using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm360.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthScheduleRemindersAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionLink",
                schema: "app",
                table: "VaccinationProtocolSteps",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DoseAmount",
                schema: "app",
                table: "VaccinationProtocolSteps",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DoseMax",
                schema: "app",
                table: "VaccinationProtocolSteps",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DoseMin",
                schema: "app",
                table: "VaccinationProtocolSteps",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DosePerKg",
                schema: "app",
                table: "VaccinationProtocolSteps",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DoseRounding",
                schema: "app",
                table: "VaccinationProtocolSteps",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "DoseUnit",
                schema: "app",
                table: "VaccinationProtocolSteps",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DoseUnitId",
                schema: "app",
                table: "VaccinationProtocolSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "app",
                table: "VaccinationProtocolSteps",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medicine");

            migrationBuilder.AddColumn<int>(
                name: "RepeatEveryDays",
                schema: "app",
                table: "VaccinationProtocolSteps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoStartForPurchased",
                schema: "app",
                table: "VaccinationProtocols",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ActionLink",
                schema: "app",
                table: "VaccinationEvents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DoseAmount",
                schema: "app",
                table: "VaccinationEvents",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DoseMax",
                schema: "app",
                table: "VaccinationEvents",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DoseMin",
                schema: "app",
                table: "VaccinationEvents",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DosePerKg",
                schema: "app",
                table: "VaccinationEvents",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DoseRounding",
                schema: "app",
                table: "VaccinationEvents",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "DoseUnit",
                schema: "app",
                table: "VaccinationEvents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DoseUnitId",
                schema: "app",
                table: "VaccinationEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DoseWeightEstimated",
                schema: "app",
                table: "VaccinationEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DoseWeightKg",
                schema: "app",
                table: "VaccinationEvents",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instruction",
                schema: "app",
                table: "VaccinationEvents",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "app",
                table: "VaccinationEvents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medicine");

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousEventId",
                schema: "app",
                table: "VaccinationEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProtocolId",
                schema: "app",
                table: "VaccinationEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatEveryDays",
                schema: "app",
                table: "VaccinationEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkipReason",
                schema: "app",
                table: "VaccinationEvents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SkippedDate",
                schema: "app",
                table: "VaccinationEvents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StepName",
                schema: "app",
                table: "VaccinationEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "app",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DedupKey",
                schema: "app",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FarmId",
                schema: "app",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Link",
                schema: "app",
                table: "Notifications",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationEvents_PreviousEventId",
                schema: "app",
                table: "VaccinationEvents",
                column: "PreviousEventId");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationEvents_Tenant_Status_Date",
                schema: "app",
                table: "VaccinationEvents",
                columns: new[] { "TenantId", "Status", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "UX_Notifications_User_DedupKey",
                schema: "app",
                table: "Notifications",
                columns: new[] { "TenantId", "UserId", "DedupKey" },
                unique: true,
                filter: "[DedupKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VaccinationEvents_PreviousEventId",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropIndex(
                name: "IX_VaccinationEvents_Tenant_Status_Date",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropIndex(
                name: "UX_Notifications_User_DedupKey",
                schema: "app",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ActionLink",
                schema: "app",
                table: "VaccinationProtocolSteps");

            migrationBuilder.DropColumn(
                name: "DoseAmount",
                schema: "app",
                table: "VaccinationProtocolSteps");

            migrationBuilder.DropColumn(
                name: "DoseMax",
                schema: "app",
                table: "VaccinationProtocolSteps");

            migrationBuilder.DropColumn(
                name: "DoseMin",
                schema: "app",
                table: "VaccinationProtocolSteps");

            migrationBuilder.DropColumn(
                name: "DosePerKg",
                schema: "app",
                table: "VaccinationProtocolSteps");

            migrationBuilder.DropColumn(
                name: "DoseRounding",
                schema: "app",
                table: "VaccinationProtocolSteps");

            migrationBuilder.DropColumn(
                name: "DoseUnit",
                schema: "app",
                table: "VaccinationProtocolSteps");

            migrationBuilder.DropColumn(
                name: "DoseUnitId",
                schema: "app",
                table: "VaccinationProtocolSteps");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "app",
                table: "VaccinationProtocolSteps");

            migrationBuilder.DropColumn(
                name: "RepeatEveryDays",
                schema: "app",
                table: "VaccinationProtocolSteps");

            migrationBuilder.DropColumn(
                name: "AutoStartForPurchased",
                schema: "app",
                table: "VaccinationProtocols");

            migrationBuilder.DropColumn(
                name: "ActionLink",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "DoseAmount",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "DoseMax",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "DoseMin",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "DosePerKg",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "DoseRounding",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "DoseUnit",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "DoseUnitId",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "DoseWeightEstimated",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "DoseWeightKg",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "Instruction",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "PreviousEventId",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "ProtocolId",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "RepeatEveryDays",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "SkippedDate",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "StepName",
                schema: "app",
                table: "VaccinationEvents");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "app",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DedupKey",
                schema: "app",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "FarmId",
                schema: "app",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Link",
                schema: "app",
                table: "Notifications");
        }
    }
}
