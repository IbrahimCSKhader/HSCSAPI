using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Relations;
using HSCSAPI.Models.Reminders;

namespace HSCSAPI.Models.Profiles;

public class AuthorizedMember
{
        public Guid AuthorizedMemberId { get; set; } = Guid.NewGuid();
    public User User { get; set; } = null!;
    public ICollection<PatientAuthorizedMember> Patients { get; set; } = new HashSet<PatientAuthorizedMember>();
    public ICollection<Invite> ReceivedInvites { get; set; } = new HashSet<Invite>();
    public ICollection<Reminder> Reminders { get; set; } = new HashSet<Reminder>();
}
