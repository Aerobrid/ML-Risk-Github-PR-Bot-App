using DeploymentRisk.Api.Models;

namespace DeploymentRisk.Api.Services.Scoring;

// any class that wants to be a risk scorer must implement this interface
// should have a name and enabled status, along with an async method returning a ScorerResult
public interface IRiskScorer
{
    string Name { get; }
    bool IsEnabled { get; }
    Task<ScorerResult> ScoreAsync(RiskContext context);
}
