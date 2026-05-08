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
    private const string CentralClinicName = "Central Care Clinic";
    private const string NorthClinicName = "North Family Clinic";
    private const string AdminSecretaryEmail = "secretary.admin@seed.local";
    private const string StaffSecretaryEmail = "secretary.staff@seed.local";
    private const string DoctorEmail = "doctor@seed.local";
    private const string PatientEmail = "patient@seed.local";
    private const string AuthorizedMemberEmail = "member@seed.local";
    private const string LaboratoryTechnologistEmail = "labtech@seed.local";
    private const string RadiologyTechnologistEmail = "radiology@seed.local";
    private const string SeedMedicalFilePath = "/seed/medical-files/consultation-summary.pdf";
    private const string SeedReportFilePath = "/seed/reports/weekly-clinic-report.pdf";
    private const string SeedPatientNotificationTitle = "Your seeded appointment is ready.";
    private const string SeedDoctorNotificationTitle = "You have a seeded appointment on Monday.";
    private const string SeedReminderText = "Take your prescribed medication after breakfast.";
    private const string SeedFileDownloadReason = "Need a copy for follow-up.";
    private const string SeedFileDownloadPurpose = "Sharing with another specialist for a second opinion.";
    private const string SeedLabTestName = "Complete Blood Count";
    private const string SeedImagingTestName = "Chest X-Ray";

    private static readonly HashSet<string> SeedClinicNames = new(StringComparer.OrdinalIgnoreCase)
    {
        CentralClinicName,
        NorthClinicName
    };

    private static readonly HashSet<string> SeedDomainEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        AdminSecretaryEmail,
        StaffSecretaryEmail,
        DoctorEmail,
        PatientEmail,
        AuthorizedMemberEmail,
        LaboratoryTechnologistEmail,
        RadiologyTechnologistEmail
    };

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
        if (!await ShouldSeedDomainDataAsync(superAdmin.Id, cancellationToken))
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var nextMonday = GetNextFutureDate(utcNow, DayOfWeek.Monday);

        var clinicOne = await EnsureClinicAsync(
            CentralClinicName,
            "Hebron - Main Street",
            superAdmin.Id,
            cancellationToken);

        await EnsureClinicAsync(
            NorthClinicName,
            "Nablus - City Center",
            superAdmin.Id,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var adminSecretary = await EnsureSeedUserAccountAsync(
            email: AdminSecretaryEmail,
            name: "Clinic Admin Secretary",
            role: UserSystemRole.Secretary,
            clinicId: clinicOne.ClinicId,
            phoneNumber: "0599000001",
            address: "Hebron",
            dateOfBirth: new DateOnly(1992, 4, 10),
            cancellationToken: cancellationToken);
        await EnsureSecretaryProfileAsync(adminSecretary.Id, cancellationToken);

        var staffSecretary = await EnsureSeedUserAccountAsync(
            email: StaffSecretaryEmail,
            name: "Clinic Staff Secretary",
            role: UserSystemRole.Secretary,
            clinicId: clinicOne.ClinicId,
            phoneNumber: "0599000002",
            address: "Hebron",
            dateOfBirth: new DateOnly(1994, 8, 15),
            cancellationToken: cancellationToken);
        await EnsureSecretaryProfileAsync(staffSecretary.Id, cancellationToken);

        var doctor = await EnsureSeedUserAccountAsync(
            email: DoctorEmail,
            name: "Dr. Samer Khalil",
            role: UserSystemRole.Doctor,
            clinicId: clinicOne.ClinicId,
            phoneNumber: "0599000003",
            address: "Hebron",
            dateOfBirth: new DateOnly(1985, 1, 20),
            cancellationToken: cancellationToken);
        await EnsureDoctorProfileAsync(doctor.Id, "DOC-10001", cancellationToken);

        var patient = await EnsureSeedUserAccountAsync(
            email: PatientEmail,
            name: "Alaa Nassar",
            role: UserSystemRole.Patient,
            clinicId: clinicOne.ClinicId,
            phoneNumber: "0599000004",
            address: "Hebron",
            dateOfBirth: new DateOnly(1998, 6, 4),
            cancellationToken: cancellationToken);
        await EnsurePatientProfileAsync(patient.Id, clinicOne.ClinicId, Gender.Male, BloodType.OPositive, cancellationToken);

        var authorizedMember = await EnsureSeedUserAccountAsync(
            email: AuthorizedMemberEmail,
            name: "Mona Nassar",
            role: UserSystemRole.AuthorizedMember,
            clinicId: null,
            phoneNumber: "0599000005",
            address: "Hebron",
            dateOfBirth: new DateOnly(1990, 11, 2),
            cancellationToken: cancellationToken);
        await EnsureAuthorizedMemberProfileAsync(authorizedMember.Id, cancellationToken);

        var laboratoryTechnologist = await EnsureSeedUserAccountAsync(
            email: LaboratoryTechnologistEmail,
            name: "Rana Lab",
            role: UserSystemRole.LaboratoryTechnologist,
            clinicId: clinicOne.ClinicId,
            phoneNumber: "0599000006",
            address: "Hebron",
            dateOfBirth: new DateOnly(1991, 9, 12),
            cancellationToken: cancellationToken);
        await EnsureLaboratoryTechnologistProfileAsync(laboratoryTechnologist.Id, "LAB-10001", cancellationToken);

        var radiologyTechnologist = await EnsureSeedUserAccountAsync(
            email: RadiologyTechnologistEmail,
            name: "Yousef Ray",
            role: UserSystemRole.RadiologyTechnologist,
            clinicId: clinicOne.ClinicId,
            phoneNumber: "0599000007",
            address: "Hebron",
            dateOfBirth: new DateOnly(1993, 3, 8),
            cancellationToken: cancellationToken);
        await EnsureRadiologyTechnologistProfileAsync(radiologyTechnologist.Id, "RAD-10001", cancellationToken);

        clinicOne.AdminSecretaryId = adminSecretary.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var slot = await EnsureAvailabilitySlotAsync(
            doctor.Id,
            DayOfWeek.Monday,
            new TimeOnly(9, 0),
            new TimeOnly(9, 30),
            isAvailable: false,
            cancellationToken);

        var appointment = await EnsureAppointmentAsync(
            doctor.Id,
            patient.Id,
            slot.AvailabilitySlotId,
            nextMonday,
            new TimeOnly(9, 0),
            "Initial seeded appointment.",
            cancellationToken);

        slot.IsAvailable = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var medicalFile = await EnsureMedicalFileAsync(
            appointment.AppointmentId,
            doctor.Id,
            MedicalFileType.Pdf,
            SeedMedicalFilePath,
            "seed-checksum-001",
            4096,
            SeverityLevel.Low,
            utcNow.AddDays(-1),
            cancellationToken);

        await EnsurePatientAuthorizedMemberAsync(
            patient.Id,
            authorizedMember.Id,
            RelationshipType.Mother,
            utcNow.AddDays(-10),
            cancellationToken);

        await EnsureInviteAsync(
            patient.Id,
            authorizedMember.Id,
            RelationshipType.Mother,
            InviteStatus.Accepted,
            utcNow.AddDays(-12),
            utcNow.AddDays(-11),
            cancellationToken);

        await EnsureNotificationAsync(
            patient.Id,
            SeedPatientNotificationTitle,
            isRead: false,
            cancellationToken);

        await EnsureNotificationAsync(
            doctor.Id,
            SeedDoctorNotificationTitle,
            isRead: true,
            cancellationToken);

        await EnsureReminderAsync(
            patient.Id,
            doctor.Id,
            authorizedMember.Id,
            SeedReminderText,
            utcNow.AddDays(1),
            cancellationToken);

        await EnsureFileDownloadRequestAsync(
            patient.Id,
            medicalFile.MedicalFileId,
            staffSecretary.Id,
            SeedFileDownloadReason,
            SeedFileDownloadPurpose,
            FileDownloadRequestStatus.Approved,
            utcNow.AddHours(-12),
            utcNow.AddHours(-6),
            rejectionReason: null,
            cancellationToken);

        await EnsureLabTestRequestAsync(
            SeedLabTestName,
            laboratoryTechnologist.Id,
            medicalFile.MedicalFileId,
            cancellationToken);

        await EnsureImagingTestRequestAsync(
            SeedImagingTestName,
            radiologyTechnologist.Id,
            medicalFile.MedicalFileId,
            cancellationToken);

        var report = await EnsureReportAsync(
            adminSecretary.Id,
            utcNow.AddDays(-2),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await EnsureReportInformationAsync(
            report.ReportId,
            ReportFileFormat.Pdf,
            SeedReportFilePath,
            2048,
            cancellationToken);

        await EnsureUserVerificationCodeAsync(
            adminSecretary.Id,
            "111111",
            VerificationPurpose.EmailVerification,
            utcNow.AddDays(-1),
            isUsed: true,
            cancellationToken);

        await EnsureUserVerificationCodeAsync(
            patient.Id,
            "222222",
            VerificationPurpose.PasswordReset,
            utcNow.AddDays(7),
            isUsed: false,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> ShouldSeedDomainDataAsync(Guid superAdminId, CancellationToken cancellationToken)
    {
        var hasSeedMarkers = await _dbContext.Clinics.AnyAsync(c => SeedClinicNames.Contains(c.Name), cancellationToken)
            || await _dbContext.Users.AnyAsync(
                u => u.Email != null && SeedDomainEmails.Contains(u.Email),
                cancellationToken);

        if (hasSeedMarkers)
        {
            return true;
        }

        var hasAnyDomainData = await _dbContext.Users.AnyAsync(u => u.Id != superAdminId, cancellationToken)
            || await _dbContext.Clinics.AnyAsync(cancellationToken)
            || await _dbContext.Patients.AnyAsync(cancellationToken)
            || await _dbContext.Doctors.AnyAsync(cancellationToken)
            || await _dbContext.Secretaries.AnyAsync(cancellationToken)
            || await _dbContext.AuthorizedMembers.AnyAsync(cancellationToken)
            || await _dbContext.LaboratoryTechnologists.AnyAsync(cancellationToken)
            || await _dbContext.RadiologyTechnologists.AnyAsync(cancellationToken)
            || await _dbContext.UserVerificationCodes.AnyAsync(cancellationToken)
            || await _dbContext.PatientAuthorizedMembers.AnyAsync(cancellationToken)
            || await _dbContext.Invites.AnyAsync(cancellationToken)
            || await _dbContext.Notifications.AnyAsync(cancellationToken)
            || await _dbContext.Reminders.AnyAsync(cancellationToken)
            || await _dbContext.Reports.AnyAsync(cancellationToken)
            || await _dbContext.ReportInformations.AnyAsync(cancellationToken)
            || await _dbContext.MedicalFiles.AnyAsync(cancellationToken)
            || await _dbContext.FileDownloadRequests.AnyAsync(cancellationToken)
            || await _dbContext.LabTestRequests.AnyAsync(cancellationToken)
            || await _dbContext.ImagingTestRequests.AnyAsync(cancellationToken)
            || await _dbContext.AvailabilitySlots.AnyAsync(cancellationToken)
            || await _dbContext.Appointments.AnyAsync(cancellationToken);

        return !hasAnyDomainData;
    }

    private async Task<Clinic> EnsureClinicAsync(
        string name,
        string? address,
        Guid createdBySuperAdminUserId,
        CancellationToken cancellationToken)
    {
        var clinic = await _dbContext.Clinics
            .FirstOrDefaultAsync(c => c.Name == name, cancellationToken);

        if (clinic == null)
        {
            clinic = new Clinic
            {
                Name = name,
                Address = address,
                CreatedBySuperAdminUserId = createdBySuperAdminUserId
            };

            _dbContext.Clinics.Add(clinic);
            return clinic;
        }

        clinic.Address = address;
        clinic.CreatedBySuperAdminUserId = createdBySuperAdminUserId;
        return clinic;
    }

    private async Task<User> EnsureSeedUserAccountAsync(
        string email,
        string name,
        UserSystemRole role,
        Guid? clinicId,
        string? phoneNumber,
        string? address,
        DateOnly? dateOfBirth,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedLookup = _userManager.NormalizeEmail(normalizedEmail);
        var user = await _userManager.Users
            .FirstOrDefaultAsync(
                u => u.NormalizedEmail == normalizedLookup || u.Email == normalizedEmail,
                cancellationToken);

        var isNewUser = user == null;
        user ??= new User
        {
            Id = Guid.NewGuid()
        };

        user.Name = name;
        user.Email = normalizedEmail;
        user.UserName = normalizedEmail;
        user.ClinicId = clinicId;
        user.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        user.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        user.DateOfBirth = dateOfBirth;
        user.EmailConfirmed = true;

        IdentityResult persistenceResult;
        if (isNewUser)
        {
            persistenceResult = await _userManager.CreateAsync(user, DefaultSeedPassword);
        }
        else
        {
            persistenceResult = await _userManager.UpdateAsync(user);
        }

        if (!persistenceResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to seed user {email}: {string.Join(" ", persistenceResult.Errors.Select(e => e.Description))}");
        }

        if (!await _userManager.IsInRoleAsync(user, role.ToString()))
        {
            var roleResult = await _userManager.AddToRoleAsync(user, role.ToString());
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to assign role {role} to {email}: {string.Join(" ", roleResult.Errors.Select(e => e.Description))}");
            }
        }

        return user;
    }

    private async Task EnsureSecretaryProfileAsync(Guid secretaryId, CancellationToken cancellationToken)
    {
        if (await _dbContext.Secretaries.AnyAsync(x => x.SecretaryId == secretaryId, cancellationToken))
        {
            return;
        }

        _dbContext.Secretaries.Add(new Secretary
        {
            SecretaryId = secretaryId
        });
    }

    private async Task EnsureDoctorProfileAsync(Guid doctorId, string professionalLicenseNumber, CancellationToken cancellationToken)
    {
        var doctor = await _dbContext.Doctors
            .FirstOrDefaultAsync(x => x.DoctorId == doctorId, cancellationToken);

        if (doctor == null)
        {
            _dbContext.Doctors.Add(new Doctor
            {
                DoctorId = doctorId,
                ProfessionalLicenseNumber = professionalLicenseNumber
            });

            return;
        }

        doctor.ProfessionalLicenseNumber = professionalLicenseNumber;
    }

    private async Task EnsurePatientProfileAsync(
        Guid patientId,
        Guid clinicId,
        Gender gender,
        BloodType? bloodType,
        CancellationToken cancellationToken)
    {
        var patient = await _dbContext.Patients
            .FirstOrDefaultAsync(x => x.PatientId == patientId, cancellationToken);

        if (patient == null)
        {
            _dbContext.Patients.Add(new Patient
            {
                PatientId = patientId,
                UserID = await _userIdGenerator.GenerateUserIdAsync(clinicId, UserSystemRole.Patient, cancellationToken),
                Gender = gender,
                BloodType = bloodType
            });

            return;
        }

        if (string.IsNullOrWhiteSpace(patient.UserID))
        {
            patient.UserID = await _userIdGenerator.GenerateUserIdAsync(clinicId, UserSystemRole.Patient, cancellationToken);
        }

        patient.Gender = gender;
        patient.BloodType = bloodType;
    }

    private async Task EnsureAuthorizedMemberProfileAsync(Guid authorizedMemberId, CancellationToken cancellationToken)
    {
        if (await _dbContext.AuthorizedMembers.AnyAsync(x => x.AuthorizedMemberId == authorizedMemberId, cancellationToken))
        {
            return;
        }

        _dbContext.AuthorizedMembers.Add(new AuthorizedMember
        {
            AuthorizedMemberId = authorizedMemberId
        });
    }

    private async Task EnsureLaboratoryTechnologistProfileAsync(
        Guid laboratoryTechnologistId,
        string professionalLicenseNumber,
        CancellationToken cancellationToken)
    {
        var laboratoryTechnologist = await _dbContext.LaboratoryTechnologists
            .FirstOrDefaultAsync(x => x.LaboratoryTechnologistId == laboratoryTechnologistId, cancellationToken);

        if (laboratoryTechnologist == null)
        {
            _dbContext.LaboratoryTechnologists.Add(new LaboratoryTechnologist
            {
                LaboratoryTechnologistId = laboratoryTechnologistId,
                ProfessionalLicenseNumber = professionalLicenseNumber
            });

            return;
        }

        laboratoryTechnologist.ProfessionalLicenseNumber = professionalLicenseNumber;
    }

    private async Task EnsureRadiologyTechnologistProfileAsync(
        Guid radiologyTechnologistId,
        string professionalLicenseNumber,
        CancellationToken cancellationToken)
    {
        var radiologyTechnologist = await _dbContext.RadiologyTechnologists
            .FirstOrDefaultAsync(x => x.RadiologyTechnologistId == radiologyTechnologistId, cancellationToken);

        if (radiologyTechnologist == null)
        {
            _dbContext.RadiologyTechnologists.Add(new RadiologyTechnologist
            {
                RadiologyTechnologistId = radiologyTechnologistId,
                ProfessionalLicenseNumber = professionalLicenseNumber
            });

            return;
        }

        radiologyTechnologist.ProfessionalLicenseNumber = professionalLicenseNumber;
    }

    private async Task<AvailabilitySlot> EnsureAvailabilitySlotAsync(
        Guid doctorId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        bool isAvailable,
        CancellationToken cancellationToken)
    {
        var slot = await _dbContext.AvailabilitySlots
            .FirstOrDefaultAsync(
                x => x.DoctorId == doctorId
                    && x.DayOfWeek == dayOfWeek
                    && x.StartTime == startTime
                    && x.EndTime == endTime,
                cancellationToken);

        if (slot == null)
        {
            slot = new AvailabilitySlot
            {
                DoctorId = doctorId,
                DayOfWeek = dayOfWeek,
                StartTime = startTime,
                EndTime = endTime,
                IsAvailable = isAvailable
            };

            _dbContext.AvailabilitySlots.Add(slot);
            return slot;
        }

        slot.IsAvailable = isAvailable;
        return slot;
    }

    private async Task<Appointment> EnsureAppointmentAsync(
        Guid doctorId,
        Guid patientId,
        Guid availabilitySlotId,
        DateOnly appointmentDate,
        TimeOnly appointmentTime,
        string? notes,
        CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.AvailabilitySlotId == availabilitySlotId, cancellationToken);

        if (appointment == null)
        {
            appointment = new Appointment
            {
                DoctorId = doctorId,
                PatientId = patientId,
                AvailabilitySlotId = availabilitySlotId,
                AppointmentDate = appointmentDate,
                AppointmentTime = appointmentTime,
                Notes = notes
            };

            _dbContext.Appointments.Add(appointment);
            return appointment;
        }

        appointment.DoctorId = doctorId;
        appointment.PatientId = patientId;
        appointment.AppointmentDate = appointmentDate;
        appointment.AppointmentTime = appointmentTime;
        appointment.Notes = notes;
        return appointment;
    }

    private async Task<MedicalFile> EnsureMedicalFileAsync(
        Guid appointmentId,
        Guid uploadedByDoctorId,
        MedicalFileType fileType,
        string filePath,
        string encryptedChecksum,
        long fileSizeInBytes,
        SeverityLevel severityLevel,
        DateTime uploadedAt,
        CancellationToken cancellationToken)
    {
        var medicalFile = await _dbContext.MedicalFiles
            .FirstOrDefaultAsync(x => x.FilePath == filePath, cancellationToken);

        if (medicalFile == null)
        {
            medicalFile = new MedicalFile
            {
                AppointmentId = appointmentId,
                UploadedByDoctorId = uploadedByDoctorId,
                FileType = fileType,
                FilePath = filePath,
                EncryptedChecksum = encryptedChecksum,
                FileSizeInBytes = fileSizeInBytes,
                SeverityLevel = severityLevel,
                UploadedAt = uploadedAt
            };

            _dbContext.MedicalFiles.Add(medicalFile);
            return medicalFile;
        }

        medicalFile.AppointmentId = appointmentId;
        medicalFile.UploadedByDoctorId = uploadedByDoctorId;
        medicalFile.FileType = fileType;
        medicalFile.EncryptedChecksum = encryptedChecksum;
        medicalFile.FileSizeInBytes = fileSizeInBytes;
        medicalFile.SeverityLevel = severityLevel;
        medicalFile.UploadedAt = uploadedAt;
        return medicalFile;
    }

    private async Task EnsurePatientAuthorizedMemberAsync(
        Guid patientId,
        Guid authorizedMemberId,
        RelationshipType relationshipType,
        DateTime authorizedAt,
        CancellationToken cancellationToken)
    {
        var relation = await _dbContext.PatientAuthorizedMembers
            .FirstOrDefaultAsync(
                x => x.PatientId == patientId && x.AuthorizedMemberId == authorizedMemberId,
                cancellationToken);

        if (relation == null)
        {
            _dbContext.PatientAuthorizedMembers.Add(new PatientAuthorizedMember
            {
                PatientId = patientId,
                AuthorizedMemberId = authorizedMemberId,
                RelationshipType = relationshipType,
                AuthorizedAt = authorizedAt
            });

            return;
        }

        relation.RelationshipType = relationshipType;
        relation.AuthorizedAt = authorizedAt;
    }

    private async Task EnsureInviteAsync(
        Guid patientId,
        Guid authorizedMemberId,
        RelationshipType relationshipType,
        InviteStatus status,
        DateTime sentAt,
        DateTime? respondedAt,
        CancellationToken cancellationToken)
    {
        var invite = await _dbContext.Invites
            .FirstOrDefaultAsync(
                x => x.PatientId == patientId
                    && x.AuthorizedMemberId == authorizedMemberId
                    && x.RelationshipType == relationshipType,
                cancellationToken);

        if (invite == null)
        {
            _dbContext.Invites.Add(new Invite
            {
                PatientId = patientId,
                AuthorizedMemberId = authorizedMemberId,
                RelationshipType = relationshipType,
                Status = status,
                SentAt = sentAt,
                RespondedAt = respondedAt
            });

            return;
        }

        invite.Status = status;
        invite.SentAt = sentAt;
        invite.RespondedAt = respondedAt;
    }

    private async Task EnsureNotificationAsync(
        Guid userId,
        string title,
        bool isRead,
        CancellationToken cancellationToken)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Title == title, cancellationToken);

        if (notification == null)
        {
            _dbContext.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                IsRead = isRead
            });

            return;
        }

        notification.IsRead = isRead;
    }

    private async Task EnsureReminderAsync(
        Guid patientId,
        Guid doctorId,
        Guid? authorizedMemberId,
        string reminderText,
        DateTime reminderAt,
        CancellationToken cancellationToken)
    {
        var reminder = await _dbContext.Reminders
            .FirstOrDefaultAsync(
                x => x.PatientId == patientId
                    && x.DoctorId == doctorId
                    && x.ReminderText == reminderText,
                cancellationToken);

        if (reminder == null)
        {
            _dbContext.Reminders.Add(new Reminder
            {
                PatientId = patientId,
                DoctorId = doctorId,
                AuthorizedMemberId = authorizedMemberId,
                ReminderText = reminderText,
                ReminderAt = reminderAt
            });

            return;
        }

        reminder.AuthorizedMemberId = authorizedMemberId;
        reminder.ReminderAt = reminderAt;
    }

    private async Task EnsureFileDownloadRequestAsync(
        Guid patientId,
        Guid medicalFileId,
        Guid? reviewedBySecretaryId,
        string reason,
        string purposeDescription,
        FileDownloadRequestStatus status,
        DateTime submittedAt,
        DateTime? reviewedAt,
        string? rejectionReason,
        CancellationToken cancellationToken)
    {
        var request = await _dbContext.FileDownloadRequests
            .FirstOrDefaultAsync(
                x => x.PatientId == patientId
                    && x.MedicalFileId == medicalFileId
                    && x.Reason == reason,
                cancellationToken);

        if (request == null)
        {
            _dbContext.FileDownloadRequests.Add(new FileDownloadRequest
            {
                PatientId = patientId,
                MedicalFileId = medicalFileId,
                ReviewedBySecretaryId = reviewedBySecretaryId,
                Reason = reason,
                PurposeDescription = purposeDescription,
                Status = status,
                SubmittedAt = submittedAt,
                ReviewedAt = reviewedAt,
                RejectionReason = rejectionReason
            });

            return;
        }

        request.ReviewedBySecretaryId = reviewedBySecretaryId;
        request.PurposeDescription = purposeDescription;
        request.Status = status;
        request.SubmittedAt = submittedAt;
        request.ReviewedAt = reviewedAt;
        request.RejectionReason = rejectionReason;
    }

    private async Task EnsureLabTestRequestAsync(
        string testName,
        Guid? laboratoryTechnologistId,
        Guid? resultMedicalFileId,
        CancellationToken cancellationToken)
    {
        var request = await _dbContext.LabTestRequests
            .FirstOrDefaultAsync(
                x => x.TestName == testName && x.ResultMedicalFileId == resultMedicalFileId,
                cancellationToken);

        if (request == null)
        {
            _dbContext.LabTestRequests.Add(new LabTestRequest
            {
                TestName = testName,
                LaboratoryTechnologistId = laboratoryTechnologistId,
                ResultMedicalFileId = resultMedicalFileId
            });

            return;
        }

        request.LaboratoryTechnologistId = laboratoryTechnologistId;
    }

    private async Task EnsureImagingTestRequestAsync(
        string testName,
        Guid? radiologyTechnologistId,
        Guid? resultMedicalFileId,
        CancellationToken cancellationToken)
    {
        var request = await _dbContext.ImagingTestRequests
            .FirstOrDefaultAsync(
                x => x.TestName == testName && x.ResultMedicalFileId == resultMedicalFileId,
                cancellationToken);

        if (request == null)
        {
            _dbContext.ImagingTestRequests.Add(new ImagingTestRequest
            {
                TestName = testName,
                RadiologyTechnologistId = radiologyTechnologistId,
                ResultMedicalFileId = resultMedicalFileId
            });

            return;
        }

        request.RadiologyTechnologistId = radiologyTechnologistId;
    }

    private async Task<Report> EnsureReportAsync(
        Guid secretaryId,
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        var report = await _dbContext.Reports
            .Include(x => x.ReportInformations)
            .FirstOrDefaultAsync(
                x => x.SecretaryId == secretaryId
                    && x.ReportInformations.Any(info => info.FilePath == SeedReportFilePath),
                cancellationToken);

        if (report == null)
        {
            report = new Report
            {
                SecretaryId = secretaryId,
                GeneratedAt = generatedAt
            };

            _dbContext.Reports.Add(report);
            return report;
        }

        report.GeneratedAt = generatedAt;
        return report;
    }

    private async Task EnsureReportInformationAsync(
        Guid reportId,
        ReportFileFormat fileFormat,
        string filePath,
        long fileSizeInBytes,
        CancellationToken cancellationToken)
    {
        var reportInformation = await _dbContext.ReportInformations
            .FirstOrDefaultAsync(x => x.FilePath == filePath, cancellationToken);

        if (reportInformation == null)
        {
            _dbContext.ReportInformations.Add(new ReportInformation
            {
                ReportId = reportId,
                FileFormat = fileFormat,
                FilePath = filePath,
                FileSizeInBytes = fileSizeInBytes
            });

            return;
        }

        reportInformation.ReportId = reportId;
        reportInformation.FileFormat = fileFormat;
        reportInformation.FileSizeInBytes = fileSizeInBytes;
    }

    private async Task EnsureUserVerificationCodeAsync(
        Guid userId,
        string code,
        VerificationPurpose purpose,
        DateTime expiresAt,
        bool isUsed,
        CancellationToken cancellationToken)
    {
        var verificationCode = await _dbContext.UserVerificationCodes
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.Purpose == purpose && x.Code == code,
                cancellationToken);

        if (verificationCode == null)
        {
            _dbContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = userId,
                Code = code,
                Purpose = purpose,
                ExpiresAt = expiresAt,
                IsUsed = isUsed
            });

            return;
        }

        verificationCode.ExpiresAt = expiresAt;
        verificationCode.IsUsed = isUsed;
    }

    private static DateOnly GetNextFutureDate(DateTime utcNow, DayOfWeek targetDay)
    {
        var currentDate = DateOnly.FromDateTime(utcNow.Date);
        var daysUntilTarget = ((int)targetDay - (int)currentDate.DayOfWeek + 7) % 7;
        if (daysUntilTarget == 0)
        {
            daysUntilTarget = 7;
        }

        return currentDate.AddDays(daysUntilTarget);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
