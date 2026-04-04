using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Relations;

public class Invite
{
    public int InviteId { get; set; }
    public int PatientId { get; set; }
    public int AuthorizedMemberId { get; set; }
    public RelationshipType RelationshipType { get; set; }
    public InviteStatus Status { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public Patient Patient { get; set; } = null!;
    public AuthorizedMember AuthorizedMember { get; set; } = null!;
}
