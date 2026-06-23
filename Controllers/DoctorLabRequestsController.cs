using HSCSAPI.DTOs.Laboratory;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Laboratory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserSystemRole.Doctor))]
[Route("api/Doctors/me/lab-requests")]
public class DoctorLabRequestsController : ControllerBase
{
    private readonly ILabTestRequestsService _labTestRequestsService;

    public DoctorLabRequestsController(ILabTestRequestsService labTestRequestsService)
    {
        _labTestRequestsService = labTestRequestsService;
    }

    [HttpGet]
    public async Task<ActionResult<LabTestRequestsResponse>> GetMyLabRequests(
        [FromQuery] string? status,
        [FromQuery] string? patientId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _labTestRequestsService.GetMyRequestsAsync(
            status,
            patientId,
            page,
            pageSize,
            User,
            cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<LabTestRequestResponse>> CreateMyLabRequest(
        [FromBody] CreateLabTestRequestRequest request,
        CancellationToken cancellationToken)
    {
        return await _labTestRequestsService.CreateMyRequestAsync(request, User, cancellationToken);
    }

    [HttpGet("{labTestRequestId:guid}")]
    public async Task<ActionResult<LabTestRequestResponse>> GetMyLabRequest(
        Guid labTestRequestId,
        CancellationToken cancellationToken)
    {
        return await _labTestRequestsService.GetMyRequestAsync(labTestRequestId, User, cancellationToken);
    }

    [HttpGet("{labTestRequestId:guid}/result-file")]
    public async Task<IActionResult> DownloadMyLabResultFile(
        Guid labTestRequestId,
        CancellationToken cancellationToken)
    {
        return await _labTestRequestsService.DownloadMyResultFileAsync(labTestRequestId, User, cancellationToken);
    }
}
