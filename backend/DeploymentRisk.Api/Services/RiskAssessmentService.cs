using DeploymentRisk.Api.Models;
using DeploymentRisk.Api.Services.Scoring;

namespace DeploymentRisk.Api.Services;

// What this class does:
// 1. Takes RiskContext (like time of day, lines of code changed, etc.)
// 2. Runs all scorers configured
// 3. Applies weights from config
// 4. Calculates weighted score + its level
// 5. Combines all risk factors and scan reports
// 6. And with most of other classes, logs info using `ILogger`
// 6. Returns a RiskAssessmentResult
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

    // Main logic
    public async Task<RiskAssessmentResult> AssessAsync(RiskContext context)
    {
        // Stores each scorer’s result keyed by name
        var scorerResults = new Dictionary<string, ScorerResult>();
        var weights = _config.GetSection("RiskScoring:Weights").Get<Dictionary<string, double>>()
                      ?? new Dictionary<string, double> { { "RuleBased", 1.0 } };
        // filter based on which ones are enabled
        var enabledScorers = _scorers.Where(s => s.IsEnabled).ToList();

        _logger.LogInformation("Running risk assessment with {Count} enabled scorers", enabledScorers.Count);

        // run each scorer
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

        // no results edge case 
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

        // Weighted average calculation
        var totalWeight = enabledScorers
            .Where(s => scorerResults.ContainsKey(s.Name))
            .Sum(s => weights.GetValueOrDefault(s.Name, 0.0));

        var weightedScore = scorerResults.Sum(kvp =>
            kvp.Value.Score * weights.GetValueOrDefault(kvp.Key, 0.0)
        ) / (totalWeight > 0 ? totalWeight : 1.0);

        // level calculation
        var overallLevel = weightedScore switch
        {
            < 0.3 => "LOW",
            < 0.5 => "MEDIUM",
            < 0.8 => "HIGH",
            _ => "CRITICAL"
        };

        // flatten risk factors from every scorer
        var allRiskFactors = scorerResults
            .SelectMany(r => r.Value.RiskFactors)
            .ToList();
        // flatten scan reports from every scorer
        var scanReport = scorerResults
            .SelectMany(r => r.Value.ScanReport)
            .ToList();

        // log summary
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
