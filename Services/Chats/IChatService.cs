using System.Security.Claims;
using HSCSAPI.DTOs.Chat;
using HSCSAPI.Models.Enums;

namespace HSCSAPI.Services.Chats;

public interface IChatService
{
    Task<ChatResponse> OpenChatAsync(
        Guid recipientUserId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<List<ChatResponse>> GetChatsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ChatMessagesPageResponse> GetMessagesAsync(
        Guid chatId,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ChatMessageResponse> SendMessageAsync(
        Guid chatId,
        ChatMessageType messageType,
        string? text,
        IFormFile? file,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ChatMessageResponse> EditMessageAsync(Guid chatId, Guid messageId, string text, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task UnsendMessageAsync(Guid chatId, Guid messageId, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    Task<MarkChatReadResponse> MarkAsReadAsync(
        Guid chatId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ChatFileDownload> GetFileAsync(
        Guid chatId,
        Guid messageId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<bool> IsMemberAsync(
        Guid chatId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<Guid>> GetChatIdsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record ChatFileDownload(
    string PhysicalPath,
    string ContentType,
    string OriginalFileName);
