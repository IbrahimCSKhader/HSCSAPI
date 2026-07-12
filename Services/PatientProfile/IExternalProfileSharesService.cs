using System.Security.Claims;
using HSCSAPI.DTOs.PatientProfile;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.PatientProfile;

public interface IExternalProfileSharesService
{
    Task<ActionResult<ExternalProfileShareResponse>> CreateShareAsync(
        CreateExternalProfileShareRequest request,
        ClaimsPrincipal user,
        string publicBaseUrl,
        CancellationToken cancellationToken = default);

    Task<ActionResult<List<ExternalProfileShareResponse>>> GetMySharesAsync(
        ClaimsPrincipal user,
        string publicBaseUrl,
        CancellationToken cancellationToken = default);

    Task<IActionResult> DeactivateShareAsync(
        Guid shareId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ExternalShareStatusResponse>> GetStatusAsync(
        string shareToken,
        CancellationToken cancellationToken = default);

    Task<IActionResult> SendVerificationCodeAsync(
        string shareToken,
        ExternalShareEmailRequest request,
        string publicBaseUrl,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ExternalShareVerifyResponse>> VerifyEmailAsync(
        string shareToken,
        ExternalShareVerifyRequest request,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ExternalSharedPatientProfileResponse>> GetSharedProfileAsync(
        string shareToken,
        string? accessToken,
        CancellationToken cancellationToken = default);

    Task<ExternalSharedFileView> GetFileForViewingAsync(
        string shareToken,
        Guid medicalFileId,
        string? accessToken,
        CancellationToken cancellationToken = default);
}

public sealed record ExternalSharedFileView(
    string PhysicalPath,
    string ContentType,
    string FileName);
