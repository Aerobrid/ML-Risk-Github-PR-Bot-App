using Octokit;
using DeploymentRisk.Api.Models;

namespace DeploymentRisk.Api.Services;

public class GitHubCodeScanningService
{
    private readonly GitHubClientService _github;
    private readonly ILogger<GitHubCodeScanningService> _logger;

    public GitHubCodeScanningService(GitHubClientService github, ILogger<GitHubCodeScanningService> logger)
    {
        _github = github;
        _logger = logger;
    }

    /// <summary>
    /// Get security alerts for a repository with multi-source fallback.
    /// Tries CodeQL first, then Dependabot, then returns empty list.
    /// </summary>
    public async Task<List<Vulnerability>> GetCodeScanningAlertsAsync(long installationId, string owner, string repo, string refName)
    {
        try
        {
            var client = await _github.GetInstallationClientAsync(installationId);

            // Try CodeQL first
            _logger.LogDebug("Fetching CodeQL alerts for {Owner}/{Repo} ref={Ref}", owner, repo, refName);
            var codeqlAlerts = await GetCodeQLAlertsAsync(client, owner, repo, refName);

            if (codeqlAlerts.Any())
            {
                _logger.LogInformation("Found {Count} CodeQL alerts for {Owner}/{Repo}", codeqlAlerts.Count, owner, repo);
                return codeqlAlerts;
            }

            // Fallback to Dependabot
            _logger.LogDebug("No CodeQL alerts, trying Dependabot for {Owner}/{Repo}", owner, repo);
            var dependabotAlerts = await GetDependabotAlertsAsync(client, owner, repo);

            if (dependabotAlerts.Any())
            {
                _logger.LogInformation("Found {Count} Dependabot alerts for {Owner}/{Repo}", dependabotAlerts.Count, owner, repo);
                return dependabotAlerts;
            }

            _logger.LogDebug("No security alerts found for {Owner}/{Repo}", owner, repo);
            return new List<Vulnerability>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch security alerts for {Owner}/{Repo}", owner, repo);
            return new List<Vulnerability>();
        }
    }

    /// <summary>
    /// Get CodeQL alerts for specific PR (filters by PR files).
    /// </summary>
    public async Task<List<Vulnerability>> GetCodeScanningAlertsForPRAsync(
        long installationId,
        string owner,
        string repo,
        int prNumber)
    {
        try
        {
            var client = await _github.GetInstallationClientAsync(installationId);

            // Get PR files to filter alerts
            var prFiles = await _github.GetPullRequestFilesAsync(installationId, owner, repo, prNumber);
            var prFilePaths = prFiles.Select(f => f.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Try CodeQL first
            var parameters = new Dictionary<string, string>
            {
                { "state", "open" },
                { "per_page", "100" }
            };

            var alerts = await client.Connection.Get<List<CodeScanningAlert>>(
                new Uri($"repos/{owner}/{repo}/code-scanning/alerts", UriKind.Relative),
                parameters,
                "application/vnd.github.v3+json"
            );

            if (alerts.Body != null && alerts.Body.Any())
            {
                // Filter alerts to only those in PR files
                var relevantAlerts = alerts.Body
                    .Where(a => a.MostRecentInstance?.Location?.Path != null &&
                               prFilePaths.Contains(a.MostRecentInstance.Location.Path))
                    .Select(a => new Vulnerability
                    {
                        Type = "CodeQL",
                        Severity = (a.Rule?.SecuritySeverityLevel ?? a.Rule?.Severity ?? "low").ToUpper(),
                        File = a.MostRecentInstance?.Location?.Path ?? "unknown",
                        Line = a.MostRecentInstance?.Location?.StartLine ?? 0,
                        Description = a.Rule?.Description ?? "Security alert"
                    })
                    .ToList();

                if (relevantAlerts.Any())
                {
                    _logger.LogInformation("Found {Count} CodeQL alerts in PR #{PRNumber} files", relevantAlerts.Count, prNumber);
                    return relevantAlerts;
                }
            }

            // Fallback to Dependabot
            _logger.LogDebug("No CodeQL alerts in PR files, checking Dependabot");
            var dependabotAlerts = await GetDependabotAlertsAsync(client, owner, repo);

            if (dependabotAlerts.Any())
            {
                _logger.LogInformation("Found {Count} Dependabot alerts (repo-wide)", dependabotAlerts.Count);
                return dependabotAlerts;
            }

            return new List<Vulnerability>();
        }
        catch (NotFoundException)
        {
            _logger.LogDebug("CodeQL not enabled for {Owner}/{Repo}, trying Dependabot", owner, repo);
            try
            {
                var client = await _github.GetInstallationClientAsync(installationId);
                return await GetDependabotAlertsAsync(client, owner, repo);
            }
            catch
            {
                return new List<Vulnerability>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch PR-specific alerts for {Owner}/{Repo} PR #{PRNumber}", owner, repo, prNumber);
            return new List<Vulnerability>();
        }
    }

    private async Task<List<Vulnerability>> GetCodeQLAlertsAsync(Octokit.IGitHubClient client, string owner, string repo, string refName)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "ref", refName },
                { "state", "open" },
                { "per_page", "100" }
            };

