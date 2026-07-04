using HSCSAPI.DTOs.RadiologyTechnologist;
using HSCSAPI.DTOs.Common;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.RadiologyTechnologists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RadiologyTechnologistsController : ControllerBase
{
    private readonly IRadiologyTechnologistsService _radiologyTechnologistsService;

    public RadiologyTechnologistsController(IRadiologyTechnologistsService radiologyTechnologistsService)
    {
        _radiologyTechnologistsService = radiologyTechnologistsService;
    }

    [HttpGet]
    [Authorize(Roles = RadiologyTechnologistsService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<List<RadiologyTechnologistResponse>>> GetAll([FromQuery] Guid? clinicId, CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.GetAllAsync(clinicId, User, cancellationToken);
    }

    [HttpGet("clinic/{clinicId:guid}")]
    [Authorize(Roles = RadiologyTechnologistsService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<List<RadiologyTechnologistResponse>>> GetByClinic(Guid clinicId, CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.GetByClinicAsync(clinicId, User, cancellationToken);
    }

    // last end point added - already-added
    [HttpGet("me")]
    [Authorize(Roles = nameof(UserSystemRole.RadiologyTechnologist))]
    public async Task<ActionResult<RadiologyTechnologistResponse>> GetMyProfile(CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.GetMyProfileAsync(User, cancellationToken);
    }

    [HttpGet("{radiologyTechnologistId:guid}")]
    [Authorize(Roles = RadiologyTechnologistsService.SuperAdminOrSecretaryOrRadiologyTechnologistRoles)]
    public async Task<ActionResult<RadiologyTechnologistResponse>> GetById(Guid radiologyTechnologistId, CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.GetByIdAsync(radiologyTechnologistId, User, cancellationToken);
    }

    [HttpPut("{radiologyTechnologistId:guid}")]
    [Authorize(Roles = RadiologyTechnologistsService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<RadiologyTechnologistResponse>> Update(
        Guid radiologyTechnologistId,
        [FromBody] UpdateRadiologyTechnologistRequest request,
        CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.UpdateAsync(radiologyTechnologistId, request, User, cancellationToken);
    }

    // last end point added - already-added
    [HttpPut("me")]
    [Authorize(Roles = nameof(UserSystemRole.RadiologyTechnologist))]
    public async Task<ActionResult<RadiologyTechnologistResponse>> UpdateMyProfile(
        [FromBody] UpdateMyRadiologyTechnologistProfileRequest request,
        CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.UpdateMyProfileAsync(request, User, cancellationToken);
    }

    // last end point added
    [HttpPut("me/password")]
    [Authorize(Roles = nameof(UserSystemRole.RadiologyTechnologist))]
    public async Task<ActionResult<ChangePasswordResponse>> ChangeMyPassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.ChangeMyPasswordAsync(request, User, cancellationToken);
    }

    [HttpPatch("{radiologyTechnologistId:guid}/deactivate")]
    [Authorize(Roles = RadiologyTechnologistsService.SuperAdminOrSecretaryRoles)]
    public async Task<IActionResult> Deactivate(Guid radiologyTechnologistId, CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.DeactivateAsync(radiologyTechnologistId, User, cancellationToken);
    }

    [HttpPatch("{radiologyTechnologistId:guid}/activate")]
    [Authorize(Roles = RadiologyTechnologistsService.SuperAdminOrSecretaryRoles)]
    public async Task<IActionResult> Activate(Guid radiologyTechnologistId, CancellationToken cancellationToken)
    {
        return await _radiologyTechnologistsService.ActivateAsync(radiologyTechnologistId, User, cancellationToken);
    }
}
