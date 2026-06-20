using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.Hub;
using HSCSAPI.Models.Identity;
using HSCSAPI.Services.Chats;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http.Features;

namespace HSCSAPI.Tests;

internal class ChatTestContext : IDisposable
{
    public ChatTestContext(bool throwOnBroadcast = false)
    {
        ContentRootPath = Path.Combine(Path.GetTempPath(), "hscsapi-chat-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ContentRootPath);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        DbContext = new AppDbContext(options);
        DbContext.Database.EnsureCreated();
        FileStorage = new ChatFileStorage(new TestWebHostEnvironment(ContentRootPath));
        ChatHub = new RecordingHubContext<ChatHub>(throwOnBroadcast);
        NotificationHub = new RecordingHubContext<NotificationHub>(throwOnBroadcast);
        Service = new ChatService(
            DbContext,
            FileStorage,
            ChatHub,
            NotificationHub,
            NullLogger<ChatService>.Instance);
    }

    public string ContentRootPath { get; }
    public AppDbContext DbContext { get; }
    public ChatFileStorage FileStorage { get; }
    public RecordingHubContext<ChatHub> ChatHub { get; }
    public RecordingHubContext<NotificationHub> NotificationHub { get; }
    public ChatService Service { get; }

    public User AddUser(string name)
    {
        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Name = name,
            UserName = $"{id:N}@test.local",
            NormalizedUserName = $"{id:N}@TEST.LOCAL",
            Email = $"{id:N}@test.local",
            NormalizedEmail = $"{id:N}@TEST.LOCAL",
            EmailConfirmed = true,
            RegisteredAt = DateTime.UtcNow
        };

        DbContext.Users.Add(user);
        DbContext.SaveChanges();
        return user;
    }

    public static ClaimsPrincipal Principal(Guid userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "Test"));
    }

    public static FormFile FormFile(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "File", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    public void Dispose()
    {
        DbContext.Dispose();
        if (Directory.Exists(ContentRootPath))
        {
            Directory.Delete(ContentRootPath, recursive: true);
        }
    }
}

internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public TestWebHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
        WebRootPath = Path.Combine(contentRootPath, "wwwroot");
    }

    public string ApplicationName { get; set; } = "HSCSAPI.Tests";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; }
    public string EnvironmentName { get; set; } = "Testing";
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class RecordingHubContext<THub> : IHubContext<THub>
    where THub : Microsoft.AspNetCore.SignalR.Hub
{
    public RecordingHubContext(bool throwOnSend = false)
    {
        Proxy = new RecordingClientProxy(throwOnSend);
        Clients = new RecordingHubClients(Proxy);
        Groups = new RecordingGroupManager();
    }

    public RecordingClientProxy Proxy { get; }
    public IHubClients Clients { get; }
    public IGroupManager Groups { get; }
}

internal sealed class RecordingClientProxy : IClientProxy
{
    private readonly bool _throwOnSend;

    public RecordingClientProxy(bool throwOnSend)
    {
        _throwOnSend = throwOnSend;
    }

    public List<(string Method, object?[] Arguments)> Calls { get; } = [];

    public Task SendCoreAsync(
        string method,
        object?[] args,
        CancellationToken cancellationToken = default)
    {
        if (_throwOnSend)
        {
            throw new InvalidOperationException("Simulated SignalR failure.");
        }

        Calls.Add((method, args));
        return Task.CompletedTask;
    }
}

internal sealed class RecordingHubClients : IHubClients
{
    private readonly IClientProxy _proxy;

    public RecordingHubClients(IClientProxy proxy)
    {
        _proxy = proxy;
    }

    public IClientProxy All => _proxy;
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;
    public IClientProxy Client(string connectionId) => _proxy;
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
    public IClientProxy Group(string groupName) => _proxy;
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
    public IClientProxy User(string userId) => _proxy;
    public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
}

internal sealed class RecordingGroupManager : IGroupManager
{
    public List<(string ConnectionId, string GroupName)> Added { get; } = [];
    public List<(string ConnectionId, string GroupName)> Removed { get; } = [];

    public Task AddToGroupAsync(
        string connectionId,
        string groupName,
        CancellationToken cancellationToken = default)
    {
        Added.Add((connectionId, groupName));
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(
        string connectionId,
        string groupName,
        CancellationToken cancellationToken = default)
    {
        Removed.Add((connectionId, groupName));
        return Task.CompletedTask;
    }
}

internal sealed class RecordingHubCallerClients : IHubCallerClients
{
    private readonly IClientProxy _proxy;

    public RecordingHubCallerClients(IClientProxy proxy)
    {
        _proxy = proxy;
    }

    public IClientProxy All => _proxy;
    public IClientProxy Caller => _proxy;
    public IClientProxy Others => _proxy;
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;
    public IClientProxy Client(string connectionId) => _proxy;
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
    public IClientProxy Group(string groupName) => _proxy;
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
    public IClientProxy OthersInGroup(string groupName) => _proxy;
    public IClientProxy User(string userId) => _proxy;
    public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
}

internal sealed class TestHubCallerContext : HubCallerContext
{
    private readonly CancellationTokenSource _connectionCancellation = new();
    private readonly IDictionary<object, object?> _items = new Dictionary<object, object?>();

    public TestHubCallerContext(ClaimsPrincipal user, string? userIdentifier = null)
    {
        User = user;
        UserIdentifier = userIdentifier ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public bool AbortCalled { get; private set; }
    public override string ConnectionId { get; } = Guid.NewGuid().ToString("N");
    public override string? UserIdentifier { get; }
    public override ClaimsPrincipal? User { get; }
    public override IDictionary<object, object?> Items => _items;
    public override IFeatureCollection Features { get; } = new FeatureCollection();
    public override CancellationToken ConnectionAborted => _connectionCancellation.Token;

    public override void Abort()
    {
        AbortCalled = true;
        _connectionCancellation.Cancel();
    }
}