            var alerts = await client.Connection.Get<List<CodeScanningAlert>>(
                new Uri($"repos/{owner}/{repo}/code-scanning/alerts", UriKind.Relative),
                parameters,
                "application/vnd.github.v3+json"
            );

            if (alerts.Body == null || !alerts.Body.Any())
                return new List<Vulnerability>();

            return alerts.Body.Select(a => new Vulnerability
            {
                Type = "CodeQL",
                Severity = (a.Rule?.SecuritySeverityLevel ?? a.Rule?.Severity ?? "low").ToUpper(),
                File = a.MostRecentInstance?.Location?.Path ?? "unknown",
                Line = a.MostRecentInstance?.Location?.StartLine ?? 0,
                Description = a.Rule?.Description ?? "Security alert"
            }).ToList();
        }
        catch (NotFoundException)
        {
            _logger.LogDebug("CodeQL not available for {Owner}/{Repo}", owner, repo);
            return new List<Vulnerability>();
        }
    }

    private async Task<List<Vulnerability>> GetDependabotAlertsAsync(Octokit.IGitHubClient client, string owner, string repo)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "state", "open" },
                { "per_page", "50" }
            };

            var alerts = await client.Connection.Get<List<DependabotAlert>>(
                new Uri($"repos/{owner}/{repo}/dependabot/alerts", UriKind.Relative),
                parameters,
                "application/vnd.github.v3+json"
            );

            if (alerts.Body == null || !alerts.Body.Any())
                return new List<Vulnerability>();

            return alerts.Body.Select(a => new Vulnerability
            {
                Type = "Dependency",
                Severity = (a.SecurityAdvisory?.Severity ?? "medium").ToUpper(),
                File = a.DependencyManifestPath ?? "package manifest",
                Line = 0,
                Description = $"{a.SecurityAdvisory?.Summary ?? "Vulnerable dependency"} (Package: {a.DependencyPackageEcosystem}/{a.DependencyPackageName})"
            }).ToList();
        }
        catch (NotFoundException)
        {
            _logger.LogDebug("Dependabot alerts not available for {Owner}/{Repo}", owner, repo);
            return new List<Vulnerability>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Dependabot alerts for {Owner}/{Repo}", owner, repo);
            return new List<Vulnerability>();
        }
    }

    // Helper classes for JSON deserialization
    private class CodeScanningAlert
    {
        public int Number { get; set; }
        public string? State { get; set; }
        public AlertRule? Rule { get; set; }
        public AlertInstance? MostRecentInstance { get; set; }
    }

    private class AlertRule
    {
        public string? Id { get; set; }
        public string? Severity { get; set; }
        public string? SecuritySeverityLevel { get; set; }
        public string? Description { get; set; }
    }

    private class AlertInstance
    {
        public string? Ref { get; set; }
        public AlertLocation? Location { get; set; }
    }

    private class AlertLocation
    {
        public string? Path { get; set; }
        public int? StartLine { get; set; }
        public int? EndLine { get; set; }
    }

    // Dependabot alert models
    private class DependabotAlert
    {
        public int Number { get; set; }
        public string? State { get; set; }
        public string? DependencyPackageEcosystem { get; set; }
        public string? DependencyPackageName { get; set; }
        public string? DependencyManifestPath { get; set; }
        public SecurityAdvisory? SecurityAdvisory { get; set; }
    }

    private class SecurityAdvisory
    {
        public string? GhsaId { get; set; }
        public string? CveId { get; set; }
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public string? Severity { get; set; }
    }
}
