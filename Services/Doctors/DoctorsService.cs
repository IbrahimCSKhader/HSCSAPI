using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Doctor;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Doctors;

public class DoctorsService : IDoctorsService
{
    public const string SuperAdminOrSecretaryRoles = nameof(UserSystemRole.SuperAdmin) + "," + nameof(UserSystemRole.Secretary);
    public const string SuperAdminOrSecretaryOrDoctorRoles = SuperAdminOrSecretaryRoles + "," + nameof(UserSystemRole.Doctor);

    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;

    public DoctorsService(
        AppDbContext dbContext,
        UserManager<User> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<ActionResult<List<DoctorResponse>>> GetAllAsync(
        Guid? clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var query = BuildDoctorResponseQuery();

        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            if (clinicId.HasValue)
            {
                query = query.Where(doctor => doctor.ClinicId == clinicId.Value);
            }

            var doctors = await query.ToListAsync(cancellationToken);
            return new OkObjectResult(doctors);
        }

        var secretaryClinicId = await GetCurrentSecretaryClinicIdAsync(user, cancellationToken);
        if (secretaryClinicId is null)
        {
            return ForbiddenList("This secretary is not assigned to any clinic.");
        }

        if (clinicId.HasValue && clinicId.Value != secretaryClinicId.Value)
        {
            return ForbiddenList("You are not allowed to access doctors outside your clinic.");
        }

        var clinicDoctors = await query
            .Where(doctor => doctor.ClinicId == secretaryClinicId.Value)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(clinicDoctors);
    }

    public async Task<ActionResult<List<DoctorResponse>>> GetByClinicAsync(
        Guid clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var clinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(clinic => clinic.ClinicId == clinicId, cancellationToken);

        if (!clinicExists)
        {
            return new NotFoundObjectResult("Clinic not found.");
        }

        if (!await CanCurrentUserAccessClinicAsync(clinicId, user, cancellationToken))
        {
            return ForbiddenList("You are not allowed to access doctors in this clinic.");
        }

        var doctors = await BuildDoctorResponseQuery()
            .Where(doctor => doctor.ClinicId == clinicId)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(doctors);
    }

