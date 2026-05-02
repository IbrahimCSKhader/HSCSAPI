using HSCSAPI.DTOs.Clinic;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Clinics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClinicsController : ControllerBase
{
    private readonly IClinicsService _clinicsService;

    public ClinicsController(IClinicsService clinicsService)
    {
        _clinicsService = clinicsService;
    }

    [HttpGet]
    // [Authorize(Roles = nameof(UserSystemRole.SuperAdmin))]
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
}
