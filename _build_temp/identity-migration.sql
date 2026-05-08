IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Roles] (
    [RoleId] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId])
);

CREATE TABLE [Users] (
    [UserId] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [DateOfBirth] date NULL,
    [Email] nvarchar(256) NOT NULL,
    [PhoneNumber] nvarchar(30) NULL,
    [Address] nvarchar(300) NULL,
    [PasswordHash] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([UserId])
);

CREATE TABLE [AuthorizedMembers] (
    [AuthorizedMemberId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AuthorizedMembers] PRIMARY KEY ([AuthorizedMemberId]),
    CONSTRAINT [FK_AuthorizedMembers_Users_AuthorizedMemberId] FOREIGN KEY ([AuthorizedMemberId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
);

CREATE TABLE [Doctors] (
    [DoctorId] uniqueidentifier NOT NULL,
    [ProfessionalLicenseNumber] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Doctors] PRIMARY KEY ([DoctorId]),
    CONSTRAINT [FK_Doctors_Users_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
);

CREATE TABLE [LaboratoryTechnologists] (
    [LaboratoryTechnologistId] uniqueidentifier NOT NULL,
    [ProfessionalLicenseNumber] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_LaboratoryTechnologists] PRIMARY KEY ([LaboratoryTechnologistId]),
    CONSTRAINT [FK_LaboratoryTechnologists_Users_LaboratoryTechnologistId] FOREIGN KEY ([LaboratoryTechnologistId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
);

CREATE TABLE [Notifications] (
    [NotificationId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [IsRead] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([NotificationId]),
    CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
);

CREATE TABLE [Patients] (
    [PatientId] uniqueidentifier NOT NULL,
    [UserID] nvarchar(20) NOT NULL,
    [Gender] nvarchar(30) NOT NULL,
    [BloodType] nvarchar(20) NULL,
    CONSTRAINT [PK_Patients] PRIMARY KEY ([PatientId]),
    CONSTRAINT [FK_Patients_Users_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
);

CREATE TABLE [RadiologyTechnologists] (
    [RadiologyTechnologistId] uniqueidentifier NOT NULL,
    [ProfessionalLicenseNumber] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_RadiologyTechnologists] PRIMARY KEY ([RadiologyTechnologistId]),
    CONSTRAINT [FK_RadiologyTechnologists_Users_RadiologyTechnologistId] FOREIGN KEY ([RadiologyTechnologistId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
);

CREATE TABLE [UserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] int NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
);

CREATE TABLE [AvailabilitySlots] (
    [AvailabilitySlotId] uniqueidentifier NOT NULL,
    [DoctorId] uniqueidentifier NOT NULL,
    [DayOfWeek] int NOT NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [IsAvailable] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_AvailabilitySlots] PRIMARY KEY ([AvailabilitySlotId]),
    CONSTRAINT [FK_AvailabilitySlots_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([DoctorId]) ON DELETE NO ACTION
);

CREATE TABLE [Invites] (
    [InviteId] uniqueidentifier NOT NULL,
    [PatientId] uniqueidentifier NOT NULL,
    [AuthorizedMemberId] uniqueidentifier NOT NULL,
    [RelationshipType] nvarchar(30) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [SentAt] datetime2 NOT NULL,
    [RespondedAt] datetime2 NULL,
    CONSTRAINT [PK_Invites] PRIMARY KEY ([InviteId]),
    CONSTRAINT [FK_Invites_AuthorizedMembers_AuthorizedMemberId] FOREIGN KEY ([AuthorizedMemberId]) REFERENCES [AuthorizedMembers] ([AuthorizedMemberId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Invites_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([PatientId]) ON DELETE NO ACTION
);

CREATE TABLE [PatientAuthorizedMembers] (
    [PatientId] uniqueidentifier NOT NULL,
    [AuthorizedMemberId] uniqueidentifier NOT NULL,
    [RelationshipType] nvarchar(30) NOT NULL,
    [AuthorizedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PatientAuthorizedMembers] PRIMARY KEY ([PatientId], [AuthorizedMemberId]),
    CONSTRAINT [FK_PatientAuthorizedMembers_AuthorizedMembers_AuthorizedMemberId] FOREIGN KEY ([AuthorizedMemberId]) REFERENCES [AuthorizedMembers] ([AuthorizedMemberId]) ON DELETE CASCADE,
    CONSTRAINT [FK_PatientAuthorizedMembers_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([PatientId]) ON DELETE CASCADE
);

CREATE TABLE [Reminders] (
    [ReminderId] uniqueidentifier NOT NULL,
    [PatientId] uniqueidentifier NOT NULL,
    [DoctorId] uniqueidentifier NOT NULL,
    [AuthorizedMemberId] uniqueidentifier NULL,
    [ReminderText] nvarchar(500) NOT NULL,
    [ReminderAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Reminders] PRIMARY KEY ([ReminderId]),
    CONSTRAINT [FK_Reminders_AuthorizedMembers_AuthorizedMemberId] FOREIGN KEY ([AuthorizedMemberId]) REFERENCES [AuthorizedMembers] ([AuthorizedMemberId]) ON DELETE SET NULL,
    CONSTRAINT [FK_Reminders_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([DoctorId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reminders_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([PatientId]) ON DELETE NO ACTION
);

CREATE TABLE [Appointments] (
    [AppointmentId] uniqueidentifier NOT NULL,
    [DoctorId] uniqueidentifier NOT NULL,
    [PatientId] uniqueidentifier NOT NULL,
    [AvailabilitySlotId] uniqueidentifier NOT NULL,
    [AppointmentDate] date NOT NULL,
    [AppointmentTime] time NOT NULL,
    [Notes] nvarchar(1000) NULL,
    CONSTRAINT [PK_Appointments] PRIMARY KEY ([AppointmentId]),
    CONSTRAINT [FK_Appointments_AvailabilitySlots_AvailabilitySlotId] FOREIGN KEY ([AvailabilitySlotId]) REFERENCES [AvailabilitySlots] ([AvailabilitySlotId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Appointments_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([DoctorId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Appointments_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([PatientId]) ON DELETE NO ACTION
);

CREATE TABLE [MedicalFiles] (
    [MedicalFileId] uniqueidentifier NOT NULL,
    [AppointmentId] uniqueidentifier NOT NULL,
    [UploadedByDoctorId] uniqueidentifier NOT NULL,
    [FileType] nvarchar(20) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [EncryptedChecksum] nvarchar(512) NOT NULL,
    [FileSizeInBytes] bigint NOT NULL,
    [SeverityLevel] nvarchar(20) NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_MedicalFiles] PRIMARY KEY ([MedicalFileId]),
    CONSTRAINT [FK_MedicalFiles_Appointments_AppointmentId] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([AppointmentId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MedicalFiles_Doctors_UploadedByDoctorId] FOREIGN KEY ([UploadedByDoctorId]) REFERENCES [Doctors] ([DoctorId]) ON DELETE NO ACTION
);

CREATE TABLE [ImagingTestRequests] (
    [ImagingTestRequestId] uniqueidentifier NOT NULL,
    [TestName] nvarchar(200) NOT NULL,
    [RadiologyTechnologistId] uniqueidentifier NULL,
    [ResultMedicalFileId] uniqueidentifier NULL,
    CONSTRAINT [PK_ImagingTestRequests] PRIMARY KEY ([ImagingTestRequestId]),
    CONSTRAINT [FK_ImagingTestRequests_MedicalFiles_ResultMedicalFileId] FOREIGN KEY ([ResultMedicalFileId]) REFERENCES [MedicalFiles] ([MedicalFileId]) ON DELETE SET NULL,
    CONSTRAINT [FK_ImagingTestRequests_RadiologyTechnologists_RadiologyTechnologistId] FOREIGN KEY ([RadiologyTechnologistId]) REFERENCES [RadiologyTechnologists] ([RadiologyTechnologistId]) ON DELETE SET NULL
);

CREATE TABLE [LabTestRequests] (
    [LabTestRequestId] uniqueidentifier NOT NULL,
    [TestName] nvarchar(200) NOT NULL,
    [LaboratoryTechnologistId] uniqueidentifier NULL,
    [ResultMedicalFileId] uniqueidentifier NULL,
    CONSTRAINT [PK_LabTestRequests] PRIMARY KEY ([LabTestRequestId]),
    CONSTRAINT [FK_LabTestRequests_LaboratoryTechnologists_LaboratoryTechnologistId] FOREIGN KEY ([LaboratoryTechnologistId]) REFERENCES [LaboratoryTechnologists] ([LaboratoryTechnologistId]) ON DELETE SET NULL,
    CONSTRAINT [FK_LabTestRequests_MedicalFiles_ResultMedicalFileId] FOREIGN KEY ([ResultMedicalFileId]) REFERENCES [MedicalFiles] ([MedicalFileId]) ON DELETE SET NULL
);

CREATE TABLE [Clinics] (
    [ClinicId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Address] nvarchar(500) NULL,
    [CreatedBySuperAdminUserId] uniqueidentifier NOT NULL,
    [AdminSecretaryId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Clinics] PRIMARY KEY ([ClinicId]),
    CONSTRAINT [FK_Clinics_Users_CreatedBySuperAdminUserId] FOREIGN KEY ([CreatedBySuperAdminUserId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
);

CREATE TABLE [Secretaries] (
    [SecretaryId] uniqueidentifier NOT NULL,
    [ClinicId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Secretaries] PRIMARY KEY ([SecretaryId]),
    CONSTRAINT [FK_Secretaries_Clinics_ClinicId] FOREIGN KEY ([ClinicId]) REFERENCES [Clinics] ([ClinicId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Secretaries_Users_SecretaryId] FOREIGN KEY ([SecretaryId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
);

CREATE TABLE [FileDownloadRequests] (
    [FileDownloadRequestId] uniqueidentifier NOT NULL,
    [PatientId] uniqueidentifier NOT NULL,
    [MedicalFileId] uniqueidentifier NOT NULL,
    [ReviewedBySecretaryId] uniqueidentifier NULL,
    [Reason] nvarchar(500) NOT NULL,
    [PurposeDescription] nvarchar(1000) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [SubmittedAt] datetime2 NOT NULL,
    [ReviewedAt] datetime2 NULL,
    [RejectionReason] nvarchar(1000) NULL,
    CONSTRAINT [PK_FileDownloadRequests] PRIMARY KEY ([FileDownloadRequestId]),
    CONSTRAINT [FK_FileDownloadRequests_MedicalFiles_MedicalFileId] FOREIGN KEY ([MedicalFileId]) REFERENCES [MedicalFiles] ([MedicalFileId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FileDownloadRequests_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([PatientId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FileDownloadRequests_Secretaries_ReviewedBySecretaryId] FOREIGN KEY ([ReviewedBySecretaryId]) REFERENCES [Secretaries] ([SecretaryId]) ON DELETE SET NULL
);

CREATE TABLE [Reports] (
    [ReportId] uniqueidentifier NOT NULL,
    [SecretaryId] uniqueidentifier NOT NULL,
    [GeneratedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Reports] PRIMARY KEY ([ReportId]),
    CONSTRAINT [FK_Reports_Secretaries_SecretaryId] FOREIGN KEY ([SecretaryId]) REFERENCES [Secretaries] ([SecretaryId]) ON DELETE NO ACTION
);

CREATE TABLE [ReportInformations] (
    [ReportInformationId] uniqueidentifier NOT NULL,
    [ReportId] uniqueidentifier NOT NULL,
    [FileFormat] nvarchar(20) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [FileSizeInBytes] bigint NOT NULL,
    CONSTRAINT [PK_ReportInformations] PRIMARY KEY ([ReportInformationId]),
    CONSTRAINT [FK_ReportInformations_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([ReportId]) ON DELETE CASCADE
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
    SET IDENTITY_INSERT [Roles] ON;
INSERT INTO [Roles] ([RoleId], [Name])
VALUES (1, N'Patient'),
(2, N'Doctor'),
(3, N'Secretary'),
(4, N'AuthorizedMember'),
(5, N'LaboratoryTechnologist'),
(6, N'RadiologyTechnologist'),
(7, N'SuperAdmin');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
    SET IDENTITY_INSERT [Roles] OFF;

CREATE UNIQUE INDEX [IX_Appointments_AvailabilitySlotId] ON [Appointments] ([AvailabilitySlotId]);

CREATE INDEX [IX_Appointments_DoctorId] ON [Appointments] ([DoctorId]);

CREATE INDEX [IX_Appointments_PatientId] ON [Appointments] ([PatientId]);

CREATE UNIQUE INDEX [IX_AvailabilitySlots_DoctorId_DayOfWeek_StartTime_EndTime] ON [AvailabilitySlots] ([DoctorId], [DayOfWeek], [StartTime], [EndTime]);

CREATE UNIQUE INDEX [IX_Clinics_AdminSecretaryId] ON [Clinics] ([AdminSecretaryId]);

CREATE INDEX [IX_Clinics_CreatedBySuperAdminUserId] ON [Clinics] ([CreatedBySuperAdminUserId]);

CREATE UNIQUE INDEX [IX_Doctors_ProfessionalLicenseNumber] ON [Doctors] ([ProfessionalLicenseNumber]);

CREATE INDEX [IX_FileDownloadRequests_MedicalFileId] ON [FileDownloadRequests] ([MedicalFileId]);

CREATE INDEX [IX_FileDownloadRequests_PatientId] ON [FileDownloadRequests] ([PatientId]);

CREATE INDEX [IX_FileDownloadRequests_ReviewedBySecretaryId] ON [FileDownloadRequests] ([ReviewedBySecretaryId]);

CREATE INDEX [IX_ImagingTestRequests_RadiologyTechnologistId] ON [ImagingTestRequests] ([RadiologyTechnologistId]);

CREATE INDEX [IX_ImagingTestRequests_ResultMedicalFileId] ON [ImagingTestRequests] ([ResultMedicalFileId]);

CREATE INDEX [IX_Invites_AuthorizedMemberId] ON [Invites] ([AuthorizedMemberId]);

CREATE INDEX [IX_Invites_PatientId] ON [Invites] ([PatientId]);

CREATE UNIQUE INDEX [IX_LaboratoryTechnologists_ProfessionalLicenseNumber] ON [LaboratoryTechnologists] ([ProfessionalLicenseNumber]);

CREATE INDEX [IX_LabTestRequests_LaboratoryTechnologistId] ON [LabTestRequests] ([LaboratoryTechnologistId]);

CREATE INDEX [IX_LabTestRequests_ResultMedicalFileId] ON [LabTestRequests] ([ResultMedicalFileId]);

CREATE INDEX [IX_MedicalFiles_AppointmentId] ON [MedicalFiles] ([AppointmentId]);

CREATE INDEX [IX_MedicalFiles_UploadedByDoctorId] ON [MedicalFiles] ([UploadedByDoctorId]);

CREATE INDEX [IX_Notifications_UserId_IsRead] ON [Notifications] ([UserId], [IsRead]);

CREATE INDEX [IX_PatientAuthorizedMembers_AuthorizedMemberId] ON [PatientAuthorizedMembers] ([AuthorizedMemberId]);

CREATE UNIQUE INDEX [IX_Patients_UserID] ON [Patients] ([UserID]);

CREATE UNIQUE INDEX [IX_RadiologyTechnologists_ProfessionalLicenseNumber] ON [RadiologyTechnologists] ([ProfessionalLicenseNumber]);

CREATE INDEX [IX_Reminders_AuthorizedMemberId] ON [Reminders] ([AuthorizedMemberId]);

CREATE INDEX [IX_Reminders_DoctorId] ON [Reminders] ([DoctorId]);

CREATE INDEX [IX_Reminders_PatientId] ON [Reminders] ([PatientId]);

CREATE INDEX [IX_ReportInformations_ReportId] ON [ReportInformations] ([ReportId]);

CREATE INDEX [IX_Reports_SecretaryId] ON [Reports] ([SecretaryId]);

CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);

CREATE INDEX [IX_Secretaries_ClinicId] ON [Secretaries] ([ClinicId]);

CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);

CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);

ALTER TABLE [Clinics] ADD CONSTRAINT [FK_Clinics_Secretaries_AdminSecretaryId] FOREIGN KEY ([AdminSecretaryId]) REFERENCES [Secretaries] ([SecretaryId]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260410195355_new-update', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260411103708_first-database-push', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260411104605_first-database-push2', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260411104914_first-database-push22', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_Clinics_AdminSecretaryId] ON [Clinics];

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Clinics]') AND [c].[name] = N'AdminSecretaryId');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Clinics] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Clinics] ALTER COLUMN [AdminSecretaryId] uniqueidentifier NULL;

CREATE UNIQUE INDEX [IX_Clinics_AdminSecretaryId] ON [Clinics] ([AdminSecretaryId]) WHERE [AdminSecretaryId] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260421212816_MakeClinicAdminSecretaryOptional', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [UserVerificationCodes] (
    [UserVerificationCodeId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Code] nvarchar(10) NOT NULL,
    [Purpose] int NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsUsed] bit NOT NULL,
    CONSTRAINT [PK_UserVerificationCodes] PRIMARY KEY ([UserVerificationCodeId]),
    CONSTRAINT [FK_UserVerificationCodes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
);

CREATE INDEX [IX_UserVerificationCodes_UserId] ON [UserVerificationCodes] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260421225316_add-new-super-admin', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Secretaries]') AND [c].[name] = N'ClinicId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Secretaries] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [Secretaries] ALTER COLUMN [ClinicId] uniqueidentifier NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260421230228_MakeSecretaryClinicOptional', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260421230613_remove-clicn-id', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260421231113_remove-clicn-id1', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260421232249_email-varification', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [AuthorizedMembers] DROP CONSTRAINT [FK_AuthorizedMembers_Users_AuthorizedMemberId];

ALTER TABLE [Doctors] DROP CONSTRAINT [FK_Doctors_Users_DoctorId];

ALTER TABLE [LaboratoryTechnologists] DROP CONSTRAINT [FK_LaboratoryTechnologists_Users_LaboratoryTechnologistId];

ALTER TABLE [Patients] DROP CONSTRAINT [FK_Patients_Users_PatientId];

ALTER TABLE [RadiologyTechnologists] DROP CONSTRAINT [FK_RadiologyTechnologists_Users_RadiologyTechnologistId];

ALTER TABLE [Secretaries] DROP CONSTRAINT [FK_Secretaries_Users_SecretaryId];

ALTER TABLE [AuthorizedMembers] ADD CONSTRAINT [FK_AuthorizedMembers_Users_AuthorizedMemberId] FOREIGN KEY ([AuthorizedMemberId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE;

ALTER TABLE [Doctors] ADD CONSTRAINT [FK_Doctors_Users_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE;

ALTER TABLE [LaboratoryTechnologists] ADD CONSTRAINT [FK_LaboratoryTechnologists_Users_LaboratoryTechnologistId] FOREIGN KEY ([LaboratoryTechnologistId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE;

ALTER TABLE [Patients] ADD CONSTRAINT [FK_Patients_Users_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [RadiologyTechnologists] ADD CONSTRAINT [FK_RadiologyTechnologists_Users_RadiologyTechnologistId] FOREIGN KEY ([RadiologyTechnologistId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE;

ALTER TABLE [Secretaries] ADD CONSTRAINT [FK_Secretaries_Users_SecretaryId] FOREIGN KEY ([SecretaryId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260429142821_chang-to-cascate', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [PatientAuthorizedMembers] DROP CONSTRAINT [FK_PatientAuthorizedMembers_AuthorizedMembers_AuthorizedMemberId];

ALTER TABLE [PatientAuthorizedMembers] ADD CONSTRAINT [FK_PatientAuthorizedMembers_AuthorizedMembers_AuthorizedMemberId] FOREIGN KEY ([AuthorizedMemberId]) REFERENCES [AuthorizedMembers] ([AuthorizedMemberId]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260429143514_chang-to-cascatee', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Patients] DROP CONSTRAINT [FK_Patients_Users_PatientId];

ALTER TABLE [Patients] ADD CONSTRAINT [FK_Patients_Users_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260429143837_chang-to-cascateee', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260429145514_chang-to-cascateeee', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Secretaries] DROP CONSTRAINT [FK_Secretaries_Clinics_ClinicId];

ALTER TABLE [UserRoles] DROP CONSTRAINT [FK_UserRoles_Roles_RoleId];

DROP INDEX [IX_Users_Email] ON [Users];

DROP INDEX [IX_Secretaries_ClinicId] ON [Secretaries];

DROP INDEX [IX_Roles_Name] ON [Roles];

DROP INDEX [IX_UserRoles_RoleId] ON [UserRoles];

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'PasswordHash');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [Users] ALTER COLUMN [PasswordHash] nvarchar(max) NULL;

DECLARE @var3 nvarchar(max);
SELECT @var3 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'Name');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT ' + @var3 + ';');
ALTER TABLE [Users] ALTER COLUMN [Name] nvarchar(200) NOT NULL;

ALTER TABLE [Users] ADD [AccessFailedCount] int NOT NULL DEFAULT 0;

ALTER TABLE [Users] ADD [ClinicId] uniqueidentifier NULL;

ALTER TABLE [Users] ADD [ConcurrencyStamp] nvarchar(max) NULL;

ALTER TABLE [Users] ADD [EmailConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Users] ADD [LockoutEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Users] ADD [LockoutEnd] datetimeoffset NULL;

ALTER TABLE [Users] ADD [NormalizedEmail] nvarchar(256) NULL;

ALTER TABLE [Users] ADD [NormalizedUserName] nvarchar(256) NULL;

ALTER TABLE [Users] ADD [PhoneNumberConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Users] ADD [SecurityStamp] nvarchar(max) NULL;

ALTER TABLE [Users] ADD [TwoFactorEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Users] ADD [UserName] nvarchar(256) NOT NULL DEFAULT N'';

ALTER TABLE [Roles] ADD [ConcurrencyStamp] nvarchar(max) NULL;

ALTER TABLE [Roles] ADD [IdentityRoleId] uniqueidentifier NULL;

ALTER TABLE [Roles] ADD [NormalizedName] nvarchar(256) NULL;

ALTER TABLE [UserRoles] ADD [IdentityRoleId] uniqueidentifier NULL;

UPDATE U
SET
    U.UserName = U.Email,
    U.NormalizedEmail = UPPER(U.Email),
    U.NormalizedUserName = UPPER(U.Email),
    U.EmailConfirmed = 1,
    U.SecurityStamp = COALESCE(U.SecurityStamp, CONVERT(nvarchar(32), NEWID())),
    U.ConcurrencyStamp = COALESCE(U.ConcurrencyStamp, CONVERT(nvarchar(32), NEWID()))
FROM Users U;

UPDATE U
SET U.ClinicId = S.ClinicId
FROM Users U
INNER JOIN Secretaries S ON U.UserId = S.SecretaryId
WHERE S.ClinicId IS NOT NULL;

UPDATE Roles
SET
    IdentityRoleId = CASE RoleId
        WHEN 1 THEN '6D3A8A70-B6D1-4F01-8F10-2F87E65F1001'
        WHEN 2 THEN '6D3A8A70-B6D1-4F01-8F10-2F87E65F1002'
        WHEN 3 THEN '6D3A8A70-B6D1-4F01-8F10-2F87E65F1003'
        WHEN 4 THEN '6D3A8A70-B6D1-4F01-8F10-2F87E65F1004'
        WHEN 5 THEN '6D3A8A70-B6D1-4F01-8F10-2F87E65F1005'
        WHEN 6 THEN '6D3A8A70-B6D1-4F01-8F10-2F87E65F1006'
        WHEN 7 THEN '6D3A8A70-B6D1-4F01-8F10-2F87E65F1007'
    END,
    NormalizedName = UPPER(Name),
    ConcurrencyStamp = COALESCE(ConcurrencyStamp, CONVERT(nvarchar(32), NEWID()));

UPDATE UR
SET UR.IdentityRoleId = R.IdentityRoleId
FROM UserRoles UR
INNER JOIN Roles R ON UR.RoleId = R.RoleId;

ALTER TABLE [UserRoles] DROP CONSTRAINT [PK_UserRoles];

ALTER TABLE [Roles] DROP CONSTRAINT [PK_Roles];

DECLARE @var4 nvarchar(max);
SELECT @var4 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Secretaries]') AND [c].[name] = N'ClinicId');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Secretaries] DROP CONSTRAINT ' + @var4 + ';');
ALTER TABLE [Secretaries] DROP COLUMN [ClinicId];

DECLARE @var5 nvarchar(max);
SELECT @var5 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserRoles]') AND [c].[name] = N'RoleId');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [UserRoles] DROP CONSTRAINT ' + @var5 + ';');
ALTER TABLE [UserRoles] DROP COLUMN [RoleId];

DECLARE @var6 nvarchar(max);
SELECT @var6 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Roles]') AND [c].[name] = N'RoleId');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Roles] DROP CONSTRAINT ' + @var6 + ';');
ALTER TABLE [Roles] DROP COLUMN [RoleId];

EXEC sp_rename N'[UserRoles].[IdentityRoleId]', N'RoleId', 'COLUMN';

EXEC sp_rename N'[Roles].[IdentityRoleId]', N'RoleId', 'COLUMN';

DECLARE @var7 nvarchar(max);
SELECT @var7 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserRoles]') AND [c].[name] = N'RoleId');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [UserRoles] DROP CONSTRAINT ' + @var7 + ';');
ALTER TABLE [UserRoles] ALTER COLUMN [RoleId] uniqueidentifier NOT NULL;

DECLARE @var8 nvarchar(max);
SELECT @var8 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Roles]') AND [c].[name] = N'RoleId');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Roles] DROP CONSTRAINT ' + @var8 + ';');
ALTER TABLE [Roles] ALTER COLUMN [RoleId] uniqueidentifier NOT NULL;

ALTER TABLE [Roles] ADD CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId]);

ALTER TABLE [UserRoles] ADD CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]);

CREATE TABLE [RoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_RoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId]) ON DELETE CASCADE
);

CREATE TABLE [UserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
);

CREATE TABLE [UserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
);

CREATE TABLE [UserTokens] (
    [UserId] uniqueidentifier NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL;

CREATE INDEX [IX_Users_ClinicId] ON [Users] ([ClinicId]);

CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE UNIQUE INDEX [RoleNameIndex] ON [Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);

CREATE INDEX [IX_RoleClaims_RoleId] ON [RoleClaims] ([RoleId]);

CREATE INDEX [IX_UserClaims_UserId] ON [UserClaims] ([UserId]);

CREATE INDEX [IX_UserLogins_UserId] ON [UserLogins] ([UserId]);

ALTER TABLE [UserRoles] ADD CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId]) ON DELETE CASCADE;

ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Clinics_ClinicId] FOREIGN KEY ([ClinicId]) REFERENCES [Clinics] ([ClinicId]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260508123826_UseIdentityAndClinicMembership', N'10.0.5');

COMMIT;
GO

