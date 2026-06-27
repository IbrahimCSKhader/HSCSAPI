using System.Security.Claims;
using HSCSAPI.DTOs.RadiologyTechnologist;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.RadiologyTechnologists;

public interface IRadiologyTechnologistsService
{
    Task<ActionResult<List<RadiologyTechnologistResponse>>> GetAllAsync(
        Guid? clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<List<RadiologyTechnologistResponse>>> GetByClinicAsync(
        Guid clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<RadiologyTechnologistResponse>> GetByIdAsync(
        Guid radiologyTechnologistId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<RadiologyTechnologistResponse>> GetMyProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<RadiologyTechnologistResponse>> UpdateAsync(
        Guid radiologyTechnologistId,
        UpdateRadiologyTechnologistRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<RadiologyTechnologistResponse>> UpdateMyProfileAsync(
        UpdateMyRadiologyTechnologistProfileRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> DeactivateAsync(
        Guid radiologyTechnologistId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> ActivateAsync(
        Guid radiologyTechnologistId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
