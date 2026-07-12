using System.ComponentModel.DataAnnotations;
using HSCSAPI.Models.Enums;

namespace HSCSAPI.DTOs.Chat;

public class OpenChatRequest
{
    public Guid? RecipientUserId { get; set; }
    public string? PatientUserId { get; set; }
}

public class SendChatMessageRequest
{
    [Required]
    public ChatMessageType MessageType { get; set; }

    [MaxLength(4000)]
    public string? Text { get; set; }

    public IFormFile? File { get; set; }
}

public class EditChatMessageRequest
{
    [Required]
    [MaxLength(4000)]
    public string Text { get; set; } = string.Empty;
}

public class ChatResponse
{
    public Guid ChatId { get; set; }
    public Guid OtherUserId { get; set; }
    public string? OtherPatientUserId { get; set; }
    public string OtherUserName { get; set; } = string.Empty;
    public string? DoctorSpecialty { get; set; }
    public string? ClinicName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
    public ChatMessageType? LastMessageType { get; set; }
    public int UnreadCount { get; set; }
}

public class ChatMessageResponse
{
    public Guid ChatMessageId { get; set; }
    public Guid ChatId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public ChatMessageType MessageType { get; set; }
    public string? Text { get; set; }
    public string? FileUrl { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeInBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? EditedAt { get; set; }
}

public class ChatMessagesPageResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<ChatMessageResponse> Items { get; set; } = [];
}

public class MarkChatReadResponse
{
    public int UpdatedMessages { get; set; }
    public DateTime ReadAt { get; set; }
}

public class ChatNotificationResponse
{
    public Guid NotificationId { get; set; }
    public Guid ChatId { get; set; }
    public Guid SenderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public bool IsRead { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
