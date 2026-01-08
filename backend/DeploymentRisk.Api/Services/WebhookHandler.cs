using System.Text;
using System.Text.Json;
using DeploymentRisk.Api.Models;
using DeploymentRisk.Api.Models.Entities;
using DeploymentRisk.Api.Repositories;

namespace DeploymentRisk.Api.Services;

public class WebhookHandler
{
    // handles incoming webhook payloads and orchestrates analysis/posting
    // basically processes webhooks and turns them into risk assessments and comments
    private readonly GitHubClientService _github;
    private readonly RiskAssessmentService _riskService;
    private readonly LlmReviewService _llmReview;
    private readonly IRiskRepository _repository;
    private readonly ILogger<WebhookHandler> _logger;

    public WebhookHandler(
        GitHubClientService github,
        RiskAssessmentService riskService,
        LlmReviewService llmReview,
        IRiskRepository repository,
        ILogger<WebhookHandler> logger)
    {
        _github = github;
        _riskService = riskService;
        _llmReview = llmReview;
        _repository = repository;
        _logger = logger;
    }

    public async Task ProcessAsync(WebhookEvent webhook)
    {
        // log basic info about incoming event
        _logger.LogInformation("Processing webhook event: {EventType}", webhook.EventType);

        try
        {
            // route by event type
            if (webhook.EventType == "pull_request")
            {
                // handle PR events (open, sync, reopen)
                await HandlePullRequestAsync(webhook.Payload);
            }
            else if (webhook.EventType == "push")
            {
                // handle push events
                await HandlePushAsync(webhook.Payload);
            }
            else
            {
                // ignore other types for now
                _logger.LogWarning("Unsupported webhook event type: {EventType}", webhook.EventType);
            }
        }
        catch (Exception ex)
        {
            // bubble up after logging so caller can handle retries if needed
            _logger.LogError(ex, "Error processing webhook {EventType}", webhook.EventType);
            throw;
        }
    }

    private async Task HandlePullRequestAsync(string payload)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        var data = JsonSerializer.Deserialize<PullRequestPayload>(payload, options);
        if (data == null)
        {
            // malformed payload, nothing to do
            _logger.LogWarning("Failed to deserialize pull request payload");
            return;
        }

        if (data.Action != "opened" && data.Action != "synchronize" && data.Action != "reopened")
        {
            // only react to open/sync/reopen events
            _logger.LogDebug("Ignoring PR action: {Action}", data.Action);
            return;
        }

        // log which PR we're processing
        _logger.LogInformation("Processing PR #{Number} in {Repo}", data.PullRequest.Number, data.Repository.FullName);

        // Fetch PR files from GitHub for analysis
        var files = await _github.GetPullRequestFilesAsync(
            data.Installation.Id,
            data.Repository.Owner.Login,
            data.Repository.Name,
            data.PullRequest.Number
        );

        // build a RiskContext object with metadata and file diffs
        var context = new RiskContext
        {
            InstallationId = data.Installation.Id,
            Owner = data.Repository.Owner.Login,
            Repo = data.Repository.Name,
            PrNumber = data.PullRequest.Number,
            Sha = data.PullRequest.Head.Sha,
            Branch = data.PullRequest.Base.Ref,
            CommitCount = data.PullRequest.Commits,
            LinesAdded = data.PullRequest.Additions,
            LinesDeleted = data.PullRequest.Deletions,
            Files = files.Select(f => new FileChange 
            { 
                Filename = f.FileName, 
                Patch = f.Patch, 
                Status = f.Status 
            }).ToList(),
            Timestamp = DateTime.UtcNow,
            Author = data.PullRequest.User.Login
        };

        // Run risk assessment (core business logic)
        var assessment = await _riskService.AssessAsync(context);

        // Generate a text review using LLM (optional) and post to PR
        var comment = await _llmReview.GenerateReviewCommentAsync(context, assessment);

        var postedComment = await _github.PostCommentAsync(
            data.Installation.Id,
            context.Owner,
            context.Repo,
            data.PullRequest.Number,
            comment
        );

        if (postedComment == null)
        {
            _logger.LogWarning("PostCommentAsync returned null for PR #{Number} in {Repo}", data.PullRequest.Number, context.Repo);
        }
        else
        {
            _logger.LogInformation("Posted risk assessment comment to PR #{Number} (url={Url})", data.PullRequest.Number, postedComment.HtmlUrl);
        }

