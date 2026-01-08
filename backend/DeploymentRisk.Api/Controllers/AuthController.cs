using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace DeploymentRisk.Api.Controllers;

// Tells ASP.NET Core that this is a web API controller
[ApiController]
// all endoints within controller is prefixed with api/auth
[Route("api/auth")]

// ControllerBase provides methods like Ok(), BadRequest(), StatusCode(), etc.
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;
    private readonly HttpClient _httpClient;

    public AuthController(
        IConfiguration config,
        ILogger<AuthController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    // recieve a body with GH code from front-end Oauth flow at endpoint: POST /api/auth/github/callback
    [HttpPost("github/callback")]
    public async Task<IActionResult> GitHubCallback([FromBody] GitHubCallbackRequest request)
    {
        try
        {   
            // validate code
            if (string.IsNullOrEmpty(request.Code))
            {
                return BadRequest(new { message = "Code is required" });
            }

            _logger.LogInformation("Processing GitHub OAuth callback with code: {Code}", request.Code.Substring(0, Math.Min(10, request.Code.Length)));

            // Exchange code for access token
            var tokenResponse = await ExchangeCodeForToken(request.Code, request.RedirectUri);

            if (tokenResponse == null)
            {
                _logger.LogWarning("Failed to exchange code for token");
                return Unauthorized(new { message = "Failed to exchange code for token. Please check your GitHub OAuth app configuration." });
            }

            // Get user information from GitHub
            var userInfo = await GetGitHubUser(tokenResponse.AccessToken);

            if (userInfo == null)
            {
                _logger.LogWarning("Failed to get user information from GitHub");
                return Unauthorized(new { message = "Failed to get user information from GitHub" });
            }

            _logger.LogInformation("Successfully authenticated user: {Login}", userInfo.Login);

            // Generate JWT token
            var jwtToken = GenerateJwtToken(userInfo);

            return Ok(new AuthResponse
            {
                Token = jwtToken,
                User = userInfo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GitHub callback: {Message}", ex.Message);
            return StatusCode(500, new { message = $"Authentication failed: {ex.Message}" });
        }
    }
    
    // 1. Sends a POST request to https://github.com/login/oauth/access_token with: client_id, client_secret, code, redirect_uri
    // 2. Github responds with an access token
    // 3. Deserializes response into a `GitHubTokenResponse`
    private async Task<GitHubTokenResponse?> ExchangeCodeForToken(string code, string? redirectUri = null)
    {
        var clientId = _config["GitHub:ClientId"];
        var clientSecret = _config["GitHub:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            _logger.LogError("GitHub ClientId or ClientSecret is not configured");
            return null;
        }

        // Default redirect URI if not provided
        if (string.IsNullOrEmpty(redirectUri))
        {
            redirectUri = "http://localhost:4200/auth/callback";
        }

        var requestBody = new
        {
            client_id = clientId,
            client_secret = clientSecret,
            code = code,
            redirect_uri = redirectUri
        };

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var response = await _httpClient.PostAsJsonAsync(
            "https://github.com/login/oauth/access_token",
            requestBody
        );

        var content = await response.Content.ReadAsStringAsync();

        // status code not 2xx
        // NOTE: test this feature
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GitHub token exchange failed with status {StatusCode}: {Content}", response.StatusCode, content);
            return null;
        }

        // GitHub returns JSON (not form-encoded) when Accept: application/json is set
        var tokenResponse = JsonSerializer.Deserialize<GitHubTokenResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            _logger.LogError("Failed to parse access token from GitHub response: {Content}", content);
            return null;
        }

        return tokenResponse;
    }

    // 1. Method calls GitHub API: GET https://api.github.com/user using the access token
    // 2. Returns a GitHubUser object containing: Id, Login, Name, Email, AvatarUrl
    private async Task<GitHubUser?> GetGitHubUser(string accessToken)
    {
        // add necessary http request headers
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DeploymentRiskPlatform");

        // send
        var response = await _httpClient.GetAsync("https://api.github.com/user");

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize<GitHubUser>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return user;
    }

    // uses `JwtSecurityToken` to create a signed token for your API
    // signed token is what the frontend will store and send for authenticated API calls
    private string GenerateJwtToken(GitHubUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _config["Jwt:SecretKey"] ?? "your-super-secret-key-min-32-chars-long-for-security"));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // add claims
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Login),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim("avatar_url", user.AvatarUrl ?? "")
        };
        
        // build signed token
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "DeploymentRiskPlatform",
            audience: _config["Jwt:Audience"] ?? "DeploymentRiskPlatform",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// request body from the frontend
public record GitHubCallbackRequest(string Code, string? RedirectUri = null);

// helper classes

// response from GitHub with the access token
public class GitHubTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }
    
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

// github user info
public class GitHubUser
{
    public long Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
}

// the API response returned to client
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public GitHubUser User { get; set; } = new();
}
