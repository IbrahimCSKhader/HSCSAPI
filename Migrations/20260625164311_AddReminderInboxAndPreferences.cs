using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderInboxAndPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reminders_AuthorizedMemberId",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_DoctorId",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_PatientId",
                table: "Reminders");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Reminders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Reminders",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "DismissedAt",
                table: "Reminders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Reminders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Reminder");

            migrationBuilder.CreateTable(
                name: "ReminderPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentRemindersEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LabResultRemindersEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MessageRemindersEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    InAppNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EmailRemindersEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ReminderPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_AuthorizedMemberId_DismissedAt_ReminderAt",
                table: "Reminders",
                columns: new[] { "AuthorizedMemberId", "DismissedAt", "ReminderAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_DoctorId_DismissedAt_ReminderAt",
                table: "Reminders",
                columns: new[] { "DoctorId", "DismissedAt", "ReminderAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_PatientId_DismissedAt_ReminderAt",
                table: "Reminders",
                columns: new[] { "PatientId", "DismissedAt", "ReminderAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReminderPreferences");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_AuthorizedMemberId_DismissedAt_ReminderAt",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_DoctorId_DismissedAt_ReminderAt",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_PatientId_DismissedAt_ReminderAt",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "DismissedAt",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Reminders");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_AuthorizedMemberId",
                table: "Reminders",
                column: "AuthorizedMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_DoctorId",
                table: "Reminders",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_PatientId",
                table: "Reminders",
                column: "PatientId");
        }
    }
}
