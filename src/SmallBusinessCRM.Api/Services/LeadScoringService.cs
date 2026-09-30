using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services.LeadScoring;

namespace SmallBusinessCRM.Api.Services;

public interface ILeadScoringService
{
    Task<int?> RecalculateAsync(int leadId, CancellationToken ct = default);
    Task<LeadScoringResult?> ScoreLeadAsync(int leadId, string? strategyName = null, CancellationToken ct = default);
    Task<int> RecalculateAllAsync(CancellationToken ct = default);
}

public class LeadScoringService(
    CrmDbContext db,
    IEnumerable<ILeadScoringStrategy> strategies,
    IRealtimeNotifier realtime,
    ILogger<LeadScoringService> logger) : ILeadScoringService
{
    private readonly List<ILeadScoringStrategy> _strategies = strategies.ToList();

    public async Task<int?> RecalculateAsync(int leadId, CancellationToken ct = default)
    {
        var result = await ScoreLeadAsync(leadId, null, ct);
        return result?.PriorityScore;
    }

    public async Task<LeadScoringResult?> ScoreLeadAsync(int leadId, string? strategyName = null, CancellationToken ct = default)
    {
        var strategy = !string.IsNullOrWhiteSpace(strategyName)
            ? _strategies.FirstOrDefault(s => string.Equals(s.StrategyName, strategyName, StringComparison.OrdinalIgnoreCase))
            : _strategies.FirstOrDefault(s => s is MachineLearningLeadScoringStrategy) ?? _strategies.FirstOrDefault();

        if (strategy is null)
        {
            strategy = new MachineLearningLeadScoringStrategy();
        }

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
                var scoringResult = await strategy.ScoreLeadAsync(lead, ct);

                lead.PriorityScore = scoringResult.PriorityScore;
                lead.ConversionProbability = scoringResult.ConversionProbability;
                lead.PredictedCloseDays = scoringResult.PredictedCloseDays;
                lead.AiScoreExplanation = JsonSerializer.Serialize(scoringResult.Factors);
                lead.LastUpdatedUtc = now;

                db.ActivityLogs.Add(new ActivityLog
                {
                    LeadId = lead.Id,
                    Type = ActivityType.System,
                    Message = $"AI Lead Scoring ({strategy.StrategyName}): Priority {lead.PriorityScore}/100, Win Probability {lead.ConversionProbability:P0}, Est. Close {lead.PredictedCloseDays} days.",
                    OccurredUtc = now
                });

                await db.SaveChangesAsync(ct);

                await realtime.LeadScoredAsync(new
                {
                    leadId = lead.Id,
                    priorityScore = lead.PriorityScore,
                    conversionProbability = lead.ConversionProbability,
                    predictedCloseDays = lead.PredictedCloseDays,
                    factors = scoringResult.Factors,
                    strategy = strategy.StrategyName
                }, ct);

                logger.LogInformation(
                    "Lead {LeadId} scored via {Strategy}: Priority={Score}, WinProbability={Prob:P0}",
                    lead.Id, strategy.StrategyName, lead.PriorityScore, lead.ConversionProbability);

                return scoringResult;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "Concurrency conflict during AI lead score recalculation for Lead {LeadId}. Retry attempt {Attempt}/{MaxRetries}...", leadId, attempt, maxRetries);
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
            await ScoreLeadAsync(id, null, ct);
        }
        return ids.Count;
    }
}
