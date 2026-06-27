using System.Security.Claims;
using HSCSAPI.DTOs.Appointment;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.Appointments;

public interface IAppointmentsService
{
    Task<ActionResult<List<AppointmentResponse>>> GetAllAsync(
        Guid? clinicId,
        DateOnly? fromDate,
        DateOnly? toDate,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<List<AppointmentResponse>>> GetMineAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<AppointmentResponse>> GetByIdAsync(
        Guid appointmentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<AppointmentResponse>> CreateAsync(
        CreateAppointmentRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<AppointmentResponse>> UpdateAsync(
        Guid appointmentId,
        UpdateAppointmentRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> DeactivateAsync(
        Guid appointmentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> ActivateAsync(
        Guid appointmentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
