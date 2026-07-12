using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Chats;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Laboratory;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Notifications;
using HSCSAPI.Models.PatientProfile;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Radiology;
using HSCSAPI.Models.Relations;
using HSCSAPI.Models.Reminders;
using HSCSAPI.Models.Secretaries;
using HSCSAPI.Models.Standards;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AppIdentityDbContextBase = Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<
    HSCSAPI.Models.Identity.User,
    HSCSAPI.Models.Identity.Role,
    System.Guid,
    Microsoft.AspNetCore.Identity.IdentityUserClaim<System.Guid>,
    HSCSAPI.Models.Identity.UserRole,
    Microsoft.AspNetCore.Identity.IdentityUserLogin<System.Guid>,
    Microsoft.AspNetCore.Identity.IdentityRoleClaim<System.Guid>,
    Microsoft.AspNetCore.Identity.IdentityUserToken<System.Guid>>;

namespace HSCSAPI.Data;

public class AppDbContext : AppIdentityDbContextBase
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Clinic> Clinics => Set<Clinic>();

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Secretary> Secretaries => Set<Secretary>();
    public DbSet<AuthorizedMember> AuthorizedMembers => Set<AuthorizedMember>();
    public DbSet<LaboratoryTechnologist> LaboratoryTechnologists => Set<LaboratoryTechnologist>();
    public DbSet<RadiologyTechnologist> RadiologyTechnologists => Set<RadiologyTechnologist>();
    public DbSet<UserVerificationCode> UserVerificationCodes => Set<UserVerificationCode>();

    public DbSet<PatientAuthorizedMember> PatientAuthorizedMembers => Set<PatientAuthorizedMember>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<ReminderPreference> ReminderPreferences => Set<ReminderPreference>();
    public DbSet<ExternalPatientProfileShare> ExternalPatientProfileShares => Set<ExternalPatientProfileShare>();

    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportInformation> ReportInformations => Set<ReportInformation>();

    public DbSet<MedicalFile> MedicalFiles => Set<MedicalFile>();
    public DbSet<FileDownloadRequest> FileDownloadRequests => Set<FileDownloadRequest>();

    public DbSet<LabTestRequest> LabTestRequests => Set<LabTestRequest>();
    public DbSet<LabTestTemplate> LabTestTemplates => Set<LabTestTemplate>();
    public DbSet<LabTestFieldDefinition> LabTestFieldDefinitions => Set<LabTestFieldDefinition>();
    public DbSet<LabTestResult> LabTestResults => Set<LabTestResult>();
    public DbSet<LabTestResultValue> LabTestResultValues => Set<LabTestResultValue>();
    public DbSet<ImagingTestRequest> ImagingTestRequests => Set<ImagingTestRequest>();
    public DbSet<LoincCode> LoincCodes => Set<LoincCode>();
    public DbSet<DiagnosisCode> DiagnosisCodes => Set<DiagnosisCode>();
    public DbSet<RadiologyExamCatalog> RadiologyExamCatalogs => Set<RadiologyExamCatalog>();

    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
