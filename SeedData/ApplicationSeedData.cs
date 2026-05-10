using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Laboratory;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Radiology;
using HSCSAPI.Models.Relations;
using HSCSAPI.Models.Secretaries;

namespace HSCSAPI.SeedData;

public static class ApplicationSeedData
{
    public const string DefaultSeedPassword = "SeedPassword123";
    public const string DefaultSuperAdminEmail = "superadmin@seed.local";
    public const string DefaultSuperAdminName = "System Super Admin";

    public const string CentralClinicKey = "central-care-clinic";
    public const string NorthClinicKey = "north-family-clinic";
    public const string AdminSecretaryKey = "admin-secretary";
    public const string StaffSecretaryKey = "staff-secretary";
    public const string DoctorKey = "doctor";
    public const string PatientKey = "patient";
    public const string AuthorizedMemberKey = "authorized-member";
    public const string LaboratoryTechnologistKey = "laboratory-technologist";
    public const string RadiologyTechnologistKey = "radiology-technologist";

    public static readonly IReadOnlyList<SeedClinic> Clinics =
    [
        new SeedClinic(
            Key: CentralClinicKey,
            Name: "Central Care Clinic",
            Address: "Hebron - Main Street"),
        new SeedClinic(
            Key: NorthClinicKey,
            Name: "North Family Clinic",
            Address: "Nablus - City Center")
    ];

