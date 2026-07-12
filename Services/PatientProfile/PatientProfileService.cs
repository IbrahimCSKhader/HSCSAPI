using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.PatientProfile;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Relations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.PatientProfile;

public class PatientProfileService : IPatientProfileService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly IWebHostEnvironment _environment;

    public PatientProfileService(
        AppDbContext dbContext,
        UserManager<User> userManager,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _environment = environment;
    }

    public async Task<ActionResult<PatientProfileDashboardResponse>> GetDashboardAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var patient = await _dbContext.Patients
            .AsNoTracking()
            .Where(x => x.PatientId == patientId.Value)
            .Select(x => new
            {
                x.PatientId,
                x.User.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (patient is null)
        {
            return new NotFoundObjectResult("Patient profile not found.");
        }

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);

        var upcomingAppointmentsQuery = _dbContext.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.PatientId == patientId.Value
                && appointment.IsActive
                && (appointment.AppointmentDate > today
                    || (appointment.AppointmentDate == today && appointment.AppointmentTime >= currentTime)));

        var upcomingAppointmentsCount = await upcomingAppointmentsQuery.CountAsync(cancellationToken);

        var upcomingAppointments = await upcomingAppointmentsQuery
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.AppointmentTime)
            .Take(5)
            .Select(appointment => new PatientProfileAppointmentSummaryResponse
            {
                AppointmentId = appointment.AppointmentId,
                DoctorId = appointment.DoctorId,
                DoctorName = appointment.Doctor.User.Name,
                DoctorSpecialty = appointment.Doctor.Specialty.ToString(),
                ClinicId = appointment.Doctor.User.ClinicId,
                ClinicName = appointment.Doctor.User.Clinic != null ? appointment.Doctor.User.Clinic.Name : null,
                AppointmentDate = appointment.AppointmentDate,
                AppointmentTime = appointment.AppointmentTime,
                AppointmentEndTime = appointment.AvailabilitySlot.EndTime,
                TreatmentId = appointment.TreatmentId,
                TreatmentName = appointment.TreatmentName,
                Notes = appointment.Notes
            })
            .ToListAsync(cancellationToken);

        var unreadNotificationsCount = await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.UserId == patientId.Value && !notification.IsRead, cancellationToken);

        var unreadMessagesCount = await _dbContext.ChatMessages
            .AsNoTracking()
            .CountAsync(
                message => message.SenderId != patientId.Value
                    && message.ReadAt == null
                    && (message.Chat.UserOneId == patientId.Value
                        || message.Chat.UserTwoId == patientId.Value),
                cancellationToken);

        var medicalRecordsQuery = _dbContext.MedicalFiles
            .AsNoTracking()
            .Where(file => file.Appointment.PatientId == patientId.Value);

        var totalRecordsCount = await medicalRecordsQuery.CountAsync(cancellationToken);
        var labResultsCount = await medicalRecordsQuery
            .CountAsync(file => file.LabTestRequestsAsResult.Any(), cancellationToken);
        var imagingResultsCount = await medicalRecordsQuery
            .CountAsync(file => file.ImagingTestRequestsAsResult.Any(), cancellationToken);

        var visitsByClinic = await _dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.PatientId == patientId.Value && appointment.IsActive)
            .GroupBy(appointment => new
            {
                appointment.Doctor.User.ClinicId,
                ClinicName = appointment.Doctor.User.Clinic != null ? appointment.Doctor.User.Clinic.Name : "Unassigned clinic"
            })
            .Select(group => new PatientVisitByClinicResponse
            {
                ClinicId = group.Key.ClinicId,
                ClinicName = group.Key.ClinicName,
                VisitsCount = group.Count()
            })
            .OrderByDescending(clinic => clinic.VisitsCount)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(new PatientProfileDashboardResponse
        {
            PatientId = patient.PatientId,
            PatientName = patient.Name,
            UpcomingAppointmentsCount = upcomingAppointmentsCount,
            UnreadMessagesCount = unreadMessagesCount,
            UnreadNotificationsCount = unreadNotificationsCount,
            UpcomingAppointments = upcomingAppointments,
            HealthOverview = new PatientHealthOverviewResponse
            {
                TotalRecordsCount = totalRecordsCount,
                LabResultsCount = labResultsCount,
                PrescriptionsCount = 0,
                ImagingResultsCount = imagingResultsCount
            },
            VisitsByClinic = new PatientVisitsByClinicSummaryResponse
            {
                TotalVisits = visitsByClinic.Sum(clinic => clinic.VisitsCount),
                ClinicsCount = visitsByClinic.Count,
                Clinics = visitsByClinic
            }
        });
    }

    public async Task<ActionResult<PagedResponse<PatientMedicalRecordResponse>>> GetMedicalRecordsAsync(
        string? type,
        string? query,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        NormalizePaging(ref page, ref pageSize);

        var recordsQuery = BuildMedicalRecordProjectionQuery(patientId.Value);
        if (!ApplyRecordTypeFilter(ref recordsQuery, type, out var typeError))
        {
            return new BadRequestObjectResult(typeError);
        }

        var normalizedQuery = NormalizeOptional(query);
        if (normalizedQuery is not null)
        {
            recordsQuery = recordsQuery.Where(record =>
                record.DoctorName.Contains(normalizedQuery)
                || (record.ClinicName != null && record.ClinicName.Contains(normalizedQuery))
                || (record.LabTestName != null && record.LabTestName.Contains(normalizedQuery))
                || (record.ImagingTestName != null && record.ImagingTestName.Contains(normalizedQuery))
                || record.FilePath.Contains(normalizedQuery));
        }

        var totalCount = await recordsQuery.CountAsync(cancellationToken);
        var records = await recordsQuery
            .OrderByDescending(record => record.UploadedAt)
            .ThenBy(record => record.MedicalFileId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(BuildPagedResponse(
            records.Select(MapMedicalRecord).ToList(),
            totalCount,
            page,
            pageSize));
    }

    public async Task<ActionResult<PatientMedicalRecordDetailResponse>> GetMedicalRecordAsync(
        Guid medicalFileId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var record = await BuildMedicalRecordProjectionQuery(patientId.Value)
            .FirstOrDefaultAsync(x => x.MedicalFileId == medicalFileId, cancellationToken);

        if (record is null)
        {
            return new NotFoundObjectResult("Medical record not found.");
        }

        return new OkObjectResult(MapMedicalRecordDetail(record));
    }

    public async Task<IActionResult> DownloadMedicalRecordAsync(
        Guid medicalFileId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var record = await BuildMedicalRecordProjectionQuery(patientId.Value)
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
            FileDownloadName = Path.GetFileName(filePath)
        };
    }

    public async Task<ActionResult<PatientFileDownloadRequestResponse>> CreateDownloadRequestAsync(
        Guid medicalFileId,
        CreateFileDownloadRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return new BadRequestObjectResult("Reason is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PurposeDescription))
        {
            return new BadRequestObjectResult("PurposeDescription is required.");
        }

        var record = await BuildMedicalRecordProjectionQuery(patientId.Value)
            .FirstOrDefaultAsync(x => x.MedicalFileId == medicalFileId, cancellationToken);

        if (record is null)
        {
            return new NotFoundObjectResult("Medical record not found.");
        }

        if (record.SeverityLevel == SeverityLevel.Low)
        {
            return new BadRequestObjectResult("Low-severity records can be downloaded directly without a request.");
        }

        var existingPendingRequest = await BuildDownloadRequestProjectionQuery(patientId.Value)
            .Where(x => x.MedicalFileId == medicalFileId && x.Status == FileDownloadRequestStatus.Pending)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingPendingRequest is not null)
        {
            return new OkObjectResult(MapDownloadRequest(existingPendingRequest));
        }

        var downloadRequest = new FileDownloadRequest
        {
            PatientId = patientId.Value,
            MedicalFileId = medicalFileId,
            Reason = request.Reason.Trim(),
            PurposeDescription = request.PurposeDescription.Trim(),
            Status = FileDownloadRequestStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };

        _dbContext.FileDownloadRequests.Add(downloadRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await BuildDownloadRequestProjectionQuery(patientId.Value)
            .FirstOrDefaultAsync(
                x => x.FileDownloadRequestId == downloadRequest.FileDownloadRequestId,
                cancellationToken);

        return response is null
            ? new NotFoundObjectResult("Download request was created but could not be loaded.")
            : new CreatedAtActionResult(
                actionName: "GetDownloadRequests",
                controllerName: "PatientProfile",
                routeValues: null,
                value: MapDownloadRequest(response));
    }

    public async Task<ActionResult<PagedResponse<PatientFileDownloadRequestResponse>>> GetDownloadRequestsAsync(
        string? status,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (!TryParseDownloadRequestStatus(status, out var parsedStatus, out var statusError))
        {
            return new BadRequestObjectResult(statusError);
        }

        NormalizePaging(ref page, ref pageSize);

        var query = BuildDownloadRequestProjectionQuery(patientId.Value);
        if (parsedStatus.HasValue)
        {
            query = query.Where(x => x.Status == parsedStatus.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var requests = await query
            .OrderByDescending(x => x.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(BuildPagedResponse(
            requests.Select(MapDownloadRequest).ToList(),
            totalCount,
            page,
            pageSize));
    }

    public async Task<ActionResult<List<PatientAuthorizedMemberResponse>>> GetAuthorizedMembersAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var members = await _dbContext.PatientAuthorizedMembers
            .AsNoTracking()
            .Where(relation => relation.PatientId == patientId.Value)
            .OrderByDescending(relation => relation.AuthorizedAt)
            .Select(relation => new PatientAuthorizedMemberResponse
            {
                AuthorizedMemberId = relation.AuthorizedMemberId,
                Name = relation.AuthorizedMember.User.Name,
                Email = relation.AuthorizedMember.User.Email ?? string.Empty,
                PhoneNumber = relation.AuthorizedMember.User.PhoneNumber,
                RelationshipType = relation.RelationshipType.ToString(),
                AuthorizedAt = relation.AuthorizedAt,
                IsActive = relation.IsActive,
                CanViewRecords = relation.CanViewRecords,
                CanViewAppointments = relation.CanViewAppointments
            })
            .ToListAsync(cancellationToken);

        return new OkObjectResult(members);
    }

    public async Task<ActionResult<List<PatientAuthorizedMemberInviteResponse>>> GetAuthorizedMemberInvitesAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var invites = await BuildInviteResponseQuery(patientId.Value)
            .OrderByDescending(invite => invite.SentAt)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(invites);
    }

    public async Task<ActionResult<PatientAuthorizedMemberInviteResponse>> CreateAuthorizedMemberInviteAsync(
        CreateAuthorizedMemberInviteRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return new BadRequestObjectResult("Email is required.");
        }

        if (!TryParseRelationshipType(request.RelationshipType, out var relationshipType, out var relationshipError))
        {
            return new BadRequestObjectResult(relationshipError);
        }

        var normalizedEmail = _userManager.NormalizeEmail(request.Email.Trim());
        var authorizedMember = await _dbContext.AuthorizedMembers
            .AsNoTracking()
            .Where(member => member.User.NormalizedEmail == normalizedEmail)
            .Select(member => new
            {
                member.AuthorizedMemberId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (authorizedMember is null)
        {
            return new BadRequestObjectResult("Email must belong to a registered Care Giver.");
        }

        var relationExists = await _dbContext.PatientAuthorizedMembers
            .AsNoTracking()
            .AnyAsync(
                relation => relation.PatientId == patientId.Value
                    && relation.AuthorizedMemberId == authorizedMember.AuthorizedMemberId,
                cancellationToken);

        if (relationExists)
        {
            return new BadRequestObjectResult("This authorized member already has access to your profile.");
        }

        var existingPendingInvite = await BuildInviteResponseQuery(patientId.Value)
            .FirstOrDefaultAsync(
                invite => invite.AuthorizedMemberId == authorizedMember.AuthorizedMemberId
                    && invite.Status == InviteStatus.Pending.ToString(),
                cancellationToken);

        if (existingPendingInvite is not null)
        {
            return new OkObjectResult(existingPendingInvite);
        }

        var invite = new Invite
        {
            PatientId = patientId.Value,
            AuthorizedMemberId = authorizedMember.AuthorizedMemberId,
            RelationshipType = relationshipType,
            Status = InviteStatus.Pending,
            SentAt = DateTime.UtcNow,
            CanViewRecords = request.CanViewRecords,
            CanViewAppointments = request.CanViewAppointments
        };

        _dbContext.Invites.Add(invite);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await BuildInviteResponseQuery(patientId.Value)
            .FirstOrDefaultAsync(x => x.InviteId == invite.InviteId, cancellationToken);

        return response is null
            ? new NotFoundObjectResult("Invite was created but could not be loaded.")
            : new CreatedAtActionResult(
                actionName: "GetAuthorizedMemberInvites",
                controllerName: "PatientProfile",
                routeValues: null,
                value: response);
    }

    public Task<IActionResult> DeactivateAuthorizedMemberAsync(
        Guid authorizedMemberId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return SetAuthorizedMemberActiveStateAsync(authorizedMemberId, false, user, cancellationToken);
    }

    public Task<IActionResult> ActivateAuthorizedMemberAsync(
        Guid authorizedMemberId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return SetAuthorizedMemberActiveStateAsync(authorizedMemberId, true, user, cancellationToken);
    }

    private async Task<IActionResult> SetAuthorizedMemberActiveStateAsync(
        Guid authorizedMemberId,
        bool isActive,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var relation = await _dbContext.PatientAuthorizedMembers
            .FirstOrDefaultAsync(
                x => x.PatientId == patientId.Value && x.AuthorizedMemberId == authorizedMemberId,
                cancellationToken);

        if (relation is null)
        {
            return new NotFoundObjectResult("Authorized member relation not found.");
        }

        relation.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new NoContentResult();
    }

    public Task<IActionResult> DeactivateAuthorizedMemberInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return SetInviteActiveStateAsync(inviteId, false, user, cancellationToken);
    }

    public Task<IActionResult> ActivateAuthorizedMemberInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return SetInviteActiveStateAsync(inviteId, true, user, cancellationToken);
    }

    private async Task<IActionResult> SetInviteActiveStateAsync(
        Guid inviteId,
        bool isActive,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var invite = await _dbContext.Invites
            .FirstOrDefaultAsync(
                x => x.InviteId == inviteId && x.PatientId == patientId.Value,
                cancellationToken);

        if (invite is null)
        {
            return new NotFoundObjectResult("Invite not found.");
        }

        if (invite.Status != InviteStatus.Pending)
        {
            return new BadRequestObjectResult("Only pending invites can be activated or deactivated.");
        }

        invite.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new NoContentResult();
    }

    private IQueryable<MedicalRecordProjection> BuildMedicalRecordProjectionQuery(Guid patientId)
    {
        return _dbContext.MedicalFiles
            .AsNoTracking()
            .Where(file => file.Appointment.PatientId == patientId)
            .Select(file => new MedicalRecordProjection
            {
                MedicalFileId = file.MedicalFileId,
                AppointmentId = file.AppointmentId,
                FileType = file.FileType,
                FilePath = file.FilePath,
                FileSizeInBytes = file.FileSizeInBytes,
                SeverityLevel = file.SeverityLevel,
                UploadedAt = file.UploadedAt,
                DoctorId = file.UploadedByDoctorId,
                DoctorName = file.UploadedByDoctor.User.Name,
                DoctorSpecialty = file.UploadedByDoctor.Specialty.ToString(),
                ClinicId = file.Appointment.Doctor.User.ClinicId,
                ClinicName = file.Appointment.Doctor.User.Clinic != null ? file.Appointment.Doctor.User.Clinic.Name : null,
                AppointmentDate = file.Appointment.AppointmentDate,
                AppointmentTime = file.Appointment.AppointmentTime,
                AppointmentNotes = file.Appointment.Notes,
                LabTestName = file.LabTestRequestsAsResult
                    .OrderBy(test => test.TestName)
                    .Select(test => test.TestName)
                    .FirstOrDefault(),
                ImagingTestName = file.ImagingTestRequestsAsResult
                    .OrderBy(test => test.TestName)
                    .Select(test => test.TestName)
                    .FirstOrDefault(),
                DiagnosisCode = file.DiagnosisCode,
                DiagnosisName = file.DiagnosisName,
                ActivityCode = file.ActivityCode,
                ActivityName = file.ActivityName,
                HasLabResult = file.LabTestRequestsAsResult.Any(),
                HasImagingResult = file.ImagingTestRequestsAsResult.Any(),
                HasPendingDownloadRequest = file.FileDownloadRequests.Any(
                    request => request.PatientId == patientId && request.Status == FileDownloadRequestStatus.Pending)
            });
    }

    private IQueryable<FileDownloadRequestProjection> BuildDownloadRequestProjectionQuery(Guid patientId)
    {
        return _dbContext.FileDownloadRequests
            .AsNoTracking()
            .Where(request => request.PatientId == patientId)
            .Select(request => new FileDownloadRequestProjection
            {
                FileDownloadRequestId = request.FileDownloadRequestId,
                MedicalFileId = request.MedicalFileId,
                Reason = request.Reason,
                PurposeDescription = request.PurposeDescription,
                Status = request.Status,
                SubmittedAt = request.SubmittedAt,
                ReviewedAt = request.ReviewedAt,
                RejectionReason = request.RejectionReason,
                ReviewedBySecretaryId = request.ReviewedBySecretaryId,
                ReviewedBySecretaryName = request.ReviewedBySecretary != null ? request.ReviewedBySecretary.User.Name : null,
                LabTestName = request.MedicalFile.LabTestRequestsAsResult
                    .OrderBy(test => test.TestName)
                    .Select(test => test.TestName)
                    .FirstOrDefault(),
                ImagingTestName = request.MedicalFile.ImagingTestRequestsAsResult
                    .OrderBy(test => test.TestName)
                    .Select(test => test.TestName)
                    .FirstOrDefault(),
                FilePath = request.MedicalFile.FilePath,
                FileType = request.MedicalFile.FileType
            });
    }

    private IQueryable<PatientAuthorizedMemberInviteResponse> BuildInviteResponseQuery(Guid patientId)
    {
        return _dbContext.Invites
            .AsNoTracking()
            .Where(invite => invite.PatientId == patientId)
            .Select(invite => new PatientAuthorizedMemberInviteResponse
            {
                InviteId = invite.InviteId,
                AuthorizedMemberId = invite.AuthorizedMemberId,
                AuthorizedMemberName = invite.AuthorizedMember.User.Name,
                AuthorizedMemberEmail = invite.AuthorizedMember.User.Email ?? string.Empty,
                RelationshipType = invite.RelationshipType.ToString(),
                Status = invite.Status.ToString(),
                SentAt = invite.SentAt,
                RespondedAt = invite.RespondedAt,
                IsActive = invite.IsActive,
                CanViewRecords = invite.CanViewRecords,
                CanViewAppointments = invite.CanViewAppointments
            });
    }

    private static bool ApplyRecordTypeFilter(
        ref IQueryable<MedicalRecordProjection> recordsQuery,
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
            case "lab-results":
                recordsQuery = recordsQuery.Where(record => record.HasLabResult);
                return true;
            case "imaging":
            case "imaging-results":
                recordsQuery = recordsQuery.Where(record => record.HasImagingResult);
                return true;
            case "prescription":
            case "prescriptions":
            case "rx":
                recordsQuery = recordsQuery.Where(_ => false);
                return true;
            default:
                error = "Invalid record type. Use all, lab, prescription, or imaging.";
                return false;
        }
    }

    private static PatientMedicalRecordResponse MapMedicalRecord(MedicalRecordProjection record)
    {
        var recordType = GetMedicalRecordType(record);

        return new PatientMedicalRecordResponse
        {
            MedicalFileId = record.MedicalFileId,
            RecordCode = BuildRecordCode(record),
            RecordType = recordType,
            Title = GetMedicalRecordTitle(record),
            Description = record.AppointmentNotes,
            SeverityLevel = record.SeverityLevel.ToString(),
            FileType = record.FileType.ToString(),
            FileSizeInBytes = record.FileSizeInBytes,
            UploadedAt = record.UploadedAt,
            DoctorId = record.DoctorId,
            DoctorName = record.DoctorName,
            DoctorSpecialty = record.DoctorSpecialty,
            ClinicId = record.ClinicId,
            ClinicName = record.ClinicName,
            AppointmentDate = record.AppointmentDate,
            DiagnosisCode = record.DiagnosisCode,
            DiagnosisName = record.DiagnosisName,
            ActivityCode = record.ActivityCode,
            ActivityName = record.ActivityName,
            CanDownloadDirectly = record.SeverityLevel == SeverityLevel.Low,
            HasPendingDownloadRequest = record.HasPendingDownloadRequest
        };
    }

    private static PatientMedicalRecordDetailResponse MapMedicalRecordDetail(MedicalRecordProjection record)
    {
        var summary = record.AppointmentNotes
            ?? $"{GetMedicalRecordTitle(record)} uploaded by {record.DoctorName}.";

        return new PatientMedicalRecordDetailResponse
        {
            MedicalFileId = record.MedicalFileId,
            AppointmentId = record.AppointmentId,
            RecordCode = BuildRecordCode(record),
            RecordType = GetMedicalRecordType(record),
            Title = GetMedicalRecordTitle(record),
            Description = record.AppointmentNotes,
            SeverityLevel = record.SeverityLevel.ToString(),
            FileType = record.FileType.ToString(),
            FileSizeInBytes = record.FileSizeInBytes,
            UploadedAt = record.UploadedAt,
            DoctorId = record.DoctorId,
            DoctorName = record.DoctorName,
            DoctorSpecialty = record.DoctorSpecialty,
            ClinicId = record.ClinicId,
            ClinicName = record.ClinicName,
            AppointmentDate = record.AppointmentDate,
            AppointmentTime = record.AppointmentTime,
            AppointmentNotes = record.AppointmentNotes,
            DiagnosisCode = record.DiagnosisCode,
            DiagnosisName = record.DiagnosisName,
            ActivityCode = record.ActivityCode,
            ActivityName = record.ActivityName,
            CanDownloadDirectly = record.SeverityLevel == SeverityLevel.Low,
            HasPendingDownloadRequest = record.HasPendingDownloadRequest,
            Summary = summary,
            ClinicalDetails = record.AppointmentNotes
        };
    }

    private static PatientFileDownloadRequestResponse MapDownloadRequest(FileDownloadRequestProjection request)
    {
        return new PatientFileDownloadRequestResponse
        {
            FileDownloadRequestId = request.FileDownloadRequestId,
            MedicalFileId = request.MedicalFileId,
            RecordTitle = GetDownloadRequestRecordTitle(request),
            Reason = request.Reason,
            PurposeDescription = request.PurposeDescription,
            Status = request.Status.ToString(),
            SubmittedAt = request.SubmittedAt,
            ReviewedAt = request.ReviewedAt,
            RejectionReason = request.RejectionReason,
            ReviewedBySecretaryId = request.ReviewedBySecretaryId,
            ReviewedBySecretaryName = request.ReviewedBySecretaryName
        };
    }

    private static string GetMedicalRecordType(MedicalRecordProjection record)
    {
        if (record.HasLabResult)
        {
            return "Lab";
        }

        if (record.HasImagingResult)
        {
            return "Imaging";
        }

        return "MedicalRecord";
    }

    private static string GetMedicalRecordTitle(MedicalRecordProjection record)
    {
        return record.LabTestName
            ?? record.ImagingTestName
            ?? Path.GetFileNameWithoutExtension(record.FilePath)
            ?? $"{record.FileType} medical record";
    }

    private static string BuildRecordCode(MedicalRecordProjection record)
    {
        return $"MR-{record.UploadedAt:yyyy}-{record.MedicalFileId.ToString("N")[..8].ToUpperInvariant()}";
    }

    private static string GetDownloadRequestRecordTitle(FileDownloadRequestProjection request)
    {
        return request.LabTestName
            ?? request.ImagingTestName
            ?? Path.GetFileNameWithoutExtension(request.FilePath)
            ?? $"{request.FileType} medical record";
    }

    private static bool TryParseDownloadRequestStatus(
        string? status,
        out FileDownloadRequestStatus? parsedStatus,
        out string error)
    {
        error = string.Empty;
        parsedStatus = null;

        var normalizedStatus = NormalizeOptional(status);
        if (normalizedStatus is null || normalizedStatus.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Enum.TryParse<FileDownloadRequestStatus>(normalizedStatus, true, out var value))
        {
            parsedStatus = value;
            return true;
        }

        error = "Invalid download request status. Use all, pending, approved, or rejected.";
        return false;
    }

    private static bool TryParseRelationshipType(
        string value,
        out RelationshipType relationshipType,
        out string error)
    {
        if (Enum.TryParse<RelationshipType>(value, true, out relationshipType))
        {
            error = string.Empty;
            return true;
        }

        error = "Invalid relationship type.";
        return false;
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

    private static PagedResponse<T> BuildPagedResponse<T>(
        List<T> items,
        int totalCount,
        int page,
        int pageSize)
    {
        return new PagedResponse<T>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
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

    private sealed class MedicalRecordProjection
    {
        public Guid MedicalFileId { get; set; }
        public Guid AppointmentId { get; set; }
        public MedicalFileType FileType { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeInBytes { get; set; }
        public SeverityLevel SeverityLevel { get; set; }
        public DateTime UploadedAt { get; set; }
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string DoctorSpecialty { get; set; } = string.Empty;
        public Guid? ClinicId { get; set; }
        public string? ClinicName { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public TimeOnly AppointmentTime { get; set; }
        public string? AppointmentNotes { get; set; }
        public string? LabTestName { get; set; }
        public string? ImagingTestName { get; set; }
        public string? DiagnosisCode { get; set; }
        public string? DiagnosisName { get; set; }
        public string? ActivityCode { get; set; }
        public string? ActivityName { get; set; }
        public bool HasLabResult { get; set; }
        public bool HasImagingResult { get; set; }
        public bool HasPendingDownloadRequest { get; set; }
    }

    private sealed class FileDownloadRequestProjection
    {
        public Guid FileDownloadRequestId { get; set; }
        public Guid MedicalFileId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string PurposeDescription { get; set; } = string.Empty;
        public FileDownloadRequestStatus Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }
        public Guid? ReviewedBySecretaryId { get; set; }
        public string? ReviewedBySecretaryName { get; set; }
        public string? LabTestName { get; set; }
        public string? ImagingTestName { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public MedicalFileType FileType { get; set; }
    }
}
