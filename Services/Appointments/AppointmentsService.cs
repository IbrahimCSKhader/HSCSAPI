using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Appointment;
using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Appointments;

public class AppointmentsService : IAppointmentsService
{
    public const string AllowedReadRoles =
        nameof(UserSystemRole.SuperAdmin) + ","
        + nameof(UserSystemRole.Secretary) + ","
        + nameof(UserSystemRole.Doctor) + ","
        + nameof(UserSystemRole.Patient);

    public const string AllowedWriteRoles =
        nameof(UserSystemRole.SuperAdmin) + ","
        + nameof(UserSystemRole.Secretary) + ","
        + nameof(UserSystemRole.Patient);

    private readonly AppDbContext _dbContext;

    public AppointmentsService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ActionResult<List<AppointmentResponse>>> GetAllAsync(
        Guid? clinicId,
        DateOnly? fromDate,
        DateOnly? toDate,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(fromDate, toDate);

        var query = ApplyDateRangeFilter(BuildAppointmentResponseQuery(), fromDate, toDate);

        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            if (clinicId.HasValue)
            {
                query = query.Where(appointment => appointment.ClinicId == clinicId.Value);
            }

            return new OkObjectResult(await OrderAppointments(query).ToListAsync(cancellationToken));
        }

        if (user.IsInRole(nameof(UserSystemRole.Secretary)))
        {
            var secretaryClinicId = await GetCurrentSecretaryClinicIdAsync(user, cancellationToken);
            if (secretaryClinicId is null)
            {
                throw new UnauthorizedAccessException("This secretary is not assigned to any clinic.");
            }

            if (clinicId.HasValue && clinicId.Value != secretaryClinicId.Value)
            {
                throw new UnauthorizedAccessException("You are not allowed to access appointments outside your clinic.");
            }

            query = query.Where(appointment => appointment.ClinicId == secretaryClinicId.Value);
            return new OkObjectResult(await OrderAppointments(query).ToListAsync(cancellationToken));
        }

        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            throw new UnauthorizedAccessException("Invalid token.");
        }

        if (user.IsInRole(nameof(UserSystemRole.Doctor)))
        {
            query = query.Where(appointment => appointment.DoctorId == currentUserId.Value);
            return new OkObjectResult(await OrderAppointments(query).ToListAsync(cancellationToken));
        }

        if (user.IsInRole(nameof(UserSystemRole.Patient)))
        {
            query = query.Where(appointment => appointment.PatientId == currentUserId.Value);
            return new OkObjectResult(await OrderAppointments(query).ToListAsync(cancellationToken));
        }

        throw new UnauthorizedAccessException("You are not allowed to access appointments.");
    }

    public async Task<ActionResult<List<AppointmentResponse>>> GetMineAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(fromDate, toDate);

        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            throw new UnauthorizedAccessException("Invalid token.");
        }

        var query = ApplyDateRangeFilter(BuildAppointmentResponseQuery(), fromDate, toDate);

        if (user.IsInRole(nameof(UserSystemRole.Doctor)))
        {
            query = query.Where(appointment => appointment.DoctorId == currentUserId.Value);
        }
        else if (user.IsInRole(nameof(UserSystemRole.Patient)))
        {
            query = query.Where(appointment => appointment.PatientId == currentUserId.Value);
        }
        else
        {
            throw new UnauthorizedAccessException("Only doctors and patients can use this endpoint.");
        }

        return new OkObjectResult(await OrderAppointments(query).ToListAsync(cancellationToken));
    }

    public async Task<ActionResult<AppointmentResponse>> GetByIdAsync(
        Guid appointmentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var appointment = await BuildAppointmentResponseQuery()
            .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId, cancellationToken);

        if (appointment is null)
        {
            throw new KeyNotFoundException("Appointment not found.");
        }

        if (!await CanAccessAppointmentAsync(appointment, user, cancellationToken))
        {
            throw new UnauthorizedAccessException("You are not allowed to access this appointment.");
        }

        return new OkObjectResult(appointment);
    }

    public async Task<ActionResult<AppointmentResponse>> CreateAsync(
        CreateAppointmentRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        await ValidateAppointmentRequestAsync(
            request.DoctorId,
            request.PatientId,
            request.AppointmentDate,
            request.AppointmentTime,
            user,
            excludedAppointmentId: null,
            cancellationToken);

        var slot = await _dbContext.AvailabilitySlots.FirstOrDefaultAsync(
            x => x.AvailabilitySlotId == request.AvailabilitySlotId
                && x.DoctorId == request.DoctorId
                && x.SlotDate == request.AppointmentDate
                && x.StartTime == request.AppointmentTime
                && x.IsAvailable,
            cancellationToken);
        if (slot is null)
        {
            throw new InvalidOperationException("The selected availability slot is invalid or unavailable.");
        }

        var slotAlreadyBooked = await _dbContext.Appointments.AsNoTracking().AnyAsync(
            x => x.AvailabilitySlotId == slot.AvailabilitySlotId && x.IsActive,
            cancellationToken);
        if (slotAlreadyBooked)
        {
            throw new InvalidOperationException("The selected availability slot is already booked.");
        }

        var appointment = new Appointment
        {
            DoctorId = request.DoctorId,
            PatientId = request.PatientId,
            AvailabilitySlotId = slot.AvailabilitySlotId,
            AppointmentDate = request.AppointmentDate,
            AppointmentTime = request.AppointmentTime,
            Notes = NormalizeOptional(request.Notes)
        };

        _dbContext.Appointments.Add(appointment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetAppointmentResponseAsync(appointment.AppointmentId, cancellationToken);
        if (response is null)
        {
            throw new Exception("Appointment was created but could not be loaded.");
        }

        return new CreatedAtActionResult(
            actionName: "GetById",
            controllerName: "Appointments",
            routeValues: new { appointmentId = response.AppointmentId },
            value: response);
    }

    public async Task<ActionResult<AppointmentResponse>> UpdateAsync(
        Guid appointmentId,
        UpdateAppointmentRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId, cancellationToken);

        if (appointment is null)
        {
            throw new KeyNotFoundException("Appointment not found.");
        }

        var currentResponse = await GetAppointmentResponseAsync(appointmentId, cancellationToken);
        if (currentResponse is null)
        {
            throw new KeyNotFoundException("Appointment not found.");
        }

        if (!await CanAccessAppointmentAsync(currentResponse, user, cancellationToken))
        {
            throw new UnauthorizedAccessException("You are not allowed to update this appointment.");
        }

        await ValidateAppointmentRequestAsync(
            request.DoctorId,
            request.PatientId,
            request.AppointmentDate,
            request.AppointmentTime,
            user,
            excludedAppointmentId: appointmentId,
            cancellationToken);

        var slot = await EnsureAvailabilitySlotAsync(
            request.DoctorId,
            request.AppointmentDate,
            request.AppointmentTime,
            cancellationToken);

        appointment.DoctorId = request.DoctorId;
        appointment.PatientId = request.PatientId;
        appointment.AvailabilitySlotId = slot.AvailabilitySlotId;
        appointment.AppointmentDate = request.AppointmentDate;
        appointment.AppointmentTime = request.AppointmentTime;
        appointment.Notes = NormalizeOptional(request.Notes);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetAppointmentResponseAsync(appointmentId, cancellationToken);
        if (response is null)
        {
            throw new KeyNotFoundException("Appointment not found.");
        }

        return new OkObjectResult(response);
    }

    public Task<IActionResult> DeactivateAsync(
        Guid appointmentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return SetActiveStateAsync(appointmentId, false, user, cancellationToken);
    }

    public Task<IActionResult> ActivateAsync(
        Guid appointmentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return SetActiveStateAsync(appointmentId, true, user, cancellationToken);
    }

    private async Task<IActionResult> SetActiveStateAsync(
        Guid appointmentId,
        bool isActive,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId, cancellationToken);

        if (appointment is null)
        {
            throw new KeyNotFoundException("Appointment not found.");
        }

        var response = await GetAppointmentResponseAsync(appointmentId, cancellationToken);
        if (response is null)
        {
            throw new KeyNotFoundException("Appointment not found.");
        }

        if (!await CanAccessAppointmentAsync(response, user, cancellationToken))
        {
            throw new UnauthorizedAccessException("You are not allowed to change this appointment's active state.");
        }

        if (appointment.IsActive == isActive)
        {
            return new NoContentResult();
        }

        if (isActive)
        {
            await ValidateAppointmentRequestAsync(
                appointment.DoctorId,
                appointment.PatientId,
                appointment.AppointmentDate,
                appointment.AppointmentTime,
                user,
                appointmentId,
                cancellationToken);
        }

        appointment.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new NoContentResult();
    }

    private async Task ValidateAppointmentRequestAsync(
        Guid doctorId,
        Guid patientId,
        DateOnly appointmentDate,
        TimeOnly appointmentTime,
        ClaimsPrincipal user,
        Guid? excludedAppointmentId,
        CancellationToken cancellationToken)
    {
        if (doctorId == Guid.Empty)
        {
            throw new ArgumentException("DoctorId is required.");
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("PatientId is required.");
        }

        if (appointmentDate == default)
        {
            throw new ArgumentException("AppointmentDate is required.");
        }

        if (appointmentTime == default)
        {
            throw new ArgumentException("AppointmentTime is required.");
        }

        var doctor = await _dbContext.Doctors
            .AsNoTracking()
            .Where(x => x.DoctorId == doctorId)
            .Select(x => new
            {
                x.DoctorId,
                x.User.ClinicId,
                x.User.IsActive,
                IsClinicActive = x.User.Clinic != null && x.User.Clinic.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (doctor is null)
        {
            throw new KeyNotFoundException("Doctor not found.");
        }

        if (!doctor.IsActive || !doctor.IsClinicActive)
        {
            throw new InvalidOperationException("Doctor account and clinic must be active.");
        }

        var patient = await _dbContext.Patients
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .Select(x => new
            {
                x.PatientId,
                x.User.ClinicId,
                x.User.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (patient is null)
        {
            throw new KeyNotFoundException("Patient not found.");
        }

        if (!patient.IsActive)
        {
            throw new InvalidOperationException("Patient account must be active.");
        }

        if (!doctor.ClinicId.HasValue || !patient.ClinicId.HasValue || doctor.ClinicId.Value != patient.ClinicId.Value)
        {
            throw new InvalidOperationException("Doctor and patient must belong to the same clinic.");
        }

        if (!await CanCurrentUserManageClinicAsync(doctor.ClinicId.Value, patientId, user, cancellationToken))
        {
            throw new UnauthorizedAccessException("You are not allowed to manage appointments for this clinic or patient.");
        }

        var doctorAlreadyBooked = await _dbContext.Appointments
            .AsNoTracking()
            .AnyAsync(
                x => x.DoctorId == doctorId
                    && x.IsActive
                    && x.AppointmentDate == appointmentDate
                    && x.AppointmentTime == appointmentTime
                    && (!excludedAppointmentId.HasValue || x.AppointmentId != excludedAppointmentId.Value),
                cancellationToken);

        if (doctorAlreadyBooked)
        {
            throw new InvalidOperationException("The doctor already has an appointment at this date and time.");
        }

        var patientAlreadyBooked = await _dbContext.Appointments
            .AsNoTracking()
            .AnyAsync(
                x => x.PatientId == patientId
                    && x.IsActive
                    && x.AppointmentDate == appointmentDate
                    && x.AppointmentTime == appointmentTime
                    && (!excludedAppointmentId.HasValue || x.AppointmentId != excludedAppointmentId.Value),
                cancellationToken);

        if (patientAlreadyBooked)
        {
            throw new InvalidOperationException("The patient already has an appointment at this date and time.");
        }
    }

    private async Task<AvailabilitySlot> EnsureAvailabilitySlotAsync(
        Guid doctorId,
        DateOnly slotDate,
        TimeOnly appointmentTime,
        CancellationToken cancellationToken)
    {
        var endTime = appointmentTime.AddMinutes(30);
        var slot = await _dbContext.AvailabilitySlots
            .FirstOrDefaultAsync(
                x => x.DoctorId == doctorId
                    && x.SlotDate == slotDate
                    && x.StartTime == appointmentTime
                    && x.EndTime == endTime,
                cancellationToken);

        if (slot is not null)
        {
            slot.IsAvailable = true;
            return slot;
        }

        slot = new AvailabilitySlot
        {
            DoctorId = doctorId,
            SlotDate = slotDate,
            DayOfWeek = slotDate.DayOfWeek,
            StartTime = appointmentTime,
            EndTime = endTime,
            IsAvailable = true
        };

        _dbContext.AvailabilitySlots.Add(slot);
        return slot;
    }

    private IQueryable<AppointmentResponse> BuildAppointmentResponseQuery()
    {
        return _dbContext.Appointments
            .AsNoTracking()
            .Select(appointment => new AppointmentResponse
            {
                AppointmentId = appointment.AppointmentId,
                DoctorId = appointment.DoctorId,
                DoctorName = appointment.Doctor.User.Name,
                PatientId = appointment.PatientId,
                PatientName = appointment.Patient.User.Name,
                AvailabilitySlotId = appointment.AvailabilitySlotId,
                ClinicId = appointment.Doctor.User.ClinicId,
                ClinicName = appointment.Doctor.User.Clinic != null ? appointment.Doctor.User.Clinic.Name : null,
                AppointmentDate = appointment.AppointmentDate,
                DayOfWeek = appointment.AvailabilitySlot.DayOfWeek,
                AppointmentTime = appointment.AppointmentTime,
                Notes = appointment.Notes,
                Status = appointment.IsActive ? "Scheduled" : "Cancelled",
                IsActive = appointment.IsActive
            });
    }

    private async Task<AppointmentResponse?> GetAppointmentResponseAsync(
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        return await BuildAppointmentResponseQuery()
            .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId, cancellationToken);
    }

    private static void ValidateDateRange(DateOnly? fromDate, DateOnly? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            throw new ArgumentException("fromDate must be before or equal to toDate.");
        }
    }

    private static IQueryable<AppointmentResponse> ApplyDateRangeFilter(
        IQueryable<AppointmentResponse> query,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        if (fromDate.HasValue)
        {
            query = query.Where(appointment => appointment.AppointmentDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(appointment => appointment.AppointmentDate <= toDate.Value);
        }

        return query;
    }

    private static IQueryable<AppointmentResponse> OrderAppointments(IQueryable<AppointmentResponse> query)
    {
        return query
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.AppointmentTime);
    }

    private async Task<bool> CanAccessAppointmentAsync(
        AppointmentResponse appointment,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
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

        if (user.IsInRole(nameof(UserSystemRole.Doctor)))
        {
            return appointment.DoctorId == currentUserId.Value;
        }

        if (user.IsInRole(nameof(UserSystemRole.Patient)))
        {
            return appointment.PatientId == currentUserId.Value;
        }

        if (user.IsInRole(nameof(UserSystemRole.Secretary)) && appointment.ClinicId.HasValue)
        {
            var secretaryClinicId = await GetCurrentSecretaryClinicIdAsync(user, cancellationToken);
            return secretaryClinicId.HasValue && secretaryClinicId.Value == appointment.ClinicId.Value;
        }

        return false;
    }

    private async Task<bool> CanCurrentUserManageClinicAsync(
        Guid clinicId,
        Guid patientId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
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

        if (user.IsInRole(nameof(UserSystemRole.Patient)))
        {
            return currentUserId.Value == patientId;
        }

        if (!user.IsInRole(nameof(UserSystemRole.Secretary)))
        {
            return false;
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

}
