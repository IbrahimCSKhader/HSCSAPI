using System.Security.Claims;
using HSCSAPI.DTOs.AuthorizedMember;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.AuthorizedMembers;

public interface IAuthorizedMembersService
{
    Task<ActionResult<AuthorizedMemberDashboardResponse>> GetDashboardAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<AuthorizedMemberProfileResponse>> GetMyProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
    Task<ActionResult<AuthorizedMemberProfileResponse>> UpdateMyProfileAsync(UpdateAuthorizedMemberProfileRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    Task<ActionResult<List<AuthorizedMemberPatientResponse>>> GetMyPatientsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<AuthorizedMemberPatientResponse>> GetMyPatientAsync(
        Guid patientId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<AuthorizedMemberAppointmentsResponse>> GetMyAppointmentsAsync(
        Guid? patientId,
        DateOnly? fromDate,
        DateOnly? toDate,
        bool upcomingOnly,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<AuthorizedMemberMedicalRecordsResponse>> GetPatientMedicalRecordsAsync(
        Guid patientId,
        string? type,
        string? query,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<AuthorizedMemberMedicalRecordDetailResponse>> GetPatientMedicalRecordAsync(
        Guid patientId,
        Guid medicalFileId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> DownloadPatientMedicalRecordAsync(
        Guid patientId,
        Guid medicalFileId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<List<AuthorizedMemberInviteResponse>>> GetMyInvitesAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<AuthorizedMemberInviteResponse>> AcceptInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<AuthorizedMemberInviteResponse>> RejectInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
