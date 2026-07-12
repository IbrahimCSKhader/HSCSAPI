using HSCSAPI.DTOs.Secretary;
using HSCSAPI.DTOs.Doctor;
using HSCSAPI.DTOs.Patient;
using HSCSAPI.DTOs.LaboratoryTechnologist;
using HSCSAPI.DTOs.RadiologyTechnologist;
using HSCSAPI.DTOs.Appointment;
using HSCSAPI.DTOs.Common;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Doctors;
using HSCSAPI.Services.Patients;
using HSCSAPI.Services.LaboratoryTechnologists;
using HSCSAPI.Services.RadiologyTechnologists;
using HSCSAPI.Services.Secretaries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SecretariesController : ControllerBase
{
    private readonly ISecretariesService _secretariesService;
    private readonly IDoctorsService _doctorsService;
    private readonly IPatientsService _patientsService;
    private readonly ILaboratoryTechnologistsService _laboratoryTechnologistsService;
    private readonly IRadiologyTechnologistsService _radiologyTechnologistsService;

    public SecretariesController(
        ISecretariesService secretariesService,
        IDoctorsService doctorsService,
        IPatientsService patientsService,
        ILaboratoryTechnologistsService laboratoryTechnologistsService,
        IRadiologyTechnologistsService radiologyTechnologistsService)
    {
        _secretariesService = secretariesService;
        _doctorsService = doctorsService;
        _patientsService = patientsService;
        _laboratoryTechnologistsService = laboratoryTechnologistsService;
        _radiologyTechnologistsService = radiologyTechnologistsService;
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<SecretaryDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        return await _secretariesService.GetDashboardAsync(User, cancellationToken);
    }

    // last end point added
    [HttpGet("me")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<SecretaryResponse>> GetMyProfile(CancellationToken cancellationToken)
    {
        return await _secretariesService.GetMyProfileAsync(User, cancellationToken);
    }

    // last end point added
    [HttpPut("me")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<SecretaryResponse>> UpdateMyProfile(
        [FromBody] UpdateSecretaryRequest request,
        CancellationToken cancellationToken)
    {
        return await _secretariesService.UpdateMyProfileAsync(request, User, cancellationToken);
    }

    // last end point added
    [HttpPut("me/password")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<ChangePasswordResponse>> ChangeMyPassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        return await _secretariesService.ChangeMyPasswordAsync(request, User, cancellationToken);
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserSystemRole.SuperAdmin))]
    public async Task<ActionResult<List<SecretaryResponse>>> GetAll([FromQuery] Guid? clinicId, CancellationToken cancellationToken)
    {
        return await _secretariesService.GetAllAsync(clinicId, cancellationToken);
    }

    [HttpGet("available")]
    [Authorize(Roles = nameof(UserSystemRole.SuperAdmin))]
    public async Task<ActionResult<List<SecretaryResponse>>> GetAvailable(CancellationToken cancellationToken)
    {
        return await _secretariesService.GetAvailableAsync(cancellationToken);
    }

    [HttpGet("clinic/{clinicId:guid}")]
    [Authorize(Roles = SecretariesService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<List<SecretaryResponse>>> GetByClinic(Guid clinicId, CancellationToken cancellationToken)
    {
        return await _secretariesService.GetByClinicAsync(clinicId, User, cancellationToken);
    }

    [HttpGet("my-clinic")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<List<SecretaryResponse>>> GetMyClinicSecretaries(CancellationToken cancellationToken)
    {
        return await _secretariesService.GetMyClinicSecretariesAsync(User, cancellationToken);
    }

    [HttpPut("{secretaryId:guid}/assign-clinic")]
    [Authorize(Roles = SecretariesService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<SecretaryResponse>> AssignToClinic(
        Guid secretaryId,
        [FromBody] AssignSecretaryToClinicRequest request,
        CancellationToken cancellationToken)
    {
        return await _secretariesService.AssignToClinicAsync(secretaryId, request, User, cancellationToken);
    }

    [HttpPatch("{secretaryId:guid}/remove-from-clinic")]
    [Authorize(Roles = SecretariesService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<SecretaryResponse>> RemoveFromClinic(Guid secretaryId, CancellationToken cancellationToken)
    {
        return await _secretariesService.RemoveFromClinicAsync(secretaryId, User, cancellationToken);
    }

    [HttpGet("my-clinic/patients")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<List<PatientResponse>>> GetMyClinicPatients(CancellationToken cancellationToken)
    {
        return await _patientsService.GetAllAsync(null, User, cancellationToken);
    }

    [HttpPut("my-clinic/patients/{patientId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<PatientResponse>> UpdateMyClinicPatient(
        Guid patientId,
        [FromBody] UpdatePatientRequest request,
        CancellationToken cancellationToken)
    {
        return await _patientsService.UpdateAsync(patientId, request, User, cancellationToken);
    }

    [HttpPatch("my-clinic/patients/{patientId:guid}/deactivate")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<IActionResult> DeactivateMyClinicPatient(Guid patientId, CancellationToken cancellationToken)
    {
        return await _patientsService.DeactivateAsync(patientId, User, cancellationToken);
    }

    [HttpPatch("my-clinic/patients/{patientId:guid}/activate")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<IActionResult> ActivateMyClinicPatient(Guid patientId, CancellationToken cancellationToken)
    {
        return await _patientsService.ActivateAsync(patientId, User, cancellationToken);
    }

    [HttpGet("my-clinic/doctors")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<List<DoctorResponse>>> GetMyClinicDoctors(CancellationToken cancellationToken)
    {
        return await _doctorsService.GetAllAsync(null, User, cancellationToken);
    }

    [HttpPut("my-clinic/doctors/{doctorId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<DoctorResponse>> UpdateMyClinicDoctor(
        Guid doctorId,
        [FromBody] UpdateDoctorRequest request,
        CancellationToken cancellationToken)
    {
        return await _doctorsService.UpdateAsync(doctorId, request, User, cancellationToken);
    }

    [HttpPatch("my-clinic/doctors/{doctorId:guid}/deactivate")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<IActionResult> DeactivateMyClinicDoctor(Guid doctorId, CancellationToken cancellationToken)
    {
        return await _doctorsService.DeactivateAsync(doctorId, User, cancellationToken);
    }

    [HttpPatch("my-clinic/doctors/{doctorId:guid}/activate")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<IActionResult> ActivateMyClinicDoctor(Guid doctorId, CancellationToken cancellationToken)
    {
        return await _doctorsService.ActivateAsync(doctorId, User, cancellationToken);
    }

    [HttpGet("my-clinic/laboratory-technologists")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<List<LaboratoryTechnologistResponse>>> GetMyClinicLaboratoryTechnologists(CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.GetAllAsync(null, User, cancellationToken);
    }

    [HttpPut("my-clinic/laboratory-technologists/{laboratoryTechnologistId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<LaboratoryTechnologistResponse>> UpdateMyClinicLaboratoryTechnologist(
        Guid laboratoryTechnologistId,
        [FromBody] UpdateLaboratoryTechnologistRequest request,
        CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.UpdateAsync(laboratoryTechnologistId, request, User, cancellationToken);
    }

    [HttpPatch("my-clinic/laboratory-technologists/{laboratoryTechnologistId:guid}/deactivate")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<IActionResult> DeactivateMyClinicLaboratoryTechnologist(Guid laboratoryTechnologistId, CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.DeactivateAsync(laboratoryTechnologistId, User, cancellationToken);
    }

    [HttpPatch("my-clinic/laboratory-technologists/{laboratoryTechnologistId:guid}/activate")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<IActionResult> ActivateMyClinicLaboratoryTechnologist(Guid laboratoryTechnologistId, CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.ActivateAsync(laboratoryTechnologistId, User, cancellationToken);
    }

    [HttpGet("my-clinic/radiology-technologists")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<List<RadiologyTechnologistResponse>>> GetMyClinicRadiologyTechnologists(CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.GetAllAsync(null, User, cancellationToken);
    }

    [HttpPut("my-clinic/radiology-technologists/{radiologyTechnologistId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<RadiologyTechnologistResponse>> UpdateMyClinicRadiologyTechnologist(
        Guid radiologyTechnologistId,
        [FromBody] UpdateRadiologyTechnologistRequest request,
        CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.UpdateAsync(radiologyTechnologistId, request, User, cancellationToken);
    }

    [HttpPatch("my-clinic/radiology-technologists/{radiologyTechnologistId:guid}/deactivate")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<IActionResult> DeactivateMyClinicRadiologyTechnologist(Guid radiologyTechnologistId, CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.DeactivateAsync(radiologyTechnologistId, User, cancellationToken);
    }

    [HttpPatch("my-clinic/radiology-technologists/{radiologyTechnologistId:guid}/activate")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<IActionResult> ActivateMyClinicRadiologyTechnologist(Guid radiologyTechnologistId, CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.ActivateAsync(radiologyTechnologistId, User, cancellationToken);
    }

    // last end point added
    [HttpGet("my-clinic/doctors/{doctorId:guid}/availability-slots")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<List<AvailabilitySlotResponse>>> GetDoctorAvailabilitySlots(
        Guid doctorId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken cancellationToken)
    {
        return await _secretariesService.GetDoctorAvailabilitySlotsAsync(doctorId, fromDate, toDate, User, cancellationToken);
    }

    // last end point added
    [HttpPost("my-clinic/doctors/{doctorId:guid}/availability-slots")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<AvailabilitySlotResponse>> CreateDoctorAvailabilitySlot(
        Guid doctorId, [FromBody] CreateAvailabilitySlotRequest request, CancellationToken cancellationToken)
    {
        return await _secretariesService.CreateDoctorAvailabilitySlotAsync(doctorId, request, User, cancellationToken);
    }

    // last end point added
    [HttpDelete("my-clinic/doctors/{doctorId:guid}/availability-slots/{slotId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<IActionResult> DeleteDoctorAvailabilitySlot(Guid doctorId, Guid slotId, CancellationToken cancellationToken)
    {
        return await _secretariesService.DeleteDoctorAvailabilitySlotAsync(doctorId, slotId, User, cancellationToken);
    }

    // last end point added
    [HttpGet("my-clinic/reports")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<List<SecretaryReportResponse>>> GetReports(CancellationToken cancellationToken)
    {
        return await _secretariesService.GetReportsAsync(User, cancellationToken);
    }

    // last end point added
    [HttpGet("my-clinic/reports/options")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<SecretaryReportOptionsResponse>> GetReportOptions(CancellationToken cancellationToken)
    {
        return await _secretariesService.GetReportOptionsAsync(cancellationToken);
    }

    // last end point added
    [HttpPost("my-clinic/reports")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<SecretaryReportResponse>> GenerateReport(
        [FromBody] GenerateSecretaryReportRequest request, CancellationToken cancellationToken)
    {
        return await _secretariesService.GenerateReportAsync(request, User, cancellationToken);
    }

    // last end point added
    [HttpDelete("my-clinic/reports/{reportId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<IActionResult> DeleteReport(Guid reportId, CancellationToken cancellationToken)
    {
        return await _secretariesService.DeleteReportAsync(reportId, User, cancellationToken);
    }

    // last end point added
    [HttpGet("my-clinic/reports/{reportId:guid}/files/{fileId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<IActionResult> DownloadReport(Guid reportId, Guid fileId, CancellationToken cancellationToken)
    {
        var file = await _secretariesService.DownloadReportAsync(reportId, fileId, User, cancellationToken);
        return PhysicalFile(file.PhysicalPath, file.ContentType, file.FileName, enableRangeProcessing: true);
    }
}
