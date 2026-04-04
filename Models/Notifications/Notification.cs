using HSCSAPI.Models.Identity;

namespace HSCSAPI.Models.Notifications;

public class Notification
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsRead { get; set; }

    public User User { get; set; } = null!;
}
