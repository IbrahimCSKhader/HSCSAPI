using System.Security.Claims;
using HSCSAPI.DTOs.Chat;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Chats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HSCSAPI.Hub;

[Authorize]
public class ChatHub : Microsoft.AspNetCore.SignalR.Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        var chatIds = await _chatService.GetChatIdsForUserAsync(userId, Context.ConnectionAborted);
        foreach (var chatId in chatIds)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetChatGroup(chatId));
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinChat(Guid chatId)
    {
        if (!await _chatService.IsMemberAsync(chatId, GetCurrentUserId(), Context.ConnectionAborted))
        {
            throw new HubException("You are not a member of this chat.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetChatGroup(chatId));
    }

    public Task LeaveChat(Guid chatId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GetChatGroup(chatId));

    public Task<ChatMessageResponse> SendTextMessage(Guid chatId, string text) =>
        _chatService.SendMessageAsync(
            chatId,
            ChatMessageType.Text,
            text,
            file: null,
            Context.User!,
            Context.ConnectionAborted);

    public Task<MarkChatReadResponse> MarkAsRead(Guid chatId) =>
        _chatService.MarkAsReadAsync(chatId, Context.User!, Context.ConnectionAborted);

    public async Task SetTyping(Guid chatId, bool isTyping)
    {
        var userId = GetCurrentUserId();
        if (!await _chatService.IsMemberAsync(chatId, userId, Context.ConnectionAborted))
        {
            throw new HubException("You are not a member of this chat.");
        }

        await Clients.OthersInGroup(GetChatGroup(chatId))
            .SendAsync("TypingChanged", new { chatId, userId, isTyping }, Context.ConnectionAborted);
    }

    private Guid GetCurrentUserId()
    {
        var claim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
        {
            Context.Abort();
            throw new HubException("Invalid token.");
        }

        return userId;
    }

    private static string GetChatGroup(Guid chatId) => $"chat:{chatId:N}";
}
