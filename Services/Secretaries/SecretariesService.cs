using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Secretary;
using HSCSAPI.DTOs.Appointment;
using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Secretaries;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace HSCSAPI.Services.Secretaries;

public class SecretariesService : ISecretariesService
{
    public const string SuperAdminOrSecretaryRoles = nameof(UserSystemRole.SuperAdmin) + "," + nameof(UserSystemRole.Secretary);
    private const int RecentRegistrationsLimit = 10;

    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;

    public SecretariesService(
        AppDbContext dbContext,
        UserManager<User> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<ActionResult<List<AvailabilitySlotResponse>>> GetDoctorAvailabilitySlotsAsync(
        Guid doctorId, DateOnly? fromDate, DateOnly? toDate, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var scope = await GetSecretaryScopeAsync(user, cancellationToken);
        if (scope is null)
        {
            return new ObjectResult("This secretary is not assigned to any clinic.") { StatusCode = 403 };
        }

        var start = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var end = toDate ?? start.AddDays(30);
        if (start > end)
        {
            return new BadRequestObjectResult("fromDate must be before or equal to toDate.");
        }

        if (!await DoctorBelongsToClinicAsync(doctorId, scope.Value.ClinicId, cancellationToken))
        {
            return new NotFoundObjectResult("Doctor not found in your clinic.");
        }

        var slots = await _dbContext.AvailabilitySlots.AsNoTracking()
            .Where(x => x.DoctorId == doctorId && x.SlotDate >= start && x.SlotDate <= end)
            .OrderBy(x => x.SlotDate).ThenBy(x => x.StartTime)
            .Select(x => new AvailabilitySlotResponse
            {
                AvailabilitySlotId = x.AvailabilitySlotId,
                DoctorId = x.DoctorId,
                SlotDate = x.SlotDate,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Notes = x.Notes,
                Status = x.Appointments.Any(a => a.IsActive) ? "booked" : "available",
                PatientName = x.Appointments.Where(a => a.IsActive).Select(a => a.Patient.User.Name).FirstOrDefault()
            }).ToListAsync(cancellationToken);

        return new OkObjectResult(slots);
    }

    public async Task<ActionResult<AvailabilitySlotResponse>> CreateDoctorAvailabilitySlotAsync(
        Guid doctorId, CreateAvailabilitySlotRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var scope = await GetSecretaryScopeAsync(user, cancellationToken);
        if (scope is null)
        {
            return new ObjectResult("This secretary is not assigned to any clinic.") { StatusCode = 403 };
        }

        if (!await DoctorBelongsToClinicAsync(doctorId, scope.Value.ClinicId, cancellationToken))
        {
            return new NotFoundObjectResult("Doctor not found in your clinic.");
        }

        if (request.SlotDate == default || request.StartTime == default || request.EndTime <= request.StartTime)
        {
            return new BadRequestObjectResult("A valid slotDate, startTime, and later endTime are required.");
        }

        var overlaps = await _dbContext.AvailabilitySlots.AsNoTracking().AnyAsync(
            x => x.DoctorId == doctorId && x.SlotDate == request.SlotDate
                && x.StartTime < request.EndTime && request.StartTime < x.EndTime,
            cancellationToken);
        if (overlaps)
        {
            return new ConflictObjectResult("The availability slot overlaps an existing slot.");
        }

        var slot = new AvailabilitySlot
        {
            DoctorId = doctorId,
            SlotDate = request.SlotDate,
            DayOfWeek = request.SlotDate.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Notes = NormalizeOptional(request.Notes),
            IsAvailable = true
        };
        _dbContext.AvailabilitySlots.Add(slot);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreatedAtActionResult(
            "GetDoctorAvailabilitySlots", "Secretaries", new { doctorId },
            new AvailabilitySlotResponse
            {
                AvailabilitySlotId = slot.AvailabilitySlotId,
                DoctorId = slot.DoctorId,
                SlotDate = slot.SlotDate,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                Notes = slot.Notes,
                Status = "available"
            });
    }

    public async Task<IActionResult> DeleteDoctorAvailabilitySlotAsync(
        Guid doctorId, Guid slotId, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var scope = await GetSecretaryScopeAsync(user, cancellationToken);
        if (scope is null || !await DoctorBelongsToClinicAsync(doctorId, scope.Value.ClinicId, cancellationToken))
        {
            return new NotFoundObjectResult("Doctor not found in your clinic.");
        }

        var slot = await _dbContext.AvailabilitySlots.FirstOrDefaultAsync(
            x => x.AvailabilitySlotId == slotId && x.DoctorId == doctorId, cancellationToken);
        if (slot is null)
        {
            return new NotFoundObjectResult("Availability slot not found.");
        }

        if (await _dbContext.Appointments.AsNoTracking().AnyAsync(x => x.AvailabilitySlotId == slotId && x.IsActive, cancellationToken))
        {
            return new ConflictObjectResult("A booked availability slot cannot be deleted.");
        }

        _dbContext.AvailabilitySlots.Remove(slot);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new NoContentResult();
    }

    public async Task<ActionResult<List<SecretaryReportResponse>>> GetReportsAsync(
        ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var scope = await GetSecretaryScopeAsync(user, cancellationToken);
        if (scope is null)
        {
            return new ObjectResult("This secretary is not assigned to any clinic.") { StatusCode = 403 };
        }

        var reports = await _dbContext.Reports.AsNoTracking()
            .Where(x => x.Secretary.User.ClinicId == scope.Value.ClinicId)
            .OrderByDescending(x => x.GeneratedAt)
            .Select(x => new SecretaryReportResponse
            {
                ReportId = x.ReportId,
                ReportType = x.ReportType,
                GeneratedAt = x.GeneratedAt,
                Files = x.ReportInformations.Select(file => new SecretaryReportFileResponse
                {
                    ReportInformationId = file.ReportInformationId,
                    FileFormat = file.FileFormat.ToString(),
                    FileName = Path.GetFileName(file.FilePath),
                    FileSizeInBytes = file.FileSizeInBytes,
                    DownloadUrl = $"/api/Secretaries/my-clinic/reports/{x.ReportId}/files/{file.ReportInformationId}"
                }).ToList()
            }).ToListAsync(cancellationToken);
        return new OkObjectResult(reports);
    }

    public async Task<ActionResult<SecretaryReportResponse>> GenerateReportAsync(
        GenerateSecretaryReportRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var scope = await GetSecretaryScopeAsync(user, cancellationToken);
        if (scope is null)
        {
            return new ObjectResult("This secretary is not assigned to any clinic.") { StatusCode = 403 };
        }

        if (!Enum.TryParse<ReportFileFormat>(request.FileFormat, true, out var format))
        {
            return new BadRequestObjectResult("fileFormat must be pdf or csv.");
        }

        var reportType = NormalizeOptional(request.ReportType) ?? "clinic-summary";
        var clinicName = await _dbContext.Clinics.Where(x => x.ClinicId == scope.Value.ClinicId).Select(x => x.Name).FirstAsync(cancellationToken);
        var patients = await _dbContext.Patients.CountAsync(x => x.User.ClinicId == scope.Value.ClinicId, cancellationToken);
        var doctors = await _dbContext.Doctors.CountAsync(x => x.User.ClinicId == scope.Value.ClinicId, cancellationToken);
        var appointments = await _dbContext.Appointments.CountAsync(x => x.Doctor.User.ClinicId == scope.Value.ClinicId, cancellationToken);
        var generatedAt = DateTime.UtcNow;
        byte[] bytes = format == ReportFileFormat.Csv
            ? Encoding.UTF8.GetBytes($"metric,value{Environment.NewLine}clinic,{Csv(clinicName)}{Environment.NewLine}patients,{patients}{Environment.NewLine}doctors,{doctors}{Environment.NewLine}appointments,{appointments}{Environment.NewLine}")
            : GenerateSummaryPdf(clinicName, patients, doctors, appointments, generatedAt);

        var extension = format == ReportFileFormat.Csv ? "csv" : "pdf";
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", scope.Value.SecretaryId.ToString("N"));
        Directory.CreateDirectory(directory);
        var fileName = $"{reportType.Replace(' ', '-')}-{generatedAt:yyyyMMddHHmmss}-{Guid.NewGuid():N}.{extension}";
        var physicalPath = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(physicalPath, bytes, cancellationToken);

        var report = new Report { SecretaryId = scope.Value.SecretaryId, ReportType = reportType, GeneratedAt = generatedAt };
        var info = new ReportInformation
        {
            ReportId = report.ReportId,
            FileFormat = format,
            FilePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), physicalPath).Replace('\\', '/'),
            FileSizeInBytes = bytes.LongLength
        };
        report.ReportInformations.Add(info);
        _dbContext.Reports.Add(report);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreatedAtActionResult("GetReports", "Secretaries", null, new SecretaryReportResponse
        {
            ReportId = report.ReportId,
            ReportType = report.ReportType,
            GeneratedAt = report.GeneratedAt,
            Files = [new SecretaryReportFileResponse
            {
                ReportInformationId = info.ReportInformationId,
                FileFormat = format.ToString(),
                FileName = fileName,
                FileSizeInBytes = info.FileSizeInBytes,
                DownloadUrl = $"/api/Secretaries/my-clinic/reports/{report.ReportId}/files/{info.ReportInformationId}"
            }]
        });
    }

    public async Task<ActionResult<SecretaryDashboardResponse>> GetDashboardAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var secretaryClinic = await _dbContext.Secretaries
            .AsNoTracking()
            .Where(secretary => secretary.SecretaryId == currentUserId.Value)
            .Select(secretary => new
            {
                secretary.User.ClinicId,
                ClinicName = secretary.User.Clinic != null ? secretary.User.Clinic.Name : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (secretaryClinic?.ClinicId is null)
        {
            return ForbiddenDashboard("This secretary is not assigned to any clinic.");
        }

        var clinicId = secretaryClinic.ClinicId.Value;
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);

        var pendingRequestsCount = await _dbContext.FileDownloadRequests
            .AsNoTracking()
            .CountAsync(
                request => request.Status == FileDownloadRequestStatus.Pending
                    && request.Patient.User.ClinicId == clinicId,
                cancellationToken);

        var patientRegistrationsCount = await _dbContext.Patients
            .AsNoTracking()
            .CountAsync(patient => patient.User.ClinicId == clinicId, cancellationToken);

        var reportsGeneratedCount = await _dbContext.Reports
            .AsNoTracking()
            .CountAsync(report => report.Secretary.User.ClinicId == clinicId, cancellationToken);

        var todayAvailabilitySlotsCount = await _dbContext.AvailabilitySlots
            .AsNoTracking()
            .CountAsync(
                slot => slot.SlotDate == today
                    && slot.Doctor.User.ClinicId == clinicId,
                cancellationToken);

        var todayAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.AppointmentDate == today
                && appointment.IsActive
                && appointment.Doctor.User.ClinicId == clinicId)
            .OrderBy(appointment => appointment.AppointmentTime)
            .Select(appointment => new SecretaryDashboardAppointmentResponse
            {
                AppointmentId = appointment.AppointmentId,
                DoctorId = appointment.DoctorId,
                DoctorName = appointment.Doctor.User.Name,
                PatientId = appointment.PatientId,
                PatientName = appointment.Patient.User.Name,
                PatientUserId = appointment.Patient.UserID,
                AppointmentDate = appointment.AppointmentDate,
                AppointmentTime = appointment.AppointmentTime,
                Notes = appointment.Notes,
                HasReached = false
            })
            .ToListAsync(cancellationToken);

        foreach (var appointment in todayAppointments)
        {
            appointment.HasReached = appointment.AppointmentTime <= currentTime;
        }

        var recentRegistrations = await _dbContext.Users
            .AsNoTracking()
            .Where(userItem => userItem.ClinicId == clinicId)
            .OrderByDescending(userItem => userItem.RegisteredAt)
            .ThenByDescending(userItem => userItem.Id)
            .Take(RecentRegistrationsLimit)
            .Select(userItem => new SecretaryRecentRegistrationResponse
            {
                UserId = userItem.Id,
                Name = userItem.Name,
                Email = userItem.Email ?? string.Empty,
                Role = userItem.UserRoles
                    .OrderBy(userRole => userRole.Role.Name)
                    .Select(userRole => userRole.Role.Name ?? string.Empty)
                    .FirstOrDefault() ?? string.Empty,
                RegisteredAt = userItem.RegisteredAt
            })
            .ToListAsync(cancellationToken);

        var dashboard = new SecretaryDashboardResponse
        {
            ClinicId = clinicId,
            ClinicName = secretaryClinic.ClinicName ?? string.Empty,
            PendingRequestsCount = pendingRequestsCount,
            PatientRegistrationsCount = patientRegistrationsCount,
            ReportsGeneratedCount = reportsGeneratedCount,
            TodayAvailabilitySlotsCount = todayAvailabilitySlotsCount,
            TodayAppointmentsCount = todayAppointments.Count,
            TodayAppointmentsReachedCount = todayAppointments.Count(appointment => appointment.HasReached),
            TodayAppointments = todayAppointments,
            RecentRegistrations = recentRegistrations
        };

        return new OkObjectResult(dashboard);
    }

    public async Task<ActionResult<List<SecretaryResponse>>> GetAllAsync(Guid? clinicId, CancellationToken cancellationToken = default)
    {
        var query = BuildSecretaryResponseQuery();

        if (clinicId.HasValue)
        {
            query = query.Where(s => s.ClinicId == clinicId.Value);
        }

        var secretaries = await query.ToListAsync(cancellationToken);
        return new OkObjectResult(secretaries);
    }

    public async Task<ActionResult<List<SecretaryResponse>>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        var secretaries = await BuildSecretaryResponseQuery()
            .Where(s => s.ClinicId == null)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(secretaries);
    }

    public async Task<ActionResult<List<SecretaryResponse>>> GetByClinicAsync(
        Guid clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var clinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(c => c.ClinicId == clinicId, cancellationToken);

        if (!clinicExists)
        {
            return new NotFoundObjectResult("Clinic not found.");
        }

        if (!await CanCurrentUserManageClinicAsync(clinicId, user, cancellationToken))
        {
            return ForbiddenList("You are not allowed to manage this clinic.");
        }

        var secretaries = await BuildSecretaryResponseQuery()
            .Where(s => s.ClinicId == clinicId)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(secretaries);
    }

    public async Task<ActionResult<List<SecretaryResponse>>> GetMyClinicSecretariesAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var myClinicId = await _dbContext.Secretaries
            .AsNoTracking()
            .Where(s => s.SecretaryId == currentUserId.Value)
            .Select(s => s.User.ClinicId)
            .FirstOrDefaultAsync(cancellationToken);

        if (myClinicId is null)
        {
            return ForbiddenList("This secretary is not assigned to any clinic.");
        }

        var secretaries = await BuildSecretaryResponseQuery()
            .Where(s => s.ClinicId == myClinicId.Value)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(secretaries);
    }

    public async Task<ActionResult<SecretaryResponse>> AssignToClinicAsync(
        Guid secretaryId,
        AssignSecretaryToClinicRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (request.ClinicId == Guid.Empty)
        {
            return new BadRequestObjectResult("ClinicId is required.");
        }

        var clinic = await _dbContext.Clinics
            .FirstOrDefaultAsync(c => c.ClinicId == request.ClinicId && c.IsActive, cancellationToken);

        if (clinic == null)
        {
            return new NotFoundObjectResult("Clinic not found or inactive.");
        }

        if (!await CanCurrentUserManageClinicAsync(request.ClinicId, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to manage this clinic.");
        }

        var secretary = await _dbContext.Secretaries
            .Include(s => s.User)
            .Include(s => s.ManagedClinic)
            .FirstOrDefaultAsync(s => s.SecretaryId == secretaryId, cancellationToken);

        if (secretary == null)
        {
            return new NotFoundObjectResult("Secretary not found.");
        }

        if (secretary.User.ClinicId.HasValue && secretary.User.ClinicId.Value != request.ClinicId)
        {
            return new BadRequestObjectResult("Secretary is already assigned to another clinic.");
        }

        if (secretary.ManagedClinic is not null && secretary.ManagedClinic.ClinicId != request.ClinicId)
        {
            return new BadRequestObjectResult("This secretary is assigned as admin to another clinic.");
        }

        secretary.User.ClinicId = clinic.ClinicId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetSecretaryResponseAsync(secretaryId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Secretary not found.")
            : new OkObjectResult(response);
    }

    public async Task<ActionResult<SecretaryResponse>> RemoveFromClinicAsync(
        Guid secretaryId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var secretary = await _dbContext.Secretaries
            .Include(s => s.User)
            .Include(s => s.ManagedClinic)
            .FirstOrDefaultAsync(s => s.SecretaryId == secretaryId, cancellationToken);

        if (secretary == null)
        {
            return new NotFoundObjectResult("Secretary not found.");
        }

        if (secretary.User.ClinicId is null)
        {
            return new BadRequestObjectResult("Secretary is not assigned to any clinic.");
        }

        if (!await CanCurrentUserManageClinicAsync(secretary.User.ClinicId.Value, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to manage this clinic.");
        }

        if (secretary.ManagedClinic is not null && secretary.ManagedClinic.ClinicId != secretary.User.ClinicId.Value)
        {
            return new BadRequestObjectResult("This secretary is the admin of another clinic. Change the clinic admin first.");
        }

        if (secretary.ManagedClinic is not null)
        {
            secretary.ManagedClinic.AdminSecretaryId = null;
        }

        secretary.User.ClinicId = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetSecretaryResponseAsync(secretaryId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Secretary not found.")
            : new OkObjectResult(response);
    }

    public async Task<ActionResult<SecretaryResponse>> UpdateInClinicAsync(
        Guid clinicId,
        Guid secretaryId,
        UpdateSecretaryRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new BadRequestObjectResult("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return new BadRequestObjectResult("Email is required.");
        }

        var clinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(clinic => clinic.ClinicId == clinicId, cancellationToken);

        if (!clinicExists)
        {
            return new NotFoundObjectResult("Clinic not found.");
        }

        if (!await CanCurrentUserManageClinicAsync(clinicId, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to manage this clinic.");
        }

        var secretary = await _dbContext.Secretaries
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(
                profile => profile.SecretaryId == secretaryId && profile.User.ClinicId == clinicId,
                cancellationToken);

        if (secretary is null)
        {
            return new NotFoundObjectResult("Secretary not found in this clinic.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedLookup = _userManager.NormalizeEmail(normalizedEmail);
        var emailAlreadyRegistered = await _userManager.Users
            .AsNoTracking()
            .AnyAsync(
                existingUser => existingUser.Id != secretaryId
                    && existingUser.NormalizedEmail == normalizedLookup,
                cancellationToken);

        if (emailAlreadyRegistered)
        {
            return new BadRequestObjectResult("Email already registered.");
        }

        secretary.User.Name = request.Name.Trim();
        secretary.User.Email = normalizedEmail;
        secretary.User.UserName = normalizedEmail;
        secretary.User.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        secretary.User.Address = NormalizeOptional(request.Address);
        secretary.User.DateOfBirth = request.DateOfBirth;

        var updateResult = await _userManager.UpdateAsync(secretary.User);
        if (!updateResult.Succeeded)
        {
            return new BadRequestObjectResult(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetSecretaryResponseAsync(secretaryId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Secretary not found.")
            : new OkObjectResult(response);
    }

    public Task<IActionResult> DeactivateInClinicAsync(
        Guid clinicId,
        Guid secretaryId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return SetActiveStateInClinicAsync(clinicId, secretaryId, false, user, cancellationToken);
    }

    public Task<IActionResult> ActivateInClinicAsync(
        Guid clinicId,
        Guid secretaryId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return SetActiveStateInClinicAsync(clinicId, secretaryId, true, user, cancellationToken);
    }

    private async Task<IActionResult> SetActiveStateInClinicAsync(
        Guid clinicId,
        Guid secretaryId,
        bool isActive,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var clinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(clinic => clinic.ClinicId == clinicId, cancellationToken);

        if (!clinicExists)
        {
            return new NotFoundObjectResult("Clinic not found.");
        }

        if (!await CanCurrentUserManageClinicAsync(clinicId, user, cancellationToken))
        {
            return ForbiddenAction("You are not allowed to manage this clinic.");
        }

        var secretary = await _dbContext.Secretaries
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(
                profile => profile.SecretaryId == secretaryId && profile.User.ClinicId == clinicId,
                cancellationToken);

        if (secretary is null)
        {
            return new NotFoundObjectResult("Secretary not found in this clinic.");
        }

        var currentUserId = GetCurrentUserId(user);
        if (!isActive
            && !user.IsInRole(nameof(UserSystemRole.SuperAdmin))
            && currentUserId.HasValue
            && currentUserId.Value == secretaryId)
        {
            return new BadRequestObjectResult("You cannot deactivate your own secretary account from this endpoint.");
        }

        if (secretary.User.IsActive == isActive)
        {
            return new NoContentResult();
        }

        secretary.User.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new NoContentResult();
    }

    private IQueryable<SecretaryResponse> BuildSecretaryResponseQuery()
    {
        return _dbContext.Secretaries
            .AsNoTracking()
            .Select(s => new SecretaryResponse
            {
                SecretaryId = s.SecretaryId,
                Name = s.User.Name,
                Email = s.User.Email ?? string.Empty,
                PhoneNumber = s.User.PhoneNumber,
                Address = s.User.Address,
                DateOfBirth = s.User.DateOfBirth,
                ClinicId = s.User.ClinicId,
                ClinicName = s.User.Clinic != null ? s.User.Clinic.Name : null,
                ManagedClinicId = s.ManagedClinic != null ? s.ManagedClinic.ClinicId : null,
                ManagedClinicName = s.ManagedClinic != null ? s.ManagedClinic.Name : null,
                IsClinicAdmin = s.ManagedClinic != null,
                IsActive = s.User.IsActive
            });
    }

    private async Task<(Guid SecretaryId, Guid ClinicId)?> GetSecretaryScopeAsync(
        ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return null;
        }

        var scope = await _dbContext.Secretaries.AsNoTracking()
            .Where(x => x.SecretaryId == userId.Value && x.User.IsActive && x.User.ClinicId != null)
            .Select(x => new { x.SecretaryId, ClinicId = x.User.ClinicId!.Value })
            .FirstOrDefaultAsync(cancellationToken);
        return scope is null ? null : (scope.SecretaryId, scope.ClinicId);
    }

    private Task<bool> DoctorBelongsToClinicAsync(Guid doctorId, Guid clinicId, CancellationToken cancellationToken) =>
        _dbContext.Doctors.AsNoTracking().AnyAsync(
            x => x.DoctorId == doctorId && x.User.ClinicId == clinicId && x.User.IsActive,
            cancellationToken);

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static byte[] GenerateSummaryPdf(
        string clinicName, int patients, int doctors, int appointments, DateTime generatedAt)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.Header().Text("Clinic summary report").FontSize(20).Bold();
            page.Content().PaddingVertical(20).Column(column =>
            {
                column.Spacing(10);
                column.Item().Text($"Clinic: {clinicName}");
                column.Item().Text($"Patients: {patients}");
                column.Item().Text($"Doctors: {doctors}");
                column.Item().Text($"Appointments: {appointments}");
            });
            page.Footer().Text($"Generated {generatedAt:yyyy-MM-dd HH:mm} UTC").FontSize(9);
        })).GeneratePdf();
    }

    private async Task<SecretaryResponse?> GetSecretaryResponseAsync(Guid secretaryId, CancellationToken cancellationToken)
    {
        return await BuildSecretaryResponseQuery()
            .FirstOrDefaultAsync(s => s.SecretaryId == secretaryId, cancellationToken);
    }

    private async Task<List<string>> GetDeleteBlockersAsync(Guid secretaryId, CancellationToken cancellationToken)
    {
        var blockers = new List<string>();

        if (await _dbContext.Reports.AnyAsync(report => report.SecretaryId == secretaryId, cancellationToken))
        {
            blockers.Add("reports");
        }

        return blockers;
    }

    private async Task<bool> CanCurrentUserManageClinicAsync(Guid clinicId, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            return true;
        }

        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return false;
        }

        return await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(c => c.ClinicId == clinicId && c.AdminSecretaryId == currentUserId.Value, cancellationToken);
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

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ActionResult<List<SecretaryResponse>> ForbiddenList(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }

    private static ActionResult<SecretaryResponse> ForbiddenSingle(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }

    private static IActionResult ForbiddenAction(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }

    private static ActionResult<SecretaryDashboardResponse> ForbiddenDashboard(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }
}
