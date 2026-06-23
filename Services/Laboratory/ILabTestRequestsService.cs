using System.Security.Claims;
using HSCSAPI.DTOs.Laboratory;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.Laboratory;

public interface ILabTestRequestsService
{
    Task<ActionResult<LabTestRequestsResponse>> GetMyRequestsAsync(
        string? status,
        string? patientId,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<LabTestRequestResponse>> GetMyRequestAsync(
        Guid labTestRequestId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<LabTestRequestResponse>> CreateMyRequestAsync(
        CreateLabTestRequestRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> DownloadMyResultFileAsync(
        Guid labTestRequestId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
