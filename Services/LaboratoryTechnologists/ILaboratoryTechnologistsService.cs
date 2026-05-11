using System.Security.Claims;
using HSCSAPI.DTOs.LaboratoryTechnologist;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.LaboratoryTechnologists;

public interface ILaboratoryTechnologistsService
{
    Task<ActionResult<List<LaboratoryTechnologistResponse>>> GetAllAsync(
        Guid? clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<List<LaboratoryTechnologistResponse>>> GetByClinicAsync(
        Guid clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<LaboratoryTechnologistResponse>> GetByIdAsync(
        Guid laboratoryTechnologistId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<LaboratoryTechnologistResponse>> GetMyProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<LaboratoryTechnologistResponse>> UpdateAsync(
        Guid laboratoryTechnologistId,
        UpdateLaboratoryTechnologistRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<LaboratoryTechnologistResponse>> UpdateMyProfileAsync(
        UpdateMyLaboratoryTechnologistProfileRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> DeleteAsync(
        Guid laboratoryTechnologistId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
