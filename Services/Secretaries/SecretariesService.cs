using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Secretary;
using HSCSAPI.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Secretaries;

public class SecretariesService : ISecretariesService
{
    public const string SuperAdminOrSecretaryRoles = nameof(UserSystemRole.SuperAdmin) + "," + nameof(UserSystemRole.Secretary);
    private const int RecentRegistrationsLimit = 10;

    private readonly AppDbContext _dbContext;

    public SecretariesService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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
        var currentDay = now.DayOfWeek;

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
                slot => slot.DayOfWeek == currentDay
                    && slot.Doctor.User.ClinicId == clinicId,
                cancellationToken);

        var todayAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.AppointmentDate == today
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
            .FirstOrDefaultAsync(c => c.ClinicId == request.ClinicId, cancellationToken);

        if (clinic == null)
        {
            return new NotFoundObjectResult("Clinic not found.");
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
                IsClinicAdmin = s.ManagedClinic != null
            });
    }

    private async Task<SecretaryResponse?> GetSecretaryResponseAsync(Guid secretaryId, CancellationToken cancellationToken)
    {
        return await BuildSecretaryResponseQuery()
            .FirstOrDefaultAsync(s => s.SecretaryId == secretaryId, cancellationToken);
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

    private static ActionResult<SecretaryDashboardResponse> ForbiddenDashboard(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }
}
