using HSCSAPI.Models.Identity;

namespace HSCSAPI.Models.Reminders;

public class ReminderPreference
{
    public Guid UserId { get; set; }
    public bool AppointmentRemindersEnabled { get; set; } = true;
    public bool LabResultRemindersEnabled { get; set; } = true;
    public bool MessageRemindersEnabled { get; set; } = true;
    public bool InAppNotificationsEnabled { get; set; } = true;
    public bool EmailRemindersEnabled { get; set; }
    public bool SmsRemindersEnabled { get; set; }
    public bool MedicationRemindersEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
