using System.ComponentModel.DataAnnotations;

namespace HSCSAPI.DTOs.Notifications;

public class NotificationListResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int UnreadCount { get; set; }
    public int ReadCount { get; set; }
    public List<NotificationResponse> Items { get; set; } = [];
}

public class NotificationResponse
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public bool IsRead { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class UpdateNotificationReadStatusRequest
{
    [Required]
    public bool? IsRead { get; set; }
}

public class MarkNotificationsReadResponse
{
    public int UpdatedNotifications { get; set; }
    public DateTime MarkedAsReadAt { get; set; }
}
