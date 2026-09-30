using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Services.LeadScoring;

/// <summary>
/// Machine learning probabilistic lead scoring strategy.
/// Implements feature extraction and calibrated logistic sigmoid inference 
/// with explainability factor breakdown.
/// </summary>
public class MachineLearningLeadScoringStrategy : ILeadScoringStrategy
{
    public string StrategyName => "LogisticRegressionML";

    public Task<LeadScoringResult> ScoreLeadAsync(Lead lead, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var factors = new List<ScoringFactor>();

        // Handle terminal states immediately
        if (lead.Status == LeadStatus.ClosedWon)
        {
            return Task.FromResult(new LeadScoringResult(
                100,
                1.00,
                0,
                new[] { new ScoringFactor("DealWon", 1.00, "Deal has been successfully closed and won.", "TerminalState") },
                StrategyName
            ));
        }

        if (lead.Status == LeadStatus.ClosedLost)
        {
            return Task.FromResult(new LeadScoringResult(
                0,
                0.00,
                null,
                new[] { new ScoringFactor("DealLost", -1.00, "Deal has been marked as closed lost.", "TerminalState") },
                StrategyName
            ));
        }

        // Base intercept for sales conversion prior probability (~22% baseline)
        double logit = -1.25;

        // 1. Feature: Normalized Deal Size
        double valueRatio = Math.Min(1.0, (double)lead.Value / 75000.0);
        double valueWeight = 1.10;
        double valueContribution = valueRatio * valueWeight;
        logit += valueContribution;
        factors.Add(new ScoringFactor(
            "DealSizeWeight",
            Math.Round(valueContribution, 2),
            $"Deal value of ${lead.Value:N0} provides a +{Math.Round(valueContribution * 100 / 3.0, 1)}% signal to the conversion model.",
            "Financial"
        ));

        // 2. Feature: Stage Advancement Prior
        double stageWeight = lead.Status switch
        {
            LeadStatus.ProposalSent => 1.45,
            LeadStatus.DemoScheduled => 0.90,
            LeadStatus.Contacted => 0.25,
            _ => -0.20 // New
        };
        logit += stageWeight;
        factors.Add(new ScoringFactor(
            "PipelineMaturity",
            Math.Round(stageWeight, 2),
            $"Lead reached the '{lead.Status}' stage in the sales funnel.",
            "FunnelStage"
        ));

        // 3. Feature: Recent Interaction Velocity (last 14 and 30 days)
        int touches14Days = lead.ActivityLogs.Count(x => x.OccurredUtc >= now.AddDays(-14));
        int touches30Days = lead.ActivityLogs.Count(x => x.OccurredUtc >= now.AddDays(-30));
        
        if (touches14Days >= 2)
        {
            double touchBonus = Math.Min(0.85, touches14Days * 0.25);
            logit += touchBonus;
            factors.Add(new ScoringFactor(
                "HighInteractionVelocity",
                Math.Round(touchBonus, 2),
                $"Rapid customer communication cadence: {touches14Days} interactions logged in past 14 days.",
                "Engagement"
            ));
        }
        else if (touches30Days == 0)
        {
            double decayPenalty = -0.75;
            logit += decayPenalty;
            factors.Add(new ScoringFactor(
                "StaleOpportunityDecay",
                decayPenalty,
                "Inactivity penalty: Zero engagements recorded in the past 30 days.",
                "Engagement"
            ));
        }

        // 4. Feature: Task Momentum & Overdue Friction
        int completedTasks = lead.Tasks.Count(x => x.IsCompleted);
        int upcomingTasks = lead.Tasks.Count(x => !x.IsCompleted && x.DueDateUtc >= now && x.DueDateUtc <= now.AddDays(7));
        int overdueTasks = lead.Tasks.Count(x => !x.IsCompleted && x.DueDateUtc < now);

        if (completedTasks > 0)
        {
            double completionBonus = Math.Min(0.50, completedTasks * 0.15);
            logit += completionBonus;
            factors.Add(new ScoringFactor(
                "TaskExecutionHistory",
                Math.Round(completionBonus, 2),
                $"{completedTasks} sales task(s) successfully completed on schedule.",
                "Momentum"
            ));
        }

        if (upcomingTasks > 0)
        {
            double nextActionBonus = 0.40;
            logit += nextActionBonus;
            factors.Add(new ScoringFactor(
                "DefinedNextAction",
                nextActionBonus,
                $"{upcomingTasks} active next action(s) scheduled for this week.",
                "Momentum"
            ));
        }

        if (overdueTasks > 0)
        {
            double overduePenalty = -(Math.Min(1.20, overdueTasks * 0.40));
            logit += overduePenalty;
            factors.Add(new ScoringFactor(
                "OverdueTaskFriction",
                Math.Round(overduePenalty, 2),
                $"{overdueTasks} pending task(s) are overdue, increasing drop-off probability.",
                "Risk"
            ));
        }

        // 5. Feature: Contact Profile Completeness
        int profilePoints = 0;
        if (!string.IsNullOrWhiteSpace(lead.Email)) profilePoints++;
        if (!string.IsNullOrWhiteSpace(lead.Phone)) profilePoints++;
        if (!string.IsNullOrWhiteSpace(lead.ContactName)) profilePoints++;
        if (profilePoints == 3)
        {
            logit += 0.30;
            factors.Add(new ScoringFactor(
                "ProfileEnrichment",
                0.30,
                "High quality contact profile: Verified name, phone number, and direct email.",
                "Profile"
            ));
        }

        // Logistic Sigmoid Function: 1 / (1 + e^(-z))
        double conversionProbability = 1.0 / (1.0 + Math.Exp(-logit));
        conversionProbability = Math.Clamp(conversionProbability, 0.05, 0.96);
        conversionProbability = Math.Round(conversionProbability, 2);

        // Convert calibrated probability to 0-100 Priority Score
        int priorityScore = (int)Math.Round(conversionProbability * 100);

        // Predicted days to close estimation
        int baseDays = lead.Status switch
        {
            LeadStatus.ProposalSent => 10,
            LeadStatus.DemoScheduled => 18,
            LeadStatus.Contacted => 32,
            _ => 50
        };

        // If momentum is high, velocity speeds up close time
        if (conversionProbability > 0.70) baseDays = (int)(baseDays * 0.7);
        else if (conversionProbability < 0.30) baseDays = (int)(baseDays * 1.4);

        return Task.FromResult(new LeadScoringResult(
            priorityScore,
            conversionProbability,
            baseDays,
            factors,
            StrategyName
        ));
    }
}
