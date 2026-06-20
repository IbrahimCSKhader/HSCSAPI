using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Chats;
using Microsoft.AspNetCore.Http;

namespace HSCSAPI.Tests;

public class ChatFileStorageTests
{
    [Fact]
    public void Constructor_CreatesChatsRootInsideContentRootWwwroot()
    {
        using var context = new ChatTestContext();

        Assert.True(Directory.Exists(Path.Combine(context.ContentRootPath, "wwwroot", "chats")));
    }

    [Fact]
    public async Task SaveImage_UsesGeneratedNameAndSanitizesOriginalName()
    {
        using var context = new ChatTestContext();
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var file = ChatTestContext.FormFile(
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01],
            "../private.png",
            "image/png");

        var result = await context.FileStorage.SaveAsync(chatId, messageId, ChatMessageType.Image, file);

        Assert.Equal("private.png", result.OriginalFileName);
        Assert.EndsWith($"/{messageId:N}.png", result.RelativePath, StringComparison.Ordinal);
        Assert.True(File.Exists(context.FileStorage.ResolvePhysicalPath(result.RelativePath)));
    }

    [Theory]
    [InlineData("audio/mpeg", "voice.mp3", new byte[] { 0x49, 0x44, 0x33, 0x04 })]
    [InlineData("audio/wav", "voice.wav", new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x41, 0x56, 0x45 })]
    [InlineData("audio/ogg", "voice.ogg", new byte[] { 0x4F, 0x67, 0x67, 0x53 })]
    [InlineData("audio/webm", "voice.webm", new byte[] { 0x1A, 0x45, 0xDF, 0xA3 })]
    public async Task SaveAudio_AcceptsSupportedSignatures(
        string contentType,
        string fileName,
        byte[] content)
    {
        using var context = new ChatTestContext();
        var file = ChatTestContext.FormFile(content, fileName, contentType);

        var result = await context.FileStorage.SaveAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ChatMessageType.Audio,
            file);

        Assert.Equal(contentType, result.ContentType);
        Assert.True(File.Exists(context.FileStorage.ResolvePhysicalPath(result.RelativePath)));
    }

    [Theory]
    [InlineData(ChatMessageType.Image, "application/pdf")]
    [InlineData(ChatMessageType.Audio, "application/octet-stream")]
    public async Task Save_RejectsUnsupportedContentType(ChatMessageType messageType, string contentType)
    {
        using var context = new ChatTestContext();
        var file = ChatTestContext.FormFile([1, 2, 3], "file.bin", contentType);

        await Assert.ThrowsAsync<ArgumentException>(() => context.FileStorage.SaveAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            messageType,
            file));
    }

    [Fact]
    public async Task Save_RejectsSpoofedContentType()
    {
        using var context = new ChatTestContext();
        var file = ChatTestContext.FormFile([1, 2, 3, 4], "fake.png", "image/png");

        var error = await Assert.ThrowsAsync<ArgumentException>(() => context.FileStorage.SaveAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ChatMessageType.Image,
            file));

        Assert.Contains("does not match", error.Message);
    }

    [Theory]
    [InlineData(ChatMessageType.Image, ChatFileStorage.MaxImageSize)]
    [InlineData(ChatMessageType.Audio, ChatFileStorage.MaxAudioSize)]
    public async Task Save_RejectsOversizedFiles(ChatMessageType messageType, long maximumSize)
    {
        using var context = new ChatTestContext();
        var file = new FormFile(Stream.Null, 0, maximumSize + 1, "File", "large.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = messageType == ChatMessageType.Image ? "image/png" : "audio/mpeg"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => context.FileStorage.SaveAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            messageType,
            file));
    }

    [Fact]
    public void ResolvePhysicalPath_RejectsTraversalOutsideChatsRoot()
    {
        using var context = new ChatTestContext();

        Assert.Throws<InvalidOperationException>(() =>
            context.FileStorage.ResolvePhysicalPath("wwwroot/chats/../../appsettings.json"));
    }
}
