using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Chat;
using HSCSAPI.Hub;
using HSCSAPI.Models.Chats;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Chats;

public class ChatService : IChatService
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;
    private const int MaxTextLength = 4000;

    private readonly AppDbContext _dbContext;
    private readonly IChatFileStorage _fileStorage;
    private readonly IHubContext<ChatHub> _chatHub;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        AppDbContext dbContext,
        IChatFileStorage fileStorage,
        IHubContext<ChatHub> chatHub,
        IHubContext<NotificationHub> notificationHub,
        ILogger<ChatService> logger)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _chatHub = chatHub;
        _notificationHub = notificationHub;
        _logger = logger;
    }

    public async Task<ChatResponse> OpenChatAsync(
        Guid recipientUserId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);

        if (recipientUserId == Guid.Empty)
        {
            throw new ArgumentException("RecipientUserId is required.");
        }

        if (recipientUserId == currentUserId)
        {
            throw new ArgumentException("You cannot open a chat with yourself.");
        }

        if (!await _dbContext.Users.AsNoTracking().AnyAsync(x => x.Id == recipientUserId, cancellationToken))
        {
            throw new KeyNotFoundException("Recipient user not found.");
        }

        var (userOneId, userTwoId) = NormalizeParticipants(currentUserId, recipientUserId);
        var existingChatId = await _dbContext.Chats
            .AsNoTracking()
            .Where(x => x.UserOneId == userOneId && x.UserTwoId == userTwoId)
            .Select(x => (Guid?)x.ChatId)
            .FirstOrDefaultAsync(cancellationToken);

        Guid chatId;
        if (existingChatId.HasValue)
        {
            chatId = existingChatId.Value;
        }
        else
        {
            var chat = new Chat
            {
                UserOneId = userOneId,
                UserTwoId = userTwoId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Chats.Add(chat);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                chatId = chat.ChatId;
            }
            catch (DbUpdateException)
            {
                _dbContext.Entry(chat).State = EntityState.Detached;
                chatId = await _dbContext.Chats
                    .AsNoTracking()
                    .Where(x => x.UserOneId == userOneId && x.UserTwoId == userTwoId)
                    .Select(x => x.ChatId)
                    .FirstAsync(cancellationToken);
            }
        }

        _fileStorage.EnsureChatDirectory(chatId);
        return await GetChatResponseAsync(chatId, currentUserId, cancellationToken);
    }

    public async Task<List<ChatResponse>> GetChatsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);

        var chats = await _dbContext.Chats
            .AsNoTracking()
            .Where(x => x.UserOneId == currentUserId || x.UserTwoId == currentUserId)
            .Select(x => new ChatResponse
            {
                ChatId = x.ChatId,
                OtherUserId = x.UserOneId == currentUserId ? x.UserTwoId : x.UserOneId,
                OtherUserName = x.UserOneId == currentUserId ? x.UserTwo.Name : x.UserOne.Name,
                CreatedAt = x.CreatedAt,
                LastMessageAt = x.LastMessageAt,
                LastMessagePreview = x.Messages
                    .OrderByDescending(message => message.CreatedAt)
                    .ThenByDescending(message => message.ChatMessageId)
                    .Select(message => message.MessageType == ChatMessageType.Text
                        ? message.Text
                        : message.MessageType == ChatMessageType.Image ? "Image" : "Audio")
                    .FirstOrDefault(),
                LastMessageType = x.Messages
                    .OrderByDescending(message => message.CreatedAt)
                    .ThenByDescending(message => message.ChatMessageId)
                    .Select(message => (ChatMessageType?)message.MessageType)
                    .FirstOrDefault(),
                UnreadCount = x.Messages.Count(message =>
                    message.SenderId != currentUserId && message.ReadAt == null)
            })
            .OrderByDescending(x => x.LastMessageAt ?? x.CreatedAt)
            .ThenByDescending(x => x.ChatId)
            .ToListAsync(cancellationToken);

        return chats;
    }

    public async Task<ChatMessagesPageResponse> GetMessagesAsync(
        Guid chatId,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        await EnsureMemberAsync(chatId, currentUserId, cancellationToken);
        NormalizePaging(ref page, ref pageSize);

        var query = _dbContext.ChatMessages
            .AsNoTracking()
            .Where(x => x.ChatId == chatId);

        var totalCount = await query.CountAsync(cancellationToken);
        var messages = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.ChatMessageId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ChatMessageResponse
            {
                ChatMessageId = x.ChatMessageId,
                ChatId = x.ChatId,
                SenderId = x.SenderId,
                SenderName = x.Sender.Name,
                MessageType = x.MessageType,
                Text = x.Text,
                FileUrl = x.FilePath == null ? null : string.Empty,
                OriginalFileName = x.OriginalFileName,
                ContentType = x.ContentType,
                FileSizeInBytes = x.FileSizeInBytes,
                CreatedAt = x.CreatedAt,
                ReadAt = x.ReadAt
            })
            .ToListAsync(cancellationToken);

        foreach (var message in messages.Where(x => x.FileUrl is not null))
        {
            message.FileUrl = $"/api/chats/{message.ChatId}/messages/{message.ChatMessageId}/file";
        }

        messages.Reverse();
        return new ChatMessagesPageResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = messages
        };
    }

    public async Task<ChatMessageResponse> SendMessageAsync(
        Guid chatId,
        ChatMessageType messageType,
        string? text,
        IFormFile? file,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        var chat = await _dbContext.Chats
            .Include(x => x.UserOne)
            .Include(x => x.UserTwo)
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (chat is null)
        {
            throw new KeyNotFoundException("Chat not found.");
        }

        if (chat.UserOneId != currentUserId && chat.UserTwoId != currentUserId)
        {
            throw new UnauthorizedAccessException("You are not a member of this chat.");
        }

        if (!Enum.IsDefined(messageType))
        {
            throw new ArgumentException("Invalid message type.");
        }

        var normalizedText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        ValidateMessage(messageType, normalizedText, file);

        var message = new ChatMessage
        {
            ChatId = chatId,
            SenderId = currentUserId,
            MessageType = messageType,
            Text = normalizedText,
            CreatedAt = DateTime.UtcNow
        };

        var recipientUserId = chat.UserOneId == currentUserId ? chat.UserTwoId : chat.UserOneId;
        var currentSenderName = chat.UserOneId == currentUserId ? chat.UserOne.Name : chat.UserTwo.Name;
        var notification = new Notification
        {
            UserId = recipientUserId,
            Title = $"New message from {currentSenderName}.",
            IsRead = false
        };

        StoredChatFile? storedFile = null;
        try
        {
            if (file is not null)
            {
                storedFile = await _fileStorage.SaveAsync(
                    chatId,
                    message.ChatMessageId,
                    messageType,
                    file,
                    cancellationToken);

                message.FilePath = storedFile.RelativePath;
                message.OriginalFileName = storedFile.OriginalFileName;
                message.ContentType = storedFile.ContentType;
                message.FileSizeInBytes = storedFile.FileSizeInBytes;
            }

            chat.LastMessageAt = message.CreatedAt;
            _dbContext.ChatMessages.Add(message);
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (storedFile is not null)
            {
                _fileStorage.DeleteIfExists(storedFile.RelativePath);
            }

            throw;
        }

        var response = MapMessage(message, currentSenderName);

        try
        {
            await _chatHub.Clients.Users(currentUserId.ToString(), recipientUserId.ToString())
                .SendAsync("ReceiveMessage", response, CancellationToken.None);

            await _notificationHub.Clients.User(recipientUserId.ToString())
                .SendAsync(
                    "ReceiveNotification",
                    new ChatNotificationResponse
                    {
                        NotificationId = notification.NotificationId,
                        ChatId = chatId,
                        SenderId = currentUserId,
                        Title = notification.Title
                    },
                    CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Message {MessageId} was saved but its real-time broadcast failed.",
                message.ChatMessageId);
        }

        return response;
    }

    public async Task<MarkChatReadResponse> MarkAsReadAsync(
        Guid chatId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        var chat = await EnsureMemberAsync(chatId, currentUserId, cancellationToken);
        var readAt = DateTime.UtcNow;

        var messages = await _dbContext.ChatMessages
            .Where(x => x.ChatId == chatId && x.SenderId != currentUserId && x.ReadAt == null)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.ReadAt = readAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        var response = new MarkChatReadResponse
        {
            UpdatedMessages = messages.Count,
            ReadAt = readAt
        };

        try
        {
            await _chatHub.Clients.Users(chat.UserOneId.ToString(), chat.UserTwoId.ToString())
                .SendAsync(
                    "MessagesRead",
                    new { chatId, readByUserId = currentUserId, response.UpdatedMessages, response.ReadAt },
                    CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Read receipt broadcast failed for chat {ChatId}.", chatId);
        }

        return response;
    }

    public async Task<ChatFileDownload> GetFileAsync(
        Guid chatId,
        Guid messageId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        await EnsureMemberAsync(chatId, currentUserId, cancellationToken);

        var file = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(x => x.ChatId == chatId && x.ChatMessageId == messageId)
            .Select(x => new
            {
                x.FilePath,
                x.ContentType,
                x.OriginalFileName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (file is null)
        {
            throw new KeyNotFoundException("Message not found.");
        }

        if (file.FilePath is null || file.ContentType is null)
        {
            throw new InvalidOperationException("This text message does not contain a file.");
        }

        var physicalPath = _fileStorage.ResolvePhysicalPath(file.FilePath);
        if (!File.Exists(physicalPath))
        {
            throw new KeyNotFoundException("The chat file is not available on the server.");
        }

        return new ChatFileDownload(
            physicalPath,
            file.ContentType,
            file.OriginalFileName ?? Path.GetFileName(physicalPath));
    }

    public Task<bool> IsMemberAsync(
        Guid chatId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Chats.AsNoTracking().AnyAsync(
            x => x.ChatId == chatId && (x.UserOneId == userId || x.UserTwoId == userId),
            cancellationToken);
    }

    public Task<List<Guid>> GetChatIdsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Chats
            .AsNoTracking()
            .Where(x => x.UserOneId == userId || x.UserTwoId == userId)
            .Select(x => x.ChatId)
            .ToListAsync(cancellationToken);
    }

    private async Task<Chat> EnsureMemberAsync(
        Guid chatId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var chat = await _dbContext.Chats
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (chat is null)
        {
            throw new KeyNotFoundException("Chat not found.");
        }

        if (chat.UserOneId != userId && chat.UserTwoId != userId)
        {
            throw new UnauthorizedAccessException("You are not a member of this chat.");
        }

        return chat;
    }

    private async Task<ChatResponse> GetChatResponseAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Chats
            .AsNoTracking()
            .Where(x => x.ChatId == chatId)
            .Select(x => new ChatResponse
            {
                ChatId = x.ChatId,
                OtherUserId = x.UserOneId == currentUserId ? x.UserTwoId : x.UserOneId,
                OtherUserName = x.UserOneId == currentUserId ? x.UserTwo.Name : x.UserOne.Name,
                CreatedAt = x.CreatedAt,
                LastMessageAt = x.LastMessageAt,
                LastMessagePreview = x.Messages
                    .OrderByDescending(message => message.CreatedAt)
                    .Select(message => message.MessageType == ChatMessageType.Text
                        ? message.Text
                        : message.MessageType == ChatMessageType.Image ? "Image" : "Audio")
                    .FirstOrDefault(),
                LastMessageType = x.Messages
                    .OrderByDescending(message => message.CreatedAt)
                    .Select(message => (ChatMessageType?)message.MessageType)
                    .FirstOrDefault(),
                UnreadCount = x.Messages.Count(message =>
                    message.SenderId != currentUserId && message.ReadAt == null)
            })
            .FirstAsync(cancellationToken);
    }

    private static ChatMessageResponse MapMessage(ChatMessage message, string senderName)
    {
        return new ChatMessageResponse
        {
            ChatMessageId = message.ChatMessageId,
            ChatId = message.ChatId,
            SenderId = message.SenderId,
            SenderName = senderName,
            MessageType = message.MessageType,
            Text = message.Text,
            FileUrl = message.FilePath is null
                ? null
                : $"/api/chats/{message.ChatId}/messages/{message.ChatMessageId}/file",
            OriginalFileName = message.OriginalFileName,
            ContentType = message.ContentType,
            FileSizeInBytes = message.FileSizeInBytes,
            CreatedAt = message.CreatedAt,
            ReadAt = message.ReadAt
        };
    }

    private static void ValidateMessage(
        ChatMessageType messageType,
        string? text,
        IFormFile? file)
    {
        if (messageType == ChatMessageType.Text)
        {
            if (text is null)
            {
                throw new ArgumentException("Text is required for a text message.");
            }

            if (text.Length > MaxTextLength)
            {
                throw new ArgumentException($"Text cannot exceed {MaxTextLength} characters.");
            }

            if (file is not null)
            {
                throw new ArgumentException("A text message cannot contain a file.");
            }

            return;
        }

        if (text is not null)
        {
            throw new ArgumentException("Image and audio messages cannot contain text.");
        }

        if (file is null)
        {
            throw new ArgumentException("File is required for image and audio messages.");
        }
    }

    private static (Guid UserOneId, Guid UserTwoId) NormalizeParticipants(Guid first, Guid second) =>
        first.CompareTo(second) < 0 ? (first, second) : (second, first);

    private static Guid GetCurrentUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid token.");
        }

        return userId;
    }

    private static void NormalizePaging(ref int page, ref int pageSize)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
    }
}
