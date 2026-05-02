using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class changtocascateee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Users_PatientId",
                table: "Patients");

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Users_PatientId",
                table: "Patients",
                column: "PatientId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Users_PatientId",
                table: "Patients");

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Users_PatientId",
                table: "Patients",
                column: "PatientId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
