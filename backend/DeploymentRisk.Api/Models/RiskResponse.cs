namespace DeploymentRisk.Api.Models;

// represents result of risk prediction/assessment
public class RiskResponse
{
    public double RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    // key: factor name | value: factor score
    public Dictionary<string, double> Details { get; set; } = new();
    public List<Vulnerability> ScanReport { get; set; } = new();
}

// represents a single security or code issue detected whenever scanning
public class Vulnerability
{
    public string Type { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? Line { get; set; }
}