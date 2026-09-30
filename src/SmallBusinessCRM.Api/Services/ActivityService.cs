using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Dtos;
using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Services;

public interface IActivityService
{
    Task<ActivityLog?> LogAsync(int leadId, LogActivityRequest request, int? userId = null, CancellationToken ct = default);
}

public class ActivityService(CrmDbContext db, ICurrentUserService currentUser) : IActivityService
{
    public async Task<ActivityLog?> LogAsync(int leadId, LogActivityRequest request, int? userId = null, CancellationToken ct = default)
    {
        if (!await db.Leads.AnyAsync(x => x.Id == leadId, ct)) return null;

        var resolvedUserId = userId ?? (currentUser.IsAuthenticated ? currentUser.UserId : null);

        var item = new ActivityLog
        {
            LeadId = leadId,
            UserId = resolvedUserId,
            Type = request.Type,
            Message = request.Message,
            OccurredUtc = DateTime.UtcNow
        };

        db.ActivityLogs.Add(item);
        await db.SaveChangesAsync(ct);
        return item;
    }
}
