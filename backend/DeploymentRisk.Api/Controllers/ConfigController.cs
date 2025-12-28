using Microsoft.AspNetCore.Mvc;
using DeploymentRisk.Api.Repositories;

namespace DeploymentRisk.Api.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly IConfigRepository _configRepo;
    private readonly IConfiguration _appConfig;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(
        IConfigRepository configRepo,
        IConfiguration appConfig,
        ILogger<ConfigController> logger)
    {
        _configRepo = configRepo;
        _appConfig = appConfig;
        _logger = logger;
    }

    [HttpGet("database")]
    public async Task<IActionResult> GetDatabaseConfig()
    {
        try
        {
            var connectionString = await _configRepo.GetValueAsync("Database:ConnectionString")
                ?? _appConfig.GetValue<string>("Database:ConnectionString");

            return Ok(new
            {
                ConnectionString = connectionString,
                IsConfigured = !string.IsNullOrEmpty(connectionString)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching database config");
            return StatusCode(500, new { message = "Error fetching database config" });
        }
    }

    [HttpPost("database")]
    public async Task<IActionResult> UpdateDatabaseConfig([FromBody] DatabaseConfigRequest request)
    {
        try
        {
            await _configRepo.SetValueAsync("Database:ConnectionString", request.ConnectionString, "Database");
            _logger.LogInformation("Database configuration updated");

            return Ok(new { Message = "Configuration saved. Restart required to take effect." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating database config");
            return StatusCode(500, new { message = "Error updating database config" });
        }
    }

    // Risk scoring configuration is not user-configurable via the API.
    // Scorer enablement and weights are controlled by server configuration and code.
}

public record DatabaseConfigRequest(string ConnectionString);
 
