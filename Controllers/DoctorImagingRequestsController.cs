using HSCSAPI.DTOs.Radiology;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Radiology;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserSystemRole.Doctor))]
[Route("api/Doctors/me/imaging-requests")]
public class DoctorImagingRequestsController : ControllerBase
{
    private readonly IImagingRequestsService _imagingRequestsService;

    public DoctorImagingRequestsController(IImagingRequestsService imagingRequestsService)
    {
        _imagingRequestsService = imagingRequestsService;
    }

    [HttpGet]
    public async Task<ActionResult<ImagingRequestsResponse>> GetMyImagingRequests(
        [FromQuery] string? status,
        [FromQuery] string? patientId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _imagingRequestsService.GetMyRequestsAsync(
            status,
            patientId,
            page,
            pageSize,
            User,
            cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<ImagingRequestResponse>> CreateMyImagingRequest(
        [FromBody] CreateImagingRequestRequest request,
        CancellationToken cancellationToken)
    {
        return await _imagingRequestsService.CreateMyRequestAsync(request, User, cancellationToken);
    }

    [HttpGet("{imagingTestRequestId:guid}")]
    public async Task<ActionResult<ImagingRequestResponse>> GetMyImagingRequest(
        Guid imagingTestRequestId,
        CancellationToken cancellationToken)
    {
        return await _imagingRequestsService.GetMyRequestAsync(imagingTestRequestId, User, cancellationToken);
    }

    [HttpGet("{imagingTestRequestId:guid}/result-file")]
    public async Task<IActionResult> DownloadMyImagingResultFile(
        Guid imagingTestRequestId,
        CancellationToken cancellationToken)
    {
        return await _imagingRequestsService.DownloadMyResultFileAsync(imagingTestRequestId, User, cancellationToken);
    }
}
