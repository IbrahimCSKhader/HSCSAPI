using HSCSAPI.Models.Identity;

namespace HSCSAPI.Models.Chats;

public class Chat
{
    public Guid ChatId { get; set; } = Guid.NewGuid();
    public Guid UserOneId { get; set; }
    public Guid UserTwoId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }

    public User UserOne { get; set; } = null!;
    public User UserTwo { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = new HashSet<ChatMessage>();
}
