using DeploymentRisk.Api.Models;

namespace DeploymentRisk.Api.Services.Scoring;

public class RuleBasedScorer : IRiskScorer
{
    private readonly IConfiguration _config;
    private readonly ILogger<RuleBasedScorer> _logger;

    public RuleBasedScorer(IConfiguration config, ILogger<RuleBasedScorer> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string Name => "RuleBased";
    public bool IsEnabled => _config.GetValue<bool>("RiskScoring:Enabled:RuleBased");

    public Task<ScorerResult> ScoreAsync(RiskContext context)
    {
        var riskFactors = new List<string>();
        var score = 0.0;
        var details = new Dictionary<string, object>();

        // Rule 1: Large change size
        var totalLines = context.LinesAdded + context.LinesDeleted;
        details["totalLines"] = totalLines;

        if (totalLines > 1000)
        {
            score += 0.3;
            riskFactors.Add($"Large changeset ({totalLines} lines changed)");
        }
        else if (totalLines > 500)
        {
            score += 0.15;
            riskFactors.Add($"Medium changeset ({totalLines} lines changed)");
        }

        // Rule 2: Many commits
        details["commitCount"] = context.CommitCount;
        if (context.CommitCount > 20)
        {
            score += 0.2;
            riskFactors.Add($"Many commits ({context.CommitCount} commits)");
        }
        else if (context.CommitCount > 10)
        {
            score += 0.1;
            riskFactors.Add($"Moderate commits ({context.CommitCount} commits)");
        }

        // Rule 3: Weekend deployment
        if (context.Timestamp.DayOfWeek == DayOfWeek.Saturday ||
            context.Timestamp.DayOfWeek == DayOfWeek.Sunday)
        {
            score += 0.2;
            riskFactors.Add("Weekend deployment");
        }

        // Rule 4: After-hours deployment (before 8am or after 6pm)
        var hour = context.Timestamp.Hour;
        if (hour < 8 || hour > 18)
        {
            score += 0.15;
            riskFactors.Add($"After-hours deployment ({hour}:00)");
        }

        // Rule 5: Critical file patterns
        var criticalFiles = context.Files.Select(f => f.Filename).Where(f =>
            f.Contains("migration", StringComparison.OrdinalIgnoreCase) ||
            f.Contains("database", StringComparison.OrdinalIgnoreCase) ||
            f.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
        ).ToList();

        if (criticalFiles.Any())
        {
            score += 0.25;
            var filesDisplay = string.Join(", ", criticalFiles.Take(3));
            riskFactors.Add($"Critical files changed: {filesDisplay}{(criticalFiles.Count > 3 ? "..." : "")}");
        }
        details["criticalFileCount"] = criticalFiles.Count;

        // Rule 6: Direct push to main/master
        if (context.PrNumber == null && (context.Branch == "main" || context.Branch == "master"))
        {
            score += 0.3;
            riskFactors.Add($"Direct push to {context.Branch} branch");
        }

        // Cap score at 1.0
        score = Math.Min(score, 1.0);

        var level = score switch
        {
            < 0.3 => "LOW",
            < 0.5 => "MEDIUM",
            < 0.8 => "HIGH",
            _ => "CRITICAL"
        };

        details["timestamp"] = context.Timestamp;
        details["finalScore"] = score;

        _logger.LogInformation("Rule-based scoring completed: {Score} ({Level}) with {FactorCount} risk factors",
            score, level, riskFactors.Count);

        return Task.FromResult(new ScorerResult
        {
            Score = score,
            Level = level,
            RiskFactors = riskFactors,
            Details = details
        });
    }
}
