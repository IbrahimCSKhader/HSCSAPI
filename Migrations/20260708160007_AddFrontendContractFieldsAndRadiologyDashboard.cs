using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddFrontendContractFieldsAndRadiologyDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MedicationRemindersEnabled",
                table: "ReminderPreferences",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SmsRemindersEnabled",
                table: "ReminderPreferences",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewAppointments",
                table: "PatientAuthorizedMembers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewRecords",
                table: "PatientAuthorizedMembers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.AddColumn<bool>(
                name: "CanViewAppointments",
                table: "Invites",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewRecords",
                table: "Invites",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Specialty",
                table: "Doctors",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "GeneralPractitioner");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MedicationRemindersEnabled",
                table: "ReminderPreferences");

            migrationBuilder.DropColumn(
                name: "SmsRemindersEnabled",
                table: "ReminderPreferences");

            migrationBuilder.DropColumn(
                name: "CanViewAppointments",
                table: "PatientAuthorizedMembers");

            migrationBuilder.DropColumn(
                name: "CanViewRecords",
                table: "PatientAuthorizedMembers");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CanViewAppointments",
                table: "Invites");

            migrationBuilder.DropColumn(
                name: "CanViewRecords",
                table: "Invites");

            migrationBuilder.DropColumn(
                name: "Specialty",
                table: "Doctors");
        }
    }
}
