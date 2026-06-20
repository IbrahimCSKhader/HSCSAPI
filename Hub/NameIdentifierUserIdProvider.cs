using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace HSCSAPI.Hub;

public class NameIdentifierUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
