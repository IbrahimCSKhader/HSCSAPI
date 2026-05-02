using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class changtocascatee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientAuthorizedMembers_AuthorizedMembers_AuthorizedMemberId",
                table: "PatientAuthorizedMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientAuthorizedMembers_AuthorizedMembers_AuthorizedMemberId",
                table: "PatientAuthorizedMembers",
                column: "AuthorizedMemberId",
                principalTable: "AuthorizedMembers",
                principalColumn: "AuthorizedMemberId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientAuthorizedMembers_AuthorizedMembers_AuthorizedMemberId",
                table: "PatientAuthorizedMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientAuthorizedMembers_AuthorizedMembers_AuthorizedMemberId",
                table: "PatientAuthorizedMembers",
                column: "AuthorizedMemberId",
                principalTable: "AuthorizedMembers",
                principalColumn: "AuthorizedMemberId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
