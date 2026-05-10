using System.Security.Claims;
using HSCSAPI.DTOs.AuthorizedMember;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.AuthorizedMembers;

public interface IAuthorizedMembersService
{
    Task<ActionResult<AuthorizedMemberProfileResponse>> GetMyProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<List<AuthorizedMemberPatientResponse>>> GetMyPatientsAsync(
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
