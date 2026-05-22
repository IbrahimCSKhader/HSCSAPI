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
using HSCSAPI.SeedData;
using HSCSAPI.Models.Secretaries;
using HSCSAPI.Services.Auth;
using HSCSAPI.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HSCSAPI.Services.Identity;

public class IdentitySeedService
{
    private static readonly HashSet<string> SeedClinicNames = ApplicationSeedData.Clinics
        .Select(clinic => clinic.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SeedDomainEmails = ApplicationSeedData.Users
        .Select(user => user.Email)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        var email = NormalizeEmail(string.IsNullOrWhiteSpace(_seedSettings.Email)
            ? ApplicationSeedData.DefaultSuperAdminEmail
            : _seedSettings.Email);
        var password = string.IsNullOrWhiteSpace(_seedSettings.Password)
            ? ApplicationSeedData.DefaultSeedPassword
            : _seedSettings.Password;
        var name = string.IsNullOrWhiteSpace(_seedSettings.Name)
            ? ApplicationSeedData.DefaultSuperAdminName
            : _seedSettings.Name.Trim();

        var existingUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == _userManager.NormalizeEmail(email), cancellationToken);

        if (existingUser == null)
        {
            existingUser = new User
            {
                Id = Guid.NewGuid(),
                Name = name,
                RegisteredAt = DateTime.UtcNow,
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

        if (existingUser.RegisteredAt == default)
        {
            existingUser.RegisteredAt = DateTime.UtcNow;
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
        var clinicsByKey = new Dictionary<string, Clinic>(StringComparer.OrdinalIgnoreCase);
        foreach (var clinicData in ApplicationSeedData.Clinics)
        {
            var clinic = await EnsureClinicAsync(
                clinicData.Name,
                clinicData.Address,
                superAdmin.Id,
                cancellationToken);

            clinicsByKey[clinicData.Key] = clinic;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var usersByKey = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
        foreach (var userData in ApplicationSeedData.Users)
        {
            var clinicId = ResolveClinicId(clinicsByKey, userData.ClinicKey);

            var user = await EnsureSeedUserAccountAsync(
                email: userData.Email,
                name: userData.Name,
                role: userData.Role,
                clinicId: clinicId,
                phoneNumber: userData.PhoneNumber,
                address: userData.Address,
                dateOfBirth: userData.DateOfBirth,
                cancellationToken: cancellationToken);

            await EnsureProfileAsync(userData, user.Id, clinicId, cancellationToken);
            usersByKey[userData.Key] = user;
        }

        clinicsByKey[ApplicationSeedData.CentralClinicKey].AdminSecretaryId = usersByKey[ApplicationSeedData.AdminSecretaryKey].Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var nextAppointmentDate = GetNextFutureDate(utcNow, ApplicationSeedData.PrimaryAppointment.DayOfWeek);

        var slot = await EnsureAvailabilitySlotAsync(
            usersByKey[ApplicationSeedData.PrimaryAvailabilitySlot.DoctorKey].Id,
            ApplicationSeedData.PrimaryAvailabilitySlot.DayOfWeek,
            ApplicationSeedData.PrimaryAvailabilitySlot.StartTime,
            ApplicationSeedData.PrimaryAvailabilitySlot.EndTime,
            ApplicationSeedData.PrimaryAvailabilitySlot.IsAvailable,
            cancellationToken);

        var appointment = await EnsureAppointmentAsync(
            usersByKey[ApplicationSeedData.PrimaryAppointment.DoctorKey].Id,
            usersByKey[ApplicationSeedData.PrimaryAppointment.PatientKey].Id,
            slot.AvailabilitySlotId,
            nextAppointmentDate,
            ApplicationSeedData.PrimaryAppointment.AppointmentTime,
            ApplicationSeedData.PrimaryAppointment.Notes,
            cancellationToken);

        slot.IsAvailable = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var medicalFileData = ApplicationSeedData.PrimaryMedicalFile;
        var medicalFile = await EnsureMedicalFileAsync(
            appointment.AppointmentId,
            usersByKey[medicalFileData.UploadedByDoctorKey].Id,
            medicalFileData.FileType,
            medicalFileData.FilePath,
            medicalFileData.EncryptedChecksum,
            medicalFileData.FileSizeInBytes,
            medicalFileData.SeverityLevel,
            utcNow.AddDays(medicalFileData.UploadedDaysOffset),
            cancellationToken);

        var authorizationData = ApplicationSeedData.PrimaryPatientAuthorization;
        await EnsurePatientAuthorizedMemberAsync(
            usersByKey[authorizationData.PatientKey].Id,
            usersByKey[authorizationData.AuthorizedMemberKey].Id,
            authorizationData.RelationshipType,
            utcNow.AddDays(authorizationData.AuthorizedDaysOffset),
            cancellationToken);

        var inviteData = ApplicationSeedData.PrimaryInvite;
        await EnsureInviteAsync(
            usersByKey[inviteData.PatientKey].Id,
            usersByKey[inviteData.AuthorizedMemberKey].Id,
            inviteData.RelationshipType,
            inviteData.Status,
            utcNow.AddDays(inviteData.SentDaysOffset),
            inviteData.RespondedDaysOffset.HasValue ? utcNow.AddDays(inviteData.RespondedDaysOffset.Value) : null,
            cancellationToken);

        foreach (var notificationData in ApplicationSeedData.Notifications)
        {
            await EnsureNotificationAsync(
                usersByKey[notificationData.UserKey].Id,
                notificationData.Title,
                notificationData.IsRead,
                cancellationToken);
        }

        var reminderData = ApplicationSeedData.PrimaryReminder;
        await EnsureReminderAsync(
            usersByKey[reminderData.PatientKey].Id,
            usersByKey[reminderData.DoctorKey].Id,
            reminderData.AuthorizedMemberKey is null ? null : usersByKey[reminderData.AuthorizedMemberKey].Id,
            reminderData.ReminderText,
            utcNow.AddDays(reminderData.ReminderDaysOffset),
            cancellationToken);

        var fileDownloadRequestData = ApplicationSeedData.PrimaryFileDownloadRequest;
        await EnsureFileDownloadRequestAsync(
            usersByKey[fileDownloadRequestData.PatientKey].Id,
            medicalFile.MedicalFileId,
            fileDownloadRequestData.ReviewedBySecretaryKey is null
                ? null
                : usersByKey[fileDownloadRequestData.ReviewedBySecretaryKey].Id,
            fileDownloadRequestData.Reason,
            fileDownloadRequestData.PurposeDescription,
            fileDownloadRequestData.Status,
            utcNow.AddHours(fileDownloadRequestData.SubmittedHoursOffset),
            fileDownloadRequestData.ReviewedHoursOffset.HasValue
                ? utcNow.AddHours(fileDownloadRequestData.ReviewedHoursOffset.Value)
                : null,
            fileDownloadRequestData.RejectionReason,
            cancellationToken);

        var labTestRequestData = ApplicationSeedData.PrimaryLabTestRequest;
        await EnsureLabTestRequestAsync(
            labTestRequestData.TestName,
            labTestRequestData.LaboratoryTechnologistKey is null
                ? null
                : usersByKey[labTestRequestData.LaboratoryTechnologistKey].Id,
            medicalFile.MedicalFileId,
            cancellationToken);

        var imagingTestRequestData = ApplicationSeedData.PrimaryImagingTestRequest;
        await EnsureImagingTestRequestAsync(
            imagingTestRequestData.TestName,
            imagingTestRequestData.RadiologyTechnologistKey is null
                ? null
                : usersByKey[imagingTestRequestData.RadiologyTechnologistKey].Id,
            medicalFile.MedicalFileId,
            cancellationToken);

        var reportData = ApplicationSeedData.PrimaryReport;
        var report = await EnsureReportAsync(
            usersByKey[reportData.SecretaryKey].Id,
            utcNow.AddDays(reportData.GeneratedDaysOffset),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var reportInformationData = ApplicationSeedData.PrimaryReportInformation;
        await EnsureReportInformationAsync(
            report.ReportId,
            reportInformationData.FileFormat,
            reportInformationData.FilePath,
            reportInformationData.FileSizeInBytes,
            cancellationToken);

        foreach (var verificationCodeData in ApplicationSeedData.VerificationCodes)
        {
            await EnsureUserVerificationCodeAsync(
                usersByKey[verificationCodeData.UserKey].Id,
                verificationCodeData.Code,
                verificationCodeData.Purpose,
                utcNow.AddDays(verificationCodeData.ExpiresDaysOffset),
                verificationCodeData.IsUsed,
                cancellationToken);
        }

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
        user.RegisteredAt = user.RegisteredAt == default ? DateTime.UtcNow : user.RegisteredAt;
        user.ClinicId = clinicId;
        user.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        user.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        user.DateOfBirth = dateOfBirth;
        user.EmailConfirmed = true;

        IdentityResult persistenceResult;
        if (isNewUser)
        {
            persistenceResult = await _userManager.CreateAsync(user, ApplicationSeedData.DefaultSeedPassword);
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

    private async Task EnsureProfileAsync(
        SeedUser userData,
        Guid userId,
        Guid? clinicId,
        CancellationToken cancellationToken)
    {
        switch (userData.Role)
        {
            case UserSystemRole.Secretary:
                await EnsureSecretaryProfileAsync(userId, cancellationToken);
                return;
            case UserSystemRole.Doctor:
                await EnsureDoctorProfileAsync(
                    userId,
                    RequireSeedValue(userData.ProfessionalLicenseNumber, userData.Key, nameof(userData.ProfessionalLicenseNumber)),
                    cancellationToken);
                return;
            case UserSystemRole.Patient:
                if (!clinicId.HasValue)
                {
                    throw new InvalidOperationException($"Seed patient '{userData.Key}' must belong to a clinic.");
                }

                await EnsurePatientProfileAsync(
                    userId,
                    clinicId.Value,
                    userData.Gender ?? throw new InvalidOperationException($"Seed patient '{userData.Key}' is missing gender."),
                    userData.BloodType,
                    cancellationToken);
                return;
            case UserSystemRole.AuthorizedMember:
                await EnsureAuthorizedMemberProfileAsync(userId, cancellationToken);
                return;
            case UserSystemRole.LaboratoryTechnologist:
                await EnsureLaboratoryTechnologistProfileAsync(
                    userId,
                    RequireSeedValue(userData.ProfessionalLicenseNumber, userData.Key, nameof(userData.ProfessionalLicenseNumber)),
                    cancellationToken);
                return;
            case UserSystemRole.RadiologyTechnologist:
                await EnsureRadiologyTechnologistProfileAsync(
                    userId,
                    RequireSeedValue(userData.ProfessionalLicenseNumber, userData.Key, nameof(userData.ProfessionalLicenseNumber)),
                    cancellationToken);
                return;
            default:
                throw new InvalidOperationException($"Unsupported seed role '{userData.Role}' for '{userData.Key}'.");
        }
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
                    && x.ReportInformations.Any(info => info.FilePath == ApplicationSeedData.PrimaryReportInformation.FilePath),
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

    private static Guid? ResolveClinicId(
        IReadOnlyDictionary<string, Clinic> clinicsByKey,
        string? clinicKey)
    {
        if (string.IsNullOrWhiteSpace(clinicKey))
        {
            return null;
        }

        if (!clinicsByKey.TryGetValue(clinicKey, out var clinic))
        {
            throw new InvalidOperationException($"Seed clinic key '{clinicKey}' was not found.");
        }

        return clinic.ClinicId;
    }

    private static string RequireSeedValue(string? value, string seedKey, string propertyName)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Seed item '{seedKey}' is missing required value '{propertyName}'.");
    }
}
