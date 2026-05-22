using HSCSAPI.DTOs.Clinic;
using HSCSAPI.DTOs.Secretary;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Clinics;
using HSCSAPI.Services.Secretaries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClinicsController : ControllerBase
{
    private readonly IClinicsService _clinicsService;
    private readonly ISecretariesService _secretariesService;

    public ClinicsController(
        IClinicsService clinicsService,
        ISecretariesService secretariesService)
    {
        _clinicsService = clinicsService;
        _secretariesService = secretariesService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ClinicResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return await _clinicsService.GetAllAsync(cancellationToken);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserSystemRole.SuperAdmin))]
    public async Task<ActionResult<ClinicResponse>> Create([FromBody] CreateClinicRequest request, CancellationToken cancellationToken)
    {
        return await _clinicsService.CreateAsync(request, User, cancellationToken);
    }

    [HttpPut("{clinicId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.SuperAdmin))]
    public async Task<ActionResult<ClinicResponse>> Update(Guid clinicId, [FromBody] UpdateClinicRequest request, CancellationToken cancellationToken)
    {
        return await _clinicsService.UpdateAsync(clinicId, request, cancellationToken);
    }

    [HttpDelete("{clinicId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.SuperAdmin))]
    public async Task<IActionResult> Delete(Guid clinicId, CancellationToken cancellationToken)
    {
        return await _clinicsService.DeleteAsync(clinicId, cancellationToken);
    }

    [HttpPut("my-clinic")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<ClinicResponse>> UpdateMyClinic([FromBody] UpdateMyClinicRequest request, CancellationToken cancellationToken)
    {
        return await _clinicsService.UpdateMyClinicAsync(request, User, cancellationToken);
    }

    [HttpPut("{clinicId:guid}/secretaries/{secretaryId:guid}")]
    [Authorize(Roles = SecretariesService.SuperAdminOrSecretaryRoles)]
    public async Task<ActionResult<SecretaryResponse>> UpdateSecretaryAccount(
        Guid clinicId,
        Guid secretaryId,
        [FromBody] UpdateSecretaryRequest request,
        CancellationToken cancellationToken)
    {
        return await _secretariesService.UpdateInClinicAsync(clinicId, secretaryId, request, User, cancellationToken);
    }

    [HttpDelete("{clinicId:guid}/secretaries/{secretaryId:guid}")]
    [Authorize(Roles = SecretariesService.SuperAdminOrSecretaryRoles)]
    public async Task<IActionResult> DeleteSecretaryAccount(
        Guid clinicId,
        Guid secretaryId,
        CancellationToken cancellationToken)
    {
        return await _secretariesService.DeleteInClinicAsync(clinicId, secretaryId, User, cancellationToken);
    }
}
