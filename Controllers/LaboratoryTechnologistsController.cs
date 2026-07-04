using HSCSAPI.DTOs.LaboratoryTechnologist;
using HSCSAPI.DTOs.Common;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.LaboratoryTechnologists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LaboratoryTechnologistsController : ControllerBase
{
    private readonly ILaboratoryTechnologistsService _laboratoryTechnologistsService;

    public LaboratoryTechnologistsController(ILaboratoryTechnologistsService laboratoryTechnologistsService)
    {
        _laboratoryTechnologistsService = laboratoryTechnologistsService;
    }

    [HttpGet]
    [Authorize(Roles = LaboratoryTechnologistsService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<List<LaboratoryTechnologistResponse>>> GetAll([FromQuery] Guid? clinicId, CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.GetAllAsync(clinicId, User, cancellationToken);
    }

    [HttpGet("clinic/{clinicId:guid}")]
    [Authorize(Roles = LaboratoryTechnologistsService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<List<LaboratoryTechnologistResponse>>> GetByClinic(Guid clinicId, CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.GetByClinicAsync(clinicId, User, cancellationToken);
    }

    [HttpGet("me")]
    [Authorize(Roles = nameof(UserSystemRole.LaboratoryTechnologist))]
    public async Task<ActionResult<LaboratoryTechnologistResponse>> GetMyProfile(CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.GetMyProfileAsync(User, cancellationToken);
    }

    [HttpGet("{laboratoryTechnologistId:guid}")]
    [Authorize(Roles = LaboratoryTechnologistsService.SuperAdminOrSecretaryOrLaboratoryTechnologistRoles)]
    public async Task<ActionResult<LaboratoryTechnologistResponse>> GetById(Guid laboratoryTechnologistId, CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.GetByIdAsync(laboratoryTechnologistId, User, cancellationToken);
    }

    [HttpPut("{laboratoryTechnologistId:guid}")]
    [Authorize(Roles = LaboratoryTechnologistsService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<LaboratoryTechnologistResponse>> Update(
        Guid laboratoryTechnologistId,
        [FromBody] UpdateLaboratoryTechnologistRequest request,
        CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.UpdateAsync(laboratoryTechnologistId, request, User, cancellationToken);
    }

    [HttpPut("me")]
    [Authorize(Roles = nameof(UserSystemRole.LaboratoryTechnologist))]
    public async Task<ActionResult<LaboratoryTechnologistResponse>> UpdateMyProfile(
        [FromBody] UpdateMyLaboratoryTechnologistProfileRequest request,
        CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.UpdateMyProfileAsync(request, User, cancellationToken);
    }

    // last end point added
    [HttpPut("me/password")]
    [Authorize(Roles = nameof(UserSystemRole.LaboratoryTechnologist))]
    public async Task<ActionResult<ChangePasswordResponse>> ChangeMyPassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.ChangeMyPasswordAsync(request, User, cancellationToken);
    }

    [HttpPatch("{laboratoryTechnologistId:guid}/deactivate")]
    [Authorize(Roles = LaboratoryTechnologistsService.SuperAdminOrSecretaryRoles)]
    public async Task<IActionResult> Deactivate(Guid laboratoryTechnologistId, CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.DeactivateAsync(laboratoryTechnologistId, User, cancellationToken);
    }

    [HttpPatch("{laboratoryTechnologistId:guid}/activate")]
    [Authorize(Roles = LaboratoryTechnologistsService.SuperAdminOrSecretaryRoles)]
    public async Task<IActionResult> Activate(Guid laboratoryTechnologistId, CancellationToken cancellationToken)
    {
        return await _laboratoryTechnologistsService.ActivateAsync(laboratoryTechnologistId, User, cancellationToken);
    }
}
