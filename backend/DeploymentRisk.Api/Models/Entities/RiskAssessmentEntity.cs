namespace DeploymentRisk.Api.Models.Entities;

public class RiskAssessmentEntity
{
    public Guid Id { get; set; }
    public string RepositoryFullName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // "pull_request" | "push"
    public int? PullRequestNumber { get; set; }
    public string Sha { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;

    // Risk Scores
    public double OverallRiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty; // LOW | MEDIUM | HIGH | CRITICAL
    public double? RuleBasedScore { get; set; }
    public double? MLScore { get; set; }
    public double? SecurityScore { get; set; }
    public double? BugScore { get; set; }

    // Metadata
    public DateTimeOffset CreatedAt { get; set; }
    public string? GitHubCommentUrl { get; set; }
    public string Author { get; set; } = string.Empty;

    // Analysis Details (JSON)
    public string? RiskFactorsJson { get; set; }
    public string? MetricsJson { get; set; }
}
