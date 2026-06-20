using HSCSAPI.Models.Enums;

namespace HSCSAPI.Services.Chats;

public interface IChatFileStorage
{
    void EnsureChatDirectory(Guid chatId);
    Task<StoredChatFile> SaveAsync(
        Guid chatId,
        Guid messageId,
        ChatMessageType messageType,
        IFormFile file,
        CancellationToken cancellationToken = default);
    void DeleteIfExists(string relativePath);
    string ResolvePhysicalPath(string relativePath);
}

public sealed record StoredChatFile(
    string RelativePath,
    string OriginalFileName,
    string ContentType,
    long FileSizeInBytes);
