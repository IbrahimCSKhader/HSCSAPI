using HSCSAPI.DTOs.PatientProfile;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.PatientProfile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/patient-profile")]
[Authorize(Roles = nameof(UserSystemRole.Patient))]
public class PatientProfileController : ControllerBase
{
    private readonly IPatientProfileService _patientProfileService;
    private readonly IExternalProfileSharesService _externalProfileSharesService;

    public PatientProfileController(
        IPatientProfileService patientProfileService,
        IExternalProfileSharesService externalProfileSharesService)
    {
        _patientProfileService = patientProfileService;
        _externalProfileSharesService = externalProfileSharesService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<PatientProfileDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        return await _patientProfileService.GetDashboardAsync(User, cancellationToken);
    }

    // last end point added - already-added
    [HttpGet("medical-records")]
    public async Task<ActionResult<PagedResponse<PatientMedicalRecordResponse>>> GetMedicalRecords(
        [FromQuery] string? type,
        [FromQuery] string? query,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _patientProfileService.GetMedicalRecordsAsync(type, query, page, pageSize, User, cancellationToken);
    }

    // last end point added - already-added
    [HttpGet("medical-records/{medicalFileId:guid}")]
    public async Task<ActionResult<PatientMedicalRecordDetailResponse>> GetMedicalRecord(
        Guid medicalFileId,
        CancellationToken cancellationToken)
    {
        return await _patientProfileService.GetMedicalRecordAsync(medicalFileId, User, cancellationToken);
    }

    [HttpGet("medical-records/{medicalFileId:guid}/download")]
    public async Task<IActionResult> DownloadMedicalRecord(Guid medicalFileId, CancellationToken cancellationToken)
    {
        return await _patientProfileService.DownloadMedicalRecordAsync(medicalFileId, User, cancellationToken);
    }

    [HttpPost("medical-records/{medicalFileId:guid}/download-requests")]
    public async Task<ActionResult<PatientFileDownloadRequestResponse>> CreateDownloadRequest(
        Guid medicalFileId,
        [FromBody] CreateFileDownloadRequest request,
        CancellationToken cancellationToken)
    {
        return await _patientProfileService.CreateDownloadRequestAsync(medicalFileId, request, User, cancellationToken);
    }

    [HttpGet("download-requests")]
    public async Task<ActionResult<PagedResponse<PatientFileDownloadRequestResponse>>> GetDownloadRequests(
        [FromQuery] string? status,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _patientProfileService.GetDownloadRequestsAsync(status, page, pageSize, User, cancellationToken);
    }

    // last end point added - already-added
    [HttpGet("authorized-members")]
    public async Task<ActionResult<List<PatientAuthorizedMemberResponse>>> GetAuthorizedMembers(
        CancellationToken cancellationToken)
    {
        return await _patientProfileService.GetAuthorizedMembersAsync(User, cancellationToken);
    }

    // last end point added - already-added
    [HttpGet("authorized-member-invites")]
    public async Task<ActionResult<List<PatientAuthorizedMemberInviteResponse>>> GetAuthorizedMemberInvites(
        CancellationToken cancellationToken)
    {
        return await _patientProfileService.GetAuthorizedMemberInvitesAsync(User, cancellationToken);
    }

    // last end point added - already-added
    [HttpPost("authorized-member-invites")]
    public async Task<ActionResult<PatientAuthorizedMemberInviteResponse>> CreateAuthorizedMemberInvite(
        [FromBody] CreateAuthorizedMemberInviteRequest request,
        CancellationToken cancellationToken)
    {
        return await _patientProfileService.CreateAuthorizedMemberInviteAsync(request, User, cancellationToken);
    }

    [HttpPatch("authorized-members/{authorizedMemberId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateAuthorizedMember(Guid authorizedMemberId, CancellationToken cancellationToken)
    {
        return await _patientProfileService.DeactivateAuthorizedMemberAsync(authorizedMemberId, User, cancellationToken);
    }

    [HttpPatch("authorized-members/{authorizedMemberId:guid}/activate")]
    public async Task<IActionResult> ActivateAuthorizedMember(Guid authorizedMemberId, CancellationToken cancellationToken)
    {
        return await _patientProfileService.ActivateAuthorizedMemberAsync(authorizedMemberId, User, cancellationToken);
    }

    [HttpPatch("authorized-member-invites/{inviteId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateAuthorizedMemberInvite(Guid inviteId, CancellationToken cancellationToken)
    {
        return await _patientProfileService.DeactivateAuthorizedMemberInviteAsync(inviteId, User, cancellationToken);
    }

    [HttpPatch("authorized-member-invites/{inviteId:guid}/activate")]
    public async Task<IActionResult> ActivateAuthorizedMemberInvite(Guid inviteId, CancellationToken cancellationToken)
    {
        return await _patientProfileService.ActivateAuthorizedMemberInviteAsync(inviteId, User, cancellationToken);
    }

    // last end point added
    [HttpPost("external-shares")]
    public async Task<ActionResult<ExternalProfileShareResponse>> CreateExternalShare(
        [FromBody] CreateExternalProfileShareRequest request,
        CancellationToken cancellationToken)
    {
        return await _externalProfileSharesService.CreateShareAsync(
            request,
            User,
            BuildPublicBaseUrl(),
            cancellationToken);
    }

    // last end point added
    [HttpGet("external-shares")]
    public async Task<ActionResult<List<ExternalProfileShareResponse>>> GetExternalShares(CancellationToken cancellationToken)
    {
        return await _externalProfileSharesService.GetMySharesAsync(
            User,
            BuildPublicBaseUrl(),
            cancellationToken);
    }

    // last end point added
    [HttpPatch("external-shares/{shareId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateExternalShare(Guid shareId, CancellationToken cancellationToken)
    {
        return await _externalProfileSharesService.DeactivateShareAsync(shareId, User, cancellationToken);
    }

    private string BuildPublicBaseUrl() => $"{Request.Scheme}://{Request.Host}";
}