    public async Task<ActionResult<DoctorResponse>> GetByIdAsync(
        Guid doctorId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (user.IsInRole(nameof(UserSystemRole.Doctor)) && currentUserId.Value != doctorId)
        {
            return ForbiddenSingle("You are not allowed to access another doctor profile.");
        }

        var doctor = await GetDoctorResponseAsync(doctorId, cancellationToken);
        if (doctor is null)
        {
            return new NotFoundObjectResult("Doctor not found.");
        }

        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)) || user.IsInRole(nameof(UserSystemRole.Doctor)))
        {
            return new OkObjectResult(doctor);
        }

        if (!doctor.ClinicId.HasValue || !await CanCurrentUserAccessClinicAsync(doctor.ClinicId.Value, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to access doctors outside your clinic.");
        }

        return new OkObjectResult(doctor);
    }

    public async Task<ActionResult<DoctorResponse>> GetMyProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var doctor = await GetDoctorResponseAsync(currentUserId.Value, cancellationToken);
        return doctor is null
            ? new NotFoundObjectResult("Doctor not found.")
            : new OkObjectResult(doctor);
    }

    public async Task<ActionResult<DoctorDashboardResponse>> GetMyDashboardAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var (doctor, error) = await GetCurrentDoctorInfoOrErrorAsync(user, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var weekStartDate = GetWeekStartDate(today);
        var weekEndDate = weekStartDate.AddDays(6);

        var todayAppointmentsCount = await _dbContext.Appointments
            .AsNoTracking()
            .CountAsync(
                appointment => appointment.DoctorId == doctor!.DoctorId
                    && appointment.AppointmentDate == today,
                cancellationToken);

        var pendingLabRequestsCount = await CountPendingLabRequestsForClinicAsync(
            doctor!.ClinicId,
            cancellationToken);
        var pendingImagingRequestsCount = await CountPendingImagingRequestsForClinicAsync(
            doctor.ClinicId,
            cancellationToken);
        var unreadMessagesCount = await CountUnreadMessagesAsync(doctor.DoctorId, cancellationToken);

        var upcomingAppointments = await BuildDoctorAppointmentProjectionQuery(doctor.DoctorId)
            .Where(appointment => appointment.AppointmentDate >= today)
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.AppointmentTime)
            .Take(3)
            .ToListAsync(cancellationToken);

        var weeklyCounts = await _dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.DoctorId == doctor.DoctorId
                && appointment.AppointmentDate >= weekStartDate
                && appointment.AppointmentDate <= weekEndDate)
            .GroupBy(appointment => appointment.AppointmentDate)
            .Select(group => new
            {
                Date = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var weeklyCountsByDate = weeklyCounts.ToDictionary(item => item.Date, item => item.Count);
        var weeklyAppointments = BuildWeekDates(weekStartDate)
            .Select(date => new DoctorWeeklyAppointmentCountResponse
            {
                Date = date,
                DayOfWeek = date.DayOfWeek.ToString(),
                AppointmentsCount = weeklyCountsByDate.TryGetValue(date, out var count) ? count : 0
            })
            .ToList();

        var totalPendingClinicalRequests = pendingLabRequestsCount + pendingImagingRequestsCount;

        return new OkObjectResult(new DoctorDashboardResponse
        {
            DoctorId = doctor.DoctorId,
            DoctorName = doctor.DoctorName,
            ClinicId = doctor.ClinicId,
            ClinicName = doctor.ClinicName,
            Today = today,
            WeekStartDate = weekStartDate,
            WeekEndDate = weekEndDate,
            TodayAppointmentsCount = todayAppointmentsCount,
            PendingLabRequestsCount = pendingLabRequestsCount,
            PendingImagingRequestsCount = pendingImagingRequestsCount,
            TotalPendingClinicalRequestsCount = totalPendingClinicalRequests,
            UnreadMessagesCount = unreadMessagesCount,
            UpcomingAppointments = upcomingAppointments.Select(ToDoctorAppointmentSummary).ToList(),
            WeeklyAppointments = weeklyAppointments,
            ClinicalRequests = new DoctorClinicalRequestsSummaryResponse
            {
                TotalPending = totalPendingClinicalRequests,
                LabRequests = pendingLabRequestsCount,
                ImagingRequests = pendingImagingRequestsCount
            }
        });
    }

    public async Task<ActionResult<DoctorAppointmentsScheduleResponse>> GetMyAppointmentsScheduleAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var (doctor, error) = await GetCurrentDoctorInfoOrErrorAsync(user, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var (rangeStart, rangeEnd) = ResolveScheduleRange(fromDate, toDate, today);
        if (rangeStart > rangeEnd)
        {
            return new BadRequestObjectResult("fromDate must be before or equal to toDate.");
        }

        var appointments = await BuildDoctorAppointmentProjectionQuery(doctor!.DoctorId)
            .Where(appointment => appointment.AppointmentDate >= rangeStart
                && appointment.AppointmentDate <= rangeEnd)
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.AppointmentTime)
            .ToListAsync(cancellationToken);

        var appointmentsByDate = appointments
            .GroupBy(appointment => appointment.AppointmentDate)
            .ToDictionary(
                group => group.Key,
                group => group.Select(ToDoctorAppointmentSummary).ToList());

        var days = new List<DoctorScheduleDayResponse>();
        for (var date = rangeStart; date <= rangeEnd; date = date.AddDays(1))
        {
            days.Add(new DoctorScheduleDayResponse
            {
                Date = date,
                DayOfWeek = date.DayOfWeek.ToString(),
                IsToday = date == today,
                Appointments = appointmentsByDate.TryGetValue(date, out var dayAppointments)
                    ? dayAppointments
                    : []
            });
        }

        return new OkObjectResult(new DoctorAppointmentsScheduleResponse
        {
            FromDate = rangeStart,
            ToDate = rangeEnd,
            TotalAppointmentsCount = appointments.Count,
            Days = days
        });
    }

    public async Task<ActionResult<DoctorAppointmentDetailResponse>> GetMyAppointmentDetailAsync(
        Guid appointmentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var (doctor, error) = await GetCurrentDoctorInfoOrErrorAsync(user, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var appointment = await BuildDoctorAppointmentProjectionQuery(doctor!.DoctorId)
            .FirstOrDefaultAsync(
                appointment => appointment.AppointmentId == appointmentId,
                cancellationToken);

        if (appointment is null)
        {
            return new NotFoundObjectResult("Appointment not found.");
        }

        return new OkObjectResult(ToDoctorAppointmentDetail(appointment));
    }

    public async Task<ActionResult<DoctorResponse>> UpdateAsync(
        Guid doctorId,
        UpdateDoctorRequest request,
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

        if (request.ClinicId == Guid.Empty)
        {
            return new BadRequestObjectResult("ClinicId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ProfessionalLicenseNumber))
        {
            return new BadRequestObjectResult("ProfessionalLicenseNumber is required.");
        }

        var doctor = await _dbContext.Doctors
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.DoctorId == doctorId, cancellationToken);

        if (doctor is null)
        {
            return new NotFoundObjectResult("Doctor not found.");
        }

        if (!await CanCurrentUserManageDoctorAsync(doctor, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to manage this doctor.");
        }

        if (!await CanCurrentUserAccessClinicAsync(request.ClinicId, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to assign the doctor to this clinic.");
        }

        var clinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(clinic => clinic.ClinicId == request.ClinicId, cancellationToken);

        if (!clinicExists)
        {
            return new NotFoundObjectResult("Clinic not found.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedLookup = _userManager.NormalizeEmail(normalizedEmail);
        var emailAlreadyRegistered = await _userManager.Users
            .AsNoTracking()
            .AnyAsync(
                existingUser => existingUser.Id != doctorId
                    && existingUser.NormalizedEmail == normalizedLookup,
                cancellationToken);

        if (emailAlreadyRegistered)
        {
            return new BadRequestObjectResult("Email already registered.");
        }

        var normalizedLicenseNumber = request.ProfessionalLicenseNumber.Trim();
        var licenseNumberExists = await _dbContext.Doctors
            .AsNoTracking()
            .AnyAsync(
                existingDoctor => existingDoctor.DoctorId != doctorId
                    && existingDoctor.ProfessionalLicenseNumber == normalizedLicenseNumber,
                cancellationToken);

        if (licenseNumberExists)
        {
            return new BadRequestObjectResult("Professional license number is already registered.");
        }

        doctor.User.Name = request.Name.Trim();
        doctor.User.Email = normalizedEmail;
        doctor.User.UserName = normalizedEmail;
        doctor.User.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        doctor.User.Address = NormalizeOptional(request.Address);
        doctor.User.DateOfBirth = request.DateOfBirth;
        doctor.User.ClinicId = request.ClinicId;
        doctor.ProfessionalLicenseNumber = normalizedLicenseNumber;

        var updateResult = await _userManager.UpdateAsync(doctor.User);
        if (!updateResult.Succeeded)
        {
            return new BadRequestObjectResult(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetDoctorResponseAsync(doctorId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Doctor not found.")
            : new OkObjectResult(response);
    }

    public async Task<ActionResult<DoctorResponse>> UpdateMyProfileAsync(
        UpdateMyDoctorProfileRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new BadRequestObjectResult("Name is required.");
        }

        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var doctor = await _dbContext.Doctors
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.DoctorId == currentUserId.Value, cancellationToken);

        if (doctor is null)
        {
            return new NotFoundObjectResult("Doctor not found.");
        }

        doctor.User.Name = request.Name.Trim();
        doctor.User.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        doctor.User.Address = NormalizeOptional(request.Address);
        doctor.User.DateOfBirth = request.DateOfBirth;

        var updateResult = await _userManager.UpdateAsync(doctor.User);
        if (!updateResult.Succeeded)
        {
            return new BadRequestObjectResult(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetDoctorResponseAsync(doctor.DoctorId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Doctor not found.")
            : new OkObjectResult(response);
    }

    public async Task<ActionResult<ChangeDoctorPasswordResponse>> ChangeMyPasswordAsync(
        ChangeDoctorPasswordRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return new BadRequestObjectResult(new ChangeDoctorPasswordResponse
            {
                Success = false,
                Message = "Current password is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return new BadRequestObjectResult(new ChangeDoctorPasswordResponse
            {
                Success = false,
                Message = "New password is required."
            });
        }

        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return new BadRequestObjectResult(new ChangeDoctorPasswordResponse
            {
                Success = false,
                Message = "New password and confirm password do not match."
            });
        }

        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var doctor = await _dbContext.Doctors
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.DoctorId == currentUserId.Value, cancellationToken);

        if (doctor is null)
        {
            return new NotFoundObjectResult("Doctor not found.");
        }

        var changeResult = await _userManager.ChangePasswordAsync(
            doctor.User,
            request.CurrentPassword,
            request.NewPassword);

        if (!changeResult.Succeeded)
        {
            return new BadRequestObjectResult(new ChangeDoctorPasswordResponse
            {
                Success = false,
                Message = string.Join(" ", changeResult.Errors.Select(error => error.Description))
            });
        }

        return new OkObjectResult(new ChangeDoctorPasswordResponse
        {
            Success = true,
            Message = "Password updated successfully."
        });
    }

    public async Task<IActionResult> DeleteAsync(
        Guid doctorId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var doctor = await _dbContext.Doctors
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.DoctorId == doctorId, cancellationToken);

        if (doctor is null)
        {
            return new NotFoundObjectResult("Doctor not found.");
        }

        if (!await CanCurrentUserManageDoctorAsync(doctor, user, cancellationToken))
        {
            return ForbiddenAction("You are not allowed to delete this doctor.");
        }

        var blockers = await GetDeleteBlockersAsync(doctorId, cancellationToken);
        if (blockers.Count > 0)
        {
            return new BadRequestObjectResult(
                $"Cannot delete doctor because related {string.Join(", ", blockers)} exist.");
        }

        var deleteResult = await _userManager.DeleteAsync(doctor.User);
        if (!deleteResult.Succeeded)
        {
            return new BadRequestObjectResult(string.Join(" ", deleteResult.Errors.Select(error => error.Description)));
        }

        return new NoContentResult();
    }

    private async Task<(CurrentDoctorInfo? Doctor, ActionResult? Error)> GetCurrentDoctorInfoOrErrorAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return (null, new UnauthorizedObjectResult("Invalid token."));
        }

        var doctor = await _dbContext.Doctors
            .AsNoTracking()
            .Where(profile => profile.DoctorId == currentUserId.Value)
            .Select(profile => new CurrentDoctorInfo
            {
                DoctorId = profile.DoctorId,
                DoctorName = profile.User.Name,
                ClinicId = profile.User.ClinicId,
                ClinicName = profile.User.Clinic != null ? profile.User.Clinic.Name : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        return doctor is null
            ? (null, new NotFoundObjectResult("Doctor not found."))
            : (doctor, null);
    }

    private IQueryable<DoctorAppointmentProjection> BuildDoctorAppointmentProjectionQuery(Guid doctorId)
    {
        return _dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.DoctorId == doctorId)
            .Select(appointment => new DoctorAppointmentProjection
            {
                AppointmentId = appointment.AppointmentId,
                DoctorId = appointment.DoctorId,
                DoctorName = appointment.Doctor.User.Name,
                PatientId = appointment.PatientId,
                PatientName = appointment.Patient.User.Name,
                PatientUserId = appointment.Patient.UserID,
                ClinicId = appointment.Doctor.User.ClinicId,
                ClinicName = appointment.Doctor.User.Clinic != null ? appointment.Doctor.User.Clinic.Name : null,
                AppointmentDate = appointment.AppointmentDate,
                AppointmentTime = appointment.AppointmentTime,
                SlotStartTime = appointment.AvailabilitySlot.StartTime,
                SlotEndTime = appointment.AvailabilitySlot.EndTime,
                Notes = appointment.Notes
            });
    }

    private async Task<int> CountPendingLabRequestsForClinicAsync(
        Guid? clinicId,
        CancellationToken cancellationToken)
    {
        if (!clinicId.HasValue)
        {
            return 0;
        }

        return await _dbContext.LabTestRequests
            .AsNoTracking()
            .CountAsync(
                request => request.ResultMedicalFileId == null
                    && request.LaboratoryTechnologist != null
                    && request.LaboratoryTechnologist.User.ClinicId == clinicId.Value,
                cancellationToken);
    }

    private async Task<int> CountPendingImagingRequestsForClinicAsync(
        Guid? clinicId,
        CancellationToken cancellationToken)
    {
        if (!clinicId.HasValue)
        {
            return 0;
        }

        return await _dbContext.ImagingTestRequests
            .AsNoTracking()
            .CountAsync(
                request => request.ResultMedicalFileId == null
                    && request.RadiologyTechnologist != null
                    && request.RadiologyTechnologist.User.ClinicId == clinicId.Value,
                cancellationToken);
    }

    private async Task<int> CountUnreadMessagesAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ChatMessages
            .AsNoTracking()
            .CountAsync(
                message => message.SenderId != doctorId
                    && message.ReadAt == null
                    && (message.Chat.UserOneId == doctorId || message.Chat.UserTwoId == doctorId),
                cancellationToken);
    }

    private static (DateOnly FromDate, DateOnly ToDate) ResolveScheduleRange(
        DateOnly? fromDate,
        DateOnly? toDate,
        DateOnly today)
    {
        if (!fromDate.HasValue && !toDate.HasValue)
        {
            var weekStart = GetWeekStartDate(today);
            return (weekStart, weekStart.AddDays(6));
        }

        if (fromDate.HasValue && !toDate.HasValue)
        {
            return (fromDate.Value, fromDate.Value);
        }

        if (!fromDate.HasValue && toDate.HasValue)
        {
            return (toDate.Value, toDate.Value);
        }

        return (fromDate!.Value, toDate!.Value);
    }

    private static DateOnly GetWeekStartDate(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset);
    }

    private static IEnumerable<DateOnly> BuildWeekDates(DateOnly weekStartDate)
    {
        for (var index = 0; index < 7; index++)
        {
            yield return weekStartDate.AddDays(index);
        }
    }

    private static DoctorAppointmentSummaryResponse ToDoctorAppointmentSummary(
        DoctorAppointmentProjection appointment)
    {
        return new DoctorAppointmentSummaryResponse
        {
            AppointmentId = appointment.AppointmentId,
            PatientId = appointment.PatientId,
            PatientName = appointment.PatientName,
            PatientUserId = appointment.PatientUserId,
            AppointmentDate = appointment.AppointmentDate,
            DayOfWeek = appointment.AppointmentDate.DayOfWeek.ToString(),
            AppointmentTime = appointment.AppointmentTime,
            DurationMinutes = CalculateDurationMinutes(appointment.SlotStartTime, appointment.SlotEndTime),
            Status = "Scheduled",
            ReasonForVisit = appointment.Notes
        };
    }

    private static DoctorAppointmentDetailResponse ToDoctorAppointmentDetail(
        DoctorAppointmentProjection appointment)
    {
        return new DoctorAppointmentDetailResponse
        {
            AppointmentId = appointment.AppointmentId,
            DoctorId = appointment.DoctorId,
            DoctorName = appointment.DoctorName,
            PatientId = appointment.PatientId,
            PatientName = appointment.PatientName,
            PatientUserId = appointment.PatientUserId,
            ClinicId = appointment.ClinicId,
            ClinicName = appointment.ClinicName,
            AppointmentDate = appointment.AppointmentDate,
            DayOfWeek = appointment.AppointmentDate.DayOfWeek.ToString(),
            AppointmentTime = appointment.AppointmentTime,
            DurationMinutes = CalculateDurationMinutes(appointment.SlotStartTime, appointment.SlotEndTime),
            Status = "Scheduled",
            ReasonForVisit = appointment.Notes
        };
    }

    private static int CalculateDurationMinutes(TimeOnly startTime, TimeOnly endTime)
    {
        var minutes = (int)(endTime.ToTimeSpan() - startTime.ToTimeSpan()).TotalMinutes;
        return minutes > 0 ? minutes : 45;
    }

    private IQueryable<DoctorResponse> BuildDoctorResponseQuery()
    {
        return _dbContext.Doctors
            .AsNoTracking()
            .Select(doctor => new DoctorResponse
            {
                DoctorId = doctor.DoctorId,
                Name = doctor.User.Name,
                Email = doctor.User.Email ?? string.Empty,
                PhoneNumber = doctor.User.PhoneNumber,
                Address = doctor.User.Address,
                DateOfBirth = doctor.User.DateOfBirth,
                ClinicId = doctor.User.ClinicId,
                ClinicName = doctor.User.Clinic != null ? doctor.User.Clinic.Name : null,
                ProfessionalLicenseNumber = doctor.ProfessionalLicenseNumber,
                EmailConfirmed = doctor.User.EmailConfirmed
            });
    }

    private async Task<DoctorResponse?> GetDoctorResponseAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        return await BuildDoctorResponseQuery()
            .FirstOrDefaultAsync(doctor => doctor.DoctorId == doctorId, cancellationToken);
    }

    private async Task<bool> CanCurrentUserManageDoctorAsync(
        Doctor doctor,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            return true;
        }

        if (!doctor.User.ClinicId.HasValue)
        {
            return false;
        }

        return await CanCurrentUserAccessClinicAsync(doctor.User.ClinicId.Value, user, cancellationToken);
    }

    private async Task<bool> CanCurrentUserAccessClinicAsync(
        Guid clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            return true;
        }

        var secretaryClinicId = await GetCurrentSecretaryClinicIdAsync(user, cancellationToken);
        return secretaryClinicId.HasValue && secretaryClinicId.Value == clinicId;
    }

    private async Task<Guid?> GetCurrentSecretaryClinicIdAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return null;
        }

        return await _dbContext.Secretaries
            .AsNoTracking()
            .Where(secretary => secretary.SecretaryId == currentUserId.Value)
            .Select(secretary => secretary.User.ClinicId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<string>> GetDeleteBlockersAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var blockers = new List<string>();

        if (await _dbContext.AvailabilitySlots.AnyAsync(slot => slot.DoctorId == doctorId, cancellationToken))
        {
            blockers.Add("availability slots");
        }

        if (await _dbContext.Appointments.AnyAsync(appointment => appointment.DoctorId == doctorId, cancellationToken))
        {
            blockers.Add("appointments");
        }

        if (await _dbContext.Reminders.AnyAsync(reminder => reminder.DoctorId == doctorId, cancellationToken))
        {
            blockers.Add("reminders");
        }

        if (await _dbContext.MedicalFiles.AnyAsync(file => file.UploadedByDoctorId == doctorId, cancellationToken))
        {
            blockers.Add("medical files");
        }

        return blockers;
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

    private static ActionResult<List<DoctorResponse>> ForbiddenList(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }

    private static ActionResult<DoctorResponse> ForbiddenSingle(string message)
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

    private sealed class CurrentDoctorInfo
    {
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public Guid? ClinicId { get; set; }
        public string? ClinicName { get; set; }
    }

    private sealed class DoctorAppointmentProjection
    {
        public Guid AppointmentId { get; set; }
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public Guid PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientUserId { get; set; } = string.Empty;
        public Guid? ClinicId { get; set; }
        public string? ClinicName { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public TimeOnly AppointmentTime { get; set; }
        public TimeOnly SlotStartTime { get; set; }
        public TimeOnly SlotEndTime { get; set; }
        public string? Notes { get; set; }
    }
}
