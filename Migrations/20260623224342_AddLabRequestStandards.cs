using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddLabRequestStandards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClinicalNotes",
                table: "LabTestRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoincCode",
                table: "LabTestRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PatientId",
                table: "LabTestRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "LabTestRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Routine");

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAt",
                table: "LabTestRequests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByDoctorId",
                table: "LabTestRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TestingClinicId",
                table: "LabTestRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTestRequests_LoincCode",
                table: "LabTestRequests",
                column: "LoincCode");

            migrationBuilder.CreateIndex(
                name: "IX_LabTestRequests_PatientId",
                table: "LabTestRequests",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTestRequests_RequestedAt",
                table: "LabTestRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LabTestRequests_RequestedByDoctorId",
                table: "LabTestRequests",
                column: "RequestedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTestRequests_TestingClinicId",
                table: "LabTestRequests",
                column: "TestingClinicId");

            migrationBuilder.AddForeignKey(
                name: "FK_LabTestRequests_Clinics_TestingClinicId",
                table: "LabTestRequests",
                column: "TestingClinicId",
                principalTable: "Clinics",
                principalColumn: "ClinicId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LabTestRequests_Doctors_RequestedByDoctorId",
                table: "LabTestRequests",
                column: "RequestedByDoctorId",
                principalTable: "Doctors",
                principalColumn: "DoctorId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LabTestRequests_Patients_PatientId",
                table: "LabTestRequests",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "PatientId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabTestRequests_Clinics_TestingClinicId",
                table: "LabTestRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_LabTestRequests_Doctors_RequestedByDoctorId",
                table: "LabTestRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_LabTestRequests_Patients_PatientId",
                table: "LabTestRequests");

            migrationBuilder.DropIndex(
                name: "IX_LabTestRequests_LoincCode",
                table: "LabTestRequests");

            migrationBuilder.DropIndex(
                name: "IX_LabTestRequests_PatientId",
                table: "LabTestRequests");

            migrationBuilder.DropIndex(
                name: "IX_LabTestRequests_RequestedAt",
                table: "LabTestRequests");

            migrationBuilder.DropIndex(
                name: "IX_LabTestRequests_RequestedByDoctorId",
                table: "LabTestRequests");

            migrationBuilder.DropIndex(
                name: "IX_LabTestRequests_TestingClinicId",
                table: "LabTestRequests");

            migrationBuilder.DropColumn(
                name: "ClinicalNotes",
                table: "LabTestRequests");

            migrationBuilder.DropColumn(
                name: "LoincCode",
                table: "LabTestRequests");

            migrationBuilder.DropColumn(
                name: "PatientId",
                table: "LabTestRequests");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "LabTestRequests");

            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "LabTestRequests");

            migrationBuilder.DropColumn(
                name: "RequestedByDoctorId",
                table: "LabTestRequests");

            migrationBuilder.DropColumn(
                name: "TestingClinicId",
                table: "LabTestRequests");
        }
    }
}
