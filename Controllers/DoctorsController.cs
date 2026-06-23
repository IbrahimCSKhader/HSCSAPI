using HSCSAPI.DTOs.Doctor;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Doctors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorsController : ControllerBase
{
    private readonly IDoctorsService _doctorsService;

    public DoctorsController(IDoctorsService doctorsService)
    {
        _doctorsService = doctorsService;
    }

    [HttpGet]
    [Authorize(Roles = DoctorsService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<List<DoctorResponse>>> GetAll([FromQuery] Guid? clinicId, CancellationToken cancellationToken)
    {
        return await _doctorsService.GetAllAsync(clinicId, User, cancellationToken);
    }

    [HttpGet("clinic/{clinicId:guid}")]
    [Authorize(Roles = DoctorsService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<List<DoctorResponse>>> GetByClinic(Guid clinicId, CancellationToken cancellationToken)
    {
        return await _doctorsService.GetByClinicAsync(clinicId, User, cancellationToken);
    }

    [HttpGet("me")]
    [Authorize(Roles = nameof(UserSystemRole.Doctor))]
    public async Task<ActionResult<DoctorResponse>> GetMyProfile(CancellationToken cancellationToken)
    {
        return await _doctorsService.GetMyProfileAsync(User, cancellationToken);
    }

    [HttpGet("me/dashboard")]
    [Authorize(Roles = nameof(UserSystemRole.Doctor))]
    public async Task<ActionResult<DoctorDashboardResponse>> GetMyDashboard(CancellationToken cancellationToken)
    {
        return await _doctorsService.GetMyDashboardAsync(User, cancellationToken);
    }

    [HttpGet("me/appointments")]
    [Authorize(Roles = nameof(UserSystemRole.Doctor))]
    public async Task<ActionResult<DoctorAppointmentsScheduleResponse>> GetMyAppointments(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        return await _doctorsService.GetMyAppointmentsScheduleAsync(fromDate, toDate, User, cancellationToken);
    }

    [HttpGet("me/appointments/{appointmentId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.Doctor))]
    public async Task<ActionResult<DoctorAppointmentDetailResponse>> GetMyAppointmentDetail(
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        return await _doctorsService.GetMyAppointmentDetailAsync(appointmentId, User, cancellationToken);
    }

    [HttpGet("me/medical-records")]
    [Authorize(Roles = nameof(UserSystemRole.Doctor))]
    public async Task<ActionResult<DoctorMedicalRecordsResponse>> GetMyMedicalRecords(
        [FromQuery] string? patientId,
        [FromQuery] Guid? clinicId,
        [FromQuery] string? type,
        [FromQuery] string? query,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _doctorsService.GetMyMedicalRecordsAsync(
            patientId,
            clinicId,
            type,
            query,
            page,
            pageSize,
            User,
            cancellationToken);
    }

    [HttpGet("me/medical-records/{medicalFileId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.Doctor))]
    public async Task<ActionResult<DoctorMedicalRecordDetailResponse>> GetMyMedicalRecord(
        Guid medicalFileId,
        CancellationToken cancellationToken)
    {
        return await _doctorsService.GetMyMedicalRecordAsync(medicalFileId, User, cancellationToken);
    }

    [HttpGet("me/medical-records/{medicalFileId:guid}/download")]
    [Authorize(Roles = nameof(UserSystemRole.Doctor))]
    public async Task<IActionResult> DownloadMyMedicalRecord(
        Guid medicalFileId,
        CancellationToken cancellationToken)
    {
        return await _doctorsService.DownloadMyMedicalRecordAsync(medicalFileId, User, cancellationToken);
    }

    [HttpGet("{doctorId:guid}")]
    [Authorize(Roles = DoctorsService.SuperAdminOrSecretaryOrDoctorRoles)]
    public async Task<ActionResult<DoctorResponse>> GetById(Guid doctorId, CancellationToken cancellationToken)
    {
        return await _doctorsService.GetByIdAsync(doctorId, User, cancellationToken);
    }

    [HttpPut("{doctorId:guid}")]
    [Authorize(Roles = DoctorsService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<DoctorResponse>> Update(
        Guid doctorId,
        [FromBody] UpdateDoctorRequest request,
        CancellationToken cancellationToken)
    {
        return await _doctorsService.UpdateAsync(doctorId, request, User, cancellationToken);
    }

    [HttpPut("me")]
    [Authorize(Roles = nameof(UserSystemRole.Doctor))]
    public async Task<ActionResult<DoctorResponse>> UpdateMyProfile(
        [FromBody] UpdateMyDoctorProfileRequest request,
        CancellationToken cancellationToken)
    {
        return await _doctorsService.UpdateMyProfileAsync(request, User, cancellationToken);
    }

    [HttpPut("me/password")]
    [Authorize(Roles = nameof(UserSystemRole.Doctor))]
    public async Task<ActionResult<ChangeDoctorPasswordResponse>> ChangeMyPassword(
        [FromBody] ChangeDoctorPasswordRequest request,
        CancellationToken cancellationToken)
    {
        return await _doctorsService.ChangeMyPasswordAsync(request, User, cancellationToken);
    }

    [HttpDelete("{doctorId:guid}")]
    [Authorize(Roles = DoctorsService.SuperAdminOrSecretaryRoles)]
    public async Task<IActionResult> Delete(Guid doctorId, CancellationToken cancellationToken)
    {
        return await _doctorsService.DeleteAsync(doctorId, User, cancellationToken);
    }
}
