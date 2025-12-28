namespace DeploymentRisk.Api.Models;

public class RiskContext
{
    public long InstallationId { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public int? PrNumber { get; set; }
    public string Sha { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public int CommitCount { get; set; }
    public int LinesAdded { get; set; }
    public int LinesDeleted { get; set; }
    public List<FileChange> Files { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public string Author { get; set; } = string.Empty;
}