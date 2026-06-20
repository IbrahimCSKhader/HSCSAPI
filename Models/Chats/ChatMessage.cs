using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;

namespace HSCSAPI.Models.Chats;

public class ChatMessage
{
    public Guid ChatMessageId { get; set; } = Guid.NewGuid();
    public Guid ChatId { get; set; }
    public Guid SenderId { get; set; }
    public ChatMessageType MessageType { get; set; }
    public string? Text { get; set; }
    public string? FilePath { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeInBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    public Chat Chat { get; set; } = null!;
    public User Sender { get; set; } = null!;
}
