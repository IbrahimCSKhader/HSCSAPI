using System.Security.Claims;
using HSCSAPI.DTOs.Patient;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.Patients;

public interface IPatientsService
{
    Task<ActionResult<List<PatientResponse>>> GetAllAsync(Guid? clinicId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<List<PatientResponse>>> GetByClinicAsync(Guid clinicId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<PatientResponse>> GetByIdAsync(Guid patientId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<PatientResponse>> GetMyProfileAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<PatientResponse>> UpdateAsync(Guid patientId, UpdatePatientRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<PatientResponse>> UpdateMyProfileAsync(UpdateMyPatientProfileRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<IActionResult> DeactivateAsync(Guid patientId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<IActionResult> ActivateAsync(Guid patientId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
