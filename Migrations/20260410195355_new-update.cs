using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class newupdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "AuthorizedMembers",
                columns: table => new
                {
                    AuthorizedMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorizedMembers", x => x.AuthorizedMemberId);
                    table.ForeignKey(
                        name: "FK_AuthorizedMembers_Users_AuthorizedMemberId",
                        column: x => x.AuthorizedMemberId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Doctors",
                columns: table => new
                {
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfessionalLicenseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doctors", x => x.DoctorId);
                    table.ForeignKey(
                        name: "FK_Doctors_Users_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryTechnologists",
                columns: table => new
                {
                    LaboratoryTechnologistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfessionalLicenseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaboratoryTechnologists", x => x.LaboratoryTechnologistId);
                    table.ForeignKey(
                        name: "FK_LaboratoryTechnologists_Users_LaboratoryTechnologistId",
                        column: x => x.LaboratoryTechnologistId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserID = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BloodType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.PatientId);
                    table.ForeignKey(
                        name: "FK_Patients_Users_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyTechnologists",
                columns: table => new
                {
                    RadiologyTechnologistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfessionalLicenseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyTechnologists", x => x.RadiologyTechnologistId);
                    table.ForeignKey(
                        name: "FK_RadiologyTechnologists_Users_RadiologyTechnologistId",
                        column: x => x.RadiologyTechnologistId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AvailabilitySlots",
                columns: table => new
                {
                    AvailabilitySlotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilitySlots", x => x.AvailabilitySlotId);
                    table.ForeignKey(
                        name: "FK_AvailabilitySlots_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "DoctorId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invites",
                columns: table => new
                {
                    InviteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorizedMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invites", x => x.InviteId);
                    table.ForeignKey(
                        name: "FK_Invites_AuthorizedMembers_AuthorizedMemberId",
                        column: x => x.AuthorizedMemberId,
                        principalTable: "AuthorizedMembers",
                        principalColumn: "AuthorizedMemberId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invites_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientAuthorizedMembers",
                columns: table => new
                {
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorizedMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AuthorizedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAuthorizedMembers", x => new { x.PatientId, x.AuthorizedMemberId });
                    table.ForeignKey(
                        name: "FK_PatientAuthorizedMembers_AuthorizedMembers_AuthorizedMemberId",
                        column: x => x.AuthorizedMemberId,
                        principalTable: "AuthorizedMembers",
                        principalColumn: "AuthorizedMemberId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PatientAuthorizedMembers_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reminders",
                columns: table => new
                {
                    ReminderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorizedMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReminderText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReminderAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminders", x => x.ReminderId);
                    table.ForeignKey(
                        name: "FK_Reminders_AuthorizedMembers_AuthorizedMemberId",
                        column: x => x.AuthorizedMemberId,
                        principalTable: "AuthorizedMembers",
                        principalColumn: "AuthorizedMemberId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Reminders_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "DoctorId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reminders_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvailabilitySlotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AppointmentTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.AppointmentId);
                    table.ForeignKey(
                        name: "FK_Appointments_AvailabilitySlots_AvailabilitySlotId",
                        column: x => x.AvailabilitySlotId,
                        principalTable: "AvailabilitySlots",
                        principalColumn: "AvailabilitySlotId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "DoctorId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicalFiles",
                columns: table => new
                {
                    MedicalFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EncryptedChecksum = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FileSizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    SeverityLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalFiles", x => x.MedicalFileId);
                    table.ForeignKey(
                        name: "FK_MedicalFiles_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalFiles_Doctors_UploadedByDoctorId",
                        column: x => x.UploadedByDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "DoctorId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImagingTestRequests",
                columns: table => new
                {
                    ImagingTestRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TestName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RadiologyTechnologistId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResultMedicalFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImagingTestRequests", x => x.ImagingTestRequestId);
                    table.ForeignKey(
                        name: "FK_ImagingTestRequests_MedicalFiles_ResultMedicalFileId",
                        column: x => x.ResultMedicalFileId,
                        principalTable: "MedicalFiles",
                        principalColumn: "MedicalFileId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImagingTestRequests_RadiologyTechnologists_RadiologyTechnologistId",
                        column: x => x.RadiologyTechnologistId,
                        principalTable: "RadiologyTechnologists",
                        principalColumn: "RadiologyTechnologistId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LabTestRequests",
                columns: table => new
                {
                    LabTestRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TestName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LaboratoryTechnologistId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResultMedicalFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTestRequests", x => x.LabTestRequestId);
                    table.ForeignKey(
                        name: "FK_LabTestRequests_LaboratoryTechnologists_LaboratoryTechnologistId",
                        column: x => x.LaboratoryTechnologistId,
                        principalTable: "LaboratoryTechnologists",
                        principalColumn: "LaboratoryTechnologistId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LabTestRequests_MedicalFiles_ResultMedicalFileId",
                        column: x => x.ResultMedicalFileId,
                        principalTable: "MedicalFiles",
                        principalColumn: "MedicalFileId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Clinics",
                columns: table => new
                {
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBySuperAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminSecretaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clinics", x => x.ClinicId);
                    table.ForeignKey(
                        name: "FK_Clinics_Users_CreatedBySuperAdminUserId",
                        column: x => x.CreatedBySuperAdminUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Secretaries",
                columns: table => new
                {
                    SecretaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Secretaries", x => x.SecretaryId);
                    table.ForeignKey(
                        name: "FK_Secretaries_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "ClinicId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Secretaries_Users_SecretaryId",
                        column: x => x.SecretaryId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FileDownloadRequests",
                columns: table => new
                {
                    FileDownloadRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedBySecretaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PurposeDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileDownloadRequests", x => x.FileDownloadRequestId);
                    table.ForeignKey(
                        name: "FK_FileDownloadRequests_MedicalFiles_MedicalFileId",
                        column: x => x.MedicalFileId,
                        principalTable: "MedicalFiles",
                        principalColumn: "MedicalFileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileDownloadRequests_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileDownloadRequests_Secretaries_ReviewedBySecretaryId",
                        column: x => x.ReviewedBySecretaryId,
                        principalTable: "Secretaries",
                        principalColumn: "SecretaryId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecretaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.ReportId);
                    table.ForeignKey(
                        name: "FK_Reports_Secretaries_SecretaryId",
                        column: x => x.SecretaryId,
                        principalTable: "Secretaries",
                        principalColumn: "SecretaryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportInformations",
                columns: table => new
                {
                    ReportInformationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileFormat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSizeInBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportInformations", x => x.ReportInformationId);
                    table.ForeignKey(
                        name: "FK_ReportInformations_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "ReportId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "RoleId", "Name" },
                values: new object[,]
                {
                    { 1, "Patient" },
                    { 2, "Doctor" },
                    { 3, "Secretary" },
                    { 4, "AuthorizedMember" },
                    { 5, "LaboratoryTechnologist" },
                    { 6, "RadiologyTechnologist" },
                    { 7, "SuperAdmin" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AvailabilitySlotId",
                table: "Appointments",
                column: "AvailabilitySlotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId",
                table: "Appointments",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId",
                table: "Appointments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_DoctorId_DayOfWeek_StartTime_EndTime",
                table: "AvailabilitySlots",
                columns: new[] { "DoctorId", "DayOfWeek", "StartTime", "EndTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clinics_AdminSecretaryId",
                table: "Clinics",
                column: "AdminSecretaryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clinics_CreatedBySuperAdminUserId",
                table: "Clinics",
                column: "CreatedBySuperAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_ProfessionalLicenseNumber",
                table: "Doctors",
                column: "ProfessionalLicenseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileDownloadRequests_MedicalFileId",
                table: "FileDownloadRequests",
                column: "MedicalFileId");

            migrationBuilder.CreateIndex(
                name: "IX_FileDownloadRequests_PatientId",
                table: "FileDownloadRequests",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FileDownloadRequests_ReviewedBySecretaryId",
                table: "FileDownloadRequests",
                column: "ReviewedBySecretaryId");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingTestRequests_RadiologyTechnologistId",
                table: "ImagingTestRequests",
                column: "RadiologyTechnologistId");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingTestRequests_ResultMedicalFileId",
                table: "ImagingTestRequests",
                column: "ResultMedicalFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Invites_AuthorizedMemberId",
                table: "Invites",
                column: "AuthorizedMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Invites_PatientId",
                table: "Invites",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryTechnologists_ProfessionalLicenseNumber",
                table: "LaboratoryTechnologists",
                column: "ProfessionalLicenseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTestRequests_LaboratoryTechnologistId",
                table: "LabTestRequests",
                column: "LaboratoryTechnologistId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTestRequests_ResultMedicalFileId",
                table: "LabTestRequests",
                column: "ResultMedicalFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalFiles_AppointmentId",
                table: "MedicalFiles",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalFiles_UploadedByDoctorId",
                table: "MedicalFiles",
                column: "UploadedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientAuthorizedMembers_AuthorizedMemberId",
                table: "PatientAuthorizedMembers",
                column: "AuthorizedMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_UserID",
                table: "Patients",
                column: "UserID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyTechnologists_ProfessionalLicenseNumber",
                table: "RadiologyTechnologists",
                column: "ProfessionalLicenseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_AuthorizedMemberId",
                table: "Reminders",
                column: "AuthorizedMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_DoctorId",
                table: "Reminders",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_PatientId",
                table: "Reminders",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportInformations_ReportId",
                table: "ReportInformations",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_SecretaryId",
                table: "Reports",
                column: "SecretaryId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Secretaries_ClinicId",
                table: "Secretaries",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Clinics_Secretaries_AdminSecretaryId",
                table: "Clinics",
                column: "AdminSecretaryId",
                principalTable: "Secretaries",
                principalColumn: "SecretaryId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clinics_Users_CreatedBySuperAdminUserId",
                table: "Clinics");

            migrationBuilder.DropForeignKey(
                name: "FK_Secretaries_Users_SecretaryId",
                table: "Secretaries");

            migrationBuilder.DropForeignKey(
                name: "FK_Clinics_Secretaries_AdminSecretaryId",
                table: "Clinics");

            migrationBuilder.DropTable(
                name: "FileDownloadRequests");

            migrationBuilder.DropTable(
                name: "ImagingTestRequests");

            migrationBuilder.DropTable(
                name: "Invites");

            migrationBuilder.DropTable(
                name: "LabTestRequests");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PatientAuthorizedMembers");

            migrationBuilder.DropTable(
                name: "Reminders");

            migrationBuilder.DropTable(
                name: "ReportInformations");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "RadiologyTechnologists");

            migrationBuilder.DropTable(
                name: "LaboratoryTechnologists");

            migrationBuilder.DropTable(
                name: "MedicalFiles");

            migrationBuilder.DropTable(
                name: "AuthorizedMembers");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "AvailabilitySlots");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "Doctors");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Secretaries");

            migrationBuilder.DropTable(
                name: "Clinics");
        }
    }
}
