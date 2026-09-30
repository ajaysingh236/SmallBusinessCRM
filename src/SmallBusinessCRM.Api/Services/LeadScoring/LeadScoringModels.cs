namespace SmallBusinessCRM.Api.Services.LeadScoring;

public record ScoringFactor(
    string FeatureName,
    double ImpactScore,
    string Description,
    string Category
);

public record LeadScoringResult(
    int PriorityScore,
    double ConversionProbability,
    int? PredictedCloseDays,
    IReadOnlyList<ScoringFactor> Factors,
    string StrategyName
);

public interface ILeadScoringStrategy
{
    string StrategyName { get; }
    Task<LeadScoringResult> ScoreLeadAsync(SmallBusinessCRM.Api.Models.Lead lead, CancellationToken ct = default);
}
