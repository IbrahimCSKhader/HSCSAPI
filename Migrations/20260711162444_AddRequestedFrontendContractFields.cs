using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestedFrontendContractFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FromDate",
                table: "Reports",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ToDate",
                table: "Reports",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionPath",
                table: "Notifications",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivityCode",
                table: "MedicalFiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivityName",
                table: "MedicalFiles",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiagnosisCode",
                table: "MedicalFiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiagnosisName",
                table: "MedicalFiles",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TreatmentId",
                table: "Appointments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TreatmentName",
                table: "Appointments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromDate",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ToDate",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ActionPath",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ActivityCode",
                table: "MedicalFiles");

            migrationBuilder.DropColumn(
                name: "ActivityName",
                table: "MedicalFiles");

            migrationBuilder.DropColumn(
                name: "DiagnosisCode",
                table: "MedicalFiles");

            migrationBuilder.DropColumn(
                name: "DiagnosisName",
                table: "MedicalFiles");

            migrationBuilder.DropColumn(
                name: "TreatmentId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "TreatmentName",
                table: "Appointments");
        }
    }
}
