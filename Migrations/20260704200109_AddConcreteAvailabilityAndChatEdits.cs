using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddConcreteAvailabilityAndChatEdits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AvailabilitySlots_DoctorId_DayOfWeek_StartTime_EndTime",
                table: "AvailabilitySlots");

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "ChatMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "AvailabilitySlots",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SlotDate",
                table: "AvailabilitySlots",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.Sql(
                """
                UPDATE availability
                SET SlotDate = COALESCE(
                    (SELECT MIN(appointment.AppointmentDate)
                     FROM Appointments appointment
                     WHERE appointment.AvailabilitySlotId = availability.AvailabilitySlotId),
                    DATEADD(day,
                        (availability.DayOfWeek - (DATEDIFF(day, '19000107', CAST(SYSUTCDATETIME() AS date)) % 7) + 7) % 7,
                        CAST(SYSUTCDATETIME() AS date)))
                FROM AvailabilitySlots availability;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_DoctorId_SlotDate_StartTime_EndTime",
                table: "AvailabilitySlots",
                columns: new[] { "DoctorId", "SlotDate", "StartTime", "EndTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AvailabilitySlots_DoctorId_SlotDate_StartTime_EndTime",
                table: "AvailabilitySlots");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "AvailabilitySlots");

            migrationBuilder.DropColumn(
                name: "SlotDate",
                table: "AvailabilitySlots");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_DoctorId_DayOfWeek_StartTime_EndTime",
                table: "AvailabilitySlots",
                columns: new[] { "DoctorId", "DayOfWeek", "StartTime", "EndTime" },
                unique: true);
        }
    }
}
