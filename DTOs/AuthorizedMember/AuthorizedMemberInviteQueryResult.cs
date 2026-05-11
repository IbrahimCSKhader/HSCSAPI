using HSCSAPI.Models.Enums;

namespace HSCSAPI.DTOs.AuthorizedMember;

internal sealed class AuthorizedMemberInviteQueryResult
{
    public Guid InviteId { get; init; }
    public Guid PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public string PatientEmail { get; init; } = string.Empty;
    public string PatientUserId { get; init; } = string.Empty;
    public Guid? ClinicId { get; init; }
    public string? ClinicName { get; init; }
    public RelationshipType RelationshipType { get; init; }
    public InviteStatus Status { get; init; }
    public DateTime SentAt { get; init; }
    public DateTime? RespondedAt { get; init; }
}