    public static readonly IReadOnlyList<SeedUser> Users =
    [
        new SeedUser(
            Key: AdminSecretaryKey,
            Email: "secretary.admin@seed.local",
            Name: "Clinic Admin Secretary",
            Role: UserSystemRole.Secretary,
            ClinicKey: CentralClinicKey,
            PhoneNumber: "0599000001",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1992, 4, 10)),
        new SeedUser(
            Key: StaffSecretaryKey,
            Email: "secretary.staff@seed.local",
            Name: "Clinic Staff Secretary",
            Role: UserSystemRole.Secretary,
            ClinicKey: CentralClinicKey,
            PhoneNumber: "0599000002",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1994, 8, 15)),
        new SeedUser(
            Key: DoctorKey,
            Email: "doctor@seed.local",
            Name: "Dr. Samer Khalil",
            Role: UserSystemRole.Doctor,
            ClinicKey: CentralClinicKey,
            PhoneNumber: "0599000003",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1985, 1, 20),
            ProfessionalLicenseNumber: "DOC-10001"),
        new SeedUser(
            Key: PatientKey,
            Email: "patient@seed.local",
            Name: "Alaa Nassar",
            Role: UserSystemRole.Patient,
            ClinicKey: CentralClinicKey,
            PhoneNumber: "0599000004",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1998, 6, 4),
            Gender: Gender.Male,
            BloodType: HSCSAPI.Models.Enums.BloodType.OPositive),
        new SeedUser(
            Key: AuthorizedMemberKey,
            Email: "member@seed.local",
            Name: "Mona Nassar",
            Role: UserSystemRole.AuthorizedMember,
            ClinicKey: null,
            PhoneNumber: "0599000005",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1990, 11, 2)),
        new SeedUser(
            Key: LaboratoryTechnologistKey,
            Email: "labtech@seed.local",
            Name: "Rana Lab",
            Role: UserSystemRole.LaboratoryTechnologist,
            ClinicKey: CentralClinicKey,
            PhoneNumber: "0599000006",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1991, 9, 12),
            ProfessionalLicenseNumber: "LAB-10001"),
        new SeedUser(
            Key: RadiologyTechnologistKey,
            Email: "radiology@seed.local",
            Name: "Yousef Ray",
            Role: UserSystemRole.RadiologyTechnologist,
            ClinicKey: CentralClinicKey,
            PhoneNumber: "0599000007",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1993, 3, 8),
            ProfessionalLicenseNumber: "RAD-10001")
    ];

    public static readonly SeedAvailabilitySlot PrimaryAvailabilitySlot = new(
        DoctorKey: DoctorKey,
        DayOfWeek: DayOfWeek.Monday,
        StartTime: new TimeOnly(9, 0),
        EndTime: new TimeOnly(9, 30),
        IsAvailable: false);

    public static readonly SeedAppointment PrimaryAppointment = new(
        DoctorKey: DoctorKey,
        PatientKey: PatientKey,
        DayOfWeek: DayOfWeek.Monday,
        AppointmentTime: new TimeOnly(9, 0),
        Notes: "Initial seeded appointment.");

    public static readonly SeedMedicalFile PrimaryMedicalFile = new(
        UploadedByDoctorKey: DoctorKey,
        FileType: MedicalFileType.Pdf,
        FilePath: "/seed/medical-files/consultation-summary.pdf",
        EncryptedChecksum: "seed-checksum-001",
        FileSizeInBytes: 4096,
        SeverityLevel: SeverityLevel.Low,
        UploadedDaysOffset: -1);

    public static readonly SeedPatientAuthorization PrimaryPatientAuthorization = new(
        PatientKey: PatientKey,
        AuthorizedMemberKey: AuthorizedMemberKey,
        RelationshipType: RelationshipType.Mother,
        AuthorizedDaysOffset: -10);

    public static readonly SeedInvite PrimaryInvite = new(
        PatientKey: PatientKey,
        AuthorizedMemberKey: AuthorizedMemberKey,
        RelationshipType: RelationshipType.Mother,
        Status: InviteStatus.Accepted,
        SentDaysOffset: -12,
        RespondedDaysOffset: -11);

    public static readonly IReadOnlyList<SeedNotification> Notifications =
    [
        new SeedNotification(
            UserKey: PatientKey,
            Title: "Your seeded appointment is ready.",
            IsRead: false),
        new SeedNotification(
            UserKey: DoctorKey,
            Title: "You have a seeded appointment on Monday.",
            IsRead: true)
    ];

    public static readonly SeedReminder PrimaryReminder = new(
        PatientKey: PatientKey,
        DoctorKey: DoctorKey,
        AuthorizedMemberKey: AuthorizedMemberKey,
        ReminderText: "Take your prescribed medication after breakfast.",
        ReminderDaysOffset: 1);

    public static readonly SeedFileDownloadRequest PrimaryFileDownloadRequest = new(
        PatientKey: PatientKey,
        ReviewedBySecretaryKey: StaffSecretaryKey,
        Reason: "Need a copy for follow-up.",
        PurposeDescription: "Sharing with another specialist for a second opinion.",
        Status: FileDownloadRequestStatus.Approved,
        SubmittedHoursOffset: -12,
        ReviewedHoursOffset: -6,
        RejectionReason: null);

    public static readonly SeedLabTestRequest PrimaryLabTestRequest = new(
        TestName: "Complete Blood Count",
        LaboratoryTechnologistKey: LaboratoryTechnologistKey);

    public static readonly SeedImagingTestRequest PrimaryImagingTestRequest = new(
        TestName: "Chest X-Ray",
        RadiologyTechnologistKey: RadiologyTechnologistKey);

    public static readonly SeedReport PrimaryReport = new(
        SecretaryKey: AdminSecretaryKey,
        GeneratedDaysOffset: -2);

    public static readonly SeedReportInformation PrimaryReportInformation = new(
        FileFormat: ReportFileFormat.Pdf,
        FilePath: "/seed/reports/weekly-clinic-report.pdf",
        FileSizeInBytes: 2048);

    public static readonly IReadOnlyList<SeedVerificationCode> VerificationCodes =
    [
        new SeedVerificationCode(
            UserKey: AdminSecretaryKey,
            Code: "111111",
            Purpose: VerificationPurpose.EmailVerification,
            ExpiresDaysOffset: -1,
            IsUsed: true),
        new SeedVerificationCode(
            UserKey: PatientKey,
            Code: "222222",
            Purpose: VerificationPurpose.PasswordReset,
            ExpiresDaysOffset: 7,
            IsUsed: false)
    ];
}

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
    HSCSAPI.Models.Enums.BloodType? BloodType = null);

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

public sealed record SeedNotification(string UserKey, string Title, bool IsRead);

public sealed record SeedReminder(
    string PatientKey,
    string DoctorKey,
    string? AuthorizedMemberKey,
    string ReminderText,
    int ReminderDaysOffset);

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
