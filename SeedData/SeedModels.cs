using HSCSAPI.Models.Enums;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Relations;
using HSCSAPI.Models.Secretaries;

namespace HSCSAPI.SeedData;

public sealed record SeedClinic(string Key, string Name, string? Address);

public sealed record SeedUser(
    string Key,
    string Email,
    string Name,
    UserSystemRole Role,
    string? ClinicKey,
    string? PhoneNumber,
    string? Address,
    DateOnly? DateOfBirth,
    string? ProfessionalLicenseNumber = null,
    Gender? Gender = null,
    BloodType? BloodType = null);

public sealed record SeedAvailabilitySlot(
    string DoctorKey,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsAvailable);

public sealed record SeedAppointment(
    string DoctorKey,
    string PatientKey,
    DayOfWeek DayOfWeek,
    TimeOnly AppointmentTime,
    string? Notes);

public sealed record SeedMedicalFile(
    string UploadedByDoctorKey,
    MedicalFileType FileType,
    string FilePath,
    string EncryptedChecksum,
    long FileSizeInBytes,
    SeverityLevel SeverityLevel,
    int UploadedDaysOffset);

public sealed record SeedPatientAuthorization(
    string PatientKey,
    string AuthorizedMemberKey,
    RelationshipType RelationshipType,
    int AuthorizedDaysOffset);

public sealed record SeedInvite(
    string PatientKey,
    string AuthorizedMemberKey,
    RelationshipType RelationshipType,
    InviteStatus Status,
    int SentDaysOffset,
    int? RespondedDaysOffset);

public sealed record SeedNotification(string UserKey, string Title, bool IsRead, string? Message = null);

public sealed record SeedReminder(
    string PatientKey,
    string DoctorKey,
    string? AuthorizedMemberKey,
    string ReminderText,
    int ReminderDaysOffset,
    string? Title = null,
    string? Category = null);

public sealed record SeedFileDownloadRequest(
    string PatientKey,
    string? ReviewedBySecretaryKey,
    string Reason,
    string PurposeDescription,
    FileDownloadRequestStatus Status,
    int SubmittedHoursOffset,
    int? ReviewedHoursOffset,
    string? RejectionReason);

public sealed record SeedLabTestRequest(
    string TestName,
    string? LaboratoryTechnologistKey);

public sealed record SeedImagingTestRequest(
    string TestName,
    string? RadiologyTechnologistKey);

public sealed record SeedReport(
    string SecretaryKey,
    int GeneratedDaysOffset);

public sealed record SeedReportInformation(
    ReportFileFormat FileFormat,
    string FilePath,
    long FileSizeInBytes);

public sealed record SeedVerificationCode(
    string UserKey,
    string Code,
    VerificationPurpose Purpose,
    int ExpiresDaysOffset,
    bool IsUsed);
