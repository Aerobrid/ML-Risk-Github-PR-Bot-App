namespace DeploymentRisk.Api.Models;

public class RiskAssessmentResult
{
    public double OverallScore { get; set; }
    public string OverallLevel { get; set; } = string.Empty;
    public Dictionary<string, ScorerResult> ScorerResults { get; set; } = new();
    public List<string> AllRiskFactors { get; set; } = new();
    public List<Vulnerability> ScanReport { get; set; } = new();
}

public class ScorerResult
{
    public double Score { get; set; }
    public string Level { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
    public List<string> RiskFactors { get; set; } = new();
    public List<Vulnerability> ScanReport { get; set; } = new();
}
