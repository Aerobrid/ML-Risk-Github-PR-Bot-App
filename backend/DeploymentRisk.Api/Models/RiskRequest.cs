namespace DeploymentRisk.Api.Models;

public class RiskRequest
{
    public int CommitCount { get; set; }
    public int LinesChanged { get; set; }
    public double TestPassRate { get; set; }
    public int HourOfDay { get; set; }
    public int DayOfWeek { get; set; }
}
