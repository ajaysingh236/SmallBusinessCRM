using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Hubs;

[Authorize]
public sealed class CrmHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(RolesCatalog.Admin) == true || Context.User?.IsInRole(RolesCatalog.Manager) == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, "Leadership");
        await base.OnConnectedAsync();
    }
}
