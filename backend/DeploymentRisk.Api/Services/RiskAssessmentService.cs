using DeploymentRisk.Api.Models;
using DeploymentRisk.Api.Services.Scoring;

namespace DeploymentRisk.Api.Services;

public class RiskAssessmentService
{
    private readonly IEnumerable<IRiskScorer> _scorers;
    private readonly IConfiguration _config;
    private readonly ILogger<RiskAssessmentService> _logger;

    public RiskAssessmentService(
        IEnumerable<IRiskScorer> scorers,
        IConfiguration config,
        ILogger<RiskAssessmentService> logger)
    {
        _scorers = scorers;
        _config = config;
        _logger = logger;
    }

    public async Task<RiskAssessmentResult> AssessAsync(RiskContext context)
    {
        var scorerResults = new Dictionary<string, ScorerResult>();
        var weights = _config.GetSection("RiskScoring:Weights").Get<Dictionary<string, double>>()
                      ?? new Dictionary<string, double> { { "RuleBased", 1.0 } };

        var enabledScorers = _scorers.Where(s => s.IsEnabled).ToList();

        _logger.LogInformation("Running risk assessment with {Count} enabled scorers", enabledScorers.Count);

        foreach (var scorer in enabledScorers)
        {
            try
            {
                var result = await scorer.ScoreAsync(context);
                scorerResults[scorer.Name] = result;
                _logger.LogDebug("Scorer {Name} completed: {Score} ({Level})",
                    scorer.Name, result.Score, result.Level);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running scorer {Name}", scorer.Name);
            }
        }

        if (!scorerResults.Any())
        {
            _logger.LogWarning("No scorers produced results, returning default LOW risk");
            return new RiskAssessmentResult
            {
                OverallScore = 0.0,
                OverallLevel = "LOW",
                ScorerResults = new Dictionary<string, ScorerResult>(),
                AllRiskFactors = new List<string> { "No risk scorers were executed" }
            };
        }

        // Weighted average
        var totalWeight = enabledScorers
            .Where(s => scorerResults.ContainsKey(s.Name))
            .Sum(s => weights.GetValueOrDefault(s.Name, 0.0));

        var weightedScore = scorerResults.Sum(kvp =>
            kvp.Value.Score * weights.GetValueOrDefault(kvp.Key, 0.0)
        ) / (totalWeight > 0 ? totalWeight : 1.0);

        var overallLevel = weightedScore switch
        {
            < 0.3 => "LOW",
            < 0.5 => "MEDIUM",
            < 0.8 => "HIGH",
            _ => "CRITICAL"
        };

        var allRiskFactors = scorerResults
            .SelectMany(r => r.Value.RiskFactors)
            .ToList();

        var scanReport = scorerResults
            .SelectMany(r => r.Value.ScanReport)
            .ToList();

        _logger.LogInformation("Risk assessment complete: {Score} ({Level}) with {FactorCount} total risk factors and {ScanCount} scan issues",
            weightedScore, overallLevel, allRiskFactors.Count, scanReport.Count);

        return new RiskAssessmentResult
        {
            OverallScore = weightedScore,
            OverallLevel = overallLevel,
            ScorerResults = scorerResults,
            AllRiskFactors = allRiskFactors,
            ScanReport = scanReport
        };
    }
}
