using System.Security.Claims;
using HSCSAPI.DTOs.Radiology;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.Radiology;

public interface IImagingRequestsService
{
    Task<ActionResult<ImagingRequestsResponse>> GetMyRequestsAsync(
        string? status,
        string? patientId,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ImagingRequestResponse>> GetMyRequestAsync(
        Guid imagingTestRequestId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ImagingRequestResponse>> CreateMyRequestAsync(
        CreateImagingRequestRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> DownloadMyResultFileAsync(
        Guid imagingTestRequestId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
