using HSCSAPI.DTOs.AuthorizedMember;
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

    [HttpGet("me")]
    public async Task<ActionResult<AuthorizedMemberProfileResponse>> GetMyProfile(CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.GetMyProfileAsync(User, cancellationToken);
    }

    [HttpGet("my-patients")]
    public async Task<ActionResult<List<AuthorizedMemberPatientResponse>>> GetMyPatients(CancellationToken cancellationToken)
    {
        return await _authorizedMembersService.GetMyPatientsAsync(User, cancellationToken);
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
