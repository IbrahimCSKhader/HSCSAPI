using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddImagingRequestWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyRegion",
                table: "ImagingTestRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClinicalNotes",
                table: "ImagingTestRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagingCode",
                table: "ImagingTestRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PatientId",
                table: "ImagingTestRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "ImagingTestRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Routine");

            migrationBuilder.AddColumn<Guid>(
                name: "RadiologyClinicId",
                table: "ImagingTestRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAt",
                table: "ImagingTestRequests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByDoctorId",
                table: "ImagingTestRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImagingTestRequests_ImagingCode",
                table: "ImagingTestRequests",
                column: "ImagingCode");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingTestRequests_PatientId",
                table: "ImagingTestRequests",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingTestRequests_RadiologyClinicId",
                table: "ImagingTestRequests",
                column: "RadiologyClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingTestRequests_RequestedAt",
                table: "ImagingTestRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingTestRequests_RequestedByDoctorId",
                table: "ImagingTestRequests",
                column: "RequestedByDoctorId");

            migrationBuilder.AddForeignKey(
                name: "FK_ImagingTestRequests_Clinics_RadiologyClinicId",
                table: "ImagingTestRequests",
                column: "RadiologyClinicId",
                principalTable: "Clinics",
                principalColumn: "ClinicId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ImagingTestRequests_Doctors_RequestedByDoctorId",
                table: "ImagingTestRequests",
                column: "RequestedByDoctorId",
                principalTable: "Doctors",
                principalColumn: "DoctorId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ImagingTestRequests_Patients_PatientId",
                table: "ImagingTestRequests",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "PatientId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImagingTestRequests_Clinics_RadiologyClinicId",
                table: "ImagingTestRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ImagingTestRequests_Doctors_RequestedByDoctorId",
                table: "ImagingTestRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ImagingTestRequests_Patients_PatientId",
                table: "ImagingTestRequests");

            migrationBuilder.DropIndex(
                name: "IX_ImagingTestRequests_ImagingCode",
                table: "ImagingTestRequests");

            migrationBuilder.DropIndex(
                name: "IX_ImagingTestRequests_PatientId",
                table: "ImagingTestRequests");

            migrationBuilder.DropIndex(
                name: "IX_ImagingTestRequests_RadiologyClinicId",
                table: "ImagingTestRequests");

            migrationBuilder.DropIndex(
                name: "IX_ImagingTestRequests_RequestedAt",
                table: "ImagingTestRequests");

            migrationBuilder.DropIndex(
                name: "IX_ImagingTestRequests_RequestedByDoctorId",
                table: "ImagingTestRequests");

            migrationBuilder.DropColumn(
                name: "BodyRegion",
                table: "ImagingTestRequests");

            migrationBuilder.DropColumn(
                name: "ClinicalNotes",
                table: "ImagingTestRequests");

            migrationBuilder.DropColumn(
                name: "ImagingCode",
                table: "ImagingTestRequests");

            migrationBuilder.DropColumn(
                name: "PatientId",
                table: "ImagingTestRequests");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ImagingTestRequests");

            migrationBuilder.DropColumn(
                name: "RadiologyClinicId",
                table: "ImagingTestRequests");

            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "ImagingTestRequests");

            migrationBuilder.DropColumn(
                name: "RequestedByDoctorId",
                table: "ImagingTestRequests");
        }
    }
}
