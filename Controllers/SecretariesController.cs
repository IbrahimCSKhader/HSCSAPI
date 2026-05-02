using HSCSAPI.DTOs.Secretary;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Secretaries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SecretariesController : ControllerBase
{
    private readonly ISecretariesService _secretariesService;

    public SecretariesController(ISecretariesService secretariesService)
    {
        _secretariesService = secretariesService;
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

    [HttpDelete("{secretaryId:guid}/remove-from-clinic")]
    [Authorize(Roles = SecretariesService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<SecretaryResponse>> RemoveFromClinic(Guid secretaryId, CancellationToken cancellationToken)
    {
        return await _secretariesService.RemoveFromClinicAsync(secretaryId, User, cancellationToken);
    }
}
