using System.Security.Claims;
using HSCSAPI.DTOs.Clinic;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.Clinics;

public interface IClinicsService
{
    Task<ActionResult<List<ClinicResponse>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ActionResult<ClinicResponse>> CreateAsync(CreateClinicRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<ClinicResponse>> UpdateAsync(Guid clinicId, UpdateClinicRequest request, CancellationToken cancellationToken = default);
    Task<IActionResult> DeleteAsync(Guid clinicId, CancellationToken cancellationToken = default);
    Task<ActionResult<ClinicResponse>> UpdateMyClinicAsync(UpdateMyClinicRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
