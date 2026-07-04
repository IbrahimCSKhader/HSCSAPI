using HSCSAPI.DTOs.AuthorizedMember;
using HSCSAPI.DTOs.Common;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.AuthorizedMembers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(UserSystemRole.AuthorizedMember))]
public class AuthorizedMembersController : ControllerBase
{
    private readonly IAuthorizedMembersService _authorizedMembersService;

    public AuthorizedMembersController(IAuthorizedMembersService authorizedMembersService)
    {
        _authorizedMembersService = authorizedMembersService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AuthorizedMemberDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.GetDashboardAsync(User, cancellationToken);
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthorizedMemberProfileResponse>> GetMyProfile(CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.GetMyProfileAsync(User, cancellationToken);
    }

    // last end point added
    [HttpPut("me")]
    public async Task<ActionResult<AuthorizedMemberProfileResponse>> UpdateMyProfile(
        [FromBody] UpdateAuthorizedMemberProfileRequest request, CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.UpdateMyProfileAsync(request, User, cancellationToken);
    }

    // last end point added
    [HttpPut("me/password")]
    public async Task<ActionResult<ChangePasswordResponse>> ChangeMyPassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.ChangeMyPasswordAsync(request, User, cancellationToken);
    }

    [HttpGet("my-patients")]
    public async Task<ActionResult<List<AuthorizedMemberPatientResponse>>> GetMyPatients(CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.GetMyPatientsAsync(User, cancellationToken);
    }

    [HttpGet("my-patients/{patientId:guid}")]
    public async Task<ActionResult<AuthorizedMemberPatientResponse>> GetMyPatient(
        Guid patientId,
        CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.GetMyPatientAsync(patientId, User, cancellationToken);
    }

    [HttpGet("my-appointments")]
    public async Task<ActionResult<AuthorizedMemberAppointmentsResponse>> GetMyAppointments(
        [FromQuery] Guid? patientId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] bool upcomingOnly,
        CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.GetMyAppointmentsAsync(
            patientId,
            fromDate,
            toDate,
            upcomingOnly,
            User,
            cancellationToken);
    }

    [HttpGet("my-patients/{patientId:guid}/medical-records")]
    public async Task<ActionResult<AuthorizedMemberMedicalRecordsResponse>> GetPatientMedicalRecords(
        Guid patientId,
        [FromQuery] string? type,
        [FromQuery] string? query,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.GetPatientMedicalRecordsAsync(
            patientId,
            type,
            query,
            page,
            pageSize,
            User,
            cancellationToken);
    }

    [HttpGet("my-patients/{patientId:guid}/medical-records/{medicalFileId:guid}")]
    public async Task<ActionResult<AuthorizedMemberMedicalRecordDetailResponse>> GetPatientMedicalRecord(
        Guid patientId,
        Guid medicalFileId,
        CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.GetPatientMedicalRecordAsync(
            patientId,
            medicalFileId,
            User,
            cancellationToken);
    }

    [HttpGet("my-patients/{patientId:guid}/medical-records/{medicalFileId:guid}/download")]
    public async Task<IActionResult> DownloadPatientMedicalRecord(
        Guid patientId,
        Guid medicalFileId,
        CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.DownloadPatientMedicalRecordAsync(
            patientId,
            medicalFileId,
            User,
            cancellationToken);
    }

    [HttpGet("my-invites")]
    public async Task<ActionResult<List<AuthorizedMemberInviteResponse>>> GetMyInvites(CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.GetMyInvitesAsync(User, cancellationToken);
    }

    [HttpPost("my-invites/{inviteId:guid}/accept")]
    public async Task<ActionResult<AuthorizedMemberInviteResponse>> AcceptInvite(Guid inviteId, CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.AcceptInviteAsync(inviteId, User, cancellationToken);
    }

    [HttpPost("my-invites/{inviteId:guid}/reject")]
    public async Task<ActionResult<AuthorizedMemberInviteResponse>> RejectInvite(Guid inviteId, CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.RejectInviteAsync(inviteId, User, cancellationToken);
    }
}
