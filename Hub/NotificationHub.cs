using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HSCSAPI.Hub;

[Authorize]
public class NotificationHub : Microsoft.AspNetCore.SignalR.Hub
{
    public override Task OnConnectedAsync()
    {
        if (string.IsNullOrWhiteSpace(Context.UserIdentifier))
        {
            Context.Abort();
            throw new HubException("Invalid token.");
        }

        return base.OnConnectedAsync();
    }
}
