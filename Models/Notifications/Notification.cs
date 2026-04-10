using HSCSAPI.Models.Identity;

namespace HSCSAPI.Models.Notifications;

public class Notification
{
    public Guid NotificationId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsRead { get; set; }

    public User User { get; set; } = null!;
}
