using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Services;

public interface ILeadScoringService
{
    Task<int?> RecalculateAsync(int leadId, CancellationToken ct = default);
    Task<int> RecalculateAllAsync(CancellationToken ct = default);
}

public class LeadScoringService(
    CrmDbContext db,
    IRealtimeNotifier realtime,
    ILogger<LeadScoringService> logger) : ILeadScoringService
{
    public async Task<int?> RecalculateAsync(int leadId, CancellationToken ct = default)
    {
        const int maxRetries = 3;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var lead = await db.Leads
                    .Include(x => x.Tasks)
                    .Include(x => x.ActivityLogs)
                    .FirstOrDefaultAsync(x => x.Id == leadId, ct);

                if (lead is null) return null;

                var now = DateTime.UtcNow;
                var score = 0;
                score += lead.Value >= 50000 ? 35 : lead.Value >= 10000 ? 20 : 8;

                var recentTouches = lead.ActivityLogs.Count(x => x.OccurredUtc >= now.AddDays(-30));
                score += Math.Min(25, recentTouches * 5);

                score += lead.Tasks.Any(x => !x.IsCompleted && x.DueDateUtc >= now && x.DueDateUtc <= now.AddDays(7)) ? 15 : 0;
                score -= lead.Tasks.Count(x => !x.IsCompleted && x.DueDateUtc < now) * 8;

                if (!lead.ActivityLogs.Any(x => x.OccurredUtc >= now.AddDays(-30))) score -= 10;
                if (lead.Status == LeadStatus.DemoScheduled || lead.Status == LeadStatus.ProposalSent) score += 10;

                lead.PriorityScore = Math.Clamp(score, 0, 100);
                lead.LastUpdatedUtc = now;

                db.ActivityLogs.Add(new ActivityLog
                {
                    LeadId = lead.Id,
                    Type = ActivityType.System,
                    Message = $"Priority score recalculated to {lead.PriorityScore}.",
                    OccurredUtc = now
                });

                await db.SaveChangesAsync(ct);

                await realtime.LeadScoredAsync(new
                {
                    leadId = lead.Id,
                    priorityScore = lead.PriorityScore
                }, ct);

                return lead.PriorityScore;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "Concurrency conflict during priority score recalculation for Lead {LeadId}. Retry attempt {Attempt}/{MaxRetries}...", leadId, attempt, maxRetries);
                foreach (var entry in ex.Entries)
                {
                    await entry.ReloadAsync(ct);
                }
            }
        }

        return null;
    }

    public async Task<int> RecalculateAllAsync(CancellationToken ct = default)
    {
        var ids = await db.Leads.Select(x => x.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            await RecalculateAsync(id, ct);
        }
        return ids.Count;
    }
}
