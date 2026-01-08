namespace DeploymentRisk.Api.Models;

// the aggregated result of a risk assessment (from all scorers)
public class RiskAssessmentResult
{
    // Example: ML predicts 0.7, CodeScanning predicts 0.4 → weighted average = 0.55
    public double OverallScore { get; set; }
    public string OverallLevel { get; set; } = string.Empty;
    public Dictionary<string, ScorerResult> ScorerResults { get; set; } = new();
    public List<string> AllRiskFactors { get; set; } = new();
    public List<Vulnerability> ScanReport { get; set; } = new();
}

// output of a single risk scorer (ML, CodeScanner, RuleBased, etc.)
public class ScorerResult
{
    public double Score { get; set; }
    public string Level { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
    public List<string> RiskFactors { get; set; } = new();
    public List<Vulnerability> ScanReport { get; set; } = new();
}
