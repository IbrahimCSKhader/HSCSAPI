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

    public PatientProfileController(IPatientProfileService patientProfileService)
    {
        _patientProfileService = patientProfileService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<PatientProfileDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        return await _patientProfileService.GetDashboardAsync(User, cancellationToken);
    }

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

    [HttpGet("authorized-members")]
    public async Task<ActionResult<List<PatientAuthorizedMemberResponse>>> GetAuthorizedMembers(
        CancellationToken cancellationToken)
    {
        return await _patientProfileService.GetAuthorizedMembersAsync(User, cancellationToken);
    }

    [HttpGet("authorized-member-invites")]
    public async Task<ActionResult<List<PatientAuthorizedMemberInviteResponse>>> GetAuthorizedMemberInvites(
        CancellationToken cancellationToken)
    {
        return await _patientProfileService.GetAuthorizedMemberInvitesAsync(User, cancellationToken);
    }

    [HttpPost("authorized-member-invites")]
    public async Task<ActionResult<PatientAuthorizedMemberInviteResponse>> CreateAuthorizedMemberInvite(
        [FromBody] CreateAuthorizedMemberInviteRequest request,
        CancellationToken cancellationToken)
    {
        return await _patientProfileService.CreateAuthorizedMemberInviteAsync(request, User, cancellationToken);
    }

    [HttpDelete("authorized-members/{authorizedMemberId:guid}")]
    public async Task<IActionResult> RemoveAuthorizedMember(Guid authorizedMemberId, CancellationToken cancellationToken)
    {
        return await _patientProfileService.RemoveAuthorizedMemberAsync(authorizedMemberId, User, cancellationToken);
    }

    [HttpDelete("authorized-member-invites/{inviteId:guid}")]
    public async Task<IActionResult> CancelAuthorizedMemberInvite(Guid inviteId, CancellationToken cancellationToken)
    {
        return await _patientProfileService.CancelAuthorizedMemberInviteAsync(inviteId, User, cancellationToken);
    }
}
