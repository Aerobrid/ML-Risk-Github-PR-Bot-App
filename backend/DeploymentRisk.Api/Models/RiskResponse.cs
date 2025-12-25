namespace DeploymentRisk.Api.Models;

public class RiskResponse
{
    public double RiskScore { get; set; }
    public string RiskLevel { get; set; } = "";
}
