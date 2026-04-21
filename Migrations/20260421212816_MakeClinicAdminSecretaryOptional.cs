using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class MakeClinicAdminSecretaryOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clinics_AdminSecretaryId",
                table: "Clinics");

            migrationBuilder.AlterColumn<Guid>(
                name: "AdminSecretaryId",
                table: "Clinics",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_Clinics_AdminSecretaryId",
                table: "Clinics",
                column: "AdminSecretaryId",
                unique: true,
                filter: "[AdminSecretaryId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clinics_AdminSecretaryId",
                table: "Clinics");

            migrationBuilder.AlterColumn<Guid>(
                name: "AdminSecretaryId",
                table: "Clinics",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clinics_AdminSecretaryId",
                table: "Clinics",
                column: "AdminSecretaryId",
                unique: true);
        }
    }
}
