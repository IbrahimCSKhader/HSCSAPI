using HSCSAPI.DTOs.Radiology;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Radiology;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserSystemRole.RadiologyTechnologist))]
[Route("api/ImagingTests")]
public class ImagingTestsController : ControllerBase
{
    private readonly IImagingRequestsService _service;
    public ImagingTestsController(IImagingRequestsService service) => _service = service;

    // last end point added
    [HttpGet("my-requests")]
    public Task<ActionResult<ImagingRequestsResponse>> GetMyRequests(
        [FromQuery] string? status, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken) =>
        _service.GetTechnologistRequestsAsync(status, page, pageSize, User, cancellationToken);

    // last end point added
    [HttpGet("my-requests/{imagingTestRequestId:guid}")]
    public Task<ActionResult<ImagingRequestResponse>> GetMyRequest(Guid imagingTestRequestId, CancellationToken cancellationToken) =>
        _service.GetTechnologistRequestAsync(imagingTestRequestId, User, cancellationToken);

    // last end point added
    [HttpPost("my-requests/{imagingTestRequestId:guid}/results")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public Task<ActionResult<ImagingRequestResponse>> UploadResult(
        Guid imagingTestRequestId, [FromForm] UploadImagingResultRequest request, CancellationToken cancellationToken) =>
        _service.UploadTechnologistResultAsync(imagingTestRequestId, request, User, cancellationToken);

    // last end point added
    [HttpGet("my-requests/{imagingTestRequestId:guid}/result-file")]
    public Task<IActionResult> DownloadResultFile(Guid imagingTestRequestId, CancellationToken cancellationToken) =>
        _service.DownloadTechnologistResultFileAsync(imagingTestRequestId, User, cancellationToken);
}