        // Persist assessment to repository if configured
        var entity = new RiskAssessmentEntity
        {
            Id = Guid.NewGuid(),
            RepositoryFullName = $"{context.Owner}/{context.Repo}",
            EventType = "pull_request",
            PullRequestNumber = context.PrNumber,
            Sha = context.Sha,
            Branch = context.Branch,
            OverallRiskScore = assessment.OverallScore,
            RiskLevel = assessment.OverallLevel,
            RuleBasedScore = assessment.ScorerResults.GetValueOrDefault("RuleBased")?.Score,
            MLScore = assessment.ScorerResults.GetValueOrDefault("MLModel")?.Score,
            CreatedAt = DateTimeOffset.UtcNow,
            Author = context.Author,
            GitHubCommentUrl = postedComment?.HtmlUrl,
            RiskFactorsJson = JsonSerializer.Serialize(assessment.AllRiskFactors),
            MetricsJson = JsonSerializer.Serialize(context)
        };

        await _repository.SaveAssessmentAsync(entity);
    }

    private async Task HandlePushAsync(string payload)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        var data = JsonSerializer.Deserialize<PushPayload>(payload, options);
        if (data == null)
        {
            _logger.LogWarning("Failed to deserialize push payload");
            return;
        }

        // log push and extract branch
        _logger.LogInformation("Processing push to {Branch} in {Repo}", data.Ref, data.Repository.FullName);

        // Extract branch name from ref (refs/heads/main -> main)
        var branch = data.Ref.Replace("refs/heads/", "");

        // Build context from commits: combine added/modified/removed files
        var context = new RiskContext
        {
            InstallationId = data.Installation.Id,
            Owner = data.Repository.Owner.Login,
            Repo = data.Repository.Name,
            PrNumber = null,
            Sha = data.After,
            Branch = branch,
            CommitCount = data.Commits.Count,
            LinesAdded = 0, // GitHub doesn't provide this in push events
            LinesDeleted = 0,
            Files = data.Commits.SelectMany(c => 
                c.Added.Select(f => new FileChange { Filename = f, Status = "added" })
                .Concat(c.Modified.Select(f => new FileChange { Filename = f, Status = "modified" }))
                .Concat(c.Removed.Select(f => new FileChange { Filename = f, Status = "removed" }))
            ).GroupBy(f => f.Filename).Select(g => g.First()).ToList(),
            Timestamp = DateTime.UtcNow,
            Author = data.Pusher.Name
        };

        // Run risk assessment on the push context
        var assessment = await _riskService.AssessAsync(context);

        _logger.LogInformation("Push risk assessment complete: {Score} ({Level})", assessment.OverallScore, assessment.OverallLevel);

        // For pushes we might create a commit status or check run; here we persist the result
        var entity = new RiskAssessmentEntity
        {
            Id = Guid.NewGuid(),
            RepositoryFullName = $"{context.Owner}/{context.Repo}",
            EventType = "push",
            PullRequestNumber = null,
            Sha = context.Sha,
            Branch = context.Branch,
            OverallRiskScore = assessment.OverallScore,
            RiskLevel = assessment.OverallLevel,
            RuleBasedScore = assessment.ScorerResults.GetValueOrDefault("RuleBased")?.Score,
            MLScore = assessment.ScorerResults.GetValueOrDefault("MLModel")?.Score,
            CreatedAt = DateTimeOffset.UtcNow,
            Author = context.Author,
            RiskFactorsJson = JsonSerializer.Serialize(assessment.AllRiskFactors),
            MetricsJson = JsonSerializer.Serialize(context)
        };

        await _repository.SaveAssessmentAsync(entity);
    }

    private string FormatAssessmentComment(RiskAssessmentResult assessment)
    {
        // Build a markdown-style comment summarizing the assessment
        // used as the body of a PR review comment

        var emoji = assessment.OverallLevel switch
        {
            "LOW" => "✅",
            "MEDIUM" => "⚠️",
            "HIGH" => "🔶",
            "CRITICAL" => "🚨",
            _ => "ℹ️"
        };

        var sb = new StringBuilder();
        sb.AppendLine($"# {emoji} Risk Assessment Report");
        sb.AppendLine("> _Automated analysis powered by XGBoost & CodeQL_");
        sb.AppendLine();
        
        // Summary Table
        sb.AppendLine("## 📊 Executive Summary");
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("| :--- | :--- |");
        sb.AppendLine($"| **Risk Level** | **{assessment.OverallLevel}** |");
        sb.AppendLine($"| **Risk Score** | {assessment.OverallScore:P0} |");
        
        // ML & Rule Based Scores
        foreach (var (name, result) in assessment.ScorerResults)
        {
            var scorerEmoji = result.Level == "LOW" ? "🟢" : result.Level == "MEDIUM" ? "🟡" : "🔴";
            sb.AppendLine($"| {name} | {scorerEmoji} {result.Score:P0} |");
        }
        sb.AppendLine();

        // Risk Factors (Heuristics)
        if (assessment.AllRiskFactors.Any())
        {
            sb.AppendLine("## 🚩 Key Risk Indicators");
            foreach (var factor in assessment.AllRiskFactors.Take(5)) // Top 5
            {
                sb.AppendLine($"- {factor}");
            }
            if (assessment.AllRiskFactors.Count > 5)
            {
                sb.AppendLine($"- _...and {assessment.AllRiskFactors.Count - 5} more factors_");
            }
            sb.AppendLine();
        }

        // Security Report (CodeQL + Internal)
        if (assessment.ScanReport.Any())
        {
            sb.AppendLine("## 🛡️ Security & Code Quality");
            
            var criticals = assessment.ScanReport.Where(i => i.Severity == "CRITICAL").ToList();
            var highs = assessment.ScanReport.Where(i => i.Severity == "HIGH").ToList();
            var others = assessment.ScanReport.Where(i => i.Severity != "CRITICAL" && i.Severity != "HIGH").ToList();

            if (criticals.Any())
            {
                sb.AppendLine("### 🚨 Critical Issues (Immediate Action Required)");
                foreach(var issue in criticals) PrintIssue(sb, issue);
            }
            
            if (highs.Any())
            {
                sb.AppendLine("### 🔶 High Severity Issues");
                foreach(var issue in highs) PrintIssue(sb, issue);
            }

            if (others.Any())
            {
                sb.AppendLine("### ⚠️ Other Findings");
                // Limit others to avoid massive comments
                foreach(var issue in others.Take(5)) PrintIssue(sb, issue);
                if (others.Count > 5) sb.AppendLine($"- _...and {others.Count - 5} more findings_");
            }
            sb.AppendLine();
        }
        else
        {
             sb.AppendLine("## 🛡️ Security Check");
             sb.AppendLine("✅ No significant security issues detected.");
             sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine($"<details><summary>Stat Analysis & Debug Info</summary>");
        sb.AppendLine();
        sb.AppendLine("- **Analysis Engine**: XGBoost v2.0");
        sb.AppendLine("- **Scanners**: CodeQL, Internal Heuristics");
        sb.AppendLine($"- **Timestamp**: {DateTime.UtcNow:u}");
        sb.AppendLine("</details>");

        // return assembled markdown comment
        return sb.ToString();
    }

    private void PrintIssue(StringBuilder sb, Vulnerability issue)
    {
        // format vulnerability details for the comment
        sb.AppendLine($"- **{issue.Type}**: {issue.Description}");
        sb.AppendLine($"  - 📄 `{issue.File}` : line {issue.Line}");
    }
}

