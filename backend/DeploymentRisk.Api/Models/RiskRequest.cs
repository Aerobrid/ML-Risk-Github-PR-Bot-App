namespace DeploymentRisk.Api.Models;

// represents singular file change in a commit or PR
public class FileChange
{
    public string Filename { get; set; } = string.Empty;
    // stores changes to that file in unified diff format if possible
    public string? Patch { get; set; }
    public string Status { get; set; } = "modified";
}

// represents input to ML Model
public class RiskRequest
{
    public int CommitCount { get; set; }
    public int LinesChanged { get; set; }
    public double TestPassRate { get; set; }
    public int HourOfDay { get; set; }
    public int DayOfWeek { get; set; }
    // note that it is a list of files that are changed
    public List<FileChange> Files { get; set; } = new();
}