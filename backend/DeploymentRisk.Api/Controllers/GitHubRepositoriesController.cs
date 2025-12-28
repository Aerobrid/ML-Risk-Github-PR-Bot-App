using Microsoft.AspNetCore.Mvc;
using DeploymentRisk.Api.Services;
using Octokit;

namespace DeploymentRisk.Api.Controllers;

[ApiController]
[Route("api/github")]
public class GitHubRepositoriesController : ControllerBase
{
    private readonly GitHubClientService _github;
    private readonly IConfiguration _config;
    private readonly ILogger<GitHubRepositoriesController> _logger;

    public GitHubRepositoriesController(
        GitHubClientService github,
        IConfiguration config,
        ILogger<GitHubRepositoriesController> logger)
    {
        _github = github;
        _config = config;
        _logger = logger;
    }

    [HttpGet("repositories")]
    public async Task<IActionResult> GetInstalledRepositories()
    {
        try
        {
            var installationIds = new HashSet<long>();

            // 1. Try to get all installations dynamically
            try
            {
                var installations = await _github.GetAllInstallationsAsync();
                foreach (var install in installations)
                {
                    installationIds.Add(install.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch installations list dynamically. Falling back to configuration.");
            }

            // 2. Add manually configured InstallationId
            var configInstallationId = _config.GetValue<long>("GitHub:InstallationId");
            if (configInstallationId > 0)
            {
                installationIds.Add(configInstallationId);
            }

            var allRepos = new List<GitHubRepositoryInfo>();

            foreach (var installationId in installationIds)
            {
                try 
                {
                    var client = await _github.GetInstallationClientAsync(installationId);
                    
                    // Get repositories for this installation
                    var repos = await client.GitHubApps.Installation.GetAllRepositoriesForCurrent();

                    allRepos.AddRange(repos.Repositories.Select(r => new GitHubRepositoryInfo
                    {
                        Id = r.Id,
                        Name = r.Name,
                        FullName = r.FullName,
                        Owner = r.Owner.Login,
                        Private = r.Private,
                        InstallationId = installationId
                    }));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch repositories for installation {InstallationId}", installationId);
                    // Continue to next installation
                }
            }

            return Ok(allRepos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching installed repositories");
            return StatusCode(500, new { message = "Failed to fetch repositories" });
        }
    }

    [HttpGet("debug")]
    public async Task<IActionResult> Debug()
    {
        var appId = _config.GetValue<int>("GitHub:AppId");
        var privateKeyPath = _config["GitHub:PrivateKeyPath"] ?? string.Empty;
        var installationId = _config.GetValue<long>("GitHub:InstallationId");

        object? installationsResult = null;
        string? installationsError = null;

        try
        {
            var installs = await _github.GetAllInstallationsAsync();
            installationsResult = installs.Select(i => new { i.Id, i.Account?.Login, i.TargetId }).ToList();
        }
        catch (Exception ex)
        {
            installationsError = ex.Message + " -- " + ex.GetType().FullName;
        }

        return Ok(new
        {
            AppId = appId,
            PrivateKeyPath = privateKeyPath,
            InstallationId = installationId,
            Installations = installationsResult,
            InstallationsError = installationsError
        });
    }
}

public class GitHubRepositoryInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public bool Private { get; set; }
    public long InstallationId { get; set; }
    public int? AssessmentCount { get; set; }
    public DateTime? LastAssessment { get; set; }
}