// Simplified payload models
public class PullRequestPayload
{
    // payload model for pull_request webhook
    public string Action { get; set; } = string.Empty;
    public PullRequestData PullRequest { get; set; } = new();
    public RepositoryData Repository { get; set; } = new();
    public InstallationData Installation { get; set; } = new();
}

public class PullRequestData
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public UserData User { get; set; } = new();
    public HeadData Head { get; set; } = new();
    public BaseData Base { get; set; } = new();
    public int Commits { get; set; }
    public int Additions { get; set; }
    public int Deletions { get; set; }
}

public class PushPayload
{
    // payload model for push webhook
    public string Ref { get; set; } = string.Empty;
    public string Before { get; set; } = string.Empty;
    public string After { get; set; } = string.Empty;
    public List<CommitData> Commits { get; set; } = new();
    public RepositoryData Repository { get; set; } = new();
    public PusherData Pusher { get; set; } = new();
    public InstallationData Installation { get; set; } = new();
}

public class CommitData
{
    // commit details contained in push events
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string> Added { get; set; } = new();
    public List<string> Modified { get; set; } = new();
    public List<string> Removed { get; set; } = new();
}

public class RepositoryData
{
    // basic repository metadata used by payloads
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public OwnerData Owner { get; set; } = new();
}

public class OwnerData
{
    public string Login { get; set; } = string.Empty;
}

public class UserData
{
    public string Login { get; set; } = string.Empty;
}

public class HeadData
{
    public string Ref { get; set; } = string.Empty;
    public string Sha { get; set; } = string.Empty;
}

public class BaseData
{
    public string Ref { get; set; } = string.Empty;
}

public class InstallationData
{
    // GitHub App installation id
    public long Id { get; set; }
}

public class PusherData
{
    public string Name { get; set; } = string.Empty;
}
