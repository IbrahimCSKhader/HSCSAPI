using HSCSAPI.DTOs.Patient;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Patients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IPatientsService _patientsService;

    public PatientsController(IPatientsService patientsService)
    {
        _patientsService = patientsService;
    }

    [HttpGet]
    [Authorize(Roles = PatientsService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<List<PatientResponse>>> GetAll([FromQuery] Guid? clinicId, CancellationToken cancellationToken)
    {
        return await _patientsService.GetAllAsync(clinicId, User, cancellationToken);
    }

    [HttpGet("clinic/{clinicId:guid}")]
    [Authorize(Roles = PatientsService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<List<PatientResponse>>> GetByClinic(Guid clinicId, CancellationToken cancellationToken)
    {
        return await _patientsService.GetByClinicAsync(clinicId, User, cancellationToken);
    }

    [HttpGet("me")]
    [Authorize(Roles = nameof(UserSystemRole.Patient))]
    public async Task<ActionResult<PatientResponse>> GetMyProfile(CancellationToken cancellationToken)
    {
        return await _patientsService.GetMyProfileAsync(User, cancellationToken);
    }

    [HttpGet("{patientId:guid}")]
    [Authorize(Roles = PatientsService.SuperAdminOrSecretaryOrPatientRoles)]
    public async Task<ActionResult<PatientResponse>> GetById(Guid patientId, CancellationToken cancellationToken)
    {
        return await _patientsService.GetByIdAsync(patientId, User, cancellationToken);
    }

    [HttpPut("{patientId:guid}")]
    [Authorize(Roles = PatientsService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<PatientResponse>> Update(
        Guid patientId,
        [FromBody] UpdatePatientRequest request,
        CancellationToken cancellationToken)
    {
        return await _patientsService.UpdateAsync(patientId, request, User, cancellationToken);
    }

    [HttpPut("me")]
    [Authorize(Roles = nameof(UserSystemRole.Patient))]
    public async Task<ActionResult<PatientResponse>> UpdateMyProfile(
        [FromBody] UpdateMyPatientProfileRequest request,
        CancellationToken cancellationToken)
    {
        return await _patientsService.UpdateMyProfileAsync(request, User, cancellationToken);
    }

    [HttpDelete("{patientId:guid}")]
    [Authorize(Roles = PatientsService.SuperAdminOrSecretaryRoles)]
    public async Task<IActionResult> Delete(Guid patientId, CancellationToken cancellationToken)
    {
        return await _patientsService.DeleteAsync(patientId, User, cancellationToken);
    }
}
