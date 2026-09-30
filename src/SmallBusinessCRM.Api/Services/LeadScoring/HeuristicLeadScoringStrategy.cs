using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Services.LeadScoring;

public class HeuristicLeadScoringStrategy : ILeadScoringStrategy
{
    public string StrategyName => "RuleBasedHeuristic";

    public Task<LeadScoringResult> ScoreLeadAsync(Lead lead, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var factors = new List<ScoringFactor>();
        var score = 0;

        // 1. Deal Value Factor
        if (lead.Value >= 50000)
        {
            score += 35;
            factors.Add(new ScoringFactor("HighDealValue", 0.35, $"Deal value of ${lead.Value:N0} is top tier (+$50k).", "DealValue"));
        }
        else if (lead.Value >= 10000)
        {
            score += 20;
            factors.Add(new ScoringFactor("MediumDealValue", 0.20, $"Deal value of ${lead.Value:N0} represents a mid-market account.", "DealValue"));
        }
        else
        {
            score += 8;
            factors.Add(new ScoringFactor("EntryDealValue", 0.08, $"Deal value of ${lead.Value:N0} is an entry level opportunity.", "DealValue"));
        }

        // 2. Recent Engagement Touches
        var recentTouches = lead.ActivityLogs.Count(x => x.OccurredUtc >= now.AddDays(-30));
        var touchScore = Math.Min(25, recentTouches * 5);
        score += touchScore;
        if (recentTouches > 0)
        {
            factors.Add(new ScoringFactor("RecentActivity", touchScore / 100.0, $"{recentTouches} customer interactions logged in the last 30 days.", "Engagement"));
        }
        else
        {
            score -= 10;
            factors.Add(new ScoringFactor("StaleOpportunity", -0.10, "No customer interaction recorded in the past 30 days.", "Engagement"));
        }

        // 3. Upcoming and Overdue Task Momentum
        var upcomingTasks = lead.Tasks.Count(x => !x.IsCompleted && x.DueDateUtc >= now && x.DueDateUtc <= now.AddDays(7));
        if (upcomingTasks > 0)
        {
            score += 15;
            factors.Add(new ScoringFactor("ActiveFollowUp", 0.15, $"{upcomingTasks} active task(s) scheduled within the next 7 days.", "Momentum"));
        }

        var overdueTasks = lead.Tasks.Count(x => !x.IsCompleted && x.DueDateUtc < now);
        if (overdueTasks > 0)
        {
            var overduePenalty = overdueTasks * 8;
            score -= overduePenalty;
            factors.Add(new ScoringFactor("OverdueTasks", -(overduePenalty / 100.0), $"{overdueTasks} task(s) are overdue without completion.", "Momentum"));
        }

        // 4. Pipeline Stage Advancement
        if (lead.Status == LeadStatus.DemoScheduled || lead.Status == LeadStatus.ProposalSent)
        {
            score += 10;
            factors.Add(new ScoringFactor("LateStagePipeline", 0.10, $"Opportunity reached {lead.Status} stage.", "PipelineStage"));
        }

        var clampedScore = Math.Clamp(score, 0, 100);
        var probability = Math.Round(clampedScore / 100.0, 2);

        int? closeDays = lead.Status switch
        {
            LeadStatus.ProposalSent => 7,
            LeadStatus.DemoScheduled => 14,
            LeadStatus.Contacted => 30,
            LeadStatus.New => 45,
            _ => null
        };

        return Task.FromResult(new LeadScoringResult(
            clampedScore,
            probability,
            closeDays,
            factors,
            StrategyName
        ));
    }
}
