// for thread-safe producer/consumer pattern (allows async queue)
using System.Threading.Channels;
// import models code
using DeploymentRisk.Api.Models;

namespace DeploymentRisk.Api.Services;

// basically processes webhooks as a background worker using a thread-safe async queue
public class WebhookProcessor : BackgroundService
{
    // async queue
    private readonly Channel<WebhookEvent> _queue;
    // used to create scoped services like DB connections safely
    private readonly IServiceScopeFactory _scopeFactory;
    // logs info, errors, etc.
    private readonly ILogger<WebhookProcessor> _logger;

    // create an unbounded queue
    public WebhookProcessor(IServiceScopeFactory scopeFactory, ILogger<WebhookProcessor> logger)
    {
        _queue = Channel.CreateUnbounded<WebhookEvent>();
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // to add webhook events to the queue
    public async Task QueueWebhookAsync(string eventType, string payload)
    {   
        // multiple callers can write concurrently, so each thread adds its own event to queue safely
        // If the writer has been completed/closed this will return false and we avoid throwing
        if (!_queue.Writer.TryWrite(new WebhookEvent
        {
            EventType = eventType,
            Payload = payload,
            ReceivedAt = DateTime.UtcNow
        }))
        {
            // Writer closed or unable to accept item (should be rare for unbounded), log and return
            _logger.LogWarning("Failed to queue webhook event (writer closed): {EventType}", eventType);
            return;
        }

        _logger.LogInformation("Queued webhook event: {EventType}", eventType);
    }

    // runs in background until stopping token in parameter triggered
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook processor started");
        // Use a top-level try/catch to handle cancellation and unexpected errors
        try
        {
            // ReadAllAsync will iterate until the channel is completed or the stopping token is cancelled
            await foreach (var webhook in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                // Create a scope per message so scoped services (DbContext, etc.) are safe to resolve
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<WebhookHandler>();

                // Process each webhook
                // Individual processing errors are logged BUT DO NOT stop the loop
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
        catch (OperationCanceledException)
        {
            // Normal shutdown path when the stopping token is triggered
            _logger.LogInformation("Webhook processor stopping due to cancellation.");
        }
        catch (Exception ex)
        {
            // Unexpected top-level exception — log it and allow service to stop
            _logger.LogError(ex, "Unexpected error in webhook processing loop");
        }
    }
}
