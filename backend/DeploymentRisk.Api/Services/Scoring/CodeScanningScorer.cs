using DeploymentRisk.Api.Models;

namespace DeploymentRisk.Api.Services.Scoring;

// code scanning scorer
public class CodeScanningScorer : IRiskScorer
{
    // NOTE: talks to GitHub Code Scanning API
    private readonly GitHubCodeScanningService _scanningService;
    private readonly IConfiguration _config;
    private readonly ILogger<CodeScanningScorer> _logger;

    public CodeScanningScorer(
        GitHubCodeScanningService scanningService,
        IConfiguration config,
        ILogger<CodeScanningScorer> logger)
    {
        _scanningService = scanningService;
        _config = config;
        _logger = logger;
    }

    // unique identfier for this scorer
    public string Name => "CodeScanning";

    // determined from config
    public bool IsEnabled => _config.GetValue<bool>("RiskScoring:Enabled:CodeScanning", true); // Default to true

    public async Task<ScorerResult> ScoreAsync(RiskContext context)
    {
        // Only run for PRs or Pushes where we have a ref
        // github code scanning needs a ref (a commit SHA or branch name)
        if (string.IsNullOrEmpty(context.Sha) && string.IsNullOrEmpty(context.Branch))
        {
            // early exit
            return new ScorerResult { Score = 0.0, Level = "LOW", RiskFactors = new List<string> { "No ref to scan" } };
        }

        // determine ref name
        var refName = !string.IsNullOrEmpty(context.Sha) ? context.Sha : $"refs/heads/{context.Branch}";
        
        // Fetch Code Scanning Alerts from github API
        var alerts = await _scanningService.GetCodeScanningAlertsAsync(
            context.InstallationId, 
            context.Owner, 
            context.Repo, 
            refName
        );

        // if no alerts pesent -> low risk
        if (!alerts.Any())
        {
             return new ScorerResult { Score = 0.0, Level = "LOW", RiskFactors = new List<string> { "No code scanning alerts found" } };
        }

        double score = 0.0;
        var factors = new List<string>();

        // Heuristic scoring based on severity
        var criticalCount = alerts.Count(a => a.Severity.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase));
        var highCount = alerts.Count(a => a.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase));
        var mediumCount = alerts.Count(a => a.Severity.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase));

        if (criticalCount > 0)
        {
            score = 1.0;
            factors.Add($"🚨 {criticalCount} CRITICAL security alerts found");
        }
        else if (highCount > 0)
        {
            score = 0.8;
            factors.Add($"🔶 {highCount} HIGH severity alerts found");
        }
        else if (mediumCount > 0)
        {
            score = 0.4;
            factors.Add($"⚠️ {mediumCount} MEDIUM severity alerts found");
        }
        else
        {
            score = 0.1;
            factors.Add($"ℹ️ {alerts.Count} LOW severity alerts found");
        }

        var level = score switch
        {
            < 0.3 => "LOW",
            < 0.5 => "MEDIUM",
            < 0.8 => "HIGH",
            _ => "CRITICAL"
        };

        return new ScorerResult
        {
            Score = score,
            Level = level,
            RiskFactors = factors,
            ScanReport = alerts // Include detailed report
        };
    }
}
