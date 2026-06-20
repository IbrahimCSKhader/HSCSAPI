using System.Security.Claims;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Chats;
using Microsoft.AspNetCore.Http;

namespace HSCSAPI.Tests;

public class ChatServiceTests
{
    [Fact]
    public async Task OpenChat_CreatesOneNormalizedChatAndServerFolder()
    {
        using var context = new ChatTestContext();
        var first = context.AddUser("First User");
        var second = context.AddUser("Second User");

        var opened = await context.Service.OpenChatAsync(second.Id, ChatTestContext.Principal(first.Id));
        var reopenedFromOtherSide = await context.Service.OpenChatAsync(first.Id, ChatTestContext.Principal(second.Id));

        Assert.Equal(opened.ChatId, reopenedFromOtherSide.ChatId);
        Assert.Single(context.DbContext.Chats);
        Assert.True(Directory.Exists(Path.Combine(
            context.ContentRootPath,
            "wwwroot",
            "chats",
            opened.ChatId.ToString("N"))));
    }

    [Fact]
    public async Task OpenChat_RejectsSelf()
    {
        using var context = new ChatTestContext();
        var user = context.AddUser("User");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            context.Service.OpenChatAsync(user.Id, ChatTestContext.Principal(user.Id)));
    }

    [Fact]
    public async Task OpenChat_RejectsUnknownRecipient()
    {
        using var context = new ChatTestContext();
        var user = context.AddUser("User");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            context.Service.OpenChatAsync(Guid.NewGuid(), ChatTestContext.Principal(user.Id)));
    }

    [Fact]
    public async Task OpenChat_RejectsInvalidTokenIdentity()
    {
        using var context = new ChatTestContext();
        var recipient = context.AddUser("Recipient");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.OpenChatAsync(recipient.Id, new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public async Task SendText_TrimsPersistsNotifiesAndBroadcasts()
    {
        using var context = new ChatTestContext();
        var sender = context.AddUser("Sender");
        var recipient = context.AddUser("Recipient");
        var chat = await context.Service.OpenChatAsync(recipient.Id, ChatTestContext.Principal(sender.Id));

        var response = await context.Service.SendMessageAsync(
            chat.ChatId,
            ChatMessageType.Text,
            "  hello  ",
            null,
            ChatTestContext.Principal(sender.Id));

        Assert.Equal("hello", response.Text);
        Assert.Null(response.FileUrl);
        Assert.Single(context.DbContext.ChatMessages);
        var notification = Assert.Single(context.DbContext.Notifications.Where(x => x.UserId == recipient.Id));
        Assert.Contains("Sender", notification.Title);
        Assert.Contains(context.ChatHub.Proxy.Calls, x => x.Method == "ReceiveMessage");
        Assert.Contains(context.NotificationHub.Proxy.Calls, x => x.Method == "ReceiveNotification");
    }

    [Fact]
    public async Task SendText_RejectsWhitespaceOnlyText()
    {
        using var context = await CreateOpenChatContextAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => context.Service.SendMessageAsync(
            context.ChatId,
            ChatMessageType.Text,
            "   ",
            null,
            ChatTestContext.Principal(context.FirstUserId)));
    }

    [Fact]
    public async Task SendText_RejectsAttachedFile()
    {
        using var context = await CreateOpenChatContextAsync();
        var image = ValidPng();

        await Assert.ThrowsAsync<ArgumentException>(() => context.Service.SendMessageAsync(
            context.ChatId,
            ChatMessageType.Text,
            "hello",
            image,
            ChatTestContext.Principal(context.FirstUserId)));
    }

    [Theory]
    [InlineData(ChatMessageType.Image)]
    [InlineData(ChatMessageType.Audio)]
    public async Task SendMedia_RequiresAFile(ChatMessageType messageType)
    {
        using var context = await CreateOpenChatContextAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => context.Service.SendMessageAsync(
            context.ChatId,
            messageType,
            null,
            null,
            ChatTestContext.Principal(context.FirstUserId)));
    }

    [Fact]
    public async Task SendMedia_RejectsTextCaption()
    {
        using var context = await CreateOpenChatContextAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => context.Service.SendMessageAsync(
            context.ChatId,
            ChatMessageType.Image,
            "caption",
            ValidPng(),
            ChatTestContext.Principal(context.FirstUserId)));
    }

    [Fact]
    public async Task SendMessage_RejectsUndefinedMessageType()
    {
        using var context = await CreateOpenChatContextAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => context.Service.SendMessageAsync(
            context.ChatId,
            (ChatMessageType)999,
            "hello",
            null,
            ChatTestContext.Principal(context.FirstUserId)));
    }

    [Fact]
    public async Task SendImage_PersistsProtectedFileMetadataAndReturnsDownloadRoute()
    {
        using var context = await CreateOpenChatContextAsync();

        var response = await context.Service.SendMessageAsync(
            context.ChatId,
            ChatMessageType.Image,
            null,
            ValidPng(),
            ChatTestContext.Principal(context.FirstUserId));
        var download = await context.Service.GetFileAsync(
            context.ChatId,
            response.ChatMessageId,
            ChatTestContext.Principal(context.SecondUserId));

        Assert.Equal("image/png", response.ContentType);
        Assert.Equal($"/api/chats/{context.ChatId}/messages/{response.ChatMessageId}/file", response.FileUrl);
        Assert.True(File.Exists(download.PhysicalPath));
        Assert.Equal("photo.png", download.OriginalFileName);
    }

    [Fact]
    public async Task SendAudio_PersistsSupportedVoiceFile()
    {
        using var context = await CreateOpenChatContextAsync();
        var audio = ChatTestContext.FormFile([0x49, 0x44, 0x33, 0x04], "voice.mp3", "audio/mpeg");

        var response = await context.Service.SendMessageAsync(
            context.ChatId,
            ChatMessageType.Audio,
            null,
            audio,
            ChatTestContext.Principal(context.FirstUserId));

        Assert.Equal(ChatMessageType.Audio, response.MessageType);
        Assert.Equal("audio/mpeg", response.ContentType);
        Assert.EndsWith(".mp3", context.DbContext.ChatMessages.Single().FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectedSpoofedFile_DoesNotCreateMessageOrNotification()
    {
        using var context = await CreateOpenChatContextAsync();
        var spoofed = ChatTestContext.FormFile([1, 2, 3], "fake.png", "image/png");

        await Assert.ThrowsAsync<ArgumentException>(() => context.Service.SendMessageAsync(
            context.ChatId,
            ChatMessageType.Image,
            null,
            spoofed,
            ChatTestContext.Principal(context.FirstUserId)));

        Assert.Empty(context.DbContext.ChatMessages);
        Assert.Empty(context.DbContext.Notifications);
    }

    [Fact]
    public async Task NonMember_CannotReadSendMarkReadOrDownload()
    {
        using var context = await CreateOpenChatContextAsync();
        var outsider = context.AddUser("Outsider");
        var outsiderPrincipal = ChatTestContext.Principal(outsider.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.GetMessagesAsync(context.ChatId, 1, 50, outsiderPrincipal));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.SendMessageAsync(context.ChatId, ChatMessageType.Text, "no", null, outsiderPrincipal));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.MarkAsReadAsync(context.ChatId, outsiderPrincipal));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.GetFileAsync(context.ChatId, Guid.NewGuid(), outsiderPrincipal));
    }

    [Fact]
    public async Task MarkAsRead_OnlyMarksIncomingUnreadMessages()
    {
        using var context = await CreateOpenChatContextAsync();
        await context.Service.SendMessageAsync(
            context.ChatId,
            ChatMessageType.Text,
            "first to second",
            null,
            ChatTestContext.Principal(context.FirstUserId));
        await context.Service.SendMessageAsync(
            context.ChatId,
            ChatMessageType.Text,
            "second to first",
            null,
            ChatTestContext.Principal(context.SecondUserId));

        var result = await context.Service.MarkAsReadAsync(
            context.ChatId,
            ChatTestContext.Principal(context.SecondUserId));

        Assert.Equal(1, result.UpdatedMessages);
        Assert.NotNull(context.DbContext.ChatMessages.Single(x => x.SenderId == context.FirstUserId).ReadAt);
        Assert.Null(context.DbContext.ChatMessages.Single(x => x.SenderId == context.SecondUserId).ReadAt);
        Assert.Contains(context.ChatHub.Proxy.Calls, x => x.Method == "MessagesRead");
    }

    [Fact]
    public async Task GetChats_ReportsLatestMessageAndUnreadCount()
    {
        using var context = await CreateOpenChatContextAsync();
        await context.Service.SendMessageAsync(
            context.ChatId,
            ChatMessageType.Text,
            "latest",
            null,
            ChatTestContext.Principal(context.FirstUserId));

        var chats = await context.Service.GetChatsAsync(ChatTestContext.Principal(context.SecondUserId));

        var chat = Assert.Single(chats);
        Assert.Equal("latest", chat.LastMessagePreview);
        Assert.Equal(ChatMessageType.Text, chat.LastMessageType);
        Assert.Equal(1, chat.UnreadCount);
    }

    [Fact]
    public async Task GetMessages_NormalizesPagingAndReturnsChronologicalPage()
    {
        using var context = await CreateOpenChatContextAsync();
        var chat = context.DbContext.Chats.Single();
        for (var index = 0; index < 105; index++)
        {
            context.DbContext.ChatMessages.Add(new ChatMessage
            {
                ChatId = context.ChatId,
                SenderId = context.FirstUserId,
                MessageType = ChatMessageType.Text,
                Text = $"message-{index:D3}",
                CreatedAt = DateTime.UtcNow.AddSeconds(index)
            });
        }

        chat.LastMessageAt = DateTime.UtcNow.AddSeconds(105);
        await context.DbContext.SaveChangesAsync();

        var page = await context.Service.GetMessagesAsync(
            context.ChatId,
            page: 0,
            pageSize: 1000,
            ChatTestContext.Principal(context.SecondUserId));

        Assert.Equal(1, page.Page);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(105, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal("message-005", page.Items.First().Text);
        Assert.Equal("message-104", page.Items.Last().Text);
    }

    [Fact]
    public async Task GetFile_RejectsTextMessageAndMissingPhysicalFile()
    {
        using var context = await CreateOpenChatContextAsync();
        var text = await context.Service.SendMessageAsync(
            context.ChatId,
            ChatMessageType.Text,
            "text",
            null,
            ChatTestContext.Principal(context.FirstUserId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.GetFileAsync(
            context.ChatId,
            text.ChatMessageId,
            ChatTestContext.Principal(context.SecondUserId)));

        var image = await context.Service.SendMessageAsync(
            context.ChatId,
            ChatMessageType.Image,
            null,
            ValidPng(),
            ChatTestContext.Principal(context.FirstUserId));
        var stored = context.DbContext.ChatMessages.Single(x => x.ChatMessageId == image.ChatMessageId);
        File.Delete(context.FileStorage.ResolvePhysicalPath(stored.FilePath!));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => context.Service.GetFileAsync(
            context.ChatId,
            image.ChatMessageId,
            ChatTestContext.Principal(context.SecondUserId)));
    }

    [Fact]
    public async Task BroadcastFailure_DoesNotRollbackSavedMessageOrDeleteMedia()
    {
        using var context = new ChatTestContext(throwOnBroadcast: true);
        var first = context.AddUser("First");
        var second = context.AddUser("Second");
        var chat = await context.Service.OpenChatAsync(second.Id, ChatTestContext.Principal(first.Id));

        var response = await context.Service.SendMessageAsync(
            chat.ChatId,
            ChatMessageType.Image,
            null,
            ValidPng(),
            ChatTestContext.Principal(first.Id));
        var stored = context.DbContext.ChatMessages.Single(x => x.ChatMessageId == response.ChatMessageId);

        Assert.Single(context.DbContext.ChatMessages);
        Assert.True(File.Exists(context.FileStorage.ResolvePhysicalPath(stored.FilePath!)));
    }

    private static FormFile ValidPng() => ChatTestContext.FormFile(
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01],
        "photo.png",
        "image/png");

    private static async Task<OpenChatTestContext> CreateOpenChatContextAsync()
    {
        var context = new OpenChatTestContext();
        context.FirstUserId = context.AddUser("First").Id;
        context.SecondUserId = context.AddUser("Second").Id;
        var chat = await context.Service.OpenChatAsync(
            context.SecondUserId,
            ChatTestContext.Principal(context.FirstUserId));
        context.ChatId = chat.ChatId;
        return context;
    }

    private sealed class OpenChatTestContext : ChatTestContext
    {
        public Guid FirstUserId { get; set; }
        public Guid SecondUserId { get; set; }
        public Guid ChatId { get; set; }
    }
}
