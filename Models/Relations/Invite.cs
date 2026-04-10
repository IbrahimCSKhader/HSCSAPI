using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Relations;

public class Invite
{
    public Guid InviteId { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid AuthorizedMemberId { get; set; }
    public RelationshipType RelationshipType { get; set; }
    public InviteStatus Status { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public Patient Patient { get; set; } = null!;
    public AuthorizedMember AuthorizedMember { get; set; } = null!;
}
