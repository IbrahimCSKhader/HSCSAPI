using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.AuthorizedMember;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Relations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.AuthorizedMembers;

public class AuthorizedMembersService : IAuthorizedMembersService
{
    private readonly AppDbContext _dbContext;

    public AuthorizedMembersService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ActionResult<AuthorizedMemberProfileResponse>> GetMyProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var profile = await _dbContext.AuthorizedMembers
            .AsNoTracking()
            .Where(authorizedMember => authorizedMember.AuthorizedMemberId == currentUserId.Value)
            .Select(authorizedMember => new AuthorizedMemberProfileResponse
            {
                AuthorizedMemberId = authorizedMember.AuthorizedMemberId,
                Name = authorizedMember.User.Name,
                Email = authorizedMember.User.Email ?? string.Empty,
                PhoneNumber = authorizedMember.User.PhoneNumber,
                Address = authorizedMember.User.Address,
                DateOfBirth = authorizedMember.User.DateOfBirth
            })
            .FirstOrDefaultAsync(cancellationToken);

        return profile is null
            ? new NotFoundObjectResult("Authorized member not found.")
            : new OkObjectResult(profile);
    }

    public async Task<ActionResult<List<AuthorizedMemberPatientResponse>>> GetMyPatientsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var patients = await _dbContext.PatientAuthorizedMembers
            .AsNoTracking()
            .Where(relation => relation.AuthorizedMemberId == currentUserId.Value)
            .OrderByDescending(relation => relation.AuthorizedAt)
            .Select(relation => new AuthorizedMemberPatientQueryResult
            {
                PatientId = relation.PatientId,
                Name = relation.Patient.User.Name,
                Email = relation.Patient.User.Email ?? string.Empty,
                PhoneNumber = relation.Patient.User.PhoneNumber,
                Address = relation.Patient.User.Address,
                DateOfBirth = relation.Patient.User.DateOfBirth,
                PatientUserId = relation.Patient.UserID,
                ClinicId = relation.Patient.User.ClinicId,
                ClinicName = relation.Patient.User.Clinic != null ? relation.Patient.User.Clinic.Name : null,
                RelationshipType = relation.RelationshipType,
                AuthorizedAt = relation.AuthorizedAt
            })
            .ToListAsync(cancellationToken);

        return new OkObjectResult(patients.Select(MapPatientResponse).ToList());
    }

    public async Task<ActionResult<List<AuthorizedMemberInviteResponse>>> GetMyInvitesAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var invites = await LoadInviteResponsesAsync(currentUserId.Value, cancellationToken);
        return new OkObjectResult(invites);
    }

    public async Task<ActionResult<AuthorizedMemberInviteResponse>> AcceptInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return await RespondToInviteAsync(inviteId, InviteStatus.Accepted, user, cancellationToken);
    }

    public async Task<ActionResult<AuthorizedMemberInviteResponse>> RejectInviteAsync(
        Guid inviteId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return await RespondToInviteAsync(inviteId, InviteStatus.Rejected, user, cancellationToken);
    }

    private async Task<ActionResult<AuthorizedMemberInviteResponse>> RespondToInviteAsync(
        Guid inviteId,
        InviteStatus targetStatus,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var invite = await _dbContext.Invites
            .FirstOrDefaultAsync(
                x => x.InviteId == inviteId && x.AuthorizedMemberId == currentUserId.Value,
                cancellationToken);

        if (invite == null)
        {
            return new NotFoundObjectResult("Invite not found.");
        }

        if (invite.Status != InviteStatus.Pending)
        {
            return new BadRequestObjectResult("Invite has already been responded to.");
        }

        var respondedAt = DateTime.UtcNow;

        if (targetStatus == InviteStatus.Accepted)
        {
            var relationExists = await _dbContext.PatientAuthorizedMembers
                .AnyAsync(
                    x => x.PatientId == invite.PatientId && x.AuthorizedMemberId == currentUserId.Value,
                    cancellationToken);

            if (!relationExists)
            {
                _dbContext.PatientAuthorizedMembers.Add(new PatientAuthorizedMember
                {
                    PatientId = invite.PatientId,
                    AuthorizedMemberId = currentUserId.Value,
                    RelationshipType = invite.RelationshipType,
                    AuthorizedAt = respondedAt
                });
            }
        }

        invite.Status = targetStatus;
        invite.RespondedAt = respondedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetInviteResponseAsync(inviteId, currentUserId.Value, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Invite not found.")
            : new OkObjectResult(response);
    }

    private async Task<List<AuthorizedMemberInviteResponse>> LoadInviteResponsesAsync(
        Guid authorizedMemberId,
        CancellationToken cancellationToken)
    {
        var invites = await _dbContext.Invites
            .AsNoTracking()
            .Where(invite => invite.AuthorizedMemberId == authorizedMemberId)
            .OrderByDescending(invite => invite.SentAt)
            .Select(invite => new AuthorizedMemberInviteQueryResult
            {
                InviteId = invite.InviteId,
                PatientId = invite.PatientId,
                PatientName = invite.Patient.User.Name,
                PatientEmail = invite.Patient.User.Email ?? string.Empty,
                PatientUserId = invite.Patient.UserID,
                ClinicId = invite.Patient.User.ClinicId,
                ClinicName = invite.Patient.User.Clinic != null ? invite.Patient.User.Clinic.Name : null,
                RelationshipType = invite.RelationshipType,
                Status = invite.Status,
                SentAt = invite.SentAt,
                RespondedAt = invite.RespondedAt
            })
            .ToListAsync(cancellationToken);

        return invites.Select(MapInviteResponse).ToList();
    }

    private async Task<AuthorizedMemberInviteResponse?> GetInviteResponseAsync(
        Guid inviteId,
        Guid authorizedMemberId,
        CancellationToken cancellationToken)
    {
        var invite = await _dbContext.Invites
            .AsNoTracking()
            .Where(x => x.InviteId == inviteId && x.AuthorizedMemberId == authorizedMemberId)
            .Select(x => new AuthorizedMemberInviteQueryResult
            {
                InviteId = x.InviteId,
                PatientId = x.PatientId,
                PatientName = x.Patient.User.Name,
                PatientEmail = x.Patient.User.Email ?? string.Empty,
                PatientUserId = x.Patient.UserID,
                ClinicId = x.Patient.User.ClinicId,
                ClinicName = x.Patient.User.Clinic != null ? x.Patient.User.Clinic.Name : null,
                RelationshipType = x.RelationshipType,
                Status = x.Status,
                SentAt = x.SentAt,
                RespondedAt = x.RespondedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return invite is null ? null : MapInviteResponse(invite);
    }

    private static AuthorizedMemberPatientResponse MapPatientResponse(AuthorizedMemberPatientQueryResult patient)
    {
        return new AuthorizedMemberPatientResponse
        {
            PatientId = patient.PatientId,
            Name = patient.Name,
            Email = patient.Email,
            PhoneNumber = patient.PhoneNumber,
            Address = patient.Address,
            DateOfBirth = patient.DateOfBirth,
            PatientUserId = patient.PatientUserId,
            ClinicId = patient.ClinicId,
            ClinicName = patient.ClinicName,
            RelationshipType = patient.RelationshipType.ToString(),
            AuthorizedAt = patient.AuthorizedAt
        };
    }

    private static AuthorizedMemberInviteResponse MapInviteResponse(AuthorizedMemberInviteQueryResult invite)
    {
        return new AuthorizedMemberInviteResponse
        {
            InviteId = invite.InviteId,
            PatientId = invite.PatientId,
            PatientName = invite.PatientName,
            PatientEmail = invite.PatientEmail,
            PatientUserId = invite.PatientUserId,
            ClinicId = invite.ClinicId,
            ClinicName = invite.ClinicName,
            RelationshipType = invite.RelationshipType.ToString(),
            Status = invite.Status.ToString(),
            SentAt = invite.SentAt,
            RespondedAt = invite.RespondedAt
        };
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null)
        {
            return null;
        }

        return Guid.TryParse(claim, out var userId) ? userId : null;
    }
}
