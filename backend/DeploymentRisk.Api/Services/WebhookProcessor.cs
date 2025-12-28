using System.Threading.Channels;
using DeploymentRisk.Api.Models;

namespace DeploymentRisk.Api.Services;

public class WebhookProcessor : BackgroundService
{
    private readonly Channel<WebhookEvent> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookProcessor> _logger;

    public WebhookProcessor(IServiceScopeFactory scopeFactory, ILogger<WebhookProcessor> logger)
    {
        _queue = Channel.CreateUnbounded<WebhookEvent>();
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task QueueWebhookAsync(string eventType, string payload)
    {
        await _queue.Writer.WriteAsync(new WebhookEvent
        {
            EventType = eventType,
            Payload = payload,
            ReceivedAt = DateTime.UtcNow
        });

        _logger.LogInformation("Queued webhook event: {EventType}", eventType);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook processor started");

        await foreach (var webhook in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            using var scope = _scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<WebhookHandler>();

            try
            {
                await handler.ProcessAsync(webhook);
                _logger.LogInformation("Successfully processed webhook: {EventType}", webhook.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook {EventType}", webhook.EventType);
            }
        }
    }
}
