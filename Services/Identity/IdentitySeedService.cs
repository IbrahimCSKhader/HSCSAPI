using HSCSAPI.Data;
using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Laboratory;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Notifications;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Radiology;
using HSCSAPI.Models.Relations;
using HSCSAPI.Models.Reminders;
using HSCSAPI.Models.Secretaries;
using HSCSAPI.Services.Auth;
using HSCSAPI.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HSCSAPI.Services.Identity;

public class IdentitySeedService
{
    private const string DefaultSeedPassword = "SeedPassword123";

    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly UserIdGeneratorService _userIdGenerator;
    private readonly SuperAdminSeedSettings _seedSettings;

    public IdentitySeedService(
        AppDbContext dbContext,
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        UserIdGeneratorService userIdGenerator,
        IOptions<SuperAdminSeedSettings> seedOptions)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _userIdGenerator = userIdGenerator;
        _seedSettings = seedOptions.Value;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken);
        var superAdmin = await EnsureSuperAdminAsync(cancellationToken);
        await SeedDomainDataAsync(superAdmin, cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in Enum.GetNames<UserSystemRole>())
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(new Role
            {
                Id = IdentityRoleIds.Get(Enum.Parse<UserSystemRole>(roleName)),
                Name = roleName
            });

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed role {roleName}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
            }
        }
    }

    private async Task<User> EnsureSuperAdminAsync(CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(string.IsNullOrWhiteSpace(_seedSettings.Email) ? "superadmin@seed.local" : _seedSettings.Email);
        var password = string.IsNullOrWhiteSpace(_seedSettings.Password) ? DefaultSeedPassword : _seedSettings.Password;
        var name = string.IsNullOrWhiteSpace(_seedSettings.Name) ? "System Super Admin" : _seedSettings.Name.Trim();

        var existingUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == _userManager.NormalizeEmail(email), cancellationToken);

        if (existingUser == null)
        {
            existingUser = new User
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = email,
                UserName = email,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(existingUser, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed super admin user: {string.Join(" ", createResult.Errors.Select(e => e.Description))}");
            }
        }
        else if (!existingUser.EmailConfirmed)
        {
            existingUser.EmailConfirmed = true;
            await _userManager.UpdateAsync(existingUser);
        }

        if (!await _userManager.IsInRoleAsync(existingUser, nameof(UserSystemRole.SuperAdmin)))
        {
            var roleResult = await _userManager.AddToRoleAsync(existingUser, nameof(UserSystemRole.SuperAdmin));
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to assign super admin role: {string.Join(" ", roleResult.Errors.Select(e => e.Description))}");
            }
        }

        return existingUser;
    }

    private async Task SeedDomainDataAsync(User superAdmin, CancellationToken cancellationToken)
    {
        var hasDomainData = await _dbContext.Clinics.AnyAsync(cancellationToken)
            || await _dbContext.Patients.AnyAsync(cancellationToken)
            || await _dbContext.Doctors.AnyAsync(cancellationToken)
            || await _dbContext.Secretaries.AnyAsync(cancellationToken)
            || await _dbContext.Appointments.AnyAsync(cancellationToken);

        if (hasDomainData)
        {
            return;
        }

        var clinicOne = new Clinic
        {
            Name = "Central Care Clinic",
            Address = "Hebron - Main Street",
            CreatedBySuperAdminUserId = superAdmin.Id
        };

        var clinicTwo = new Clinic
        {
            Name = "North Family Clinic",
            Address = "Nablus - City Center",
            CreatedBySuperAdminUserId = superAdmin.Id
        };

        _dbContext.Clinics.AddRange(clinicOne, clinicTwo);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var adminSecretary = await CreateSeedUserAsync(
            email: "secretary.admin@seed.local",
            name: "Clinic Admin Secretary",
            role: UserSystemRole.Secretary,
            clinicId: clinicOne.ClinicId,
            configureUser: user =>
            {
                user.PhoneNumber = "0599000001";
                user.Address = "Hebron";
                user.DateOfBirth = new DateOnly(1992, 4, 10);
                user.SecretaryProfile = new Secretary
                {
                    SecretaryId = user.Id,
                    User = user
                };
            },
            cancellationToken: cancellationToken);

        var staffSecretary = await CreateSeedUserAsync(
            email: "secretary.staff@seed.local",
            name: "Clinic Staff Secretary",
            role: UserSystemRole.Secretary,
            clinicId: clinicOne.ClinicId,
            configureUser: user =>
            {
                user.PhoneNumber = "0599000002";
                user.Address = "Hebron";
                user.DateOfBirth = new DateOnly(1994, 8, 15);
                user.SecretaryProfile = new Secretary
                {
                    SecretaryId = user.Id,
                    User = user
                };
            },
            cancellationToken: cancellationToken);

        var doctor = await CreateSeedUserAsync(
            email: "doctor@seed.local",
            name: "Dr. Samer Khalil",
            role: UserSystemRole.Doctor,
            clinicId: clinicOne.ClinicId,
            configureUser: user =>
            {
                user.PhoneNumber = "0599000003";
                user.Address = "Hebron";
                user.DateOfBirth = new DateOnly(1985, 1, 20);
                user.DoctorProfile = new Doctor
                {
                    DoctorId = user.Id,
                    ProfessionalLicenseNumber = "DOC-10001",
                    User = user
                };
            },
            cancellationToken: cancellationToken);

        var patientUserId = await _userIdGenerator.GenerateUserIdAsync(clinicOne.ClinicId, UserSystemRole.Patient, cancellationToken);
        var patient = await CreateSeedUserAsync(
            email: "patient@seed.local",
            name: "Alaa Nassar",
            role: UserSystemRole.Patient,
            clinicId: clinicOne.ClinicId,
            configureUser: user =>
            {
                user.PhoneNumber = "0599000004";
                user.Address = "Hebron";
                user.DateOfBirth = new DateOnly(1998, 6, 4);
                user.PatientProfile = new Patient
                {
                    PatientId = user.Id,
                    UserID = patientUserId,
                    Gender = Gender.Male,
                    BloodType = BloodType.OPositive,
                    User = user
                };
            },
            cancellationToken: cancellationToken);

        var authorizedMember = await CreateSeedUserAsync(
            email: "member@seed.local",
            name: "Mona Nassar",
            role: UserSystemRole.AuthorizedMember,
            clinicId: null,
            configureUser: user =>
            {
                user.PhoneNumber = "0599000005";
                user.Address = "Hebron";
                user.DateOfBirth = new DateOnly(1990, 11, 2);
                user.AuthorizedMemberProfile = new AuthorizedMember
                {
                    AuthorizedMemberId = user.Id,
                    User = user
                };
            },
            cancellationToken: cancellationToken);

        var laboratoryTechnologist = await CreateSeedUserAsync(
            email: "labtech@seed.local",
            name: "Rana Lab",
            role: UserSystemRole.LaboratoryTechnologist,
            clinicId: clinicOne.ClinicId,
            configureUser: user =>
            {
                user.PhoneNumber = "0599000006";
                user.Address = "Hebron";
                user.DateOfBirth = new DateOnly(1991, 9, 12);
                user.LaboratoryTechnologistProfile = new LaboratoryTechnologist
                {
                    LaboratoryTechnologistId = user.Id,
                    ProfessionalLicenseNumber = "LAB-10001",
                    User = user
                };
            },
            cancellationToken: cancellationToken);

        var radiologyTechnologist = await CreateSeedUserAsync(
            email: "radiology@seed.local",
            name: "Yousef Ray",
            role: UserSystemRole.RadiologyTechnologist,
            clinicId: clinicOne.ClinicId,
            configureUser: user =>
            {
                user.PhoneNumber = "0599000007";
                user.Address = "Hebron";
                user.DateOfBirth = new DateOnly(1993, 3, 8);
                user.RadiologyTechnologistProfile = new RadiologyTechnologist
                {
                    RadiologyTechnologistId = user.Id,
                    ProfessionalLicenseNumber = "RAD-10001",
                    User = user
                };
            },
            cancellationToken: cancellationToken);

        clinicOne.AdminSecretaryId = adminSecretary.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var slot = new AvailabilitySlot
        {
            DoctorId = doctor.Id,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            IsAvailable = false
        };

        _dbContext.AvailabilitySlots.Add(slot);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var appointment = new Appointment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            AvailabilitySlotId = slot.AvailabilitySlotId,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)),
            AppointmentTime = new TimeOnly(9, 0),
            Notes = "Initial seeded appointment."
        };

        _dbContext.Appointments.Add(appointment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var medicalFile = new MedicalFile
        {
            AppointmentId = appointment.AppointmentId,
            UploadedByDoctorId = doctor.Id,
            FileType = MedicalFileType.Pdf,
            FilePath = "/seed/medical-files/consultation-summary.pdf",
            EncryptedChecksum = "seed-checksum-001",
            FileSizeInBytes = 4096,
            SeverityLevel = SeverityLevel.Low,
            UploadedAt = DateTime.UtcNow.AddDays(-1)
        };

        var relation = new PatientAuthorizedMember
        {
            PatientId = patient.Id,
            AuthorizedMemberId = authorizedMember.Id,
            RelationshipType = RelationshipType.Mother,
            AuthorizedAt = DateTime.UtcNow.AddDays(-10)
        };

        var invite = new Invite
        {
            PatientId = patient.Id,
            AuthorizedMemberId = authorizedMember.Id,
            RelationshipType = RelationshipType.Mother,
            Status = InviteStatus.Accepted,
            SentAt = DateTime.UtcNow.AddDays(-12),
            RespondedAt = DateTime.UtcNow.AddDays(-11)
        };

        var patientNotification = new Notification
        {
            UserId = patient.Id,
            Title = "Your seeded appointment is ready.",
            IsRead = false
        };

        var doctorNotification = new Notification
        {
            UserId = doctor.Id,
            Title = "You have a seeded appointment on Monday.",
            IsRead = true
        };

        var reminder = new Reminder
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            AuthorizedMemberId = authorizedMember.Id,
            ReminderText = "Take your prescribed medication after breakfast.",
            ReminderAt = DateTime.UtcNow.AddDays(1)
        };

        var fileDownloadRequest = new FileDownloadRequest
        {
            PatientId = patient.Id,
            MedicalFileId = medicalFile.MedicalFileId,
            ReviewedBySecretaryId = staffSecretary.Id,
            Reason = "Need a copy for follow-up.",
            PurposeDescription = "Sharing with another specialist for a second opinion.",
            Status = FileDownloadRequestStatus.Approved,
            SubmittedAt = DateTime.UtcNow.AddHours(-12),
            ReviewedAt = DateTime.UtcNow.AddHours(-6)
        };

        var labTestRequest = new LabTestRequest
        {
            TestName = "Complete Blood Count",
            LaboratoryTechnologistId = laboratoryTechnologist.Id,
            ResultMedicalFileId = medicalFile.MedicalFileId
        };

        var imagingTestRequest = new ImagingTestRequest
        {
            TestName = "Chest X-Ray",
            RadiologyTechnologistId = radiologyTechnologist.Id,
            ResultMedicalFileId = medicalFile.MedicalFileId
        };

        var report = new Report
        {
            SecretaryId = adminSecretary.Id,
            GeneratedAt = DateTime.UtcNow.AddDays(-2)
        };

        _dbContext.AddRange(
            medicalFile,
            relation,
            invite,
            patientNotification,
            doctorNotification,
            reminder,
            fileDownloadRequest,
            labTestRequest,
            imagingTestRequest,
            report);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var reportInformation = new ReportInformation
        {
            ReportId = report.ReportId,
            FileFormat = ReportFileFormat.Pdf,
            FilePath = "/seed/reports/weekly-clinic-report.pdf",
            FileSizeInBytes = 2048
        };

        var emailVerificationCode = new UserVerificationCode
        {
            UserId = adminSecretary.Id,
            Code = "111111",
            Purpose = VerificationPurpose.EmailVerification,
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            IsUsed = true
        };

        var passwordResetCode = new UserVerificationCode
        {
            UserId = patient.Id,
            Code = "222222",
            Purpose = VerificationPurpose.PasswordReset,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsUsed = false
        };

        _dbContext.ReportInformations.Add(reportInformation);
        _dbContext.UserVerificationCodes.AddRange(emailVerificationCode, passwordResetCode);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> CreateSeedUserAsync(
        string email,
        string name,
        UserSystemRole role,
        Guid? clinicId,
        Action<User> configureUser,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var existingUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == _userManager.NormalizeEmail(normalizedEmail), cancellationToken);

        if (existingUser != null)
        {
            return existingUser;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = normalizedEmail,
            UserName = normalizedEmail,
            ClinicId = clinicId,
            EmailConfirmed = true
        };

        configureUser(user);

        var createResult = await _userManager.CreateAsync(user, DefaultSeedPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to seed user {email}: {string.Join(" ", createResult.Errors.Select(e => e.Description))}");
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role.ToString());
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to assign role {role} to {email}: {string.Join(" ", roleResult.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
