using Octokit;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace DeploymentRisk.Api.Services;

public class GitHubClientService
{
    private readonly IConfiguration _config;
    private readonly ILogger<GitHubClientService> _logger;

    public GitHubClientService(IConfiguration config, ILogger<GitHubClientService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private async Task<GitHubClient> GetAppClientAsync()
    {
        var appId = _config.GetValue<int>("GitHub:AppId");
        var privateKeyPath = _config["GitHub:PrivateKeyPath"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(privateKeyPath))
        {
            throw new InvalidOperationException("GitHub:PrivateKeyPath not configured. Set environment variable GitHub__PrivateKeyPath or update appsettings.");
        }

        // Try several candidate locations to be tolerant in development (absolute, repo-relative, secrets folder)
        var candidates = new List<string>
        {
            privateKeyPath,
            Path.GetFullPath(privateKeyPath),
            Path.Combine(Directory.GetCurrentDirectory(), privateKeyPath),
            Path.Combine(AppContext.BaseDirectory, privateKeyPath),
            Path.Combine(Directory.GetCurrentDirectory(), "secrets", Path.GetFileName(privateKeyPath)),
            Path.Combine(AppContext.BaseDirectory, "secrets", Path.GetFileName(privateKeyPath)),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "secrets", Path.GetFileName(privateKeyPath))
        };

        string? resolved = null;
        foreach (var c in candidates.Distinct())
        {
            try
            {
                var p = string.IsNullOrEmpty(c) ? c : Path.GetFullPath(c!);
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                {
                    resolved = p;
                    break;
                }
            }
            catch { /* ignore invalid candidate paths */ }
        }

        if (resolved == null)
        {
            _logger.LogError("GitHub App private key not found. Tried paths: {Candidates}", string.Join(";", candidates));
            throw new FileNotFoundException($"GitHub App private key not found at '{privateKeyPath}' (tried candidates). Set GitHub__PrivateKeyPath to the PEM file path.");
        }

        var privateKeyPem = await File.ReadAllTextAsync(resolved);
        
        // Create RSA object without 'using' so it lives as long as the RsaSecurityKey needs it
        var rsa = RSA.Create();
        try 
        {
            rsa.ImportFromPem(privateKeyPem);
        }
        catch (ArgumentException)
        {
             _logger.LogWarning("Standard ImportFromPem failed, attempting manual cleanup...");
             throw; 
        }

        var securityKey = new RsaSecurityKey(rsa);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = appId.ToString(),
            IssuedAt = DateTime.UtcNow.AddSeconds(-60), 
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        var jwt = handler.WriteToken(token);

        return new GitHubClient(new ProductHeaderValue("deployment-risk-bot"))
        {
            Credentials = new Credentials(jwt, AuthenticationType.Bearer)
        };
    }

    public async Task<IReadOnlyList<Installation>> GetAllInstallationsAsync()
    {
        var appClient = await GetAppClientAsync();
        return await appClient.GitHubApps.GetAllInstallationsForCurrent();
    }

    public async Task<IGitHubClient> GetInstallationClientAsync(long installationId)
    {
        var appClient = await GetAppClientAsync();
        var response = await appClient.GitHubApps.CreateInstallationToken(installationId);

        return new GitHubClient(new ProductHeaderValue("deployment-risk-bot"))
        {
            Credentials = new Credentials(response.Token)
        };
    }

    public async Task<PullRequest> GetPullRequestAsync(long installationId, string owner, string repo, int prNumber)
    {
        var client = await GetInstallationClientAsync(installationId);
        return await client.PullRequest.Get(owner, repo, prNumber);
    }

    public async Task<IssueComment> PostCommentAsync(long installationId, string owner, string repo, int issueNumber, string body)
    {
        var client = await GetInstallationClientAsync(installationId);
        try
        {
            var comment = await client.Issue.Comment.Create(owner, repo, issueNumber, body);
            _logger.LogInformation("Posted comment to {Owner}/{Repo}#{Issue} (id={Id})", owner, repo, issueNumber, comment.Id);
            return comment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post comment to {Owner}/{Repo}#{Issue} (installation={InstallationId}) - body length: {Length}", owner, repo, issueNumber, installationId, body?.Length ?? 0);
            throw;
        }
    }

    public async Task<Repository> GetRepositoryAsync(long installationId, string owner, string repo)
    {
        var client = await GetInstallationClientAsync(installationId);
        return await client.Repository.Get(owner, repo);
    }

    public async Task<IReadOnlyList<PullRequestFile>> GetPullRequestFilesAsync(long installationId, string owner, string repo, int prNumber)
    {
        var client = await GetInstallationClientAsync(installationId);
        return await client.PullRequest.Files(owner, repo, prNumber);
    }
}