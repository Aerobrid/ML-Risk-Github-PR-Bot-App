namespace DeploymentRisk.Api.Models;

public class RiskResponse
{
    public double RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public Dictionary<string, double> Details { get; set; } = new();
    public List<Vulnerability> ScanReport { get; set; } = new();
}

public class Vulnerability
{
    public string Type { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? Line { get; set; }
}