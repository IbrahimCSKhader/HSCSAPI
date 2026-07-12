using HSCSAPI.DTOs.PatientProfile;
using HSCSAPI.Services.PatientProfile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[AllowAnonymous]
public class SharedPatientProfilesController : ControllerBase
{
    private readonly IExternalProfileSharesService _sharesService;

    public SharedPatientProfilesController(IExternalProfileSharesService sharesService)
    {
        _sharesService = sharesService;
    }

    // last end point added
    [HttpGet("shared-profiles/{shareToken}")]
    public ContentResult Page(string shareToken)
    {
        return Content(SharedPatientProfileHtml.Page(shareToken), "text/html");
    }

    // last end point added
    [HttpGet("api/shared-profiles/{shareToken}/status")]
    public async Task<ActionResult<ExternalShareStatusResponse>> Status(
        string shareToken,
        CancellationToken cancellationToken)
    {
        return await _sharesService.GetStatusAsync(shareToken, cancellationToken);
    }

    // last end point added
    [HttpPost("api/shared-profiles/{shareToken}/send-code")]
    public async Task<IActionResult> SendCode(
        string shareToken,
        [FromBody] ExternalShareEmailRequest request,
        CancellationToken cancellationToken)
    {
        return await _sharesService.SendVerificationCodeAsync(
            shareToken,
            request,
            BuildPublicBaseUrl(),
            cancellationToken);
    }

    // last end point added
    [HttpPost("api/shared-profiles/{shareToken}/verify")]
    public async Task<ActionResult<ExternalShareVerifyResponse>> Verify(
        string shareToken,
        [FromBody] ExternalShareVerifyRequest request,
        CancellationToken cancellationToken)
    {
        return await _sharesService.VerifyEmailAsync(shareToken, request, cancellationToken);
    }

    // last end point added
    [HttpGet("api/shared-profiles/{shareToken}/profile")]
    public async Task<ActionResult<ExternalSharedPatientProfileResponse>> Profile(
        string shareToken,
        [FromQuery] string? accessToken,
        CancellationToken cancellationToken)
    {
        accessToken ??= Request.Headers["X-Share-Access-Token"].FirstOrDefault();
        return await _sharesService.GetSharedProfileAsync(shareToken, accessToken, cancellationToken);
    }

    // last end point added
    [HttpGet("shared-profiles/{shareToken}/files/{medicalFileId:guid}/viewer")]
    public ContentResult FileViewer(
        string shareToken,
        Guid medicalFileId,
        [FromQuery] string accessToken)
    {
        return Content(SharedPatientProfileHtml.FileViewer(shareToken, medicalFileId, accessToken), "text/html");
    }

    // last end point added
    [HttpGet("shared-profiles/{shareToken}/files/{medicalFileId:guid}/content")]
    public async Task<IActionResult> FileContent(
        string shareToken,
        Guid medicalFileId,
        [FromQuery] string? accessToken,
        CancellationToken cancellationToken)
    {
        accessToken ??= Request.Headers["X-Share-Access-Token"].FirstOrDefault();
        var file = await _sharesService.GetFileForViewingAsync(shareToken, medicalFileId, accessToken, cancellationToken);
        Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(file.PhysicalPath, file.ContentType, enableRangeProcessing: true);
    }

    private string BuildPublicBaseUrl() => $"{Request.Scheme}://{Request.Host}";
}
