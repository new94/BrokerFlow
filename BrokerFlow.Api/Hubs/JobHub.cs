using Microsoft.AspNetCore.SignalR;

namespace BrokerFlow.Api.Hubs;

public class JobHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
}
