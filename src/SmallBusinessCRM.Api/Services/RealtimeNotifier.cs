using Microsoft.AspNetCore.SignalR;
using SmallBusinessCRM.Api.Hubs;

namespace SmallBusinessCRM.Api.Services;

public interface IRealtimeNotifier
{
    Task LeadAssignedAsync(int userId, object alert, CancellationToken ct = default);
    Task ActivityAsync(object activity, CancellationToken ct = default);
    Task LeadStageChangedAsync(object stageChange, CancellationToken ct = default);
    Task LeadScoredAsync(object leadScore, CancellationToken ct = default);
}

public sealed class RealtimeNotifier(IHubContext<CrmHub> hub) : IRealtimeNotifier
{
    public Task LeadAssignedAsync(int userId, object alert, CancellationToken ct = default) =>
        hub.Clients.User(userId.ToString()).SendAsync("leadAssigned", alert, ct);

    public Task ActivityAsync(object activity, CancellationToken ct = default) =>
        hub.Clients.Group("Leadership").SendAsync("activityCreated", activity, ct);

    public Task LeadStageChangedAsync(object stageChange, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync("leadStageChanged", stageChange, ct);

    public Task LeadScoredAsync(object leadScore, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync("leadScored", leadScore, ct);
}
