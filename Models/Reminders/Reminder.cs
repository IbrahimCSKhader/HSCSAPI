using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Reminders;

public class Reminder
{
    public int ReminderId { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public int? AuthorizedMemberId { get; set; }
    public string ReminderText { get; set; } = string.Empty;
    public DateTime ReminderAt { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
    public AuthorizedMember? AuthorizedMember { get; set; }
}
