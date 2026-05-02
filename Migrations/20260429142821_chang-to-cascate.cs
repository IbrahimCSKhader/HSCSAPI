using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class changtocascate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuthorizedMembers_Users_AuthorizedMemberId",
                table: "AuthorizedMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Doctors_Users_DoctorId",
                table: "Doctors");

            migrationBuilder.DropForeignKey(
                name: "FK_LaboratoryTechnologists_Users_LaboratoryTechnologistId",
                table: "LaboratoryTechnologists");

            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Users_PatientId",
                table: "Patients");

            migrationBuilder.DropForeignKey(
                name: "FK_RadiologyTechnologists_Users_RadiologyTechnologistId",
                table: "RadiologyTechnologists");

            migrationBuilder.DropForeignKey(
                name: "FK_Secretaries_Users_SecretaryId",
                table: "Secretaries");

            migrationBuilder.AddForeignKey(
                name: "FK_AuthorizedMembers_Users_AuthorizedMemberId",
                table: "AuthorizedMembers",
                column: "AuthorizedMemberId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Doctors_Users_DoctorId",
                table: "Doctors",
                column: "DoctorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LaboratoryTechnologists_Users_LaboratoryTechnologistId",
                table: "LaboratoryTechnologists",
                column: "LaboratoryTechnologistId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Users_PatientId",
                table: "Patients",
                column: "PatientId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RadiologyTechnologists_Users_RadiologyTechnologistId",
                table: "RadiologyTechnologists",
                column: "RadiologyTechnologistId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Secretaries_Users_SecretaryId",
                table: "Secretaries",
                column: "SecretaryId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuthorizedMembers_Users_AuthorizedMemberId",
                table: "AuthorizedMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Doctors_Users_DoctorId",
                table: "Doctors");

            migrationBuilder.DropForeignKey(
                name: "FK_LaboratoryTechnologists_Users_LaboratoryTechnologistId",
                table: "LaboratoryTechnologists");

            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Users_PatientId",
                table: "Patients");

            migrationBuilder.DropForeignKey(
                name: "FK_RadiologyTechnologists_Users_RadiologyTechnologistId",
                table: "RadiologyTechnologists");

            migrationBuilder.DropForeignKey(
                name: "FK_Secretaries_Users_SecretaryId",
                table: "Secretaries");

            migrationBuilder.AddForeignKey(
                name: "FK_AuthorizedMembers_Users_AuthorizedMemberId",
                table: "AuthorizedMembers",
                column: "AuthorizedMemberId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Doctors_Users_DoctorId",
                table: "Doctors",
                column: "DoctorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LaboratoryTechnologists_Users_LaboratoryTechnologistId",
                table: "LaboratoryTechnologists",
                column: "LaboratoryTechnologistId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Users_PatientId",
                table: "Patients",
                column: "PatientId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RadiologyTechnologists_Users_RadiologyTechnologistId",
                table: "RadiologyTechnologists",
                column: "RadiologyTechnologistId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Secretaries_Users_SecretaryId",
                table: "Secretaries",
                column: "SecretaryId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
