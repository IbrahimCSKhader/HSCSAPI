using System.Security.Claims;
using HSCSAPI.Controllers;
using HSCSAPI.DTOs.Chat;
using HSCSAPI.Hub;
using HSCSAPI.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace HSCSAPI.Tests;

public class ChatHubAndControllerTests
{
    [Fact]
    public async Task ChatHub_OnConnectedJoinsEveryExistingChatGroup()
    {
        using var context = new ChatTestContext();
        var first = context.AddUser("First");
        var second = context.AddUser("Second");
        var third = context.AddUser("Third");
        var firstChat = await context.Service.OpenChatAsync(second.Id, ChatTestContext.Principal(first.Id));
        var secondChat = await context.Service.OpenChatAsync(third.Id, ChatTestContext.Principal(first.Id));
        var hubContext = new TestHubCallerContext(ChatTestContext.Principal(first.Id));
        var groupManager = new RecordingGroupManager();
        var hub = CreateHub(context, hubContext, groupManager);

        await hub.OnConnectedAsync();

        Assert.Contains(groupManager.Added, x => x.GroupName == $"chat:{firstChat.ChatId:N}");
        Assert.Contains(groupManager.Added, x => x.GroupName == $"chat:{secondChat.ChatId:N}");
    }

    [Fact]
    public async Task ChatHub_JoinChatRejectsNonMember()
    {
        using var context = new ChatTestContext();
        var first = context.AddUser("First");
        var second = context.AddUser("Second");
        var outsider = context.AddUser("Outsider");
        var chat = await context.Service.OpenChatAsync(second.Id, ChatTestContext.Principal(first.Id));
        var hub = CreateHub(
            context,
            new TestHubCallerContext(ChatTestContext.Principal(outsider.Id)),
            new RecordingGroupManager());

        await Assert.ThrowsAsync<HubException>(() => hub.JoinChat(chat.ChatId));
    }

    [Fact]
    public async Task ChatHub_SendTextUsesTokenIdentityAndPersistsMessage()
    {
        using var context = new ChatTestContext();
        var first = context.AddUser("First");
        var second = context.AddUser("Second");
        var chat = await context.Service.OpenChatAsync(second.Id, ChatTestContext.Principal(first.Id));
        var hub = CreateHub(
            context,
            new TestHubCallerContext(ChatTestContext.Principal(first.Id)),
            new RecordingGroupManager());

        var response = await hub.SendTextMessage(chat.ChatId, "from hub");

        Assert.Equal(first.Id, response.SenderId);
        Assert.Equal("from hub", context.DbContext.ChatMessages.Single().Text);
    }

    [Fact]
    public async Task ChatHub_SetTypingBroadcastsOnlyAfterMembershipCheck()
    {
        using var context = new ChatTestContext();
        var first = context.AddUser("First");
        var second = context.AddUser("Second");
        var chat = await context.Service.OpenChatAsync(second.Id, ChatTestContext.Principal(first.Id));
        var callerClients = new RecordingHubCallerClients(context.ChatHub.Proxy);
        var hub = new ChatHub(context.Service)
        {
            Context = new TestHubCallerContext(ChatTestContext.Principal(first.Id)),
            Groups = new RecordingGroupManager(),
            Clients = callerClients
        };

        await hub.SetTyping(chat.ChatId, true);

        Assert.Contains(context.ChatHub.Proxy.Calls, x => x.Method == "TypingChanged");
    }

    [Fact]
    public async Task NotificationHub_AbortsConnectionWithoutTokenIdentity()
    {
        var callerContext = new TestHubCallerContext(
            new ClaimsPrincipal(new ClaimsIdentity()),
            userIdentifier: null);
        var hub = new NotificationHub { Context = callerContext };

        await Assert.ThrowsAsync<HubException>(() => hub.OnConnectedAsync());

        Assert.True(callerContext.AbortCalled);
    }

    [Fact]
    public async Task ChatsController_ReturnsRangeEnabledProtectedPhysicalFile()
    {
        using var context = new ChatTestContext();
        var first = context.AddUser("First");
        var second = context.AddUser("Second");
        var principal = ChatTestContext.Principal(first.Id);
        var chat = await context.Service.OpenChatAsync(second.Id, principal);
        var image = ChatTestContext.FormFile(
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01],
            "photo.png",
            "image/png");
        var message = await context.Service.SendMessageAsync(
            chat.ChatId,
            ChatMessageType.Image,
            null,
            image,
            principal);
        var controller = new ChatsController(context.Service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };

        var action = await controller.GetFile(chat.ChatId, message.ChatMessageId, CancellationToken.None);

        var file = Assert.IsType<PhysicalFileResult>(action);
        Assert.True(file.EnableRangeProcessing);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal("photo.png", file.FileDownloadName);
    }

    private static ChatHub CreateHub(
        ChatTestContext context,
        TestHubCallerContext callerContext,
        RecordingGroupManager groups)
    {
        return new ChatHub(context.Service)
        {
            Context = callerContext,
            Groups = groups,
            Clients = new RecordingHubCallerClients(context.ChatHub.Proxy)
        };
    }
}
