using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Laboratory;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Notifications;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Radiology;
using HSCSAPI.Models.Relations;
using HSCSAPI.Models.Reminders;
using HSCSAPI.Models.Secretaries;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Data;

public class AppDbContext : IdentityDbContext<
    User,
    Role,
    Guid,
    IdentityUserClaim<Guid>,
    UserRole,
    IdentityUserLogin<Guid>,
    IdentityRoleClaim<Guid>,
    IdentityUserToken<Guid>>
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

    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportInformation> ReportInformations => Set<ReportInformation>();

    public DbSet<MedicalFile> MedicalFiles => Set<MedicalFile>();
    public DbSet<FileDownloadRequest> FileDownloadRequests => Set<FileDownloadRequest>();

    public DbSet<LabTestRequest> LabTestRequests => Set<LabTestRequest>();
    public DbSet<ImagingTestRequest> ImagingTestRequests => Set<ImagingTestRequest>();

    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

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
