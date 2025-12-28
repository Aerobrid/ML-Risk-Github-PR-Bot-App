namespace DeploymentRisk.Api.Models;

public class FileChange
{
    public string Filename { get; set; } = string.Empty;
    public string? Patch { get; set; }
    public string Status { get; set; } = "modified";
}

public class RiskRequest
{
    public int CommitCount { get; set; }
    public int LinesChanged { get; set; }
    public double TestPassRate { get; set; }
    public int HourOfDay { get; set; }
    public int DayOfWeek { get; set; }
    public List<FileChange> Files { get; set; } = new();
}