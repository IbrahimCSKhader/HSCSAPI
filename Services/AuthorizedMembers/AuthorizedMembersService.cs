using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.AuthorizedMember;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Relations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.AuthorizedMembers;

public class AuthorizedMembersService : IAuthorizedMembersService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public AuthorizedMembersService(AppDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    public async Task<ActionResult<AuthorizedMemberDashboardResponse>> GetDashboardAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var profile = await _dbContext.AuthorizedMembers
            .AsNoTracking()
            .Where(authorizedMember => authorizedMember.AuthorizedMemberId == currentUserId.Value)
            .Select(authorizedMember => new
            {
                authorizedMember.AuthorizedMemberId,
                authorizedMember.User.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return new NotFoundObjectResult("Authorized member not found.");
        }

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);

        var linkedPatientIds = BuildLinkedPatientIdsQuery(currentUserId.Value);
        var upcomingAppointmentsQuery = BuildLinkedAppointmentProjectionQuery(currentUserId.Value)
            .Where(appointment => appointment.AppointmentDate > today
                || (appointment.AppointmentDate == today && appointment.AppointmentTime >= currentTime));

        var linkedPatients = await BuildLinkedPatientQuery(currentUserId.Value, today, currentTime)
            .OrderByDescending(patient => patient.AuthorizedAt)
            .ThenBy(patient => patient.Name)
            .Take(5)
            .ToListAsync(cancellationToken);

        var upcomingAppointments = await upcomingAppointmentsQuery
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.AppointmentTime)
            .Take(5)
            .ToListAsync(cancellationToken);

        var dashboard = new AuthorizedMemberDashboardResponse
        {
            AuthorizedMemberId = profile.AuthorizedMemberId,
            Name = profile.Name,
            LinkedPatientsCount = await linkedPatientIds.CountAsync(cancellationToken),
            MedicalRecordsCount = await _dbContext.MedicalFiles
                .AsNoTracking()
                .CountAsync(file => linkedPatientIds.Contains(file.Appointment.PatientId), cancellationToken),
            UnreadNotificationsCount = await _dbContext.Notifications
                .AsNoTracking()
                .CountAsync(
                    notification => notification.UserId == currentUserId.Value && !notification.IsRead,
                    cancellationToken),
            UpcomingAppointmentsCount = await upcomingAppointmentsQuery.CountAsync(cancellationToken),
            LinkedPatients = linkedPatients.Select(MapLinkedPatientSummary).ToList(),
            UpcomingAppointments = upcomingAppointments
                .Select(appointment => MapAppointment(appointment, today, currentTime))
                .ToList()
        };

        return new OkObjectResult(dashboard);
    }

    public async Task<ActionResult<AuthorizedMemberProfileResponse>> GetMyProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var profile = await _dbContext.AuthorizedMembers
            .AsNoTracking()
            .Where(authorizedMember => authorizedMember.AuthorizedMemberId == currentUserId.Value)
            .Select(authorizedMember => new AuthorizedMemberProfileResponse
            {
                AuthorizedMemberId = authorizedMember.AuthorizedMemberId,
                Name = authorizedMember.User.Name,
                Email = authorizedMember.User.Email ?? string.Empty,
                PhoneNumber = authorizedMember.User.PhoneNumber,
                Address = authorizedMember.User.Address,
                DateOfBirth = authorizedMember.User.DateOfBirth,
                PasswordLastUpdatedIso = authorizedMember.User.PasswordLastUpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return profile is null
            ? new NotFoundObjectResult("Authorized member not found.")
            : new OkObjectResult(profile);
    }

    public async Task<ActionResult<AuthorizedMemberProfileResponse>> UpdateMyProfileAsync(
        UpdateAuthorizedMemberProfileRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
            return new UnauthorizedObjectResult("Invalid token.");
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
            return new BadRequestObjectResult("Name and email are required.");

        var profile = await _dbContext.AuthorizedMembers.Include(x => x.User)
            .FirstOrDefaultAsync(x => x.AuthorizedMemberId == currentUserId.Value, cancellationToken);
        if (profile is null)
            return new NotFoundObjectResult("Authorized member not found.");

        var email = request.Email.Trim().ToLowerInvariant();
        var normalizedEmail = email.ToUpperInvariant();
        if (await _dbContext.Users.AsNoTracking().AnyAsync(x => x.Id != currentUserId.Value && x.NormalizedEmail == normalizedEmail, cancellationToken))
            return new ConflictObjectResult("Email already registered.");

        profile.User.Name = request.Name.Trim();
        profile.User.Email = email;
        profile.User.UserName = email;
        profile.User.NormalizedEmail = normalizedEmail;
        profile.User.NormalizedUserName = normalizedEmail;
        profile.User.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        profile.User.Address = NormalizeOptional(request.Address);
        profile.User.DateOfBirth = request.DateOfBirth;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetMyProfileAsync(user, cancellationToken);
    }

    public async Task<ActionResult<List<AuthorizedMemberPatientResponse>>> GetMyPatientsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);

        var patients = await BuildLinkedPatientQuery(currentUserId.Value, today, currentTime)
            .OrderByDescending(patient => patient.AuthorizedAt)
            .ThenBy(patient => patient.Name)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(patients.Select(MapPatientResponse).ToList());
    }

    public async Task<ActionResult<AuthorizedMemberPatientResponse>> GetMyPatientAsync(
        Guid patientId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);

        var patient = await BuildLinkedPatientQuery(currentUserId.Value, today, currentTime)
            .FirstOrDefaultAsync(x => x.PatientId == patientId, cancellationToken);

        return patient is null
            ? new NotFoundObjectResult("Linked patient not found.")
            : new OkObjectResult(MapPatientResponse(patient));
    }

    public async Task<ActionResult<AuthorizedMemberAppointmentsResponse>> GetMyAppointmentsAsync(
        Guid? patientId,
        DateOnly? fromDate,
        DateOnly? toDate,
        bool upcomingOnly,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (!ValidateDateRange(fromDate, toDate, out var dateRangeError))
        {
            return new BadRequestObjectResult(dateRangeError);
        }

        if (patientId.HasValue
            && !await IsLinkedToPatientAsync(currentUserId.Value, patientId.Value, cancellationToken))
        {
            return new NotFoundObjectResult("Linked patient not found.");
        }

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);

        var appointmentsQuery = BuildLinkedAppointmentProjectionQuery(currentUserId.Value);
        if (patientId.HasValue)
        {
            appointmentsQuery = appointmentsQuery.Where(appointment => appointment.PatientId == patientId.Value);
        }

        appointmentsQuery = ApplyAppointmentFilters(
            appointmentsQuery,
            fromDate,
            toDate,
            upcomingOnly,
            today,
            currentTime);

        var appointments = await appointmentsQuery
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.AppointmentTime)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(new AuthorizedMemberAppointmentsResponse
        {
            TotalCount = appointments.Count,
            Items = appointments.Select(appointment => MapAppointment(appointment, today, currentTime)).ToList()
        });
    }

    public async Task<ActionResult<AuthorizedMemberMedicalRecordsResponse>> GetPatientMedicalRecordsAsync(
        Guid patientId,
        string? type,
        string? query,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (!await IsLinkedToPatientAsync(currentUserId.Value, patientId, cancellationToken))
        {
            return new NotFoundObjectResult("Linked patient not found.");
        }

        NormalizePaging(ref page, ref pageSize);

        var recordsQuery = BuildMedicalRecordProjectionQuery(patientId);
        recordsQuery = ApplyMedicalRecordSearch(recordsQuery, query);

        var typeCounts = new AuthorizedMemberMedicalRecordTypeCountsResponse
        {
            All = await recordsQuery.CountAsync(cancellationToken),
            LabResults = await recordsQuery.CountAsync(record => record.HasLabResult, cancellationToken),
            Prescriptions = await recordsQuery.CountAsync(
                record => !record.HasLabResult && !record.HasImagingResult,
                cancellationToken),
            Imaging = await recordsQuery.CountAsync(record => record.HasImagingResult, cancellationToken)
        };

        if (!ApplyMedicalRecordTypeFilter(ref recordsQuery, type, out var typeError))
        {
            return new BadRequestObjectResult(typeError);
        }

        var totalCount = await recordsQuery.CountAsync(cancellationToken);
        var records = await recordsQuery
            .OrderByDescending(record => record.UploadedAt)
            .ThenBy(record => record.MedicalFileId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(new AuthorizedMemberMedicalRecordsResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            TypeCounts = typeCounts,
            Items = records.Select(MapMedicalRecord).ToList()
        });
    }

    public async Task<ActionResult<AuthorizedMemberMedicalRecordDetailResponse>> GetPatientMedicalRecordAsync(
        Guid patientId,
        Guid medicalFileId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (!await IsLinkedToPatientAsync(currentUserId.Value, patientId, cancellationToken))
        {
            return new NotFoundObjectResult("Linked patient not found.");
        }

        var record = await BuildMedicalRecordProjectionQuery(patientId)
            .FirstOrDefaultAsync(x => x.MedicalFileId == medicalFileId, cancellationToken);

        return record is null
            ? new NotFoundObjectResult("Medical record not found.")
            : new OkObjectResult(MapMedicalRecordDetail(record));
    }

    public async Task<IActionResult> DownloadPatientMedicalRecordAsync(
        Guid patientId,
        Guid medicalFileId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (!await IsLinkedToPatientAsync(currentUserId.Value, patientId, cancellationToken))
        {
            return new NotFoundObjectResult("Linked patient not found.");
        }

        var record = await BuildMedicalRecordProjectionQuery(patientId)
            .FirstOrDefaultAsync(x => x.MedicalFileId == medicalFileId, cancellationToken);

        if (record is null)
        {
            return new NotFoundObjectResult("Medical record not found.");
        }

        if (record.SeverityLevel == SeverityLevel.High)
        {
            return new BadRequestObjectResult("High-severity records require an approved download request.");
        }

        var filePath = ResolvePhysicalFilePath(record.FilePath);
        if (!File.Exists(filePath))
        {
            return new NotFoundObjectResult("The medical record exists, but the physical file is not available on this server.");
        }

        return new PhysicalFileResult(filePath, GetContentType(filePath))
        {
            FileDownloadName = Path.GetFileName(filePath),
            EnableRangeProcessing = true
        };
    }

    public async Task<ActionResult<List<AuthorizedMemberInviteResponse>>> GetMyInvitesAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var invites = await LoadInviteResponsesAsync(currentUserId.Value, cancellationToken);
        return new OkObjectResult(invites);
    }

    public async Task<ActionResult<AuthorizedMemberInviteResponse>> AcceptInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return await RespondToInviteAsync(inviteId, InviteStatus.Accepted, user, cancellationToken);
    }

    public async Task<ActionResult<AuthorizedMemberInviteResponse>> RejectInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return await RespondToInviteAsync(inviteId, InviteStatus.Rejected, user, cancellationToken);
    }

    private async Task<ActionResult<AuthorizedMemberInviteResponse>> RespondToInviteAsync(
        Guid inviteId,
        InviteStatus targetStatus,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var invite = await _dbContext.Invites
            .FirstOrDefaultAsync(
                x => x.InviteId == inviteId && x.AuthorizedMemberId == currentUserId.Value,
                cancellationToken);

        if (invite == null)
        {
            return new NotFoundObjectResult("Invite not found.");
        }

        if (!invite.IsActive)
        {
            return new BadRequestObjectResult("Invite is inactive.");
        }

        if (invite.Status != InviteStatus.Pending)
        {
            return new BadRequestObjectResult("Invite has already been responded to.");
        }

        var respondedAt = DateTime.UtcNow;

        if (targetStatus == InviteStatus.Accepted)
        {
            var relation = await _dbContext.PatientAuthorizedMembers
                .FirstOrDefaultAsync(
                    x => x.PatientId == invite.PatientId && x.AuthorizedMemberId == currentUserId.Value,
                    cancellationToken);

            if (relation is null)
            {
                _dbContext.PatientAuthorizedMembers.Add(new PatientAuthorizedMember
                {
                    PatientId = invite.PatientId,
                    AuthorizedMemberId = currentUserId.Value,
                    RelationshipType = invite.RelationshipType,
                    AuthorizedAt = respondedAt
                });
            }
            else
            {
                relation.IsActive = true;
                relation.RelationshipType = invite.RelationshipType;
                relation.AuthorizedAt = respondedAt;
            }
        }

        invite.Status = targetStatus;
        invite.RespondedAt = respondedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetInviteResponseAsync(inviteId, currentUserId.Value, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Invite not found.")
            : new OkObjectResult(response);
    }

    private IQueryable<Guid> BuildLinkedPatientIdsQuery(Guid authorizedMemberId)
    {
        return _dbContext.PatientAuthorizedMembers
            .AsNoTracking()
            .Where(relation => relation.AuthorizedMemberId == authorizedMemberId && relation.IsActive)
            .Select(relation => relation.PatientId);
    }

    private IQueryable<AuthorizedMemberPatientQueryResult> BuildLinkedPatientQuery(
        Guid authorizedMemberId,
        DateOnly today,
        TimeOnly currentTime)
    {
        return _dbContext.PatientAuthorizedMembers
            .AsNoTracking()
            .Where(relation => relation.AuthorizedMemberId == authorizedMemberId && relation.IsActive)
            .Select(relation => new AuthorizedMemberPatientQueryResult
            {
                PatientId = relation.PatientId,
                Name = relation.Patient.User.Name,
                Email = relation.Patient.User.Email ?? string.Empty,
                PhoneNumber = relation.Patient.User.PhoneNumber,
                Address = relation.Patient.User.Address,
                DateOfBirth = relation.Patient.User.DateOfBirth,
                Gender = relation.Patient.Gender,
                BloodType = relation.Patient.BloodType,
                PatientUserId = relation.Patient.UserID,
                ClinicId = relation.Patient.User.ClinicId,
                ClinicName = relation.Patient.User.Clinic != null ? relation.Patient.User.Clinic.Name : null,
                RelationshipType = relation.RelationshipType,
                AuthorizedAt = relation.AuthorizedAt,
                MedicalRecordsCount = relation.Patient.Appointments
                    .SelectMany(appointment => appointment.MedicalFiles)
                    .Count(),
                UpcomingAppointmentsCount = relation.Patient.Appointments.Count(
                    appointment => appointment.IsActive
                        && (appointment.AppointmentDate > today
                            || (appointment.AppointmentDate == today && appointment.AppointmentTime >= currentTime)))
            });
    }

    private IQueryable<AuthorizedMemberAppointmentProjection> BuildLinkedAppointmentProjectionQuery(
        Guid authorizedMemberId)
    {
        return _dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.IsActive
                && appointment.Patient.AuthorizedMembers
                    .Any(relation => relation.AuthorizedMemberId == authorizedMemberId && relation.IsActive))
            .Select(appointment => new AuthorizedMemberAppointmentProjection
            {
                AppointmentId = appointment.AppointmentId,
                PatientId = appointment.PatientId,
                PatientName = appointment.Patient.User.Name,
                PatientUserId = appointment.Patient.UserID,
                DoctorId = appointment.DoctorId,
                DoctorName = appointment.Doctor.User.Name,
                ClinicId = appointment.Doctor.User.ClinicId,
                ClinicName = appointment.Doctor.User.Clinic != null ? appointment.Doctor.User.Clinic.Name : null,
                AppointmentDate = appointment.AppointmentDate,
                AppointmentTime = appointment.AppointmentTime,
                Notes = appointment.Notes
            });
    }

    private IQueryable<AuthorizedMemberMedicalRecordProjection> BuildMedicalRecordProjectionQuery(Guid patientId)
    {
        return _dbContext.MedicalFiles
            .AsNoTracking()
            .Where(file => file.Appointment.PatientId == patientId)
            .Select(file => new AuthorizedMemberMedicalRecordProjection
            {
                MedicalFileId = file.MedicalFileId,
                AppointmentId = file.AppointmentId,
                FileType = file.FileType,
                FilePath = file.FilePath,
                FileSizeInBytes = file.FileSizeInBytes,
                SeverityLevel = file.SeverityLevel,
                UploadedAt = file.UploadedAt,
                PatientId = file.Appointment.PatientId,
                PatientName = file.Appointment.Patient.User.Name,
                PatientUserId = file.Appointment.Patient.UserID,
                RecordedByDoctorId = file.UploadedByDoctorId,
                RecordedByDoctorName = file.UploadedByDoctor.User.Name,
                ClinicId = file.Appointment.Doctor.User.ClinicId,
                ClinicName = file.Appointment.Doctor.User.Clinic != null ? file.Appointment.Doctor.User.Clinic.Name : null,
                AppointmentDate = file.Appointment.AppointmentDate,
                AppointmentTime = file.Appointment.AppointmentTime,
                AppointmentNotes = file.Appointment.Notes,
                LabTestName = file.LabTestRequestsAsResult
                    .OrderBy(test => test.TestName)
                    .Select(test => test.TestName)
                    .FirstOrDefault(),
                LabClinicalNotes = file.LabTestRequestsAsResult
                    .OrderBy(test => test.TestName)
                    .Select(test => test.ClinicalNotes)
                    .FirstOrDefault(),
                ImagingTestName = file.ImagingTestRequestsAsResult
                    .OrderBy(test => test.TestName)
                    .Select(test => test.TestName)
                    .FirstOrDefault(),
                ImagingClinicalNotes = file.ImagingTestRequestsAsResult
                    .OrderBy(test => test.TestName)
                    .Select(test => test.ClinicalNotes)
                    .FirstOrDefault(),
                HasLabResult = file.LabTestRequestsAsResult.Any(),
                HasImagingResult = file.ImagingTestRequestsAsResult.Any(),
                HasPendingDownloadRequest = file.FileDownloadRequests.Any(
                    request => request.PatientId == patientId && request.Status == FileDownloadRequestStatus.Pending)
            });
    }

    private async Task<bool> IsLinkedToPatientAsync(
        Guid authorizedMemberId,
        Guid patientId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.PatientAuthorizedMembers
            .AsNoTracking()
            .AnyAsync(
                relation => relation.AuthorizedMemberId == authorizedMemberId
                    && relation.PatientId == patientId
                    && relation.IsActive,
                cancellationToken);
    }

    private static IQueryable<AuthorizedMemberAppointmentProjection> ApplyAppointmentFilters(
        IQueryable<AuthorizedMemberAppointmentProjection> appointmentsQuery,
        DateOnly? fromDate,
        DateOnly? toDate,
        bool upcomingOnly,
        DateOnly today,
        TimeOnly currentTime)
    {
        if (fromDate.HasValue)
        {
            appointmentsQuery = appointmentsQuery.Where(appointment => appointment.AppointmentDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            appointmentsQuery = appointmentsQuery.Where(appointment => appointment.AppointmentDate <= toDate.Value);
        }

        if (upcomingOnly)
        {
            appointmentsQuery = appointmentsQuery.Where(
                appointment => appointment.AppointmentDate > today
                    || (appointment.AppointmentDate == today && appointment.AppointmentTime >= currentTime));
        }

        return appointmentsQuery;
    }

    private static IQueryable<AuthorizedMemberMedicalRecordProjection> ApplyMedicalRecordSearch(
        IQueryable<AuthorizedMemberMedicalRecordProjection> recordsQuery,
        string? query)
    {
        var normalizedQuery = NormalizeOptional(query);
        if (normalizedQuery is null)
        {
            return recordsQuery;
        }

        return recordsQuery.Where(record =>
            record.PatientName.Contains(normalizedQuery)
            || record.PatientUserId.Contains(normalizedQuery)
            || record.RecordedByDoctorName.Contains(normalizedQuery)
            || (record.ClinicName != null && record.ClinicName.Contains(normalizedQuery))
            || (record.AppointmentNotes != null && record.AppointmentNotes.Contains(normalizedQuery))
            || (record.LabClinicalNotes != null && record.LabClinicalNotes.Contains(normalizedQuery))
            || (record.ImagingClinicalNotes != null && record.ImagingClinicalNotes.Contains(normalizedQuery))
            || (record.LabTestName != null && record.LabTestName.Contains(normalizedQuery))
            || (record.ImagingTestName != null && record.ImagingTestName.Contains(normalizedQuery))
            || record.FilePath.Contains(normalizedQuery));
    }

    private static bool ApplyMedicalRecordTypeFilter(
        ref IQueryable<AuthorizedMemberMedicalRecordProjection> recordsQuery,
        string? type,
        out string error)
    {
        error = string.Empty;
        var normalizedType = NormalizeOptional(type)?.ToLowerInvariant() ?? "all";

        switch (normalizedType)
        {
            case "all":
                return true;
            case "lab":
            case "labs":
            case "lab-result":
            case "lab-results":
                recordsQuery = recordsQuery.Where(record => record.HasLabResult);
                return true;
            case "imaging":
            case "imaging-result":
            case "imaging-results":
                recordsQuery = recordsQuery.Where(record => record.HasImagingResult);
                return true;
            case "prescription":
            case "prescriptions":
            case "visit":
            case "visits":
            case "medical-record":
            case "medical-records":
                recordsQuery = recordsQuery.Where(record => !record.HasLabResult && !record.HasImagingResult);
                return true;
            default:
                error = "Invalid record type. Use all, lab-results, prescriptions, or imaging.";
                return false;
        }
    }

    private static bool ValidateDateRange(DateOnly? fromDate, DateOnly? toDate, out string error)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            error = "fromDate must be before or equal to toDate.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private async Task<List<AuthorizedMemberInviteResponse>> LoadInviteResponsesAsync(
        Guid authorizedMemberId,
        CancellationToken cancellationToken)
    {
        var invites = await _dbContext.Invites
            .AsNoTracking()
            .Where(invite => invite.AuthorizedMemberId == authorizedMemberId)
            .OrderByDescending(invite => invite.SentAt)
            .Select(invite => new AuthorizedMemberInviteQueryResult
            {
                InviteId = invite.InviteId,
                PatientId = invite.PatientId,
                PatientName = invite.Patient.User.Name,
                PatientEmail = invite.Patient.User.Email ?? string.Empty,
                PatientUserId = invite.Patient.UserID,
                ClinicId = invite.Patient.User.ClinicId,
                ClinicName = invite.Patient.User.Clinic != null ? invite.Patient.User.Clinic.Name : null,
                RelationshipType = invite.RelationshipType,
                Status = invite.Status,
                SentAt = invite.SentAt,
                RespondedAt = invite.RespondedAt,
                IsActive = invite.IsActive
            })
            .ToListAsync(cancellationToken);

        return invites.Select(MapInviteResponse).ToList();
    }

    private async Task<AuthorizedMemberInviteResponse?> GetInviteResponseAsync(
        Guid inviteId,
        Guid authorizedMemberId,
        CancellationToken cancellationToken)
    {
        var invite = await _dbContext.Invites
            .AsNoTracking()
            .Where(x => x.InviteId == inviteId && x.AuthorizedMemberId == authorizedMemberId)
            .Select(x => new AuthorizedMemberInviteQueryResult
            {
                InviteId = x.InviteId,
                PatientId = x.PatientId,
                PatientName = x.Patient.User.Name,
                PatientEmail = x.Patient.User.Email ?? string.Empty,
                PatientUserId = x.Patient.UserID,
                ClinicId = x.Patient.User.ClinicId,
                ClinicName = x.Patient.User.Clinic != null ? x.Patient.User.Clinic.Name : null,
                RelationshipType = x.RelationshipType,
                Status = x.Status,
                SentAt = x.SentAt,
                RespondedAt = x.RespondedAt,
                IsActive = x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        return invite is null ? null : MapInviteResponse(invite);
    }

    private static AuthorizedMemberPatientResponse MapPatientResponse(AuthorizedMemberPatientQueryResult patient)
    {
        return new AuthorizedMemberPatientResponse
        {
            PatientId = patient.PatientId,
            Name = patient.Name,
            Email = patient.Email,
            PhoneNumber = patient.PhoneNumber,
            Address = patient.Address,
            DateOfBirth = patient.DateOfBirth,
            Gender = patient.Gender.ToString(),
            BloodType = FormatBloodType(patient.BloodType),
            PatientUserId = patient.PatientUserId,
            ClinicId = patient.ClinicId,
            ClinicName = patient.ClinicName,
            RelationshipType = patient.RelationshipType.ToString(),
            AuthorizedAt = patient.AuthorizedAt,
            MedicalRecordsCount = patient.MedicalRecordsCount,
            UpcomingAppointmentsCount = patient.UpcomingAppointmentsCount,
            CanViewRecords = true,
            CanViewAppointments = true
        };
    }

    private static AuthorizedMemberLinkedPatientSummaryResponse MapLinkedPatientSummary(
        AuthorizedMemberPatientQueryResult patient)
    {
        return new AuthorizedMemberLinkedPatientSummaryResponse
        {
            PatientId = patient.PatientId,
            PatientUserId = patient.PatientUserId,
            Name = patient.Name,
            RelationshipType = patient.RelationshipType.ToString(),
            MedicalRecordsCount = patient.MedicalRecordsCount,
            UpcomingAppointmentsCount = patient.UpcomingAppointmentsCount
        };
    }

    private static AuthorizedMemberAppointmentResponse MapAppointment(
        AuthorizedMemberAppointmentProjection appointment,
        DateOnly today,
        TimeOnly currentTime)
    {
        return new AuthorizedMemberAppointmentResponse
        {
            AppointmentId = appointment.AppointmentId,
            PatientId = appointment.PatientId,
            PatientName = appointment.PatientName,
            PatientUserId = appointment.PatientUserId,
            DoctorId = appointment.DoctorId,
            DoctorName = appointment.DoctorName,
            ClinicId = appointment.ClinicId,
            ClinicName = appointment.ClinicName,
            AppointmentDate = appointment.AppointmentDate,
            DayOfWeek = appointment.AppointmentDate.DayOfWeek.ToString(),
            AppointmentTime = appointment.AppointmentTime,
            Status = IsAppointmentUpcoming(appointment, today, currentTime) ? "Scheduled" : "Past",
            Notes = appointment.Notes
        };
    }

    private static AuthorizedMemberMedicalRecordResponse MapMedicalRecord(
        AuthorizedMemberMedicalRecordProjection record)
    {
        var notes = GetMedicalRecordNotes(record);

        return new AuthorizedMemberMedicalRecordResponse
        {
            MedicalFileId = record.MedicalFileId,
            AppointmentId = record.AppointmentId,
            RecordCode = BuildRecordCode(record),
            RecordType = GetMedicalRecordType(record),
            Title = GetMedicalRecordTitle(record),
            Description = notes,
            FileName = Path.GetFileName(record.FilePath),
            FileType = record.FileType.ToString(),
            FileSizeInBytes = record.FileSizeInBytes,
            SeverityLevel = record.SeverityLevel.ToString(),
            UploadedAt = record.UploadedAt,
            PatientId = record.PatientId,
            PatientUserId = record.PatientUserId,
            PatientName = record.PatientName,
            RecordedByDoctorId = record.RecordedByDoctorId,
            RecordedByDoctorName = record.RecordedByDoctorName,
            ClinicId = record.ClinicId,
            ClinicName = record.ClinicName,
            AppointmentDate = record.AppointmentDate,
            AppointmentTime = record.AppointmentTime,
            LabTestName = record.LabTestName,
            ImagingTestName = record.ImagingTestName,
            CanDownloadDirectly = record.SeverityLevel == SeverityLevel.Low,
            HasPendingDownloadRequest = record.HasPendingDownloadRequest,
            FileUrl = $"/api/AuthorizedMembers/my-patients/{record.PatientId}/medical-records/{record.MedicalFileId}/download"
        };
    }

    private static AuthorizedMemberMedicalRecordDetailResponse MapMedicalRecordDetail(
        AuthorizedMemberMedicalRecordProjection record)
    {
        var notes = GetMedicalRecordNotes(record);
        var summary = notes
            ?? $"{GetMedicalRecordTitle(record)} recorded by {record.RecordedByDoctorName}.";

        return new AuthorizedMemberMedicalRecordDetailResponse
        {
            MedicalFileId = record.MedicalFileId,
            AppointmentId = record.AppointmentId,
            RecordCode = BuildRecordCode(record),
            RecordType = GetMedicalRecordType(record),
            Title = GetMedicalRecordTitle(record),
            Description = notes,
            FileName = Path.GetFileName(record.FilePath),
            FileType = record.FileType.ToString(),
            FileSizeInBytes = record.FileSizeInBytes,
            SeverityLevel = record.SeverityLevel.ToString(),
            UploadedAt = record.UploadedAt,
            PatientId = record.PatientId,
            PatientUserId = record.PatientUserId,
            PatientName = record.PatientName,
            RecordedByDoctorId = record.RecordedByDoctorId,
            RecordedByDoctorName = record.RecordedByDoctorName,
            ClinicId = record.ClinicId,
            ClinicName = record.ClinicName,
            AppointmentDate = record.AppointmentDate,
            AppointmentTime = record.AppointmentTime,
            LabTestName = record.LabTestName,
            ImagingTestName = record.ImagingTestName,
            CanDownloadDirectly = record.SeverityLevel == SeverityLevel.Low,
            HasPendingDownloadRequest = record.HasPendingDownloadRequest,
            FileUrl = $"/api/AuthorizedMembers/my-patients/{record.PatientId}/medical-records/{record.MedicalFileId}/download",
            AppointmentNotes = record.AppointmentNotes,
            Summary = summary,
            ClinicalDetails = notes
        };
    }

    private static AuthorizedMemberInviteResponse MapInviteResponse(AuthorizedMemberInviteQueryResult invite)
    {
        return new AuthorizedMemberInviteResponse
        {
            InviteId = invite.InviteId,
            PatientId = invite.PatientId,
            PatientName = invite.PatientName,
            PatientEmail = invite.PatientEmail,
            PatientUserId = invite.PatientUserId,
            ClinicId = invite.ClinicId,
            ClinicName = invite.ClinicName,
            RelationshipType = invite.RelationshipType.ToString(),
            Status = invite.Status.ToString(),
            SentAt = invite.SentAt,
            RespondedAt = invite.RespondedAt,
            IsActive = invite.IsActive
        };
    }

    private string ResolvePhysicalFilePath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
        {
            return filePath;
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, filePath));
    }

    private static string GetContentType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static string? FormatBloodType(BloodType? bloodType)
    {
        return bloodType switch
        {
            BloodType.APositive => "A+",
            BloodType.ANegative => "A-",
            BloodType.BPositive => "B+",
            BloodType.BNegative => "B-",
            BloodType.ABPositive => "AB+",
            BloodType.ABNegative => "AB-",
            BloodType.OPositive => "O+",
            BloodType.ONegative => "O-",
            _ => null
        };
    }

    private static bool IsAppointmentUpcoming(
        AuthorizedMemberAppointmentProjection appointment,
        DateOnly today,
        TimeOnly currentTime)
    {
        return appointment.AppointmentDate > today
            || (appointment.AppointmentDate == today && appointment.AppointmentTime >= currentTime);
    }

    private static string? GetMedicalRecordNotes(AuthorizedMemberMedicalRecordProjection record)
    {
        return record.LabClinicalNotes
            ?? record.ImagingClinicalNotes
            ?? record.AppointmentNotes;
    }

    private static string GetMedicalRecordType(AuthorizedMemberMedicalRecordProjection record)
    {
        if (record.HasLabResult)
        {
            return "LabResult";
        }

        if (record.HasImagingResult)
        {
            return "Imaging";
        }

        return "Prescription";
    }

    private static string GetMedicalRecordTitle(AuthorizedMemberMedicalRecordProjection record)
    {
        return record.LabTestName
            ?? record.ImagingTestName
            ?? Path.GetFileNameWithoutExtension(record.FilePath)
            ?? $"{record.FileType} medical record";
    }

    private static string BuildRecordCode(AuthorizedMemberMedicalRecordProjection record)
    {
        return $"MR-{record.UploadedAt:yyyy}-{record.MedicalFileId.ToString("N")[..8].ToUpperInvariant()}";
    }

    private static void NormalizePaging(ref int page, ref int pageSize)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, MaxPageSize);
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null)
        {
            return null;
        }

        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class AuthorizedMemberAppointmentProjection
    {
        public Guid AppointmentId { get; set; }
        public Guid PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientUserId { get; set; } = string.Empty;
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public Guid? ClinicId { get; set; }
        public string? ClinicName { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public TimeOnly AppointmentTime { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class AuthorizedMemberMedicalRecordProjection
    {
        public Guid MedicalFileId { get; set; }
        public Guid AppointmentId { get; set; }
        public MedicalFileType FileType { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeInBytes { get; set; }
        public SeverityLevel SeverityLevel { get; set; }
        public DateTime UploadedAt { get; set; }
        public Guid PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientUserId { get; set; } = string.Empty;
        public Guid RecordedByDoctorId { get; set; }
        public string RecordedByDoctorName { get; set; } = string.Empty;
        public Guid? ClinicId { get; set; }
        public string? ClinicName { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public TimeOnly AppointmentTime { get; set; }
        public string? AppointmentNotes { get; set; }
        public string? LabTestName { get; set; }
        public string? LabClinicalNotes { get; set; }
        public string? ImagingTestName { get; set; }
        public string? ImagingClinicalNotes { get; set; }
        public bool HasLabResult { get; set; }
        public bool HasImagingResult { get; set; }
        public bool HasPendingDownloadRequest { get; set; }
    }
}
