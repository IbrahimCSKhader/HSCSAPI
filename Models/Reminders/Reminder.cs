using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Reminders;

public class Reminder
{
    public Guid ReminderId { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid? AuthorizedMemberId { get; set; }
    public string ReminderText { get; set; } = string.Empty;
    public DateTime ReminderAt { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
    public AuthorizedMember? AuthorizedMember { get; set; }
}
