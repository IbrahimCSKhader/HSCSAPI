using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalPatientProfileShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalPatientProfileShares",
                columns: table => new
                {
                    ExternalPatientProfileShareId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DoctorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShareToken = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ShareTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastCodeSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerificationCodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    VerificationCodeExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccessSessionTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AccessSessionExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAccessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalPatientProfileShares", x => x.ExternalPatientProfileShareId);
                    table.ForeignKey(
                        name: "FK_ExternalPatientProfileShares_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPatientProfileShares_PatientId_DoctorEmail_IsActive",
                table: "ExternalPatientProfileShares",
                columns: new[] { "PatientId", "DoctorEmail", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPatientProfileShares_ShareTokenHash",
                table: "ExternalPatientProfileShares",
                column: "ShareTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalPatientProfileShares");
        }
    }
}
