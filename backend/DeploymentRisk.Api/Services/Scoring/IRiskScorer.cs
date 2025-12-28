using DeploymentRisk.Api.Models;

namespace DeploymentRisk.Api.Services.Scoring;

public interface IRiskScorer
{
    string Name { get; }
    bool IsEnabled { get; }
    Task<ScorerResult> ScoreAsync(RiskContext context);
}
