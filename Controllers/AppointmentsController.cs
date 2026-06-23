using HSCSAPI.DTOs.Appointment;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentsService _appointmentsService;

    public AppointmentsController(IAppointmentsService appointmentsService)
    {
        _appointmentsService = appointmentsService;
    }

    [HttpGet]
    [Authorize(Roles = AppointmentsService.AllowedReadRoles)]
    public async Task<ActionResult<List<AppointmentResponse>>> GetAll(
        [FromQuery] Guid? clinicId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        return await _appointmentsService.GetAllAsync(clinicId, fromDate, toDate, User, cancellationToken);
    }

    [HttpGet("me")]
    [Authorize(Roles = nameof(UserSystemRole.Patient) + "," + nameof(UserSystemRole.Doctor))]
    public async Task<ActionResult<List<AppointmentResponse>>> GetMine(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        return await _appointmentsService.GetMineAsync(fromDate, toDate, User, cancellationToken);
    }

    [HttpGet("{appointmentId:guid}")]
    [Authorize(Roles = AppointmentsService.AllowedReadRoles)]
    public async Task<ActionResult<AppointmentResponse>> GetById(Guid appointmentId, CancellationToken cancellationToken)
    {
        return await _appointmentsService.GetByIdAsync(appointmentId, User, cancellationToken);
    }

    [HttpPost]
    [Authorize(Roles = AppointmentsService.AllowedWriteRoles)]
    public async Task<ActionResult<AppointmentResponse>> Create(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        return await _appointmentsService.CreateAsync(request, User, cancellationToken);
    }

    [HttpPut("{appointmentId:guid}")]
    [Authorize(Roles = AppointmentsService.AllowedWriteRoles)]
    public async Task<ActionResult<AppointmentResponse>> Update(
        Guid appointmentId,
        [FromBody] UpdateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        return await _appointmentsService.UpdateAsync(appointmentId, request, User, cancellationToken);
    }

    [HttpDelete("{appointmentId:guid}")]
    [Authorize(Roles = AppointmentsService.AllowedWriteRoles)]
    public async Task<IActionResult> Delete(Guid appointmentId, CancellationToken cancellationToken)
    {
        return await _appointmentsService.DeleteAsync(appointmentId, User, cancellationToken);
    }
}
