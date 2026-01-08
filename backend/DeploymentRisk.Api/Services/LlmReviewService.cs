using System.Net.Http.Headers;
using System.Text;
using DeploymentRisk.Api.Models;

namespace DeploymentRisk.Api.Services;

// TODO: Add functionality to app + test thoroughly
// Service for generating AI-powered PR review comments using LLM APIs (OpenAI or Claude)
// Falls back to template-based comments when LLM is disabled or fails
public class LlmReviewService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<LlmReviewService> _logger;

    public LlmReviewService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<LlmReviewService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Generate a PR review comment using LLM or fallback to template.
    /// </summary>
    public async Task<string> GenerateReviewCommentAsync(
        RiskContext context,
        RiskAssessmentResult assessment)
    {
        // make toggable
        var isEnabled = _config.GetValue<bool>("LLM:Enabled", false);
        var apiKey = _config["LLM:ApiKey"] ?? Environment.GetEnvironmentVariable("LLM_API_KEY");

        if (!isEnabled || string.IsNullOrEmpty(apiKey))
        {
            _logger.LogDebug("LLM disabled or API key not configured. Using template fallback.");
            return FormatTemplateComment(assessment);
        }

        // Provider switches the concrete request/response shape we expect
        // Keep provider names simple: "OpenAI" or "Claude" (will configure later)
        var provider = _config["LLM:Provider"] ?? "OpenAI";

        try
        {
            _logger.LogInformation("Generating AI review using {Provider}", provider);

            var review = provider.ToUpper() switch
            {
                "OPENAI" => await GenerateOpenAIReviewAsync(context, assessment, apiKey),
                "CLAUDE" => await GenerateClaudeReviewAsync(context, assessment, apiKey),
                _ => throw new NotSupportedException($"LLM provider '{provider}' not supported")
            };

            _logger.LogInformation("AI review generated successfully");
            return review;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate AI review. Falling back to template.");

            var fallbackEnabled = _config.GetValue<bool>("LLM:FallbackToBasic", true);
            if (fallbackEnabled)
            {
                return FormatTemplateComment(assessment);
            }

            // If the operator opted out of fallback, bubble the error so the caller can decide
            throw;
        }
    }

    private async Task<string> GenerateOpenAIReviewAsync(
        RiskContext context,
        RiskAssessmentResult assessment,
        string apiKey)
    {
        // Note: model names evolve quickly so keep models configurable in appsettings
        var model = _config["LLM:Model"] ?? "gpt-4o-mini";
        var maxTokens = _config.GetValue<int>("LLM:MaxTokens", 500);
        var temperature = _config.GetValue<double>("LLM:Temperature", 0.7);

        var prompt = BuildPrompt(context, assessment);

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = "You are a senior software engineer providing concise, professional code review feedback." },
                new { role = "user", content = prompt }
            },
            max_tokens = maxTokens,
            temperature
        };

        // Create an HttpClient from the factory. If you want a base address or default headers
        // configured centrally, register a named/typed client in Program.cs.
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Be explicit about the endpoint shape we expect; if OpenAI changes API shape we'll catch it here.
        var response = await client.PostAsJsonAsync(
            "https://api.openai.com/v1/chat/completions",
            requestBody
        );

        // Let callers see non-success via exception. We catch higher up and decide fallback.
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
        var review = responseBody?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

        if (string.IsNullOrEmpty(review))
        {
            throw new InvalidOperationException("OpenAI returned empty response");
        }

        return WrapAIReview(review, assessment);
    }

    private async Task<string> GenerateClaudeReviewAsync(
        RiskContext context,
        RiskAssessmentResult assessment,
        string apiKey)
    {
        // Anthropic/Claude configuration is similar but uses api-key header naming conventions.
        var model = _config["LLM:Model"] ?? "claude-3-5-sonnet-20241022";
        var maxTokens = _config.GetValue<int>("LLM:MaxTokens", 500);
        var temperature = _config.GetValue<double>("LLM:Temperature", 0.7);

        var prompt = BuildPrompt(context, assessment);

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            max_tokens = maxTokens,
            temperature,
            system = "You are a senior software engineer providing concise, professional code review feedback."
        };

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var response = await client.PostAsJsonAsync(
            "https://api.anthropic.com/v1/messages",
            requestBody
        );

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadFromJsonAsync<ClaudeResponse>();
        var review = responseBody?.Content?.FirstOrDefault()?.Text?.Trim();

        if (string.IsNullOrEmpty(review))
        {
            throw new InvalidOperationException("Claude returned empty response");
        }

        return WrapAIReview(review, assessment);
    }

    private string BuildPrompt(RiskContext context, RiskAssessmentResult assessment)
    {
        // similar type also used in java
        var sb = new StringBuilder();
        // prompting logic
        sb.AppendLine("You are reviewing a pull request. Provide a concise, professional code review comment.");
        sb.AppendLine();
        sb.AppendLine("PR DETAILS:");
        sb.AppendLine($"- Files changed: {context.Files.Count}");
        sb.AppendLine($"- Commits: {context.CommitCount}");
        sb.AppendLine($"- Lines added: {context.LinesAdded}");
        sb.AppendLine($"- Lines deleted: {context.LinesDeleted}");

        // Identify critical files
        var criticalFiles = context.Files
            .Where(f => f.Filename.Contains("migration", StringComparison.OrdinalIgnoreCase) ||
                       f.Filename.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
                       f.Filename.Contains("database", StringComparison.OrdinalIgnoreCase) ||
                       f.Filename.Contains(".sql", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Filename)
            .ToList();

        if (criticalFiles.Any())
        {
            sb.AppendLine($"- Critical files: {string.Join(", ", criticalFiles.Take(3))}");
        }

        sb.AppendLine();
        sb.AppendLine("RISK ASSESSMENT:");
        sb.AppendLine($"- Overall Risk: {assessment.OverallLevel} ({assessment.OverallScore:P0})");

        // Add scorer breakdown
        foreach (var (name, result) in assessment.ScorerResults)
        {
            sb.AppendLine($"- {name}: {result.Level} ({result.Score:P0})");
        }

        if (assessment.AllRiskFactors.Any())
        {
            sb.AppendLine();
            sb.AppendLine("RISK FACTORS:");
            foreach (var factor in assessment.AllRiskFactors.Take(5))
            {
                sb.AppendLine($"- {factor}");
            }
        }

        if (assessment.ScanReport.Any())
        {
            sb.AppendLine();
            sb.AppendLine("SECURITY FINDINGS:");

            var criticals = assessment.ScanReport.Where(v => v.Severity == "CRITICAL").ToList();
            var highs = assessment.ScanReport.Where(v => v.Severity == "HIGH").ToList();

            if (criticals.Any())
            {
                sb.AppendLine($"- {criticals.Count} CRITICAL issue(s) found:");
                foreach (var issue in criticals.Take(3))
                {
                    sb.AppendLine($"  - {issue.Type}: {issue.Description} in {issue.File}");
                }
            }

            if (highs.Any())
            {
                sb.AppendLine($"- {highs.Count} HIGH severity issue(s) found:");
                foreach (var issue in highs.Take(3))
                {
                    sb.AppendLine($"  - {issue.Type}: {issue.Description} in {issue.File}");
                }
            }
        }

        // more prompting
        sb.AppendLine();
        sb.AppendLine("TASK:");
        sb.AppendLine("Write a professional PR review comment (200-300 words) that:");
        sb.AppendLine("1. Starts with an emoji and friendly greeting");
        sb.AppendLine("2. Summarizes the risk level in conversational terms");
        sb.AppendLine("3. Highlights key concerns (if any) with actionable recommendations");
        sb.AppendLine("4. Mentions positive aspects if low risk");
        sb.AppendLine("5. Uses emojis for readability");
        sb.AppendLine("6. Formats in Markdown");
        sb.AppendLine("7. Ends with encouragement or next steps");
        sb.AppendLine();
        sb.AppendLine("Do NOT just repeat metrics. Provide insights and guidance like a senior engineer would.");
        sb.AppendLine("Be concise and focus on what matters most.");

        return sb.ToString();
    }

    private string WrapAIReview(string aiReview, RiskAssessmentResult assessment)
    {
        var sb = new StringBuilder();

        // Add AI-generated review at the top
        sb.AppendLine(aiReview);
        sb.AppendLine();

        // Add summary table for quick reference
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 📊 Risk Assessment Summary");
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("| :--- | :--- |");
        sb.AppendLine($"| **Overall Risk** | **{assessment.OverallLevel}** ({assessment.OverallScore:P0}) |");

        foreach (var (name, result) in assessment.ScorerResults)
        {
            var emoji = result.Level switch
            {
                "LOW" => "🟢",
                "MEDIUM" => "🟡",
                "HIGH" => "🔴",
                "CRITICAL" => "🚨",
                _ => "⚪"
            };
            sb.AppendLine($"| {name} Score | {emoji} {result.Score:P0} |");
        }

        sb.AppendLine();

        // Add detailed debug info in collapsible section
        sb.AppendLine("<details>");
        sb.AppendLine("<summary>📋 Detailed Analysis & Debug Info</summary>");
        sb.AppendLine();

        if (assessment.AllRiskFactors.Any())
        {
            sb.AppendLine("### Risk Factors");
            foreach (var factor in assessment.AllRiskFactors)
            {
                sb.AppendLine($"- {factor}");
            }
            sb.AppendLine();
        }

        if (assessment.ScanReport.Any())
        {
            sb.AppendLine("### Security Scan Report");
            foreach (var issue in assessment.ScanReport)
            {
                sb.AppendLine($"- **{issue.Severity}** - {issue.Type}: {issue.Description}");
                sb.AppendLine($"  - File: `{issue.File}:{issue.Line}`");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"**Analysis Timestamp**: {DateTime.UtcNow:u}");
        sb.AppendLine($"**AI Model**: {_config["LLM:Provider"]}/{_config["LLM:Model"]}");
        sb.AppendLine("</details>");

        return sb.ToString();
    }

    private string FormatTemplateComment(RiskAssessmentResult assessment)
    {
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
        sb.AppendLine("> _Automated analysis powered by ML & security scanners_");
        sb.AppendLine();

        // Summary Table
        sb.AppendLine("## 📊 Executive Summary");
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("| :--- | :--- |");
        sb.AppendLine($"| **Risk Level** | **{assessment.OverallLevel}** |");
        sb.AppendLine($"| **Risk Score** | {assessment.OverallScore:P0} |");

        foreach (var (name, result) in assessment.ScorerResults)
        {
            var scorerEmoji = result.Level == "LOW" ? "🟢" : result.Level == "MEDIUM" ? "🟡" : "🔴";
            sb.AppendLine($"| {name} | {scorerEmoji} {result.Score:P0} |");
        }
        sb.AppendLine();

        // Risk Factors
        if (assessment.AllRiskFactors.Any())
        {
            sb.AppendLine("## 🚩 Key Risk Indicators");
            foreach (var factor in assessment.AllRiskFactors.Take(5))
            {
                sb.AppendLine($"- {factor}");
            }
            if (assessment.AllRiskFactors.Count > 5)
            {
                sb.AppendLine($"- _...and {assessment.AllRiskFactors.Count - 5} more factors_");
            }
            sb.AppendLine();
        }

        // Security Report
        if (assessment.ScanReport.Any())
        {
            sb.AppendLine("## 🛡️ Security & Code Quality");

            var criticals = assessment.ScanReport.Where(i => i.Severity == "CRITICAL").ToList();
            var highs = assessment.ScanReport.Where(i => i.Severity == "HIGH").ToList();
            var others = assessment.ScanReport.Where(i => i.Severity != "CRITICAL" && i.Severity != "HIGH").ToList();

            if (criticals.Any())
            {
                sb.AppendLine("### 🚨 Critical Issues");
                foreach (var issue in criticals)
                {
                    sb.AppendLine($"- **{issue.Type}**: {issue.Description}");
                    sb.AppendLine($"  - 📄 `{issue.File}:{issue.Line}`");
                }
            }

            if (highs.Any())
            {
                sb.AppendLine("### 🔶 High Severity");
                foreach (var issue in highs)
                {
                    sb.AppendLine($"- **{issue.Type}**: {issue.Description}");
                    sb.AppendLine($"  - 📄 `{issue.File}:{issue.Line}`");
                }
            }

            if (others.Any())
            {
                sb.AppendLine("### ⚠️ Other Findings");
                foreach (var issue in others.Take(5))
                {
                    sb.AppendLine($"- {issue.Description} (`{issue.File}`)");
                }
                if (others.Count > 5)
                {
                    sb.AppendLine($"- _...and {others.Count - 5} more_");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine($"_Analysis timestamp: {DateTime.UtcNow:u}_");

        return sb.ToString();
    }

    // Response models
    private class OpenAIResponse
    {
        public List<OpenAIChoice>? Choices { get; set; }
    }

    private class OpenAIChoice
    {
        public OpenAIMessage? Message { get; set; }
    }

    private class OpenAIMessage
    {
        public string? Content { get; set; }
    }

    private class ClaudeResponse
    {
        public List<ClaudeContent>? Content { get; set; }
    }

    private class ClaudeContent
    {
        public string? Text { get; set; }
    }
}
