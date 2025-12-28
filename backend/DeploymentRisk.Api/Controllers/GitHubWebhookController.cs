using System.Security.Cryptography;
using System.Text;
using DeploymentRisk.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeploymentRisk.Api.Controllers;

[ApiController]
[Route("api/github/webhook")]
public class GitHubWebhookController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<GitHubWebhookController> _logger;
    private readonly WebhookProcessor _processor;

    public GitHubWebhookController(
        IConfiguration config,
        ILogger<GitHubWebhookController> logger,
        WebhookProcessor processor)
    {
        _config = config;
        _logger = logger;
        _processor = processor;
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> HandleWebhook()
    {
        try
        {
            _logger.LogInformation("Webhook received - Method: {Method}, Path: {Path}", Request.Method, Request.Path);

            // Read the raw body
            Request.EnableBuffering();
            Request.Body.Position = 0;
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            _logger.LogInformation("Webhook body length: {Length}", body.Length);

            // Verify webhook signature (allow missing signature for testing)
            var signature = Request.Headers["X-Hub-Signature-256"].ToString();
            var eventType = Request.Headers["X-GitHub-Event"].ToString();

            _logger.LogInformation("Webhook headers - Event: {Event}, Signature present: {HasSignature}", 
                eventType, !string.IsNullOrEmpty(signature));

            // In development, allow webhooks without signature for testing
            var isDevelopment = _config["ASPNETCORE_ENVIRONMENT"] == "Development";
            var webhookSecret = _config["GitHub:WebhookSecret"];

            if (!string.IsNullOrEmpty(signature) && !string.IsNullOrEmpty(webhookSecret) && webhookSecret != "your-webhook-secret-here")
            {
                if (!VerifySignature(body, signature))
                {
                    _logger.LogWarning("Webhook signature verification failed");
                    return Unauthorized(new { message = "Invalid signature" });
                }
                _logger.LogInformation("Webhook signature verified successfully");
            }
            else if (isDevelopment || string.IsNullOrEmpty(webhookSecret) || webhookSecret == "your-webhook-secret-here")
            {
                _logger.LogWarning("Webhook signature verification skipped - Development mode or secret not configured");
            }
            else
            {
                _logger.LogWarning("Webhook signature missing in production");
                return Unauthorized(new { message = "Missing webhook signature" });
            }

            // Allow testing without event type header in development
            if (string.IsNullOrEmpty(eventType))
            {
                if (isDevelopment)
                {
                    _logger.LogWarning("GitHub event type header missing - using 'test' for development");
                    eventType = "test";
                }
                else
                {
                    _logger.LogWarning("GitHub event type header missing");
                    return BadRequest(new { message = "Missing X-GitHub-Event header" });
                }
            }

            _logger.LogInformation("Received GitHub webhook: {EventType}", eventType);

            // Queue for background processing
            await _processor.QueueWebhookAsync(eventType, body);

            return Ok(new { message = "Webhook received" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling webhook");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    private bool VerifySignature(string payload, string signature)
    {
        var secret = _config["GitHub:WebhookSecret"];
        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning("GitHub webhook secret not configured");
            return false;
        }

        var hash = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hash.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = "sha256=" + BitConverter.ToString(computedHash)
            .Replace("-", "").ToLowerInvariant();

        return signature == computedSignature;
    }
}
