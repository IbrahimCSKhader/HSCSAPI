using HSCSAPI.DTOs.Chat;
using HSCSAPI.Services.Chats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/chats")]
[Authorize]
public class ChatsController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatsController(IChatService chatService)
    {
        _chatService = chatService;
    }

    // last end point added - already-added
    [HttpPost("open")]
    public Task<ChatResponse> OpenChat(
        [FromBody] OpenChatRequest request,
        CancellationToken cancellationToken) =>
        _chatService.OpenChatAsync(request.RecipientUserId, User, cancellationToken);

    [HttpGet]
    public Task<List<ChatResponse>> GetChats(CancellationToken cancellationToken) =>
        _chatService.GetChatsAsync(User, cancellationToken);

    [HttpGet("{chatId:guid}/messages")]
    public Task<ChatMessagesPageResponse> GetMessages(
        Guid chatId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        _chatService.GetMessagesAsync(chatId, page, pageSize, User, cancellationToken);

    [HttpPost("{chatId:guid}/messages")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(27 * 1024 * 1024)]
    public Task<ChatMessageResponse> SendMessage(
        Guid chatId,
        [FromForm] SendChatMessageRequest request,
        CancellationToken cancellationToken) =>
        _chatService.SendMessageAsync(
            chatId,
            request.MessageType,
            request.Text,
            request.File,
            User,
            cancellationToken);

    // last end point added
    [HttpPut("{chatId:guid}/messages/{messageId:guid}")]
    public Task<ChatMessageResponse> EditMessage(
        Guid chatId,
        Guid messageId,
        [FromBody] EditChatMessageRequest request,
        CancellationToken cancellationToken) =>
        _chatService.EditMessageAsync(chatId, messageId, request.Text, User, cancellationToken);

    // last end point added
    [HttpDelete("{chatId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> UnsendMessage(Guid chatId, Guid messageId, CancellationToken cancellationToken)
    {
        await _chatService.UnsendMessageAsync(chatId, messageId, User, cancellationToken);
        return NoContent();
    }

    [HttpPost("{chatId:guid}/read")]
    public Task<MarkChatReadResponse> MarkAsRead(
        Guid chatId,
        CancellationToken cancellationToken) =>
        _chatService.MarkAsReadAsync(chatId, User, cancellationToken);

    [HttpGet("{chatId:guid}/messages/{messageId:guid}/file")]
    public async Task<IActionResult> GetFile(
        Guid chatId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var file = await _chatService.GetFileAsync(chatId, messageId, User, cancellationToken);
        return new PhysicalFileResult(file.PhysicalPath, file.ContentType)
        {
            FileDownloadName = file.OriginalFileName,
            EnableRangeProcessing = true
        };
    }
}
