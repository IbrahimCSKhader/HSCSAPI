using System.Security.Claims;
using HSCSAPI.DTOs.PatientProfile;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.PatientProfile;

public interface IPatientProfileService
{
    Task<ActionResult<PatientProfileDashboardResponse>> GetDashboardAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<PagedResponse<PatientNotificationResponse>>> GetNotificationsAsync(
        string? status,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<PatientNotificationResponse>> MarkNotificationAsReadAsync(
        Guid notificationId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> MarkAllNotificationsAsReadAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<PagedResponse<PatientMedicalRecordResponse>>> GetMedicalRecordsAsync(
        string? type,
        string? query,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<PatientMedicalRecordDetailResponse>> GetMedicalRecordAsync(
        Guid medicalFileId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> DownloadMedicalRecordAsync(
        Guid medicalFileId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<PatientFileDownloadRequestResponse>> CreateDownloadRequestAsync(
        Guid medicalFileId,
        CreateFileDownloadRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<PagedResponse<PatientFileDownloadRequestResponse>>> GetDownloadRequestsAsync(
        string? status,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<List<PatientAuthorizedMemberResponse>>> GetAuthorizedMembersAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<List<PatientAuthorizedMemberInviteResponse>>> GetAuthorizedMemberInvitesAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<PatientAuthorizedMemberInviteResponse>> CreateAuthorizedMemberInviteAsync(
        CreateAuthorizedMemberInviteRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> RemoveAuthorizedMemberAsync(
        Guid authorizedMemberId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> CancelAuthorizedMemberInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
